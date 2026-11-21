using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSharpshooterTargetsMode : NativeQaMode
{
    // The starter passives need 57 memory together with their full ranked prerequisite profile.
    private const int X = 17000, Y = 495, Z = 2300, Level = 27;
    private const ushort Insight = 113, Operative = 143, Sharpshooter = 148;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Insight, Operative, Sharpshooter).Order().ToArray();
    private static readonly (string Name, int Id, int X, int Z, int Org)[] Stands =
    {
        ("QAEnemy", 6114, 17120, 2300, 2), ("QAFriend", 6111, 17230, 2420, 1),
        ("QAFar", 6112, 17500, 2300, 2), ("QAOther", 6113, 17320, 2380, 2)
    };

    public override string Name => "ability-parity-starter-sharpshooter-targets";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { PvpOpponentsFixture.NativeSql, CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},rotation=64 WHERE charId=110",
            "DELETE FROM inventory WHERE charId IN (6111,6112,6113,6114) AND slot BETWEEN 105 AND 107",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (6113,{CombatFixtureWeapons.Handgun},{CombatFixtureWeapons.HandSlot},1,0)",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)" }
        .Concat(Stands.Select(stand => $"UPDATE characters SET x={stand.X},y={Y},z={stand.Z},alignment={stand.Org}," +
            $"level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},healthC=20000,healthM=20000 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new ushort[] { Insight, Operative, Sharpshooter },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [CombatFixtureWeapons.WeaponMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "113,143,148", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory including all prerequisites. CSR ({X},{Y},{Z}) faces QAEnemy (17120,495,2300). QAFriend (17230,495,2420), QAFar (17500,495,2300) and armed QAOther (17320,495,2380) are on the clear street. Type ?cd w rifle and wait for 3f slot 105 kind 16 goid 692. Use the target radial Fire!, then type ?cd stop after one shot before changing the target or loadout.",
            "Keep 113 and 143 loaded throughout. At login expect [MODIFIER] char=110 source=113 stat=1048 +5, and source=143 stat=526 +50. Their records stay loaded and the base attack input stays fixed.",
            "With 148 loaded, Fire! on QAEnemy, QAFar and QAOther in turn. [SHARPSHOOTER OURS] modifier 0, +15, 0 respectively. Type ?qaunload 148 and repeat: -15, +15, 0. QAOther is armed, so the shot skips the unarmed check.",
            """With 148 still unloaded, use helper request {"actor":"QAFriend","action":"melee","target":"QAEnemy"} via the helper request file. While both helpers remain interlocked, Fire! on QAEnemy. Expect two opponents, modifier -30. Stop, type ?qaload 148, and repeat while the helpers remain interlocked. Expect 0 with countSampledFalse. CSR stays outside interlock throughout. No maximum [VITALS] changes."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "The full prerequisite profile fits native memory");
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var (id, stat, value) in new (ushort, ushort, int)[] { (Insight, 1048, 5), (Operative, 526, 50) })
            await StockAbilityProfile.ObserveAsync(actor, 0,
                frame => StockAbilityProfile.Bonus(frame, id, stat, value, 0, false), $"Stable passive {id}/{stat}");

        actor.SendRpc("3f 6b 00 00 00 00 00");
        int equipAt = actor.World.RpcSnapshot().Count;
        actor.SendChat("?cd w rifle");
        var rifle = await StockAbilityProfile.ObserveAsync(actor, equipAt,
            frame => frame.Bytes is [0x5e, _, 0, 0xb4, 2, 0, 0, 0, 0, 0, 16, 1], "Client receives a refreshed rifle inventory entry");
        byte slot = rifle.Bytes[1];
        await actor.WaitForRpcAsync($"65 {slot:x2} 00 69 00 00");
        await actor.WaitForTextAsync("Fixture equipped rifle (692)");
        actor.SendRpc($"3f {slot:x2} 00 10 00 00 00");
        actor.SendRpc("3f 69 00 10 00 00 00");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains("slot 105, kind 16, goid 692"),
            "Rifle becomes the stock Free Attack selection", HeadlessPlayer.DefaultTimeout);

        int? baseAttack = null;
        await FireAsync("QAEnemy", 0, true, false, 0);
        await FireAsync("QAFar", 15, true, false, 0);
        await FireAsync("QAOther", 0, true, true, 0);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: false);
        await FireAsync("QAEnemy", -15, false, false, 1);
        await FireAsync("QAFar", 15, false, false, 1);
        await FireAsync("QAOther", 0, false, true, 0);

        var ally = context.Helpers["QAFriend"];
        var enemy = context.Helpers["QAEnemy"];
        string close = Handle(await ally.WaitForViewNamedAsync(enemy.Handle));
        ally.SendRpc($"40 {close} 01 6b");
        await ally.WaitForRpcAsync("47");
        await FireAsync("QAEnemy", -30, false, false, 2);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: true);
        await FireAsync("QAEnemy", 0, true, false, 0);
        ally.SendRpc("44 00");
        enemy.SendRpc("44 00");
        await ally.WaitForRpcAsync($"46 {close} 00");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Sharpshooter batch framing");
        Console.WriteLine("SHARPSHOOTER TARGETS PASS: near 0/-15; far +15; armed 0; two opponents -30/0.");
        return Array.Empty<byte>();

        async Task FireAsync(string name, int modifier, bool loaded, bool armed, int opponents)
        {
            string target = Handle(await actor.WaitForViewNamedAsync(name));
            int at = server.DebugLog().Length;
            actor.SkipReceived();
            actor.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {target}"),
                WireFormat.FromHex($"40 {target} 00 69"));
            await actor.WaitForRpcAsync("4e 00");
            actor.SendChat("?cd stop");
            await actor.WaitForTextAsync("Fixture combat ended.");
            actor.SendRpc("3d 00");
            var inputs = server.DebugLog()[at..].Split('\n').Select(line => SharpshooterFixture.ReadInput(line.TrimEnd('\r'), actor.Handle))
                .OfType<SharpshooterFixture.Input>().ToArray();
            Check.That(inputs.Length > 0, $"Actual ranged shot inputs for {name}");
            foreach (var shot in inputs)
            {
                baseAttack ??= shot.Before;
                Check.Equal(baseAttack.Value, shot.Before, "Same unmodified ballistic attack input");
                Check.Equal((modifier, loaded, armed, opponents, !loaded && !armed),
                    (shot.Modifier, shot.Loaded, shot.DefenderGun, shot.Opponents, shot.CountSampled),
                    $"Current role, distance and live opponents determine the penalty for {name}");
            }
        }
    }

    private static string Handle(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
}
