using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Globalization;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static readonly IReadOnlyDictionary<string,string> ZionNextFiveEnvironment=new Dictionary<string,string>(StillNpcs){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="101208"};
    private static Task ZionCriticalNextFive(ScenarioContext context)=>RunPrimaChain(context,"ZionNextFive",1,ZionNextFiveFixture.Missions,
        ZionNextFiveFixture.Prerequisites,"zion_1_2_*.xml",requireStandardCourier:true,evidenceLabel:"zion-critical-next-five",
        evidenceOptions:new(checked((int)PlayerProgression.ExperienceForLevel(50)),ColdRestart:true,RestartEnvironment:ZionNextFiveEnvironment,ExpectedExperienceDelta:434250,ExpectedInformationDelta:455000,FreshUploadBooth:true,MissionExperience:new uint[]{68625,90750,137500,68625,68750},MissionInformation:new uint[]{91000,91000,91000,91000,91000},ExactZionNextFive:true));
    private static async Task ZionNextFiveExit(ScenarioContext context,HeadlessPlayer p,Action<object>? note=null)
    {
        await ZionNextFiveRide(context,p,false,note);
    }
    private static async Task ZionNextFiveRide(ScenarioContext context,HeadlessPlayer p,bool enter,Action<object>? note=null)
    {
        var state=await MissionCheckpoint(context,p);var door=state.Waypoints.Single();var lobby=state.Exits.Single();
        uint key=enter?ZionNextFiveFixture.DoorAt(new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),lobby):BitConverter.ToUInt32(Convert.FromHexString(door.doorHex),0);
        var pose=enter?lobby:new[]{door.x,door.y,door.z};await MoveForMissionTest(p,pose[0],pose[1]-50,pose[2]);
        int cursor=context.Server.DebugLog().Length,rpc=p.World.RpcSnapshot().Count;string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
        if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[cursor..].Contains("[MISSION] Teleported to "),"ordinary saved-site static-door ride",TimeSpan.FromSeconds(15));
        note?.Invoke(new{Kind="Ordinary saved-site door request",Mission=state.Mission,Phase=state.Phase,Enter=enter,Key=key,Request=request,RpcCursor=rpc,SourceWaypoint=door,SourceExit=lobby,RideLog=context.Server.DebugLog()[cursor..]});
    }
    private static async Task ZionNextFiveDefeat(HeadlessPlayer p,ViewCreation target)
    {
        int cursor=p.World.RpcSnapshot().Count;int npcLevel=BitConverter.ToUInt16(target.Attributes["Level"],0);
        uint expected=ZionNextFiveFixture.ExpectedKill(npcLevel);
        await DefeatMissionNpc(p,target);
        await p.WaitUntilAsync(()=>p.World.RpcSnapshot().Skip(cursor).Any(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe6&&BitConverter.ToUInt32(r.Bytes,6)==1),"actual fighting XP after normal combat",TimeSpan.FromSeconds(10));
        var gains=p.World.RpcSnapshot().Skip(cursor).Where(r=>r.Bytes.Length==10&&r.Bytes[0]==0x80&&r.Bytes[1]==0xe6&&BitConverter.ToUInt32(r.Bytes,6)==1).ToArray();
        Check.Equal(1,gains.Length,"One earned kill gain for actual target");Check.Equal(expected,BitConverter.ToUInt32(gains[0].Bytes,2),"Independent shipped experience formula for actual NPC level, current all-weakling policy");
    }
    private static async Task<ViewCreation> ZionNextFiveNpc(ScenarioContext context,HeadlessPlayer p,XElement definition,int creationStart)
    {
        var state=await MissionCheckpoint(context,p);ushort id=checked((ushort)(int)definition.Attribute("id")!);
        var mob=state.Mobs.Single(m=>m.Npc==id);
        await MoveForMissionTest(p,mob.X+100,mob.Y,mob.Z);
        ViewCreation? current=null;
        await p.WaitUntilAsync(()=>(current=ZionNextFiveFixture.CurrentNpc(p.World.CreationSnapshot(),creationStart,mob))!=null,"actual saved NPC identity/position for "+id,TimeSpan.FromSeconds(15));
        return current!;
    }
    private static async Task<ViewCreation> ZionNextFiveObject(ScenarioContext context,HeadlessPlayer p,int objectId,int creationStart,Action<object>? note=null)
    {
        var state=await MissionCheckpoint(context,p);var obj=state.Objects.Single(o=>o.Id==objectId);
        if(obj.OpenFloor==null)
        {
            Check.That(NumericalUtils.ByteArrayToUint16(obj.Spot.type,1)==obj.Goid,"Only an actual bound native object can lack a free-proxy witness");
            // Native furniture stays bound in place. This fixture projects onto actual NAV from the
            // current avatar component; it does not turn a spawner centre into a player destination.
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            var ride=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
            double[] from=Enumerable.Range(1,3).Select(i=>double.Parse(ride.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
            Check.That(NavMesh.Locate(1,from[0],from[1],from[2],out _),"Actual mission Ride root is on current floor NAV");
            var rootSector=SectorProperties.At(1,from[0],from[2]);var objectSector=SectorProperties.At(1,obj.Spot.pos_x,obj.Spot.pos_z);
            double flat=(from[0]-obj.Spot.pos_x)*(from[0]-obj.Spot.pos_x)+(from[2]-obj.Spot.pos_z)*(from[2]-obj.Spot.pos_z);
            Check.That(rootSector!=null&&objectSector!=null&&(rootSector.sector==objectSector.sector||flat<=1600d*1600d),"NAV projection must retain its root-component anchor rather than drop a distant cross-sector origin");
            Check.That(NavMesh.Project(1,obj.Spot.pos_x,obj.Spot.pos_y,obj.Spot.pos_z,out var bound,maxDistance:800,reachableFrom:from),"Bound native furniture has current-component NAV approach");
            note?.Invoke(new{Kind="Bound native furniture NAV-only approach",Mission=state.Mission,Phase=state.Phase,ObjectId=objectId,Source=obj.Spot,ActualRideRoot=from,Standing=new[]{bound.x,bound.y,bound.z},NativeCollisionClearanceProven=false});
            await MoveForMissionTest(p,bound.x,bound.y,bound.z);
            bool Bound(ViewCreation v)=>v.Goid==obj.Goid&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
                Math.Abs(BitConverter.ToDouble(pos)-obj.Spot.pos_x)<1&&Math.Abs(BitConverter.ToDouble(pos,8)-obj.Spot.pos_y)<1&&Math.Abs(BitConverter.ToDouble(pos,16)-obj.Spot.pos_z)<1;
            await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(creationStart).Any(Bound),"native furniture current bound body",TimeSpan.FromSeconds(15));
            return p.World.CreationSnapshot().Skip(creationStart).Last(Bound);
        }
        var w=MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(obj.OpenFloor,new JsonSerializerOptions{IncludeFields=true}));
        w.Require(state.Mission,state.Phase,objectId,obj.Spot.mxoStaticId,NumericalUtils.ByteArrayToUint16(obj.Spot.type,1),obj.Goid);
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
        Check.That(NavMesh.Locate(1,w.Standing[0],w.Standing[1],w.Standing[2],out var standing),"Published free-object Standing is on actual NAV");
        Check.Equal(standing.floorY+95,w.Standing[1],"Published Standing uses actual NAV height");
        for(int i=0;i<w.Route.Count;i+=3)await MoveForMissionTest(p,w.Route[i],w.Route[i+1],w.Route[i+2]);
        bool Exact(ViewCreation v)=>v.Goid==obj.Goid&&v.Attributes.TryGetValue("Position",out var pos)&&pos.Length==24&&
            Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(pos,i*8)==w.Centre[i]);
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(creationStart).Any(Exact),"current fixed object body at committed witness",TimeSpan.FromSeconds(15));
        var body=p.World.CreationSnapshot().Skip(creationStart).Last(Exact);
        Check.Equal(w.Quaternion,Convert.ToHexString(body.Attributes["Orientation"]),"Fixed DTO orientation on actual03 body");
        var after=await MissionCheckpoint(context,p);var current=after.Objects.Single(o=>o.Id==objectId);
        Check.Equal(JsonSerializer.Serialize(new{obj.Id,obj.Goid,obj.Spot,obj.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{current.Id,current.Goid,current.Spot,current.OpenFloor},new JsonSerializerOptions{IncludeFields=true}),"Whole raw source/fixed DTO unchanged through ordinary ride/route movement");
        Check.Equal(JsonSerializer.Serialize(new{state.OpenFloorSites,state.OpenFloorCurrentSiteIndex,state.Waypoints,state.Exits},new JsonSerializerOptions{IncludeFields=true}),JsonSerializer.Serialize(new{after.OpenFloorSites,after.OpenFloorCurrentSiteIndex,after.Waypoints,after.Exits},new JsonSerializerOptions{IncludeFields=true}),"Entire fixed site history/current door tuple unchanged");
        note?.Invoke(new{Kind="Immutable source/fixed DTO",Source=obj.Spot,Witness=w.RawJson,body.ViewId,body.SpawnId});return body;
    }
}
