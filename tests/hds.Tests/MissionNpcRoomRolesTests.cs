using System.Text.Json;
using hds;
using E=hds.MissionInteriorEvidence;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    private static MissionNpcRoomRoles.Candidate RoomCandidate(ushort room,int hops,int order)
    {
        var raw=Marker();raw.mxoStaticId+=(uint)order;raw.staticId+=(uint)order;
        return new MissionNpcRoomRoles.Candidate(raw,new[]{(double)order,6095,0,0},new E.NpcRoute("raw",new E.Node(room,0),Enumerable.Repeat(new E.Node(room,0),hops).ToArray(),Array.Empty<E.DoorPose>()),order);
    }
    [Fact] public void RoomCapacityKeepsAllRolesTogetherRatherThanNearestSmallRoom()
    {
        var choices=new[]{RoomCandidate(1,1,0),RoomCandidate(2,3,1),RoomCandidate(2,4,2),RoomCandidate(2,5,3),RoomCandidate(3,10,4)};
        Assert.All(MissionNpcRoomRoles.Allocate(choices,3),c=>Assert.Equal((ushort)2,c.Route.Target.Region));
        Assert.Equal(new[]{1,2,3},MissionNpcRoomRoles.Allocate(choices,3).Select(c=>c.Order));
    }
    [Fact] public void CertifiedRoomExpansionIsStableWhenNoSingleRoomHasCapacity()
    {
        var choices=new[]{RoomCandidate(2,8,3),RoomCandidate(1,2,1),RoomCandidate(2,7,2),RoomCandidate(1,1,0)};
        Assert.Equal(new[]{0,1,2},MissionNpcRoomRoles.Allocate(choices,3).Select(c=>c.Order));
    }
    [Fact] public void RoomPolicyRoundtripPreservesExplicitTagAndOldPolicyJsonShape()
    {
        var prepared=Prepared(true);Assert.Equal(1,prepared.Pending.Selection.RolePlan);
        prepared.Progress.npcInteriorPending=prepared.Pending;
        var saved=prepared.Progress.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{prepared.Site});
        var restored=MissionProgress.RestoreWithNpcCatalog(prepared.Progress.mission,saved,new[]{prepared.Site},prepared.Provider);
        Assert.Equal(1,restored.npcInteriorPending.Selection.RolePlan);
        var old=new MissionNpcInteriorSelection(201201,"old",1,2,MissionOpenFloorSite.From(prepared.Site),prepared.Pending.Selection.Actors);
        Assert.DoesNotContain("RolePlan",JsonSerializer.Serialize(old,MissionProgress.SaveJson));
        Assert.Equal(0,JsonSerializer.Deserialize<MissionNpcInteriorSelection>(JsonSerializer.Serialize(old,MissionProgress.SaveJson),MissionProgress.SaveJson)!.RolePlan);
    }
    [Theory] [InlineData("role")] [InlineData("duplicate")] [InlineData("synthetic")]
    public void RoomRoleMetadataCannotSwapRolesReuseOccupiedSpotOrInventMarker(string change)
    {
        var prepared=Prepared();var s=prepared.Pending.Selection;var a=s.Actors[0];
        var actor=new MissionNpcInteriorActor(a.Kind,change=="role"?999:a.Id,a.Index,a.WirePhase,a.Deferred,a.RawMarkerJson,change=="synthetic"?new[]{0d,6095,0,0}:a.Standing);
        var actors=change=="duplicate"?new[]{a,a}:new[]{actor};
        var changed=new MissionNpcInteriorSelection(s.Mission,s.Identity,s.Started,s.Phase,s.Site,actors,1);
        Assert.False(MissionNpcRoomRoles.ValidSelection(prepared.Progress,prepared.Site,changed));
    }
    [Theory] [InlineData(550)] [InlineData(842)] [InlineData(1150)] [InlineData(1349)]
    public void ManifestCoverageLoadsEveryDeclaredBatchScene(int sector)
    {
        string directory=Path.Combine(Repo,"data","mission_interior_evidence","1");
        Assert.True(E.Declared(directory,1,(ushort)sector));Assert.NotNull(E.LoadAudited(directory,1,(ushort)sector));
        Assert.False(E.Declared(directory,1,1));
    }
    [Fact] public void CapacityCountsNamedAndDeferredWaveRolesInAuthoredOrder()
    {
        var mission=Definition();mission.phases[2].waves.Add(new MissionWave{id=9,enemy="Machines",count=2,arrival=new MissionArrival()});
        Assert.True(MissionNpcInteriorPlanning.Roles(mission,2,out var roles,out var why),why);
        Assert.Equal(new[]{"Npc","Wave","Wave"},roles.Select(r=>r.Kind));
        Assert.Equal(new[]{6,9,9},roles.Select(r=>r.Id));Assert.Equal(new[]{0,0,1},roles.Select(r=>r.Index));
        Assert.All(MissionNpcRoomRoles.Allocate(new[]{RoomCandidate(1,1,0),RoomCandidate(2,2,1),RoomCandidate(2,3,2),RoomCandidate(2,4,3)},roles.Length),c=>Assert.Equal((ushort)2,c.Route.Target.Region));
    }
    [Fact] public void DifferentYawDoesNotMakeAnOccupiedPositionAvailableAgain()
    {
        var first=RoomCandidate(1,2,0);var rotated=new MissionNpcRoomRoles.Candidate(first.Marker,new[]{first.Standing[0],first.Standing[1],first.Standing[2],90d},first.Route,1);
        Assert.Single(MissionNpcRoomRoles.Allocate(new[]{first,rotated},2));
    }
    private sealed class SceneProvider : IMissionNpcInteriorEvidenceProvider
    {
        internal readonly Dictionary<uint,StaticWorldObject> Doors;
        internal SceneProvider(Dictionary<uint,StaticWorldObject> doors){Doors=doors;}
        public E Evidence(ushort world,ushort sector)=>throw new InvalidOperationException("allocation must use its supplied scene");
        public StaticWorldObject[] Markers(ushort world,ushort sector)=>throw new InvalidOperationException();
        public StaticWorldObject Door(ushort world,uint key)=>Doors.GetValueOrDefault(key)!;
        public MissionOpenFloorEntry.Result Entry(ushort world,G.Position door,double x,double z,string why)=>throw new InvalidOperationException("allocation must not calculate another entry");
    }
    [Fact] public void Original842SceneAllocatesThreeRolesInOneCertifiedRoom()
    {
        string directory=Path.Combine(Repo,"data","mission_interior_evidence","1");var evidence=E.LoadAudited(directory,1,842);
        using var input=File.OpenRead(Path.Combine(directory,"842.json.gz"));using var gz=new System.IO.Compression.GZipStream(input,System.IO.Compression.CompressionMode.Decompress);
        var doc=JsonSerializer.Deserialize<E.Document>(gz)!;
        StaticWorldObject From(E.Group group,E.Signature sig)=>Raw(group.Key,sig.Goid,sig.Position[0],sig.Position[1],sig.Position[2],sig.Rotation,sig.Quaternion);
        var doors=doc.Groups.ToDictionary(g=>g.Key,g=>From(g,g.Legacy.OrderBy(l=>l.Line).First().Signature));
        var markers=doc.Groups.SelectMany(g=>g.Legacy.Where(l=>l.Signature.Goid==19).Select(l=>From(g,l.Signature))).ToArray();
        var door=From(doc.Groups.Single(g=>g.Key==0x34a0028b),doc.Groups.Single(g=>g.Key==0x34a0028b).Legacy.Single(l=>l.Signature.Position[0]==10900).Signature);
        NavLocation? Locate(G.Position point)
        {
            foreach(var region in doc.Regions.Where(r=>r.Available))foreach(var polygon in region.Polygons)
            {
                double floor=-(polygon.Plane[0]*point.X+polygon.Plane[2]*point.Z+polygon.Plane[3])/polygon.Plane[1];if(Math.Abs(floor+95-point.Y)>G.Epsilon)continue;
                var edges=polygon.Edges;var cross=edges.Select((edge,i)=>{var next=edges[(i+1)%edges.Length];return(next.Vertex[0]-edge.Vertex[0])*(point.Z-edge.Vertex[2])-(next.Vertex[2]-edge.Vertex[2])*(point.X-edge.Vertex[0]);}).ToArray();
                if(cross.All(v=>v>=-G.Epsilon)||cross.All(v=>v<=G.Epsilon))return new NavLocation{sector=842,region=region.Id,polygon=(ushort)polygon.Index,floorY=floor,indoors=true};
            }
            return null;
        }
        int calculations=0;MissionOpenFloorEntry.Result Calculate(G.Position native,double cx,double cz){calculations++;return MissionOpenFloorEntry.Calculate(native,cx,cz,"original scene context",_=>true,Locate);}
        Assert.True(evidence.TryBuildingEntry(door,markers,3,Calculate,out var entry,out uint carPrefab,out var why),why);Assert.Equal((ushort)21,entry.Node.Region);Assert.Equal(1,calculations);
        Assert.True(evidence.TryLobbyEntry(doors[0x34a0009f],carPrefab,Calculate,out var lobby,out why),why);Assert.Equal((ushort)5,lobby.Node.Region);Assert.Equal(2,calculations);
        var mission=Definition();mission.npcs.Add(new MissionNpc{id=7,at=3,arrival=new MissionArrival()});mission.npcs.Add(new MissionNpc{id=8,at=3,arrival=new MissionArrival()});
        var progress=new MissionProgress(mission){persistenceId="room-test",startedAt=1};var site=new MissionSite{metrId=1,elevatorDoor=door,lobbyDoor=door};
        foreach(var marker in markers)site.npcSpots.Add(new[]{marker.pos_x,marker.pos_y-50,marker.pos_z,marker.rot});
        Assert.True(MissionNpcRoomRoles.Select(progress,2,site,markers,evidence,entry,new SceneProvider(doors),out var selected,out why),why);
        Assert.Equal(new[]{6,7,8},selected.Actors.Select(a=>a.Id));Assert.Equal(3,selected.Actors.Select(a=>string.Join("|",a.Standing.Take(3))).Distinct().Count());
        var rooms=selected.Actors.Select(a=>{var raw=MissionNpcInteriorPlanning.ReadRaw(a.RawMarkerJson);Assert.True(evidence.TryMeasuredNpcRoute(entry,raw,a.Standing,k=>doors.GetValueOrDefault(k),out var route,out var refusal),refusal);return route.Target.Region;}).Distinct();Assert.Single(rooms);
    }
    [Fact] public void RoomModeStartsFreshCriticalOfferWithOnlyEarlierReceipts()
    {
        var mode=NativeQa.NativeQaModes.RequireKnown("mission-npc-room-roles");var plan=mode.Plan;
        Assert.Equal("101213",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(32,NativeQa.Modes.MissionNpcRoomRolesMode.EarlierReceipts.Length);Assert.DoesNotContain(101213,NativeQa.Modes.MissionNpcRoomRolesMode.EarlierReceipts);
        Assert.Contains("(110,101212)",Assert.Single(plan.BeforeLoadout));Assert.DoesNotContain("(110,101213)",Assert.Single(plan.BeforeLoadout));
        Assert.Equal((17000,2300),(plan.X,plan.Z));Assert.All(plan.ReadyMessages,m=>Assert.DoesNotContain("force842 as",m));
    }
}

