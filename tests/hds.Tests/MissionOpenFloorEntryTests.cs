using hds;
using System.Text.Json;
using G=hds.MissionOpenFloorResolver;

namespace hds.Tests;

public class MissionOpenFloorEntryTests
{
    private static string Repo
    { get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds","Hardline Dreams MxO server.csproj")))p=Path.GetDirectoryName(p)!;return p;} }
    private static G.Position Pos(JsonElement a) => new(a[0].GetDouble(),a[1].GetDouble(),a[2].GetDouble());
    [Fact]
    public void SixLiteralSelectedSiteEntriesMatchExistingNormalRideArithmeticAndActualLocate()
    {
        SectorProperties.Load(Path.Combine(Repo,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data","navmesh"));
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"tests","hds.Tests","MissionOpenFloorEntryCases.json")));
        var cases=doc.RootElement.GetProperty("Cases").EnumerateArray().ToArray();Assert.Equal(6,cases.Length);
        foreach(var c in cases)
        {
            var r=MissionOpenFloorEntry.Calculate(1,Pos(c.GetProperty("Door")),c.GetProperty("Centre")[0].GetDouble(),c.GetProperty("Centre")[1].GetDouble(),c.GetProperty("Proof").GetString()!);
            Assert.Null(r.Refusal);Assert.NotNull(r.Root);var expected=Pos(c.GetProperty("Expected"));
            Assert.Equal(expected.X,r.Published.X,6);Assert.Equal(expected.Y,r.Published.Y,6);Assert.Equal(expected.Z,r.Published.Z,6);
            var id=c.GetProperty("RootId");Assert.Equal(new G.Id(id[0].GetUInt16(),id[1].GetUInt16(),id[2].GetUInt16()),r.Root.Surface);
            Assert.True(r.CarPlaceNear);Assert.Equal(c.GetProperty("Fallback").GetBoolean(),r.FallbackUsed);
            Assert.True(NavMesh.Locate(1,r.Published.X,r.Published.Y,r.Published.Z,out var at));Assert.Equal(at.floorY+95,r.Published.Y,6);
        }
    }
    [Fact]
    public void FailedCarPlaceNearDoesNotInventFallbackAndFinalLocateStillMustSucceed()
    {
        var queried=new List<G.Position>();var door=new G.Position(0,145,0);
        var r=MissionOpenFloorEntry.Calculate(door,100,0,"literal",p=>false,p=>{queried.Add(p);return null;});
        Assert.False(r.CarPlaceNear);Assert.False(r.FallbackUsed);Assert.Single(queried);Assert.Equal(new G.Position(200,95,0),r.Published);
        Assert.Null(r.Root);Assert.Equal("EntryLocateFailed",r.Refusal);
    }
    [Fact]
    public void CompleteFailedNearestSearchRetainsDoorOutputButRefusesStrictRoot()
    {
        int calls=0;var r=MissionOpenFloorEntry.Calculate(new G.Position(0,145,0),100,0,"literal",p=>true,p=>{calls++;return null;});
        Assert.True(r.FallbackUsed);Assert.False(r.NearestFound);Assert.Equal(130,calls);Assert.Equal(new G.Position(0,95,0),r.Published);
        Assert.Null(r.Root);Assert.Equal("EntryLocateFailed",r.Refusal);
    }
    [Fact]
    public void NearestSearchUsesOriginalRadiusAngleOrderingAndStandingIsNotSilentlyChanged()
    {
        var positions=new List<G.Position>();
        NavLocation? Locate(G.Position p)
        {
            positions.Add(p);return p.X==50&&p.Z==0?new NavLocation{sector=7,region=8,polygon=9,floorY=0}:null;
        }
        var r=MissionOpenFloorEntry.Calculate(new G.Position(0,145,0),100,0,"literal",p=>true,Locate);
        Assert.True(r.NearestFound);Assert.Equal(new G.Position(50,95,0),r.Published);Assert.Equal(3,positions.Count);
        Assert.Equal(new G.Id(7,8,9),r.Root.Surface);
        var wrong=MissionOpenFloorEntry.Calculate(new G.Position(0,145,0),100,0,"literal",p=>false,p=>new NavLocation{floorY=400});
        Assert.Equal(95,wrong.Published.Y);Assert.Null(wrong.Root);Assert.Equal("EntryStandingHeightMismatch",wrong.Refusal);
    }
    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public void NonfiniteLocatedFloorCannotBecomeAnAdmittedRoot(double floor)
    {
        var r=MissionOpenFloorEntry.Calculate(new G.Position(0,145,0),100,0,"literal",p=>false,p=>new NavLocation{floorY=floor});
        Assert.Null(r.Root);Assert.Equal("EntryNonfiniteFloor",r.Refusal);
    }

    [Fact]
    public void NonfiniteOrMissingEntryEvidenceRefusesBeforeAnyQuery()
    {
        bool called=false;
        var r=MissionOpenFloorEntry.Calculate(new G.Position(double.NaN,145,0),100,0,"x",p=>{called=true;return true;},p=>null);
        Assert.Equal("InvalidEntryInput",r.Refusal);Assert.False(called);
        r=MissionOpenFloorEntry.Calculate(new G.Position(0,145,0),100,0,"",p=>{called=true;return true;},p=>null);
        Assert.Equal("InvalidEntryInput",r.Refusal);Assert.False(called);
        r=MissionOpenFloorEntry.Calculate(new G.Position(double.MaxValue,145,0),-double.MaxValue,0,"x",p=>{called=true;return true;},p=>null);
        Assert.Equal("InvalidEntryArithmetic",r.Refusal);Assert.False(called);
    }
}
