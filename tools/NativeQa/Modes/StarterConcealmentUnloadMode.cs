using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterConcealmentUnloadMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 6400, EnemyX = 17400, FriendX = 16800;
    private const ushort Rifles = 147, Parent = 503, Conceal = Stealth.ConcealmentGrantAbility, Shot = CombatSpecials.ConcealedShot;
    // PR429 stock 1137 (7483f644ff39:1600–1601) and PR410 stock 504 (3ef3dc4c8f4c:2400–2401).
    private const string ConcealFrame = "80b97104000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string ShotFrame = "80b9f801040002000000000000000000000000000000000000000000585cc10001000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Rifles, Parent, Shot }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-concealment-unload";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
        StarterHackerPassiveFixture.CapRanks
    }, new[]
    {
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
            "healthC=1000,healthM=1000,innerStrC=400,innerStrM=400,alignment=1,pvpflag=0 WHERE charId=110",
        $"UPDATE characters SET x={EnemyX},y={Y},z={Z},rotation=64,alignment=2,level=10," +
            $"exp={PlayerProgression.ExperienceForLevel(10)},pvpflag=0,healthC=1000,healthM=1000 WHERE charId=6114",
        $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=64,alignment=1,level={Level}," +
            $"exp={PlayerProgression.ExperienceForLevel(Level)},pvpflag=0 WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id IN (6111,6114) AND ability_id NOT IN (2147485696,2147501056)"
    }, DevCommands: true, hotbarAbilities: new[] { Rifles, Conceal, Shot }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "6"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "503,1137,504", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, 42/49 memory, rifle equipped. CSR ({X},{Y},{Z}) faces QAEnemy ({EnemyX},{Y},{Z}) from 4 m behind, QAFriend ({FriendX},{Y},{Z}) observes. All stands on outdoor NAV floor 400. Page 2 is Rifles, Concealment and Concealed Shot. Use /target QAEnemy when needed and keep the selection without Escape.",
            "Choose Rifles, then press slot 2. Wait for [STEALTH] 1137 on (bits 2, SL 19). Type ?qaunload 504 followed by ?qaload 504. Both should round-trip through 80 ae while Concealment remains active.",
            "Type ?qaunload 503 while still concealed. Expect one [STEALTH] 1137 off (grant unloaded), one child unload, and [MODIFIER] removal of source 1137's Stealth +19 and states 623/881 in PacketLog. The owner and friendly observer clear Sneak and the Concealed/Stealthed flags. Without using slot 2 again, try slot 3. Expect the local Concealment requirement or one reason 23 (0x00040000) without an IS debit, cast bar, hit or timer.",
            "Type ?qaload 503. The grant returns once with no active state. For the scope control, type ?stealth sneak, then ?qaunload 503. The separate 1111 Sneak remains active. Use ?stealth off to clean up. No max [VITALS] changes or [PVP] rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        StarterHackerPassiveFixture.CheckNav(context, EnemyX);
        StarterHackerPassiveFixture.CheckNav(context, FriendX);
        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFriend" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await helper.EnterWorldAsync();
            await helper.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        await actor.WaitForViewUpdateAsync(2, PvpOpponentsFixture.SelfFlag);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var friend = context.Helpers["QAFriend"];
        var friendView = await friend.WaitForViewNamedAsync(actor.Handle);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        await StarterFixedDamageMode.SelectStyle(actor, self, Rifles, 171);
        int start = actor.World.RpcSnapshot().Count;
        actor.SendRpc(ConcealFrame);
        await EvidenceRetestWire.CastBarAsync(actor, Conceal, 2);
        await ObserveToggleAsync(actor, Conceal, true, start);

        int prior = await ChangeLoadoutAsync(actor, archive, new[] { Shot }, load: false);
        await ChangeLoadoutAsync(actor, archive, new[] { Shot }, load: true);
        Check.That(!SawToggle(actor, prior, Conceal, false), "Removing and restoring the shot leaves the originating Concealment active");

        int ownerViewAt = actor.World.ViewSnapshot().Count;
        int observerAt = friend.World.ViewSnapshot().Count;
        int unload = await ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        await ObserveAsync(actor, unload, GrantRecord(Conceal, remove: true), "Removing its parent withdraws the 1137 action");
        await ObserveToggleAsync(actor, Conceal, false, unload);
        foreach (var (stat, value) in new (ushort, int)[] { (209, Level), (Stealth.ConcealedAbility, 0), (Stealth.StealthStateAbility, 0) })
            await ObserveAsync(actor, unload, frame => Bonus(frame, Conceal, stat, value, 0, true),
                $"Unloading the parent removes 1137's modifier {stat}");
        await EvidenceRetestWire.ViewSinceAsync(actor, ownerViewAt, 2, WireFormat.ToHex(Stealth.BuildSelf(0, PvpFlag.PvpBit)[2..]));
        await EvidenceRetestWire.ViewSinceAsync(friend, observerAt, friendView.ViewId, WireFormat.ToHex(Stealth.BuildOther(0, PvpFlag.PvpBit)));
        Check.Equal(1, actor.World.RpcSnapshot().Skip(unload)
            .Count(frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Conceal, false))),
            "The originating active state is released once");

        var target = await actor.WaitForViewNamedAsync("QAEnemy");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        var shot = StarterHackerControlTargetMode.WithHandle(ShotFrame, handle);
        var refused = await StarterHackerControlTargetMode.RefusedAsync(actor, Shot, shot, AbilityRefusals.CasterStateRequired);
        Check.That(AbilityRefusals.TryDecode(refused.Bytes, out var gate) && gate.StatesNotMet == Stealth.ConcealmentFlag,
            "With 504 still loaded the former Concealment no longer admits its shot");

        int reload = await ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: true);
        await ObserveAsync(actor, reload, GrantRecord(Conceal, remove: false, rank: Level), "The parent restores the action at rank 19");
        Check.That(!SawToggle(actor, reload, Conceal, true), "Grant restoration leaves the state off");
        int scope = actor.World.RpcSnapshot().Count;
        actor.SendChat("?stealth sneak");
        await ObserveToggleAsync(actor, Stealth.SneakGrantAbility, true, scope);
        int scopeUnload = await ChangeLoadoutAsync(actor, archive, new[] { Parent }, load: false);
        await ObserveAsync(actor, scopeUnload, GrantRecord(Conceal, remove: true), "The 503 grant is removed with an independent state active");
        Check.That(!SawToggle(actor, scopeUnload, Stealth.SneakGrantAbility, false), "The Sneak scope control remains active");
        actor.SendChat("?stealth off");
        await ObserveToggleAsync(actor, Stealth.SneakGrantAbility, false, scopeUnload);
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Concealment grant unload framing");
        return Array.Empty<byte>();
    }

    private static Task<RpcRecord> ObserveToggleAsync(HeadlessPlayer actor, ushort ability, bool on, int since) =>
        ObserveAsync(actor, since, frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(ability, on)),
            $"{ability} toggles {(on ? "on" : "off")}");

    private static bool SawToggle(HeadlessPlayer actor, int since, ushort ability, bool on) =>
        actor.World.RpcSnapshot().Skip(since).Any(frame => frame.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(ability, on)));
}
