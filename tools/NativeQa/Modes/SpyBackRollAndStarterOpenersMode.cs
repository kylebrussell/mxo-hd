using System.Text.RegularExpressions;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class SpyBackRollAndStarterOpenersMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;
    private const ushort SneakPassive = 151, SneakGrant = 1111;
    private const ushort BackRollEscape = CombatSpecials.BackRollEscape; // 122
    private const ushort CannyStrike = CannyStrikeDamage.CannyStrike;    // 295
    private const ushort Suplex = SuplexDamage.Suplex;                   // 287
    private const ushort ShadowBullDog = BullDogDamage.Id;               // 387
    private const ushort SpyBullDog = StealthShieldAbilities.BullDog;    // 1110

    private const byte Level = 50, Rank = 50;
    // Melee Dummy sits at (24499, 495, -1498) facing +Z (rotation 0).
    // Spawning 2.52 m directly behind it matches ?combatdepth fixture-reset and satisfies Sneak FOV and 4 m / 6 m ranges.
    private const int BehindDummyX = 24499, BehindDummyZ = -1750;

    private static readonly ushort[] Roots = { 2, 17, SneakPassive, SpyStyle, BackRollEscape, CannyStrike, Suplex, ShadowBullDog, SpyBullDog };
    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-spy-backroll-and-starter-openers";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, BehindDummyX, BehindDummyZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new[] { SpyStyle, SneakGrant, BackRollEscape, CannyStrike, Suplex, ShadowBullDog, SpyBullDog },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (65/100 memory), spawned 2.52 m directly behind Melee Dummy. Page 2 hotbar slots: 1=1112 Spy Style, 2=1111 Sneak, 3=122 Back Roll Escape, 4=295 Canny Strike, 5=287 Suplex, 6=387 Bull Dog (Shadow), 7=1110 Bull Dog (Spy).",
            "Step A (Close Combat: 295 Canny Strike + 122 Back Roll Escape): Select Melee Dummy, open ordinary Close Combat, and reselect Spy Style (slot 1). Press slot 4 (295 Canny Strike): expect 70 IS spent, 30.0 s own reuse, queued move '[COMBAT] CannyStrikeAbility is the next move', and landed thrown damage 13 (VF4 base 9 + 54% Lv50 closure). Next round, press slot 3 (122 Back Roll Escape): expect 25 IS spent, 12.0 s shared family 1200 reuse (no 0 s own timer), 0 damage, '[EFFECTS] 122 on Melee Dummy: State 93 0 10 s' (Enraged 10 s), and '[SPECIAL] Back Roll Escape: 110 rolls out of the fight' leaving Close Combat.",
            "Step B (1110 Bull Dog - Spy): Use ?combatdepth fixture-reset, wait, strafe/turn to reveal the fresh Melee Dummy view, click Melee Dummy, and reselect Spy Style (slot 1). Enter submits chat; Escape can holster the active style. Press slot 2 (1111 Sneak) from behind Melee Dummy, then press slot 7 (1110 Bull Dog): expect instant cast, 10 IS spent, 10.0 s own reuse & 15.0 s shared family 691 reuse, Sneak breaks ('1111 off (used ability 1110)'), knife throw animation, and '[STEALTH-FREE]1110 terminal10 OURS on Melee Dummy' (10 damage at 0.5 s).",
            "Step C (287 Suplex - Knife Thrower): Use ?combatdepth fixture-reset, wait for the 15 s family 691 timer to clear, strafe/turn to reveal the fresh Melee Dummy view, click it, reselect Spy Style (slot 1), cast Sneak (slot 2), then press slot 5 (287 Suplex): expect instant cast, 50 IS spent, 38.0 s own & 15.0 s family 691 reuse, Sneak breaks ('1111 off (used ability 287)'), landed thrown damage 646 (VF3 base 120 + 300 stealth = 420, +54% closure), and '[EFFECTS] 287 on Melee Dummy: State 200 0 15 s, State 201 0 15 s, State 199 0 15 s' (Off-Balance, Staggered, Dazed 15 s).",
            "Step D (387 Bull Dog - Shadow): Use ?combatdepth fixture-reset, wait for the 15 s family 691 timer to clear, strafe/turn to reveal the fresh Melee Dummy view, click it, reselect Spy Style (slot 1), cast Sneak (slot 2), then press slot 6 (387 Bull Dog): expect instant cast, 37 IS spent, 60.0 s own & 15.0 s family 691 reuse, Sneak breaks ('1111 off (used ability 387)'), and landed thrown damage 1578 (fixed 75 + 950 stealth = 1025, +54% closure)."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "122,287,295,387,1110",
            ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);

        // Rest refusals: 122 & 295 require close combat; 1110, 287 & 387 require a live enemy target.
        await AssertDeniedAsync(BackRollEscape, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId),
            "[ABILITY] Denied 122: native refusal 24", "Back Roll Escape refuses outside close combat");
        await AssertDeniedAsync(CannyStrike, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId),
            "[ABILITY] Denied 295: native refusal 24", "Canny Strike refuses outside close combat");
        await AssertDeniedAsync(Suplex, 0,
            "[ABILITY] Denied 287: native refusal 24", "Suplex refuses empty target");
        await AssertDeniedAsync(ShadowBullDog, 0,
            "[ABILITY] Denied 387: native refusal 24", "Shadow Bull Dog refuses empty target");
        await AssertDeniedAsync(SpyBullDog, 0,
            "[ABILITY] Denied 1110: native refusal 24", "Spy Bull Dog refuses empty target");

        // Step A: Close combat -> 295 Canny Strike, then 122 Back Roll Escape.
        await StarterInterlockDamageMode.Open(caster, dummy);
        await StarterInterlockDamageMode.Select(caster, dummy, SpyStyle);

        int cast295 = server.DebugLog().Length;
        int rpc295 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(CannyStrike, dummy));
        var land295 = new Regex($@"\[ABILITY\] 295 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => land295.IsMatch(server.DebugLog()[cast295..]),
            "Canny Strike lands in close combat", TimeSpan.FromSeconds(15));
        Check.Equal(13, int.Parse(land295.Match(server.DebugLog()[cast295..]).Groups[1].Value),
            "Canny Strike lands for 13 thrown damage at Lv50 rank 50");
        CheckTimer(rpc295, CannyStrike, AbilityCooldowns.OwnTimer, 30, "Canny Strike 30 s own reuse");

        int cast122 = server.DebugLog().Length;
        int rpc122 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(BackRollEscape, dummy));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[cast122..].Contains("[SPECIAL] Back Roll Escape:", StringComparison.Ordinal) &&
            server.DebugLog()[cast122..].Contains("[EFFECTS] 122 on Melee Dummy: State 93 0 10 s", StringComparison.Ordinal),
            "Back Roll Escape lands, applies Enraged 10 s, and rolls out of the fight", TimeSpan.FromSeconds(15));
        string log122 = server.DebugLog()[cast122..];
        Check.That(log122.Contains($"[ABILITY] 122 lands on view {dummy.ViewId} for 0", StringComparison.Ordinal),
            "Back Roll Escape lands for 0 damage");
        CheckTimer(rpc122, 1200, AbilityCooldowns.GroupTimer, 12, "Back Roll Escape 12 s family 1200 reuse");
        byte[] zeroOwn122 = AbilityCooldowns.BuildTimer(BackRollEscape, AbilityCooldowns.OwnTimer, 0, true);
        Check.That(!caster.World.RpcSnapshot().Skip(rpc122).Any(r => r.Bytes.AsSpan().SequenceEqual(zeroOwn122)),
            "Back Roll Escape does not emit a 0 s own timer");

        // Step B: Reset -> Sneak -> 1110 Bull Dog (Spy).
        dummy = await ResetAsync();
        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);
        await ActivateSneakAsync();
        int cast1110 = server.DebugLog().Length;
        int rpc1110 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(SpyBullDog, dummy));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[cast1110..].Contains("[STEALTH-FREE]1110 terminal10 OURS on Melee Dummy", StringComparison.Ordinal) &&
            server.DebugLog()[cast1110..].Contains("1111 off (used ability 1110)", StringComparison.Ordinal),
            "Spy Bull Dog 1110 breaks Sneak and applies terminal 10 damage", TimeSpan.FromSeconds(8));
        CheckTimer(rpc1110, SpyBullDog, AbilityCooldowns.OwnTimer, 10, "Spy Bull Dog 10 s own reuse");
        CheckTimer(rpc1110, 691, AbilityCooldowns.GroupTimer, 15, "Spy Bull Dog 15 s family 691 reuse");

        // Wait for shared family 691 reuse (15 s) before 287 Suplex.
        await Task.Delay(TimeSpan.FromSeconds(16));

        // Step C: Reset -> Sneak -> 287 Suplex.
        dummy = await ResetAsync();
        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);
        await ActivateSneakAsync();
        int cast287 = server.DebugLog().Length;
        int rpc287 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(Suplex, dummy));
        var land287 = new Regex($@"\[ABILITY\] 287 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => land287.IsMatch(server.DebugLog()[cast287..]),
            "Suplex lands from Sneak", TimeSpan.FromSeconds(10));
        string log287 = server.DebugLog()[cast287..];
        Check.Equal(646, int.Parse(land287.Match(log287).Groups[1].Value), "Suplex lands for 646 thrown damage");
        Check.That(log287.Contains("1111 off (used ability 287)", StringComparison.Ordinal), "Suplex breaks Sneak");
        Check.That(log287.Contains("State 200 0 15 s", StringComparison.Ordinal) &&
                   log287.Contains("State 201 0 15 s", StringComparison.Ordinal) &&
                   log287.Contains("State 199 0 15 s", StringComparison.Ordinal),
            "Suplex applies Off-Balance, Staggered and Dazed for 15 s");
        CheckTimer(rpc287, Suplex, AbilityCooldowns.OwnTimer, 38, "Suplex 38 s own reuse");
        CheckTimer(rpc287, 691, AbilityCooldowns.GroupTimer, 15, "Suplex 15 s family 691 reuse");

        // Wait for shared family 691 reuse (15 s) before 387 Bull Dog.
        await Task.Delay(TimeSpan.FromSeconds(16));

        // Step D: Reset -> Sneak -> 387 Bull Dog (Shadow).
        dummy = await ResetAsync();
        await StarterFixedDamageMode.SelectStyle(caster, self, SpyStyle, 1034);
        await ActivateSneakAsync();
        int cast387 = server.DebugLog().Length;
        int rpc387 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(ShadowBullDog, dummy));
        var land387 = new Regex($@"\[ABILITY\] 387 lands on view {dummy.ViewId} for (\d+)\b");
        await caster.WaitUntilAsync(() => land387.IsMatch(server.DebugLog()[cast387..]),
            "Shadow Bull Dog lands from Sneak", TimeSpan.FromSeconds(10));
        string log387 = server.DebugLog()[cast387..];
        Check.Equal(1578, int.Parse(land387.Match(log387).Groups[1].Value), "Shadow Bull Dog lands for 1578 thrown damage");
        Check.That(log387.Contains("1111 off (used ability 387)", StringComparison.Ordinal), "Shadow Bull Dog breaks Sneak");
        CheckTimer(rpc387, ShadowBullDog, AbilityCooldowns.OwnTimer, 60, "Shadow Bull Dog 60 s own reuse");
        CheckTimer(rpc387, 691, AbilityCooldowns.GroupTimer, 15, "Shadow Bull Dog 15 s family 691 reuse");

        Console.WriteLine("  Back Roll Escape (122), Canny Strike (295), Spy Bull Dog (1110), Suplex (287), and Shadow Bull Dog (387) verified");
        return Array.Empty<byte>();

        byte[] Use(ushort ability, ViewCreation target) =>
            new AbilityUseRecord(ability, DuelPackets.Handle(target.ViewId, target.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();

        void CheckTimer(int fromRpc, ushort id, byte kind, float seconds, string message)
        {
            byte[] expected = AbilityCooldowns.BuildTimer(id, kind, seconds, true);
            Check.That(caster.World.RpcSnapshot().Skip(fromRpc).Any(r => r.Bytes.AsSpan().SequenceEqual(expected)), message);
        }

        async Task AssertDeniedAsync(ushort ability, uint targetHandle, string expectedLog, string message)
        {
            int from = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(ability, targetHandle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            await caster.WaitUntilAsync(() => server.DebugLog()[from..].Contains(expectedLog, StringComparison.Ordinal),
                message, TimeSpan.FromSeconds(5));
        }

        async Task ActivateSneakAsync()
        {
            int from = server.DebugLog().Length;
            caster.SendRpc(new AbilityUseRecord(SneakGrant, 0, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
            await caster.WaitUntilAsync(() => server.DebugLog()[from..].Contains("[STEALTH] CSR_Tester 1111 on", StringComparison.Ordinal),
                "Sneak activates behind Melee Dummy", TimeSpan.FromSeconds(8));
        }

        async Task<ViewCreation> ResetAsync()
        {
            int at = caster.World.CreationSnapshot().Count;
            caster.SendChat("?combatdepth fixture-reset");
            await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
            await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(at)
                .Any(view => view.Name == CombatFixtureLoadout.DummyName),
                "A fresh Melee Dummy is visible after fixture-reset", HeadlessPlayer.DefaultTimeout);
            return await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        }
    }
}
