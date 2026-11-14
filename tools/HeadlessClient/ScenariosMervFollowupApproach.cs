using hds;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static readonly JsonSerializerOptions MervFollowupFields=new(){IncludeFields=true};
    private static async Task<MissionProgress.SavedState> MervFollowupCheckpoint(ScenarioContext context,HeadlessPlayer p)=>
        JsonSerializer.Deserialize<MissionProgress.SavedState>(await CarriedMissionNpcFixture.ReadOwnedAsync(context,p),MervFollowupFields)!;
    private static async Task MervFollowupRide(ScenarioContext context,HeadlessPlayer p,bool enter,List<object> evidence)
    {
        var saved=await MervFollowupCheckpoint(context,p);var door=saved.Waypoints.Single();var lobby=saved.Exits.Single();
        uint key=enter?MervFollowupApproachFixture.DoorAt(new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"}.SelectMany(f=>File.ReadLines(Path.Combine(context.RepositoryRoot,"data",f))),lobby):BitConverter.ToUInt32(Convert.FromHexString(door.doorHex),0);
        var pose=enter?lobby:new[]{door.x,door.y,door.z};await MoveForMissionTest(p,pose[0],pose[1]-50,pose[2]);
        int log=context.Server.DebugLog().Length;
        string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);
        if(door.y!=lobby[1])await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"ordinary saved-door elevator ride",TimeSpan.FromSeconds(15));
        evidence.Add(new{Operation="ordinary-door",saved.Mission,saved.Phase,Enter=enter,DoorKey=key,Request=request,Waypoint=door,Lobby=lobby,RideLog=context.Server.DebugLog()[log..]});
        if(enter)
        {
            var area=Regex.Matches(context.Server.DebugLog(),@"\[MISSION\] Phase \d+ at a .* floor in .* \(([-\d.]+),([-\d.]+),([-\d.]+)\), elevator ").Last();
            double D(int n)=>double.Parse(area.Groups[n].Value,CultureInfo.InvariantCulture);
            await MoveForMissionTest(p,D(1),D(2),D(3)); // Current phase only; test movement, not native walking proof.
        }
    }
    private static async Task<ViewCreation> MervFollowupObject(ScenarioContext context,HeadlessPlayer p,int id,int cursor,List<object> evidence)
    {
        var saved=await MervFollowupCheckpoint(context,p);var owned=saved.Objects.Single(o=>o.Id==id);
        string Raw(MissionProgress.SavedObject o)=>JsonSerializer.Serialize(new{o.Id,o.Spot,o.Goid,o.OpenFloor},MervFollowupFields);
        string original=Raw(owned);double[] centre;string quaternion;string qualification;
        if(owned.OpenFloor!=null)
        {
            var w=MissionOpenFloorWitness.Read(JsonSerializer.SerializeToElement(owned.OpenFloor,MervFollowupFields));
            w.Require(saved.Mission,saved.Phase,id,owned.Spot.mxoStaticId,MissionSites.Goid(owned.Spot),owned.Goid);
            centre=w.Centre.ToArray();quaternion=w.Quaternion;
            Check.That(ContainerActions.InReach(w.Standing.SelectMany(BitConverter.GetBytes).ToArray(),centre[0],centre[1],centre[2]),"Published Standing remains in ordinary use range");
            for(int i=0;i<w.Route.Count;i+=3)await MoveForMissionTest(p,w.Route[i],w.Route[i+1],w.Route[i+2]);
            qualification="current persisted Root/Route/Standing; native collision/picking unproven";
        }
        else
        {
            Check.That(!MervFollowupApproachFixture.RequiresWitness(owned)&&MissionSites.Goid(owned.Spot)==owned.Goid,"Only genuinely bound sources may lack an adopted witness");
            SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
            var pose=MissionObjectPoses.For(new MissionObjectState{spot=owned.Spot,goid=owned.Goid});centre=new[]{pose.X,pose.Y,pose.Z};quaternion=Convert.ToHexString(pose.Orientation);
            double[]? standing=null;
            foreach(var d in new[]{(100d,0d),(-100d,0d),(0d,100d),(0d,-100d),(0d,0d)})
                if(NavMesh.Locate(owned.Spot.metrId,pose.X+d.Item1,pose.Y,pose.Z+d.Item2,out var at))
                {
                    var candidate=new[]{pose.X+d.Item1,at.floorY+95,pose.Z+d.Item2};
                    if(ContainerActions.InReach(candidate.SelectMany(BitConverter.GetBytes).ToArray(),pose.X,pose.Y,pose.Z)){standing=candidate;break;}
                }
            Check.That(standing!=null,"Bound source requires a NAV-grounded in-range approach");
            await MoveForMissionTest(p,standing![0],standing[1],standing[2]);qualification="bound source NAV/range only; no route/collision/picking proof";
        }
        bool Match(ViewCreation v)=>MervFollowupApproachFixture.Matches(v,owned.Goid,centre,quaternion);
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Skip(cursor).Any(Match),"Current saved object XYZ/quaternion view",TimeSpan.FromSeconds(12));
        var after=await MervFollowupCheckpoint(context,p);
        Check.Equal(saved.Mission,after.Mission,"Approach retains selected mission");Check.Equal(saved.Phase,after.Phase,"Approach retains current phase");
        Check.Equal(original,Raw(after.Objects.Single(o=>o.Id==id)),"Approach retains exact raw source and fixed witness DTO");
        evidence.Add(new{Operation="current-object-approach",saved.Mission,saved.Phase,Object=id,SourceAndWitness=original,Centre=centre,Quaternion=quaternion,Qualification=qualification});
        return p.World.CreationSnapshot().Skip(cursor).Last(Match);
    }
}
