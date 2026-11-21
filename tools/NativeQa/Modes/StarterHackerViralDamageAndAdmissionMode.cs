using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerViralDamageAndAdmissionMode : NativeQaMode
{
    private const int Level = 33, AbilityRank = 19, X = 24800, Y = 495, Z = -1498;
    private const ushort Style = CombatStyles.HackerStyle, HarmfulCode = 53;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Style, HarmfulCode, 58, 59, 60)
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (ushort Id, int Cast, int OwnReuse, int FamilyReuse, int NeutralDamage, int ResistedDamage, string Capture)[] Samples =
    {
        (HarmfulCode, 4, 8, 4, 14, 10, HarmfulCodeMode.StockViralUse),
        (58, 2, 4, 4, 12, 8, StarterHackerViralSightMode.Samples[1].Capture),
        (59, 2, 4, 4, 19, 14, StarterHackerViralSightMode.Samples[2].Capture),
        (60, 4, 2, 2, 8, 5, StarterHackerViralSightMode.Samples[3].Capture)
    };

    public override string Name => "ability-parity-starter-hacker-viral-admission";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "alignment=1,pvpflag=0,innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={AbilityRank} WHERE char_id=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z=-1898," +
                "alignment=1,pvpflag=0 WHERE charId=6113",
            "DELETE FROM char_abilities WHERE char_id=6113"
        }, DevCommands: true, hotbarAbilities: Samples.Select(sample => sample.Id).Prepend(Style).ToArray(),
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "53,58,59,60", ["NativeCloseableRows"] = 0, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, full profile {Memory}/{AbilityMemory.ForLevel(Level)} memory with rank-{AbilityRank} starter abilities. Choose Hacker Style (page 2 slot 1). Try Harmful Code (slot 2) with no target and then with same-organization QAOther 4 m south. Expect no cast, IS debit or reuse. If a request is sent, expect one invalid-target refusal.",
            "Enter ?combatdepth foundations-viral-peer. Viral Neutral (same level as CSR) is 4 m straight ahead and Viral Resisted is slightly southeast with +75 Viral Resistance. Remain out of interlock. At login, [MODIFIER] char=110 source=95 stat=1031 adds +5. Hacker Style adds source=1006 stat=172 +12. Both are duration=0s.",
            "Cast slots 2–5 on Viral Neutral, waiting for shared reuse between casts. Cast slots 2–4 again on Viral Resisted. Code Nuke (slot 5) splashes the resisted target on the same press. Expect neutral → resisted [ABILITY] hits of 14 → 10 (53), 12 → 8 (58), 19 → 14 (59), and 8 → 5 (60). The matching [TYPED-RESISTANCE OURS] records say type2 total75. Cast / own reuse / shared reuse in seconds: 53 = 4/8/4, 58 and 59 = 2/4/4, 60 = 4/2/2.",
            "For Harmful Code range and sight, enter ?combatdepth foundations-sight-pick and select QA Sight Target 2 m ahead. Keep it selected. Use ?combatdepth foundations-sight-far and try slot 2. If stock sends it, expect the out-of-range refusal with no cast, IS debit, reuse or damage. Use ?combatdepth foundations-sight-blocked without pressing Escape and try slot 2 again. If sent, expect Sight blocked ability 53 and no contest or damage."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var friendly = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther", stockCharacterLoad: true);
        context.Helpers.Add(friendly.Handle, friendly);
        await friendly.EnterWorldAsync();
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value)), "The full starter viral profile is stocked and fits memory");
        await actor.EnterWorldAsync();
        int styleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await StockAbilityProfile.ObserveAsync(actor, styleAt,
            packet => StockAbilityProfile.Bonus(packet, Style, 172, 12, 0, false), "Stock Hacker Style adds +12 Viral Damage");
        var ally = await actor.WaitForViewNamedAsync(friendly.Handle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);

        actor.SendChat("?combatdepth foundations-viral-peer");
        await actor.WaitForTextAsync("Viral fixture ready.");
        var neutral = await actor.WaitForViewNamedAsync("Viral Neutral");
        var resisted = await actor.WaitForViewNamedAsync("Viral Resisted");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, HarmfulCode, HarmfulCodeMode.StockViralUse, neutral, ally);

        await StockAbilityProfile.ObserveAsync(actor, 0,
            packet => StockAbilityProfile.Bonus(packet, 95, 1031, 5, 0, false), "Transmit Virus contributes +5 at rank 19");

        foreach (var sample in Samples)
        {
            int logAt = server.DebugLog().Length;
            await CastAsync(sample, neutral);
            int raw = await LandedAsync(sample.Id, neutral, logAt);
            Check.Equal(sample.NeutralDamage, raw, $"Ability {sample.Id} lands for the expected sourced base plus fixture influence");
            if (sample.Id != 60)
            {
                logAt = server.DebugLog().Length;
                await CastAsync(sample, resisted);
            }
            int final = await LandedAsync(sample.Id, resisted, logAt);
            Check.Equal(sample.ResistedDamage, final, $"Viral resistance lowers the peer hit for {sample.Id}");
            Check.Equal((int)TypedAbilityResistance.Amount(raw, Level, Level, 75), final, $"Viral resistance applies to {sample.Id} once");
            Check.That(server.DebugLog()[logAt..].Contains($"{sample.Id} type2 total75 raw{raw} final{final}"),
                $"Ability {sample.Id} uses the resisted recipient's stat");
        }

        actor.SendChat("?combatdepth foundations-sight-pick");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        var sight = await actor.WaitForViewNamedAsync("QA Sight Target");
        uint sightHandle = Handle(sight);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(sightHandle))}");
        actor.SendChat("?combatdepth foundations-sight-far");
        await actor.WaitForTextAsync("QA Sight Target is beyond 20 m");
        byte[] use = StarterHackerControlTargetMode.WithHandle(HarmfulCodeMode.StockViralUse, sightHandle);
        await StarterHackerControlTargetMode.RefusedAsync(actor, HarmfulCode, use, AbilityRefusals.TargetOutOfRange);
        await SightStandCastProof.CheckBlockedAsync(context, sight, new[] { (HarmfulCode, 8f, HarmfulCodeMode.StockViralUse) });
        return Array.Empty<byte>();

        async Task CastAsync((ushort Id, int Cast, int OwnReuse, int FamilyReuse, int NeutralDamage, int ResistedDamage, string Capture) sample, ViewCreation victim)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, Handle(victim)));
            await EvidenceRetestWire.CastBarAsync(actor, sample.Id, sample.Cast);
            await EvidenceRetestWire.TimerAsync(actor, cursor, sample.Id, AbilityCooldowns.OwnTimer, sample.OwnReuse);
            await EvidenceRetestWire.TimerAsync(actor, cursor, 682, AbilityCooldowns.GroupTimer, sample.FamilyReuse);
            await EvidenceRetestWire.TimerAsync(actor, cursor, sample.Id, AbilityCooldowns.OwnTimer, sample.OwnReuse, ended: true);
        }

        async Task<int> LandedAsync(ushort id, ViewCreation victim, int start)
        {
            var hit = new Regex($@"\[ABILITY\] {id} lands on view {victim.ViewId} for (\d+)\b");
            await actor.WaitUntilAsync(() => hit.IsMatch(server.DebugLog()[start..]), $"Ability {id} lands on {victim.Name}",
                HeadlessPlayer.DefaultTimeout);
            return int.Parse(hit.Match(server.DebugLog()[start..]).Groups[1].Value);
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
