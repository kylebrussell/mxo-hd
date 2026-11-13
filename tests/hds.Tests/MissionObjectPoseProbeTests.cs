using hds;
using hds.shared;
using NativeQa;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class MissionObjectPoseProbeTests:IDisposable
{
    readonly ServerConfig old=Store.config;readonly string? dev=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),probe=Environment.GetEnvironmentVariable(MissionObjectPoseProbe.Variable),fakeDb=Environment.GetEnvironmentVariable("MXO_DB_NAME");
    readonly WorldClient client;readonly MissionProgress progress;readonly MissionObjectState state;
    public MissionObjectPoseProbeTests()
    {
        Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","1");Environment.SetEnvironmentVariable(MissionObjectPoseProbe.Variable,"full-native");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_pose"});
        client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new();client.playerInstance=new();client.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(client,TimeUtils.getUnixTimeUint32());
        client.playerData.setCharID(110);client.playerData.setDistrictId(1);client.playerData.setOnWorld(true);client.playerData.setUDPSessionEstablished(true);
        var name=new byte[32];Encoding.ASCII.GetBytes("CSR_Tester").CopyTo(name,0);client.playerInstance.CharacterName.setValue(name);
        var mission=new Mission{id=3072};progress=new MissionProgress(mission);client.playerData.activeMission=mission;client.playerData.missionProgress=progress;
        state=new MissionObjectState{def=new MissionObjectDef{id=1,kind="Computer"},goid=3,spot=new StaticWorldObject{metrId=1,sectorID=347,mxoStaticId=0x15b01176,staticId=0x1176,type=new byte[]{0x87,5},pos_x=92740,pos_y=4445,pos_z=13535,quat=MissionObjectPoseProbe.OriginalQuaternion}};progress.objects[1]=state;
    }
    public void Dispose(){Store.config=old;Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",dev);Environment.SetEnvironmentVariable(MissionObjectPoseProbe.Variable,probe);Environment.SetEnvironmentVariable("MXO_DB_NAME",fakeDb);}
    [Theory][InlineData("control",92740,4445,13535,false)][InlineData("native-y",92740,4300,13535,false)][InlineData("full-native",91865,4300,12660,true)]
    public void ExactVariantsOnlyChangeDerivedSerialization(string mode,double x,double y,double z,bool identity)
    {
        Environment.SetEnvironmentVariable(MissionObjectPoseProbe.Variable,mode);var original=state.spot;var bytes=NumericalUtils.doublesToLtVector3d(original.pos_x,original.pos_y,original.pos_z);
        Assert.True(MissionObjectPoseProbe.SourceMatches(original));Assert.Equal("CSR_Tester",SocialHandler.HandleOf(client));Assert.True(MissionObjectPoseProbe.DatabaseAllowed(Store.config.dbParams.DatabaseName));Assert.True(client.playerData.getUDPSEssionEstablished());Assert.True(client.playerData.getOnWorld());Assert.Equal(110u,client.playerData.getCharID());var p=MissionObjectPoseProbe.SerializedPose(client,progress,state);Assert.Equal(new[]{x,y,z},new[]{p.X,p.Y,p.Z});Assert.Equal(identity?MissionObjectPoseProbe.IdentityQuaternion:original.quat,p.Quaternion);
        Assert.Same(original,state.spot);Assert.Equal(bytes,NumericalUtils.doublesToLtVector3d(original.pos_x,original.pos_y,original.pos_z));Assert.Equal(MissionObjectPoseProbe.OriginalQuaternion,original.quat);
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("4325")][InlineData("FULL-NATIVE")][InlineData("full-native ")]
    public void DisabledAndUnknownVariantsAreByteExactDefault(string? mode)
    {Environment.SetEnvironmentVariable(MissionObjectPoseProbe.Variable,mode);AssertDefault();}
    void AssertDefault()
    {var s=state.spot;var p=MissionObjectPoseProbe.SerializedPose(client,progress,state);Assert.Equal(NumericalUtils.doublesToLtVector3d(s.pos_x,s.pos_y,s.pos_z),NumericalUtils.doublesToLtVector3d(p.X,p.Y,p.Z));Assert.Equal(StringUtils.hexStringToBytes(s.quat),StringUtils.hexStringToBytes(p.Quaternion));}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)][InlineData(13)][InlineData(14)][InlineData(15)][InlineData(16)][InlineData(17)][InlineData(18)][InlineData(19)][InlineData(20)][InlineData(21)][InlineData(22)][InlineData(23)]
    public void AuthorityAndExactSourceGuardsFailClosed(int which)
    {
        if(which==0)Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","0");
        if(which==1){Store.config.dbParams.DatabaseName="reality_hd";Environment.SetEnvironmentVariable("MXO_DB_NAME","mxotest_fake");}
        if(which==2)Store.config.dbParams.DatabaseName="mxotest_bad-name";
        if(which==3)client.playerData.setCharID(2);if(which==4)client.playerData.setCharID(111);
        if(which==5)client.playerInstance.CharacterName.setValue(new byte[32]);if(which==6)client.Alive=false;
        if(which==7)client.playerData.setOnWorld(false);if(which==8)client.playerData.worldChangePending=true;
        if(which==9)client.playerData.isDead=true;if(which==10)client.playerData.duelDeath=true;
        if(which==11)client.playerData.setDistrictId(2);if(which==12)client.playerData.setUDPSessionEstablished(false);
        if(which==13)client.playerData.missionProgress=null;if(which==14)client.playerData.activeMission=new Mission{id=3072};
        if(which==15)progress.phaseIndex=1;if(which==16)progress.failed=true;if(which==17)state.def.id=2;
        if(which==18)state.goid=4;if(which==19)progress.objects.Clear();if(which==20)state.spot.mxoStaticId++;
        if(which==21)state.spot.pos_y=4325;if(which==22)state.spot.type=new byte[]{0};if(which==23)state.spot.quat=MissionObjectPoseProbe.IdentityQuaternion;
        AssertDefault();
    }
    [Theory][InlineData("control")][InlineData("native-y")][InlineData("full-native")]
    public void ModesOnlyEnableExactDiagnosticAndExistingLayoutSeed(string variant)
    {
        var p=NativeQaModes.Find("mission-object-pose-"+variant).Plan;Assert.True(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Equal(variant,p.EnvironmentOverrides[MissionObjectPoseProbe.Variable]);Assert.Equal("3072",p.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal(2,p.EnvironmentOverrides.Count);Assert.Empty(p.BeforeLoadout);Assert.Empty(p.AfterLoadout);
    }
}
