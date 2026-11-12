using hds;

namespace hds.Tests;

public class DataMiningTests
{
    [Fact]
    public void RepeatedExternalStopsNeverReactivateOrDuplicateLocatorOffRecords()
    {
        var c = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData = new ClientData(); c.playerData.setDistrictId(1);
        c.messageQueue = new MessageQueue();
        Assert.False(UtilityAbilities.StopFindDataNode(c));
        Assert.Empty(c.messageQueue.RPCMessagesQueue);
        DataMining.Start(c,422);
        Assert.True(UtilityAbilities.StopFindDataNode(c));
        int records = c.messageQueue.RPCMessagesQueue.Count;
        Assert.False(UtilityAbilities.StopFindDataNode(c));
        Assert.False(DataMining.Searching(c));
        Assert.Equal(records,c.messageQueue.RPCMessagesQueue.Count);
        // Simulate visibility cleanup winning the race before the pickup's independent stop callback.
        DataMining.Start(c,423);
        Assert.True(DataMining.Stop(c));
        records = c.messageQueue.RPCMessagesQueue.Count;
        Assert.False(UtilityAbilities.StopFindDataNode(c));
        Assert.False(DataMining.Searching(c));
        Assert.Equal(records,c.messageQueue.RPCMessagesQueue.Count);
    }
    [Fact]
    public void BaseLocatorRemainsDistinctAndKeepsItsOwnActivationAndToolPrerequisites()
    {
        Assert.True(UtilityAbilities.Handles(215));
        Assert.True(UtilityAbilities.Handles(621));
        Assert.Equal(215,AbilityCatalog.Canonical(215));
        Assert.Equal(215,AbilityCatalog.IdOf((uint)AbilityCatalog.Find(621).Number("Ability/MasteryBaseAbility")));
        Assert.Equal(5,AbilityCosts.Cost(215));
        Assert.Equal(0,AbilityCosts.Cost(621));
        using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"data","utility_objects.json")));
        foreach(var tool in json.RootElement.GetProperty("locators").EnumerateArray())
            Assert.Equal(AbilityCatalog.IdOf(tool.GetProperty("fields").GetProperty("AbilityPrerequisite/ReqAbilityID").GetUInt32()),
                UtilityAbilities.LocatorRequirement(tool.GetProperty("goid").GetUInt32()));
        var c=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData(); c.messageQueue=new MessageQueue();
        DataMining.Start(c,423,215);
        Assert.True(DataMining.Stop(c,out ushort stopped));
        Assert.Equal(215,stopped);
        Assert.False(DataMining.Stop(c,out _));
    }
    [Fact]
    public void StoredGobDefinitionsRetainRealPickupComponentsAndLevels()
    {
        Assert.Equal(2,DataMining.Find(596).PickupUse);
        Assert.Equal(6,DataMining.Find(47014).PickupUse);
        Assert.Equal(11,DataMining.Find(47100).PickupUse);
        Assert.Equal(10,DataMining.Find(596).Level);
        Assert.Equal(40,DataMining.Find(47100).Level);
        Assert.Equal(3,DataMining.Find(47276).Exclusive.Length);
        Assert.True(DataMining.IsLight(596));
        Assert.False(DataMining.IsLight(606));
        Assert.False(DataMining.IsLight(2562));
        Assert.False(DataMining.IsLight(2399));
        Assert.NotNull(DataMining.BuildObject(new DataMining.Ground{Goid=596,District=1,X=1,Y=2,Z=3}));
    }
    [Fact]
    public void VisualMarkerUsesNativeFloatPositionAndTwentyByteLayout()
    {
        Assert.Equal("80CD3412620400280000C84200004843000096C3",
            Convert.ToHexString(DataMiningPackets.Marker(0x1234,0x28000462,100,200,-300)));
        Assert.Equal("80CE3412",Convert.ToHexString(DataMiningPackets.Remove(0x1234)));
        Assert.Equal("80CF",Convert.ToHexString(DataMiningPackets.Clear()));
        Assert.Equal("80CB5A64",Convert.ToHexString(DataMiningPackets.Indicator(90,100)));
    }

    [Fact]
    public void MapUsesFourByteNestedPositionRecordsAndSignedMetres()
    {
        var p = DataMiningPackets.Relative(10000,20000,9800,20300,2)!.Value;
        Assert.Equal("80C501000A00FE0302",Convert.ToHexString(DataMiningPackets.Map(new[]{p},10)));
        Assert.Null(DataMiningPackets.Relative(0,0,12800,0,0));
        Assert.NotNull(DataMiningPackets.Relative(0,0,-12800,12700,0));
        Assert.Null(DataMiningPackets.Relative(0,0,double.NaN,0,0));
        Assert.Equal(517,DataMiningPackets.Map(Enumerable.Repeat(p,1000),3).Length);
    }

    [Fact]
    public void SearchAndPickupCannotCrossDistrictsOrFloors()
    {
        var g = new DataMining.Ground{District=1,X=100,Y=200,Z=300};
        Assert.True(DataMining.InRange(g,1,100,200,400,100));
        Assert.False(DataMining.InRange(g,2,100,200,300,100));
        Assert.False(DataMining.InRange(g,1,100,451,300,100));
        Assert.False(DataMining.InRange(g,1,100,200,401,100));
        Assert.False(DataMining.CanPickup(g,1,100,301,300,100));
    }

    [Fact]
    public void PickupRequiresLevelSingletonAndNoExclusiveCarriedItem()
    {
        var d = new DataMining.Definition{Goid=596,Level=10,Singleton=true,Exclusive=new uint[]{561}};
        Assert.Null(DataMining.PickupRefusal(d,10,Array.Empty<Crafting.InventoryItem>()));
        Assert.Contains("level",DataMining.PickupRefusal(d,9,Array.Empty<Crafting.InventoryItem>()));
        Assert.Contains("exclusive",DataMining.PickupRefusal(d,50,new[]{new Crafting.InventoryItem(1,596)}));
        Assert.Contains("exclusive",DataMining.PickupRefusal(d,50,new[]{new Crafting.InventoryItem(1,561)}));
        d.Size=1;
        Assert.Contains("Heavy",DataMining.PickupRefusal(d,50,Array.Empty<Crafting.InventoryItem>()));
    }
}
