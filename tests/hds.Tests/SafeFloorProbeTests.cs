using System.Reflection;
using System.Runtime.CompilerServices;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class SafeFloorProbeTests:IDisposable
{
    readonly WorldClient oldClient=Store.currentClient;readonly ServerConfig oldConfig=Store.config;
    readonly string? oldDev=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),oldMode=Environment.GetEnvironmentVariable(SafeFloorProbe.EnvironmentKey);
    public SafeFloorProbeTests()
    {
        Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","1");Environment.SetEnvironmentVariable(SafeFloorProbe.EnvironmentKey,"1");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_safe"});
    }
    public void Dispose(){Store.currentClient=oldClient;Store.config=oldConfig;Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",oldDev);Environment.SetEnvironmentVariable(SafeFloorProbe.EnvironmentKey,oldMode);}
    static WorldClient Client()
    {
        var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerInstance=new();c.viewMan=new();c.messageQueue=new();c.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());Store.currentClient=c;return c;
    }
    static ViewCreation Creation(WorldClient c)=>WireFormat.FindCreations(c.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>().Last().content).Single();
    [Theory]
    [InlineData("1","1","mxotest_safe",110u,true,true)] [InlineData("0","1","mxotest_safe",110u,true,false)]
    [InlineData("1","true","mxotest_safe",110u,true,false)] [InlineData("1","1","reality_hd",110u,true,false)]
    [InlineData("1","1","mxotest_",110u,true,false)] [InlineData("1","1","mxotest_bad-name",110u,true,false)]
    [InlineData("1","1","mxotest_safe",2u,true,false)] [InlineData("1","1","mxotest_safe",111u,true,false)] [InlineData("1","1","mxotest_safe",110u,false,false)]
    public void StrictOptInActualDatabaseCsrAndReadyGuard(string dev,string mode,string db,uint id,bool ready,bool expected)=>Assert.Equal(expected,SafeFloorProbe.Allowed(dev,mode,db,id,ready));
    [Fact] public void FreeCreationUsesActual552SchemaAndSourceDefaultWithoutBoundsOverride()
    {
        var obj=SafeFloorProbe.CreateObject();Assert.Equal(uint.MaxValue,obj.GetRelatedStaticObjId());Assert.False(obj.HalfExtents.isActive());
        var raw=new ObjectManager().GenerateCreationPacket(obj,0,7).getBytes();Assert.Equal(0x0c,raw[0]);
        var v=WireFormat.FindCreations(new byte[]{1,0}.Concat(raw).Concat(new byte[]{12,0,0}).ToArray()).Single();Assert.Equal((ushort)552,v.Goid);
        Assert.Equal(Convert.FromHexString(SafeFloorProbe.Identity),v.Attributes["Orientation"]);Assert.Equal(BitConverter.GetBytes(0x835u),v.Attributes["CurrentState"]);
        Assert.Equal(new byte[]{1},v.Attributes["UseCount"]);Assert.Equal(new byte[4],v.Attributes["MissionKey"]);
        Assert.Equal(new[]{25100d,2050d,14100d},Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(v.Attributes["Position"],8*i)));
    }
    [Fact] public void ActualOnOffOwnsOneFreeViewAndDoesNotRegisterMissionAuthority()
    {
        var c=Client();Assert.True(SafeFloorProbe.Activate(c));var first=Creation(c);var oldView=c.viewMan.getViewById(first.ViewId);Assert.Null(c.playerData.missionProgress);Assert.Null(c.playerData.activeMission);Assert.Empty(c.messageQueue.RPCMessagesQueue);
        Assert.True(SafeFloorProbe.Activate(c));var second=Creation(c);Assert.NotEqual((first.ViewId,first.SpawnId),(second.ViewId,second.SpawnId));Assert.NotSame(oldView,c.viewMan.getViewById(second.ViewId));
        int before=c.messageQueue.ObjectMessagesQueue.Count;SafeFloorProbe.Clear(c);Assert.Equal(before+1,c.messageQueue.ObjectMessagesQueue.Count);Assert.Null(c.viewMan.getViewById(second.ViewId));
        SafeFloorProbe.Clear(c);Assert.Equal(before+1,c.messageQueue.ObjectMessagesQueue.Count);
    }
    [Fact] public void ActualGuardRejectsCurrentDatabaseAndWorldFailuresWithoutCreation()
    {
        var c=Client();Store.config.dbParams.DatabaseName="reality_hd";Assert.False(SafeFloorProbe.Activate(c));Store.config.dbParams.DatabaseName="mxotest_safe";
        c.playerData.worldChangePending=true;Assert.False(SafeFloorProbe.Activate(c));c.playerData.worldChangePending=false;c.playerData.isDead=true;Assert.False(SafeFloorProbe.Activate(c));c.playerData.isDead=false;
        c.playerData.setDistrictId(901);Assert.False(SafeFloorProbe.Activate(c));c.playerData.setDistrictId(1);Environment.SetEnvironmentVariable(SafeFloorProbe.EnvironmentKey,"0");Assert.False(SafeFloorProbe.Activate(c));Assert.Empty(c.messageQueue.ObjectMessagesQueue);
    }
    [Fact] public void ResetCancelsOldLeaseWithoutDeletingReplacementOrUnrelatedMessages()
    {
        var c=Client();Assert.True(SafeFloorProbe.Activate(c));c.messageQueue.addRpcMessage(new byte[]{0x81,0x49});
        c.TransportFence.PublishIncoming(true,()=>{});long reset=c.TransportFence.Capture();int before=c.messageQueue.ObjectMessagesQueue.Count;
        SafeFloorProbe.ClearBeforeEpoch(c,reset);Assert.Equal(before,c.messageQueue.ObjectMessagesQueue.Count);Assert.Single(c.messageQueue.RPCMessagesQueue);
        c.TransportFence.ApplyReset(reset,()=>c.viewMan=new ViewManager());c.TransportFence.Resume(reset);c.playerData.worldGeneration++;
        Assert.True(SafeFloorProbe.Activate(c));var replacement=Creation(c);SafeFloorProbe.ClearBeforeEpoch(c,reset);Assert.NotNull(c.viewMan.getViewById(replacement.ViewId));
        SafeFloorProbe.Clear(c);Assert.Null(c.viewMan.getViewById(replacement.ViewId));Assert.Single(c.messageQueue.RPCMessagesQueue);
    }
    [Fact] public void LifeChangeCleansOnlyExactLeaseAndReusedSpawnIsNeverDeleted()
    {
        var c=Client();Assert.True(SafeFloorProbe.Activate(c));var first=Creation(c);c.playerData.lifeGeneration++;SafeFloorProbe.Check(c);Assert.Null(c.viewMan.getViewById(first.ViewId));
        Assert.True(SafeFloorProbe.Activate(c));var second=Creation(c);var view=c.viewMan.getViewById(second.ViewId);view.spawnId++;int count=c.messageQueue.ObjectMessagesQueue.Count;
        SafeFloorProbe.Clear(c);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);Assert.Same(view,c.viewMan.getViewById(second.ViewId));
    }
    [Fact] public void RevokedOptInAndMarkedDeletedViewCannotRemainAnActiveLease()
    {
        var c=Client();Assert.True(SafeFloorProbe.Activate(c));var first=Creation(c);
        Environment.SetEnvironmentVariable(SafeFloorProbe.EnvironmentKey,"0");SafeFloorProbe.Check(c);Assert.Null(c.viewMan.getViewById(first.ViewId));
        Environment.SetEnvironmentVariable(SafeFloorProbe.EnvironmentKey,"1");Assert.True(SafeFloorProbe.Activate(c));var second=Creation(c);var view=c.viewMan.getViewById(second.ViewId);view.viewNeedsToBeDeleted=true;
        int count=c.messageQueue.ObjectMessagesQueue.Count;SafeFloorProbe.Check(c);SafeFloorProbe.Clear(c);Assert.Equal(count,c.messageQueue.ObjectMessagesQueue.Count);
    }
    [Fact] public void ModeIsStandaloneAndDefaultModesHaveNoAuthority()
    {
        var plan=new NativeQa.Modes.SafeFloorMode().Plan;Assert.Equal(new ushort[]{2,17},plan.Abilities);Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal("1",plan.EnvironmentOverrides[SafeFloorProbe.EnvironmentKey]);Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")[SafeFloorProbe.EnvironmentKey]);Assert.DoesNotContain(plan.AfterHelpers,s=>s.Contains("char_missions"));
    }
}
