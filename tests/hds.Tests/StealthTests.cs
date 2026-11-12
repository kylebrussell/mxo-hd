using System.Linq;
using hds;
using hds.shared;

namespace hds.Tests;

public class StealthTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void SneakTestIsTheNativeFormula()
    {
        // client.dll 0x62513950: no stealth level is always seen
        Assert.True(Stealth.SneakTest(0, 0, 0, 5000, 99));
        // distfac - noise + 2*SL + roll < 2*SA + 50
        Assert.False(Stealth.SneakTest(10, 50, 0, 300, 0));     // 100 + roll vs 70
        Assert.True(Stealth.SneakTest(50, 10, 0, 300, 99));     // 20 + 99 vs 150
        Assert.True(Stealth.SneakTest(25, 25, 0, 300, 49));     // 99 < 100
        Assert.False(Stealth.SneakTest(25, 25, 0, 300, 50));    // 100 is not < 100
        // Distance counts in metres beyond 3: 13 m adds 10
        Assert.True(Stealth.SneakTest(25, 25, 0, 1300, 39));
        Assert.False(Stealth.SneakTest(25, 25, 0, 1300, 40));
        // Inside 3 m the distance adds nothing; noise is subtracted
        Assert.True(Stealth.SneakTest(25, 25, 0, 100, 49));
        Assert.True(Stealth.SneakTest(25, 25, 10, 300, 59));
        // Arguments are bytes in the native call
        Assert.Equal(Stealth.SneakTest(25, 25, 0, 300, 10), Stealth.SneakTest(25 + 256, 25, 0, 300, 10));
    }

    [Fact]
    public void RollAndRangeFollowTheScripts()
    {
        var random = new System.Random(1);
        for (int i = 0; i < 1000; i++) Assert.InRange(Stealth.Roll(random), 0, 99);
        // utility.py: 300 + (2000 - 2000 * level / 50), level capped at 50
        Assert.Equal(2300, Stealth.DetectionRangeUnits(0));
        Assert.Equal(1300, Stealth.DetectionRangeUnits(25));
        Assert.Equal(300, Stealth.DetectionRangeUnits(50));
        Assert.Equal(300, Stealth.DetectionRangeUnits(80));
        // misc.py: 30 s + 2 s a level
        Assert.Equal(30, Stealth.InvisibilitySeconds(0));
        Assert.Equal(130, Stealth.InvisibilitySeconds(50));
    }

    [Fact]
    public void FrustumIsSixtyDegreesEitherSide()
    {
        // Yaw 0 faces +z (MobAggro.YawTowards)
        Assert.True(Stealth.InFrustum(0, 0, 0, 0, 100));
        Assert.True(Stealth.InFrustum(0, 0, 0, 100, 100));     // 45 degrees
        Assert.False(Stealth.InFrustum(0, 0, 0, 100, 10));     // ~84 degrees
        Assert.False(Stealth.InFrustum(0, 0, 0, 0, -100));     // behind
        Assert.True(Stealth.InFrustum(0, 0, 128, 0, -100));    // turned round
        Assert.True(Stealth.InFrustum(0, 0, 64, 100, 0));      // a quarter turn faces +x
        Assert.Equal((byte)64, MobAggro.YawTowards(0, 0, 100, 0));
    }

    [Fact]
    public void ViewRecordsCarryDissemblingTypeAndFlags()
    {
        // Own view: attribute 36 (DissemblingType) and 42 (ConditionStateFlags)
        Assert.Equal("0200" + "02808080808082" + "0200" + "01" + "00000002",
            Hex(Stealth.BuildSelf(Stealth.Sneak, Stealth.StealthedFlag)));
        // Other views: attribute 27 and 30
        Assert.Equal("02808080c0" + "0400" + "04" + "00000002", Hex(Stealth.BuildOther(Stealth.Invisible, Stealth.StealthedFlag)));
        Assert.Equal("02808080c0" + "0000" + "04" + "00000001", Hex(Stealth.BuildOther(0, PvpFlag.PvpBit)));
    }

    [Fact]
    public void ClientConstantsAreTheDllValues()
    {
        // client.dll 0x62521e2f; kSpeeding 0x400 and kHyperSpeed 0x1000 match AbilityBuffs' retail captures
        Assert.Equal(0x02, Stealth.Sneak);
        Assert.Equal(0x04, Stealth.Invisible);
        Assert.Equal(0x10, Stealth.ShadowObject);
        Assert.Equal(0x0400, AbilityBuffs.Retail[184].DissemblingType);
        Assert.Equal(0x1000, AbilityBuffs.Retail[12].DissemblingType);
        // Condition table 0x6269ef26: ST_PVP and ST_STEALTHED are neighbours
        Assert.Equal(PvpFlag.PvpBit << 1, Stealth.StealthedFlag);
    }

    [Fact]
    public void StoredModifiersBecomeRecords()
    {
        // Sneak: Stealth (fn 2, ability level), Noise +10, StealthState, Movement Speed -200
        var sneak = Stealth.ModifiersFor(Stealth.Defs[Stealth.SneakGrantAbility], 40, 50);
        Assert.Contains(sneak, m => m.Stat == Stealth.StealthAbility && m.Value == 40 && m.Kind == AbilityBuffs.KindStat);
        Assert.Contains(sneak, m => m.Stat == Stealth.NoiseLevelBuffTargetAbility && m.Value == 10);
        Assert.Contains(sneak, m => m.Stat == Stealth.MovementSpeedAbility && m.Value == -200);
        Assert.Contains(sneak, m => m.Stat == Stealth.StealthStateAbility && m.Kind == AbilityBuffs.KindState);
        // Escape and Evasion: Sneak Movement 25 and Stealth from the character level (fn 3, scale 1)
        var evasion = Stealth.ModifiersFor(Stealth.Defs[Stealth.EscapeAndEvasionAbility], 10, 37);
        Assert.Contains(evasion, m => m.Stat == Stealth.SneakMovementAbility && m.Value == 25);
        Assert.Contains(evasion, m => m.Stat == Stealth.StealthAbility && m.Value == 37);
        // Concealment: Concealed and StealthState
        var conceal = Stealth.ModifiersFor(Stealth.Defs[Stealth.ConcealmentGrantAbility], 12, 50);
        Assert.Contains(conceal, m => m.Stat == Stealth.ConcealedAbility && m.Kind == AbilityBuffs.KindState);
        Assert.Contains(conceal, m => m.Stat == Stealth.StealthAbility && m.Value == 12);
        // Invisibility: Movement Speed -300 and StealthState (misc.py)
        var invis = Stealth.ModifiersFor(Stealth.Defs[Stealth.InvisibilityGrantAbility], 50, 50);
        Assert.Equal(new[] { (ushort)115, (ushort)881 }, invis.Select(m => m.Stat).ToArray());
        Assert.Empty(Stealth.ModifiersFor(Stealth.Defs[Stealth.InvisibilityImpartedAbility], 50, 50));
    }

    [Fact]
    public void OnlyTheStealthAbilitiesAreHandled()
    {
        foreach (ushort id in new ushort[] { 1111, 186, 507, 1137, 1116, 629, 366, 368, 1118 }) Assert.True(Stealth.Handles(id), id.ToString());
        foreach (ushort id in new ushort[] { 12, 184, 217, 123, 156, 1101, 1112, 1114 }) Assert.False(Stealth.Handles(id), id.ToString());
    }
}
