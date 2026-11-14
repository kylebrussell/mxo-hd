using System.Text.Json;
using System.Xml;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
namespace hds.Tests;
public class MissionDepartureTests
{
    static Mission Definition()
    {
        var d=new XmlDocument();d.LoadXml("<mission id='901' title='Exit'><phase><objective goal='1' type='Use' object='1' text='Open'/></phase><phase><objective goal='2' type='Use' object='1' text='Open'/></phase></mission>");
        return MissionXml.Parse(d.DocumentElement!);
    }
    static StaticWorldObject Door(uint id,double y,double x=1000,double z=0)=>new(){metrId=1,sectorID=405,mxoStaticId=id,pos_x=x,pos_y=y,pos_z=z};
    static MissionProgress Progress()
    {
        var p=new MissionProgress(Definition());p.site=new MissionSite{elevatorDoor=Door(1,2545),lobbyDoor=Door(2,145)};
        p.site.shaft.AddRange(new[]{p.site.lobbyDoor,p.site.elevatorDoor});p.sites.Add(p.site);
        p.waypoints.Add(new(){x=1000,y=2545,z=0});p.exits.Add(new double[]{1000,145,0});
        p.Complete(p.Phase.missionObjectives[0]);return p;
    }
    static MissionProgress.SavedState Saved(MissionProgress p)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(JsonSerializer.Serialize(p.Capture(1,Array.Empty<Mob>()),MissionProgress.SaveJson),MissionProgress.SaveJson)!;
    static MissionProgress Restore(MissionProgress p,MissionProgress.SavedState state)=>MissionProgress.RestoreFromCatalog(p.mission,state,new[]{p.site});
    [Theory]
    [InlineData("phase")][InlineData("site")][InlineData("mission")][InlineData("world")][InlineData("life")]
    [InlineData("dead")][InlineData("shutdown")][InlineData("transport")]
    public void DelayedRideStampRejectsStaleOwnershipAndLifetime(string change)
    {
        var p=Progress();var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        c.playerData=new ClientData();c.playerData.missionProgress=p;c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        var stamp=new MissionRideStamp(c,p);Assert.True(stamp.Current);
        switch(change)
        {
            case "phase":p.NextPhase();break;case "site":p.site=new MissionSite();break;
            case "mission":c.playerData.missionProgress=Progress();break;
            case "world":c.playerData.worldGeneration++;break;case "life":c.playerData.lifeGeneration++;break;
            case "dead":c.playerData.isDead=true;break;case "shutdown":c.playerData.waitForRPCShutDown=true;break;
            case "transport":c.TransportFence.PublishIncoming(true,()=>{});break;
        }
        Assert.False(stamp.Current);Assert.False(p.HasGeneratedDeparture);
    }
    [Fact] public void StationaryInteriorDoorDoesNotLeaveAndEscortPredicateRemainsUnchanged()
    {
        var p=Progress();Assert.True(p.waitingForExit);Assert.True(MissionRuntime.NeedsToLeave(p,p.Phase.missionObjectives[0],1200,2495,100));
        for(int tick=0;tick<10;tick++)Assert.False(p.IsExiting(1200,2495,100));
        Assert.True(p.AtExit(1200,2495,100)); // authored Escort-to-exit behavior is deliberately retained
        Assert.False(p.IsExiting(1200,145,100)); // a pose alone does not manufacture transport evidence
        p.CommitGeneratedFloor(0,p.site,p.site.lobbyDoor);Assert.True(p.IsExiting(1200,145,100));
    }
    [Fact] public void WrongDestinationHistoricalSiteAndOldPhaseCannotAuthorizeDeparture()
    {
        var p=Progress();var site=p.site;
        p.CommitGeneratedFloor(1,site,site.lobbyDoor);p.CommitGeneratedFloor(0,new MissionSite(),site.lobbyDoor);
        p.CommitGeneratedFloor(0,site,Door(9,145));Assert.False(p.HasGeneratedDeparture);
        p.CommitGeneratedFloor(0,site,site.lobbyDoor);Assert.True(p.HasGeneratedDeparture);
        p.NextPhase();p.site=site;p.waypoints.Add(new(){x=1000,y=2545,z=0});p.exits.Add(new double[]{1000,145,0});p.waitingForExit=true;p.CommitGeneratedFloor(0,site,site.lobbyDoor);
        Assert.False(p.HasGeneratedDeparture);Assert.False(p.IsExiting(1000,2495,0));
    }
    [Fact] public void ReturningToMissionFloorClearsEarlierDeparture()
    {
        var p=Progress();p.CommitGeneratedFloor(0,p.site,p.site.lobbyDoor);p.CommitGeneratedFloor(0,p.site,p.site.elevatorDoor);
        Assert.False(p.HasGeneratedDeparture);Assert.False(p.IsExiting(1000,2495,0));
    }
    [Theory][InlineData(false)][InlineData(true)] public void ColdRestoreRetainsExactEvidenceAndLegacyDefaultsToAbsent(bool openFloor)
    {
        var p=Progress();p.openFloorCheckpoint=openFloor;Assert.False(Restore(p,Saved(p)).IsExiting(1000,2495,0));
        p.CommitGeneratedFloor(0,p.site,p.site.lobbyDoor);var saved=Saved(p);Assert.True(Restore(p,saved).HasGeneratedDeparture);
        saved.DepartedPhase=1;Assert.False(Restore(p,saved).HasGeneratedDeparture);
        saved.DepartedPhase=0;saved.DepartedSite=Door(1,2545,x:1001);Assert.False(Restore(p,saved).HasGeneratedDeparture);
        saved.DepartedPhase=null;saved.DepartedSite=null;Assert.False(Restore(p,saved).HasGeneratedDeparture);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void TransportRollbackRestoresPositionPanelAndTeleportIdentity(bool commit)
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerInstance=new PlayerCharacter();
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        byte[] original=NumericalUtils.doublesToLtVector3d(10,2495,20);c.playerInstance.Position.setValue(original);
        var panel=Door(5,2545);c.playerData.elevatorPanel=panel;c.playerData.teleportCounter=7;
        using(var transport=new MissionTransportState(c))
        {
            c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(1000,95,0));
            c.playerData.elevatorPanel=Door(6,145);c.playerData.teleportCounter++;
            if(commit)transport.Commit();
        }
        Assert.Equal(commit?8:7,c.playerData.teleportCounter);
        if(commit){Assert.NotSame(panel,c.playerData.elevatorPanel);Assert.NotEqual(original,c.playerInstance.Position.getValue());}
        else{Assert.Same(panel,c.playerData.elevatorPanel);Assert.Equal(original,c.playerInstance.Position.getValue());}
    }
    [Fact] public void RestoringCommittedSnapshotRollsBackUncommittedDeparture()
    {
        var p=Progress();var before=Saved(p);p.CommitGeneratedFloor(0,p.site,p.site.lobbyDoor);
        Assert.True(p.HasGeneratedDeparture);var rolledBack=Restore(p,before);Assert.False(rolledBack.HasGeneratedDeparture);
        rolledBack.CommitGeneratedFloor(0,rolledBack.site,rolledBack.site.lobbyDoor);var retry=Saved(rolledBack);
        Assert.True(Restore(rolledBack,retry).HasGeneratedDeparture);
    }
    [Fact] public void GeneratedSitePreservesExplicitLeaveUploadAndEscortOverrides()
    {
        var p=Progress();var upload=new MissionObjective{type=MissionGoalType.Upload};
        Assert.False(MissionRuntime.NeedsToLeave(p,upload,1000,2495,0));
        Assert.False(MissionRuntime.NeedsToLeave(p,new MissionObjective{type=MissionGoalType.Escort,escortTo="exit"},1000,2495,0));
        Assert.False(MissionRuntime.NeedsToLeave(p,new MissionObjective{type=MissionGoalType.Escort,escortTo="hardline"},1000,2495,0));
        p.Phase.leave=true;Assert.True(MissionRuntime.NeedsToLeave(p,upload,1000,2495,0));
        p.Phase.leave=false;Assert.False(MissionRuntime.NeedsToLeave(p,p.Phase.missionObjectives[0],1000,2495,0));
    }
    [Fact] public void GroundFloorAndAuthoredSitesRetainLegacyExitAndFarAndImmediatePathsRemain()
    {
        var p=Progress();p.site.lobbyDoor=p.site.elevatorDoor;Assert.False(p.UsesGeneratedDeparture);Assert.True(p.IsExiting(1000,2495,0));
        p.site=null;Assert.True(p.IsExiting(1000,2495,0));
        p=Progress();Assert.True(p.IsExiting(10000,2495,0));p.leaveNotNeeded=true;Assert.True(p.IsExiting(1000,2495,0));
        p.leaveNotNeeded=false;p.waypoints.Clear();p.exits.Clear();Assert.True(p.IsExiting(0,0,0));
    }
    [Fact] public void ShippedPanelPairMustMatchCurrentShaftAndExactFloorDelta()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"data","elevator_panels.csv")))root=Path.GetDirectoryName(root)!;
        Elevators.Load(Path.Combine(root,"data","elevator_panels.csv"));
        var p=Progress();p.site.elevatorDoor=Door(0x1950031f,945,60100,33400);p.site.lobbyDoor=Door(0x19500379,145,60100,33400);
        var inside=Elevators.PanelForDoor(p.site.elevatorDoor)!;var lobby=Elevators.PanelForDoor(p.site.lobbyDoor)!;
        Assert.Equal(2,inside.floorFromBottom);Assert.Equal(0,lobby.floorFromBottom);Assert.Equal(inside.shaft,lobby.shaft);
        p.CommitGeneratedPanelFloor(inside,inside);Assert.False(p.HasGeneratedDeparture);
        var wrong=new Elevators.PanelFloor{metrId=inside.metrId,sector=inside.sector,shaft=(ushort)(inside.shaft+1),floorFromBottom=0};
        p.CommitGeneratedPanelFloor(inside,wrong);Assert.False(p.HasGeneratedDeparture);
        p.CommitGeneratedPanelFloor(inside,Elevators.FloorOf(inside,2));Assert.False(p.HasGeneratedDeparture);
        p.CommitGeneratedPanelFloor(inside,lobby);Assert.True(p.HasGeneratedDeparture);
        p.CommitGeneratedPanelFloor(lobby,inside);Assert.False(p.HasGeneratedDeparture);
        p.site.lobbyDoor.pos_y++;p.CommitGeneratedPanelFloor(inside,lobby);Assert.False(p.HasGeneratedDeparture);
    }
}
