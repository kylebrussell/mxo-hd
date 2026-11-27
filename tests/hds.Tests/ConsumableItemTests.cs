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
        Assert.True(ConsumableItems.IsUsable(ConsumableItems.Items[2407])); // Owned health boost path
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

    [Fact]
    public void QuicksilverItemUsesItsShippedTimersAndFixedBuff()
    {
        Assert.True(ConsumableItems.TryGet(47107, out var pill));
        Assert.Equal(1274, pill.AbilityId);
        Assert.Equal(45, ConsumableItems.RequiredLevel(47107));
        Assert.Equal(1f, AbilityCatalog.Find(pill.AbilityId).CastSeconds);
        Assert.Equal(30f, pill.Seconds);
        Assert.Equal(0, pill.Group);
        Assert.True(AbilityBuffs.TryGet(pill.AbilityId, out var buff));
        Assert.Equal(300f, buff.Seconds);
        Assert.Equal(0x0100, buff.DissemblingType);
        Assert.Equal(new[] { (115, 10), (183, 12), (1009, 12), (1010, 12), (1011, 12) },
            buff.Modifiers.OrderBy(mod => mod.Stat).Select(mod => ((int)mod.Stat, mod.Value)));
    }

    [Fact]
    public void StockRsiPillsResolveForTheMatchingAvatarOnly()
    {
        var parts = System.IO.File.ReadLines(DataLoader.DataPath("newrsiIDs.csv")).Skip(1).Select(line =>
        {
            string[] fields = line.Split(',');
            return new NewRSIItem { type = fields[3], newRSIID = ushort.Parse(fields[1]),
                internalId = ushort.Parse(fields[2]), gender = ushort.Parse(fields[4]) };
        }).ToArray();

        int supported = 0;
        for (uint id = 46736; id <= 46973; id++)
        {
            if (!RsiPills.TryGet(id, out var pill)) continue;
            supported++;
            Assert.InRange(pill.Resolve(pill.Sex, parts), 0, 31);
            Assert.Equal(-1, pill.Resolve(1 - pill.Sex, parts));
            Assert.True(ConsumableItems.TryGet(id, out var consumable));
            Assert.Equal(1245, consumable.AbilityId);
            Assert.Equal(0f, consumable.Seconds);
        }
        Assert.Equal(235, supported);
        Assert.False(ConsumableItems.TryGet(46736, out _)); // out-of-range hair-color callback
        Assert.True(RsiPills.TryGet(46954, out var hair));
        Assert.Equal(RsiPills.Part.Hair, hair.Field);
        Assert.Equal(2, hair.Resolve(0, parts));             // descriptor 107, not ordinal 107
        Assert.True(RsiPills.TryGet(46973, out var facial));
        Assert.Equal(1, facial.Resolve(0, parts));          // descriptor 101 in the facial detail allocation
        Assert.True(RsiPills.TryGet(46873, out var tattoo));
        Assert.Equal(2, tattoo.Resolve(0, parts));
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
    public void LeavingAndReenteringTheSameDistrictInvalidatesThePillCast()
    {
        var data = new ClientData();
        data.setDistrictId(1); data.setOnWorld(true);
        AbilityCooldowns.StartCast(data, 10, 1);
        int world = data.worldGeneration, life = data.lifeGeneration;
        double stamp = data.castEndsAt;
        Assert.True(ConsumableItems.CastMatches(data, world, life, stamp));
        data.setOnWorld(false); data.setOnWorld(true);
        Assert.Equal(1u, data.getDistrictId());
        Assert.False(ConsumableItems.CastMatches(data, world, life, stamp));
    }

    [Fact]
    public void RevivalAndAReplacementCastInvalidateTheOriginalPillCast()
    {
        var data = new ClientData();
        AbilityCooldowns.StartCast(data, 10, 1);
        int world = data.worldGeneration, life = data.lifeGeneration;
        double stamp = data.castEndsAt;
        data.lifeGeneration++; // death/revival may finish before the pending callback runs
        Assert.False(data.isDead);
        Assert.False(ConsumableItems.CastMatches(data, world, life, stamp));
        AbilityCooldowns.StartCast(data, 11, 1);
        Assert.False(ConsumableItems.CastMatches(data, world, data.lifeGeneration, stamp));
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
