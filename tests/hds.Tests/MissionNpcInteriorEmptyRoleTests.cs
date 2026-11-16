using System.Globalization;
using System.Text.Json;
using System.Xml;
using hds;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    private static Mission HardTimeZeroRoles()
    {var doc=new XmlDocument();doc.Load(Path.Combine(Repo,"data","missions","machines_1_2_3_hard_time.xml"));return MissionXml.Parse(doc.DocumentElement!);}
    // Counterfactual historical authoring ONLY: preserve the generic empty-building replay contract.
    // Current shipped Hard Time is no-site; its changed-authored pending is not claimed to recover.
    private static Mission LegacyHardTimeEmptyBuilding()
    {var m=HardTimeZeroRoles();m.phases[1].site="Normal";m.phases[3].site="Normal";return m;}
    [Fact] public void ActualHardTimeUploadPhasesHaveNoNpcPlacementButMalformedOrPositiveWorkStaysGated()
    {
        var m=HardTimeZeroRoles();Assert.False(MissionNpcInteriorAdmission.Candidate(m,0));Assert.False(MissionNpcInteriorAdmission.EmptyRoles(m,0));Assert.True(MissionNpcInteriorAdmission.Candidate(m,2));
        foreach(int index in new[]{1,3}){Assert.Null(m.phases[index].site);Assert.True(HeadlessClient.MachinesNoSiteUploadFixture.IsNoSiteUpload(m,index));Assert.False(MissionNpcInteriorAdmission.EmptyRoles(m,index));Assert.False(MissionNpcInteriorAdmission.Candidate(m,index));Assert.All(m.phases[index].missionObjectives,g=>Assert.Equal(MissionGoalType.Upload,g.type));}
        m=LegacyHardTimeEmptyBuilding();Assert.True(MissionNpcInteriorAdmission.EmptyRoles(m,1));
        m.phases[1].enemies=-1;Assert.False(MissionNpcInteriorAdmission.EmptyRoles(m,1));Assert.True(MissionNpcInteriorAdmission.Candidate(m,1));m.phases[1].enemies=0;
        m.phases[1].waves.Add(new MissionWave{count=-1});Assert.True(MissionNpcInteriorAdmission.Candidate(m,1));m.phases[1].waves.Clear();
        m.phases[1].waves.Add(new MissionWave{count=1,enemy="Zion"});Assert.True(MissionNpcInteriorAdmission.Candidate(m,1));m.phases[1].waves.Clear();
        m.npcs.Add(new MissionNpc{id=99,at=2,arrival=new MissionArrival{at="object:999"}});Assert.True(MissionNpcInteriorAdmission.Candidate(m,1));Assert.False(MissionNpcInteriorPlanning.Roles(m,1,out _,out _));
    }
    [Fact] public void FreshEmptyUploadSkipsNpcChooserAndLeavesPreviewAndProgressUntouched()
    {
        var p=new MissionProgress(HardTimeZeroRoles());var c=Client(p);var preview=Site();c.playerData.missionPreview=preview;
        Assert.True(MissionNpcInteriorAdmission.TryPrepare(c,p,1,out var plan,out var why),why);Assert.Null(plan);Assert.Null(p.npcInteriorPending);Assert.Null(p.site);Assert.Same(preview,c.playerData.missionPreview);Assert.Equal(0,p.phaseIndex);
    }
    private static readonly Lazy<List<MissionSite>> EmptyRoleSites=new(()=>
    {
        var objects=new List<StaticWorldObject>();
        foreach(string file in new[]{"staticObjects_slums_1.csv","staticObjects_slums_2.csv"})foreach(string line in File.ReadLines(Path.Combine(Repo,"data",file)))
        {
            if(!line.StartsWith("1,209,")&&!line.StartsWith("1,550,"))continue;var f=line.Split(',');double D(int n)=>double.Parse(f[n],CultureInfo.InvariantCulture);
            objects.Add(new(){metrId=ushort.Parse(f[0]),sectorID=ushort.Parse(f[1]),mxoStaticId=BitConverter.ToUInt32(Convert.FromHexString(f[2])),staticId=BitConverter.ToUInt32(Convert.FromHexString(f[3])),
                type=Convert.FromHexString(f[4][..4]),exterior=bool.Parse(f[5]),pos_x=D(6),pos_y=D(7),pos_z=D(8),rot=D(9),quat=f[10]});
        }
        return MissionSites.Build(objects,1);
    });
    private static (MissionProgress P,WorldClient C,MissionSite Selected,Provider Evidence) ZeroPending()
    {
        var pending=JsonSerializer.Deserialize<MissionNpcInteriorPending>("{\"Version\":2,\"Kind\":\"NpcInterior\",\"Selection\":{\"Mission\":201203,\"Identity\":\"4a0a82cdabf54d07b3a9a333e9a78018\",\"Started\":1794782008,\"Phase\":1,\"Site\":{\"District\":1,\"DoorKey\":576727048,\"DoorX\":40300,\"DoorY\":8145,\"DoorZ\":25800,\"Fingerprint\":\"064CB29257ADF5C0E957625E90C5644C3360C093350AF70BFCAE25DA82C2A83F\"},\"Actors\":[]},\"Witness\":null,\"Refusal\":\"MissingNpcSiteRole\"}",MissionProgress.SaveJson)!;
        var m=LegacyHardTimeEmptyBuilding();var p=new MissionProgress(m){phaseIndex=0,persistenceId=pending.Selection.Identity,startedAt=pending.Selection.Started,waitingForExit=true};
        foreach(var goal in m.phases[0].missionObjectives)p.Complete(goal);p.itemSlots[6796]=40;p.itemSlots[6795]=41;
        var selected=pending.Selection.Site.Resolve(EmptyRoleSites.Value);Assert.NotNull(selected);Assert.Equal(0x22602808u,selected.elevatorDoor.mxoStaticId);
        var prior=Assert.Single(EmptyRoleSites.Value.Where(s=>s.elevatorDoor?.mxoStaticId==0x0d100020&&s.elevatorDoor.pos_y==1345&&s.containers.Concat(s.realObjects).Any(o=>o.mxoStaticId==0x0d1001a2)));
        p.site=prior;p.sites.Add(prior);var raw=prior.containers.Concat(prior.realObjects).Single(o=>o.mxoStaticId==0x0d1001a2);
        p.objects[1]=new MissionObjectState{def=m.phases[0].objects.Single(),spot=raw,goid=326};p.npcInteriorPending=pending;
        return(p,Client(p),selected,new Provider());
    }
    [Fact] public void CurrentNoSiteAuthoringDoesNotRecoverHistoricalBuildingPending()
    {
        var f=ZeroPending();f.P.mission.phases[1].site=null;
        Assert.False(MissionNpcInteriorAdmission.Replay(f.C,f.P,1,EmptyRoleSites.Value,f.Evidence,out _,out _));
        Assert.NotNull(f.P.npcInteriorPending);Assert.Equal(0,f.Evidence.Reads);
    }
    [Fact] public void ExactOldEmptyPendingContinuesSameSiteWithoutChooserWitnessOrRandomAndRollbackRetainsIt()
    {
        var f=ZeroPending();var p=f.P;var catalog=EmptyRoleSites.Value;string before=JsonSerializer.Serialize(p.CaptureWithNpcCatalog(48,Array.Empty<Mob>(),catalog),MissionProgress.SaveJson);
        Assert.True(MissionNpcInteriorAdmission.Replay(f.C,p,1,catalog,f.Evidence,out var plan,out string why),why);
        Assert.True(plan.EmptyRoleReplay);Assert.False(plan.Adopted);Assert.Null(plan.Pending.Witness);Assert.Same(p.npcInteriorPending,plan.Pending);Assert.Same(f.Selected,plan.Selected.Site);Assert.Null(plan.Selected.Random);Assert.Equal(0,f.Evidence.Reads);
        Assert.Equal(before,JsonSerializer.Serialize(p.CaptureWithNpcCatalog(48,Array.Empty<Mob>(),catalog),MissionProgress.SaveJson));
        p.NextPhase();plan.RequireStaging(f.C);var rng=new CountingRandom(724);Assert.Empty(MissionStaging.StageSourceChoices(p.Phase,f.Selected,rng,plan,0));Assert.Equal(0,rng.Draws);
        p.site=f.Selected;p.sites.Add(f.Selected);p.npcInteriorPending=null;plan.RequirePublished(f.C);Assert.Empty(p.npcInteriorWitnesses);
        // Actual capture/restore body used on SQL rollback, not a claimed live SQL failure.
        var restored=MissionProgress.RestoreWithNpcCatalog(p.mission,JsonSerializer.Deserialize<MissionProgress.SavedState>(before,MissionProgress.SaveJson)!,catalog,f.Evidence);
        Assert.Equal(before,JsonSerializer.Serialize(restored.CaptureWithNpcCatalog(48,Array.Empty<Mob>(),catalog),MissionProgress.SaveJson));Assert.Equal((ushort)40,restored.itemSlots[6796]);Assert.Equal((ushort)41,restored.itemSlots[6795]);Assert.NotNull(restored.npcInteriorPending);Assert.Null(restored.npcInteriorPending.Witness);
    }
    [Theory][InlineData("owner")][InlineData("reset")][InlineData("phase")][InlineData("changedRoles")][InlineData("changedSite")]
    public void EmptyPendingCannotStageAfterOwnerWorldOrAuthoredSelectionChanges(string change)
    {
        var f=ZeroPending();Assert.True(MissionNpcInteriorAdmission.Replay(f.C,f.P,1,EmptyRoleSites.Value,f.Evidence,out var plan,out _));f.P.NextPhase();plan.RequireStaging(f.C);
        if(change=="owner")f.C.playerData=new ClientData();else if(change=="reset")f.C.TransportFence.PublishIncoming(true,()=>{});else if(change=="phase")f.P.phaseIndex=0;
        else if(change=="changedRoles")f.P.Phase.enemies=-1;else f.Selected.minX--;
        try{Assert.Throws<MissionCheckpointException>(()=>plan.RequireStaging(f.C));}finally{if(change=="changedSite")f.Selected.minX++;}
    }
    [Theory][InlineData("refusal")][InlineData("mission")][InlineData("kind")][InlineData("version")][InlineData("negativeWave")]
    public void OnlyExactHistoricalMissingRoleMetadataCanUseEmptyContinuation(string change)
    {
        var f=ZeroPending();var old=f.P.npcInteriorPending;var selection=old.Selection;
        if(change=="mission")selection=new MissionNpcInteriorSelection(999,selection.Identity,selection.Started,selection.Phase,selection.Site,selection.Actors);
        if(change=="negativeWave")f.P.mission.phases[1].waves.Add(new MissionWave{count=-1});
        f.P.npcInteriorPending=new MissionNpcInteriorPending(change=="version"?1:2,change=="kind"?"other":old.Kind,selection,null!,change=="refusal"?"OtherRefusal":old.Refusal);
        Assert.False(MissionNpcInteriorAdmission.Replay(f.C,f.P,1,EmptyRoleSites.Value,f.Evidence,out _,out _));Assert.NotNull(f.P.npcInteriorPending);Assert.Equal(0,f.Evidence.Reads);
    }
}
