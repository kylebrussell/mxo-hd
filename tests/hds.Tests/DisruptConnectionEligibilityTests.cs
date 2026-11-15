using hds;
namespace hds.Tests;

public class DisruptConnectionEligibilityTests
{
    [Theory]
    [InlineData(470u)]
    [InlineData(531u)]
    [InlineData(0u)]
    public void OtherToolsCannotSatisfy1124(uint goid)
    {
        Assert.True(DisruptConnectionEligibility.Refused(1124, new[] {new Crafting.InventoryItem(1,goid)}));
        Assert.True(DisruptConnectionEligibility.Refused(5220, new[] {new Crafting.InventoryItem(1,goid)}));
        Assert.False(DisruptConnectionEligibility.Refused(384, new[] {new Crafting.InventoryItem(1,goid)}));
    }
    [Fact]
    public void ShippedPrerequisitesPreserveDistinctIdentities()
    {
        Assert.Equal((ushort)1125,DisruptConnectionEligibility.Tools[470]);
        Assert.DoesNotContain((ushort)1124,DisruptConnectionEligibility.Tools.Values);
        Assert.True(AbilityCatalog.Find(1124).Number("Ability/RequiresTool") == 1);
        Assert.Empty(AbilityCatalog.Find(1124).Modifiers);
        Assert.Equal(3,AbilityPassives.Modifiers(384).Count());
        Assert.True(DisruptConnectionEligibility.Refused(1124,Array.Empty<Crafting.InventoryItem>()));
    }
}
