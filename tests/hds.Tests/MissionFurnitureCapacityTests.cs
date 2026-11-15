using hds;
namespace hds.Tests;
public class MissionFurnitureCapacityTests
{
    private static MissionSite Floor(int furniture,int generic)
    {
        var site=new MissionSite{style="SlumApt",minX=35700,maxX=35900,minZ=10975,maxZ=11175,elevatorDoor=new StaticWorldObject()};
        for(int i=0;i<furniture;i++)site.containers.Add(new StaticWorldObject());
        for(int i=0;i<generic;i++)site.objectSpots.Add(new StaticWorldObject());return site;
    }
    private static MissionSite? Pick(MissionSite site,MissionPhase phase)=>MissionSites.Choose(new[]{site},"Normal",35800,11075,0,
        MissionStaging.PlacesNeeded(phase),new Random(1),requiredFurniture:MissionStaging.FurnitureNeeded(phase));
    [Fact]
    public void MaraCounterexampleCannotSubstituteAGenericSpawnerForFurniture()
    {
        var phase=new MissionPhase();phase.objects.Add(new MissionObjectDef{id=1,kind="Furniture"});
        Assert.Null(Pick(Floor(0,1),phase));Assert.NotNull(Pick(Floor(1,0),phase));
        phase.objects[0].kind="Safe";Assert.NotNull(Pick(Floor(0,1),phase));
        phase.objects[0].kind="Phone";Assert.Null(Pick(Floor(0,1),phase));
    }
    [Theory]
    [InlineData(false,2,0)] [InlineData(true,2,0)] [InlineData(false,1,1)] [InlineData(true,1,1)]
    public void GenericAndFurnitureBothOrdersRequireTheSeparateFurnitureCapacity(bool reverse,int furniture,int generic)
    {
        var phase=new MissionPhase();var kinds=reverse?new[]{"Furniture","Computer"}:new[]{"Computer","Furniture"};
        foreach(string kind in kinds)phase.objects.Add(new MissionObjectDef{id=(ushort)(phase.objects.Count+1),kind=kind});
        Assert.Equal(1,MissionStaging.FurnitureNeeded(phase));
        Assert.NotNull(Pick(Floor(furniture,generic),phase));
        Assert.Null(Pick(Floor(0,2),phase));
        // A spawned kind takes a spawner before furniture. The combined-capacity and furniture inequalities
        // therefore hold after either first allocation; XML ordering cannot consume the later required slot.
    }
    [Fact]
    public void MultipleRequiredKindsCannotShareOneFurnitureSlot()
    {
        var phase=new MissionPhase();foreach(string kind in new[]{"Computer","Furniture","Phone"})phase.objects.Add(new MissionObjectDef{kind=kind});
        Assert.Equal(2,MissionStaging.FurnitureNeeded(phase));Assert.Null(Pick(Floor(1,2),phase));Assert.NotNull(Pick(Floor(2,1),phase));
    }
    [Theory]
    [InlineData("0","mxotest_zion","230410")] [InlineData("1","reality_hd","230410")]
    [InlineData("1","mxotest_","230410")] [InlineData("1","mxotest_zion","-1")] [InlineData("1","mxotest_zion","not-an-int")]
    public void LayoutSeedCannotAffectOrdinaryServerRandomness(string dev,string database,string seed)=>Assert.Null(MissionStaging.QaSiteSeed(dev,database,seed,2304,0));
    [Fact]
    public void FixtureLayoutSeedIsStableAndPhaseSpecific()
    {
        var a=MissionStaging.QaSiteSeed("1","mxotest_zion","230410",2304,0);Assert.NotNull(a);
        Assert.Equal(a,MissionStaging.QaSiteSeed("1","mxotest_zion","230410",2304,0));
        Assert.NotEqual(a,MissionStaging.QaSiteSeed("1","mxotest_zion","230410",2304,1));
    }
}
