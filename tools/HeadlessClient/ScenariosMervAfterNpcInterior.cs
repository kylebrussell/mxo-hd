using System.Globalization;
using System.Text.RegularExpressions;
using hds;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static async Task MervAfterNpcRide(ScenarioContext context,HeadlessPlayer p,bool enter,MissionProgress.SavedState saved,MissionNpcInteriorProof.Plan plan,List<object> evidence,int mission,int phase)
    {
        Check.Equal(mission,saved.Mission,"NPC ride expected mission");Check.Equal(phase,saved.Phase,"NPC ride expected current phase");
        var requested=enter?plan.Lobby:plan.Floor;var destination=enter?plan.Floor:plan.Lobby;
        await MoveForMissionTest(p,requested.Standing![0],requested.Standing[1],requested.Standing[2]);
        Check.That(MervFollowupElevatorFixture.SameEntry(saved,await MervFollowupCurrent(context,p)),"NPC ride retains exact owner/site/current or historical witness after approach");
        var life=p.World.CurrentOwnViewLife();int sections=p.World.ViewSnapshot().Count,log=context.Server.DebugLog().Length,cursor=p.World.RpcSnapshot().Count;
        string request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(requested.Key))} 03 00";p.SendRpc(request);
        NpcInteriorComputerState.DoorObservation? door=null;NpcInteriorComputerState.Teleport? teleport=null;
        await p.WaitUntilAsync(()=>
        {
            try{var current=p.World.CurrentOwnViewLife();door=NpcInteriorComputerState.CurrentDoor(current,requested);teleport=NpcInteriorComputerState.FreshTeleport(current,life,sections,destination.Standing!);return true;}
            catch(InvalidDataException){return false;}
        },"Normal saved NPC door Open0834 plus fresh same-target own teleport",TimeSpan.FromSeconds(15));
        await p.WaitUntilAsync(()=>context.Server.DebugLog()[log..].Contains("[MISSION] Teleported to "),"Ordinary NPC ride commit",TimeSpan.FromSeconds(15));
        var match=Regex.Matches(context.Server.DebugLog()[log..],@"\[MISSION\] Teleported to ([-\d.]+),([-\d.]+),([-\d.]+)").Last();
        var actual=Enumerable.Range(1,3).Select(i=>double.Parse(match.Groups[i].Value,CultureInfo.InvariantCulture)).ToArray();
        Check.That(actual.SequenceEqual(destination.Standing!),"Ordinary NPC ride uses the exact saved opposite Standing");
        SectorProperties.Load(Path.Combine(context.RepositoryRoot,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot,"data","navmesh"));
        Check.That(NavMesh.Locate(1,actual[0],actual[1],actual[2],out var nav),"NPC measured ride has actual NAV");
        var node=plan.Witness.GetProperty(enter?"Floor":"Lobby").GetProperty("Node");
        Check.Equal((ushort)(destination.Key>>20),nav.sector,"NPC ride source sector");Check.Equal(node.GetProperty("Region").GetUInt16(),nav.region,"NPC ride exact selected original region");
        Check.Equal(node.GetProperty("Polygon").GetUInt16(),nav.polygon,"NPC ride exact saved polygon");
        evidence.Add(new{Operation="ordinary-npc-witness-ride",saved.Mission,saved.Phase,Enter=enter,Plan=plan,Request=request,RpcCursor=cursor,Door=door,Preexisting=door!.Capability.Birth<sections,FreshTeleport=teleport,ActualRoot=actual,NativeHoverProven=false});
    }
    private static async Task MervAfterNpcRoute(ScenarioContext context,HeadlessPlayer p,MissionProgress.SavedState saved,ushort npc,List<object> evidence)
    {
        var plan=MissionNpcInteriorRide.ForSite(saved);
        if(plan==null||plan.Witness.GetProperty("Selection").GetProperty("Phase").GetInt32()!=saved.Phase)return;
        var actors=plan.Witness.GetProperty("Selection").GetProperty("Actors").EnumerateArray().ToArray();
        int index=Array.FindIndex(actors,a=>a.GetProperty("Kind").GetString()=="Npc"&&a.GetProperty("Id").GetInt32()==npc);
        // Generated wave bodies have their own role identity; named NPC routes never borrow another actor.
        if(index<0)return;
        var route=MissionNpcInteriorProof.Route(context.RepositoryRoot,plan,index);var doors=MissionNpcInteriorProof.RequiredDoors(plan);var opened=new HashSet<uint>();
        foreach(var step in route.Skip(1))
        {
            var current=await MervFollowupCurrent(context,p);Check.That(current!=null&&MervFollowupElevatorFixture.SameEntry(saved,current),"Actor approach keeps exact owner/phase/site/witness");
            foreach(uint key in step.Doors)
            {
                if(!opened.Add(key))continue;var required=doors.Single(d=>d.Key==key);int cursor=p.World.RpcSnapshot().Count;
                NpcInteriorComputerState.DoorObservation? observed=MervFollowupElevatorFixture.CurrentOpenPortal(p.World.CurrentOwnViewLife(),required);
                bool alreadyOpen=observed!=null;string? request=null;
                if(!alreadyOpen){request=$"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(key))} 03 00";p.SendRpc(request);}
                await p.WaitUntilAsync(()=>{try{observed=NpcInteriorComputerState.CurrentDoor(p.World.CurrentOwnViewLife(),required);return true;}catch(InvalidDataException){return false;}},"Ordinary required own-GO portal Open0834",TimeSpan.FromSeconds(15));
                evidence.Add(new{Operation="ordinary-npc-portal-open",Actor=npc,Door=required,Request=request,AlreadyOpen=alreadyOpen,RpcCursor=cursor,Current=observed,NativePassageProven=false});
            }
            await MoveForMissionTest(p,step.Standing[0],step.Standing[1],step.Standing[2]);
        }
        evidence.Add(new{Operation="current-npc-authored-route",Actor=npc,ActorIndex=index,Plan=plan,Route=route,NativeWalkingProven=false});
    }
}
