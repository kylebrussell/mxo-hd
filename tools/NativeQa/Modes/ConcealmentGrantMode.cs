using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class ConcealmentGrantMode : NativeQaMode
{
    private const ushort Parent = 503, Concealment = Stealth.ConcealmentGrantAbility, Aura = 42, Awareness = 293;
    private const ushort HackerStyle = CombatStyles.HackerStyle, Rifles = CombatStyles.Rifles;
    private const int Level = 37, ConcealmentRank = 19, X = 17000, Y = 495, Z = 7400, FriendX = 16800;
    private const int FarZ = 9900, WatchingZ = 9000;
    private const string StockAura = "80b92a00000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    // PR429 captured the stock rank-18 grant's ordinary action (7483f644ff39:1600).
    private const string StockConcealment = "80b97104000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private static readonly ushort[] Loadout = DisablingShotMode.Closure(2, 17, Rifles, Parent, HackerStyle, Aura).Order().ToArray();
    private static readonly ushort[] EnemyLoadout = Profile(2, 17, Awareness).Order().ToArray();

    public override string Name => "ability-parity-concealment-grant";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(), new[]
    {
        CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
        $"UPDATE characters SET x={X},y={Y},z={Z},rotation=0,alignment=1,pvpflag=0,level={Level}," +
            $"exp={PlayerProgression.ExperienceForLevel(Level)},innerStrC=300,innerStrM=300 WHERE charId=110",
        $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
        $"UPDATE char_abilities SET level={ConcealmentRank} WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Parent)}",
        $"UPDATE characters SET x={X},y={Y},z={FarZ},rotation=128,alignment=2,pvpflag=0,level=50," +
            $"exp={PlayerProgression.ExperienceForLevel(50)},healthC=15000,healthM=15000,innerStrC=400,innerStrM=400 WHERE charId=6114",
        $"UPDATE characters SET x={FriendX},y={Y},z={Z},rotation=64,alignment=1,pvpflag=0,level={Level}," +
            $"exp={PlayerProgression.ExperienceForLevel(Level)} WHERE charId=6111",
        "DELETE FROM char_abilities WHERE char_id=6114",
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
            string.Join(",", EnemyLoadout.Select((id, i) => $"(6114,{400 + i},{AbilityMessages.AbilityGameObject(id)},50,1)"))
    }, DevCommands: true, hotbarAbilities: new[] { HackerStyle, Aura, Rifles, Concealment }, environmentOverrides: new()
    {
        ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1",
        ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
        [CombatFixtureWeapons.DisengageMode] = "1"
    }, extraQaMetadata: new() { ["ScopedStockRows"] = "1137", ["NativeCloseableRows"] = 1, ["NativeCloseableIds"] = "1137" },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory, full prerequisites, 503 rank {ConcealmentRank}, ordinary Sneak not loaded. Page 2 slots 1-4 Hacker Style / Combat Aura / Rifles / Concealment. Stay on outdoor CSR ({X},{Y},{Z}), facing north. QAFriend ({FriendX},{Y},{Z}) is allied. QAEnemy starts at ({X},{Y},{FarZ}), 25 m north and facing CSR. All stands and sight verified.",
            """Select Rifles. Send helper {"actor":"QAEnemy","action":"concealment-grant","phase":"watch"}; QAEnemy moves to (17000,495,9000) and faces CSR at 16 m. Press slot 4 once from rest. Expect one refusal, no cast bar, no 1137 records and no IS/reuse. Then /target QAEnemy. In Actions > Weapons, drag Automatic Rifle onto the figure icon for the hotbar Free Fire button. If the stance says ready, wait for idle before clicking the Free Fire button to fight. While firing, slot 4 is suppressed or reason 21, again with no cost. End the shots with ?cd stop.""",
            """Send helper {"actor":"QAEnemy","action":"concealment-grant","phase":"reset"}. Press slot 1 then slot 2 to enable Combat Aura 42. Immediately press Rifles (slot 3) then Concealment (slot 4). Expect a 2 s cast, 20 IS, 15 s reuse, [MODIFIER] source=1137 stat=209 add +19, 623 and 881 add, own/ally stance 1a, [AURA] 42 off and no further aura ticks after activation. The starting enemy stays outside the 23 m detection range and cannot see CSR. QAFriend keeps the view.""",
            """Send helper {"actor":"QAEnemy","action":"concealment-grant","phase":"reveal"}. It uses stock Countermeasures (+80 Awareness) and returns to the 16 m stand facing CSR. Expect [STEALTH] QAEnemy sees CSR_Tester (SA 93 SL 19 ...), then exactly one 1137 off (revealed), matching 209/623/881 removals, own/ally stance 00 and a fresh CSR view on the hostile helper. PR768/778/780 already check damage break, active grant unload and pending cast cancellation. No Concealed Shot or armor-piercing claim."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (FriendX, Z), (X, WatchingZ), (X, (FarZ + WatchingZ) / 2), (X, FarZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Verified outdoor Concealment stand and sight ({x},{Y},{z})");
        var server = context.ActualServer!;
        var enemy = await HelperAsync("QAEnemy");
        var friend = await HelperAsync("QAFriend");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        DisablingShotMode.CheckProfile(actor, Loadout, Level);
        var profile = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.Equal((ushort)ConcealmentRank, profile[Parent].Level, "Legal rank-19 grant on a different character level");
        Check.That(!profile.ContainsKey(151) && !profile.ContainsKey(Stealth.SneakGrantAbility), "No ordinary Sneak narrows the observing range");
        await PvpOpponentsFixture.EnterAsync(actor);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        await ObserveAsync(actor, 0, GrantRecord(Concealment, false, ConcealmentRank), "Rank-19 stock grant at login");
        var allyView = await friend.WaitForViewNamedAsync(actor.Handle);
        await enemy.WaitForViewNamedAsync(actor.Handle);
        int wrongStyleAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockConcealment);
        await ObserveRefusalAsync(wrongStyleAt, AbilityRefusals.WrongStyle);
        actor.SendRpc(DisablingShotMode.StockRifles);
        await ObserveAsync(actor, 0, frame => Bonus(frame, Rifles, 524, 48, 0, false), "Stock Rifles is active");

        await SendPhaseAsync(enemy, "watch");
        int refusedAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockConcealment);
        await ObserveRefusalAsync(refusedAt, AbilityRefusals.NoClientMessage);
        await actor.WaitForTextAsync("You cannot use Concealment while an enemy is watching.");
        actor.SendRpc("3f 69 00 10 00 00 00");
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)));
        actor.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {handle}"), WireFormat.FromHex($"40 {handle} 00 69"));
        await actor.WaitForRpcAsync("4e 00");
        await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds));
        int inCombatAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockConcealment);
        await ObserveRefusalAsync(inCombatAt, AbilityRefusals.MustBeOutsideCombat);
        actor.SendChat("?cd stop");
        await actor.WaitForTextAsync("Fixture combat ended");
        Check.That(!server.DebugLog().Contains("ability=1137 paid=", StringComparison.Ordinal), "Admission refusals cost no IS");
        await SendPhaseAsync(enemy, "reset");

        int auraAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        actor.SendRpc(StockAura);
        await ObserveToggleAsync(Aura, auraAt, on: true);
        int riflesAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(DisablingShotMode.StockRifles);
        await ObserveAsync(actor, riflesAt, frame => Bonus(frame, Rifles, 524, 48, 0, false), "Return to Rifles with the aura still on");
        int ownPoseAt = actor.World.ViewSnapshot().Count, allyPoseAt = friend.World.ViewSnapshot().Count;
        int activeAt = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockConcealment);
        await EvidenceRetestWire.CastBarAsync(actor, Concealment, 2);
        await EvidenceRetestWire.TimerAsync(actor, activeAt, Concealment, AbilityCooldowns.OwnTimer, 15);
        await ObserveToggleAsync(Concealment, activeAt, on: true);
        await ObserveToggleAsync(Aura, activeAt, on: false);
        await ObserveModifiersAsync(activeAt, remove: false);
        await ObservePoseAsync(actor, ownPoseAt, 2, on: true);
        await ObservePoseAsync(friend, allyPoseAt, allyView.ViewId, on: true);

        int releaseAt = actor.World.RpcSnapshot().Count;
        int enemyViews = enemy.World.CreationSnapshot().Count;
        ownPoseAt = actor.World.ViewSnapshot().Count;
        allyPoseAt = friend.World.ViewSnapshot().Count;
        await SendPhaseAsync(enemy, "reveal");
        await ObserveToggleAsync(Concealment, releaseAt, on: false);
        await ObserveModifiersAsync(releaseAt, remove: true);
        await ObservePoseAsync(actor, ownPoseAt, 2, on: false);
        await ObservePoseAsync(friend, allyPoseAt, allyView.ViewId, on: false);
        await enemy.WaitUntilAsync(() => enemy.World.CreationSnapshot().Skip(enemyViews).Any(view => view.Name == actor.Handle),
            "A real hostile observer regains the revealed player", HeadlessPlayer.DefaultTimeout);
        string proof = server.DebugLog();
        Check.That(proof.Contains("QAEnemy sees CSR_Tester (SA 93 SL 19 d 1600", StringComparison.Ordinal) &&
            proof.Contains("1137 off (revealed)", StringComparison.Ordinal), "Concealment rank feeds the live periodic contest");
        Check.Equal(0, actor.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count, "Concealment packet framing");
        Console.WriteLine("CONCEALMENT GRANT BOOTSTRAP PASS: stock watching refusal, pose 1a to 00, aura off, and hostile 16 m reveal (SA 93 / SL 19).");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> HelperAsync(string handle)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            return helper;
        }

        Task ObserveRefusalAsync(int cursor, uint reason) => ObserveAsync(actor, cursor,
            frame => AbilityRefusals.TryDecode(frame.Bytes, out var refusal) && refusal.AbilityID == Concealment && refusal.ErrorID == reason,
            $"Concealment admission refusal {reason}");

        Task ObserveToggleAsync(ushort id, int cursor, bool on) => ObserveAsync(actor, cursor,
            rpc => rpc.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(id, on)), $"Stock toggle {id} on={on}");

        async Task ObserveModifiersAsync(int cursor, bool remove)
        {
            foreach (var (stat, value) in new (ushort, int)[] { (209, ConcealmentRank), (623, 0), (881, 0) })
                await ObserveAsync(actor, cursor, frame => Bonus(frame, Concealment, stat, value, 0, remove),
                    $"Concealment stat/state {stat} remove={remove}");
        }
    }

    internal static async Task SendPhaseAsync(HeadlessPlayer observer, string phase)
    {
        if (observer.Handle != "QAEnemy" || phase is not ("watch" or "reset" or "reveal"))
            throw new InvalidOperationException("Use QAEnemy and a watch, reset or reveal phase.");
        if (phase == "reveal")
        {
            int cursor = observer.World.RpcSnapshot().Count;
            observer.SendRpc(StarterStealthObserverMode.StockCountermeasures);
            await ObserveAsync(observer, cursor, frame => Bonus(frame, Awareness, 204, 80, 30, false), "Countermeasures adds 80 Awareness");
        }
        // Same stock self-position layout as the recorded Sneak observer. Only coordinates change.
        foreach (int z in new[] { (FarZ + WatchingZ) / 2, phase == "reset" ? FarZ : WatchingZ })
        {
            var position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes);
            observer.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            observer.SkipReceived();
            observer.SendRpc("81 48");
            await observer.WaitForRpcAsync("81 49");
        }
    }

    private static Task ObservePoseAsync(HeadlessPlayer recipient, int cursor, ushort view, bool on)
    {
        string header = view == 2 ? "01 28" : "01 2a";
        return EvidenceRetestWire.ViewSinceAsync(recipient, cursor, view, $"{header} ?? 40 {(on ? "1a" : "00")} 00 00 10");
    }
}
