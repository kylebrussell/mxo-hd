using System.Collections.Generic;
using System.Linq;
using hds;
using hds.shared;

namespace hds.Tests;

public class PlayerProgressionTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static byte[] Bytes(string hex) => StringUtils.hexStringToBytes(hex.Replace(" ", ""));

    // ---- experience and levels ---------------------------------------------------------------------------------

    [Theory]
    // Totals around each retail level-up in the Recursion capture.
    [InlineData(1, 0, 1)]
    [InlineData(1, 501, 2)]
    [InlineData(2, 4318, 2)]
    [InlineData(2, 9818, 3)]
    [InlineData(3, 11665, 3)]
    [InlineData(3, 22665, 4)]
    [InlineData(4, 30274, 4)]
    [InlineData(4, 63274, 5)]
    public void LevelsFollowTheRetailExperienceTotals(int current, long experience, int expected)
    {
        Assert.Equal(expected, PlayerProgression.LevelForExperience(current, experience));
    }

    [Fact]
    public void BigGainsSkipLevelsAndLevelStopsAtFifty()
    {
        Assert.Equal(8, PlayerProgression.LevelForExperience(1, 224466));
        Assert.Equal(50, PlayerProgression.LevelForExperience(1, 800000000));
        Assert.Equal(50, PlayerProgression.LevelForExperience(50, 800000000));
    }

    [Fact]
    public void ExperienceNeverTakesALevelAway()
    {
        Assert.Equal(20, PlayerProgression.LevelForExperience(20, 0));
    }

    [Theory]
    [InlineData(1, 150, 100)]
    [InlineData(2, 200, 103)]
    [InlineData(3, 250, 106)]
    [InlineData(4, 300, 109)]
    [InlineData(5, 350, 112)]
    public void StatsAtLevelMatchRetailOwnViews(int level, ushort health, ushort innerStrength)
    {
        Assert.Equal(health, PlayerProgression.MaxHealthAtLevel(level));
        Assert.Equal(innerStrength, PlayerProgression.MaxInnerStrengthAtLevel(level));
    }

    [Fact]
    public void LevelUpUpdateMatchesRetail()
    {
        // uriah tutorial, level 2, with retail's MissionKey group (c0 ffffffff) taken out.
        Assert.Equal("0200" + "02" + "8080" + "83c80002" + "d4670067 00c800".Replace(" ", "") + "30b905002801",
            Hex(ServerPackets.BuildLevelUpUpdate(2, 200, 103, 103, 200, 1)));
    }

    [Fact]
    public void LevelUpMessageIsTheRetailOne()
    {
        Assert.Equal("2e1700000000000000000000008900006000000000000000000000000000000000000000",
            Hex(ServerPackets.BuildLevelUpMessage()));
    }

    [Fact]
    public void ExperienceTotalCarriesTheGainType()
    {
        Assert.Equal("80e57c03000001000000", Hex(ServerPackets.BuildEXPTotal(0x37c, 1)));
    }

    // ---- ability upgrades --------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 1, 1, 800)]         // Recursion: 80 e7 e0 fc ff ff
    [InlineData(2, 2, 2, 5000)]        // Recursion, +2 from level 2
    [InlineData(2, 4, 1, 5000)]
    [InlineData(45, 1, 3, 5800)]       // Unknown: 2d 00, levels 2-4
    [InlineData(113, 1, 3, 5800)]
    [InlineData(37, 1, 3, 2900)]       // WriteCode pays half
    [InlineData(1006, 1, 3, 2900)]     // HackerCombatStyle pays half
    [InlineData(477, 32, 1, 217800)]   // Vector: dd 01 to level 33
    [InlineData(298, 1, 49, 8584800)]  // Vector: 2a 01 up to 50 in one go
    [InlineData(391, 30, 20, 6694000)] // Vector: 87 01 from 30 to 50
    public void UpgradeCostMatchesRetail(ushort ability, int from, int levels, long cost)
    {
        Assert.Equal(cost, AbilityProgression.UpgradeCost(ability, from, levels));
    }

    [Fact]
    public void UpgradeRequestParsesAbilitySlotAndLevels()
    {
        AbilityProgression.UpgradeRequest request = AbilityProgression.ParseUpgrade(Bytes("dd01370101")).Value;
        Assert.Equal(0x1dd, request.Ability);
        Assert.Equal(0x137, request.Slot);
        Assert.Equal(1, request.Levels);

        Assert.Null(AbilityProgression.ParseUpgrade(Bytes("dd0137")));
    }

    [Fact]
    public void UpgradeAnswerMatchesRetail()
    {
        Assert.Equal("80b80200020000000000", Hex(ServerPackets.BuildAbilityUpgraded(2, 2)));
    }

    // ---- loading and unloading ---------------------------------------------------------------------------------

    [Fact]
    public void LoadRequestParsesAndAnswersLikeRetail()
    {
        // Vector loads Force ability 0x274 (level 50) in slot 0x189.
        Assert.True(AbilityProgression.TryParseLoadout(Bytes("00000000 0000 0a00 0100 00 8901 74020000 3200"),
            out bool unload, out List<AbilityProgression.LoadoutEntry> entries));
        Assert.False(unload);
        AbilityProgression.LoadoutEntry entry = Assert.Single(entries);

        Assert.Equal("80b2740232008901", Hex(ServerPackets.BuildAbilityLoad(entry.Ability, entry.Level, entry.Slot)));
    }

    [Fact]
    public void UnloadRequestParsesAndAnswersLikeRetail()
    {
        Assert.True(AbilityProgression.TryParseLoadout(Bytes("00000000 0a00 0000 0100 00 8901 74020000 3200"),
            out bool unload, out List<AbilityProgression.LoadoutEntry> entries));
        Assert.True(unload);

        Assert.Equal("80b37402", Hex(ServerPackets.BuildAbilityUnload(Assert.Single(entries).Ability)));
    }

    [Fact]
    public void LoadRequestWithSeveralAbilitiesReadsEachOne()
    {
        // proxy_025 hacker tree, first entries.
        Assert.True(AbilityProgression.TryParseLoadout(
            Bytes("00000000 0000 0a00 0300 00 0e00 4d000000 0000 00 f401 ee030000 3200 00 1100 2d000000 3200"),
            out bool unload, out List<AbilityProgression.LoadoutEntry> entries));

        Assert.False(unload);
        Assert.Equal(new ushort[] { 0x4d, 0x3ee, 0x2d }, entries.Select(e => e.Ability).ToArray());
        Assert.Equal(new ushort[] { 0x0e, 0x1f4, 0x11 }, entries.Select(e => e.Slot).ToArray());
        Assert.Equal(new ushort[] { 0, 50, 50 }, entries.Select(e => e.Level).ToArray());
    }

    [Fact]
    public void TruncatedLoadRequestIsRefused()
    {
        Assert.False(AbilityProgression.TryParseLoadout(Bytes("00000000 0000 0a00 0200 00 0e00 4d000000 0000"),
            out _, out _));
    }

    [Fact]
    public void HiddenAbilitiesFollowRetail()
    {
        // 80 b2 25 00 0f 00 19 00 came with 80 b2 ca 03 00 00 08 02 (proxy_022).
        Assert.Equal((ushort)970, AbilityProgression.GrantOf(37));
        Assert.Equal("80b2ca0300000802", Hex(ServerPackets.BuildAbilityLoad(970,
            AbilityProgression.GrantLevel(37, 15), AbilityProgression.HiddenSlot)));

        // Awakened's follows its level: 80 b2 11 00 05 00 08 02.
        Assert.Equal("80b2110005000802", Hex(ServerPackets.BuildAbilityLoad(AbilityProgression.SelfDefenseAbility,
            AbilityProgression.GrantLevel(AbilityProgression.AwakenedAbility, 5), AbilityProgression.HiddenSlot)));

        Assert.Null(AbilityProgression.GrantOf(45));
    }

    // ---- buying abilities --------------------------------------------------------------------------------------

    [Fact]
    public void BuyingAnAbilityMatchesRetail()
    {
        // others_buyawakend: 81 0e 00 08 00 80 00 at 100 info, bag slot 0x58.
        const uint awakened = 0x80000800;
        Assert.True(AbilityProgression.IsAbilityCode(awakened));
        Assert.False(AbilityProgression.IsAbilityCode(2399));

        Assert.Equal("5e58002e0000000108008001", Hex(ServerPackets.BuildAbilityCodeAdd(0x58, awakened)));
        Assert.Equal("810f2e000000010800806400000000", Hex(ServerPackets.BuildAbilityCodeBuyAck(awakened, 100)));
    }

    [Fact]
    public void KnownAbilityEntrySharesTheCodeStorageLayout()
    {
        Assert.Equal("5e2400000000000108008000",
            Hex(ServerPackets.BuildKnownAbilityAdd(0x24, AbilityProgression.AbilityCodeValue(0x80000800))));
    }

    // ---- attributes --------------------------------------------------------------------------------------------

    [Fact]
    public void AttributeRaiseParsesAndAnswersLikeRetail()
    {
        CharacterAttributes.Raise raise = Assert.Single(CharacterAttributes.ParseRaises(Bytes("0400 0100 00 51000000 02")));
        Assert.Equal(CharacterAttributes.Reason, raise.Attribute);
        Assert.Equal(2, raise.Points);

        Assert.Equal("80b60800000000000100005100000009", Hex(ServerPackets.BuildAttributeRaised(0x51, 9)));
    }

    [Fact]
    public void SpentPointsListUsesRetailLayout()
    {
        // Warboy, level 50: 03 4e000000 1600 52000000 1900 54000000 0200.
        byte[] list = CharacterAttributes.BuildSpentPoints(new Dictionary<byte, ushort>
        {
            { CharacterAttributes.Focus, 30 }, { CharacterAttributes.Perception, 8 }, { CharacterAttributes.Reason, 8 },
            { CharacterAttributes.Belief, 33 }, { CharacterAttributes.Vitality, 10 }
        });
        Assert.Equal("034e000000160052000000190054000000" + "0200", Hex(list));
    }

    [Fact]
    public void FreshCharacterHasSpentNothing()
    {
        Assert.Equal("00", Hex(CharacterAttributes.BuildSpentPoints(CharacterAttributes.All.ToDictionary(
            a => a, a => CharacterAttributes.StartingValue))));
    }

    [Fact]
    public void CharactersWithoutRowsKeepTheOldList()
    {
        Assert.Equal("03510000000400520000000b00540000000100",
            Hex(CharacterAttributes.BuildSpentPoints(new Dictionary<byte, ushort>())));
    }

    [Fact]
    public void AttributeRowsStayOutOfTheAbilitySlots()
    {
        Assert.True(CharacterAttributes.IsHiddenSlot(CharacterAttributes.SlotOf(CharacterAttributes.Focus)));
        // The highest ability slot in the retail upgrade captures.
        Assert.False(CharacterAttributes.IsHiddenSlot(0x1f4));
        // Packed like an ability id: the 0x80 flag with the id shifted up 10 bits.
        Assert.Equal(unchecked((int)0x80013800), CharacterAttributes.RowValueOf(CharacterAttributes.Focus));
    }
}
