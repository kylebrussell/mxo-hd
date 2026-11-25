using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.StockAbilityProfile;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

/// <summary>
/// Impart Invisibility (366, Master Shadow): ImpartInvisibility_Test refuses the caster, ImpartInvisibilityInit refuses an
/// interlocked or already-invisible recipient, the stored 8 m selector and sight bind the friendly player, and the landed
/// cast puts 629 Invisibility Imparted (kInvisible, 120 s) on that recipient. The shipped level-99 load prerequisite is
/// bypassed by this disposable QA loadout only.
/// </summary>
public sealed class ImpartInvisibilityMode : NativeQaMode
{
    private const int Level = 50, X = 21550, Y = 1095, Z = 16150, FriendX = 21650, FriendZ = 16250;
    private const ushort Spy = CombatStyles.SpyStyle, Impart = Stealth.ImpartInvisibilityAbility;
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Spy, Impart);

    public override string Name => "ability-parity-impart-invisibility";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        PvpOpponentsFixture.NativeSql,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},alignment=1,pvpflag=0 WHERE charId IN (110,6111,6114)",
        $"UPDATE characters SET x={X},z={Z},rotation=64,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
        $"UPDATE characters SET x={FriendX},z={FriendZ},rotation=192 WHERE charId=6111",
        $"UPDATE characters SET x=21750,z={Z},rotation=64,alignment=2 WHERE charId=6114",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
        $"DELETE FROM inventory WHERE charId IN (110,6111,6114) AND slot={CombatFixtureWeapons.HandSlot}"
    }, DevCommands: true, loadAbilityAncestors: false, hotbarAbilities: new[] { Spy, Impart },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "366",
            ["NativeCloseableRows"] = 0,
            ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory (366's shipped level-99 prerequisite is bypassed by this QA loadout only). CSR at HOME ({X},{Y},{Z}); friendly QAFriend is 1.4 m away ({FriendX},{FriendZ}). Page 2: 1=Spy Style, 2=Impart Invisibility (366).",
            "Step 1 (cast first): /target QAFriend, press 2. Save the first full 366 request. Expect 100 IS, a 1.5 s cast, 10 s reuse, and QAFriend turning invisible: '[STEALTH] QAFriend 629 on' with a 120 s record on QAFriend. Note what CSR and QAFriend each see.",
            "Step 2 (refusals, no IS, cast or timer): after the 10 s reuse press 2 again on the still-invisible QAFriend -> 'Your target is already invisible.'; clear the target (or /target yourself) and press 2 -> the self refusal; at the printed FAR stand (>8 m) -> 34. Record whether the stock client gates any of these locally."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        SectorProperties.Load(Path.Combine(context.RepositoryRoot!, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot!, "data", "navmesh"));
        var far = StarterConcealedShotTargetingMode.FindFarStand();
        Check.Equal(8d, AbilityCatalog.Find(Impart).Number("Ability/SelectionRange"), "Shipped 366 selector");
        Console.WriteLine($"READY FAR {far.Command}");

        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add("QAFriend", friend);
        await PvpOpponentsFixture.EnterAsync(friend);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        DisablingShotMode.CheckProfile(caster, Loadout, Level);
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);

        var view = await caster.WaitForViewNamedAsync(friend.Handle);
        uint handle = DuelPackets.Handle(view.ViewId, view.SpawnId);

        // Self target: ImpartInvisibility_Test.
        int log = server.DebugLog().Length;
        caster.SendRpc(Request(Impart, 0));
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[ABILITY] Denied 366: You cannot impart invisibility on yourself.", StringComparison.Ordinal),
            "366 refuses the caster", HeadlessPlayer.DefaultTimeout);

        // Landed cast on the friendly recipient (after the server's double-click window).
        await Task.Delay(TimeSpan.FromSeconds(2));
        log = server.DebugLog().Length;
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        caster.SendRpc(Request(Impart, handle));
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[STEALTH] QAFriend 629 on", StringComparison.Ordinal),
            "366 imparts 629 on QAFriend", TimeSpan.FromSeconds(6));
        Check.That(server.DebugLog()[log..].Contains("[ABILITY-COST] char=110 ability=366 paid=100", StringComparison.Ordinal), "366 pays 100 IS");
        Console.WriteLine("READY BOOTSTRAP 366 imparts 629 on QAFriend after the 1.5 s cast (100 IS)");

        // ImpartInvisibilityInit: an already-invisible recipient.
        await Task.Delay(TimeSpan.FromSeconds(10.5));
        log = server.DebugLog().Length;
        caster.SendRpc(Request(Impart, handle));
        await caster.WaitUntilAsync(() => server.DebugLog()[log..].Contains("[ABILITY] Denied 366: Your target is already invisible.", StringComparison.Ordinal),
            "366 refuses an invisible recipient", HeadlessPlayer.DefaultTimeout);

        // Stored 8 m selector.
        await Task.Delay(TimeSpan.FromSeconds(2));
        caster.SkipReceived();
        caster.SendChat(far.Command);
        await caster.WaitForTextAsync(far.Confirmation);
        view = await caster.WaitForViewNamedAsync(friend.Handle);
        await RefusedAsync(caster, Impart, Request(Impart, DuelPackets.Handle(view.ViewId, view.SpawnId)), AbilityRefusals.TargetOutOfRange);

        Check.Equal(0, caster.World.Problems.Count + friend.World.Problems.Count, "Clean stock framing");
        Console.WriteLine("IMPART INVISIBILITY PASS: self, invisible-recipient and range refusals, and the landed 629 on QAFriend.");
        return Array.Empty<byte>();
    }

    private static byte[] Request(ushort ability, uint target) =>
        new AbilityUseRecord(ability, target, 0, 0, 0, ability | (0x6bUL << 40), 8, 0x6b, 0, 0).Encode();
}
