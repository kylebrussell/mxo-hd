namespace HeadlessClient;

/// <summary>
/// Fights and duels between players (docs/PVP.md). The client requests are written from client.dll's record classes and
/// the retail captures; what is checked on the way back is what those captures show each side receiving.
/// </summary>
public static partial class Scenarios
{
    // A property: the scenario list in Scenarios.cs is built before this file's static fields are
    private static IReadOnlyDictionary<string, string> PvpServer => new Dictionary<string, string>
    {
        ["MXO_MOB_WANDER"] = "0", ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0",
        ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
    };

    /// <summary>A player as the owner of this view names them: view, spawn id, 00.</summary>
    private static string HandleOf(ViewCreation view) => $"{Le(view.ViewId)} {view.SpawnId:x2} 00";

    /// <summary>The player as their own client names them: view 2 and the self-spawn id.</summary>
    private static string SelfHandle(HeadlessPlayer player) => HandleOf(player.World.CreationSnapshot().Last(c => c.ViewId == 2));

    private const string OwnPvpFlagOn = "02 80 80 80 80 80 80 01 00 00 00 01";
    private const string OtherPvpFlagOn = "02 80 80 80 80 04 00 00 00 01";
    private const string OwnDeath = "02 80 80 80 c1 01 00 00 80 80 01 00 00 00 01";
    private const string OtherDeath = "02 80 80 12 01 00 00";

    // RPC 45: won at 5, own has-ability byte at 7, the opponent's at 26, target at 49, result at 61, damage to self at 69
    // and to the opponent at 73
    private static uint RoundResult(RpcRecord round) => BitConverter.ToUInt32(round.Bytes, 61);
    private static ushort DamageToSelf(RpcRecord round) => BitConverter.ToUInt16(round.Bytes, 69);
    private static ushort DamageToOpponent(RpcRecord round) => BitConverter.ToUInt16(round.Bytes, 73);

    /// <summary>
    /// A player who isn't flagged can't be attacked; flagged, a player of another organisation can. The interlock then
    /// gives each side its own records, the health of both goes to both, and the loser dies the PvP death and respawns.
    /// </summary>
    private static async Task PvpCloseCombatToDeath(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, ViewCreation bobView, ViewCreation aliceView) = await TwoNearby(context,
            new CharacterSeed("Alice") { Level = 50, Organization = 1 },
            new CharacterSeed("Bob") { X = 17120, Level = 10, Organization = 2 });
        string bobForAlice = HandleOf(bobView), aliceForBob = HandleOf(aliceView);

        // Both clients are told the server's PvP mode: 2, by flag (retail's value off its PvP server)
        Check.That(alice.Received("3a 05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00 04 00 02 00 00 00").Count > 0, "PvPServer = 2 at world entry");

        // Not flagged: "Target is not an opponent." (4d reason 24) for close combat, gunfire and abilities alike
        alice.SendRpc($"40 {bobForAlice} 01 6b");
        await alice.WaitForRpcAsync($"4d 18 00 00 00 {bobForAlice}");
        alice.SendRpc($"41 {bobForAlice}");
        await alice.WaitForRpcAsync($"4d 18 00 00 00 {bobForAlice}");
        Check.Equal(0, bob.Received("46").Count, "Fight starts on the unflagged player");

        // The flag reaches the player and everyone who sees them
        bob.SendChat("?pvpflag on");
        await bob.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        await alice.WaitForViewUpdateAsync(bobView.ViewId, OtherPvpFlagOn);
        Check.Equal("1", await context.Database.ScalarAsync($"SELECT pvpflag FROM characters WHERE charId={bob.CharId}"), "Saved PvP flag");

        // The attacker isn't flagged, so the flagged player can't start anything: only the target's flag counts
        bob.SendRpc($"40 {aliceForBob} 01 6b");
        await bob.WaitForRpcAsync($"4d 18 00 00 00 {aliceForBob}");

        bob.SendChat("?health 60");
        await alice.WaitForViewUpdateAsync(bobView.ViewId, "02 80 80 10 3c 00");
        alice.SkipReceived();
        bob.SkipReceived();
        alice.SendRpc($"40 {bobForAlice} 01 6b");

        // The attacker's side, as against an NPC
        await alice.WaitForRpcAsync("4e 01");
        await alice.WaitForRpcAsync($"40 {bobForAlice} 00 6b");
        await alice.WaitForRpcAsync($"46 {bobForAlice} 01");
        await alice.WaitForRpcAsync("47 00 03 ?? 01 01 ?? ?? 00 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? 1a 00 04 00 00 00");
        RpcRecord aliceOpening = await alice.WaitForRpcAsync("45 00 00 01 00");
        await alice.WaitForViewUpdateAsync(1, $"02 a7 00 ?? ?? 01 {Wild(24)} 01 00 00 00 03 00 00 00 {SelfHandle(alice)} {bobForAlice} 00");

        // The attacked player's side: their own first move with the has-ability byte 0, then 40 and 46 naming the attacker
        await bob.WaitForRpcAsync("47 00 00 ?? 01 01 ?? ?? 00 00 00 00 ?? ?? ?? ?? ?? ?? ?? ?? 1a 00 04 00 00 00");
        await bob.WaitForRpcAsync($"40 {aliceForBob} 00 6b");
        await bob.WaitForRpcAsync($"46 {aliceForBob} 01");
        RpcRecord bobOpening = await bob.WaitForRpcAsync("45 00 00 01 00");
        await bob.WaitForViewUpdateAsync(1, $"02 a7 00 ?? ?? 01 {Wild(24)} 01 00 00 00 03 00 00 00 {aliceForBob} {SelfHandle(bob)} 00");

        // One round, seen from both ends
        Check.Equal(89, aliceOpening.Bytes.Length, "RPC 45 length");
        Check.Equal(1, aliceOpening.Bytes[5], "The starter wins the opening round, in their own record");
        Check.Equal(0, bobOpening.Bytes[5], "and loses it in the other's");
        Check.Equal(3, aliceOpening.Bytes[7], "The attacker's opening move, own slot");
        Check.Equal(3, bobOpening.Bytes[26], "The attacker's opening move, in the defender's record");
        Check.Equal(0, bobOpening.Bytes[7], "The defender's own first move");
        Check.Equal(bobForAlice.Replace(" ", ""), WireFormat.ToHex(aliceOpening.Bytes[49..53]), "The attacker's target");
        Check.Equal(aliceForBob.Replace(" ", ""), WireFormat.ToHex(bobOpening.Bytes[49..53]), "The defender's target");
        Check.Equal(RoundResult(aliceOpening), RoundResult(bobOpening), "Round result");
        Check.Equal(DamageToOpponent(aliceOpening), DamageToSelf(bobOpening), "Damage to the defender");
        Check.Equal(DamageToSelf(aliceOpening), DamageToOpponent(bobOpening), "Damage to the attacker");

        // The loser's health falls on their own view and on the other's view of them, to the death
        TimeSpan toDie = TimeSpan.FromSeconds(120);
        await bob.WaitForViewUpdateAsync(2, OwnDeath, toDie);
        await alice.WaitForViewUpdateAsync(bobView.ViewId, OtherDeath);
        // (It doesn't only fall: health regenerates in a fight, slowly, and that reaches both views too.)
        List<ushort> seenByAlice = ViewUpdateValues(alice, bobView.ViewId, "028080 10"), seenByBob = ViewUpdateValues(bob, 2, "02808080 40");
        Check.That(seenByBob.Count > 0 && seenByBob.All(seenByAlice.Contains), $"The attacker saw the same health: {string.Join(",", seenByAlice)} vs {string.Join(",", seenByBob)}");

        // Retail's PvP death: the kill notice with both names for both, the death message for the dead
        RpcRecord notice = await bob.WaitForRpcAsync($"81 9d {aliceForBob} {SelfHandle(bob)} 0c 00");
        Check.That(Contains(notice.Bytes, "Alice") && Contains(notice.Bytes, "Bob"), "Names in the kill notice");
        Check.That(bob.Received("2e 67 00 00 00 00 00 00 00 00 00 00 00 92 00 00 60").Count == 1, "Death message");
        await alice.WaitForRpcAsync($"81 9d {SelfHandle(alice)} {bobForAlice} 0c 00");
        // The interlock comes down after the death, as in retail (8 to 9 s later), and the dead get no 46 00
        await alice.WaitForRpcAsync($"46 {bobForAlice} 00", TimeSpan.FromSeconds(15));
        Check.Equal(0, bob.Received($"46 {aliceForBob} 00").Count, "46 00 for the dead");

        // The dead don't fight
        alice.SendRpc($"40 {bobForAlice} 01 6b");
        await alice.WaitForRpcAsync($"4d 02 01 00 00 {bobForAlice}");

        // The usual respawn; its heal clears the flag, as retail's did
        bob.SendRpc("81 4c");
        await bob.WaitForViewUpdateAsync(2, "02 80 80 80 c0 ?? ?? 80 80 01 00 00 00 00");
        await bob.WaitForRpcAsync("80 fd 00 00 00 00 01 01");
        Check.Equal(0, alice.Received("80 fd").Count, "The winner stays");
    }

    private static string Wild(int bytes) => string.Join(" ", Enumerable.Repeat("??", bytes));

    /// <summary>
    /// Gunfire at a flagged player of another organisation: the shooter's own shots, the shot on everyone else's view of
    /// the shooter with the target as each of them knows it, the target's health and the combat-log line for both.
    /// </summary>
    private static async Task PvpGunfire(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Shooter") { Level = 50, Organization = 1 },
            new CharacterSeed("Target") { X = 17320, Level = 50, Organization = 2 },
            new CharacterSeed("Onlooker") { X = 17220, Z = 2893, Level = 50 });
        HeadlessPlayer shooter = players[0], target = players[1], onlooker = players[2];
        ViewCreation targetForShooter = await shooter.WaitForViewNamedAsync("Target");
        ViewCreation shooterForTarget = await target.WaitForViewNamedAsync("Shooter");
        ViewCreation shooterForOnlooker = await onlooker.WaitForViewNamedAsync("Shooter");
        ViewCreation targetForOnlooker = await onlooker.WaitForViewNamedAsync("Target");

        target.SendChat("?pvpflag on");
        await shooter.WaitForViewUpdateAsync(targetForShooter.ViewId, OtherPvpFlagOn);

        // The client's three records for a shot: draw, fire, and a close-combat request the server must leave alone
        shooter.SendRpc("3d 01");
        shooter.SendRpc($"41 {HandleOf(targetForShooter)}");
        shooter.SendRpc($"40 {HandleOf(targetForShooter)} 00 6b");

        await shooter.WaitForViewUpdateAsync(2, $"03 28 ?? 01 0c ?? ?? ?? {HandleOf(targetForShooter)}", TimeSpan.FromSeconds(15));
        await shooter.WaitForRpcAsync("4e 00");
        await target.WaitForViewUpdateAsync(shooterForTarget.ViewId, $"03 2a ?? 01 0c ?? ?? ?? {SelfHandle(target)} 00", TimeSpan.FromSeconds(15));
        await onlooker.WaitForViewUpdateAsync(shooterForOnlooker.ViewId, $"03 2a ?? 01 0c ?? ?? ?? {HandleOf(targetForOnlooker)} 00", TimeSpan.FromSeconds(15));

        RpcRecord shooterLine = await shooter.WaitForRpcAsync("81 68");
        RpcRecord targetLine = await target.WaitForRpcAsync("81 68");
        Check.That(Contains(shooterLine.Bytes, "Shooter") && Contains(shooterLine.Bytes, "Target"), "Names in the shooter's combat-log line");
        Check.Equal(1, shooterLine.Bytes[22], "The shooter's line says the attacker is them");
        Check.Equal(0, targetLine.Bytes[22], "The target's line says it isn't");

        // A hit takes health, for the target, the shooter and the onlooker alike (three in four shots hit)
        await target.WaitUntilAsync(() => ViewUpdateValues(target, 2, "02808080 40").Count > 0, "a hit", TimeSpan.FromSeconds(60));
        ushort health = ViewUpdateValues(target, 2, "02808080 40")[0];
        await shooter.WaitForViewUpdateAsync(targetForShooter.ViewId, $"02 80 80 10 {Le(health)}");
        await onlooker.WaitForViewUpdateAsync(targetForOnlooker.ViewId, $"02 80 80 10 {Le(health)}");
        Check.Equal(0, shooter.Received("47").Count + target.Received("47").Count, "Interlock records in a gunfight");

        // The shooter stops (44): no more shots
        shooter.SendRpc("44 00");
        await Task.Delay(1000);
        int shots = CountViewUpdate(target, shooterForTarget.ViewId, "032a");
        await Task.Delay(TimeSpan.FromSeconds(7));
        Check.Equal(shots, CountViewUpdate(target, shooterForTarget.ViewId, "032a"), "Shots after the shooter stopped");
        shooter.SendRpc("3d 01");
        shooter.SendRpc($"41 {HandleOf(targetForShooter)}");
        await shooter.WaitUntilAsync(() => CountViewUpdate(target, shooterForTarget.ViewId, "032a") > shots,
            "Gunfire restarts", TimeSpan.FromSeconds(10));
        // Native Escape sends this, with no RPC44. It used to leave the server's gunfire running.
        shooter.SendRpc("3d 00");
        await shooter.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 02 22");
        await Task.Delay(500);
        shots = CountViewUpdate(target, shooterForTarget.ViewId, "032a");
        await Task.Delay(TimeSpan.FromSeconds(7));
        Check.Equal(shots, CountViewUpdate(target, shooterForTarget.ViewId, "032a"), "Shots after native holster");
    }

    /// <summary>
    /// A duel from the challenge to the loser standing up again: 50, 54 and 51, 52, 55 with the paired DuelIDs, an
    /// ordinary fight between two players who could not otherwise touch each other, and the duel death.
    /// </summary>
    private static async Task DuelToDefeat(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Alice") { Level = 50 },
            new CharacterSeed("Bob") { X = 17120, Level = 10 },
            new CharacterSeed("Carol") { X = 17070, Z = 2793 });
        HeadlessPlayer alice = players[0], bob = players[1], carol = players[2];
        ViewCreation bobView = await alice.WaitForViewNamedAsync("Bob"), aliceView = await bob.WaitForViewNamedAsync("Alice");
        ViewCreation aliceForCarol = await carol.WaitForViewNamedAsync("Alice"), bobForCarol = await carol.WaitForViewNamedAsync("Bob");
        string bobForAlice = HandleOf(bobView), aliceForBob = HandleOf(aliceView);

        // No organisation, no flag: they can't fight
        alice.SendRpc($"40 {bobForAlice} 01 6b");
        await alice.WaitForRpcAsync($"4d 18 00 00 00 {bobForAlice}");

        // /duel on the target: "challenged" for the challenger, the dialog for the target
        alice.SendRpc($"50 {bobForAlice}");
        await alice.WaitForRpcAsync($"54 {bobForAlice} 00 00");
        await bob.WaitForRpcAsync($"51 {aliceForBob}");
        // A second challenge while the first is open
        alice.SendRpc($"50 {bobForAlice}");
        await alice.WaitForRpcAsync($"53 0a 00 {bobForAlice}");
        carol.SendRpc($"50 {HandleOf(bobForCarol)}");
        await carol.WaitForRpcAsync($"53 09 01 {HandleOf(bobForCarol)}");

        // Yes: 55 for both, and the DuelIDs, the challenger's even and the other's the next one
        bob.SendRpc($"52 {aliceForBob} 01 00");
        await alice.WaitForRpcAsync($"55 {bobForAlice}");
        await bob.WaitForRpcAsync($"55 {aliceForBob}");
        byte[] own = await alice.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 01");
        uint aliceId = ViewUpdateU32(alice, 2, "02808080808001"), bobId = ViewUpdateU32(bob, 2, "02808080808001");
        Check.That(aliceId != 0 && aliceId % 2 == 0, "The challenger's DuelID is even: " + aliceId);
        Check.Equal(aliceId + 1, bobId, "The other's DuelID");
        await alice.WaitForViewUpdateAsync(bobView.ViewId, $"02 80 80 80 20 {Le(bobId)}");
        await bob.WaitForViewUpdateAsync(aliceView.ViewId, $"02 80 80 80 20 {Le(aliceId)}");
        await carol.WaitForViewUpdateAsync(aliceForCarol.ViewId, $"02 80 80 80 20 {Le(aliceId)}");

        // Nobody else can join in, and the duelists can challenge nobody
        carol.SendRpc($"50 {HandleOf(bobForCarol)}");
        await carol.WaitForRpcAsync($"53 07 01 {HandleOf(bobForCarol)}");
        alice.SendRpc($"50 {bobForAlice}");
        await alice.WaitForRpcAsync($"53 0b 00 {bobForAlice}");

        // The fight is an ordinary one
        bob.SendChat("?health 60");
        await alice.WaitForViewUpdateAsync(bobView.ViewId, "02 80 80 10 3c 00");
        alice.SendRpc($"40 {bobForAlice} 01 6b");
        await alice.WaitForRpcAsync($"46 {bobForAlice} 01");
        await bob.WaitForRpcAsync($"46 {aliceForBob} 01");
        await alice.WaitForViewUpdateAsync(1, "02 a7 00");
        await bob.WaitForViewUpdateAsync(1, "02 a7 00");

        // The loser lies down: IsDuelDeath, IsDead, Health 0 and DuelID 0 on their own view, and flags without the
        // in-combat bit (retail: the PvP flag alone); no death
        TimeSpan toFall = TimeSpan.FromSeconds(120);
        ushort bobCombatView = CombatViewOf(bob), aliceCombatView = CombatViewOf(alice);
        await bob.WaitForViewUpdateAsync(2, UnflaggedDuelDeath, toFall);
        await bob.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 de 00 00 3a");
        await bob.WaitForRpcAsync($"56 01 00 {aliceForBob} 00 00");
        RpcRecord bobBroadcast = bob.Received($"81 9d {SelfHandle(bob)} {aliceForBob} 0c 00").Single();
        Check.Equal(0x57, bobBroadcast.Bytes[14], "The duel result is a 57 inside the 81 9d");
        Check.Equal(0, bob.Received("2e 67 00 00 00 00 00 00 00 00 00 00 00 92 00 00 60").Count, "Death messages for a duel's loser");

        // The winner: their own DuelID cleared, the loser down on their view, "You have defeated"
        await alice.WaitForRpcAsync($"56 01 01 {bobForAlice} 00 00");
        await alice.WaitForViewUpdateAsync(2, OwnDuelIdZero);
        await alice.WaitForViewUpdateAsync(bobView.ViewId, "02 80 80 92 01 00 00 20 00 00 00 00");

        // All of that while the interlock is still up (PR 94 QA: sent after the teardown, in its packet, the real client
        // dropped it). Retail took the combat view down 8 to 9 s later; for the loser without 46 00.
        Check.Equal(-1, SectionOf(bob, 1, $"01 01 00 {Le(bobCombatView)}"), "The loser's combat view is gone before the defeat");
        Check.Equal(-1, SectionOf(alice, 1, $"01 01 00 {Le(aliceCombatView)}"), "The winner's combat view is gone before the duel's end");
        Check.Equal(0, alice.Received($"46 {bobForAlice} 00").Count, "The winner released before the duel's end");
        await alice.WaitForRpcAsync($"46 {bobForAlice} 00", TimeSpan.FromSeconds(15));
        await alice.WaitForViewUpdateAsync(1, $"01 01 00 {Le(aliceCombatView)}");
        await bob.WaitForViewUpdateAsync(1, $"01 01 00 {Le(bobCombatView)}");
        Check.That(SectionOf(bob, 2, UnflaggedDuelDeath) < SectionOf(bob, 1, $"01 01 00 {Le(bobCombatView)}"), "The defeat first, in a packet of its own");
        Check.That(SectionOf(alice, 2, OwnDuelIdZero) < SectionOf(alice, 1, $"01 01 00 {Le(aliceCombatView)}"), "DuelID 0 first, in a packet of its own");
        Check.Equal(0, bob.Received($"46 {aliceForBob} 00").Count, "46 00 for the loser");
        // And once more after the teardown, in a packet of their own (ours)
        await bob.WaitUntilAsync(() => SectionsWith(bob, 2, UnflaggedDuelDeath) == 2, "the defeat again after the teardown", TimeSpan.FromSeconds(5));
        await alice.WaitUntilAsync(() => SectionsWith(alice, 2, OwnDuelIdZero) == 2, "DuelID 0 again after the teardown", TimeSpan.FromSeconds(5));
        Check.Equal(0, SectionsWithBoth(bob, 1, $"01 01 00 {Le(bobCombatView)}", 2, UnflaggedDuelDeath), "The defeat in the teardown's packet");
        Check.Equal(0, SectionsWithBoth(alice, 1, $"01 01 00 {Le(aliceCombatView)}", 2, OwnDuelIdZero), "DuelID 0 in the teardown's packet");
        // The onlooker hears who won
        RpcRecord heard = await carol.WaitForRpcAsync(r => r.Opcode == 0x19d && r.Bytes.Length > 14 && r.Bytes[14] == 0x57, "the duel result");
        Check.Equal($"{HandleOf(bobForCarol)}{HandleOf(aliceForCarol)}".Replace(" ", ""), WireFormat.ToHex(heard.Bytes[2..10]), "Loser and winner in the broadcast");
        Check.That(Contains(heard.Bytes, "Alice") && Contains(heard.Bytes, "Bob"), "Names in the broadcast");

        // With the duel over they are no opponents again
        alice.SendRpc($"40 {bobForAlice} 01 6b");
        await alice.WaitForRpcAsync(r => r.Opcode == 0x4d && r.Bytes[1] != 0x0e, "a refusal");

        // 81 4c stands the loser up where they are with 1 health: no Loading Area
        bob.SendRpc("81 4c");
        await bob.WaitForViewUpdateAsync(2, "03 28 ?? 40 00 00 00 10 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 80 80 80 41 00 01 00");
        await alice.WaitForViewUpdateAsync(bobView.ViewId, "02 80 80 12 00 01 00");
        await Task.Delay(500);
        Check.Equal(0, bob.Received("80 fd").Count, "Loading Area trips after a duel");
        Check.Equal(0, bob.World.Problems.Count + alice.World.Problems.Count, "Protocol problems");
    }

    /// <summary>A duel's loser without the PvP flag on their own view: MaxHealth, IsDuelDeath 1, IsDead 1, Health 0,
    /// DuelID 0, ConditionStateFlags 0 (niobe3dualduality 03:03:51 had the flag, 0x01000000, and DeathPenalty).</summary>
    private const string UnflaggedDuelDeath = "02 80 80 c1 ?? ?? 01 c1 01 00 00 80 81 00 00 00 00 01 00 00 00 00";
    private const string OwnDuelIdZero = "02 80 80 80 80 80 01 00 00 00 00";

    private static BytePattern ViewPattern(ushort viewId, string pattern) => BytePattern.Parse($"{viewId & 0xff:x2} {viewId >> 8:x2} {pattern}");

    /// <summary>Which packet (its 03 list, in arrival order) first held the view id followed by these bytes; -1 for none.</summary>
    private static int SectionOf(HeadlessPlayer player, ushort viewId, string pattern, int from = 0)
    {
        BytePattern parsed = ViewPattern(viewId, pattern);
        List<byte[]> sections = player.World.ViewSnapshot();
        return from >= sections.Count ? -1 : sections.FindIndex(from, parsed.Occurs);
    }

    /// <summary>How many packets held the update.</summary>
    private static int SectionsWith(HeadlessPlayer player, ushort viewId, string pattern)
    {
        BytePattern parsed = ViewPattern(viewId, pattern);
        return player.World.ViewSnapshot().Count(parsed.Occurs);
    }

    /// <summary>How many packets held both updates.</summary>
    private static int SectionsWithBoth(HeadlessPlayer player, ushort viewA, string patternA, ushort viewB, string patternB)
    {
        BytePattern a = ViewPattern(viewA, patternA), b = ViewPattern(viewB, patternB);
        return player.World.ViewSnapshot().Count(s => a.Occurs(s) && b.Occurs(s));
    }

    /// <summary>The combat view of the player's latest interlock, from its combat init (<c>01 00 02 a7 00 &lt;view&gt; 01 ...</c>).</summary>
    private static ushort CombatViewOf(HeadlessPlayer player)
    {
        byte[] needle = WireFormat.FromHex("010002a700");
        ushort view = 0;
        foreach (byte[] section in player.World.ViewSnapshot())
        {
            for (int at = BytePattern.IndexOf(section, needle, 0); at >= 0; at = BytePattern.IndexOf(section, needle, at + 1))
            {
                if (at + needle.Length + 2 <= section.Length)
                {
                    view = BitConverter.ToUInt16(section, at + needle.Length);
                }
            }
        }
        return view;
    }

    /// <summary>The u32 after the last `&lt;view&gt; &lt;bytes&gt;` in what the player received.</summary>
    private static uint ViewUpdateU32(HeadlessPlayer player, ushort viewId, string bytes)
    {
        byte[] needle = WireFormat.FromHex($"{viewId & 0xff:x2}{viewId >> 8:x2}{bytes}");
        uint value = 0;
        foreach (byte[] section in player.World.ViewSnapshot())
        {
            for (int at = BytePattern.IndexOf(section, needle, 0); at >= 0; at = BytePattern.IndexOf(section, needle, at + 1))
            {
                if (at + needle.Length + 4 <= section.Length)
                {
                    value = BitConverter.ToUInt32(section, at + needle.Length);
                }
            }
        }
        return value;
    }

    /// <summary>A challenge declined, the minute's wait after it, a surrender, and a duel broken off by distance.</summary>
    private static async Task DuelDeclineSurrenderAndDistance(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, ViewCreation bobView, ViewCreation aliceView) = await TwoNearby(context,
            new CharacterSeed("Alice"), new CharacterSeed("Bob") { X = 17120 });
        string bobForAlice = HandleOf(bobView), aliceForBob = HandleOf(aliceView);

        // Not a player
        alice.SendRpc("50 f0 7f 01 00");
        await alice.WaitForRpcAsync("53 03 00 f0 7f 01 00");

        // No: "%s declined your challenge", then the wait before asking the same player again
        alice.SendRpc($"50 {bobForAlice}");
        await bob.WaitForRpcAsync($"51 {aliceForBob}");
        bob.SendRpc($"52 {aliceForBob} 00 00");
        await alice.WaitForRpcAsync($"53 00 01 {bobForAlice}");
        alice.SendRpc($"50 {bobForAlice}");
        RpcRecord wait = await alice.WaitForRpcAsync($"53 0c 00 {bobForAlice}");
        float seconds = BitConverter.ToSingle(wait.Bytes, 9);
        Check.That(seconds > 50 && seconds <= 60, "Seconds to wait after a decline: " + seconds);
        Check.Equal(1, bob.Received("51").Count, "Challenges the target saw");

        // The other way round nothing was declined
        bob.SendRpc($"50 {aliceForBob}");
        await alice.WaitForRpcAsync($"51 {bobForAlice}");
        alice.SendRpc($"52 {bobForAlice} 01 00");
        await alice.WaitForRpcAsync($"55 {bobForAlice}");
        await bob.WaitForRpcAsync($"55 {aliceForBob}");

        // /duel stop in a duel is a surrender: nobody falls
        alice.SendRpc($"40 {bobForAlice} 01 6b");
        await bob.WaitForRpcAsync($"46 {aliceForBob} 01");
        await bob.WaitForViewUpdateAsync(1, "02 a7 00");
        ushort bobCombatView = CombatViewOf(bob);
        bob.SendRpc("50 00 00 00 00");
        // The duel's end first (DuelID 0, 56), the interlock a second later in packets of its own
        await bob.WaitForRpcAsync($"56 02 00 {aliceForBob}");
        await bob.WaitForRpcAsync($"46 {aliceForBob} 00", TimeSpan.FromSeconds(5));
        await alice.WaitForRpcAsync($"56 02 01 {bobForAlice}");
        await alice.WaitForRpcAsync($"46 {bobForAlice} 00", TimeSpan.FromSeconds(5));
        await bob.WaitForViewUpdateAsync(2, OwnDuelIdZero);
        await bob.WaitForViewUpdateAsync(1, $"01 01 00 {Le(bobCombatView)}");
        Check.That(SectionOf(bob, 2, OwnDuelIdZero) < SectionOf(bob, 1, $"01 01 00 {Le(bobCombatView)}"), "DuelID 0 before the teardown");
        Check.Equal(0, SectionsWithBoth(bob, 1, $"01 01 00 {Le(bobCombatView)}", 2, OwnDuelIdZero), "DuelID 0 in the teardown's packet");
        Check.Equal(0, CountViewUpdate(bob, 2, "028080c1"), "Duel deaths after a surrender");

        // A new duel, and one of them walks away: "too far apart" for both
        await Task.Delay(TimeSpan.FromSeconds(11)); // out of combat again (the reset comes ten seconds after a fight)
        alice.SkipReceived();
        bob.SkipReceived();
        bob.SendRpc($"50 {aliceForBob}");
        await alice.WaitForRpcAsync($"51 {bobForAlice}");
        alice.SendRpc($"52 {bobForAlice} 01 00");
        await bob.WaitForRpcAsync($"55 {aliceForBob}");
        alice.SendChat("?gotopos 40000 495 2693");
        await alice.WaitForRpcAsync("56 05 00", TimeSpan.FromSeconds(15));
        await bob.WaitForRpcAsync("56 05 00", TimeSpan.FromSeconds(15));
        Check.Equal(0, bob.World.Problems.Count + alice.World.Problems.Count, "Protocol problems");
    }

    /// <summary>
    /// ?duelbot: the server-played partner is a player like any other to the client. It accepts a duel and attacks
    /// (the attacked player's side of an interlock), or challenges and waits for the answer.
    /// </summary>
    private static async Task DuelBotDuels(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice") { Level = 20 }))[0];

        player.SendChat("?duelbot attack");
        ViewCreation bot = await player.WaitForViewNamedAsync("DuelBot");
        Check.Equal(20, bot.Attributes["Level"][0], "The bot's level is the player's");
        string botHandle = HandleOf(bot);

        player.SendRpc($"50 {botHandle}");
        await player.WaitForRpcAsync($"54 {botHandle} 00 00");
        await player.WaitForRpcAsync($"55 {botHandle}", TimeSpan.FromSeconds(10));
        uint duelId = ViewUpdateU32(player, 2, "02808080808001");
        await player.WaitForViewUpdateAsync(bot.ViewId, $"02 80 80 80 20 {Le(duelId + 1)}");

        // The bot pulls the player into close combat a few seconds later
        await player.WaitForRpcAsync("47 00 00 ?? 01 01 ?? ?? 00 00 00 00", TimeSpan.FromSeconds(15));
        await player.WaitForRpcAsync($"40 {botHandle} 00 6b");
        await player.WaitForRpcAsync($"46 {botHandle} 01");
        RpcRecord opening = await player.WaitForRpcAsync("45 00 00 01 00");
        Check.Equal(0, opening.Bytes[5], "The bot started, so it wins the opening round");
        // A round later the fight goes on, and the player can change tactic and gets the 47 for it
        await player.WaitForRpcAsync("45 01 00 02 00", TimeSpan.FromSeconds(15));
        player.SendRpc("42 03 00 00 00 00 00");
        await player.WaitForRpcAsync("47 00 ?? ?? 01 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 1a 00 06 00 00 00");

        // An instant ability on the opponent (Cheap Shot, 198) becomes the next move, as against an NPC
        player.SendRpc($"80 b9 c6 00 {Le(bot.ViewId)}");
        await player.WaitForRpcAsync("47 00 ?? ?? 01 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 1a 00 06 00 00 00 01 00 00 01 ?? 01 ?? ?? ?? c6 00 00 80");

        // Surrender, then let the bot challenge
        player.SendRpc("50 00 00 00 00");
        await player.WaitForRpcAsync($"56 02 00 {botHandle}");
        await player.WaitForRpcAsync($"46 {botHandle} 00", TimeSpan.FromSeconds(5));

        player.SendChat("?duelbot challenge");
        // The old bot's view goes and a new one comes
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(c => c.Name == "DuelBot") == 2, "the new bot", TimeSpan.FromSeconds(10));
        ViewCreation challenger = player.World.CreationSnapshot().Last(c => c.Name == "DuelBot");
        await player.WaitForRpcAsync($"51 {HandleOf(challenger)}", TimeSpan.FromSeconds(15));
        player.SendRpc($"52 {HandleOf(challenger)} 01 00");
        await player.WaitForRpcAsync($"55 {HandleOf(challenger)}");

        // Taking the bot away mid-duel: the duel's end (DuelID 0, 56 reason 4) reaches the player before the bot's
        // view goes, in a packet of its own, so the client isn't left locked onto a gone opponent (PR 94 QA)
        int duelEndsBefore = SectionsWith(player, 2, OwnDuelIdZero);
        player.SendChat("?duelbot off");
        await player.WaitForRpcAsync($"56 04 01", TimeSpan.FromSeconds(10));
        await player.WaitForViewUpdateAsync(1, $"01 01 00 {Le(challenger.ViewId)}");
        List<byte[]> sections = player.World.ViewSnapshot();
        int duelEnd = sections.FindLastIndex(ViewPattern(2, OwnDuelIdZero).Occurs);
        int botGone = sections.FindIndex(ViewPattern(1, $"01 01 00 {Le(challenger.ViewId)}").Occurs);
        Check.That(SectionsWith(player, 2, OwnDuelIdZero) > duelEndsBefore, "DuelID 0 when the bot went");
        Check.That(duelEnd >= 0 && duelEnd < botGone, $"DuelID 0 (packet {duelEnd}) before the bot's view went (packet {botGone})");

        // Losing to the bot (PR 94 QA, bug A): the defeat while the interlock is up, and /reconstruct at once takes the
        // interlock down first and stands the player up in a packet after it
        player.SendChat("?duelbot attack");
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(c => c.Name == "DuelBot") == 3, "the third bot", TimeSpan.FromSeconds(10));
        ViewCreation winner = player.World.CreationSnapshot().Last(c => c.Name == "DuelBot");
        await Task.Delay(TimeSpan.FromSeconds(11)); // out of combat again after the surrender's fight
        int from = player.World.ViewSnapshot().Count; // view ids are reused: look only at what comes from here
        player.SendRpc($"50 {HandleOf(winner)}");
        await player.WaitForRpcAsync($"55 {HandleOf(winner)}", TimeSpan.FromSeconds(10));
        await player.WaitForRpcAsync($"46 {HandleOf(winner)} 01", TimeSpan.FromSeconds(15));
        await player.WaitUntilAsync(() => SectionOf(player, 1, "02 a7 00", from) >= 0, "the combat init", TimeSpan.FromSeconds(5));
        ushort combatView = CombatViewOf(player);
        player.SendChat("?health 1");
        await player.WaitForViewUpdateAsync(2, UnflaggedDuelDeath, TimeSpan.FromSeconds(120));
        Check.Equal(-1, SectionOf(player, 1, $"01 01 00 {Le(combatView)}", from), "The interlock down before the defeat");
        player.SendRpc("81 4c");
        string standUp = "03 28 ?? 40 00 00 00 10 ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? ?? 80 80 80 41 00 01 00";
        await player.WaitForViewUpdateAsync(2, standUp, TimeSpan.FromSeconds(5));
        int down = SectionOf(player, 1, $"01 01 00 {Le(combatView)}", from), up = SectionOf(player, 2, standUp, from);
        Check.That(down >= 0 && down < up, $"The interlock down (packet {down}) before the stand-up (packet {up})");
        Check.Equal(0, player.Received($"46 {HandleOf(winner)} 00").Count, "46 00 for the loser");
        Check.Equal(0, player.Received("80 fd").Count, "Loading Area trips after a duel");
        Check.Equal(0, player.World.Problems.Count, "Protocol problems");
    }

    /// <summary>
    /// The bot with a gun fires in a duel, and the flagged bot of another organisation can be attacked with no duel.
    /// </summary>
    private static async Task DuelBotGunAndPvp(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Alice") { Level = 20 }))[0];

        player.SendChat("?duelbot gun");
        ViewCreation bot = await player.WaitForViewNamedAsync("DuelBot");
        Check.Equal(2565u, BitConverter.ToUInt32(bot.Attributes["EquippedItemID"]), "The gun in the bot's hand");
        player.SendRpc($"50 {HandleOf(bot)}");
        await player.WaitForRpcAsync($"55 {HandleOf(bot)}", TimeSpan.FromSeconds(10));
        // Its shots at the player, on its view, and the combat-log line for each
        await player.WaitForViewUpdateAsync(bot.ViewId, $"03 2a ?? 01 0c 38 ?? ?? {SelfHandle(player)} 00", TimeSpan.FromSeconds(15));
        RpcRecord line = await player.WaitForRpcAsync("81 68");
        Check.That(Contains(line.Bytes, "DuelBot") && Contains(line.Bytes, "Alice"), "Names in the combat-log line");
        Check.Equal(0, line.Bytes[22], "The attacker isn't the player");
        player.SendRpc("50 00 00 00 00");
        await player.WaitForRpcAsync($"56 02 00 {HandleOf(bot)}");

        // No duel: flagged, and of another organisation than the player's
        player.SendChat("?org 1");
        await player.WaitForViewUpdateAsync(2, "02 80 02 01");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT alignment FROM characters WHERE charId={player.CharId}"),
            "The dev organisation change reaches the database as well as the own view");
        player.SendChat("?duelbot pvp");
        await player.WaitUntilAsync(() => player.World.CreationSnapshot().Count(c => c.Name == "DuelBot") == 2, "the flagged bot", TimeSpan.FromSeconds(10));
        ViewCreation flagged = player.World.CreationSnapshot().Last(c => c.Name == "DuelBot");
        Check.Equal("00000001", WireFormat.ToHex(flagged.Attributes["ConditionStateFlags"]), "The bot's PvP flag in its creation record");
        Check.Equal(2, flagged.Attributes["OrganizationID"][0], "The bot's organisation");
        player.SendRpc($"50 {HandleOf(flagged)}");
        await player.WaitForRpcAsync($"53 00 01 {HandleOf(flagged)}", TimeSpan.FromSeconds(10)); // this one declines duels
        player.SendRpc($"40 {HandleOf(flagged)} 01 6b");
        await player.WaitForRpcAsync($"46 {HandleOf(flagged)} 01");
        await player.WaitForRpcAsync("45 00 00 01 00");
        Check.Equal(0, player.World.Problems.Count, "Protocol problems");
    }

    /// <summary>
    /// A PvP server (PvPServer 6): everyone above the safe level is flagged from world entry on, a low level only if
    /// they say so with /pvp, and the flag is in the creation record others get. Off a PvP server /pvp is refused.
    /// </summary>
    private static async Task PvpServerFlags(ScenarioContext context)
    {
        (HeadlessPlayer veteran, HeadlessPlayer novice, ViewCreation noviceView, ViewCreation veteranView) = await TwoNearby(context,
            new CharacterSeed("Veteran") { Level = 30, Organization = 1 },
            new CharacterSeed("Novice") { X = 17120, Level = 15, Organization = 2 });

        Check.That(veteran.Received("3a 05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00 04 00 06 00 00 00").Count > 0, "PvPServer = 6");
        Check.That(veteran.Received("3a 05 00 17 00 10 00 50 76 50 4d 61 78 53 61 66 65 4c 65 76 65 6c 00 04 00 0f 00 00 00").Count > 0, "PvPMaxSafeLevel = 15");

        // Above the safe level: flagged on the own view right after the spawn, and in the record others get
        await veteran.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        Check.Equal("00000001", WireFormat.ToHex(veteranView.Attributes["ConditionStateFlags"]), "The veteran's flag in their creation record");
        Check.That(!noviceView.Attributes.ContainsKey("ConditionStateFlags"), "No flag on a character at the safe level");
        Check.Equal(0, CountViewUpdate(novice, 2, "0280808080808001"), "Flag updates for the novice");

        // The novice is safe from the veteran, but can attack them, and is then fair game
        veteran.SendRpc($"40 {HandleOf(noviceView)} 01 6b");
        await veteran.WaitForRpcAsync($"4d 18 00 00 00 {HandleOf(noviceView)}");

        // /pvp is the novice's choice; the veteran has none
        novice.SendRpc("81 9e");
        await novice.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        await veteran.WaitForViewUpdateAsync(noviceView.ViewId, OtherPvpFlagOn);
        novice.SendRpc("81 9e");
        await novice.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 01 00 00 00 00");
        await veteran.WaitForViewUpdateAsync(noviceView.ViewId, "02 80 80 80 80 04 00 00 00 00");
        veteran.SendRpc("81 9e");
        await Task.Delay(500);
        Check.Equal(0, CountViewUpdate(veteran, 2, "028080808080800100000000"), "The veteran's flag going off");

        // Attacking flags the attacker
        novice.SendRpc($"40 {HandleOf(veteranView)} 01 6b");
        await novice.WaitForRpcAsync($"46 {HandleOf(veteranView)} 01");
        await veteran.WaitForViewUpdateAsync(noviceView.ViewId, "02 80 80 80 80 04 00 00 10 01");
        Check.Equal(0, novice.World.Problems.Count + veteran.World.Problems.Count, "Protocol problems");
    }

    /// <summary>Off a PvP server: /pvp is refused, and the flag comes and goes with the zone abilities' tempmods.</summary>
    private static async Task PvpFlagAbilities(ScenarioContext context)
    {
        (HeadlessPlayer alice, HeadlessPlayer bob, ViewCreation bobView, ViewCreation aliceView) = await TwoNearby(context,
            new CharacterSeed("Alice") { Organization = 1 }, new CharacterSeed("Bob") { X = 17120, Organization = 2 });

        // "Cannot change flag. Not a PvP server."
        alice.SendRpc("81 9e");
        await alice.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 94 02 00 7a");
        Check.Equal(0, CountViewUpdate(alice, 2, "0280808080808001"), "Flag changes from /pvp off a PvP server");

        // A PvP pulse: the flag and its message, once
        alice.SendChat("?pvpability 1210");
        await alice.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 47 02 00 08");
        await alice.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        await bob.WaitForViewUpdateAsync(aliceView.ViewId, OtherPvpFlagOn);
        alice.SendChat("?pvpability 1210");
        await Task.Delay(500);
        Check.Equal(1, alice.Received("2e 67 00 00 00 00 00 00 00 00 00 00 00 47 02 00 08").Count, "Pulse messages while already flagged");

        // Now the other organisation can attack her
        bob.SendRpc($"40 {HandleOf(aliceView)} 01 6b");
        await bob.WaitForRpcAsync($"46 {HandleOf(aliceView)} 01");
        await alice.WaitForRpcAsync($"46 {HandleOf(bobView)} 01");
        // 44 01 withdraws: tried each round until it works, then the fight is over for both
        alice.SendRpc("44 01");
        await alice.WaitForRpcAsync($"46 {HandleOf(bobView)} 00", TimeSpan.FromSeconds(120));
        await bob.WaitForRpcAsync($"46 {HandleOf(aliceView)} 00");
        Check.That(alice.Received("45").Any(r => RoundResult(r) == 0x200), "A round with the withdraw result");

        // A pacification zone takes the flag off again
        alice.SendChat("?pvpability 1232");
        await alice.WaitForRpcAsync("2e 67 00 00 00 00 00 00 00 00 00 00 00 29 00 00 04");
        await bob.WaitForViewUpdateAsync(aliceView.ViewId, "02 80 80 80 80 04 00 00 ?? 00");
        Check.Equal(0, alice.World.Problems.Count + bob.World.Problems.Count, "Protocol problems");
    }
}
