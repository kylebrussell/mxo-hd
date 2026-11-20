using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterViralResistanceBatchMode : NativeQaMode
{
    private const ushort StaticBlast = 33, Tinkering = 36, LogicBarrage = 57, Hacker = 1006, Coder = 1008;
    private const int Level = 18, ViralResistance = 75;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, StaticBlast, Tinkering, LogicBarrage, Hacker, Coder)
        .Order().ToArray();
    // Full stock bodies from PR504 (33 and 36), PR396 (57) and PR521 (styles).
    private const string BlastUse = "80b92100040002000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string TinkeringUse = "80b924000400020000000000000000000000000000000000000000000000002090767241086b0000000000";
    private const string BarrageUse = "80b93900040002000000000000000000000000000000000000000000585cba0001000000086b0000000000";
    private const string HackerUse = "80b9ee030000000000000000000000000000000000000000000000000000000084d75741086b0000000000";
    private const string CoderUse = "80b9f0030400020000000000000000000000000000000000000000000000000084d75741086b0000000000";

    public override string Name => "ability-parity-starter-viral-resistance";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET y=495,level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, StaticBlast, Tinkering, Hacker, LogicBarrage },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "33,36,57", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Enter ?combatdepth foundations-viral once. At the street stand, Viral Neutral is 4 m east. Viral Resisted is just southeast with +75 Viral Resistance. Use Enter for chat and turn or strafe to select each NPC.",
            "Select Coder Style (page 2 slot 1). Use Static Blast (slot 2) on Neutral, wait for its six-second reuse, then use the same slot on Resisted. Compare the landed damage and the source-specific typed resistance log.",
            "Select Neutral. Use Tinkering (slot 3) once. Compare the primary hit to the splash on Resisted from the same cast. Then select Hacker Style (slot 4). Use Logic Barrage (slot 5) on Neutral, wait for own and shared reuse, then use it on Resisted. The disposable profile fixes only eligible viral accuracy draws."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal loaded memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");
        actor.SendChat("?combatdepth foundations-viral");
        await actor.WaitForTextAsync("Viral fixture ready.");
        var neutral = await actor.WaitForViewNamedAsync("Viral Neutral");
        var resisted = await actor.WaitForViewNamedAsync("Viral Resisted");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        await SelectStyleAsync(Coder, CoderUse, Handle(neutral));
        await CompareAsync(StaticBlast, BlastUse, cast: 2, reuse: 6);
        int splashAt = server.DebugLog().Length;
        int tinkeringAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(TinkeringUse, Handle(neutral)));
        await EvidenceRetestWire.CastBarAsync(actor, Tinkering, 6);
        int primary = await LandedAsync(Tinkering, neutral, splashAt);
        int splash = await LandedAsync(Tinkering, resisted, splashAt);
        CheckReduction(Tinkering, primary, splash, splashAt);
        await EvidenceRetestWire.TimerAsync(actor, tinkeringAt, Tinkering, AbilityCooldowns.OwnTimer, 30);

        await SelectStyleAsync(Hacker, HackerUse, 0);
        await CompareAsync(LogicBarrage, BarrageUse, cast: 2, reuse: 4);
        Check.Equal(0, actor.World.Problems.Count, "Viral resistance replay framing");
        return Array.Empty<byte>();

        async Task SelectStyleAsync(ushort style, string stock, uint target)
        {
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, target));
            await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(style)} 00 00");
        }

        async Task CompareAsync(ushort id, string stock, float cast, float reuse)
        {
            int start = server.DebugLog().Length;
            int sent = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, Handle(neutral)));
            await EvidenceRetestWire.CastBarAsync(actor, id, cast);
            int raw = await LandedAsync(id, neutral, start);
            await EvidenceRetestWire.TimerAsync(actor, sent, id, AbilityCooldowns.OwnTimer, reuse, ended: true);

            start = server.DebugLog().Length;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, Handle(resisted)));
            await EvidenceRetestWire.CastBarAsync(actor, id, cast);
            int final = await LandedAsync(id, resisted, start);
            CheckReduction(id, raw, final, start);
        }

        async Task<int> LandedAsync(ushort id, ViewCreation victim, int start)
        {
            var hit = new Regex($@"\[ABILITY\] {id} lands on view {victim.ViewId} for (\d+)\b");
            await actor.WaitUntilAsync(() => hit.IsMatch(server.DebugLog()[start..]), $"Ability {id} lands on {victim.Name}",
                HeadlessPlayer.DefaultTimeout);
            return int.Parse(hit.Match(server.DebugLog()[start..]).Groups[1].Value);
        }

        void CheckReduction(ushort id, int raw, int final, int start)
        {
            Check.That(raw > 0 && final < raw, $"Viral resistance reduces the hit from {id}");
            Check.Equal((int)TypedAbilityResistance.Amount(raw, Level, Level, ViralResistance), final,
                $"Ability {id} consumes the matching resistance once");
            Check.That(server.DebugLog()[start..].Contains(
                $"{id} type2 total{ViralResistance} raw{raw} final{final}", StringComparison.Ordinal),
                $"Ability {id} uses the selected target's viral resistance");
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
