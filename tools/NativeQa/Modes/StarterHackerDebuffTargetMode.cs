using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerDebuffTargetMode : NativeQaMode
{
    private const int Level = 14, Rank = 12;
    private const ushort HackerStyle = 1006, Overload = 66, ProcessorLag = 69, Slow = 86;
    private const ushort ResistViruses = 76;

    // PR439 stock 537fece48163:1596 and 1717. Overload adapts the same targeted layout.
    private const string OverloadRequest = "80b94200040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000";
    private const string ProcessorLagRequest = "80b94500040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000";
    private const string SlowRequest = "80b95600040002000000000000000000000000000000000000000000585cbb0001000000086b0000000000";

    public override string Name => "ability-parity-starter-hacker-debuff-targets";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, HackerStyle, Overload, ProcessorLag, Slow }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray(),
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=200,innerStrM=200 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,CASE WHEN ability_id={AbilityMessages.AbilityGameObject(ResistViruses)} THEN {Level} ELSE {Rank} END) WHERE char_id=110"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, readyMessages: new[]
        {
            "Select Hacker Style outside interlock and use Overload 1.0 (66) with no selection. Expect one Target is not valid line with no cast bar, reuse or IS debit.",
            "Select Melee Dummy and retry immediately. Expect a two-second cast, 43s reuse and four -10 accuracy/defense debuffs for 33s. Capture the two stock 66 requests. Slow and Processor Lag already passed this check in PR439."
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "66", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Plan.Abilities.All(loaded.Contains), "Starter debuff fixture loads all three abilities and their prerequisites");
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        await StarterFixedDamageMode.SelectStyle(caster, self, HackerStyle, 172);

        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var proof = new List<object>();
        foreach (var (ability, capture, reuseSeconds) in new[] { (Overload, OverloadRequest, 43), (Slow, SlowRequest, 8), (ProcessorLag, ProcessorLagRequest, 43) })
        {
            var missing = await StarterHackerControlTargetMode.RefusedAsync(caster, ability,
                StarterHackerControlTargetMode.WithHandle(capture, 0));
            var stale = await StarterHackerControlTargetMode.RefusedAsync(caster, ability,
                StarterHackerControlTargetMode.WithHandle(capture, handle ^ 0x10000));
            int start = caster.World.RpcSnapshot().Count;
            byte[] request = StarterHackerControlTargetMode.WithHandle(capture, handle);
            caster.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(caster, ability, 2);
            await EvidenceRetestWire.TimerAsync(caster, start, ability, AbilityCooldowns.OwnTimer, reuseSeconds);
            string effectPrefix = $"[EFFECTS] {ability} on {dummy.Name}: ";
            string? effect = null;
            await caster.WaitUntilAsync(() => (effect = server.DebugLog().Split('\n')
                .FirstOrDefault(line => line.Contains(effectPrefix))) != null, $"{ability} applies to the selected dummy", HeadlessPlayer.DefaultTimeout);
            if (ability == ProcessorLag)
            {
                // This rank-12 Hacker Style profile scales the stored total from 30 to 33.
                Check.That(effect!.Contains("Dot 1157 33 30 s"), $"Processor Lag applies the style-scaled ViralDOT, got {effect}");
                await caster.WaitUntilAsync(() => server.DebugLog().Split('\n').Any(line =>
                    line.TrimEnd().EndsWith($"[EFFECTS] {ability} ticks on {dummy.Name} for 6", StringComparison.Ordinal)),
                    "Processor Lag deals a six-point tick at the shipped five-second interval", HeadlessPlayer.DefaultTimeout);
            }
            else
            {
                var values = effect!.Split(effectPrefix)[1].Trim().Split(", ");
                var expected = ability == Overload
                    ? new[] { "Stat 522 -10 33 s", "Stat 524 -10 33 s", "Stat 1009 -10 33 s", "Stat 1010 -10 33 s" }
                    : new[] { "Stat 519 -15 28 s", "Stat 115 -50 28 s", "Stat 877 -50 28 s", "Stat 878 -50 28 s", "Stat 524 -19 28 s" };
                Check.That(values.Order().SequenceEqual(expected.Order()),
                    $"Ability {ability} applies the shipped stat debuffs, got {effect}");
            }
            proof.Add(new { ability, Request = Convert.ToHexString(request), Missing = missing.Hex, Stale = stale.Hex, reuseSeconds, effect });
        }
        Check.Equal(0, caster.World.Problems.Count, "Starter debuff request framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-hacker-debuff-targets.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();
    }
}
