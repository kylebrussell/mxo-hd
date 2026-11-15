using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
[Collection("DelayedActions")]
public class MervBound326ScopeTests
{
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static MissionProgress.SavedState State(bool legacy)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures",legacy?"MervBound326Legacy-0651da78.json":"MervBound326-4e46e554.json")));
        return JsonSerializer.Deserialize<MissionProgress.SavedState>(doc.RootElement.GetProperty("State").GetRawText(),Fields)!;
    }
    [Fact] public void ExactLegacy0651IsAnExplicitOldProofScopeNotANewSamplerFallback()
    {
        var s=State(true);Assert.Equal(301203,s.Mission);Assert.Equal(2,s.Phase);Assert.Equal(0x46000076u,s.Objects[0].Spot.mxoStaticId);
        Assert.Equal(MervBound326Approach.ProofScope.Legacy,MervBound326Approach.Classify(s));
        SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
        var pose=MissionObjectPoses.For(new(){spot=s.Objects[0].Spot,goid=326});Assert.Equal((18300d,2080d,-8930d),(pose.X,pose.Y,pose.Z));double[]? standing=null;
        foreach(var d in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(0d,0d)})
            if(NavMesh.Locate(1,pose.X+d.Item1,pose.Y,pose.Z+d.Item2,out var at))
            {var point=new[]{pose.X+d.Item1,at.floorY+95,pose.Z+d.Item2};if(ContainerActions.InReach(point.SelectMany(BitConverter.GetBytes).ToArray(),pose.X,pose.Y,pose.Z)){standing=point;break;}}
        Assert.Equal(new[]{18300d,2095,-8830},standing); // exact pre0651 branch, only its original limited claim
        Assert.False(NavMesh.Project(1,17029.289321881344,2095,-8470.710678118654,out _,0,new[]{17029.289321881344,2095,-8470.710678118654},false));
    }
    [Fact] public void GenuineV1LegacyRemainsOutsideNewMeasuredProof()
    {
        var s=State(true);s.Version=1;s.OpenFloorSites=null!;s.OpenFloorCurrentSiteIndex=null;s.OpenFloorPhase=null;s.OpenFloorLegacyPhase=null;s.ElevatorWitnesses=null!;
        Assert.Equal(MervBound326Approach.ProofScope.Legacy,MervBound326Approach.Classify(s));
        s.ElevatorWitnesses=State(false).ElevatorWitnesses;Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Classify(s));
    }
    [Fact] public void ActualMeasured4eRemainsInsideTheStrictNewScope()
    {Assert.Equal(MervBound326Approach.ProofScope.Measured,MervBound326Approach.Classify(State(false)));}
    [Fact] public void ARealHistoricalWitnessForAnotherSiteDoesNotMakeCurrentLegacyMeasured()
    {
        var legacy=State(true);var measured=State(false);int i=measured.OpenFloorCurrentSiteIndex!.Value;
        legacy.OpenFloorSites=legacy.OpenFloorSites.Append(measured.OpenFloorSites[i]).ToArray();legacy.SiteDoors=legacy.SiteDoors.Append(measured.SiteDoors[i]).ToArray();legacy.ElevatorWitnesses=measured.ElevatorWitnesses;
        Assert.Equal(MervBound326Approach.ProofScope.Legacy,MervBound326Approach.Classify(legacy));
    }
    [Theory][InlineData("index")][InlineData("site-key")][InlineData("raw-door")][InlineData("duplicate")][InlineData("wrong-site")][InlineData("floor")][InlineData("bad-quaternion")][InlineData("source-json")][InlineData("marked-missing")][InlineData("adopted-missing")]
    public void MalformedOrMarkedCurrentEvidenceCannotDowngradeToLegacy(string fault)
    {
        var s=State(false);var w=s.ElevatorWitnesses.Single();switch(fault){case "index":s.OpenFloorCurrentSiteIndex=-1;break;
            case "site-key":s.OpenFloorSites[s.OpenFloorCurrentSiteIndex!.Value]=new(1,w.FloorKey+1,0,0,0,w.SiteFingerprint);break;case "raw-door":s.SiteDoors[s.OpenFloorCurrentSiteIndex!.Value].mxoStaticId++;break;
            case "duplicate":s.ElevatorWitnesses=new[]{w,w};break;
            case "wrong-site":s.ElevatorWitnesses=new[]{new MissionElevatorWitness(w.Version,"other-site",w.Region,w.Floor,w.Lobby,w.FloorQuaternion,w.LobbyQuaternion,w.Evidence,w.Sources,w.FloorKey,w.LobbyKey,w.LobbyRegion)};break;
            case "floor":var floor=w.Floor;floor[0]++;s.ElevatorWitnesses=new[]{new MissionElevatorWitness(w.Version,w.SiteFingerprint,w.Region,floor,w.Lobby,w.FloorQuaternion,w.LobbyQuaternion,w.Evidence,w.Sources,w.FloorKey,w.LobbyKey,w.LobbyRegion)};break;case "bad-quaternion":s.ElevatorWitnesses=new[]{new MissionElevatorWitness(w.Version,w.SiteFingerprint,w.Region,w.Floor,w.Lobby,"broken",w.LobbyQuaternion,w.Evidence,w.Sources,w.FloorKey,w.LobbyKey,w.LobbyRegion)};break;
            case "source-json":var sources=w.Sources;sources[0]="not json";s.ElevatorWitnesses=new[]{new MissionElevatorWitness(w.Version,w.SiteFingerprint,w.Region,w.Floor,w.Lobby,w.FloorQuaternion,w.LobbyQuaternion,w.Evidence,sources,w.FloorKey,w.LobbyKey,w.LobbyRegion)};break;case "marked-missing":s.ElevatorWitnesses=null!;break;
            case "adopted-missing":s.OpenFloorPhase=null;s.ElevatorWitnesses=null!;break;}
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Classify(s));
    }
    [Fact] public void CurrentMeasuredStateRequiresActualRideVerificationAndCannotUseALegacyFlag()
    {
        var s=State(false);var own=File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures/MervBound326-4e46e554.json"));using var doc=JsonDocument.Parse(own);
        byte[] bytes=doc.RootElement.GetProperty("OwnBirthBase64").GetBytesFromBase64();var log=new OwnViewLifeLog();log.Observe(false,bytes,0,WireFormat.FindCreations(bytes));
        var arrival=MervBound326Approach.Capture(s,new[]{40350.342586904524,1695,10732.675011609463},new(){sector=1118,region=36,polygon=106,floorY=1600},log.Snapshot());
        Assert.False(arrival.VerifiedMeasured);Assert.Equal(MervBound326Approach.ProofScope.Measured,MervBound326Approach.Classify(s));
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.RequireScope(s,1,arrival,log.Snapshot()));
        Assert.Throws<InvalidDataException>(()=>MervBound326Approach.Capture(State(true),arrival.Root,new(){sector=1120,region=26,floorY=2000},log.Snapshot(),true));
    }
}
