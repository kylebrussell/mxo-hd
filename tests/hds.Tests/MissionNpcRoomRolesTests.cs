using System.Text.Json;
using hds;
using E=hds.MissionInteriorEvidence;
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
        var prepared=Prepared();Assert.Equal(1,prepared.Pending.Selection.RolePlan);
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
}
