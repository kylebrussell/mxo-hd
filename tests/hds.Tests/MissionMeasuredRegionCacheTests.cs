using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Xml;
using hds;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionMeasuredRegionCacheTests
{
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static readonly Lazy<MissionSite> Site=new(()=>
    {
        var rows=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)).Where(l=>l.StartsWith("1,209,")))
        {
            var f=line.Split(',');double D(int i)=>double.Parse(f[i],CultureInfo.InvariantCulture);
            rows.Add(new(){metrId=1,sectorID=209,mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(rows,1).Single(s=>s.elevatorDoor?.mxoStaticId==0x0d100020&&s.elevatorDoor.pos_x==28800&&s.elevatorDoor.pos_y==1345&&s.elevatorDoor.pos_z==14700&&s.objectSpots.Any(o=>o.mxoStaticId==0x0d100166)&&MissionOpenFloorSite.CatalogFingerprint(s)=="CBA90BD052592F00D5F89B23ACBAF3C5D13A5315357586285D0D2010F5B9BFD9");
    });
    static MissionElevatorWitness Witness()
    {
        SectorProperties.Load(Path.Combine(Repo,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));
        var raw=Site.Value.objectSpots.Single(o=>o.mxoStaticId==0x0d100166);
        var choice=new MissionOpenFloorChoice(1,3,JsonSerializer.Serialize(raw,MissionProgress.SaveJson));
        Assert.True(MissionElevatorPoses.TryCreate(Site.Value,new[]{choice},out var witness,out string why),why);return Assert.IsType<MissionElevatorWitness>(witness);
    }
    static MissionElevatorWitness Copy(MissionElevatorWitness w,string change="")
    {
        var floor=w.Floor;var lobby=w.Lobby;var sources=w.Sources;
        if(change=="floor")floor[0]+=1;if(change=="lobby")lobby[0]+=1;if(change=="sources")sources[0]+=" ";
        return new(w.Version+(change=="version"?1:0),w.SiteFingerprint+(change=="site"?"x":""),(ushort)(w.Region+(change=="region"?1:0)),floor,lobby,
            w.FloorQuaternion+(change=="floor-q"?"00":""),w.LobbyQuaternion+(change=="lobby-q"?"00":""),w.Evidence+(change=="evidence"?"x":""),sources,
            w.FloorKey+(change=="floor-key"?1u:0),w.LobbyKey+(change=="lobby-key"?1u:0),(ushort)(w.LobbyRegion+(change=="lobby-region"?1:0)));
    }
    static G.ReachableRegion Region(MissionElevatorWitness? w)
    {Assert.True(MissionInteriorSites.TryRegion(Site.Value,out var region,out string why,w),why);return region;}
    [Fact] public void CompleteEquivalentWitnessReusesOnlyTheMeasuredSlot()
    {
        var witness=Witness();var legacy=Region(null);var measured=Region(witness);
        Assert.NotSame(legacy,measured);Assert.Same(measured,Region(Copy(witness)));Assert.Same(legacy,Region(null));Assert.Same(measured,Region(witness));
    }
    [Theory][InlineData("version")][InlineData("site")][InlineData("region")][InlineData("lobby-region")][InlineData("floor")][InlineData("lobby")]
    [InlineData("floor-q")][InlineData("lobby-q")][InlineData("floor-key")][InlineData("lobby-key")][InlineData("evidence")][InlineData("sources")]
    public void EveryWitnessPropertyChangesThePureRegionKey(string change)
    {
        var witness=Witness();var original=Region(witness);var changed=Copy(witness,change);
        Assert.True(MissionInteriorSites.TryRegion(Site.Value,out var different,out string why,changed),why);
        Assert.NotSame(original,different);Assert.Same(different,Region(changed));
        // Pure geometry caching grants no authority to an altered witness.
        var raw=Site.Value.objectSpots.Single(o=>o.mxoStaticId==0x0d100166);var choices=new[]{new MissionOpenFloorChoice(1,3,JsonSerializer.Serialize(raw,MissionProgress.SaveJson))};
        Assert.False(MissionElevatorPoses.Validate(Site.Value,choices,changed,out _));
    }
    [Fact] public void CurrentSiteFingerprintAndNavRevisionInvalidateMeasuredResults()
    {
        var witness=Witness();var a=Region(witness);double before=Site.Value.maxX;
        try{Site.Value.maxX++;Assert.NotSame(a,Region(witness));}finally{Site.Value.maxX=before;}
        var b=Region(witness);NavMesh.UseDirectory(Path.Combine(Repo,"data/navmesh"));Assert.NotSame(b,Region(witness));
    }
    [Fact] public void DataRootRefusalIsIsolatedAndCannotPoisonTheRestoredRoot()
    {
        var witness=Witness();var good=Region(witness);string cwd=Directory.GetCurrentDirectory();string temp=Path.Combine(Path.GetTempPath(),"mxo-region-cache-root-"+Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(temp,"data/mission_open_floor/1"));File.WriteAllBytes(Path.Combine(temp,"data/mission_open_floor/1/209.json.gz"),new byte[]{1,2});Directory.SetCurrentDirectory(temp);
            Assert.False(MissionInteriorSites.TryRegion(Site.Value,out _,out string first,witness));Assert.NotNull(first);
            Assert.False(MissionInteriorSites.TryRegion(Site.Value,out _,out string again,Copy(witness)));Assert.Equal(first,again);
        }
        finally
        {
            Directory.SetCurrentDirectory(cwd);
            string full=Path.GetFullPath(temp);if(!full.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Unsafe test cleanup");Directory.Delete(full,true);
        }
        Assert.NotSame(good,Region(witness));
    }
    sealed record Outcome(bool? Success,string? Refusal,string? Exception);
    static Outcome Observe(MissionElevatorWitness witness,bool directBuild)
    {
        try
        {
            if(!directBuild){bool success=MissionInteriorSites.TryRegion(Site.Value,out _,out string why,witness);return new(success,why,null);}
            var method=typeof(MissionInteriorSites).GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Static)!;object?[] args={Site.Value,null,null,witness};bool result=(bool)method.Invoke(null,args)!;return new(result,(string?)args[2],null);
        }
        catch(Exception ex){return new(null,null,(ex is TargetInvocationException?ex.InnerException!:ex).GetType().FullName);}
    }
    [Theory][InlineData(double.NaN)][InlineData(double.PositiveInfinity)][InlineData(double.NegativeInfinity)]
    public void NonfiniteWitnessKeepsTheOriginalBuilderBehavior(double coordinate)
    {
        var w=Witness();_=Region(w);var floor=w.Floor;floor[0]=coordinate;
        var invalid=new MissionElevatorWitness(w.Version,w.SiteFingerprint,w.Region,floor,w.Lobby,w.FloorQuaternion,w.LobbyQuaternion,w.Evidence,w.Sources,w.FloorKey,w.LobbyKey,w.LobbyRegion);
        Assert.Equal(Observe(invalid,true),Observe(invalid,false));Assert.Equal(Observe(invalid,true),Observe(invalid,false));Assert.Same(Region(w),Region(Copy(w)));
    }
    [Fact] public void WarmPureRegionCannotAuthorizeAnInvalidAdmissionWitness()
    {
        var witness=Witness();var invalid=Copy(witness,"evidence");_=Region(invalid);
        var xml=new XmlDocument();xml.LoadXml("<mission id='9803' title='Cache authority' description='D'><phase site='Normal'><object id='1' kind='Computer'/><objective goal='1' type='Use' object='1' text='Use'/></phase></mission>");var mission=MissionXml.Parse(xml.DocumentElement!);
        var source=Site.Value.objectSpots.Single(o=>o.mxoStaticId==0x0d100166);var choices=new[]{new MissionOpenFloorChoice(1,3,JsonSerializer.Serialize(source,MissionProgress.SaveJson))};
        var pending=new MissionOpenFloorPendingAdmission(1,mission.id,new string('a',32),0,100,MissionOpenFloorSite.From(Site.Value),choices,null,invalid);string before=JsonSerializer.Serialize(pending,MissionProgress.SaveJson);
        Assert.False(MissionOpenFloorAdmission.TryGeometry(mission,0,Site.Value,choices,out var poses,out string why,pending));Assert.Empty(poses);Assert.Equal("ChangedMissionElevatorWitness",why);
        Assert.Equal(before,JsonSerializer.Serialize(pending,MissionProgress.SaveJson));
    }
}
