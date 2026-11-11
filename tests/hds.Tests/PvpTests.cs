using hds;
using hds.shared;

namespace hds.Tests;

// The byte strings are retail's, from the captures that hold fights and duels between players
// (research/packet-dumps/community-intake: died_in_pvp, some_pvp, niobe3dualduality, lastday2); docs/PVP.md lists them.
public class PvpTests
{
    private static string H(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", " ").ToLowerInvariant();
    private static byte[] B(string hex) => StringUtils.hexStringToBytes(hex.Replace(" ", ""));

    private static PvpRules.Subject Player(uint id, int level = 30, uint org = 1, bool flag = false, uint duel = 0, bool zone = false,
        uint team = 0, bool dead = false) => new PvpRules.Subject
    {
        CharId = id, Level = level, Organization = org, PvpFlag = flag, DuelId = duel, InPvpZone = zone, MissionTeamId = team, Dead = dead
    };

    // Attribute updates

    [Fact]
    public void OwnDeathUpdateIsRetails()
    {
        // died_in_pvp 19:42:29: IsDead 1, Health 0, ConditionStateFlags 0x01000000
        byte[] update = PlayerViewAttributes.BuildSelf(
            (PlayerViewAttributes.SelfConditionStateFlags, PlayerViewAttributes.U32(0x01000000)),
            (PlayerViewAttributes.SelfIsDead, PlayerViewAttributes.U8(1)),
            (PlayerViewAttributes.SelfHealth, PlayerViewAttributes.U16(0)));
        Assert.Equal("02 00 02 80 80 80 c1 01 00 00 80 80 01 00 00 00 01", H(update));
        Assert.Equal(H(ServerPackets.BuildSelfDeathUpdate()), H(update));
    }

    [Fact]
    public void AnotherPlayersHealthIsAttribute18()
    {
        // died_in_pvp: 05 00 02 80 80 10 0e 06
        Assert.Equal("02 80 80 10 0e 06", H(PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(0x060e)))));
    }

    [Fact]
    public void AnotherPlayersHitWithAnEffect()
    {
        // died_in_pvp: 05 00 02 80 80 90 19 06 0c ff 06 00 28 cf
        byte[] update = PlayerViewAttributes.Build(
            (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(0x0619)),
            (PlayerViewAttributes.OtherEffectId, PlayerViewAttributes.U32(0x280006ff)),
            (PlayerViewAttributes.OtherEffectCounter, PlayerViewAttributes.U8(0xcf)));
        Assert.Equal("02 80 80 90 19 06 0c ff 06 00 28 cf", H(update));
    }

    [Fact]
    public void AnotherPlayersDeath()
    {
        // died_in_pvp, an onlooker's view of the victim: 03 00 02 80 80 12 01 00 00
        byte[] update = PlayerViewAttributes.Build(
            (PlayerViewAttributes.OtherIsDead, PlayerViewAttributes.U8(1)),
            (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(0)));
        Assert.Equal("02 80 80 12 01 00 00", H(update));
    }

    [Fact]
    public void DuelIdsOnBothViews()
    {
        // niobe3dualduality 03:03:20, with the 55
        Assert.Equal("02 00 02 80 80 80 80 80 01 a3 cc 00 00",
            H(PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfDuelId, PlayerViewAttributes.U32(0xcca3)))));
        Assert.Equal("02 80 80 80 20 a2 cc 00 00",
            H(PlayerViewAttributes.Build((PlayerViewAttributes.OtherDuelId, PlayerViewAttributes.U32(0xcca2)))));
    }

    [Fact]
    public void FlagsOnBothViews()
    {
        // some_pvp, right after world entry: 02 00 02 80 80 80 80 80 90 11 00 00 00 01 00 00 00 01 (with CurCombatExclusiveAbility)
        Assert.Equal("02 00 02 80 80 80 80 80 80 01 00 00 00 01", H(PvpFlag.BuildSelfFlags(PvpFlag.PvpBit)));
        // died_in_pvp, another player's flags: ... 80 80 80 80 04 <flags>
        Assert.Equal("02 80 80 80 80 04 00 00 10 01", H(PvpFlag.BuildOtherFlags(PvpFlag.PvpBit | PvpFlag.InCombatBit)));
    }

    [Fact]
    public void PvpSettingsAreRetails()
    {
        // died_in_pvp, at world entry
        Assert.Equal("3a 05 00 11 00 0a 00 50 76 50 53 65 72 76 65 72 00 04 00 06 00 00 00", H(ServerPackets.BuildServerSettingUInt("PvPServer", 6)));
        Assert.Equal("3a 05 00 17 00 10 00 50 76 50 4d 61 78 53 61 66 65 4c 65 76 65 6c 00 04 00 0f 00 00 00",
            H(ServerPackets.BuildServerSettingUInt("PvPMaxSafeLevel", 15)));
    }

    // Who may fight whom (client.dll 0x625ed830)

    [Fact]
    public void DuelIdsPairByBitZero()
    {
        Assert.True(PvpRules.DuelOpponents(0xcca2, 0xcca3));
        Assert.True(PvpRules.DuelOpponents(0x626d, 0x626c));
        Assert.False(PvpRules.DuelOpponents(0xcca2, 0xcca2));
        Assert.False(PvpRules.DuelOpponents(0xcca2, 0xcca4));
        Assert.False(PvpRules.DuelOpponents(0, 1));
    }

    [Fact]
    public void UnflaggedPlayerOutsideAPvpZoneCantBeAttacked()
    {
        PvpRules.Subject attacker = Player(1, org: 1, flag: true), target = Player(2, org: 2);
        Assert.False(PvpRules.IsOpponent(attacker, target, PvpRules.ModeByFlag, 15));
        Assert.False(PvpRules.IsOpponent(attacker, target, PvpRules.ModeHostile, 15));
        Assert.Equal(PvpRules.Verdict.NotOpponent, PvpRules.Check(attacker, target, PvpRules.ModeHostile, 15));
    }

    [Fact]
    public void OnlyTheTargetsFlagCounts()
    {
        PvpRules.Subject unflagged = Player(1, org: 1), flagged = Player(2, org: 2, flag: true);
        Assert.True(PvpRules.IsOpponent(unflagged, flagged, PvpRules.ModeByFlag, 15));
        Assert.False(PvpRules.IsOpponent(flagged, unflagged, PvpRules.ModeByFlag, 15));
        // Modes 2, 4 and 6 are the same to the client
        Assert.True(PvpRules.IsOpponent(unflagged, flagged, 4, 15));
        Assert.True(PvpRules.IsOpponent(unflagged, flagged, PvpRules.ModeHostile, 15));
        // With the client's default, 0, the flag means nothing
        Assert.False(PvpRules.IsOpponent(unflagged, flagged, PvpRules.ModeOff, 15));
    }

    [Fact]
    public void OrganisationsMustDiffer()
    {
        Assert.False(PvpRules.IsOpponent(Player(1, org: 1), Player(2, org: 1, flag: true), PvpRules.ModeByFlag, 15));
        Assert.False(PvpRules.IsOpponent(Player(1, org: 0), Player(2, org: 2, flag: true), PvpRules.ModeByFlag, 15));
        Assert.False(PvpRules.IsOpponent(Player(1, org: 1), Player(2, org: 0, flag: true), PvpRules.ModeByFlag, 15));
        Assert.True(PvpRules.IsOpponent(Player(1, org: 3), Player(2, org: 2, flag: true), PvpRules.ModeByFlag, 15));
    }

    [Fact]
    public void PvpZonesNeedNoFlag()
    {
        PvpRules.Subject zion = Player(1, org: 1, zone: true), machine = Player(2, org: 2, zone: true);
        Assert.True(PvpRules.IsOpponent(zion, machine, PvpRules.ModeByFlag, 15));
        Assert.True(PvpRules.IsOpponent(zion, machine, PvpRules.ModeOff, 15));
        // Both must stand in one
        Assert.False(PvpRules.IsOpponent(zion, Player(2, org: 2), PvpRules.ModeByFlag, 15));
        // Same organisation, or the same mission team: no
        Assert.False(PvpRules.IsOpponent(zion, Player(2, org: 1, zone: true), PvpRules.ModeByFlag, 15));
        Assert.False(PvpRules.IsOpponent(Player(1, org: 1, zone: true, team: 7), Player(2, org: 2, zone: true, team: 7), PvpRules.ModeByFlag, 15));
    }

    [Fact]
    public void LevelModeProtectsLowLevels()
    {
        // Mode 1: both strictly above the safe level
        Assert.True(PvpRules.IsOpponent(Player(1, level: 16, org: 1), Player(2, level: 16, org: 2), PvpRules.ModeByLevel, 15));
        Assert.False(PvpRules.IsOpponent(Player(1, level: 16, org: 1), Player(2, level: 15, org: 2), PvpRules.ModeByLevel, 15));
        Assert.False(PvpRules.IsOpponent(Player(1, level: 15, org: 1), Player(2, level: 50, org: 2), PvpRules.ModeByLevel, 15));
        Assert.True(PvpRules.HostileServerFlags(16, 15));
        Assert.False(PvpRules.HostileServerFlags(15, 15));
    }

    [Fact]
    public void ForcedModes()
    {
        PvpRules.Subject a = Player(1, org: 1), b = Player(2, org: 1);
        Assert.True(PvpRules.IsOpponent(a, b, PvpRules.ModeEveryone, 15));
        Assert.True(PvpRules.IsOpponent(a, b, PvpRules.ModeEveryoneButTeam, 15));
        Assert.False(PvpRules.IsOpponent(Player(1, team: 3), Player(2, team: 3), PvpRules.ModeEveryoneButTeam, 15));
        Assert.False(PvpRules.IsOpponent(a, a, PvpRules.ModeEveryone, 15));
    }

    [Fact]
    public void DuelistsFightEachOtherAndNobodyElse()
    {
        PvpRules.Subject a = Player(1, duel: 0x1002), b = Player(2, duel: 0x1003);
        // Same organisation, no flags, no zone: the duel alone makes them opponents
        Assert.True(PvpRules.IsOpponent(a, b, PvpRules.ModeOff, 15));
        Assert.Equal(PvpRules.Verdict.Allowed, PvpRules.Check(a, b, PvpRules.ModeByFlag, 15));
        // A flagged outsider of another organisation can't join in, either way round
        PvpRules.Subject outsider = Player(3, org: 2, flag: true);
        Assert.Equal(PvpRules.Verdict.NotOpponent, PvpRules.Check(a, outsider, PvpRules.ModeByFlag, 15));
        Assert.Equal(PvpRules.Verdict.NotOpponent, PvpRules.Check(outsider, Player(1, org: 1, flag: true, duel: 0x1002), PvpRules.ModeByFlag, 15));
    }

    [Fact]
    public void TheDeadDontFight()
    {
        PvpRules.Subject a = Player(1, org: 1, flag: true), b = Player(2, org: 2, flag: true);
        Assert.Equal(PvpRules.Verdict.Allowed, PvpRules.Check(a, b, PvpRules.ModeByFlag, 15));
        Assert.Equal(PvpRules.Verdict.AttackerDead, PvpRules.Check(Player(1, org: 1, dead: true), b, PvpRules.ModeByFlag, 15));
        Assert.Equal(PvpRules.Verdict.TargetDead, PvpRules.Check(a, Player(2, org: 2, flag: true, dead: true), PvpRules.ModeByFlag, 15));
    }

    // The flag abilities (tempmods.py)

    [Fact]
    public void FlagTempmods()
    {
        Assert.True(PvpFlag.AfterAbility(PvpFlag.AbilityAction.Flip, false, out bool says));
        Assert.False(says);
        Assert.False(PvpFlag.AfterAbility(PvpFlag.AbilityAction.Flip, true, out _));
        Assert.True(PvpFlag.AfterAbility(PvpFlag.AbilityAction.On, true, out _));
        // PVPFlagMsgOnInit: the message only when the flag was off
        Assert.True(PvpFlag.AfterAbility(PvpFlag.AbilityAction.OnWithMessage, false, out says));
        Assert.True(says);
        Assert.True(PvpFlag.AfterAbility(PvpFlag.AbilityAction.OnWithMessage, true, out says));
        Assert.False(says);
        // PVPFlagMsgOffInit: only when it was on
        Assert.False(PvpFlag.AfterAbility(PvpFlag.AbilityAction.OffWithMessage, true, out says));
        Assert.True(says);
        Assert.False(PvpFlag.AfterAbility(PvpFlag.AbilityAction.OffWithMessage, false, out says));
        Assert.False(says);
        Assert.Equal(PvpFlag.AbilityAction.OnWithMessage, PvpFlag.Abilities[1210]);
        Assert.Equal(PvpFlag.AbilityAction.OffWithMessage, PvpFlag.Abilities[1232]);
    }

    // The interlock records as the attacked player gets them

    [Fact]
    public void AttackedPlayersFirstMove()
    {
        // died_in_pvp 19:41:11: 47 kind 4 with the defender's own move, has-ability byte 0, ability field 0
        var move = new CombatInterlock.Move
        {
            Seq = 1, Tactic = CombatTactic.Power, Value = 1, NoAbilityField = true, R1 = 0x56, R2 = 0x68, R3 = 0x5a, R4 = 0x26
        };
        Assert.Equal("47 00 00 01 01 01 01 00 00 00 00 00 56 00 68 00 5a 00 26 00 1a 00 04 00 00 00 00 00",
            H(CombatInterlock.BuildRoundState(CombatInterlock.KindStart, move, null)));
    }

    [Fact]
    public void AttackedPlayersOpeningRound()
    {
        // died_in_pvp 19:41:11: the receiver's own move first, "won" 0, the attacker's opening move with flag 3, the
        // attacker as target, result 0x801, 145 damage to the receiver. Retail's last four bytes name the attacker's
        // weapon (22 b6 00 00); the server leaves them 0.
        var own = new CombatInterlock.Move
        {
            Seq = 1, Tactic = CombatTactic.Power, Value = 1, NoAbilityField = true, R1 = 0x56, R2 = 0x68, R3 = 0x5a, R4 = 0x26
        };
        var theirs = new CombatInterlock.Move
        {
            Seq = 1, Tactic = CombatTactic.Speed, Value = 0x46, First = true, R1 = 0xbe, R2 = 0x32, R3 = 0x17, R4 = 0x41
        };
        byte[] record = CombatInterlock.BuildRoundResult(0, false, own, theirs, 0x00f80005, 0x801, 145, 15, 0, 0, 0x203be6ae, 0, theirs.Flag);
        Assert.Equal(89, record.Length);
        Assert.Equal("45 00 00 01 00 00 00 00 01 01 01 01 00 00 00 00 00 56 00 68 00 5a 00 26 00 " +
                     "00 03 00 01 01 46 00 00 00 00 00 be 00 32 00 17 00 41 00 " +
                     "00 00 00 00 01 05 00 f8 00 05 00 f8 00 00 00 00 00 01 08 00 00 00 00 00 00 " +
                     "91 00 0f 00 00 00 00 00 ae e6 3b 20 00 00 00 00 00 00 00 00", H(record));
    }

    [Fact]
    public void NpcRoundResultIsUnchanged()
    {
        // The opponent's has-ability byte is 0 unless given: NPC fights send what they did before
        var a = new CombatInterlock.Move { Seq = 2, Tactic = CombatTactic.Speed };
        var b = new CombatInterlock.Move { Seq = 2, Tactic = CombatTactic.Grab };
        byte[] record = CombatInterlock.BuildRoundResult(1, true, a, b, 0x0022002f, 1, 0, 0, 5, 0, 0);
        Assert.Equal("00 00 02", H(record[25..28]));
    }

    [Fact]
    public void CombatInitNamesTheAttackerFirst()
    {
        // died_in_pvp 19:41:11, the attacked player's: ... 05 00 f8 00 02 00 d3 00 01 01 02
        byte[] centre = B("00 00 00 00 00 9a d0 40 00 00 00 00 00 90 7f 40 00 00 00 00 00 f8 a1 40");
        byte[] record = ServerPackets.BuildCombatInitialize(6, centre, 0x00f80005, 0x00d30002);
        Assert.Equal("01 00 02 a7 00 06 00 01 00 00 00 00 00 9a d0 40 00 00 00 00 00 90 7f 40 00 00 00 00 00 f8 a1 40 " +
                     "01 00 00 00 03 00 00 00 05 00 f8 00 02 00 d3 00 01 01 02", H(record));
    }

    [Fact]
    public void AttackedByAndRefusals()
    {
        // died_in_pvp: 40 05 00 f8 00 00 6b, sent with no request from the receiver
        Assert.Equal("40 05 00 f8 00 00 6b", H(PvpFight.BuildAttackedBy(0x00f80005)));
        // lastday2, a refused interlock: 4d 0e 00 01 01 0d 00 07 00
        Assert.Equal("4d 0e 00 01 01 0d 00 07 00", H(PvpCombat.BuildCombatRefused(14, false, 0x0007000d, true, true)));
        // "Target is not an opponent." is reason 24 (client.dll 0x6219c410)
        Assert.Equal("4d 18 00 00 00 0d 00 07 00", H(PvpCombat.BuildCombatRefused(PvpCombat.RefusedNotOpponent, false, 0x0007000d)));
    }

    // Duel records (client.dll classes; examples from the duel captures)

    [Fact]
    public void DuelRecordsMatchRetail()
    {
        Assert.Equal("51 35 00 68 00", H(DuelPackets.BuildChallenged(0x00680035)));
        Assert.Equal("54 35 00 68 00 00 00", H(DuelPackets.BuildChallengeSent(0x00680035)));
        Assert.Equal("55 2d 00 ee 00", H(DuelPackets.BuildBegin(0x00ee002d)));
        // niobe3dualduality: 53 04 01 35 00 68 00 00 00 00 00 00 00 (target on a mission team)
        Assert.Equal("53 04 01 35 00 68 00 00 00 00 00 00 00", H(DuelPackets.BuildRequestFailed(DuelPackets.FailOnTeam, true, 0x00680035)));
        // 53 00 01 39 00 2c 00 ... (declined)
        Assert.Equal("53 00 01 39 00 2c 00 00 00 00 00 00 00", H(DuelPackets.BuildRequestFailed(DuelPackets.FailUnknownOrDeclined, true, 0x002c0039)));
        // lastday2, the winner's: 56 01 01 0d 00 07 00 00 00; niobe3dualduality, the loser's: 56 01 00 2d 00 ee 00 00 00
        Assert.Equal("56 01 01 0d 00 07 00 00 00", H(DuelPackets.BuildStop(DuelPackets.StopDefeated, true, 0x0007000d)));
        Assert.Equal("56 01 00 2d 00 ee 00 00 00", H(DuelPackets.BuildStop(DuelPackets.StopDefeated, false, 0x00ee002d)));
    }

    [Fact]
    public void MustWaitCarriesTheSeconds()
    {
        // niobe3dualduality 03:24:57: 53 0c 00 39 00 2c 00 00 00 07 01 34 42 (45.0 s)
        float wait = BitConverter.ToSingle(B("07 01 34 42"), 0);
        Assert.Equal("53 0c 00 39 00 2c 00 00 00 07 01 34 42", H(DuelPackets.BuildRequestFailed(DuelPackets.FailMustWait, false, 0x002c0039, null, wait)));
        Assert.InRange(wait, 44.9f, 45.1f);
        // Declined at 03:24:42, asked again at 03:24:57
        Assert.Equal(60, DuelHandler.RechallengeSeconds);
    }

    [Fact]
    public void RequestFailedWithAName()
    {
        // client.dll: the name at the u16 offset, as <length with the 0> text 0
        byte[] record = DuelPackets.BuildRequestFailed(DuelPackets.FailUnavailable, false, 0, "Bob");
        Assert.Equal("53 01 00 00 00 00 00 0d 00 00 00 00 00 04 00 42 6f 62 00", H(record));
    }

    [Fact]
    public void DuelResultBroadcastIsRetails()
    {
        // niobe3dualduality 03:03:52: 81 9d <loser> <winner> 0c 00 20 00 | 57 01 ... NlOBEE SteveB
        byte[] inner = DuelPackets.BuildBroadcast(DuelPackets.StopDefeated, "NlOBEE", "SteveB");
        Assert.Equal("57 01 00 00 00 00 00 00 00 00 0e 00 17 00 07 00 4e 6c 4f 42 45 45 00 07 00 53 74 65 76 65 42 00", H(inner));
        Assert.Equal("81 9d 02 00 7e 00 2d 00 ee 00 0c 00 20 00 " + H(inner), H(DuelPackets.BuildAboutTwo(0x007e0002, 0x00ee002d, inner)));
    }

    [Fact]
    public void KillNoticeIsRetails()
    {
        // died_in_pvp 19:42:29
        byte[] line = DuelPackets.BuildKillLine("f1rewa11", "AfterWhoruNeo");
        Assert.Equal("81 67 17 00 22 00 86 00 00 60 00 00 10 00 00 00 00 00 00 00 00 00 08 09 00 66 31 72 65 77 61 31 31 00 " +
                     "0e 00 41 66 74 65 72 57 68 6f 72 75 4e 65 6f 00", H(line));
        Assert.Equal("81 9d 0f 00 43 00 02 00 d3 00 0c 00 32 00 " + H(line), H(DuelPackets.BuildAboutTwo(0x0043000f, 0x00d30002, line)));
    }

    [Fact]
    public void DuelDeathMessage()
    {
        // niobe3dualduality: 2e 67 ... de 00 00 3a ...; an ordinary death has 92 00 00 60 there
        Assert.Equal("2e 67 00 00 00 00 00 00 00 00 00 00 00 de 00 00 3a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
            H(DuelPackets.BuildSystemString(DuelPackets.DuelDeathMessageString)));
        Assert.Equal(H(ServerPackets.BuildDeathMessage()), H(DuelPackets.BuildSystemString(0x60000092)));
    }

    [Fact]
    public void LosersOwnView()
    {
        // niobe3dualduality 03:03:51: 02 00 02 80 80 c1 ba 13 01 c1 01 00 00 80 89 00 00 00 00 4c 37 69 3f 01 00 00 00 01.
        // The same without DeathPenalty (attribute 38), which the server doesn't send:
        Assert.Equal("02 00 02 80 80 c1 ba 13 01 c1 01 00 00 80 81 00 00 00 00 01 00 00 00 01",
            H(DuelHandler.BuildSelfDuelDeath(0x13ba, 0x01000000)));
    }

    [Fact]
    public void LoserStandsUpWithOneHealth()
    {
        // niobe3dualduality 03:04:08, after the client's 81 4c
        byte[] update = DuelHandler.BuildStandUp(0x0c, BitConverter.ToSingle(B("d7 42 10 47"), 0), BitConverter.ToSingle(B("00 20 94 c4"), 0),
            BitConverter.ToSingle(B("f6 47 38 c5"), 0), 0x0817a97c);
        Assert.Equal("02 00 03 28 0c 40 00 00 00 10 d7 42 10 47 00 20 94 c4 f6 47 38 c5 7c a9 17 08 80 80 80 41 00 01 00", H(update));
    }

    [Fact]
    public void ParsesTheClientsDuelRequests()
    {
        // niobe3dualduality: 50 35 00 68 00; /duel stop is target 0
        Assert.Equal(0x00680035u, DuelPackets.ParseChallenge(B("35 00 68 00")));
        Assert.Equal(0u, DuelPackets.ParseChallenge(B("00 00 00 00")));
        Assert.Null(DuelPackets.ParseChallenge(B("35 00")));
        // 52 <challenger> 01 00
        Assert.True(DuelPackets.TryParseResponse(B("2d 00 ee 00 01 00"), out uint challenger, out bool accepts, out byte reason));
        Assert.Equal(0x00ee002du, challenger);
        Assert.True(accepts);
        Assert.Equal(0, reason);
        Assert.True(DuelPackets.TryParseResponse(B("2d 00 ee 00 00 02"), out _, out accepts, out reason));
        Assert.False(accepts);
        Assert.Equal(2, reason);
    }

    [Fact]
    public void ChallengeObstaclesInOrder()
    {
        (byte, bool)? Check(bool selfDueling = false, bool sameDuel = false, bool sameChallenge = false, bool otherChallenge = false,
            bool considering = false, bool inCombat = false, bool onTeam = false, bool available = true, bool targetTeam = false,
            bool targetCombat = false, bool targetDueling = false, bool targetChallenging = false, bool targetConsidering = false,
            bool far = false, double wait = 0) =>
            DuelHandler.Obstacle(selfDueling, sameDuel, sameChallenge, otherChallenge, considering, inCombat, onTeam, available,
                targetTeam, targetCombat, targetDueling, targetChallenging, targetConsidering, far, wait);

        Assert.Null(Check());
        Assert.Equal((DuelPackets.FailAlreadyDuelingSame, false), Check(selfDueling: true, sameDuel: true));
        Assert.Equal((DuelPackets.FailAlreadyDueling, false), Check(selfDueling: true));
        Assert.Equal((DuelPackets.FailChallengingSame, false), Check(sameChallenge: true));
        Assert.Equal((DuelPackets.FailChallengingOther, false), Check(otherChallenge: true));
        Assert.Equal((DuelPackets.FailConsideringOther, false), Check(considering: true));
        Assert.Equal((DuelPackets.FailInCombat, false), Check(inCombat: true, targetCombat: true));
        Assert.Equal((DuelPackets.FailOnTeam, false), Check(onTeam: true));
        Assert.Equal((DuelPackets.FailUnavailable, false), Check(available: false));
        Assert.Equal((DuelPackets.FailOnTeam, true), Check(targetTeam: true));
        Assert.Equal((DuelPackets.FailInCombat, true), Check(targetCombat: true));
        Assert.Equal((DuelPackets.FailAlreadyDueling, true), Check(targetDueling: true));
        Assert.Equal((DuelPackets.FailChallengingOther, true), Check(targetChallenging: true));
        Assert.Equal((DuelPackets.FailConsideringOther, true), Check(targetConsidering: true));
        Assert.Equal((DuelPackets.FailTooFar, false), Check(far: true));
        Assert.Equal((DuelPackets.FailMustWait, false), Check(wait: 12));
    }

    [Fact]
    public void DuelBotModes()
    {
        Assert.True(DuelBot.TryParseMode("", out DuelBot.Mode mode));
        Assert.Equal(DuelBot.Mode.Wait, mode);
        Assert.True(DuelBot.TryParseMode("GUN", out mode));
        Assert.Equal(DuelBot.Mode.Gun, mode);
        Assert.True(DuelBot.TryParseMode("challenge", out mode));
        Assert.Equal(DuelBot.Mode.Challenge, mode);
        Assert.False(DuelBot.TryParseMode("dance", out _));
    }

    [Fact]
    public void FirstShotCarriesThePlayersFlags()
    {
        // The first shot of a fight sets ConditionStateFlags on the own view: in combat, and the PvP flag if the player has it
        byte[] plain = ServerPackets.BuildRangedShot(1, 0x00f80005, 0, 0, 0, 0, 0x32, true);
        byte[] flagged = ServerPackets.BuildRangedShot(1, 0x00f80005, 0, 0, 0, 0, 0x32, true, true, RangedWeapons.ClassPistol,
            PvpFlag.InCombatBit | PvpFlag.PvpBit);
        Assert.Equal("00 00 10 00 21", H(plain[^5..]));
        Assert.Equal("00 00 10 01 21", H(flagged[^5..]));
    }

    // The end of a duel on the real client (PR 94 QA): what goes out, in what order, and where the bot stands

    [Fact]
    public void UnflaggedLosersFlagsAreClearedWithTheDefeat()
    {
        // Retail's loser carried 0x01000000, the PvP flag alone: no in-combat bit (niobe3dualduality 03:03:51).
        // A loser without the flag gets 0 there.
        Assert.Equal("02 00 02 80 80 c1 64 00 01 c1 01 00 00 80 81 00 00 00 00 01 00 00 00 00",
            H(DuelHandler.BuildSelfDuelDeath(100, 0)));
    }

    [Fact]
    public void WinnersOwnDuelIdZeroIsRetails()
    {
        // lastday2 21:54:53: 02 00 02 80 84 b6 80 80 80 01 00 00 00 00 (NoiseLevel b6 and DuelID 0); without NoiseLevel:
        Assert.Equal("02 00 02 80 80 80 80 80 01 00 00 00 00",
            H(PlayerViewAttributes.BuildSelf((PlayerViewAttributes.SelfDuelId, PlayerViewAttributes.U32(0)))));
    }

    [Fact]
    public void TheInterlockComesDownAfterTheDeath()
    {
        // Retail took the combat view down 8 to 9 s after a duel's end: the loser 03:03:51 -> 03:04:00
        // (niobe3dualduality), the winner 21:54:53 -> 21:55:01 (lastday2)
        Assert.InRange(PvpFight.TeardownDelayFor(PvpFight.EndReason.Death), 8.0, 9.0);
        Assert.True(PvpFight.TeardownDelayFor(PvpFight.EndReason.DuelOver) > DuelHandler.ReassertSeconds);
        Assert.True(PvpFight.TeardownDelayFor(PvpFight.EndReason.Left) > DuelHandler.ReassertSeconds);
        Assert.Equal(0.0, PvpFight.TeardownDelayFor(PvpFight.EndReason.Withdrew));
        // The gap that keeps records out of one packet is longer than the queue's gathering time
        Assert.True(DuelHandler.ReassertSeconds * 1000 > WorldClient.FlushIntervalMs);
    }

    [Fact]
    public void BotTurnsWithTheOtherPlayerStateRecord()
    {
        // died_in_pvp, another player turning and moving on view 03: 03 00 01 0c e6 1e 6a 8c 46 00 80 f7 43 65 0e 95 44
        byte[] record = PvpFight.BuildFacing(0xe6, BitConverter.ToSingle(B("1e 6a 8c 46"), 0), BitConverter.ToSingle(B("00 80 f7 43"), 0),
            BitConverter.ToSingle(B("65 0e 95 44"), 0));
        Assert.Equal("01 0c e6 1e 6a 8c 46 00 80 f7 43 65 0e 95 44", H(record));
    }

    private static void LoadNavMesh()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "sector_properties.csv")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }
        SectorProperties.Load(Path.Combine(dir, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(dir, "data", "navmesh"));
    }

    [Theory]
    // Indoor and outdoor spots from NavMeshTests (retail positions), facing eight ways each
    [InlineData(1, 10682.4, 1695.0, -8798.3)]
    [InlineData(1, 45092.1, 95.0, 6129.0)]
    [InlineData(3, 44387.0, 2495.0, -3707.1)]
    [InlineData(1, 9842.2, 895.0, 13694.8)]
    [InlineData(1, 63242.0, 95.0, -91737.3)]
    [InlineData(2, -146314.1, 695.0, 28528.2)]
    [InlineData(1, 41010.332, 1695.0, 12280.438)] // Mara, a room by a wall (sector 1118, where a raw 3 m offset was off the floor)
    public void DuelBotStandsOnTheFloorInFrontOfItsOwner(ushort district, double x, double y, double z)
    {
        LoadNavMesh();
        Assert.True(NavMesh.Locate(district, x, y, z, out NavLocation owner));
        for (int yaw = 0; yaw < 256; yaw += 32)
        {
            Assert.True(DuelBot.Spot(district, x, y, z, (byte)yaw, out double bx, out double by, out double bz, out NavLocation spot));
            Assert.Equal(owner.indoors, spot.indoors);
            if (owner.indoors)
            {
                Assert.Equal(owner.region, spot.region);
            }
            double distance = Math.Sqrt((bx - x) * (bx - x) + (bz - z) * (bz - z));
            Assert.InRange(distance, 140.0, 310.0);
            Assert.True(NavMesh.Locate(district, bx, by, bz, out NavLocation at), $"yaw {yaw}: off the mesh at {bx}, {by}, {bz}");
            Assert.InRange(by - 95 - at.floorY, -1.0, 1.0);
            // Every step between them is floor: no wall in the way
            for (int i = 1; i < 10; i++)
            {
                Assert.True(NavMesh.Locate(district, x + (bx - x) * i / 10, by, z + (bz - z) * i / 10, out _));
            }
        }
    }

    [Theory]
    // Open ground (retail positions from NavMeshTests): straight ahead of the owner, 2.5 m, whichever way they face
    [InlineData(2, -146314.1, 695.0, 28528.2)]
    [InlineData(3, -48349.6, 95.0, -31817.8)]
    [InlineData(1, 40266.6, 95.0, -21183.1)]
    public void DuelBotGoesStraightAheadOnOpenGround(ushort district, double x, double y, double z)
    {
        LoadNavMesh();
        foreach (byte yaw in new byte[] { 0, 64, 128, 192 })
        {
            DuelBot.Spot(district, x, y, z, yaw, out double bx, out _, out double bz, out _);
            Assert.Equal(yaw, MobAggro.YawTowards(x, z, bx, bz));
            Assert.InRange(Math.Sqrt((bx - x) * (bx - x) + (bz - z) * (bz - z)), 249.0, 251.0);
        }
    }

    [Fact]
    public void DuelBotStaysOutOfTheMaraLobbysElevator()
    {
        // PR 94 re-test: in the Mara apartment lobby (sector 209, indoor region 84, polygon 3, floor 400), facing the
        // elevator, the bot went 2.5 m ahead into the car (region 87, another prefab) behind its closed doors: there is
        // floor all the way, but it is another region's. Whichever way the player faces, the bot stays in the lobby.
        LoadNavMesh();
        double x = 23828, y = 495, z = 13984;
        Assert.True(NavMesh.Locate(1, x, y, z, out NavLocation lobby));
        Assert.Equal((ushort)209, lobby.sector);
        Assert.Equal((ushort)84, lobby.region);
        // The car is straight ahead at yaw 144: floor of region 87 from about 1.75 m on
        double ahead = 144 * 2 * Math.PI / 256;
        Assert.True(NavMesh.Locate(1, x + 250 * Math.Sin(ahead), y, z + 250 * Math.Cos(ahead), out NavLocation car));
        Assert.Equal((ushort)87, car.region);
        for (int yaw = 0; yaw < 256; yaw += 8)
        {
            Assert.True(DuelBot.Spot(1, x, y, z, (byte)yaw, out double bx, out double by, out double bz, out NavLocation spot));
            Assert.Equal((ushort)84, spot.region);
            Assert.True(NavMesh.Locate(1, bx, by, bz, out NavLocation check));
            Assert.Equal((ushort)84, check.region);
            Assert.Equal(400.0, by - 95, 1);
        }
    }

    [Fact]
    public void ElevatorCarIsNotOpenFromTheLobby()
    {
        LoadNavMesh();
        double x = 23828, y = 495, z = 13984, ahead = 144 * 2 * Math.PI / 256;
        Assert.True(NavMesh.Locate(1, x, y, z, out NavLocation lobby));
        Assert.True(NavMesh.Locate(1, x + 250 * Math.Sin(ahead), y, z + 250 * Math.Cos(ahead), out NavLocation car));
        Assert.False(NavMesh.OpenBetween(1, lobby, car));
        // Two lobby polygons the probe walked across (3 and 1, towards the car) do share an open edge
        Assert.True(NavMesh.Locate(1, x + 150 * Math.Sin(ahead), y, z + 150 * Math.Cos(ahead), out NavLocation nearDoor));
        Assert.Equal((ushort)84, nearDoor.region);
        Assert.True(NavMesh.OpenBetween(1, lobby, lobby));
    }
}
