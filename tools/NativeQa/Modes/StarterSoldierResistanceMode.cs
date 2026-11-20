using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSoldierResistanceMode : NativeQaMode
{
    private const int Level = 50, Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private sealed record Sample(ushort Id, ushort Style, string Weapon, uint Goid, int Type, int Reuse, int Move,
        string StockStyle, string StockUse, params string[] Effects);

    // Stock PR397 1088/1187, PR497 1131/1229, and PR505b 737/775.
    private static readonly Sample[] Samples =
    {
        new(145, 132, "unarmed", 0, 0, 12, 277, "80b984000400020000000000000000000000000000000000000000000000000000000000086b0000000000", "80b9910004000200000000000000000000000000000000000000000091000000006b0000086b0000000000", "Stat 522 -10 10 s", "Stat 1009 -10 10 s"),
        new(105, 147, "rifle", CombatFixtureWeapons.Rifle, 1, 8, 11364, "80b993000400020000000000000000000000000000000000000000000000000000000000086b0000000000", "80b9690004000200000000000000000000000000000000000000000069000000006b0000086b0000000000", "Stat 180 -5 10 s", "Stat 181 -5 10 s"),
        new(130, 129, "handgun", CombatFixtureWeapons.Handgun, 1, 8, 0, "80b98100040002000000000000000000000000000000000000000000585cc00001000000086b0000000000", "80b98200040002000000000000000000000000000000000000000000d557377c20000000086b0000000000", "Stat 115 -50 8 s", "Stat 877 -50 8 s", "Stat 878 -50 8 s", "Stat 519 -10 8 s")
    };
    private static readonly ushort[] Roots = Samples.SelectMany(sample => new[] { sample.Id, sample.Style })
        .Concat(new ushort[] { 2, 17 }).Distinct().ToArray();
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-soldier-resistance";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            "UPDATE characters SET y=495,healthC=4000,healthM=4000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=LEAST(level,15) WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Samples.SelectMany(sample => new[] { sample.Style, sample.Id }).ToArray(),
        testMobFixture: "awakened-interlock-resistance.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.WeaponMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "105,130,145", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Page 2 slots 1–6 are Karate, Overhand Smash, Rifles, Body Shot, Handguns and Hindering Shot. Use one neutral and one resisted dummy for each move. Start each attempt with ?combatdepth fixture-reset or the resistance-specific reset. Press Enter, strafe or turn for the new view, then reselect Melee Dummy. Do not press Escape.",
            "For Overhand Smash, enter ?combatdepth fixture-weapon unarmed first. Open Close Combat, reselect Karate, and use slot 2. Check move 277 and both -10 melee records for 10 s. Repeat after the 12 s own reuse with ?combatdepth fixture-reset-resisted for +75 Melee Damage Resistance.",
            "Equip the rifle with ?combatdepth fixture-weapon rifle. Open Close Combat, reselect Rifles, and use slot 4 on a fresh neutral dummy. Check move 11364 and -5 Melee and Ballistic Damage Resistance for 10 s. Repeat after the 8 s own reuse with ?combatdepth fixture-reset-ballistic-resisted for +75 Ballistic Damage Resistance.",
            "Equip the handgun. Select Handguns and use slot 6 from rest, without opening Close Combat. Repeat after 8 s on a fresh ballistic-resistant dummy. Check two-second cast and the eight-second movement and force-combat penalties. Expect Overhand Smash 7 to 5, Body Shot 8 to 6 and Hindering Shot 9 to 7. Retry a missed interlock round after reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Both weapon chains and Karate fit in memory");
        var server = context.ActualServer!;
        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        CombatFixtureLoadout.AssertDummy(caster, await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName));

        var timerStart = new Dictionary<ushort, int>();
        foreach (var sample in Samples)
        {
            await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, context.OwnedDatabase!, sample.Weapon, sample.Goid);
            var neutral = await LandAsync(sample, resisted: false);
            var reduced = await LandAsync(sample, resisted: true);
            Check.Equal(neutral.Final, reduced.Raw, $"Ability {sample.Id} keeps the same raw result");
            Check.Equal((int)TypedAbilityResistance.Amount(neutral.Final, Level, Level, Resistance), reduced.Final,
                $"Ability {sample.Id} applies only the matching resistance");
            Check.That(reduced.Final < neutral.Final, $"Ability {sample.Id} lands for less damage against resistance");
        }
        Check.Equal(0, caster.World.Problems.Count, "Soldier combat fixture packet framing");
        return Array.Empty<byte>();

        async Task<(int Raw, int Final)> LandAsync(Sample sample, bool resisted)
        {
            string reset = resisted ? sample.Type == 0 ? "fixture-reset-resisted" : "fixture-reset-ballistic-resisted" : "fixture-reset";
            for (int attempt = 0; attempt < 3; ++attempt)
            {
                if (timerStart.TryGetValue(sample.Id, out int prior))
                {
                    byte[] off = AbilityCooldowns.BuildTimer(sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, start: false);
                    await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(prior).Any(frame => frame.Bytes.AsSpan().SequenceEqual(off)),
                        $"Ability {sample.Id} own reuse expired", TimeSpan.FromSeconds(sample.Reuse + 3));
                }
                int creations = caster.World.CreationSnapshot().Count;
                caster.SendChat($"?combatdepth {reset}");
                await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(creations)
                    .Any(view => view.Name == CombatFixtureLoadout.DummyName), "A fresh dummy view is available", HeadlessPlayer.DefaultTimeout);
                var dummy = caster.World.CreationSnapshot().Skip(creations).Single(view => view.Name == CombatFixtureLoadout.DummyName);
                uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
                string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
                if (sample.Move != 0)
                {
                    caster.SkipReceived();
                    caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle),
                        WireFormat.FromHex($"40 {hex} 01 6b"));
                    await caster.WaitForRpcAsync($"40 {hex} 00 6b");
                    await caster.WaitForRpcAsync("47");
                }
                int views = caster.World.ViewSnapshot().Count;
                caster.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.StockStyle, handle));
                byte[] selected = PlayerViewAttributes.BuildSelf((39, PlayerViewAttributes.U32(sample.Style)));
                await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(views).Any(view => BytePattern.IndexOf(view, selected, 0) >= 0),
                    $"Select stock style {sample.Style}", HeadlessPlayer.DefaultTimeout);

                int debugAt = server.DebugLog().Length;
                int rpcs = caster.World.RpcSnapshot().Count;
                timerStart[sample.Id] = rpcs;
                caster.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.StockUse, handle));
                await EvidenceRetestWire.TimerAsync(caster, rpcs, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
                var hit = new Regex($@"\[ABILITY\] {sample.Id} lands on view {dummy.ViewId} for (\d+)\b");
                string name = AbilityCatalog.Find(sample.Id).Name;
                await caster.WaitUntilAsync(() =>
                {
                    string slice = server.DebugLog()[debugAt..];
                    return hit.IsMatch(slice) || slice.Contains($"{name} fails", StringComparison.Ordinal);
                }, $"Ability {sample.Id} resolves", HeadlessPlayer.DefaultTimeout);
                string log = server.DebugLog()[debugAt..];
                var match = hit.Match(log);
                if (!match.Success) continue;
                int final = int.Parse(match.Groups[1].Value);
                if (sample.Move != 0)
                    Check.That(log.Contains($"animation {sample.Move} (", StringComparison.Ordinal), "The existing stock move is used");
                var effects = log.Split('\n').Single(line => line.Contains($"[EFFECTS] {sample.Id} on Melee Dummy:", StringComparison.Ordinal));
                Check.That(sample.Effects.All(effect => effects.Contains(effect, StringComparison.Ordinal)),
                    $"Ability {sample.Id} retains all matching target penalties");
                if (!resisted) return (final, final);
                var mitigation = Regex.Match(log, $@"\[TYPED-RESISTANCE OURS\] {sample.Id} type{sample.Type} total{Resistance} raw(\d+) final{final}\b");
                Check.That(mitigation.Success, $"Ability {sample.Id} consumes the matching resistance");
                return (int.Parse(mitigation.Groups[1].Value), final);
            }
            throw new ScenarioFailure($"Ability {sample.Id} missed all three rounds.");
        }
    }
}
