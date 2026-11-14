using HeadlessClient;
namespace hds.Tests;
public class Rollback842FixtureTests
{
    [Fact]public void CapturedLegacySeedsInvertExistingScopedQaControl()
    {
        Assert.Equal(1814967188,hds.MissionStaging.QaSiteSeed("1","mxotest_owned",Rollback842Fixture.InitialSeed.ToString(),399905,0));
        Assert.Equal(1814967189,hds.MissionStaging.QaSiteSeed("1","mxotest_owned",Rollback842Fixture.Phase1Seed.ToString(),399905,1));
        Assert.Null(hds.MissionStaging.QaSiteSeed("1","mxo_live",Rollback842Fixture.Phase1Seed.ToString(),399905,1));
        Assert.Null(hds.MissionStaging.QaSiteSeed("0","mxotest_owned",Rollback842Fixture.Phase1Seed.ToString(),399905,1));
    }
    [Fact]public void ReproductionRejectsChangedPreGetOrigin()
    {
        Rollback842Fixture.PreGet(new double[]{27480,895,14650});
        Assert.ThrowsAny<Exception>(()=>Rollback842Fixture.PreGet(new double[]{27481,895,14650}));
        Assert.ThrowsAny<Exception>(()=>Rollback842Fixture.PreGet(new double[]{27480,945,14650}));
    }
    [Fact]public void ExactRetainedChoicePassesButChangedNaturalSourceRefuses()
    {
        const string raw="""{"exterior":false,"pos_x":8850,"pos_y":2945,"pos_z":10110,"rot":-90.9999883795,"quat":"00000000f20435bf00000000f304353f","sectorID":842,"metrId":1,"mxoStaticId":882902114,"staticId":1122,"type":"ggU=","strType":null}""";
        var spot=System.Text.Json.JsonSerializer.Deserialize<hds.StaticWorldObject>(raw,new System.Text.Json.JsonSerializerOptions{IncludeFields=true})!;
        var saved=new hds.MissionProgress.SavedState{Mission=399905,Phase=1,OpenFloorSites=new[]{new hds.MissionOpenFloorSite(1,219152420,28800,945,14700,Rollback842Fixture.Phase0Fingerprint),new hds.MissionOpenFloorSite(1,882901607,10900,2945,10400,Rollback842Fixture.Phase1Fingerprint)},
            Objects=new(){new hds.MissionProgress.SavedObject{Id=1,Goid=552,Spot=spot}}};
        Rollback842Fixture.Phase1(saved);
        saved.OpenFloorSites[0]=new hds.MissionOpenFloorSite(1,219152420,28800,945,14700,new string('0',64));
        Assert.ThrowsAny<Exception>(()=>Rollback842Fixture.Phase1(saved));
        saved.OpenFloorSites[0]=new hds.MissionOpenFloorSite(1,219152420,28800,945,14700,Rollback842Fixture.Phase0Fingerprint);
        spot.pos_x+=1;
        Assert.ThrowsAny<Exception>(()=>Rollback842Fixture.Phase1(saved));
    }
    [Fact]public void CurrentCursorAndPublishedPoseKeyRejectHistoricalSafe()
    {
        var centre=new double[]{10099.4033203125,2850,9950};const string q="00000000f20435bf00000000f304353f";
        HeadlessClient.ViewCreation View(ushort id,double x,ushort mission)=>new(id,552,1,new Dictionary<string,byte[]>{
            ["Position"]=new[]{x,centre[1],centre[2]}.SelectMany(BitConverter.GetBytes).ToArray(),
            ["Orientation"]=Convert.FromHexString(q),["MissionKey"]=new byte[]{255,(byte)mission,(byte)(mission>>8),2}});
        var historical=View(7,centre[0],47);var wrongPose=View(8,centre[0]+1,47);var wrongKey=View(9,centre[0],48);var current=View(10,centre[0],47);
        var snapshots=new[]{historical,wrongPose,wrongKey,current};
        Assert.Same(current,Assert.Single(snapshots.Skip(1).Where(v=>Rollback842Fixture.MatchesSafe(v,centre,q,47))));
        Assert.Empty(snapshots.Skip(4).Where(v=>Rollback842Fixture.MatchesSafe(v,centre,q,47)));
    }
}
