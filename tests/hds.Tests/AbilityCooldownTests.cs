using hds;
using hds.shared;

namespace hds.Tests;

// Shares the static DelayedActions timer queue: running due actions in one class would fire the other's.
[Collection("DelayedActions")]
public class AbilityCooldownTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void CooldownRecordsMatchRetail()
    {
        // Repair RSI 1.0 on self (abilities_cast_repairRSI1.0_tomyself): 80 bd 01 4d 00 04 00 00 80 40 01
        Assert.Equal("80bd014d000400008040" + "01", Hex(AbilityCooldowns.BuildTimer(0x4d, AbilityCooldowns.OwnTimer, 4.0f, true)));
        // Cheap Shot ending (luizoe Unknown log): 80 bd 01 c6 00 04 00 00 a0 41 00
        Assert.Equal("80bd01c600040000a041" + "00", Hex(AbilityCooldowns.BuildTimer(198, AbilityCooldowns.OwnTimer, 20.0f, false)));
        // The shared heal group alongside Repair RSI: 80 bd 01 ae 02 00 00 00 80 40 01
        Assert.Equal("80bd01ae020000008040" + "01", Hex(AbilityCooldowns.BuildTimer(AbilityCooldowns.Heal2, AbilityCooldowns.GroupTimer, 4.0f, true)));
    }

    [Fact]
    public void RetailDurations()
    {
        Assert.Equal(20f, AbilityCooldowns.Retail[198].Seconds); // Cheap Shot
        Assert.Equal(30f, AbilityCooldowns.Retail[197].Seconds); // Head Butt
        Assert.Equal(4f, AbilityCooldowns.Retail[77].Seconds);   // Repair RSI 1.0
    }

    [Fact]
    public void AbilityIsBlockedWhileItOrItsGroupCoolsDown()
    {
        ClientData data = new ClientData();
        data.cooldownEndsAt[198] = 120;
        Assert.True(AbilityCooldowns.IsCoolingDown(data, 198, 100));
        Assert.False(AbilityCooldowns.IsCoolingDown(data, 198, 120));

        // Logic Bomb 2 and Logic Blast 3.0 share aaVirusDamage1
        data.cooldownEndsAt[AbilityCooldowns.VirusDamage1] = 50;
        Assert.True(AbilityCooldowns.IsCoolingDown(data, 329, 49));
        Assert.False(AbilityCooldowns.IsCoolingDown(data, 329, 51));

        // Abilities without a known cooldown are never blocked
        Assert.False(AbilityCooldowns.IsCoolingDown(data, 12, 0));
    }

    [Fact]
    public void DelayedActionsRunWhenDue()
    {
        int ran = 0;
        DelayedActions.After(0, () => ran++);
        Assert.Equal(1, ran);

        DelayedActions.After(1000, () => ran += 10);
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds());
        Assert.Equal(1, ran);
        DelayedActions.RunDue(TimeUtils.GetSimTimeSeconds() + 1001);
        Assert.Equal(11, ran);
    }
}
