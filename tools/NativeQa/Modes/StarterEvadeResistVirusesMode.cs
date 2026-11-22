using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterEvadeResistVirusesMode : NativeQaMode
{
    private const ushort Evade = 217, Resist = 76, Insight = 113;
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, EnemyX = 17250;
    private const string StockEvade = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Evade, Resist).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] Stats = { (1030, 4), (1045, 4), (1049, 2) };

    public override string Name => "ability-parity-starter-evade-resist-viruses";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={EnemyX},y={Y},z={Z} WHERE charId=6114",
            "DELETE FROM inventory WHERE charId IN (110,6114) AND slot=107",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                $"(6114,400,2147485696,{Level},1),(6114,401,2147501056,{Level},1),(6114,402,2147599360,{Level},1)"
        }, DevCommands: true, hotbarAbilities: new[] { Evade }, hotbarWithdraw: true, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
            ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "76,113,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Level-19 CSR (17000,495,6400) faces QAEnemy (17250,495,6400), both clear outdoor NAV. No bag or player-target clicks. Leave CSR on Speed. QAEnemy is level 19, in Power with Combat Insight +3, force 103.",
            "With Resist Viruses loaded, press page 2 slot 1 Evade Combat at rest. Expect [VITALS] IS 2000 to 1960 and shield 624. Send helper melee on CSR. Check [COMBAT-INSIGHT OURS] for force103 evade114 allowedFalse and [EVASION] IS 1960->1950, shield 255->163. Enter ?qaunload 76 on CSR and check [MODIFIER] 1030 +4, 1045 +4, 1049 +2 removals. Repeat helper melee: force103 evade112 allowedFalse, IS 1950->1940, shield 163->71. Execute Program and Selective Phage still contribute +1 each.",
            "Enter ?qaload 76 and check all three records restore. Repeat helper melee: IS 1940->1930, shield 71->0, no round, state off, 30 s own reuse. Send helper melee once more. This time it starts a [PVP] round. End with stock Withdraw (page 2 slot 2) and helper stop. Check own shield and the helper observer meter in the capture."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Evade needs repository NAV data.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, EnemyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var at) && !at.indoors && Math.Abs(at.floorY + 95 - Y) < 1,
                $"Evade stand ({x},{Y},{Z}) is on outdoor NAV");
        var server = context.ActualServer!;
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await PvpOpponentsFixture.EnterAsync(enemy);
        NativeQaReadiness.RequireNoAmbientAggro(server, enemy);
        enemy.SendRpc("42 01 00 00 00 00 00");
        await StockAbilityProfile.ObserveAsync(enemy, 0, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == 1017 && record.TargetId == 1012,
            "Helper selected Power");
        await StockAbilityProfile.ObserveAsync(enemy, 0, frame =>
            StockAbilityProfile.Bonus(frame, Insight, 1048, 3, 0, false), "Helper Combat Insight +3");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(req => archive.TryGetValue(req.Key, out var row) && row.Level >= req.Value)),
            "The full level-19 prerequisite loadout fits stock memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await enemy.WaitForViewNamedAsync(actor.Handle);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
        await ObservePassivesAsync(0, false);
        int shieldAt = actor.World.RpcSnapshot().Count;
        int viewAt = actor.World.ViewSnapshot().Count;
        actor.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(actor, shieldAt, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Evade &&
            record.TargetId == EvasionShield.StateAbility && record.AddRemoveType == 0, "Live Evade shield");
        await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewAt)
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)1960),
            "Evade spends 40 IS", HeadlessPlayer.DefaultTimeout);

        await AttackAsync(114, 1950, 163);
        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Resist }, load: false);
        await ObservePassivesAsync(removed, true);
        await AttackAsync(112, 1940, 71);
        int restored = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Resist }, load: true);
        await ObservePassivesAsync(restored, false);
        int shieldOff = actor.World.RpcSnapshot().Count;
        await AttackAsync(114, 1930, 0);
        await StockAbilityProfile.ObserveAsync(actor, shieldOff, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Evade &&
            record.TargetId == EvasionShield.StateAbility && record.AddRemoveType == 1, "Evade shield removed by the final resisted attempt");
        await EvidenceRetestWire.TimerAsync(actor, shieldOff, Evade, AbilityCooldowns.OwnTimer, 30);
        await AttackAsync(null);
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count, "Evade/Resist stock framing");
        Console.WriteLine("EVADE RESIST PASS: force 103, shield 255/163/71/0, and admitted with shield off.");
        return Array.Empty<byte>();

        async Task ObservePassivesAsync(int from, bool removed)
        {
            foreach (var (stat, value) in Stats)
                await StockAbilityProfile.ObserveAsync(actor, from,
                    frame => StockAbilityProfile.Bonus(frame, Resist, stat, value, 0, removed), $"Resist Viruses {stat}, remove={removed}");
        }

        async Task AttackAsync(int? evade, ushort expectedIs = 0, byte expectedHealth = 0)
        {
            int logAt = server.DebugLog().Length;
            enemy.SkipReceived();
            actor.SkipReceived();
            enemy.SendRpc($"40 {handle} 01 6b");
            bool admitted = evade is null;
            await enemy.WaitForRpcAsync(admitted ? $"46 {handle} 01" : $"4d 0e 00 01 00 {handle}");
            Check.That(admitted ? !server.DebugLog()[logAt..].Contains("[COMBAT-INSIGHT OURS]") :
                server.DebugLog()[logAt..].Contains($"QAEnemy -> CSR_Tester force103 evade{evade} allowedFalse"),
                "Same attack consumes the current shield and Resist Viruses bonus");
            if (!admitted)
            {
                byte[] own = PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfInnerStrength, PlayerViewAttributes.U16(expectedIs)),
                    (30, PlayerViewAttributes.U8(expectedHealth)));
                await actor.WaitForViewUpdateAsync(2, WireFormat.ToHex(own[2..]));
                if (expectedHealth > 0)
                {
                    byte[] other = PlayerViewAttributes.Build((21, PlayerViewAttributes.U8(expectedHealth)));
                    await enemy.WaitForViewUpdateAsync(target.ViewId, WireFormat.ToHex(other));
                }
            }
            if (admitted)
            {
                enemy.SkipReceived();
                enemy.SendRpc("44 00");
                actor.SendRpc("44 00");
                await enemy.WaitForRpcAsync($"46 {handle} 00", TimeSpan.FromSeconds(20));
            }
        }
    }
}
