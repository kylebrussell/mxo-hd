using hds;
using NativeQa;

namespace hds.Tests;
public class SatiFallbackLayoutTests
{
    private static string Root { get { var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p; } }
    [Fact]
    public void FullReturnerFootprintAndStraightWalkStayOnOneSourceFloor()
    {
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        Assert.True(SatiFallbackLayout.TryGet(out var arrival,out var proxy));
        Assert.Equal((14225d,95d,-5225d),(arrival.x,arrival.y,arrival.z));
        Assert.Equal((14225d,95d,-4925d),(proxy.x,proxy.y,proxy.z));
        Assert.Equal(300,proxy.z-arrival.z);
        // Source returner25/95/95 and ordinary actor23/95/23 are disjoint, not merely different centres.
        Assert.True(proxy.z-arrival.z > 95+23);
        foreach(double x in new[]{proxy.x-25,proxy.x+25})
            foreach(double z in new[]{proxy.z-95,proxy.z+95})
            {
                Assert.True(NavMesh.Locate(9,x,95,z,out var loc));
                Assert.Equal((2,3,6),((int)loc.sector,(int)loc.region,(int)loc.polygon));
                Assert.Equal(0,loc.floorY);
            }
        Assert.True(NavMesh.Project(9,proxy.x,proxy.y,proxy.z,out _,0,arrival.Position,requireDirect:true));
        for(double z=arrival.z;z<=proxy.z;z+=25)Assert.True(NavMesh.Locate(9,arrival.x,95,z,out _));
        // Previous20-unit corner inset left three default38271 footprint corners off every same-floor polygon.
        Assert.False(NavMesh.Locate(9,14045,95,-4165,out _));
        Assert.False(NavMesh.Locate(9,14045,95,-3975,out _));
        Assert.False(NavMesh.Locate(9,14095,95,-3975,out _));
    }
    [Fact]
    public void NativeMainProfileNamesExplicitOursLayoutWithoutChangingDoorDiagnostic()
    {
        var plan=NativeQaModes.Find("sati").Plan;
        Assert.Equal(14225,plan.ExtraQaMetadata["SatiArrivalX"].GetDouble());
        Assert.Equal(-5225,plan.ExtraQaMetadata["SatiArrivalZ"].GetDouble());
        Assert.Equal(-4925,plan.ExtraQaMetadata["SatiReturnerZ"].GetDouble());
        Assert.True(plan.ExtraQaMetadata["RetailArrivalUnrecovered"].GetBoolean());
        Assert.False(plan.ExtraQaMetadata["GenericSourceReturnersBound"].GetBoolean());
        Assert.False(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(34800,HeadlessClient.NativeWorldDoorFixtures.Sati.X);
    }
}
