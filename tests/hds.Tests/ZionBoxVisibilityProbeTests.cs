using System.Reflection;
using System.Runtime.CompilerServices;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class ZionBoxVisibilityProbeTests:IDisposable
{
    readonly WorldClient oldClient=Store.currentClient;readonly ServerConfig oldConfig=Store.config;
    readonly string? oldDev=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),oldMode=Environment.GetEnvironmentVariable(ZionBoxVisibilityProbe.EnvironmentKey);
    public ZionBoxVisibilityProbeTests()
    {
        Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","1");Environment.SetEnvironmentVariable(ZionBoxVisibilityProbe.EnvironmentKey,"1");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_zion_visibility"});
    }
    public void Dispose(){Store.currentClient=oldClient;Store.config=oldConfig;Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",oldDev);Environment.SetEnvironmentVariable(ZionBoxVisibilityProbe.EnvironmentKey,oldMode);}
    static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerInstance=new();c.viewMan=new();c.messageQueue=new();c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());Store.currentClient=c;Attach(c);return c;
    }
    static ViewCreation Creation(WorldClient c)=>WireFormat.FindCreations(c.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>().Last().content).Single();
    static void Attach(WorldClient c)
    {
        var xml=new System.Xml.XmlDocument();xml.LoadXml("<mission id='101209' title='Hardline Hunter' description='D' plot='1' exp='1' info='1'><phase><object id='1' kind='Box'/><objective goal='1' type='Use' object='1' text='Box'/></phase></mission>");
        var mission=MissionXml.Parse(xml.DocumentElement!);var p=new MissionProgress(mission);c.playerData.activeMission=mission;c.playerData.missionProgress=p;c.playerData.missionInstanceCounter=7;
        var raw=new StaticWorldObject{metrId=1,mxoStaticId=0x0d100397,type=BitConverter.GetBytes((ushort)1412),pos_x=24070,pos_y=988,pos_z=15112.5,quat=ZionBoxVisibilityProbe.Identity};
        var o=new MissionObjectState{def=mission.phases[0].objects.Single(),spot=raw,goid=548,entity=12345};
        o.OpenFloor=new MissionOpenFloorPlacement(4,1,101209,0,1,raw.mxoStaticId,1412,548,MissionOpenFloorPlacement.RawHash(raw),new string('a',64),ZionBoxVisibilityProbe.Identity,
            new[]{ZionBoxVisibilityProbe.X,ZionBoxVisibilityProbe.Y,ZionBoxVisibilityProbe.Z},new[]{23814.611328125,895,14143.16796875},
            new[]{23739.223227,895,13996.116135,23814.611328125,895,14143.16796875},new ushort[]{209,83,5},new ushort[]{209,83,3},new ushort[]{209,83,3});
        p.objects[1]=o;var v=c.viewMan.GetViewForEntityAndGo(o.entity,548);v.spawnId=33;v.viewCreated=true;
    }
    [Theory]
    [InlineData("1","1","mxotest_zion",110u,true,true)] [InlineData("0","1","mxotest_zion",110u,true,false)]
    [InlineData("1","true","mxotest_zion",110u,true,false)] [InlineData("1","1","reality_hd",110u,true,false)]
    [InlineData("1","1","mxotest_",110u,true,false)] [InlineData("1","1","mxotest_bad-name",110u,true,false)]
    [InlineData("1","1","mxotest_zion",2u,true,false)] [InlineData("1","1","mxotest_zion",110u,false,false)]
    public void StrictDisposableCsrOptIn(string dev,string mode,string db,uint id,bool ready,bool expected)=>Assert.Equal(expected,ZionBoxVisibilityProbe.Allowed(dev,mode,db,id,ready));
    [Theory] [InlineData(0)] [InlineData(-50)] [InlineData(-100)] [InlineData(-150)] [InlineData(-200)] [InlineData(-300)]
    public void Actual03UsesSameRawDefaultAndOnlySouthOffset(int delta)
    {
        var obj=ZionBoxVisibilityProbe.CreateObject(delta);Assert.Equal(uint.MaxValue,obj.GetRelatedStaticObjId());Assert.False(obj.HalfExtents.isActive());Assert.False(obj.CurrentState.isActive());
        var raw=new ObjectManager().GenerateCreationPacket(obj,0,7).getBytes();Assert.Equal(0x0c,raw[0]);
        var v=WireFormat.FindCreations(new byte[]{1,0}.Concat(raw).Concat(new byte[]{12,0,0}).ToArray()).Single();Assert.Equal((ushort)548,v.Goid);
        Assert.Equal(Convert.FromHexString(ZionBoxVisibilityProbe.Identity),v.Attributes["Orientation"]);Assert.False(v.Attributes.ContainsKey("CurrentState"));
        Assert.Equal(new byte[]{1},v.Attributes["UseCount"]);Assert.Equal(new byte[4],v.Attributes["MissionKey"]);
        Assert.Equal(new[]{ZionBoxVisibilityProbe.X,840,ZionBoxVisibilityProbe.Z+delta},Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(v.Attributes["Position"],8*i)));
    }
    [Fact] public void ReplacementAndOffDeleteOnlyPrivateLeaseNeverOriginalExecutorOrState()
    {
        var c=Client();var p=c.playerData.missionProgress;var original=p.objects[1];var view=c.viewMan.FindViewForEntity(original.entity);var witness=original.OpenFloor;
        foreach(int delta in new[]{0,-50,-100,-150,-200,-300})
        {
            Assert.True(ZionBoxVisibilityProbe.Activate(c,delta));var clone=Creation(c);Assert.NotEqual(view.ViewID,clone.ViewId);
            Assert.Same(p,c.playerData.missionProgress);Assert.Same(original,p.objects[1]);Assert.Same(witness,original.OpenFloor);Assert.Same(view,c.viewMan.FindViewForEntity(original.entity));Assert.Empty(p.itemSlots);
            ZionBoxVisibilityProbe.Clear(c);Assert.Null(c.viewMan.getViewById(clone.ViewId));Assert.Same(view,c.viewMan.FindViewForEntity(original.entity));
        }
        Assert.Empty(c.messageQueue.RPCMessagesQueue);Assert.Equal(0,p.phaseIndex);Assert.False(p.waitingForExit);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void WrongCurrentMissionOrExactSourceOrPoseIsRefusedWithoutFallback(int change)
    {
        var c=Client();var p=c.playerData.missionProgress;var o=p.objects[1];
        switch(change){case 0:p.phaseIndex=1;break;case 1:o.spot.mxoStaticId++;break;case 2:o.spot.pos_y++;break;case 3:o.goid=552;break;case 4:o.OpenFloor=null;break;case 5:p.waitingForExit=true;break;case 6:c.playerData.activeMission=null;break;case 7:c.viewMan.FindViewForEntity(o.entity).viewCreated=false;break;}
        Assert.False(ZionBoxVisibilityProbe.Activate(c,0));Assert.Empty(c.messageQueue.ObjectMessagesQueue);Assert.Empty(c.messageQueue.RPCMessagesQueue);
    }
    [Fact] public void MissionReplacementAndOriginalSpawnReuseInvalidateOnlyPrivateClone()
    {
        var c=Client();Assert.True(ZionBoxVisibilityProbe.Activate(c,0));var clone=Creation(c);var original=c.viewMan.FindViewForEntity(c.playerData.missionProgress.objects[1].entity);original.spawnId++;
        ZionBoxVisibilityProbe.Check(c);Assert.Null(c.viewMan.getViewById(clone.ViewId));Assert.Same(original,c.viewMan.getViewById(original.ViewID));
        Attach(c);Assert.True(ZionBoxVisibilityProbe.Activate(c,0));clone=Creation(c);c.playerData.missionProgress=new MissionProgress(c.playerData.activeMission);ZionBoxVisibilityProbe.Check(c);Assert.Null(c.viewMan.getViewById(clone.ViewId));Assert.Same(original,c.viewMan.getViewById(original.ViewID));
    }
    [Fact] public void ResetDoesNotDeleteReusedViewAndOwnerReplacementCannotWriteIntoNewLifecycle()
    {
        var c=Client();Assert.True(ZionBoxVisibilityProbe.Activate(c,0));var clone=Creation(c);int count=c.messageQueue.ObjectMessagesQueue.Count;
        c.TransportFence.PublishIncoming(true,()=>{});long epoch=c.TransportFence.Capture();ZionBoxVisibilityProbe.ClearBeforeEpoch(c,epoch);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);
        c.TransportFence.ApplyReset(epoch,()=>c.viewMan=new ViewManager());c.TransportFence.Resume(epoch);c.playerData.worldGeneration++;Attach(c);
        Assert.True(ZionBoxVisibilityProbe.Activate(c,0));clone=Creation(c);ZionBoxVisibilityProbe.ClearBeforeEpoch(c,epoch);Assert.NotNull(c.viewMan.getViewById(clone.ViewId));
        var replacement=new ClientData();replacement.setCharID(110);replacement.setDistrictId(1);replacement.setOnWorld(true);replacement.worldGeneration=c.playerData.worldGeneration;replacement.lifeGeneration=c.playerData.lifeGeneration;c.playerData=replacement;
        count=c.messageQueue.ObjectMessagesQueue.Count;ZionBoxVisibilityProbe.Check(c);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);Assert.NotNull(c.viewMan.getViewById(clone.ViewId));
    }
    [Fact] public void LifeAndOptInRevocationClearExactLease()
    {
        var c=Client();Assert.True(ZionBoxVisibilityProbe.Activate(c,0));var clone=Creation(c);c.playerData.lifeGeneration++;ZionBoxVisibilityProbe.Check(c);Assert.Null(c.viewMan.getViewById(clone.ViewId));
        Assert.True(ZionBoxVisibilityProbe.Activate(c,0));clone=Creation(c);Environment.SetEnvironmentVariable(ZionBoxVisibilityProbe.EnvironmentKey,"0");ZionBoxVisibilityProbe.Check(c);Assert.Null(c.viewMan.getViewById(clone.ViewId));
    }
    [Theory] [InlineData("0",true)] [InlineData("-50",true)] [InlineData("-300",true)] [InlineData("50",false)] [InlineData("-25",false)] [InlineData("-0",false)] [InlineData("00",false)] [InlineData("+0",false)]
    public void OnlyLiteralSouthOffsets(string input,bool expected)=>Assert.Equal(expected,ZionBoxVisibilityProbe.Parse(new[]{"?zionbox",input},out _,out _,out _));
    [Fact] public void ModeRetainsOrdinaryZionProfileAndAddsOnlyExplicitFeature()
    {
        var p=new NativeQa.Modes.ZionBoxVisibilityMode().Plan;var normal=new NativeQa.Modes.InteriorAdmissionZionMode().Plan;
        Assert.Equal(normal.Abilities,p.Abilities);Assert.Equal(normal.AfterHelpers,p.AfterHelpers);Assert.Equal(7800,p.X);Assert.Equal(13795,p.Z);Assert.True(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Equal("1",p.EnvironmentOverrides[ZionBoxVisibilityProbe.EnvironmentKey]);Assert.Equal("101208",p.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")[ZionBoxVisibilityProbe.EnvironmentKey]);
        Assert.DoesNotContain(p.AfterHelpers,s=>s.Contains("char_active_missions"));
    }
    [Fact] public void SameKeyControlUsesOnlyCapturedCurrentInstanceAndNoExecutor()
    {
        var c=Client();var p=c.playerData.missionProgress;var original=p.objects[1];var view=c.viewMan.FindViewForEntity(original.entity);
        Assert.True(ZionBoxVisibilityProbe.ActivateSame(c));var clone=Creation(c);
        Assert.Equal(new byte[]{0xff,7,0,2},clone.Attributes["MissionKey"]);Assert.NotEqual(view.ViewID,clone.ViewId);
        var zero=ZionBoxVisibilityProbe.CreateObject(0);var raw=new ObjectManager().GenerateCreationPacket(zero,0,7).getBytes();
        var expected=WireFormat.FindCreations(new byte[]{1,0}.Concat(raw).Concat(new byte[]{12,0,0}).ToArray()).Single();
        Assert.Equal(expected.Attributes.Keys.Order(),clone.Attributes.Keys.Order());foreach(var a in expected.Attributes)if(a.Key!="MissionKey")Assert.Equal(a.Value,clone.Attributes[a.Key]);
        Assert.Same(original,p.objects[1]);Assert.Single(p.objects);Assert.Empty(p.itemSlots);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        c.playerData.missionInstanceCounter++;ZionBoxVisibilityProbe.Check(c);Assert.Null(c.viewMan.getViewById(clone.ViewId));Assert.Same(view,c.viewMan.FindViewForEntity(original.entity));
        Assert.False(ZionBoxVisibilityProbe.Activate(c,548,-50,true));
    }
    [Fact] public void SameIsSingleLiteralAndNeverArbitraryKeyInput()
    {
        Assert.True(ZionBoxVisibilityProbe.Parse(new[]{"?zionbox","same"},out _,out int delta,out bool off,out bool same));Assert.True(same);Assert.False(off);Assert.Equal(0,delta);
        foreach(var words in new[]{new[]{"?zionbox","same","0"},new[]{"?zionbox","FF070002"},new[]{"?zionbox","Same"},new[]{"?zionbox","same","-50"}})
            Assert.False(ZionBoxVisibilityProbe.Parse(words,out _,out _,out _,out _));
    }

}
