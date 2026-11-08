using hds;
using hds.shared;

namespace hds.Tests;

public class CombatInterlockTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static string Nospace(string s) => s.Replace(" ", "");

    [Fact]
    public void TacticRecordsMatchRetail()
    {
        // Tutorial capture 13:29:10 (42 00 after Power): the Speed tactic's six records. Record id 0x300 is ours.
        UInt16[] ids = { 0x300, 0x301, 0x302, 0x303, 0x304, 0x305 };
        var records = CombatTactics.BuildRecords(CombatTactic.Speed, ids, false);
        Assert.Equal(CombatTactics.RecordsPerTactic, records.Count);
        Assert.Equal(Nospace("80bc 1500 0003 00 f7030000 0802 00000000 00 00000000"), Hex(records[0])); // EvadeInterlock 520 +0
        Assert.Equal(Nospace("80bc 1500 0103 00 f7030000 0702 ecffffff 00 00000000"), Hex(records[1])); // ForceCombat 519 -20
        Assert.Equal(Nospace("80bc 1500 0303 00 f7030000 f403 00000000 00 00000000"), Hex(records[3])); // CurrentTactic 1012 = 0
        Assert.Equal(Nospace("80bc 1500 0403 00 f7030000 5104 f6ffffff 00 00000000"), Hex(records[4])); // Defense% 1105 -10
        Assert.Equal(Nospace("80bc 1500 0503 00 f7030000 5204 0f000000 00 00000000"), Hex(records[5])); // Tactic% 1106 +15
    }

    [Fact]
    public void OffRecordsCarryTheOffByte()
    {
        UInt16[] ids = { 1, 2, 3, 4, 5, 6 };
        // Power off, as sent when Speed replaced it (ability 1017 = 0x3f9, CurrentTactic = 1)
        var records = CombatTactics.BuildRecords(CombatTactic.Power, ids, true);
        Assert.Equal(Nospace("80bc 1500 0400 00 f9030000 f403 01000000 01 00000000"), Hex(records[3]));
    }

    [Theory]
    [InlineData(0, CombatTactic.Speed, 1015)]
    [InlineData(1, CombatTactic.Power, 1017)]
    [InlineData(2, CombatTactic.Grab, 1016)]
    [InlineData(3, CombatTactic.Block, 1013)]
    public void ButtonsMapToRetailTactics(byte rpc, CombatTactic tactic, int ability)
    {
        Assert.Equal(tactic, CombatTactics.FromRpc(rpc));
        Assert.Equal(ability, CombatTactics.Get(tactic).Ability);
    }

    [Theory]
    [InlineData(0, CombatTacticType.Normal)]
    [InlineData(1, CombatTacticType.Energized)]
    [InlineData(2, CombatTacticType.Precise)]
    [InlineData(9, CombatTacticType.Normal)]
    public void TacticTypesMapFromTheRpc(int rpc, CombatTacticType type)
    {
        Assert.Equal(type, CombatTactics.TypeFromRpc(rpc));
    }

    [Fact]
    public void EnergizedHitsHarderButLessOftenAndPreciseTheReverse()
    {
        double Damage(CombatTacticType type)
        {
            var rng = new Random(21);
            int sum = 0;
            for (int i = 0; i < 2000; i++) sum += CombatCalculator.CloseCombatDamage(rng, 100, 100, CombatTactic.Power, out _, type);
            return sum / 2000.0;
        }
        int Hits(CombatTacticType type)
        {
            var rng = new Random(22);
            int hits = 0;
            for (int i = 0; i < 4000; i++)
            {
                if (CombatInterlock.RollHit(rng, CombatTactic.Speed, 200, CombatTactic.Speed, 200, out _, out _, type)) hits++;
            }
            return hits;
        }
        Assert.True(Damage(CombatTacticType.Energized) > Damage(CombatTacticType.Normal));
        Assert.True(Damage(CombatTacticType.Precise) < Damage(CombatTacticType.Normal));
        Assert.True(Hits(CombatTacticType.Precise) > Hits(CombatTacticType.Normal));
        Assert.True(Hits(CombatTacticType.Energized) < Hits(CombatTacticType.Normal));
    }

    [Fact]
    public void UnknownTacticIsRefused()
    {
        Assert.Null(CombatTactics.FromRpc(4));
    }

    [Fact]
    public void TacticChangeCostIsPaidOnlyWhenAffordable()
    {
        Assert.Equal((ushort)90, CombatTactics.PayForChange(100, 10));
        Assert.Equal((ushort)0, CombatTactics.PayForChange(10, 10));
        Assert.Null(CombatTactics.PayForChange(9, 10));
    }

    private static CombatInterlock.Move RetailMove(byte seq, UInt16 value, UInt16 ability, CombatTactic tactic, UInt16 r1, UInt16 r2, UInt16 r3, UInt16 r4)
    {
        return new CombatInterlock.Move { Seq = seq, Value = value, Ability = ability, Tactic = tactic, R1 = r1, R2 = r2, R3 = r3, R4 = r4 };
    }

    [Fact]
    public void NewRoundStateMatchesRetail()
    {
        // Tutorial 13:28:45: 47 00 00 01 01 0c ... 1a 00 03 00 00 00 01 00 00 00 01 01 0d ...
        string retail = Nospace("47 00 00 01 01 0c 00 00 00 00 00 80 00 00 00 00 00 00 00 00 1a 00 03 00 00 00 " +
                                "01 00 00 00 01 01 0d 00 00 00 00 00 80 00 00 00 00 00 00 00 00");
        Assert.Equal(retail, Hex(CombatInterlock.BuildRoundState(CombatInterlock.KindNewRound,
            RetailMove(0x0c, 0, 0, CombatTactic.Power, 0, 0, 0, 0), RetailMove(0x0d, 0, 0, CombatTactic.Power, 0, 0, 0, 0))));
    }

    [Fact]
    public void AbilityInNextMoveMatchesRetail()
    {
        // Tutorial 13:28:39, Cheap Shot (0xc6) used: kind 6 and the next move names it
        string retail = Nospace("47 00 00 01 01 00 00 00 00 00 00 80 00 00 00 00 00 00 00 00 1a 00 06 00 00 00 " +
                                "01 00 00 01 01 01 0b 00 00 c6 00 00 80 00 00 00 00 00 00 00 00");
        Assert.Equal(retail, Hex(CombatInterlock.BuildRoundState(CombatInterlock.KindNextMoveChanged,
            RetailMove(0x00, 0, 0, CombatTactic.Power, 0, 0, 0, 0), RetailMove(0x0b, 0, 198, CombatTactic.Power, 0, 0, 0, 0))));
    }

    [Fact]
    public void OpeningStateMatchesRetail()
    {
        // Tutorial 13:28:32: 47 00 03 00 01 09 4c ... 1a 00 04 00 00 00 00 00
        string retail = Nospace("47 00 03 00 01 09 4c 00 00 00 00 00 35 00 1a 00 15 00 14 00 1a 00 04 00 00 00 00 00");
        var move = RetailMove(0x09, 0x4c, 0, CombatTactic.Speed, 0x35, 0x1a, 0x15, 0x14);
        move.First = true;
        Assert.Equal(retail, Hex(CombatInterlock.BuildRoundState(CombatInterlock.KindStart, move, null)));
    }

    [Fact]
    public void RoundResultMatchesRetail()
    {
        // Tutorial 13:28:54: the player lost 4 and the NPC 21 in this round (result 8 = both hit)
        string retail = Nospace("45 06 00 07 00 01 00 00 01 01 0e 57 00 00 00 00 80 08 00 45 00 20 00 25 00 00 00 00 " +
                                "01 0a 1a 00 00 00 00 80 16 00 25 00 1d 00 16 00 00 00 00 00 01 07 00 f0 00 07 00 f0 00 " +
                                "00 00 00 00 08 00 00 00 00 00 00 00 04 00 00 00 15 00 fb ff df 80 e6 1e 00 00 00 00 00 00 00 00");
        byte[] built = CombatInterlock.BuildRoundResult(6, true,
            RetailMove(0x0e, 0x57, 0, CombatTactic.Power, 0x08, 0x45, 0x20, 0x25),
            RetailMove(0x0a, 0x1a, 0, CombatTactic.Speed, 0x16, 0x25, 0x1d, 0x16),
            0x00f00007, CombatInterlock.ResultBothHit, 4, 0, 21, -5, 0x1ee680df);
        Assert.Equal(retail, Hex(built));
        Assert.Equal(89, built.Length);
    }

    [Fact]
    public void AbilityRoundCarriesTheAbilityAndItsFlag()
    {
        // Tutorial 13:28:41: 45 03 00 04 00 01 00 01 01 01 0b 1e 00 c6 00 00 80 ...
        var move = RetailMove(0x0b, 0x1e, 198, CombatTactic.Power, 0x36, 0x1d, 0x11, 0x16);
        byte[] built = CombatInterlock.BuildRoundResult(3, true, move, RetailMove(4, 0x51, 0, CombatTactic.Speed, 0, 0, 0, 0),
            0x00f00007, CombatInterlock.ResultAbility, 0, 0, 0, 0, 0);
        Assert.StartsWith(Nospace("45 03 00 04 00 01 00 01 01 01 0b 1e 00 c6 00 00 80 36 00 1d 00 11 00 16 00"), Hex(built));
    }

    [Theory]
    [InlineData(true, true, false, false, 0x08u)]
    [InlineData(true, false, false, false, 0x01u)]
    [InlineData(false, true, false, false, 0x01u)]
    [InlineData(false, false, false, false, 0x04u)]
    [InlineData(false, true, true, false, 0x100u)]
    [InlineData(true, false, false, true, 0x41u)] // retail's killing round in the tutorial
    public void ResultFollowsTheHits(bool playerHit, bool npcHit, bool ability, bool died, uint expected)
    {
        Assert.Equal(expected, CombatInterlock.ResultFor(playerHit, npcHit, ability, died));
    }

    [Fact]
    public void BlockNeverLandsAHit()
    {
        var rng = new Random(7);
        for (int i = 0; i < 500; i++)
        {
            Assert.False(CombatInterlock.RollHit(rng, CombatTactic.Block, 50, CombatTactic.Grab, 1, out _, out _));
        }
    }

    [Fact]
    public void TacticsChangeTheRollsAsRetailsMultipliersSay()
    {
        int Hits(CombatTactic attacker, CombatTactic defender)
        {
            var rng = new Random(11);
            int hits = 0;
            for (int i = 0; i < 4000; i++)
            {
                if (CombatInterlock.RollHit(rng, attacker, 50, defender, 50, out _, out _)) hits++;
            }
            return hits;
        }
        // Tactic%: Speed +15 hits more often than Power -10. Defense%: Block +50 is hit less often than Speed -10.
        Assert.True(Hits(CombatTactic.Speed, CombatTactic.Power) > Hits(CombatTactic.Power, CombatTactic.Power));
        Assert.True(Hits(CombatTactic.Speed, CombatTactic.Block) < Hits(CombatTactic.Speed, CombatTactic.Speed));
    }

    [Fact]
    public void RollsHaveRetailsRange()
    {
        var rng = new Random(13);
        long attack = 0, defense = 0;
        for (int i = 0; i < 4000; i++)
        {
            ushort a = CombatInterlock.TacticRoll(rng, 0, CombatInterlock.AttackRollRange);
            ushort d = CombatInterlock.TacticRoll(rng, 0, CombatInterlock.DefenseRollRange);
            Assert.InRange(a, (ushort)1, (ushort)100);
            Assert.InRange(d, (ushort)1, (ushort)69);
            attack += a;
            defense += d;
        }
        // getTacticRoll centres on half the range
        Assert.InRange(attack / 4000.0, 48.0, 52.0);
        Assert.InRange(defense / 4000.0, 33.0, 36.0);
    }

    [Fact]
    public void NpcsUseAllTactics()
    {
        var rng = new Random(3);
        var seen = new HashSet<CombatTactic>();
        for (int i = 0; i < 500; i++) seen.Add(CombatInterlock.NpcTactic(rng));
        Assert.Contains(CombatTactic.Speed, seen);
        Assert.Contains(CombatTactic.Power, seen);
        Assert.Contains(CombatTactic.Grab, seen);
        Assert.Contains(CombatTactic.Block, seen);
    }

    [Theory]
    [InlineData(CombatTactic.Speed, 0x02u)] // Daze
    [InlineData(CombatTactic.Grab, 0x04u)] // Off Balance
    [InlineData(CombatTactic.Power, 0x08u)] // Staggered
    [InlineData(CombatTactic.Block, 0u)]
    public void TacticsCauseTheirCondition(CombatTactic tactic, uint bit)
    {
        Assert.Equal(bit, CombatInterlock.ConditionBit(tactic));
    }

    [Fact]
    public void ConditionUpdateMatchesRetail()
    {
        // NPC view while Staggered: group bytes then ConditionStateFlags 0x00100008
        Assert.Equal(Nospace("04 80 80 80 80 80 80 02 08 00 10 00"), Hex(Mob.BuildConditionUpdate(0x00100008)));
    }

    [Fact]
    public void CombatEffectRecordsMatchRetail()
    {
        // Tutorial 13:28:32: 80 bc 1d 00 5f 05 00 9e 02 00 00 72 00 b0 ff ff ff 00 00 00 00 00 (ids from another fight)
        var records = CombatInterlock.BuildCombatEffectRecords(0x055f, false);
        Assert.Equal(Nospace("80 bc 1d 00 5f 05 00 9e 02 00 00 72 00 b0 ff ff ff 00 00 00 00 00"), Hex(records[0]));
        Assert.Equal(Nospace("80 bc 15 00 60 05 00 9e 02 00 00 70 00 b0 ff ff ff 00 00 00 00 00"), Hex(records[1]));
    }

    [Fact]
    public void CombatLogNamesTheAttackersTactic()
    {
        // Retail after 42 01: the tactic field after the weapon is 1 (05 0a 00 00 01 00)
        string line = Hex(ServerPackets.BuildCombatLog(true, 0x13, -7, 0x44, 0x1c, true, 0x0a05, "Alex", "bboysex", CombatTactic.Power));
        Assert.Contains("050a00000100", line);
    }

    [Fact]
    public void GunNpcOpensFireWithinItsRangeOnly()
    {
        Assert.True(MobAggro.OpensFire(22f, 2199)); // AutomaticRifle1: 22 m
        Assert.False(MobAggro.OpensFire(22f, 2201));
        Assert.False(MobAggro.OpensFire(0u, 100)); // no weapon: close combat
    }
}
