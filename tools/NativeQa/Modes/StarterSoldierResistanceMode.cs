using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSoldierResistanceMode : NativeQaMode
{
    private const int Level = 50, Resistance = CombatFixtureWeapons.DummyPhysicalResistance;
    private const int X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    internal sealed record Sample(ushort Id, ushort Style, string Weapon, uint Goid, int Type, int Reuse, int Move,
        string StockStyle, string StockUse, params string[] Effects);

    // Stock PR397 1088/1187, PR497 1131/1229, and PR505b 737/775.
    internal static readonly Sample[] Samples =
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
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            CombatFixtureLoadout.SeedWeapons,
            $"UPDATE characters SET x={X},y={Y},z={Z},rotation=64,healthC=4000,healthM=4000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=LEAST(level,15) WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: Samples.SelectMany(sample => new[] { sample.Style, sample.Id }).ToArray(),
        testMobFixture: "starter-soldier-resistance.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1", [CombatFixtureWeapons.LiveResetMode] = "1", [CombatFixtureWeapons.WeaponMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "105,130,145", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Outdoor NAV CSR ({X},{Y},{Z}) faces Melee Dummy ({DummyX},{Y},{Z}) east. Strafe and reselect if picking misses. No helper or bag actions.",
            "At rest with the dummy selected, try Overhand Smash after selecting Karate and Body Shot after selecting Rifles with the rifle equipped. The stock shortcuts stay disabled. If either request reaches the server, expect reason 24 without any [ABILITY-COST], damage or reuse.",
            "Page 2 slots 1–6 are Karate, Overhand Smash, Rifles, Body Shot, Handguns and Hindering Shot. Compare one neutral and one resisted pass on the same passive dummy. Start each attempt with ?combatdepth fixture-reset or the resistance-specific reset. When the ready line names the existing view and the combat HUD clears, click the same dummy. Press Enter to submit commands. Do not press Escape.",
            "For Overhand Smash, enter ?combatdepth fixture-weapon unarmed first. Open Close Combat, reselect Karate, and use slot 2. Check move 277 and both -10 melee records for 10 s. Repeat after the 12 s own reuse with ?combatdepth fixture-reset-resisted for +75 Melee Damage Resistance.",
            "Equip the rifle with ?combatdepth fixture-weapon rifle. Open Close Combat, reselect Rifles, and use slot 4 on the neutral dummy. Check move 11364 and -5 Melee and Ballistic Damage Resistance for 10 s. Repeat after the 8 s own reuse with ?combatdepth fixture-reset-ballistic-resisted for +75 Ballistic Damage Resistance.",
            "Equip the handgun. Select Handguns and use slot 6 from rest, without opening Close Combat. Repeat after 8 s on the ballistic-resistant dummy. Check two-second cast and the eight-second movement and force-combat penalties. Expect Overhand Smash 7 to 5, Body Shot 8 to 6 and Hindering Shot 9 to 7. Retry a missed interlock round after reuse."
        });

    public override Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Outdoor Soldier stand ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "The Soldier stand has a clear path to the dummy");
        return VerifyAsync(context, Loadout, Samples, verifyAdmission: true);
    }

    internal static async Task<byte[]> VerifyAsync(NativeQaBootstrapContext context, ushort[] loadout, Sample[] samples, uint? weaponAtLogin = null, bool verifyAdmission = false)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var archive = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            archive.Keys.ToHashSet().SetEquals(loadout) && loadout.All(id => AbilityPrerequisites.Current.Closure(id)
                .All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "The full ranked weapon and move profile fits in memory");
        var server = context.ActualServer!;
        if (weaponAtLogin.HasValue) await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(caster, weaponAtLogin.Value);
        else await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        var timerStart = new Dictionary<ushort, int>();
        uint? equipped = weaponAtLogin;
        ushort previousAbility = 0;
        foreach (var sample in samples)
        {
            if (AbilityCooldowns.TryGet(previousAbility, out var previous) && previous.Group != 0 &&
                AbilityCooldowns.TryGet(sample.Id, out var next) && previous.Group == next.Group)
            {
                await EvidenceRetestWire.TimerAsync(caster, timerStart[previousAbility], previous.Group,
                    AbilityCooldowns.GroupTimer, previous.GroupSeconds, ended: true);
            }
            if (equipped != sample.Goid)
                await CombatFixtureLoadout.VerifyWeaponSwapAsync(caster, context.OwnedDatabase!, sample.Weapon, sample.Goid);
            equipped = sample.Goid;
            var neutral = await LandAsync(sample, resisted: false);
            var reduced = await LandAsync(sample, resisted: true);
            Check.Equal(neutral.Final, reduced.Raw, $"Ability {sample.Id} keeps the same raw result");
            Check.Equal((int)TypedAbilityResistance.Amount(neutral.Final, Level, Level, Resistance), reduced.Final,
                $"Ability {sample.Id} applies only the matching resistance");
            Check.That(reduced.Final < neutral.Final, $"Ability {sample.Id} lands for less damage against resistance");
            previousAbility = sample.Id;
        }
        Check.Equal(0, caster.World.Problems.Count, "Weapon resistance fixture packet framing");
        return Array.Empty<byte>();

        async Task<(int Raw, int Final)> LandAsync(Sample sample, bool resisted)
        {
            string reset = resisted ? sample.Type == 0 ? "fixture-reset-resisted" : "fixture-reset-ballistic-resisted" : "fixture-reset";
            for (int attempt = 0; attempt < 5; ++attempt)
            {
                if (timerStart.TryGetValue(sample.Id, out int prior))
                {
                    byte[] off = AbilityCooldowns.BuildTimer(sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse, start: false);
                    await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(prior).Any(frame => frame.Bytes.AsSpan().SequenceEqual(off)),
                        $"Ability {sample.Id} own reuse expired", TimeSpan.FromSeconds(sample.Reuse + 3));
                }
                int creations = caster.World.CreationSnapshot().Count;
                int resetViews = caster.World.ViewSnapshot().Count;
                caster.SkipReceived();
                caster.SendChat($"?combatdepth {reset}");
                await caster.WaitForTextAsync($"Fixture ready: Melee Dummy view {dummy.ViewId}, spawn {dummy.SpawnId}");
                Check.That(!caster.World.CreationSnapshot().Skip(creations)
                    .Any(view => view.Name == CombatFixtureLoadout.DummyName), "Preparing the same dummy never allocates a new view");
                byte[] deleted = { 1, 1, 0, (byte)dummy.ViewId, (byte)(dummy.ViewId >> 8) };
                Check.That(!caster.World.ViewSnapshot().Skip(resetViews).Any(view => BytePattern.IndexOf(view, deleted, 0) >= 0),
                    "Preparing the same dummy never deletes the displayed view");
                uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
                string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
                caster.SendRpc($"81 51 {hex}");
                if (sample.Move != 0)
                {
                    if (verifyAdmission && !resisted && attempt == 0)
                    {
                        await SelectStyleAsync(sample, handle);
                        await StarterHackerControlTargetMode.RefusedAsync(caster, sample.Id,
                            StarterHackerControlTargetMode.WithHandle(sample.StockUse, handle));
                    }
                    caster.SkipReceived();
                    caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle),
                        WireFormat.FromHex($"40 {hex} 01 6b"));
                    await caster.WaitForRpcAsync($"40 {hex} 00 6b");
                    await caster.WaitForRpcAsync("47");
                }
                await SelectStyleAsync(sample, handle);

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
                if (sample.Effects.Length > 0)
                {
                    var effects = log.Split('\n').Single(line => line.Contains($"[EFFECTS] {sample.Id} on Melee Dummy:", StringComparison.Ordinal));
                    Check.That(sample.Effects.All(pattern => Regex.IsMatch(effects, pattern)),
                        $"Ability {sample.Id} retains all matching target penalties");
                }
                if (!resisted) return (final, final);
                var mitigation = Regex.Match(log, $@"\[TYPED-RESISTANCE OURS\] {sample.Id} type{sample.Type} total{Resistance} raw(\d+) final{final}\b");
                Check.That(mitigation.Success, $"Ability {sample.Id} consumes the matching resistance");
                return (int.Parse(mitigation.Groups[1].Value), final);
            }
            throw new ScenarioFailure($"Ability {sample.Id} missed all five attempts.");
        }

        async Task SelectStyleAsync(Sample sample, uint handle)
        {
            int views = caster.World.ViewSnapshot().Count;
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(sample.StockStyle, handle));
            byte[] selected = PlayerViewAttributes.BuildSelf((39, PlayerViewAttributes.U32(sample.Style)));
            await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(views).Any(view => BytePattern.IndexOf(view, selected, 0) >= 0),
                $"Select stock style {sample.Style}", HeadlessPlayer.DefaultTimeout);
        }
    }
}
