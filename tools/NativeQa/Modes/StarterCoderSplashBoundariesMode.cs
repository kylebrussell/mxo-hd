using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterCoderSplashBatchMode;

namespace NativeQa.Modes;

public sealed class StarterCoderSplashBoundariesMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, PrimaryZ = 6400;
    private const int Z = PrimaryZ + 300, LegalZ = PrimaryZ + 1600, OutsideZ = PrimaryZ + 1700;
    private const ushort Style = 1008, Lockdown = 21;
    private static readonly ushort[] Abilities = Samples.Select(sample => sample.Id).Prepend(Style).Append(Lockdown).ToArray();
    private static readonly ushort[] Loadout = Abilities.Concat(new ushort[] { 2, 17 })
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z, int Alignment)[] Helpers =
    {
        ("QAEnemy", 6114, X, PrimaryZ, 2),
        ("QAOther", 6113, X + 200, PrimaryZ, 2),
        ("QAFar", 6112, X - 201, PrimaryZ, 2),
        ("QAFriend", 6111, X - 100, PrimaryZ + 50, 1)
    };

    public override string Name => "ability-parity-starter-coder-splash-boundaries";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X, Z, X, PrimaryZ)} WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},innerStrM=300,innerStrC=300 WHERE charId IN (110,6111,6112,6113,6114)",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)"
        }.Concat(Helpers.Select(stand => $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Alignment} WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: Abilities, testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0", [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "21,33,36,514", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAEnemy 3 m ahead ({X},{Y},{PrimaryZ}). Verified outdoor NAV floor 400 for all stands. QAOther ({X + 200},{Y},{PrimaryZ}) is a hostile exactly 2 m east of QAEnemy. QAFar ({X - 201},{Y},{PrimaryZ}) is a hostile 2.01 m west. QAFriend ({X - 100},{Y},{PrimaryZ + 50}) is friendly, inside the splash.",
            $"Select Coder Style in page 2 slot 1 and /target QAEnemy. Use the OUTSIDE teleport printed by the fixture, reselect QAEnemy if needed, and try slots 2–5. At 17 m either stock suppresses them or the server refuses out of range. No cast bar, IS debit, reuse or [PVP] hit.",
            "Use the EDGE teleport printed by the fixture, then /target QAEnemy. At 16 m, cast Static Blast (slot 2), Tinkering (3), then Advanced Tinkering (4) after each lands. If the stock UI suppresses a cast right at 16 m, step slightly forward while facing QAEnemy, reselect and report that before retrying.",
            "Expect [PVP] 33 hits QAEnemy for 24, then 36 for 4 and 514 for 6. At 2 m from QAEnemy, QAOther gets 4/6 from the Tinkering casts only. QAFar and QAFriend receive nothing. [AREA-QA] separates radius and NotOpponent. Cast/reuse 2/6, 6/30 and 6/30 s. IS spends 15/20/40. Then cast slot 5 Lockdown. It costs 10 IS, casts for 2 s, gives [MODIFIER] char=6114 source=21 state 99 for 12 s and starts 13.5 s own reuse. No interlock. Only the existing disposable viral draw is fixed."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var stand in Helpers.Select(helper => (helper.Name, helper.X, helper.Z))
            .Concat(new (string Name, int X, int Z)[] { ("CSR", X, Z), ("legal cast", X, LegalZ), ("outside range", X, OutsideZ) }))
        {
            Check.That(NavMesh.Locate(1, stand.X, Y, stand.Z, out var floor) && !floor.indoors &&
                Math.Abs(floor.floorY + 95 - Y) < 1, $"{stand.Name} stands on verified outdoor NAV");
            Check.That(NavMesh.HasSightPath(1, stand.X, Y, stand.Z, X, Y, PrimaryZ),
                $"{stand.Name} has a clear same-floor line to QAEnemy");
        }

        var outside = Teleport(OutsideZ);
        var edge = Teleport(LegalZ);
        Console.WriteLine($"CODER SPLASH OUTSIDE (17 m): {outside.Command}");
        Console.WriteLine($"CODER SPLASH EDGE (16 m): {edge.Command}");

        var server = context.ActualServer!;
        foreach (var stand in Helpers)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var profile = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Loadout.All(id => profile.ContainsKey(id) && AbilityPrerequisites.Current.Closure(id).All(need =>
                profile.TryGetValue(need.Key, out var rank) && rank >= need.Value)), "Complete ranked prerequisites fit level 19");
        foreach (var sample in Samples)
        {
            var source = AbilityCatalog.Find(sample.Id);
            Check.Equal(16d, source.Number("Ability/SelectionRange"), "The stand uses the shipped selection range");
            Check.Equal(sample.Splash ? 2d : 0d, source.Number("Ability/RadiusOfEffect"), "The splash edge uses the shipped radius");
        }

        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var primary = context.Helpers["QAEnemy"];
        var target = await caster.WaitForViewNamedAsync(primary.Handle);
        foreach (var helper in context.Helpers.Values)
            await caster.WaitForViewNamedAsync(helper.Handle);
        caster.SendRpc(StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        await MoveAsync(outside);
        foreach (var sample in Samples)
            await StarterHackerControlTargetMode.RefusedAsync(caster, sample.Id,
                StarterHackerControlTargetMode.WithHandle(sample.Request, handle), AbilityRefusals.TargetOutOfRange);
        await StarterHackerControlTargetMode.RefusedAsync(caster, Lockdown,
            StarterHackerControlTargetMode.WithHandle(StockLockdown, handle), AbilityRefusals.TargetOutOfRange);
        await MoveAsync(edge);

        foreach (var sample in Samples)
        {
            var cursors = context.Helpers.Values.ToDictionary(player => player, player => player.World.RpcSnapshot().Count);
            await StarterFixedDamageMode.Land(caster, primary, target, sample.Id, sample.Damage, sample.Cast, sample.Reuse, sample.Request);
            foreach (var (helper, cursor) in cursors.Where(pair => pair.Key != primary))
            {
                byte[] damage = AbilityMessages.Damage(false, sample.Id, caster.Handle, helper.Handle, sample.Damage, 0);
                if (sample.Splash && helper.Handle == "QAOther")
                {
                    await StockAbilityProfile.ObserveAsync(helper, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(damage),
                        $"{sample.Id} includes the secondary at exactly two metres");
                    continue;
                }

                helper.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(helper, cursor, frame => frame.Bytes is [0x81, 0x49, ..],
                    "The excluded player's stream passed the damage boundary");
                Check.That(!helper.World.RpcSnapshot().Skip(cursor).Any(frame => frame.Bytes.AsSpan().StartsWith(damage.AsSpan(0, damage.Length - 8))),
                    $"{sample.Id} excludes {helper.Handle}");
            }
        }
        int rootAt = primary.World.RpcSnapshot().Count;
        int castAt = caster.World.RpcSnapshot().Count;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(StockLockdown, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Lockdown, 2);
        await EvidenceRetestWire.TimerAsync(caster, castAt, Lockdown, AbilityCooldowns.OwnTimer, 13.5f);
        await StockAbilityProfile.ObserveAsync(primary, rootAt, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var root) && root!.SourceId == Lockdown &&
            root.Type == TempMods.KindState && root.TargetId == TempMods.RootedAbility &&
            root.AddRemoveType == 0 && root.Duration == 12, "Lockdown applies its root at the valid range edge");

        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count), "Stock cast and recipient framing");
        return Array.Empty<byte>();

        static (string Command, string Reply) Teleport(int z)
        {
            Check.That(NavMesh.Locate(1, X, Y, z, out var nav), "The exact teleport has a mapped outdoor polygon");
            return ($"?tp {X} {Y} {z} {nav.sector:x} {nav.region:x} {nav.polygon:x}",
                $"Teleported to {nav.sector:x}/{nav.region:x}");
        }

        async Task MoveAsync((string Command, string Reply) stand)
        {
            caster.SendChat(stand.Command);
            await caster.WaitForTextAsync(stand.Reply);
            caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        }
    }
}
