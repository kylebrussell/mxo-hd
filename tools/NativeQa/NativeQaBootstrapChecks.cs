using HeadlessClient;
using System.Text.Json;

namespace NativeQa;

/// <summary>Pre-client integration tests. No client lock, native launch or main-database reset.</summary>
public static class NativeQaBootstrapChecks
{
    public static bool HasCustomHook(NativeQaMode mode) =>
        mode.GetType().GetMethod(nameof(NativeQaMode.AfterStartAsync))!.DeclaringType != typeof(NativeQaMode);

    public static IReadOnlyDictionary<string,string> IsolatedEnvironment(string mode) =>
        NativeQaFixturePlan.EnvironmentFor(mode).Where(p => p.Key is not
            ("MXO_AUTH_PORT" or "MXO_MARGIN_PORT" or "MXO_WORLD_PORT" or "MXO_CONSOLE_PORT"))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    public static async Task RunAsync(string root, string output, IReadOnlyList<string> names)
    {
        if (names.Count == 0 || names.Distinct(StringComparer.Ordinal).Count() != names.Count ||
            names.Any(n => !NativeQaModes.Names.Contains(n, StringComparer.Ordinal)))
            throw new ArgumentException("Bootstrap checks require distinct discovered mode names.");
        root = Path.GetFullPath(root); output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        // Share the baseline for no-op hooks whose actual seed does not need its own check.
        var modes = names.Select(NativeQaModes.Find).ToArray();
        var inherited = modes.Where(mode => !HasCustomHook(mode) && !mode.Plan.LoadAbilityAncestors)
            .Select(mode => mode.Name).ToArray();
        var separate = names.Except(inherited, StringComparer.Ordinal);
        var groups = new List<string[]>();
        if (inherited.Length != 0) groups.Add(inherited);
        groups.AddRange(separate.Select(n => new[] { n }));
        var results = new List<object>();
        try
        {
            foreach (var group in groups)
                await RunGroupAsync(root, output, group, results);
            await WriteAsync(Path.Combine(output, "summary.json"), new
            {
                Passed = true, Modes = names, ModeCount = names.Count, ServerCount = groups.Count,
                CustomHookCount = modes.Count(HasCustomHook), SharedNoOpHookCount = inherited.Length,
                Results = results, At = DateTime.UtcNow
            });
        }
        catch (Exception error)
        {
            await WriteAsync(Path.Combine(output, "summary.json"), new
            {
                Passed = false, Modes = names, Results = results, Error = error.ToString(), At = DateTime.UtcNow
            });
            throw;
        }
    }

    private static async Task RunGroupAsync(string root, string output, string[] names, List<object> results)
    {
        string seedMode = names.Length == 1 ? names[0] : "";
        var mode = NativeQaModes.Find(seedMode);
        var plan = mode.Plan;
        byte[]? characterUi = mode is Modes.AbilitySweepMode
            ? AbilitySweepHotbar.FromEnvironment(plan.Abilities, required: false)
            : null;
        using var database = await ThrowawayDatabase.CreateAsync(root, "bootstrap");
        await WriteAsync(Path.Combine(output, database.Name + ".json"), new { Database = database.Name, Modes = names, At = DateTime.UtcNow });
        var seeded = await NativeQaFixtureSeeder.PrepareAsync(root, database, plan);
        using var server = await ServerInstance.StartAsync(root, database.Name, IsolatedEnvironment(seedMode), plan.MissionFixture,
            testMobFixture: plan.TestMobFixture == null ? null : Path.Combine(root, "tools", "NativeQa", "Fixtures", plan.TestMobFixture),
            testCharacterUi: characterUi);
        string sourceAssembly = server.SourceAssembly, sourceHash = server.SourceAssemblySha256;
        string directory = Path.Combine(output, "server-" + server.ProcessId);
        Directory.CreateDirectory(directory);
        Console.WriteLine($"BOOTSTRAP SERVER {server.ProcessId} {database.Name} modes={names.Length}");
        // Authenticate and enter through the actual server even when every inherited hook is empty.
        HeadlessPlayer? canary = null;
        HeadlessPlayer? probe = null;
        try
        {
            if (characterUi != null || plan.LoadAbilityAncestors)
            {
                probe = await HeadlessPlayer.LoginAsync(server.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
                Check.Equal(110u, probe.CharId, "Ability-sweep UI seed is delivered only to the owned CSR");
                if (characterUi != null)
                    Check.That(probe.CharacterData.TryGetValue(hds.QaCharacterUi.DataType, out var received) &&
                        received.AsSpan().SequenceEqual(characterUi), "Stock Margin UI chunks reconstruct the seeded profile byte-for-byte");
                if (plan.LoadAbilityAncestors)
                {
                    var loadout = AbilityMemoryUiFixture.Decode(probe.CharacterData[6]);
                    foreach (var (id, slot) in seeded.Abilities.Distinct().Concat(seeded.LoadedAncestors).Select((id, slot) => (id, slot)))
                        Check.That(loadout.Any(row => row.Slot == slot && row.Level > 0 && row.Descriptor == (0x80000000u | ((uint)id << 10))),
                            $"Stock Margin has loaded fixture ability or prerequisite {id} at slot {slot}");
                }
                if (plan.EnvironmentOverrides.GetValueOrDefault(hds.CombatFixtureWeapons.WeaponMode) == "1")
                    await CombatFixtureLoadout.VerifyWeaponsAsync(probe, database);
                if (mode is Modes.GunStyleChoreographyMode gunStyle)
                    await CombatFixtureLoadout.VerifyGunChoreographyAsync(probe, server, gunStyle);
                if (mode is Modes.AbilityParityMartialSweepMode martialSweep)
                    await martialSweep.VerifyHeadlessAsync(probe, server);
                if (mode is Modes.AbilityParityOperativeSweepMode operativeSweep)
                    await operativeSweep.VerifyHeadlessAsync(probe, server);
                if (mode is Modes.AbilitySweepMartialArtistMode)
                    await Modes.AbilitySweepMartialArtistMode.VerifyBatchTwoAsync(probe, server);
                if (mode is Modes.AbilityParityOperativeSweep2Mode operativeSweep2)
                    await operativeSweep2.VerifyHeadlessAsync(probe);
                if (seedMode == "style-choreography")
                {
                    await CombatFixtureLoadout.VerifyWeaponSwapAsync(probe, database, "rifle", hds.CombatFixtureWeapons.Rifle);
                    await CombatFixtureLoadout.VerifyFireAfterSwapAsync(probe, server, hds.CombatFixtureWeapons.Rifle);
                    probe.SendRpc("80 fe 00");
                    await probe.WaitForRpcAsync("80 fd");
                    probe.Dispose();
                    // Allow the normal world/margin character reservation to expire after logout.
                    await Task.Delay(TimeSpan.FromSeconds(33));
                    probe = await HeadlessPlayer.LoginAsync(server.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
                    await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(probe, hds.CombatFixtureWeapons.Rifle);
                    await CombatFixtureLoadout.VerifyWeaponSwapAsync(probe, database, "handgun", hds.CombatFixtureWeapons.Handgun);
                }
            }
            if (names.Length > 1 || !HasCustomHook(NativeQaModes.Find(names[0])))
            {
                canary = await HeadlessPlayer.LoginAsync(server.Address, "QAFriend", "QAFriend");
                await canary.EnterWorldAsync();
            }
            foreach (string name in names)
            {
                var checkedMode = NativeQaModes.Find(name);
                string modeDirectory = Path.Combine(directory, name.Length == 0 ? "default" : name);
                Directory.CreateDirectory(modeDirectory);
                var helpers = new Dictionary<string,HeadlessPlayer>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    Console.WriteLine("BOOTSTRAP START " + (name.Length == 0 ? "(default)" : name));
                    byte[] ack = await NativeQaReadiness.BootstrapAsync(checkedMode, new NativeQaBootstrapContext(modeDirectory,
                        NativeQaFixtureSeeder.HelperNames, helpers, server.Address, database, server, root, probe));
                    var modePlan = checkedMode.Plan;
                    if (name == "mission-catalog-sample" && modePlan.ExtraQaMetadata.ContainsKey("CheckpointArea"))
                        await CheckPhaseLoginAsync(database, server, modePlan);
                    var standard = new NativeQaFixtureState
                    {
                        Database = database.Name, Directory = server.WorkingDirectory, Account = "CSR_AbilityQA",
                        Character = "CSR_Tester", CharId = 110, BuildCommit = "bootstrap-check",
                        Configuration = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(sourceAssembly))) ?? "unknown",
                        Status = "BootstrapChecked", Abilities = seeded.Abilities,
                        SourceAssembly = sourceAssembly, SourceAssemblySha256 = sourceHash,
                        OwnedUnloadedDisciplineAncestors = seeded.DisciplineAncestors,
                        LoadedAbilityAncestors = seeded.LoadedAncestors,
                        StealthShieldQa = plan.StealthShieldQa, ShieldReady = ack.Length > 0,
                        CasterOrganization = plan.StealthShieldQa ? 1 : null,
                        CasterPvpFlag = plan.StealthShieldQa, HealthCallbackQa = plan.HealthCallbackQa,
                        CompileItemsQa = plan.CompileItemsQa,
                        CallbackRoll15 = plan.HealthCallbackQa ? 0 : null,
                        CallbackRoll100 = plan.HealthCallbackQa ? 0 : null
                    };
                    // Every mode's declared metadata is serialized against the executor's exact
                    // standard shape, including inherited no-op modes. This catches key collisions.
                    await WriteAsync(Path.Combine(modeDirectory, "state-schema-check.json"), modePlan.StateWithMetadata(standard));
                    // This separate state describes the ACTUAL seeded server, including a shared baseline.
                    await WriteAsync(Path.Combine(modeDirectory, "state.json"), plan.StateWithMetadata(standard));
                    string? equipped = null;
                    object? actionProof = null;
                    if (name.StartsWith("mobius-free-attack-", StringComparison.Ordinal))
                    {
                        equipped = await database.ScalarAsync("SELECT CONCAT("
                            + "(SELECT COUNT(*) FROM inventory WHERE charId=6114 AND slot=105 AND goid=692 AND count=1),':',"
                            + "(SELECT COUNT(*) FROM inventory WHERE charId=6114 AND slot=1))");
                        if (equipped != "1:0") throw new InvalidOperationException("QAEnemy rifle did not equip: " + equipped);
                        actionProof = await Modes.MobiusCodeFixtureMode.VerifyInterlockAsync(server, probe!, helpers["QAEnemy"],
                            helpers["QAOther"], name.EndsWith("-positive", StringComparison.Ordinal));
                    }
                    if (name == "ability-health-parity")
                        actionProof = await Modes.AbilityHealthParityMode.VerifyRejuvenateAsync(probe!, helpers["QAFriend"]);
                    if (name == "combat-foundations")
                        actionProof = await Modes.CombatFoundationsMode.VerifyLogicBarrageAsync(server, probe!);
                    if (name == "ability-sweep-coder")
                        actionProof = await CoderSweepRetest.VerifyAsync(server, probe!);
                    var problems = helpers.Values.SelectMany(p => p.World.Problems).Concat(canary?.World.Problems ?? Array.Empty<string>()).ToArray();
                    if (problems.Length != 0) throw new InvalidOperationException(string.Join("; ", problems));
                    var result = new { Mode = name, BootstrapPassed = true, Database = database.Name, ServerPid = server.ProcessId,
                        Address = server.Address, CustomHook = HasCustomHook(checkedMode), SharedNoOpFixture = names.Length > 1,
                        HotbarBytesVerified = characterUi?.Length,
                        LoadedAbilityAncestors = seeded.LoadedAncestors,
                        DeclaredModeAbilities = modePlan.Abilities, SeededAbilities = seeded.Abilities,
                        HelperCount = helpers.Count, EquippedRifleSlotCounts = equipped, FixtureAction = actionProof,
                        StateSerialized = true, At = DateTime.UtcNow };
                    results.Add(result);
                    await WriteAsync(Path.Combine(modeDirectory, "result.json"), result);
                    Console.WriteLine("BOOTSTRAP PASS " + (name.Length == 0 ? "(default)" : name));
                }
                catch (Exception error)
                {
                    await WriteAsync(Path.Combine(modeDirectory, "failure.json"), new { Mode = name, Error = error.ToString(), At = DateTime.UtcNow });
                    throw;
                }
                finally
                {
                    try
                    {
                        foreach (var pair in helpers)
                        {
                            await WriteAsync(Path.Combine(modeDirectory, pair.Key + "-wire.json"), new
                            {
                                pair.Value.CharId,
                                Rpcs = pair.Value.World.RpcSnapshot().Select(r => Convert.ToHexString(r.Bytes)).ToArray(),
                                Views = pair.Value.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),
                                Problems = pair.Value.World.Problems.ToArray()
                            });
                        }
                    }
                    finally { foreach (var player in helpers.Values) player.Dispose(); }
                }
            }
        }
        finally
        {
            probe?.Dispose();
            canary?.Dispose();
            await File.WriteAllTextAsync(Path.Combine(directory, "server-output.txt"), server.FullOutput());
            foreach (string file in new[] { "DebugLog.txt", "PacketLog.txt" })
                if (File.Exists(Path.Combine(server.WorkingDirectory, file)))
                    File.Copy(Path.Combine(server.WorkingDirectory, file), Path.Combine(directory, file), true);
            await WriteAsync(Path.Combine(directory, "owned-server.json"), new { Database = database.Name, Pid = server.ProcessId,
                server.WorkingDirectory, server.Address, SourceAssembly = sourceAssembly, SourceAssemblySha256 = sourceHash,
                SeedMode = seedMode, Modes = names, At = DateTime.UtcNow });
        }
    }

    private static async Task CheckPhaseLoginAsync(ThrowawayDatabase database, ServerInstance server, NativeQaModePlan plan)
    {
        const string payload = "SELECT HEX(payload) FROM char_active_missions WHERE charId=110";
        var before = MervFollowupCheckpointFixture.ParseHex(await database.ScalarAsync(payload))
            ?? throw new ScenarioFailure("Missing phase-start checkpoint before the login probe");
        using var player = await HeadlessPlayer.LoginAsync(server.Address, "CSR_AbilityQA", "CSR_Tester");
        await player.EnterWorldAsync();
        await player.WaitForRpcAsync("80 9c");
        var after = MervFollowupCheckpointFixture.ParseHex(await database.ScalarAsync(payload));
        Check.That(after?.Identity == before.Identity && after?.Mission == plan.ExtraQaMetadata["CheckpointMission"].GetInt32() &&
            after.Phase == plan.ExtraQaMetadata["CheckpointArea"].GetInt32() - 1 && before.Done.SequenceEqual(after.Done) &&
            before.Following.Order().SequenceEqual(after.Following.Order()), "Ordinary login restores the staged mission and its followers");
        Check.Equal(0, player.World.Problems.Count, "Phase login has no wire errors");
    }

    private static Task WriteAsync(string path, object value) => File.WriteAllTextAsync(path,
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
