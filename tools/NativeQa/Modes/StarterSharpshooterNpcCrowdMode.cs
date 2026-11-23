using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterSharpshooterNpcCrowdMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300, DummyX = 17200, FriendZ = 2500;
    private const ushort Sharpshooter = 148;
    private const string ReturnToStand = "?tp 17000 495 2300 393 11 3 - 40";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Sharpshooter }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-sharpshooter-npc-crowd";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        StarterHackerPassiveFixture.CapRanks,
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Sharpshooter)}"
    }, new[]
    {
        CombatFixtureLoadout.SeedWeaponsFor(CombatFixtureWeapons.Rifle),
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
            "healthC=20000,healthM=20000 WHERE charId=110",
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={DummyX},y={Y},z={FriendZ}," +
            $"rotation={MobAggro.YawTowards(DummyX, FriendZ, DummyX, Z)},healthC=20000,healthM=20000 WHERE charId=6111",
        $"DELETE FROM inventory WHERE charId=6111 AND slot IN (105,107); INSERT INTO inventory(charId,goid,slot,count,purity) " +
            $"VALUES (6111,{CombatFixtureWeapons.Rifle},105,1,0)"
    }, DevCommands: true, hotbarAbilities: new ushort[] { 17 }, testMobFixture: "starter-soldier-resistance.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "148", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, Rifle 692 in hand, Sharpshooter 148 archived with all prerequisites loaded. Outdoor NAV CSR ({X},{Y},{Z}) faces Melee Dummy ({DummyX},{Y},{Z}), with QAFriend at ({DummyX},{Y},{FriendZ}). Return: {ReturnToStand}.",
            "Select Speed and Self Defense (page 2 slot 1). Right-click the dummy and choose Close Combat. Double-click fires instead. Stay within 3 m. Wait for an ordinary opposed round with no special or Withdraw. QAFriend is still a bystander: expect [SHARPSHOOTER NPC INTERLOCK] class=55 loaded148=False modifier=-15, opponents=1, and -15 on attack and defence.",
            """Use ?cd stop and wait for the HUD to clear. Return to the stand if moved, strafe and reselect. Start helper free fire with {"actor":"QAFriend","action":"ranged","target":"Melee Dummy"} before CSR reopens radial Close Combat. This order makes CSR the NPC's current opponent. Wait for the ordinary opposed round. Expect modifier=-30 and opponents=2, with both attack and defence lowered by 30.""",
            """While both links stay live, use ?qaload 148 for 0/0 and ?qaunload 148 for -30/-30 again. Send {"actor":"QAFriend","action":"stop"} without closing CSR's fight. The next ordinary opposed CSR round must return to -15/-15 and opponents=1. Use ?cd stop to finish."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in new[] { (X, Z), (DummyX, Z), (DummyX, FriendZ) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, x, Y, z, DummyX, Y, Z), $"Verified outdoor NAV and sight at ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && (home.sector, home.region, home.polygon) == (0x393, 0x11, 3),
            "Verified CSR's printed return address");

        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(friend, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        var friendTarget = await friend.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(friend, friendTarget);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout.Where(id => id != Sharpshooter).ToArray(), Sharpshooter);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Rifle);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, target);
        await actor.WaitForViewNamedAsync(friend.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        string wire = WireFormat.ToHex(BitConverter.GetBytes(handle));

        int from = await OpenAsync();
        await Expect(from, false, -15, 1);
        await StopAsync();
        actor.SendChat(ReturnToStand);
        await actor.WaitForTextAsync("Teleported to 393/11");

        // Ordinary stock rifle selection and Fire! with its companion frame from the retained free-fire regression.
        string friendWire = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(friendTarget.ViewId, friendTarget.SpawnId)));
        friend.SendRpc("3f 69 00 10 00 00 00");
        friend.SkipReceived();
        friend.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {friendWire}"),
            WireFormat.FromHex($"40 {friendWire} 00 69"));
        await friend.WaitForRpcAsync("81 68");

        from = await OpenAsync();
        await Expect(from, false, -30, 2);
        from = server.DebugLog().Length;
        await ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: true);
        await Expect(from, true, 0, 2);
        from = server.DebugLog().Length;
        await ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: false);
        await Expect(from, false, -30, 2);
        from = server.DebugLog().Length;
        friend.SendRpc("44 00");
        await Expect(from, false, -15, 1);
        await StopAsync();

        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Crowded NPC and loadout packet framing");
        Console.WriteLine("NPC CROWD BOOTSTRAP PASS: bystander, live rifle fire, load/unload and immediate return to the solo modifiers.");
        return Array.Empty<byte>();

        async Task<int> OpenAsync()
        {
            actor.SkipReceived();
            int cursor = server.DebugLog().Length;
            actor.World.SendRpcs(WireFormat.FromHex("42 00 00 00 00 00 00"),
                StarterHackerControlTargetMode.WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle),
                WireFormat.FromHex($"40 {wire} 01 6b"));
            await actor.WaitForRpcAsync($"40 {wire} 00 6b");
            await actor.WaitForRpcAsync("47");
            return cursor;
        }

        async Task StopAsync()
        {
            actor.SkipReceived();
            actor.SendChat("?cd stop");
            await actor.WaitForRpcAsync($"46 {wire} 00");
            await actor.WaitForTextAsync("Fixture combat ended.");
        }

        async Task Expect(int cursor, bool loaded, int modifier, int opponents)
        {
            var pattern = new Regex($@"\[SHARPSHOOTER NPC INTERLOCK\] CSR_Tester class=55 loaded148={loaded} modifier={modifier} " +
                $@"attack=(-?\d+)->(-?\d+) defence=(-?\d+)->(-?\d+) opponents={opponents}\b", RegexOptions.CultureInvariant);
            Match? witness = null;
            await actor.WaitUntilAsync(() => (witness = pattern.Match(server.DebugLog()[cursor..])).Success,
                $"Ordinary CSR NPC round at modifier {modifier} for {opponents} live opponents", TimeSpan.FromSeconds(24));
            int[] stats = witness!.Groups.Cast<Group>().Skip(1).Select(group => int.Parse(group.Value)).ToArray();
            Check.Equal(modifier, stats[1] - stats[0], "Shot modifier is applied to the CSR attack");
            Check.Equal(modifier, stats[3] - stats[2], "Shot modifier is applied to the CSR defence");
            Console.WriteLine(witness.Value);
        }
    }
}
