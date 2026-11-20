using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AwakenedInterlockResistanceMode : NativeQaMode
{
    private const ushort PowerShot = 14, HeadButt = 197, CheapShot = 198;
    private const int Level = 50, Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private const string PowerReturn = "81 67 ?? ?? ?? ?? af 01 00 c4 01 0e 00 00 00";

    public override string Name => "ability-parity-awakened-interlock-resistance";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, PowerShot, CheapShot, HeadButt },
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=350,innerStrM=1000 WHERE charId=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { PowerShot, CheapShot, HeadButt },
        testMobFixture: "awakened-interlock-resistance.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.ResetMode] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "14,197,198", ["NativeCloseableRows"] = 2 },
        readyMessages: new[]
        {
            "Open ordinary Close Combat unarmed in Self Defense. Land Cheap Shot (page 2 slot 2) on the neutral Melee Dummy. Reset with ?combatdepth fixture-reset, strafe or turn to reveal the fresh dummy, reselect and open again. With that target selected, ?combatdepth daze 60 and land Head Butt (slot 3). Expected neutral hits are 8 and 19.",
            "Use ?combatdepth fixture-reset-resisted instead for the next three attempts. It restores one fresh level-50 dummy with +75 Melee Damage Resistance. Strafe, reselect and open each time. Use Cheap Shot then dazed Head Butt when own reuse clears. Check the same choreography and a smaller hit for each. Repeat after reuse if a live round misses.",
            "Use Power Shot (slot 1) against a fresh resisted dummy. Verify its typed physical damage and a separate positive Inner Strength gain. Submit commands with Enter without Escape. The linked reduction is the current server model."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        ViewCreation target = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, target);

        var baselines = new Dictionary<ushort, int>();
        var timers = new Dictionary<ushort, int>();
        foreach (ushort ability in new[] { CheapShot, HeadButt })
        {
            await ResetAsync(false);
            baselines[ability] = await LandAsync(ability, false);
        }
        Check.Equal(8, baselines[CheapShot], "Cheap Shot baseline");
        Check.Equal(19, baselines[HeadButt], "Head Butt baseline");

        foreach (ushort ability in new[] { CheapShot, HeadButt, PowerShot })
        {
            await ResetAsync(true);
            await LandAsync(ability, true);
        }
        Check.Equal(0, actor.World.Problems.Count, "Awakened resistance replay framing");
        return Array.Empty<byte>();

        async Task<int> LandAsync(ushort ability, bool resisted)
        {
            var sample = AwakenedInterlockAdmissionMode.Samples.Single(sample => sample.Ability == ability);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (timers.TryGetValue(ability, out int previous))
                {
                    byte[] ready = AbilityCooldowns.BuildTimer(ability, AbilityCooldowns.OwnTimer, sample.Reuse, start: false);
                    await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(previous)
                        .Any(frame => frame.Bytes.AsSpan().SequenceEqual(ready)),
                        $"Own reuse clears for {ability}", TimeSpan.FromSeconds(sample.Reuse + 3));
                }

                uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
                string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
                actor.SkipReceived();
                actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle),
                    WireFormat.FromHex($"40 {hex} 01 6b"));
                await actor.WaitForRpcAsync($"40 {hex} 00 6b");
                await actor.WaitForRpcAsync("47");
                actor.SendRpc($"81 51 {hex}");
                if (ability == HeadButt)
                {
                    actor.SendChat("?combatdepth daze 60");
                    await actor.WaitForTextAsync("Daze fixture applied 884/199 to Melee Dummy for 60 seconds");
                }

                int logAt = server.DebugLog().Length;
                int rpcAt = actor.World.RpcSnapshot().Count;
                timers[ability] = rpcAt;
                actor.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.Capture, handle));
                await EvidenceRetestWire.TimerAsync(actor, rpcAt, ability, AbilityCooldowns.OwnTimer, sample.Reuse);
                var hit = new Regex($@"\[ABILITY\] {ability} lands on view {target.ViewId} for (\d+)\b");
                await actor.WaitUntilAsync(() =>
                {
                    string log = server.DebugLog()[logAt..];
                    return hit.IsMatch(log) || log.Contains($"{sample.AbilityName} fails", StringComparison.Ordinal);
                }, $"Ability {ability} resolves in the live interlock", HeadlessPlayer.DefaultTimeout);
                string resolved = server.DebugLog()[logAt..];
                var landed = hit.Match(resolved);
                if (!landed.Success)
                {
                    await ResetAsync(resisted);
                    continue;
                }

                int damage = int.Parse(landed.Groups[1].Value);
                if (resisted)
                {
                    var reduction = Regex.Match(resolved, $@"\[TYPED-RESISTANCE OURS\] {ability} type0 total{Resistance} raw(\d+) final{damage}\b");
                    Check.That(reduction.Success, $"Ability {ability} consumes the selected dummy's physical resistance");
                    int raw = int.Parse(reduction.Groups[1].Value);
                    if (baselines.TryGetValue(ability, out int baseline))
                        Check.Equal(baseline, raw, $"Ability {ability} retains its matched neutral base");
                    Check.Equal((int)TypedAbilityResistance.Amount(raw, Level, Level, Resistance), damage,
                        $"Ability {ability} applies the typed reduction once");
                    Check.That(damage < raw, "Physical mitigation lowers the landed hit");
                }

                if (ability == PowerShot)
                {
                    var returned = await StockAbilityProfile.ObserveAsync(actor, rpcAt,
                        frame => BytePattern.Parse(PowerReturn).IsMatch(frame.Bytes), "Power Shot still restores Inner Strength");
                    int gain = BitConverter.ToInt32(returned.Bytes.AsSpan(returned.Bytes.Length - 4));
                    Check.That(gain is >= 10 and <= 100, "Power Shot still reports its independent Inner Strength gain");
                }
                return damage;
            }
            throw new InvalidOperationException($"Ability {ability} missed all three live rounds.");
        }

        async Task ResetAsync(bool resisted)
        {
            int before = actor.World.CreationSnapshot().Count;
            actor.SkipReceived();
            actor.SendChat(resisted ? "?combatdepth fixture-reset-resisted" : "?combatdepth fixture-reset");
            await actor.WaitForTextAsync(resisted ? "Melee Dummy restored with +75" : "Fixture reset: Melee Dummy restored.");
            await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(before)
                .Any(view => view.Name == CombatFixtureLoadout.DummyName), "The reset exposes a fresh dummy view", HeadlessPlayer.DefaultTimeout);
            target = actor.World.CreationSnapshot().Skip(before).Last(view => view.Name == CombatFixtureLoadout.DummyName);
        }
    }
}
