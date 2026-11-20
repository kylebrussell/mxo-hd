using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class MasterShadowBatchMode : NativeQaMode
{
    private const ushort SpyStyle = CombatStyles.SpyStyle;                  // 1112
    private const ushort ImpartInvisibility = Stealth.ImpartInvisibilityAbility;   // 366
    private const ushort MasterShadowPassive = 367;
    private const ushort Vanish = Stealth.VanishAbility;                    // 368
    private const ushort InvisibilityImparted = Stealth.InvisibilityImpartedAbility; // 629
    private const ushort MasterShadowActive = Stealth.MasterShadowAbility;  // 1118

    private const byte Level = 50, Rank = 50;
    private const int GroundY = 495;
    // CSR at (24499, 495, -1750): Melee Dummy is 2.52 m north at (24499, 495, -1498), QAFriend is 2 m east at (24699, 495, -1750).
    private const int CasterX = 24499, CasterZ = -1750;
    private const int FriendX = 24699, FriendZ = -1750;

    private static readonly ushort[] Roots =
    {
        2, 17, SpyStyle, ImpartInvisibility, MasterShadowPassive, Vanish, InvisibilityImparted
    };

    private static readonly ushort[] Loadout = Roots
        .Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct()
        .Order()
        .ToArray();

    public override string Name => "ability-parity-master-shadow-batch";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CasterX, CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={GroundY}," +
                "healthC=3000,healthM=3000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}",
            $"UPDATE characters SET x={FriendX},y={GroundY},z={FriendZ},healthC=3000,healthM=3000,innerStrC=1000,innerStrM=1000 WHERE charId=6111"
        },
        DevCommands: true,
        loadAbilityAncestors: false,
        testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new[] { SpyStyle, MasterShadowActive, ImpartInvisibility, InvisibilityImparted, Vanish },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0",
            ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.ResetMode] = "1"
        },
        readyMessages: new[]
        {
            "Level 50, rank 50, unarmed (64/100 memory, max IS 2000). Melee Dummy is 2.52 m north; friendly QAFriend is 2 m east. Page 2 hotbar slots: 1=1112 Spy Style, 2=1118 Master Shadow (granted by 367), 3=366 Impart Invisibility, 4=629 Impart Invisibility (direct/imparted), 5=368 Vanish.",
            "Step A (367 Master Shadow passive & grant 1118): Clear target selection. Confirm Master Shadow (1118) is present on Actions/hotbar and Contested Withdraw Bonus (stat 1045) includes +5. Send '?qaunload 367' (memory 64 -> 59; stat 1045 drops -5 and grant 1118 is removed via 80 b3 5e 04), then send '?qaload 367' (memory 59 -> 64; stat 1045 +5 and grant 1118 restored via 80 b2 5e 04).",
            "Step B (1118 Master Shadow toggle & drain): With no target, press slot 2 (1118 Master Shadow). Expect 12 IS spent (2000 -> 1988), 1.5 s cast bar, NO reuse timer on activation, '[STEALTH] CSR_Tester 1118 on (bits 16, SL 0, 180 s); QAFriend sees (not an enemy)'. Wait ~6 s for the first 5 s drain (-25 IS), then press slot 2 again to toggle off: expect '[STEALTH] CSR_Tester 1118 off (switched off)' and 30.0 s own reuse timer starting on deactivation.",
            "Step C (366 Impart Invisibility -> 629 on QAFriend, then 629 direct on CSR): Select friendly QAFriend (2 m east) and press slot 3 (366 Impart Invisibility). Expect 100 IS spent, 1.5 s cast bar, 10.0 s own reuse, and '[STEALTH] QAFriend 629 on (bits 4, SL 0, 120 s); CSR_Tester sees (not an enemy)'. Then clear selection and press slot 4 (629 Impart Invisibility): expect 0 IS spent, 3.0 s cast bar, 30.0 s own reuse, '[STEALTH] CSR_Tester 629 on (bits 4, SL 0, 120 s)'. Press slot 4 again to toggle 629 off.",
            "Step D (368 Vanish in Close Combat): Select Melee Dummy (2.52 m north) and open ordinary Close Combat. Press slot 5 (368 Vanish): expect instant cast, 30 IS spent, 1800.0 s own reuse, and after 2.0 s Close Combat ends (AttackerFled) and Invisibility activates ('[STEALTH] CSR_Tester 1116 on (bits 4, SL 0, 130 s)') even next to Melee Dummy."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "366,367,368,629,1118",
            ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost),
            ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add("QAFriend", friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, friend);

        if (context.Server == null) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            $"Legal level-{Level} memory ({Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)})");

        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        var friendView = await caster.WaitForViewNamedAsync("QAFriend");

        // Step A: Unload/reload 367 Master Shadow leaf -> grant 1118 removed & restored.
        int unloadRpc = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaunload 367");
        await caster.WaitForTextAsync("Unloaded 367");
        byte[] unload1118 = ServerPackets.BuildAbilityUnload(MasterShadowActive);
        Check.That(caster.World.RpcSnapshot().Skip(unloadRpc).Any(r => r.Bytes.AsSpan().SequenceEqual(unload1118)),
            "Unloading 367 sends 80 b3 unload for grant 1118");

        int reloadRpc = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaload 367");
        await caster.WaitForTextAsync("Loaded 367");
        byte[] load1118 = ServerPackets.BuildAbilityLoad(MasterShadowActive, 0, AbilityProgression.HiddenSlot);
        Check.That(caster.World.RpcSnapshot().Skip(reloadRpc).Any(r => r.Bytes.AsSpan().SequenceEqual(load1118)),
            "Reloading 367 sends 80 b2 load for grant 1118");

        // Step B: 1118 Master Shadow toggle on (1.5 s cast, bits 16, no activation reuse), wait 5 s drain, toggle off (30 s reuse).
        int log1118 = server.DebugLog().Length;
        int rpc1118On = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(MasterShadowActive, 0));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log1118..].Contains("[STEALTH] CSR_Tester 1118 on (bits 16, SL 0, 180 s)", StringComparison.Ordinal),
            "Master Shadow 1118 activates with bits 16 and 180 s duration", TimeSpan.FromSeconds(8));
        byte[] reuse1118 = AbilityCooldowns.BuildTimer(MasterShadowActive, AbilityCooldowns.OwnTimer, 30, true);
        Check.That(!caster.World.RpcSnapshot().Skip(rpc1118On).Any(r => r.Bytes.AsSpan().SequenceEqual(reuse1118)),
            "Master Shadow 1118 does not start reuse on activation");

        await Task.Delay(TimeSpan.FromSeconds(5.5));
        int log1118Off = server.DebugLog().Length;
        int rpc1118Off = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(MasterShadowActive, 0));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log1118Off..].Contains("[STEALTH] CSR_Tester 1118 off (switched off)", StringComparison.Ordinal),
            "Master Shadow 1118 toggles off", TimeSpan.FromSeconds(5));
        Check.That(caster.World.RpcSnapshot().Skip(rpc1118Off).Any(r => r.Bytes.AsSpan().SequenceEqual(reuse1118)),
            "Master Shadow 1118 starts 30 s reuse on deactivation");

        // Step C: 366 Impart Invisibility on QAFriend -> 629 on QAFriend (120 s, bits 4), then 629 direct on CSR.
        int log366 = server.DebugLog().Length;
        int rpc366 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(ImpartInvisibility, DuelPackets.Handle(friendView.ViewId, friendView.SpawnId)));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log366..].Contains("[STEALTH] QAFriend 629 on (bits 4, SL 0, 120 s)", StringComparison.Ordinal),
            "Impart Invisibility 366 puts 629 (bits 4, 120 s) on QAFriend", TimeSpan.FromSeconds(8));
        byte[] reuse366 = AbilityCooldowns.BuildTimer(ImpartInvisibility, AbilityCooldowns.OwnTimer, 10, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpc366).Any(r => r.Bytes.AsSpan().SequenceEqual(reuse366)),
            "Impart Invisibility 366 starts 10 s own reuse");

        int log629 = server.DebugLog().Length;
        int rpc629 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(InvisibilityImparted, 0));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log629..].Contains("[STEALTH] CSR_Tester 629 on (bits 4, SL 0, 120 s)", StringComparison.Ordinal),
            "Direct 629 puts bits 4 (120 s) on CSR", TimeSpan.FromSeconds(8));
        byte[] reuse629 = AbilityCooldowns.BuildTimer(InvisibilityImparted, AbilityCooldowns.OwnTimer, 30, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpc629).Any(r => r.Bytes.AsSpan().SequenceEqual(reuse629)),
            "Direct 629 starts 30 s own reuse");
        int log629Off = server.DebugLog().Length;
        caster.SendRpc(Use(InvisibilityImparted, 0));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log629Off..].Contains("[STEALTH] CSR_Tester 629 off (switched off)", StringComparison.Ordinal),
            "Direct 629 toggles off", TimeSpan.FromSeconds(5));

        // Step D: Open Close Combat on Melee Dummy -> 368 Vanish ends combat after 2 s and enters 1116 Invisibility (130 s).
        await StarterInterlockDamageMode.Open(caster, dummy);
        int log368 = server.DebugLog().Length;
        int rpc368 = caster.World.RpcSnapshot().Count;
        caster.SendRpc(Use(Vanish, DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        await caster.WaitUntilAsync(() =>
            server.DebugLog()[log368..].Contains("[STEALTH] CSR_Tester 1116 on (bits 4, SL 0, 130 s)", StringComparison.Ordinal),
            "Vanish 368 ends Close Combat and activates 1116 Invisibility (130 s)", TimeSpan.FromSeconds(8));
        byte[] reuse368 = AbilityCooldowns.BuildTimer(Vanish, AbilityCooldowns.OwnTimer, 1800, true);
        Check.That(caster.World.RpcSnapshot().Skip(rpc368).Any(r => r.Bytes.AsSpan().SequenceEqual(reuse368)),
            "Vanish 368 starts 1800 s own reuse");

        Console.WriteLine("  Master Shadow batch (366, 367, 368, 629, 1118) verified end-to-end");
        return Array.Empty<byte>();

        static byte[] Use(ushort ability, uint targetHandle) =>
            new AbilityUseRecord(ability, targetHandle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
    }
}
