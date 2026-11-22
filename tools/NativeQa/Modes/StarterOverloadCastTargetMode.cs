using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterOverloadCastTargetMode : NativeQaMode
{
    private const int X = 17000, Y = 495, CasterZ = 8200, InZ = 6600, OutZ = 6100, AllyX = 17100, AllyZ = 6700;
    private const ushort Overload = 66;
    private static readonly ushort[] Abilities = StarterHackerPassiveFixture.Loadout(Overload);
    private static readonly ushort[] ModifierStats = { 522, 524, 1009, 1010 };

    public override string Name => "ability-parity-starter-overload-cast-target";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, CasterZ, Array.Empty<string>(),
        new[] { StarterHackerPassiveFixture.CapRanks }, new[]
        {
            StarterHackerPassiveFixture.PutActor(X),
            $"UPDATE characters SET y={Y},z={CasterZ},rotation={MobAggro.YawTowards(X,CasterZ,X,InZ)} WHERE charId=110",
            StarterHackerPassiveFixture.PutHelper(6114, X, 2, 3000, 3000),
            $"UPDATE characters SET z={InZ} WHERE charId=6114",
            StarterHackerPassiveFixture.PutHelper(6111, AllyX, 1, 3000, 3000),
            $"UPDATE characters SET z={AllyZ} WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)"
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Overload },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "66", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level 19, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(19)} memory. CSR ({X},{Y},{CasterZ}) faces QAEnemy IN ({X},{Y},{InZ}), exactly 16 m away. QAFriend ({AllyX},{Y},{AllyZ}) is allied. Both helpers and OUT ({X},{Y},{OutZ}) stand on clear outdoor NAV away from the ice.",
            "Select Hacker Style in page 2 slot 1. /target QAFriend and press slot 2: local refusal or 80 ba reason 24 if sent. /target QAEnemy and send OUT before pressing slot 2: local refusal or 80 ba reason 34. Check no cast bar, [ABILITY-COST], reuse or [MODIFIER] in either case.",
            $$"""OUT helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{OutZ}}}. IN helper request: {"actor":"QAEnemy","action":"move","x":{{X}},"y":{{Y}},"z":{{InZ}}}. Keep CSR at the same stand.""",
            "Return QAEnemy IN. Prepare OUT, use slot 2, and submit OUT during its 2 s cast bar. Expect [ABILITY] 66 fizzles: the selected Overload target changed or left range, with 14 IS (700 to 686), 43 s reuse, and no hit or modifier.",
            "After reuse, return QAEnemy IN and repeat with no move. Expect 2 s, 43 s own reuse, 14 IS (686 to 672), and [MODIFIER] char=6114 source=66 stats 522/524/1009/1010 each -10 for 33 s. No HP loss or interlock round."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, CasterZ), (X, InZ), (X, OutZ), (AllyX, AllyZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var floor) && !floor.indoors && Math.Abs(floor.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, CasterZ, x, Y, z), $"Verified outdoor NAV and sight at ({x},{Y},{z})");

        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var enemy = context.Helpers["QAEnemy"];
        var ally = context.Helpers["QAFriend"];
        StarterHackerPassiveFixture.VerifyProfile(caster, Abilities);
        uint handle = await StarterHackerPassiveFixture.StartAsync(caster, enemy, server, pvp: true);
        var view = await caster.WaitForViewNamedAsync(enemy.Handle);
        var friendView = await caster.WaitForViewNamedAsync(ally.Handle);
        uint allyHandle = DuelPackets.Handle(friendView.ViewId, friendView.SpawnId);
        int allyStart = ally.World.RpcSnapshot().Count;
        await StarterHackerControlTargetMode.RefusedAsync(caster, Overload, Request(0));
        await StarterHackerControlTargetMode.RefusedAsync(caster, Overload, Request(handle ^ 0x10000));
        Select(allyHandle);
        await StarterHackerControlTargetMode.RefusedAsync(caster, Overload, Request(allyHandle));
        Check.That(!ally.World.RpcSnapshot().Skip(allyStart).Any(frame => ModifierStats.Any(stat =>
            StockAbilityProfile.Bonus(frame, Overload, stat, -10, 33, false))), "An ally receives no Overload modifiers");
        Select(handle);
        await MoveAsync(OutZ);
        await StarterHackerControlTargetMode.RefusedAsync(caster, Overload, Request(handle), AbilityRefusals.TargetOutOfRange);

        await MoveAsync(InZ);
        int logStart = server.DebugLog().Length;
        int targetStart = enemy.World.RpcSnapshot().Count;
        int castStart = Begin();
        await EvidenceRetestWire.CastBarAsync(caster, Overload, 2);
        await MoveAsync(OutZ);
        await caster.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
            "[ABILITY] 66 fizzles: the selected Overload target changed or left range"),
            "Overload fizzles if its original enemy leaves range during the cast", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(caster, castStart, Overload, AbilityCooldowns.OwnTimer, 43);
        await CheckIsAsync(686);
        enemy.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(enemy, targetStart, frame => frame.Opcode == 0x149, "The recipient stream advances beyond the cast");
        Check.That(!enemy.World.RpcSnapshot().Skip(targetStart).Any(frame => ModifierStats.Any(stat =>
            StockAbilityProfile.Bonus(frame, Overload, stat, -10, 33, false))), "Leaving range prevents all four modifiers");
        byte[] reuseEnd = AbilityCooldowns.BuildTimer(Overload, AbilityCooldowns.OwnTimer, 43, start: false);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(castStart).Any(rpc => rpc.Bytes.AsSpan().SequenceEqual(reuseEnd)),
            "The accepted but fizzled Overload's usual reuse ends", TimeSpan.FromSeconds(46));

        await MoveAsync(InZ);
        targetStart = enemy.World.RpcSnapshot().Count;
        castStart = Begin();
        await EvidenceRetestWire.CastBarAsync(caster, Overload, 2);
        await EvidenceRetestWire.TimerAsync(caster, castStart, Overload, AbilityCooldowns.OwnTimer, 43);
        await CheckIsAsync(672);
        foreach (ushort stat in ModifierStats)
            await StockAbilityProfile.ObserveAsync(enemy, targetStart, frame =>
                StockAbilityProfile.Bonus(frame, Overload, stat, -10, 33, false), $"Overload {stat} lands at exactly 16 m");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + ally.World.Problems.Count, "Overload target wire framing");
        Console.WriteLine("OVERLOAD CAST TARGET PASS 66");
        return Array.Empty<byte>();

        byte[] Request(uint target) => StarterHackerControlTargetMode.WithHandle(HackerSightStandBatchMode.OverloadStock, target);

        void Select(uint target) => caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(target))}");

        int Begin()
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SkipReceived();
            caster.SendRpc(Request(handle));
            return cursor;
        }

        async Task MoveAsync(int z)
        {
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = caster.World.ViewSnapshot().Count;
            enemy.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(caster, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }

        Task CheckIsAsync(ushort value) => caster.WaitUntilAsync(() => caster.World.ViewSnapshot()
            .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains(value),
            $"Ordinary Overload cost gives {value} IS", HeadlessPlayer.DefaultTimeout);
    }
}
