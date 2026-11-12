using hds;
using hds.shared;

namespace hds.Tests;

public class ConsumableItemTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static byte[] Bytes(string hex) => StringUtils.hexStringToBytes(hex.Replace(" ", ""));

    [Fact]
    public void HealthPillsMapToTheirReplenishAbilities()
    {
        Assert.True(ConsumableItems.TryGet(2399, out var pill1)); // Health Pill 1.0
        Assert.Equal(190, pill1.AbilityId);                      // PillHealthReplenish1Ability
        Assert.Equal(ConsumableItems.Effect.Heal, pill1.Effect);
        Assert.Equal(AbilityCooldowns.HealPill, pill1.Group);
        Assert.Equal(511, ConsumableItems.Items[7913].AbilityId); // Health Pill 5.0
        Assert.False(ConsumableItems.TryGet(7332, out _));       // Code Bit 1 is not usable
        Assert.False(ConsumableItems.IsUsable(ConsumableItems.Items[2407])); // Health Boost needs buffs
    }

    [Fact]
    public void AntibioticMatchesTheRetailUse()
    {
        // PacketsZombiesPortingEtc line 77036: client 80 b9 a6 02 (678) ... 08 1e 01 00 00 00 00 for Antibiotic 5.0
        var antibiotic = ConsumableItems.Items[39410];
        Assert.Equal(678, antibiotic.AbilityId);
        Assert.Equal(AbilityCooldowns.AntibioticPill, antibiotic.Group);
        Assert.Equal(8f, antibiotic.Seconds);
    }

    [Theory]
    [InlineData(39406u, 0)]
    [InlineData(39407u, 10)]
    [InlineData(39408u, 20)]
    [InlineData(39409u, 30)]
    [InlineData(39410u, 40)]
    [InlineData(38539u, 0)]
    public void SweepConsumablesAreUsableWithTheirStoredCharacterRequirements(uint item, int level)
    {
        Assert.True(ConsumableItems.IsUsable(ConsumableItems.Items[item]));
        Assert.Equal(level, ConsumableItems.RequiredLevel(item));
    }

    [Fact]
    public void ItemSlotComesFromTheAbilityRequest()
    {
        // Retail Antibiotic 5.0 from bag slot 0x1e (body after the 80 b9 header).
        byte[] itemUse = Bytes("a6 02 0b 00 2f 00 " + string.Concat(System.Linq.Enumerable.Repeat("00 ", 28)) + "08 1e 01 00 00 00 00");
        Assert.Equal(41, itemUse.Length);
        Assert.Equal((ushort)0x1e, ConsumableItems.ItemSlotOfAbilityRequest(itemUse));

        // Retail cookie from slot 0x41 with no target.
        byte[] cookie = Bytes("ea 04 " + string.Concat(System.Linq.Enumerable.Repeat("00 ", 32)) + "08 41 01 00 00 00 00");
        Assert.Equal((ushort)0x41, ConsumableItems.ItemSlotOfAbilityRequest(cookie));

        // A plain ability (Repair RSI style) ends in 08 6b 00 00 00 00 00: no item.
        byte[] plain = Bytes("87 00 0c 00 9f 00 " + string.Concat(System.Linq.Enumerable.Repeat("00 ", 28)) + "08 6b 00 00 00 00 00");
        Assert.Null(ConsumableItems.ItemSlotOfAbilityRequest(plain));
        Assert.Null(ConsumableItems.ItemSlotOfAbilityRequest(new byte[] { 0x87, 0x00 }));
    }

    [Fact]
    public void ItemTimersMatchRetail()
    {
        // Antibiotic 5.0 (goid 39410 = 0x99f2): 80 bd 01 f2 99 00 00 00 00 41 01 and group 740: 80 bd 01 e4 02 00 ...
        Assert.Equal("80bd01f29900000000410" + "1", Hex(AbilityCooldowns.BuildTimer(39410, AbilityCooldowns.GroupTimer, 8f, true)));
        Assert.Equal("80bd01e40200000000410" + "1", Hex(AbilityCooldowns.BuildTimer(AbilityCooldowns.AntibioticPill, AbilityCooldowns.GroupTimer, 8f, true)));
        // aaHealPill (abilities_mobiuscode): 80 bd 01 c7 02 00 00 00 20 41 01
        Assert.Equal("80bd01c70200000020410" + "1", Hex(AbilityCooldowns.BuildTimer(AbilityCooldowns.HealPill, AbilityCooldowns.GroupTimer, 10f, true)));

        // Mobius Code started the same pill group alongside its own 600 s timer.
        Assert.Equal(AbilityCooldowns.HealPill, AbilityCooldowns.Retail[967].Group);
        Assert.Equal(600f, AbilityCooldowns.Retail[967].Seconds);
    }

    [Fact]
    public void ARunningTimerBlocksTheNextPill()
    {
        ClientData data = new ClientData();
        data.cooldownEndsAt[AbilityCooldowns.HealPill] = 110;
        Assert.True(AbilityCooldowns.IsTimerRunning(data, AbilityCooldowns.HealPill, 105));
        Assert.False(AbilityCooldowns.IsTimerRunning(data, AbilityCooldowns.HealPill, 111));
        Assert.False(AbilityCooldowns.IsTimerRunning(data, 2399, 105));
    }
}
