using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public sealed class MissionPropProbeTests:IDisposable
{
 readonly WorldClient oldClient=Store.currentClient; readonly ServerConfig oldConfig=Store.config; readonly WorldServer oldWorld=Store.world;
 readonly string? dev=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),mode=Environment.GetEnvironmentVariable(MissionPropProbe.EnvironmentKey);
 public MissionPropProbeTests(){Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","1");Environment.SetEnvironmentVariable(MissionPropProbe.EnvironmentKey,"1");Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_probe"});Store.world=(WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));Store.world.objMan=new ObjectManager();}
 public void Dispose(){Store.currentClient=oldClient;Store.config=oldConfig;Store.world=oldWorld;Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",dev);Environment.SetEnvironmentVariable(MissionPropProbe.EnvironmentKey,mode);}
 static readonly byte[] Position=new[]{12.5,1225.003776550293,34.25}.SelectMany(BitConverter.GetBytes).ToArray();
 static readonly byte[] Quaternion=Convert.FromHexString("00000000F204353F00000000F40435BF"),Key=Convert.FromHexString("FF2A0002");
 static WorldClient Client(bool entered=true)
 {
  var c=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));c.playerData=new();c.playerData.setCharID(110);c.playerData.setDistrictId(1);c.playerData.setOnWorld(true);c.playerInstance=new();c.viewMan=new();c.messageQueue=new();c.Alive=true;c.playerData.missionInstanceCounter=42;
  typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,TimeUtils.getUnixTimeUint32());typeof(WorldClient).GetField("lastFlushTick",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,long.MaxValue);typeof(WorldClient).GetField("<TransportFence>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(c,new WorldSendFence());Store.currentClient=c;
  var xml=new XmlDocument();xml.LoadXml("<mission id='201201' title='D' description='D'><phase/><phase><object id='1' kind='Computer'/><objective goal='1' type='Search' object='1' item='6783'/></phase></mission>");var m=MissionXml.Parse(xml.DocumentElement!);var p=new MissionProgress(m){phaseIndex=1,entered=entered};c.playerData.activeMission=m;c.playerData.missionProgress=p;
  var source=new StaticWorldObject{metrId=1,sectorID=209,mxoStaticId=0x0d100166,pos_x=29300,pos_y=1426,pos_z=13860,type=BitConverter.GetBytes((ushort)1412),quat=Convert.ToHexString(Quaternion)};
  var pose=new MissionOpenFloorPlacement(4,1,201201,1,1,source.mxoStaticId,1412,3,MissionOpenFloorPlacement.RawHash(source),new string('A',64),Convert.ToHexString(Quaternion),new[]{12.5,1225.003776550293,34.25},new[]{12.5,1295.0,35.0},new[]{12.5,1295.0,36.0,12.5,1295.0,35.0},new ushort[]{209,1,1},new ushort[]{209,1,1},new ushort[]{209,1,1});
  p.objects[1]=new MissionObjectState{def=m.phases[1].objects[0],spot=source,goid=3,entity=989003001,OpenFloor=pose,shownState=0x961};
  var obj=MissionPropProbe.Build(3,0,true,Position,Quaternion,0x961,Key,new byte[]{1});new ServerPackets().SendSpawnGameObject(c,obj,p.objects[1].entity);return c;
 }
 static ViewCreation Last(WorldClient c)=>WireFormat.FindCreations(c.messageQueue.ObjectMessagesQueue.Cast<SequencedMessage>().Last().content).Single();
 [Theory][InlineData("1","1","mxotest_probe",110u,true,true)][InlineData("0","1","mxotest_probe",110u,true,false)][InlineData("1","1","reality_hd",110u,true,false)][InlineData("1","1","mxotest_",110u,true,false)][InlineData("1","1","mxotest_probe",2u,true,false)][InlineData("1","1","mxotest_probe",110u,false,false)]
 public void StrictActualOwnerGuard(string d,string m,string db,uint id,bool ready,bool expected)=>Assert.Equal(expected,MissionPropProbe.Allowed(d,m,db,id,ready));
 [Theory][InlineData("same","3","0",true)][InlineData("zero","548","95",true)][InlineData("same","552","100",true)][InlineData("same","03","0",false)][InlineData("same","3","+25",false)][InlineData("same","3","26",false)][InlineData("same","357","0",false)][InlineData("bad","3","0",false)]
 public void LiteralSelectors(string key,string asset,string height,bool expected)=>Assert.Equal(expected,MissionPropProbe.Parse(new[]{"?missionprop",key,asset,height},out _,out _,out _,out _));
 [Fact]public void LiteralBaselineWirePreservesEveryOriginalFieldAndOnlyOnePrivateLease()
 {
  var c=Client();var original=Last(c);Assert.True(MissionPropProbe.Activate(c,true,3,0));var first=Last(c);Assert.NotEqual(original.ViewId,first.ViewId);foreach(string a in new[]{"Position","Orientation","MissionKey","CurrentState","UseCount"})Assert.Equal(original.Attributes[a],first.Attributes[a]);Assert.Single(c.playerData.missionProgress.objects);Assert.Empty(c.messageQueue.RPCMessagesQueue);
  Assert.True(MissionPropProbe.Activate(c,false,3,0));var next=Last(c);Assert.Equal(new byte[4],next.Attributes["MissionKey"]);Assert.Null(c.viewMan.getViewById(first.ViewId));Assert.NotNull(c.viewMan.getViewById(original.ViewId));MissionPropProbe.Clear(c);Assert.Null(c.viewMan.getViewById(next.ViewId));Assert.NotNull(c.viewMan.getViewById(original.ViewId));
 }
 [Theory][InlineData((ushort)3,1225.003776550293)][InlineData((ushort)548,1240)][InlineData((ushort)552,1250)]
 public void MeasuredSupportFloorAndLiteralQuaternionWithoutBoundsOverride(ushort goid,double baseline)
 {foreach(int delta in new[]{0,25,50,75,95,100}){var obj=MissionPropProbe.Build(goid,delta,true,Position,Quaternion,0x961,Key,new byte[]{1});var v=WireFormat.FindCreations(new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,0,7).getBytes()).Concat(new byte[]{12,0,0}).ToArray()).Single();Assert.Equal(baseline+delta,BitConverter.ToDouble(v.Attributes["Position"],8));Assert.Equal(12.5,BitConverter.ToDouble(v.Attributes["Position"],0));Assert.Equal(34.25,BitConverter.ToDouble(v.Attributes["Position"],16));Assert.Equal(Quaternion,v.Attributes["Orientation"]);Assert.False(v.Attributes.ContainsKey("HalfExtents"));Assert.Equal(goid==3,v.Attributes.ContainsKey("CurrentState"));Assert.Equal(uint.MaxValue,obj.GetRelatedStaticObjId());}}
 [Theory][InlineData("phase")][InlineData("world")][InlineData("life")][InlineData("dead")][InlineData("failed")][InlineData("state")][InlineData("entity")][InlineData("owner")][InlineData("queue")][InlineData("player")][InlineData("view")][InlineData("revoked")]
 public void CurrentCapabilityCannotSurviveMutationOrDeleteOriginal(string change)
 {var c=Client();var original=Last(c);Assert.True(MissionPropProbe.Activate(c,true,3,0));var clone=Last(c);switch(change){case "phase":c.playerData.missionProgress.phaseIndex=0;break;case "world":c.playerData.worldGeneration++;break;case "life":c.playerData.lifeGeneration++;break;case "dead":c.playerData.isDead=true;break;case "failed":c.playerData.missionProgress.failed=true;break;case "state":c.playerData.missionProgress.objects[1].shownState++;break;case "entity":c.playerData.missionProgress.objects[1].entity++;break;case "owner":c.playerData=new ClientData();break;case "queue":c.messageQueue=new MessageQueue();break;case "player":c.playerInstance=new PlayerCharacter();break;case "view":c.viewMan.getViewById(original.ViewId).spawnId++;break;case "revoked":Environment.SetEnvironmentVariable(MissionPropProbe.EnvironmentKey,"0");break;}MissionPropProbe.Check(c);Assert.False(MissionPropProbe.Activate(c,true,3,0));Assert.NotNull(c.viewMan.getViewById(original.ViewId));if(change!="world"&&change!="owner"&&change!="queue"&&change!="player")Assert.Null(c.viewMan.getViewById(clone.ViewId));}
 [Fact]public void PreEntryObservationIsRetainedButCannotPublishUntilEnteredAndResetInvalidates()
 {var c=Client(false);Assert.False(MissionPropProbe.Activate(c,true,3,0));MissionPropProbe.Check(c);c.playerData.missionProgress.entered=true;Assert.True(MissionPropProbe.Activate(c,true,3,0));var clone=Last(c);c.TransportFence.PublishIncoming(true,()=>{});MissionPropProbe.ClearBeforeEpoch(c,c.TransportFence.Capture());c.TransportFence.Resume(c.TransportFence.Capture());Assert.False(MissionPropProbe.Activate(c,true,3,0));Assert.NotNull(c.viewMan.getViewById(clone.ViewId));}
 [Fact]public async Task ResetPublicationAndCleanupDoNotAcquireMissionLockUnderTransportGate()
 {
  var c=Client();using var ownerHeld=new ManualResetEventSlim();using var resetDone=new ManualResetEventSlim();
  var owner=Task.Run(()=>{lock(c.playerData.missionSync){ownerHeld.Set();Assert.True(resetDone.Wait(TimeSpan.FromSeconds(3)));Assert.False(MissionPropProbe.Activate(c,true,3,0));}});
  var reset=Task.Run(()=>{Assert.True(ownerHeld.Wait(TimeSpan.FromSeconds(3)));c.TransportFence.PublishIncoming(true,()=>{});MissionPropProbe.ClearBeforeEpoch(c,c.TransportFence.Capture());resetDone.Set();});
  await Task.WhenAll(owner,reset).WaitAsync(TimeSpan.FromSeconds(5));
 }
 [Theory][InlineData(false)][InlineData(true)]public void ActualNormalUseOfEitherKeyDoesNotOwnMissionGoalOrItem(bool same)
 {
  var c=Client();Assert.True(MissionPropProbe.Activate(c,same,3,0));var clone=Last(c);var p=c.playerData.missionProgress;string before=System.Text.Json.JsonSerializer.Serialize(p.Capture(42,Array.Empty<Mob>()),MissionProgress.SaveJson);
  using(var output=MissionOutput.Begin(c.messageQueue)){byte[] request=BitConverter.GetBytes(clone.ViewId).Concat(new byte[]{clone.SpawnId,0,5,0}).ToArray();new ObjectInteractionHandler().processObjectDynamic(ref request);output.Commit();}
  Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(p.Capture(42,Array.Empty<Mob>()),MissionProgress.SaveJson));Assert.Single(p.objects);Assert.Empty(p.itemSlots);Assert.False(p.objects[1].lootOpen);
  Assert.Contains(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),r=>MissionPropReplies.Empty(r.content.Skip(1).ToArray()));Assert.All(c.messageQueue.RPCMessagesQueue.Cast<SequencedMessage>(),r=>Assert.True(MissionPropReplies.Allowed(3,r.content.Skip(1).ToArray())));
 }
 [Fact]public void PlanUsesNormalEarlierReceiptsOnlyAndOtherModesKeepFeatureOff()
 {var p=new NativeQa.Modes.MissionPropVisibilityMode().Plan;Assert.Equal("1",p.EnvironmentOverrides[MissionPropProbe.EnvironmentKey]);Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("")[MissionPropProbe.EnvironmentKey]);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Equal(20,InteriorAdmissionFixture.Machines.Prerequisites.Length);Assert.DoesNotContain(201201,InteriorAdmissionFixture.Machines.Prerequisites);Assert.DoesNotContain(p.AfterHelpers,s=>s.Contains("char_active_missions"));}
 [Fact]public void GenericReplyOracleRejectsItemsClaimsGoalsSummaryAndRewards()
 {var empty=Convert.FromHexString("811900000000100000000000000000000000");Assert.True(MissionPropReplies.Allowed(3,empty));Assert.False(MissionPropReplies.Allowed(548,empty));Assert.False(MissionPropReplies.Allowed(3,empty.Concat(new byte[]{1}).ToArray()));foreach(string s in new[]{"81200000","80A001060001","80A200","80E600"})Assert.False(MissionPropReplies.Allowed(3,Convert.FromHexString(s)));}
}
