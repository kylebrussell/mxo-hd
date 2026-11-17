using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using hds;
using hds.shared;
using NativeQa;
using NativeQa.Modes;

namespace hds.Tests;

[CollectionDefinition("MissionGeneratedEscortExit",DisableParallelization=true)]
public class MissionGeneratedEscortExitCollection {}
[Collection("MissionGeneratedEscortExit")]
public class MissionGeneratedEscortExitTests : IDisposable
{
    readonly WorldServer oldWorld=Store.world;
    readonly Dictionary<string,WorldClient> oldClients=WorldServer.Clients;
    public MissionGeneratedEscortExitTests()
    {
        Store.world=(WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        WorldServer.Clients=new Dictionary<string,WorldClient>();
    }
    public void Dispose(){Store.world=oldWorld;WorldServer.Clients=oldClients;}

    static (WorldClient c,MissionProgress p,Mob follower) Captured()
    {
        string root=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(root,"data/missions/zion_1_2_10_interview_with_a_bluepill.xml")))root=Path.GetDirectoryName(root)!;
        var xml=new XmlDocument();xml.Load(Path.Combine(root,"data/missions/zion_1_2_10_interview_with_a_bluepill.xml"));
        var p=new MissionProgress(MissionXml.Parse(xml.DocumentElement!));
        p.site=new MissionSite {metrId=1,elevatorDoor=new StaticWorldObject{metrId=1,sectorID=1286,mxoStaticId=1348470568,type=BitConverter.GetBytes((ushort)419),staticId=1832,pos_x=54200,pos_y=2945,pos_z=-16700},
            lobbyDoor=new StaticWorldObject{metrId=1,sectorID=1286,mxoStaticId=1348471441,type=BitConverter.GetBytes((ushort)417),pos_x=54200,pos_y=145,pos_z=-16700}};
        p.sites.Add(p.site);p.site.shaft.Add(p.site.elevatorDoor);p.site.shaft.Add(p.site.lobbyDoor);
        p.waypoints.Add(new(){x=54200,y=2945,z=-16700});p.exits.Add(new double[]{54200,145,-16700});
        p.Complete(p.Phase.missionObjectives.Single(o=>o.goal==1));p.following.Add(1);
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new ClientData();c.playerData.missionProgress=p;
        c.playerInstance=new PlayerCharacter();c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(54200,2895,-16700));
        c.messageQueue=new MessageQueue();c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());
        var follower=new Mob();follower.setDistrict(1);follower.missionNpcId=1;follower.setXPos(54028.9609375);follower.setYPos(2895);follower.setZPos(-16596.33984375);c.playerData.missionMobs.Add(follower);
        return(c,p,follower);
    }
    [Fact] public void Actual101210FloorCannotFinishEscortBeforeCommittedStreetRide()
    {
        var(c,p,follower)=Captured();Assert.False(p.waitingForExit);Assert.False(p.leaveNotNeeded);
        Assert.True(MissionRuntime.ReadyExitEscort(c,p,54200,2895,-16700));
        Assert.True(MissionStaging.ExitEscortRide(c,p,p.site,p.site.elevatorDoor,54200,2895,-16700));
        Assert.False(p.AtExit(54200,2895,-16700));Assert.False(p.AtExit(54200,95,-16700));
        // Real escort tick at the failed pose must not publish goal completion or advance the phase.
        bool sent=(bool)typeof(MissionRuntime).GetMethod("TickEscorts",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{c,p,54200d,2895d,-16700d})!;
        Assert.False(sent);Assert.False(p.IsDone(2));Assert.Equal(0,p.phaseIndex);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        // This is the existing successful Ride order: move player, commit floor, carry followers.
        c.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(54200,95,-16700));
        p.CommitGeneratedFloor(0,p.site,p.site.lobbyDoor);MissionStaging.OnFloorReached(c,54200,95,-16700);
        Assert.Equal(95,follower.getYPos());Assert.True(p.AtExit(54200,95,-16700));
        Assert.True(MissionRuntime.ReadyExitEscort(c,p,54200,95,-16700));
        Assert.False(p.waitingForExit);Assert.False(p.leaveNotNeeded);Assert.False(p.IsDone(2));
    }
    [Theory]
    [InlineData("not-following")][InlineData("distant")][InlineData("dead")][InlineData("wrong-goal")]
    [InlineData("closed")][InlineData("owner")][InlineData("wrong-door")][InlineData("lobby")][InlineData("waiting")][InlineData("historical")]
    public void OnlyCurrentOpenExitEscortWithActualNearFollowersCanRequestDownride(string change)
    {
        var(c,p,f)=Captured();var site=p.site;var used=site.elevatorDoor;double y=2895;
        switch(change)
        {
            case "not-following":p.following.Clear();break;
            case "distant":f.setXPos(60000);break;
            case "dead":f.setIsDead(true);break;
            case "wrong-goal":p.Phase.missionObjectives.Single(o=>o.goal==2).escortTo="npc:3";break;
            case "closed":p.Complete(p.Phase.missionObjectives.Single(o=>o.goal==2));break;
            case "owner":c.playerData.missionProgress=new MissionProgress(p.mission);break;
            case "wrong-door":used=new StaticWorldObject{metrId=1,mxoStaticId=7};break;
            case "lobby":used=site.lobbyDoor;y=95;break;
            case "waiting":p.waitingForExit=true;break;
            case "historical":p.site=new MissionSite();break;
        }
        Assert.False(MissionStaging.ExitEscortRide(c,p,site,used,54200,y,-16700));
    }
    [Theory][InlineData("owner")][InlineData("phase")][InlineData("transport")]
    public void StaleRideCannotMoveOrAuthorizeEscortDeparture(string change)
    {
        var(c,p,f)=Captured();var stamp=new MissionRideStamp(c,p);
        if(change=="owner")c.playerData=new ClientData();
        if(change=="phase")p.NextPhase();
        if(change=="transport")c.TransportFence.PublishIncoming(true,()=>{});
        bool called=false;
        Assert.False(MissionTransportState.Run(c,()=>{called=true;p.CommitGeneratedFloor(0,p.site,p.site.lobbyDoor);return true;},stamp));
        Assert.False(called);Assert.False(p.HasGeneratedDeparture);Assert.Equal(2895,f.getYPos());
    }
    [Fact] public void LegacyGroundAndFarExitControlsRemain()
    {
        var(c,p,f)=Captured();Assert.False(p.AtExit(80000,2895,-16700));
        p.entered=true;Assert.True(p.AtExit(80000,2895,-16700));
        p.site.lobbyDoor=p.site.elevatorDoor;Assert.True(p.AtExit(54200,2895,-16700));
        Assert.False(MissionStaging.ExitEscortRide(c,p,p.site,p.site.elevatorDoor,54200,2895,-16700));
        p.site=null;Assert.True(p.AtExit(54200,2895,-16700));
    }
    [Fact] public void NativeModeOffersOrdinarilyWithoutSeedingSelectedProgress()
    {
        var mode=NativeQaModes.RequireKnown("mission-generated-escort-exit");Assert.IsType<MissionGeneratedEscortExitMode>(mode);
        Assert.Equal(29,MissionGeneratedEscortExitMode.EarlierMissions.Count);Assert.DoesNotContain(101210,MissionGeneratedEscortExitMode.EarlierMissions);
        string sql=string.Join("\n",mode.Plan.AfterHelpers);
        Assert.DoesNotContain("char_mission_progress",sql);Assert.DoesNotContain("101210",sql);
        Assert.Equal("101208",mode.Plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
    }
}
