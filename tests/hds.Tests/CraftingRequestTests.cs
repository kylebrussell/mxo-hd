using hds;
using hds.shared;

namespace hds.Tests;

public class CraftingRequestTests
{
    [Fact]
    public void CompileUsesItsDeclaredArrayAndPackedSource()
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(7458u << 7));
        bytes.AddRange(new byte[] { 9, 0, 4, 1, 0 });
        bytes.AddRange(BitConverter.GetBytes(2u));
        bytes.AddRange(BitConverter.GetBytes(7336u));
        var request = CraftingRequests.ParseCompile(bytes.ToArray());
        Assert.NotNull(request);
        Assert.Equal(7458u, request.Item);
        Assert.Equal(4, request.ToolSlot);
        Assert.Equal(2, request.Fragments[7336]);
        Assert.True(CraftingRequests.Matches(request, new Crafting.Recipe { ResultGoid = 7458, Ingredients = new uint[] { 7336, 7336 } }));
        Assert.False(CraftingRequests.Matches(request, new Crafting.Recipe { ResultGoid = 7458, Ingredients = new uint[] { 7336 } }));
        Assert.Equal(7458u, CraftingRequests.SourceItem(CodeStorage.Pack(7458, 10)));
        Assert.Equal(0x80045000u, CraftingRequests.SourceItem(0x80045005));
        Assert.Null(CraftingRequests.ParseCompile(bytes.Take(bytes.Count - 1).ToArray()));
        bytes[4] = 1; // points inside the fixed header
        Assert.Null(CraftingRequests.ParseCompile(bytes.ToArray()));
        Assert.Null(CraftingRequests.ParseCompile(new byte[6]));
    }

    [Fact]
    public void InventoryAbilityIsAnAbilityAndSlotRatherThanARecipeGoid()
    {
        var request = CraftingRequests.ParseInventoryAbility(new byte[] { 0x14, 1, 0, 0, 5, 0x6b, 1 });
        Assert.NotNull(request);
        Assert.Equal(276u, request.Ability);
        Assert.Equal(5, request.Slot);
        Assert.Equal(0x6b, request.ToolSlot);
        Assert.True(request.IsRecipeKnown);
        Assert.Null(CraftingRequests.ParseInventoryAbility(new byte[8]));
        Assert.Null(CraftingRequests.ParseInventoryAbility(new byte[] { 0x14, 1, 0, 0, 5, 0x6b, 2 }));
        Assert.Equal("81-2F-01-05-5F-09-00-00-00-00-00-00", BitConverter.ToString(CraftingRequests.BuildResult(1, 5, 2399)));
    }

    [Fact]
    public void InventoryProducerRequiresTargetSevenAndCannotActivateTheWriterOrItsParent()
    {
        Assert.True(CraftingRequests.IsInventoryTargetAbility(276));
        Assert.False(CraftingRequests.IsInventoryTargetAbility(970));
        Assert.False(CraftingRequests.IsInventoryTargetAbility(37));
        Assert.False(CraftingRequests.IsInventoryTargetAbility(65536u + 276));
        Assert.Equal((ushort)970, AbilityProgression.GrantOf(37));
        Assert.Equal((ushort)37, AbilityCatalog.IdOf((uint)AbilityCatalog.Find(970).Number("Ability/AbilityID")));
        Assert.Equal((ushort)970, AbilityCatalog.IdOf(CraftingRules.Find(531).ToolAbility));
    }

    [Fact]
    public void StoredItemRulesAndCompileCostMatchTheScriptAndBinary()
    {
        var pill = CraftingRules.Find(2399);
        Assert.NotNull(pill);
        Assert.Equal(1, pill.Complexity);
        Assert.Equal(75, pill.Price);
        Assert.True(pill.Learnable);
        Assert.Equal(9, CraftingRules.InfoCost(pill));
        Assert.Equal(4, CraftingRules.InfoCost(pill, 50));
        Assert.Equal(0, CraftingRules.InfoCost(pill, 200));
        Assert.False(CraftingRules.Succeeds(0, 1, 100));
        Assert.False(CraftingRules.Succeeds(1, 1, 50)); // equal is failure
        Assert.True(CraftingRules.Succeeds(1, 1, 51));
        Assert.True(CraftingRules.Find(38538).CantDecompile); // PvP ticket
    }

    [Fact]
    public void FullBagPreflightConsidersFreedIngredientsAndStackRoom()
    {
        var bag = Enumerable.Range(1, 96).Select(i => new Crafting.InventoryItem((ushort)i, 1271)).ToList();
        Assert.False(CraftingRules.HasResultSpace(2399, bag, new ushort[] { 0x8000 })); // storage ingredients free no bag slot
        Assert.True(CraftingRules.HasResultSpace(2399, bag, new ushort[] { 3 }));
        bag[2] = new Crafting.InventoryItem(3, 2399, 2);
        Assert.True(CraftingRules.HasResultSpace(2399, bag, Array.Empty<ushort>()));
    }

    [Fact]
    public void ContainerRequestHasOneBagSlotAndAStatusReply()
    {
        Assert.Equal((byte)4, ContainerActions.Parse(new byte[] { 4 }));
        Assert.Null(ContainerActions.Parse(new byte[] { 0 }));
        Assert.Null(ContainerActions.Parse(new byte[] { 97 }));
        Assert.Null(ContainerActions.Parse(new byte[] { 4, 0 }));
        Assert.Equal(14, ContainerActions.BuildReply(2399, 0x82000300, 0).Length);
        Assert.True(ContainerActions.InReach(NumericalUtils.doublesToLtVector3d(100, 495, 200), 150, 500, 300));
        Assert.False(ContainerActions.InReach(NumericalUtils.doublesToLtVector3d(100, 495, 200), 150, 995, 300));
    }
}
