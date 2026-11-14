using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using hds;
using hds.shared;
namespace hds.Tests;
[Collection("ConcentrationEnvironment")]
public class PosterBoxPoseProbeTests:IDisposable
{
    private readonly ServerConfig old=Store.config;
    private readonly string? oldDev=Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),oldProbe=Environment.GetEnvironmentVariable(PosterBoxPoseProbe.Variable),oldFake=Environment.GetEnvironmentVariable("MXO_DB_NAME");
    private readonly WorldClient client;private readonly MissionProgress progress;private readonly MissionObjectState state;
    public PosterBoxPoseProbeTests()
    {
        Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","1");Environment.SetEnvironmentVariable(PosterBoxPoseProbe.Variable,"source-native");
        Store.config=new ServerConfig("unused");typeof(ServerConfig).GetField("_dbParams",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(Store.config,new DbParams{DatabaseName="mxotest_poster_pose"});
        client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));client.playerData=new();client.playerInstance=new();client.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(client,TimeUtils.getUnixTimeUint32());
        client.playerData.setCharID(110);client.playerData.setDistrictId(1);client.playerData.setOnWorld(true);client.playerData.setUDPSessionEstablished(true);
        var name=new byte[32];Encoding.ASCII.GetBytes("CSR_Tester").CopyTo(name,0);client.playerInstance.CharacterName.setValue(name);
        var mission=new Mission{id=201202};progress=new MissionProgress(mission);client.playerData.activeMission=mission;client.playerData.missionProgress=progress;
        state=new MissionObjectState{def=new MissionObjectDef{id=1,kind="Box"},goid=548,spot=new StaticWorldObject{metrId=1,sectorID=209,staticId=988,mxoStaticId=0x0d1003dc,type=new byte[]{0x87,5},pos_x=23940,pos_y=2245,pos_z=15050,quat=PosterBoxPoseProbe.LegacyQuaternion}};progress.objects[1]=state;
    }
    public void Dispose(){Store.config=old;Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS",oldDev);Environment.SetEnvironmentVariable(PosterBoxPoseProbe.Variable,oldProbe);Environment.SetEnvironmentVariable("MXO_DB_NAME",oldFake);}
    private MissionComputerAnchors.Pose Base()=>MissionComputerAnchors.For(state);
    private void Unchanged(){var normal=Base();Assert.Same(normal,PosterBoxPoseProbe.SerializedPose(client,progress,state,normal));}
    [Theory][InlineData(null)][InlineData("")][InlineData("SOURCE-NATIVE")][InlineData("source-native ")][InlineData("native-y")]
    public void UnsetAndUnknownVariantsKeepExactNormalPoseObject(string? variant)
    {Environment.SetEnvironmentVariable(PosterBoxPoseProbe.Variable,variant);Unchanged();}
    [Theory][InlineData("control")][InlineData("source-native")]
    public void QualifiedVariantsChangeOnlyTheDerivedSerializationPose(string variant)
    {
        Environment.SetEnvironmentVariable(PosterBoxPoseProbe.Variable,variant);var options=new JsonSerializerOptions{IncludeFields=true};string before=JsonSerializer.Serialize(state,options);var normal=Base();
        var pose=PosterBoxPoseProbe.SerializedPose(client,progress,state,normal);
        Assert.Equal((23940d,variant=="control"?2245d:2100d,15050d),(pose.X,pose.Y,pose.Z));
        Assert.Equal(Convert.FromHexString(variant=="control"?PosterBoxPoseProbe.LegacyQuaternion:PosterBoxPoseProbe.NativeQuaternion),pose.Orientation);
        Assert.Equal(before,JsonSerializer.Serialize(state,options));Assert.Equal((ushort)548,state.goid);Assert.Equal(2245,state.spot.pos_y);
        if(variant=="control")Assert.Same(normal,pose);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)][InlineData(13)][InlineData(14)][InlineData(15)][InlineData(16)][InlineData(17)][InlineData(18)][InlineData(19)][InlineData(20)][InlineData(21)][InlineData(22)][InlineData(23)][InlineData(24)][InlineData(25)][InlineData(26)][InlineData(27)][InlineData(28)][InlineData(29)][InlineData(30)][InlineData(31)]
    public void ActualAuthorityOwnershipAndKnownSourceGuardsFailClosed(int which)
    {
        if(which==0)Environment.SetEnvironmentVariable("MXO_DEV_COMMANDS","0");
        if(which==1){Store.config.dbParams.DatabaseName="reality_hd";Environment.SetEnvironmentVariable("MXO_DB_NAME","mxotest_fake");}
        if(which==2)Store.config.dbParams.DatabaseName="mxotest_bad-name";if(which==3)Store.config.dbParams.DatabaseName="mxotest_";
        if(which==4)client.playerData.setCharID(111);if(which==5)client.playerInstance.CharacterName.setValue(new byte[32]);if(which==6)client.Alive=false;
        if(which==7)client.playerData.setOnWorld(false);if(which==8)client.playerData.worldChangePending=true;if(which==9)client.playerData.isDead=true;if(which==10)client.playerData.duelDeath=true;
        if(which==11)client.playerData.setDistrictId(2);if(which==12)client.playerData.setUDPSessionEstablished(false);if(which==13)client.playerData.missionProgress=null;
        if(which==14)client.playerData.activeMission=new Mission{id=201202};if(which==15)progress.phaseIndex=1;if(which==16)progress.failed=true;
        if(which==17)state.def.id=2;if(which==18)state.def.kind="Computer";if(which==19)state.goid=3;if(which==20)progress.objects.Clear();
        if(which==21)state.spot.mxoStaticId++;if(which==22)state.spot.pos_y++;if(which==23)state.spot.type=new byte[]{0x87,5,1,0};
        if(which==24)state.spot.quat=PosterBoxPoseProbe.NativeQuaternion;if(which==25)state.spot.exterior=true;if(which==26)state.spot.metrId=2;
        if(which==27)state.spot.sectorID++;if(which==28)state.spot.staticId++;if(which==29)state.spot.type=new byte[]{0};
        if(which==30)progress.mission.id=201201;if(which==31)progress.objects[1]=new MissionObjectState{def=state.def,goid=state.goid,spot=state.spot};
        Unchanged();
    }
    [Fact] public void AChangedFutureNormalPoseIsNeverMasked()
    {var future=new MissionComputerAnchors.Pose(23940,2100,15050,Convert.FromHexString(PosterBoxPoseProbe.NativeQuaternion));Assert.Same(future,PosterBoxPoseProbe.SerializedPose(client,progress,state,future));}
    [Fact] public void IndependentCountedSourceLiteralPreservesUnnormalizedFloat32Quaternion()
    {
        // Counted SIDI36 record1: corner22000/0/10000, pivot200/200/580, quarter-turn2, local60/100/750.
        var pose=PosterBoxPoseProbe.SerializedPose(client,progress,state,Base());Assert.Equal(22000+200*10-60,pose.X);Assert.Equal(200*10+100,pose.Y);Assert.Equal(10000+580*10-750,pose.Z);
        float localY=0.7071067094802856f,localW=0.7071067690849304f;var literal=new[]{0f,localW,0f,-localY}.SelectMany(BitConverter.GetBytes).ToArray();Assert.Equal(literal,pose.Orientation);
    }
    [Fact] public void GroundedApproachMatchesCompiledNavAndSameFloor()
    {
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root,"data","sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(root,"data","navmesh"));
        Assert.True(NavMesh.Locate(1,24080,2095,15080,out var nav));Assert.Equal(2000,nav.floorY,2);
        Assert.True(NavMesh.Project(1,24080,2095,15080,out var p,reachableFrom:new[]{24080d,2095d,15080d},requireDirect:true));Assert.Equal(24080,p.x,2);Assert.Equal(2095,p.y,2);Assert.Equal(15080,p.z,2);
    }
    [Theory][InlineData("poster-box-pose-control","control")][InlineData("poster-box-pose-native","source-native")]
    public void DiscoveredModesOnlyOptInToTwoScopedControls(string name,string variant)
    {
        var plan=NativeQa.NativeQaModes.Find(name).Plan;Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(variant,plan.EnvironmentOverrides[PosterBoxPoseProbe.Variable]);Assert.Equal("201202",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal(2,plan.EnvironmentOverrides.Count);
        Assert.Equal(HeadlessClient.PosterHuntFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);Assert.DoesNotContain(201202,HeadlessClient.PosterHuntFixture.Prerequisites);
        Assert.Equal("",NativeQa.NativeQaFixturePlan.EnvironmentFor("poster-hunt-count")[PosterBoxPoseProbe.Variable]);
    }
}
