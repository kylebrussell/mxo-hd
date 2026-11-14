using System.Reflection;
using System.Runtime.CompilerServices;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class PropSupportProbeTests:IDisposable
{
    readonly WorldClient oldClient=Store.currentClient;readonly ServerConfig oldConfig=Store.config;
    readonly string? oldDev=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),oldMode=Environment.GetEnvironmentVariable(PropSupportProbe.EnvironmentKey);
    public PropSupportProbeTests()
    {
        Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","1");Environment.SetEnvironmentVariable(PropSupportProbe.EnvironmentKey,"1");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_support"});
    }
    public void Dispose(){Store.currentClient=oldClient;Store.config=oldConfig;Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",oldDev);Environment.SetEnvironmentVariable(PropSupportProbe.EnvironmentKey,oldMode);}
    static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerInstance=new();c.viewMan=new();c.messageQueue=new();c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());Store.currentClient=c;return c;
    }
    static ViewCreation Creation(WorldClient c)=>WireFormat.FindCreations(c.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>().Last().content).Single();
    [Theory]
    [InlineData("1","1","mxotest_support",110u,true,true)] [InlineData("0","1","mxotest_support",110u,true,false)]
    [InlineData("1","true","mxotest_support",110u,true,false)] [InlineData("1","1","reality_hd",110u,true,false)]
    [InlineData("1","1","mxotest_",110u,true,false)] [InlineData("1","1","mxotest_bad-name",110u,true,false)]
    [InlineData("1","1","mxotest_support",2u,true,false)] [InlineData("1","1","mxotest_support",111u,true,false)] [InlineData("1","1","mxotest_support",110u,false,false)]
    public void StrictOptInActualDatabaseCsrAndReadyGuard(string dev,string mode,string db,uint id,bool ready,bool expected)=>Assert.Equal(expected,PropSupportProbe.Allowed(dev,mode,db,id,ready));
    [Theory] [InlineData(0)] [InlineData(25)] [InlineData(50)] [InlineData(75)] [InlineData(100)]
    public void OnlyRequestedHeightChangesAndInvalidDeltasAreRejected(int delta)
    {
        var a=PropSupportProbe.CreateObject(delta);var p=a.Position.getValue();
        Assert.Equal(PropSupportProbe.X,BitConverter.ToDouble(p,0));Assert.Equal(PropSupportProbe.Y+delta,BitConverter.ToDouble(p,8));Assert.Equal(PropSupportProbe.Z,BitConverter.ToDouble(p,16));
        Assert.Throws<ArgumentOutOfRangeException>(()=>PropSupportProbe.CreateObject(-25));Assert.Throws<ArgumentOutOfRangeException>(()=>PropSupportProbe.CreateObject(101));
    }
    [Fact] public void FreeCreationUsesActual3SchemaAndSourceDefaultWithoutBoundsOverride()
    {
        var obj=PropSupportProbe.CreateObject(0);Assert.Equal(uint.MaxValue,obj.GetRelatedStaticObjId());Assert.False(obj.HalfExtents.isActive());
        var raw=new ObjectManager().GenerateCreationPacket(obj,0,7).getBytes();Assert.Equal(0x0c,raw[0]);
        var v=WireFormat.FindCreations(new byte[]{1,0}.Concat(raw).Concat(new byte[]{12,0,0}).ToArray()).Single();Assert.Equal((ushort)3,v.Goid);
        Assert.Equal(Convert.FromHexString(PropSupportProbe.Identity),v.Attributes["Orientation"]);Assert.Equal(BitConverter.GetBytes(0x961u),v.Attributes["CurrentState"]);Assert.True(obj.CurrentState.isActive());
        Assert.Equal(new byte[]{1},v.Attributes["UseCount"]);Assert.Equal(new byte[4],v.Attributes["MissionKey"]);
        Assert.Equal(new[]{PropSupportProbe.X,PropSupportProbe.Y,PropSupportProbe.Z},Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(v.Attributes["Position"],8*i)));
    }
    [Fact] public void ActualOnOffOwnsOneFreeViewAndDoesNotRegisterMissionAuthority()
    {
        var c=Client();Assert.True(PropSupportProbe.Activate(c,0));var first=Creation(c);var oldView=c.viewMan.getViewById(first.ViewId);Assert.Null(c.playerData.missionProgress);Assert.Null(c.playerData.activeMission);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        Assert.True(PropSupportProbe.Activate(c,0));var second=Creation(c);Assert.NotEqual((first.ViewId,first.SpawnId),(second.ViewId,second.SpawnId));Assert.NotSame(oldView,c.viewMan.getViewById(second.ViewId));
        int before=c.messageQueue.ObjectMessagesQueue.Count;PropSupportProbe.Clear(c);Assert.Equal(before+1,c.messageQueue.ObjectMessagesQueue.Count);Assert.Null(c.viewMan.getViewById(second.ViewId));
        PropSupportProbe.Clear(c);Assert.Equal(before+1,c.messageQueue.ObjectMessagesQueue.Count);
    }
    [Fact] public void ActualGuardRejectsCurrentDatabaseAndWorldFailuresWithoutCreation()
    {
        var c=Client();Store.config.dbParams.DatabaseName="reality_hd";Assert.False(PropSupportProbe.Activate(c,0));Store.config.dbParams.DatabaseName="mxotest_support";
        c.playerData.worldChangePending=true;Assert.False(PropSupportProbe.Activate(c,0));c.playerData.worldChangePending=false;c.playerData.isDead=true;Assert.False(PropSupportProbe.Activate(c,0));c.playerData.isDead=false;
        c.playerData.setDistrictId(901);Assert.False(PropSupportProbe.Activate(c,0));c.playerData.setDistrictId(1);Environment.SetEnvironmentVariable(PropSupportProbe.EnvironmentKey,"0");Assert.False(PropSupportProbe.Activate(c,0));Assert.Empty(c.messageQueue.ObjectMessagesQueue);
    }
    [Fact] public void ResetCancelsOldLeaseWithoutDeletingReplacementOrUnrelatedMessages()
    {
        var c=Client();Assert.True(PropSupportProbe.Activate(c,0));c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});
        c.TransportFence.PublishIncoming(true,()=>{});long reset=c.TransportFence.Capture();int before=c.messageQueue.ObjectMessagesQueue.Count;
        PropSupportProbe.ClearBeforeEpoch(c,reset);Assert.Equal(before,c.messageQueue.ObjectMessagesQueue.Count);Assert.Single(c.messageQueue.RPCMessagesQueue);
        c.TransportFence.ApplyReset(reset,()=>c.viewMan=new ViewManager());c.TransportFence.Resume(reset);c.playerData.worldGeneration++;
        Assert.True(PropSupportProbe.Activate(c,0));var replacement=Creation(c);PropSupportProbe.ClearBeforeEpoch(c,reset);Assert.NotNull(c.viewMan.getViewById(replacement.ViewId));
        PropSupportProbe.Clear(c);Assert.Null(c.viewMan.getViewById(replacement.ViewId));Assert.Single(c.messageQueue.RPCMessagesQueue);
    }
    [Fact] public void LifeChangeCleansOnlyExactLeaseAndReusedSpawnIsNeverDeleted()
    {
        var c=Client();Assert.True(PropSupportProbe.Activate(c,0));var first=Creation(c);c.playerData.lifeGeneration++;PropSupportProbe.Check(c);Assert.Null(c.viewMan.getViewById(first.ViewId));
        Assert.True(PropSupportProbe.Activate(c,0));var second=Creation(c);var view=c.viewMan.getViewById(second.ViewId);view.spawnId++;int count=c.messageQueue.ObjectMessagesQueue.Count;
        PropSupportProbe.Clear(c);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);Assert.Same(view,c.viewMan.getViewById(second.ViewId));
    }
    [Fact] public void RevokedOptInAndMarkedDeletedViewCannotRemainAnActiveLease()
    {
        var c=Client();Assert.True(PropSupportProbe.Activate(c,0));var first=Creation(c);
        Environment.SetEnvironmentVariable(PropSupportProbe.EnvironmentKey,"0");PropSupportProbe.Check(c);Assert.Null(c.viewMan.getViewById(first.ViewId));
        Environment.SetEnvironmentVariable(PropSupportProbe.EnvironmentKey,"1");Assert.True(PropSupportProbe.Activate(c,0));var second=Creation(c);var view=c.viewMan.getViewById(second.ViewId);view.viewNeedsToBeDeleted=true;
        int count=c.messageQueue.ObjectMessagesQueue.Count;PropSupportProbe.Check(c);PropSupportProbe.Clear(c);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);
    }
    [Fact] public void ReplacedOwnerWithSameCountersCannotDeleteOldViewOrCreateIntoAnotherCurrentClient()
    {
        var c=Client();Assert.True(PropSupportProbe.Activate(c,0));var first=Creation(c);int count=c.messageQueue.ObjectMessagesQueue.Count;
        var replacement=new ClientData();replacement.setCharID(110);replacement.setDistrictId(1);replacement.setOnWorld(true);
        replacement.worldGeneration=c.playerData.worldGeneration;replacement.lifeGeneration=c.playerData.lifeGeneration;c.playerData=replacement;
        PropSupportProbe.Check(c);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);Assert.NotNull(c.viewMan.getViewById(first.ViewId));
        Store.currentClient=Client();Assert.False(PropSupportProbe.Activate(c,25));Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);
    }
    [Theory] [InlineData((ushort)3,1625.003776550293)] [InlineData((ushort)548,1640)] [InlineData((ushort)552,1650)]
    public void ThreeRealAssetsShareBaselineSupportAndOneLease(ushort goid,double y)
    {
        var c=Client();foreach(int delta in new[]{0,25,50,75,100})
        {
            Assert.True(PropSupportProbe.Activate(c,goid,delta));var v=Creation(c);Assert.Equal(goid,v.Goid);
            Assert.Equal(y+delta,BitConverter.ToDouble(v.Attributes["Position"],8));Assert.False(v.Attributes.ContainsKey("HalfExtents"));
            if(goid==3)Assert.Equal(BitConverter.GetBytes(0x961u),v.Attributes["CurrentState"]);else Assert.False(v.Attributes.ContainsKey("CurrentState"));
        }
        var last=Creation(c);PropSupportProbe.Clear(c);Assert.Null(c.viewMan.getViewById(last.ViewId));
    }
    [Theory] [InlineData("3","25",true)] [InlineData("548","0",true)] [InlineData("552","100",true)]
    [InlineData("1415","0",false)] [InlineData("3","26",false)] [InlineData("03","0",false)] [InlineData("3","+25",false)]
    public void LiteralSelectorsOnly(string asset,string delta,bool expected)=>Assert.Equal(expected,PropSupportProbe.Parse(new[]{"?propsupport",asset,delta},out _,out _,out _));
    [Fact] public void ModeIsStandaloneAndDefaultModesHaveNoAuthority()
    {
        var plan=new NativeQa.Modes.PropSupportMode().Plan;Assert.Equal(new ushort[]{2,17},plan.Abilities);Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("1",plan.EnvironmentOverrides[PropSupportProbe.EnvironmentKey]);Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")[PropSupportProbe.EnvironmentKey]);Assert.DoesNotContain(plan.AfterHelpers,s=>s.Contains("char_missions"));
    }
}
