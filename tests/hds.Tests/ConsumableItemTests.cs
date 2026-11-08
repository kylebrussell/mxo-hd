using hds;
using hds.shared;

namespace hds.Tests;

public class ConsumableItemTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    [Fact]
    public void HealthPillsMapToTheirReplenishAbilities()
    {
        Assert.True(ConsumableItems.TryGet(2399, out var pill1)); // Health Pill 1.0
        Assert.Equal(190, pill1.AbilityId);                      // PillHealthReplenish1Ability
        Assert.Equal(ConsumableItems.Effect.Heal, pill1.Effect);
        Assert.Equal(511, ConsumableItems.Items[7913].AbilityId); // Health Pill 5.0
        Assert.False(ConsumableItems.TryGet(7332, out _));       // Code Bit 1 is not usable
    }

    [Fact]
    public void PillsShareTheRetailHealPillTimer()
    {
        // Retail (abilities_mobiuscode): 80 bd 01 c7 02 00 00 00 20 41 01 = aaHealPill group, 10 s
        Assert.Equal("80bd01c70200000020410" + "1",
            Hex(AbilityCooldowns.BuildTimer(AbilityCooldowns.HealPill, AbilityCooldowns.GroupTimer, 10f, true)));
        foreach (var consumable in ConsumableItems.Items.Values)
        {
            Assert.Equal(AbilityCooldowns.HealPill, AbilityCooldowns.Retail[consumable.AbilityId].Group);
            Assert.Equal(10f, AbilityCooldowns.Retail[consumable.AbilityId].GroupSeconds);
        }

        // Mobius Code started the same group alongside its own 600 s timer.
        Assert.Equal(AbilityCooldowns.HealPill, AbilityCooldowns.Retail[967].Group);
        Assert.Equal(600f, AbilityCooldowns.Retail[967].Seconds);
    }

    [Fact]
    public void AnotherPillWaitsForTheSharedTimer()
    {
        ClientData data = new ClientData();
        data.cooldownEndsAt[AbilityCooldowns.HealPill] = 110;
        Assert.True(AbilityCooldowns.IsCoolingDown(data, 191, 105));
        Assert.False(AbilityCooldowns.IsCoolingDown(data, 191, 111));
    }
}
