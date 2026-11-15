using System.Text.Json;
using hds;
namespace hds.Tests;
public class MissionDefinitionsTests
{
    static string Root
    {
        get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"data","mission_contacts.csv")))p=Path.GetDirectoryName(p)!;return p;}
    }
    static MissionDefinitionsTests()
    {
        MissionContacts.Load(Path.Combine(Root,"data","mission_contacts.csv"));
        MissionContactNpcs.Load(Path.Combine(Root,"data","mission_contact_npcs.csv"));
    }
    static readonly JsonSerializerOptions Json=new(){IncludeFields=true};
    [Fact] public void All153ExistingOffersResolveToTheirExactCanonicalDefinition()
    {
        var ids=new HashSet<int>();
        foreach(var spot in MissionContactNpcs.All)
        {
            var offered=NeighbourhoodMissions.Generic(spot.Contact);
            var rebuilt=NeighbourhoodMissions.Build(spot.Contact,spot);
            Assert.Equal(JsonSerializer.Serialize(offered,Json),JsonSerializer.Serialize(rebuilt,Json));
            foreach(var mission in offered)
            {
                Assert.True(ids.Add(mission.id));
                Assert.Same(mission,MissionDefinitions.Find(mission.id,Array.Empty<Mission>()));
            }
        }
        Assert.Equal(153,ids.Count);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(3000)] [InlineData(3001)]
    [InlineData(3070)] [InlineData(3074)] [InlineData(3079)] [InlineData(9999)]
    [InlineData(23001)] [InlineData(int.MinValue)] [InlineData(int.MaxValue)]
    public void UnknownKindContactOrgAndOverflowLikeIdsNeverBecomeAnotherTemplate(int id)=>
        Assert.Null(MissionDefinitions.Find(id,Array.Empty<Mission>()));
    [Fact] public void UnplacedContactsCannotUseTheBuildersNullSpotFallback()
    {
        var absent=MissionContacts.All.Where(c=>c.IsNeighbourhood&&!MissionContactNpcs.All.Any(s=>s.contactId==c.id)).ToArray();
        Assert.NotEmpty(absent);
        foreach(var contact in absent)foreach(int kind in new[]{1,2,3})
            Assert.Null(MissionDefinitions.Find(3000+(int)contact.id*10+kind,Array.Empty<Mission>()));
    }
    [Fact] public void AuthoredDefinitionsStayExactButAllAmbiguousAuthorityRefuses()
    {
        var file=new Mission{id=9001,title="Authored"};Assert.Same(file,MissionDefinitions.Find(9001,new[]{file}));
        Assert.Null(MissionDefinitions.Find(9001,new[]{file,new Mission{id=9001}}));
        Assert.Null(MissionDefinitions.Find(3072,new[]{new Mission{id=3072,title="Collision"}}));
        Assert.NotNull(MissionDefinitions.Find(3072,new[]{file}));
    }
    [Fact] public void ResolvingARelogKeepsAcceptanceAndActualObjectItemProgressWithoutRerolling()
    {
        var mission=MissionDefinitions.Find(3072,Array.Empty<Mission>())!;
        var accepted=new MissionProgress(mission){entered=true,waitingForExit=true,startedAt=1234};
        accepted.Complete(mission.phases[0].missionObjectives[0]);accepted.itemSlots[6831]=7;
        accepted.waypoints.Add(new MissionWaypoint{x=123,y=495,z=456,doorHex="2a000000"});accepted.exits.Add(new double[]{123,495,456});
        accepted.objects[1]=new MissionObjectState{def=mission.phases[0].objects[0],goid=1414,shownState=0x961,unlockedOnce=true,
            spot=new StaticWorldObject{mxoStaticId=42,metrId=1,sectorID=1147,pos_x=111,pos_y=222,pos_z=333}};
        var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(JsonSerializer.Serialize(accepted.Capture(23,Array.Empty<Mob>()),Json),Json)!;
        var restored=MissionProgress.Restore(MissionDefinitions.Find(saved.Mission,Array.Empty<Mission>()),saved);
        Assert.Equal(accepted.persistenceId,restored.persistenceId);Assert.Equal(1234,restored.startedAt);Assert.True(restored.waitingForExit);Assert.True(restored.entered);
        Assert.Equal((ushort)7,restored.itemSlots[6831]);Assert.True(restored.IsDone(1));
        Assert.Equal(42u,restored.objects[1].spot.mxoStaticId);Assert.Equal(111,restored.objects[1].spot.pos_x);
        Assert.Equal(0x961u,restored.objects[1].shownState);Assert.True(restored.objects[1].unlockedOnce);
        Assert.Equal(123,Assert.Single(restored.waypoints).x);Assert.Equal(456,Assert.Single(restored.exits)[2]);
        Assert.Same(mission.phases[0].objects[0],restored.objects[1].def);
        Assert.NotEqual(accepted.persistenceId,new MissionProgress(mission).persistenceId);
    }
    [Fact] public void ContactFixtureStandingPointUsesTheShippedWalkableFloor()
    {
        SectorProperties.Load(Path.Combine(Root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data","navmesh"));
        Assert.True(NavMesh.Locate(1,76015,485,15845,out var point));
        Assert.Equal(1147,point.sector);Assert.Equal(390,point.floorY);
    }
}
