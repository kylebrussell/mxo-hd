using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AwakenedPowerShotCapMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, Level = 18;
    private const ushort Power = 14, Cheap = 198, Head = 197;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Power, Cheap, Head).Order().ToArray();
    private static readonly BytePattern Return = BytePattern.Parse("81 67 ?? ?? ?? ?? af 01 00 c4 01 0e 00 00 00");

    public override string Name => "ability-parity-awakened-power-shot-cap";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64," +
                "healthC=3000,healthM=3000,innerStrC=140,innerStrM=140 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, hotbarAbilities: new[] { Power, Cheap, Head }, testMobFixture: "awakened-power-shot-cap.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_MARTIAL_PLAYER_CONTROL_QA"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "14,197,198", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Awakened rank 18. CSR (17000,495,2300) faces Melee Dummy (17200,495,2300) on the clear street. Page 2 slots 1–3 are Power Shot, Cheap Shot and Head Butt. IS starts 140/140 and does not regenerate. The unarmed stock Close Combat control uses Self Defense.",
            "Enter Close Combat on Melee Dummy. Use slot 1 Power Shot first. Keep the damage and 60 s own reuse. It pays 20 IS. On a hit, [POWER-SHOT] reports an independent rolledIS of 10–100, restoredIS=min(rolledIS,20), and currentIS=120 + restoredIS, at most 140. The boost line gives actual restoredIS. On a miss there is no [POWER-SHOT] line or refund. If retrying after reuse, use the observed IS after the new debit to calculate the smaller headroom.",
            "In the same interlock use Cheap Shot in slot 2. Then type ?cd daze 30 on the current selected dummy and use Head Butt in slot 3. Record their normal cost and landed results. Neither can publish source 14's refund. No maximum [VITALS] changes. [COMBAT] rounds should continue with the same dummy."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Every ranked prerequisite fits the stock loadout");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var view = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        uint selected = DuelPackets.Handle(view.ViewId, view.SpawnId);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(selected));
        actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, selected),
            WireFormat.FromHex($"40 {handle} 01 6b"));
        await actor.WaitForRpcAsync($"40 {handle} 00 6b");
        await actor.WaitForRpcAsync("47");
        actor.SendRpc($"81 51 {handle}");

        int castAt = actor.World.RpcSnapshot().Count;
        int views = actor.World.ViewSnapshot().Count;
        int logAt = server.DebugLog().Length;
        await QueueAsync(Power);
        var expected = new Regex(@"\[POWER-SHOT\] char=110 rolledIS=(\d+) restoredIS=(\d+) currentIS=(\d+) maxIS=140");
        await actor.WaitUntilAsync(() => expected.IsMatch(server.DebugLog()[logAt..]) ||
            server.DebugLog()[logAt..].Contains("player PowerShotAbility fails"), "The queued Power Shot resolves", HeadlessPlayer.DefaultTimeout);
        var match = expected.Match(server.DebugLog()[logAt..]);
        int after = 120;
        if (match.Success)
        {
            var gain = await StockAbilityProfile.ObserveAsync(actor, castAt, frame => Return.IsMatch(frame.Bytes), "Power Shot's reported actual IS gain");
            int roll = int.Parse(match.Groups[1].Value), restored = int.Parse(match.Groups[2].Value);
            after = int.Parse(match.Groups[3].Value);
            Check.That(roll is >= 10 and <= 100 && restored == Math.Min(20, roll) && after == 120 + restored &&
                restored == BitConverter.ToInt32(gain.Bytes, gain.Bytes.Length - 4), "The exact positive gain obeys the maximum and matches the stock message");
            Console.WriteLine($"POWER SHOT CAP: rolled {roll}, granted {restored}, current {after}/140.");
        }
        await actor.WaitUntilAsync(() =>
        {
            var values = actor.World.ViewSnapshot().Skip(views).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).ToArray();
            return values.Contains((ushort)120) && values.Contains((ushort)after);
        }, "The actual debit and resulting IS agree with the actor's view", HeadlessPlayer.DefaultTimeout);

        int controls = actor.World.RpcSnapshot().Count;
        await QueueAsync(Cheap);
        await WaitForRoundAsync();
        actor.SendChat("?cd daze 30");
        await actor.WaitForTextAsync("Daze fixture applied");
        await QueueAsync(Head);
        await WaitForRoundAsync();
        Check.Equal(match.Success ? 1 : 0, actor.World.RpcSnapshot().Skip(castAt).Count(frame => Return.IsMatch(frame.Bytes)),
            "Only a landed Power Shot emits a refund");
        Check.That(!actor.World.RpcSnapshot().Skip(controls).Any(frame => Return.IsMatch(frame.Bytes)), "Neither control emits a Power Shot refund");
        Check.Equal(0, actor.World.Problems.Count, "Awakened cap batch framing");
        Console.WriteLine($"POWER SHOT CAP { (match.Success ? "LAND PASS" : "MISS PASS") }");
        return Array.Empty<byte>();

        async Task QueueAsync(ushort id)
        {
            var sample = AwakenedInterlockAdmissionMode.Samples.Single(row => row.Ability == id);
            int from = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, selected));
            await EvidenceRetestWire.TimerAsync(actor, from, id, AbilityCooldowns.OwnTimer, sample.Reuse);
        }

        async Task WaitForRoundAsync()
        {
            await actor.WaitForRpcAsync("47");
        }
    }
}
