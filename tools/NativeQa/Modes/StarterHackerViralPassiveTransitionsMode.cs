using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerViralPassiveTransitionsMode : NativeQaMode
{
    private const int Level = 19, X = 24800, Y = 495, Z = -1498;
    private const ushort Hacker = 1006, Network = 64, Contagion = 73, Viruses = 76, Upgrade = 1078, Blast = 58;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, Hacker, Network, Contagion, Viruses, Blast).Order().ToArray();
    private static readonly int Memory = Abilities.Sum(AbilityMemory.Cost);
    private static readonly (ushort Id, int Value)[] NetworkStats = { (1147, 1) };
    private static readonly (ushort Id, int Value)[] ContagionStats = { (995, 1), (1147, 5) };
    private static readonly (ushort Id, int Value)[] VirusesStats = { (1030, 4), (1045, 4), (1049, 2) };
    private static readonly (ushort Id, int Value)[] UpgradeStats = { (342, 4), (564, 7), (114, 10) };
    // Complete stock PR396 Logic Blast and c59bfd3cc0aa:1594 Network Hacker Upgrade.
    private const string BlastStock = "80b93a000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string UpgradeStock = "80b93604000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";

    public override string Name => "ability-parity-starter-hacker-viral-passive-transitions";
    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[] { $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
            "innerStrC=700,innerStrM=1000 WHERE charId=110" }, DevCommands: true, hotbarAbilities: new[] { Hacker, Blast, Upgrade },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "64,73,76,1078", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full profile {Memory}/{AbilityMemory.ForLevel(Level)} memory. CSR starts outdoors facing east. Choose Hacker Style (page 2 slot 1), enter ?combatdepth foundations-viral, and select Viral Neutral 4 m ahead. Keep the default Speed tactic and selection. Use slot 2 Logic Blast as a control and expect [FREECAST-VIRAL] inputs=35/0.",
            "Enter ?qaunload 64. Expect [MODIFIER] char=110 source=64 stat=1147 remove value=+1 duration=0s, and grant 1078 unloads. When slot 2 is ready, cast it again while 73 is still loaded. Expect [FREECAST-VIRAL] inputs=34/0. Enter ?qaunload 73 and ?qaload 73. Expect source=73 stat=995 value=+1 and stat=1147 value=+5, remove then add for each. Enter ?qaload 64 to restore its +1 and grant 1078.",
            "Enter ?qaunload 76, then ?qaload 76. Expect [MODIFIER] source=76 stat=1030 value=+4, stat=1045 value=+4 and stat=1049 value=+2, all duration=0s, with matching remove/add pairs.",
            "Press slot 3 Network Hacker Upgrade. Expect [MODIFIER] source=1078 stat=342 value=+4, stat=564 value=+7 and stat=114 value=+10, add on CSR only. Use slot 2 again on the same target. Expect [FREECAST-VIRAL] inputs=40/0. Enter ?dismissbuff 1078 and confirm matching removals."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(archive.Keys.ToHashSet().SetEquals(Abilities) && Abilities.All(id => AbilityPrerequisites.Current.Closure(id)
            .All(need => archive.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "Full ranked passive profile in Margin");
        Check.That(Memory <= AbilityMemory.ForLevel(Level), "The level-19 profile fits memory");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await RecordsAsync(0, Network, NetworkStats);
        await RecordsAsync(0, Contagion, ContagionStats);
        await RecordsAsync(0, Viruses, VirusesStats);
        await StockAbilityProfile.ObserveAsync(actor, 0, StockAbilityProfile.GrantRecord(Upgrade, false), "Network Hacker grants 1078");
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendChat("?combatdepth foundations-viral");
        await actor.WaitForTextAsync("Viral fixture ready");
        var target = await actor.WaitForViewNamedAsync("Viral Neutral");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        Check.Equal(35, await AttackInputAsync(), "The initial loaded viral attack input");

        int networkOff = await ChangeAsync(Network, NetworkStats, load: false);
        await StockAbilityProfile.ObserveAsync(actor, networkOff, StockAbilityProfile.GrantRecord(Upgrade, true), "Unloading 64 takes away 1078");
        Check.Equal(34, await AttackInputAsync(), "Removing Network Hacker crosses the level-19 rounding threshold");
        await ChangeAsync(Contagion, ContagionStats, load: false);
        await ChangeAsync(Contagion, ContagionStats, load: true);
        int networkOn = await ChangeAsync(Network, NetworkStats, load: true);
        await StockAbilityProfile.ObserveAsync(actor, networkOn, StockAbilityProfile.GrantRecord(Upgrade, false), "Reloading 64 restores 1078");
        await ChangeAsync(Viruses, VirusesStats, load: false);
        await ChangeAsync(Viruses, VirusesStats, load: true);

        int buffOn = actor.World.RpcSnapshot().Count;
        actor.SendRpc(UpgradeStock);
        await RecordsAsync(buffOn, Upgrade, UpgradeStats);
        await EvidenceRetestWire.TimerAsync(actor, buffOn, Upgrade, AbilityCooldowns.OwnTimer, 30);
        Check.Equal(40, await AttackInputAsync(), "The direct +4 goes through the Speed tactic multiplier");
        int buffOff = actor.World.RpcSnapshot().Count;
        actor.SendChat("?dismissbuff 1078");
        await RecordsAsync(buffOff, Upgrade, UpgradeStats, remove: true);
        Check.Equal(0, actor.World.Problems.Count, "Viral passive transitions frame cleanly");
        return Array.Empty<byte>();

        async Task<int> ChangeAsync(ushort source, (ushort Id, int Value)[] stats, bool load)
        {
            int cursor = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { source }, load);
            await RecordsAsync(cursor, source, stats, remove: !load);
            return cursor;
        }

        async Task RecordsAsync(int cursor, ushort source, (ushort Id, int Value)[] stats, bool remove = false)
        {
            foreach (var (stat, value) in stats)
                await StockAbilityProfile.ObserveAsync(actor, cursor, rpc => StockAbilityProfile.Bonus(rpc, source, stat, value, 0, remove),
                    $"{source}/{stat} value {value}, remove={remove}");
        }

        async Task<int> AttackInputAsync()
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int logAt = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(BlastStock, handle));
            var contest = new Regex(@"\[FREECAST-VIRAL\] ability=58 source=CSR_Tester target=Viral Neutral inputs=(\d+)/0");
            Match result = Match.Empty;
            await actor.WaitUntilAsync(() => (result = contest.Match(server.DebugLog()[logAt..])).Success,
                "Logic Blast uses the live viral attack input", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, Blast, AbilityCooldowns.OwnTimer, 4, ended: true);
            return int.Parse(result.Groups[1].Value);
        }
    }
}
