using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerDoctorUpgradeMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 6400, FriendX = 17200;
    private const ushort Doctor = 236, Upgrade = 1097, Patcher = 55, Style = CombatStyles.HackerStyle;
    private static readonly ushort[] Loaded = StockAbilityProfile.Profile(2, 17, Style, Doctor, 217, Patcher).Order().ToArray();
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passive =
        { (Doctor, Upgrade, new (ushort, int)[] { (1045, 2) }) };
    // Captured Patcher from proxy_024_chatting_hyperjump_fight, also retained by StarterHackerDefensivePassivesMode.
    private const string StockPatcher = "80b937000d005a0000000000000000000000000000000000000000002000000078f21200086b0000000000";

    public override string Name => "ability-parity-hacker-doctor-upgrade";

    public override NativeQaModePlan Plan { get; } = new(Loaded, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation={MobAggro.YawTowards(X, Z, FriendX, Z)},healthC=100,healthM=500,innerStrC=700,innerStrM=1000,alignment=1 WHERE charId=110",
        $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},healthC=500,healthM=500,alignment=1 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id=6111"
    }, DevCommands: true, hotbarAbilities: new[] { Style, Upgrade, Patcher }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "1097", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loaded.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            $"Level {Level}, {Loaded.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAFriend ({FriendX},{Y},{Z}). Doctor is loaded normally and grants one Doctor Upgrade in Actions. Page 2: Hacker Style, Doctor Upgrade, Patcher.",
            "Select Hacker Style (slot 1), /target QAFriend, and use Patcher (slot 3) below full health. Expect [ABILITY] Heal 55 -> CSR_Tester for 26 and the 43 s reuse. QAFriend receives no Patcher heal. Record the [VITALS] health change if available.",
            "Keep QAFriend selected and press Doctor Upgrade (slot 2) at rest. Save the first full stock 80 b9 49 04 request. Expect 20 IS, 30 s own timer before the on records, and [MODIFIER] on CSR only for 174 +5, 564 +7 and 520 +20, all indefinite. After Patcher reuse, repeat slot 3: the heal on CSR becomes 27. No effect record on QAFriend.",
            "Press slot 2 again to remove the three records. Type ?qaunload 236 and verify Doctor Upgrade leaves Actions and its shortcut cannot activate. Type ?qaload 236 and verify one grant returns without reactivating the toggle. After Patcher reuse and below full HP, the self heal is 26 again."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerPassiveFixture.CheckNav(context, FriendX);
        var server = context.ActualServer ?? throw new InvalidOperationException("Doctor Upgrade requires the disposable server.");
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[7]);
        Check.That(archive.Keys.ToHashSet().SetEquals(Loaded) && Loaded.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level),
            "Stock Margin has the exact legal level-30 profile");
        Check.That(Loaded.All(id => AbilityPrerequisites.Current.Closure(id).All(parent =>
            archive.TryGetValue(parent.Key, out var row) && row.Level >= parent.Value)), "Every trained ancestor has the required rank");
        Check.That(!owned.Any(row => AbilityMemory.IsAbility(row.Descriptor) && AbilityCatalog.IdOf(row.Descriptor) == Upgrade),
            "Doctor Upgrade is a live grant, never an Archive leaf");

        uint handle = await StarterHackerPassiveFixture.StartAsync(actor, friend, server);
        var proof = new List<object>();
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, Passive, cursor: 0, remove: false);
        int viewAt = actor.World.ViewSnapshot().Count;
        int castAt = actor.World.RpcSnapshot().Count;
        int logAt = server.DebugLog().Length;
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(StockPatcher, handle));
        await actor.WaitUntilAsync(() => server.DebugLog()[logAt..].Contains("Heal 55 -> CSR_Tester for 26", StringComparison.Ordinal),
            "The captured Patcher uses the exact level-30 baseline build while QAFriend is selected", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(actor, castAt, Patcher, AbilityCooldowns.OwnTimer, 43);
        await EvidenceRetestWire.ViewSinceAsync(actor, viewAt, 2, "02 80 80 80 40 ?? ??");

        int removed = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Doctor }, load: false);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, Passive, removed, remove: true);
        int restored = await StockAbilityProfile.ChangeLoadoutAsync(actor, archive, new[] { Doctor }, load: true);
        await StockAbilityProfile.CheckPassivesAsync(actor, proof, Passive, restored, remove: false);
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Doctor starter framing");
        Console.WriteLine("DOCTOR UPGRADE BOOTSTRAP PASS: captured Hacker Style and Patcher, legal Archive grant round trip. First 1097 cast awaits stock capture.");
        return Array.Empty<byte>();
    }
}
