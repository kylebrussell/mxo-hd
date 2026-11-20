using HeadlessClient;
using hds;
using hds.shared;

namespace hds.Tests;

public class CraftingRequestTests
{
    [Theory]
    [InlineData(0, 0, 9, 9)]
    [InlineData(0, 25, 6, 7)]
    [InlineData(10, 25, 6, 6)]
    [InlineData(0, 100, 0, 0)]
    public void WriterChecksAffordabilityBeforeTruncatingTheDebit(int general, int writer, long debit, long required)
    {
        Assert.Equal((debit, required), CraftingRules.WriterCost(CraftingRules.Find(2399), general, writer));
    }

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
    public void CapturedWriterRecipeRemembersEachSelectedBagSlot()
    {
        byte[] body = WireFormat.FromHex(Scenarios.HealthPillWriterRequest)[2..];
        var recipe = new Crafting.Recipe { ResultGoid = 2399, Ingredients = new uint[] { 2435, 2438, 7458, 2472, 2454 } };
        var request = CraftingRequests.ParseCompile(body, writer: true);
        Assert.NotNull(request);
        Assert.Equal(2, request.ToolSlot);
        Assert.Equal(5, request.SelectedIngredients.Count);
        for (int i = 0; i < recipe.Ingredients.Length; i++)
            Assert.Equal(1, request.SelectedIngredients[((ushort)(4 + i), recipe.Ingredients[i])]);
        Assert.True(CraftingRequests.Matches(request, recipe));
        Assert.Null(CraftingRequests.ParseCompile(body));
        Assert.Null(CraftingRequests.ParseCompile(body[..^1], writer: true));
    }

    [Fact]
    public void WriterUsesStorageBitsAndCountsRepeatedSelections()
    {
        byte[] body = WireFormat.FromHex(Scenarios.HealthPillWriterRequest)[2..][..25];
        body[7] = 2; // Two records from the same archive stack.
        body[9] = 0x35;
        body[10] = 0x4e;
        body[11] = 0xff;
        body[12] = 1;
        body.AsSpan(9, 8).CopyTo(body.AsSpan(17, 8));
        var request = CraftingRequests.ParseCompile(body, writer: true);
        Assert.NotNull(request);
        Assert.Equal(2, request.SelectedIngredients[((ushort)(Crafting.StorageSlotFlag | 511), 2435)]);
        Assert.Equal(2, request.Fragments[2435]);
        body[10] = 0xce; // Missing-ingredient sentinel cannot authorize consumption.
        Assert.Null(CraftingRequests.ParseCompile(body, writer: true));
        body[10] = 0x4e;
        body[11] = 8;
        body[12] = 2; // Archive slot 520 exceeds the client array.
        Assert.Null(CraftingRequests.ParseCompile(body, writer: true));
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
        Assert.False(CraftingRules.Succeeds(0, 5, 50, 50));
        Assert.True(CraftingRules.Succeeds(0, 5, 51, 50));
        Assert.False(CraftingRules.Succeeds(0, 1, 100, 9));
        Assert.True(CraftingRules.Succeeds(1, 1, 51, -50));
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
