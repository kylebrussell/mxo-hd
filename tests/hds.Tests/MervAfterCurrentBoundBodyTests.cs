using HeadlessClient;
using hds;
using hds.shared;
using hds.resources.gameobjects;
using System.IO.Compression;
using System.Text.Json;
namespace hds.Tests;
public sealed class MervAfterCurrentBoundBodyTests
{
    const string FailedBirth="01000846018B01B04A27CDAB04C60100000000F30435BF00000000F304353F00000000C032EC400000000000409A40000000000075CC4004FF310002290000";
    const uint Source=0x4ab0018b;
    static MissionProgress.SavedState State()=>new(){Version=1,Mission=301210,Phase=1,Instance=73,Identity=new string('a',32),Started=100,Flags=new(){["entered"]=true},Objects=new(){new(){Id=1,Goid=326,Spot=new(){metrId=1,sectorID=1195,staticId=395,rot=-1,mxoStaticId=Source,type=BitConverter.GetBytes((ushort)326),pos_x=57230,pos_y=1745,pos_z=14050,quat="0000000000000000000000000000803f"}}}};
    static byte[] Own(){var o=new PlayerCharacter();o.UnsetRelatedStaticObjId();o.DisableAllAttributes();return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,1).getBytes()).Concat(new byte[]{2,0,0}).ToArray();}
    static byte[] Birth(byte spawn=4,ushort view=9,bool bound=true)
    {
        var field=typeof(GameObjectDefinitions).GetField("Object326")!;var o=(GameObject)Activator.CreateInstance(field.FieldType,field.Name,(ushort)326)!;
        if(bound)o.SetRelatedStaticObjId(Source);else o.UnsetRelatedStaticObjId();o.DisableAllAttributes();
        void Set(string name,byte[] b){var a=(hds.Attribute)o.GetType().GetField(name)!.GetValue(o)!;a.enable();a.setValue(b);}
        Set("Position",new[]{57750d,1680d,14570d}.SelectMany(BitConverter.GetBytes).ToArray());Set("Orientation",Convert.FromHexString("00000000F30435BF00000000F304353F"));Set("MissionKey",new byte[]{255,73,0,2});
        return new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(o,0,spawn).getBytes()).Concat(BitConverter.GetBytes(view)).Concat(new byte[]{0}).ToArray();
    }
    static byte[] Delete(ushort view=9)=>new byte[]{1,0,1,1,0}.Concat(BitConverter.GetBytes(view)).ToArray();
    static OwnViewLifeSnapshot Stream(params byte[][] sections){var log=new OwnViewLifeLog();for(int i=0;i<sections.Length;i++)log.Observe(false,sections[i],i,WireFormat.FindCreations(sections[i]));return log.Snapshot();}
    static MervAfterCurrentBoundBody.Result Observe(params byte[][] sections){var s=State();return MervAfterCurrentBoundBody.Require(Stream(sections),MervAfterCurrentBoundBody.Capture(s,1),s);}
    [Fact]public void Actual326SerializerHeaderRequiresItsExactNativeBindingPoseAndInstance()
    {
        var birth=Birth();Assert.Equal((byte)8,birth[2]);var result=Observe(Own(),birth);
        Assert.Equal((ushort)326,result.View.Goid);Assert.Equal(Source,result.StaticBinding);Assert.Equal("FF490002",Convert.ToHexString(result.View.Attributes["MissionKey"]));
        Assert.Equal((byte)4,result.View.SpawnId);Assert.Equal((ushort)9,result.View.ViewId);
    }
    [Fact]public void DeletedReusedAndAmbiguousBindingsCannotAdoptHistoricalBirth()
    {
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Delete()));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Delete(),Birth()));
        Assert.Equal((byte)5,Observe(Own(),Birth(),Delete(),Birth(5)).View.SpawnId);
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),Birth(5,10)));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(bound:false)));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),new byte[]{9,0,2,255}));
    }
    [Fact]public void ExactOwnTeleportAndKnownStateOnlyUpdatesDoNotReplaceNativePose()
    {
        var tp=PacketsUtils.createTeleportPacket(100,295,300,209,1,1,9,0);
        Assert.Equal((ushort)9,Observe(Own(),Birth(),tp).View.ViewId);
        var update=BitConverter.GetBytes((ushort)9).Concat(ObjectStates.BuildStateUpdate(326,123)).ToArray();
        Assert.Equal((ushort)9,Observe(Own(),Birth(),update).View.ViewId);
    }
    [Fact]public void ResetAndChangedOwnedSourceMissionScopeInvalidateTheCapability()
    {
        var state=State();var scope=MervAfterCurrentBoundBody.Capture(state,1);var log=new OwnViewLifeLog();var own=Own();var birth=Birth();
        log.Observe(false,own,0,WireFormat.FindCreations(own));log.Observe(false,birth,1,WireFormat.FindCreations(birth));var old=log.Snapshot();
        Assert.Equal(Source,MervAfterCurrentBoundBody.Require(old,scope,state).StaticBinding);
        state.Phase++;Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(old,scope,state));state.Phase--;
        state.Objects[0].Spot.pos_y++;Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(old,scope,state));state.Objects[0].Spot.pos_y--;
        log.Observe(true,null,2,Array.Empty<ViewCreation>());Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(old,scope,state));
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(log.Snapshot(),scope,state));
    }
    [Fact]public void Captured326UsesAuditedNativeMidpointWithoutOverwritingItsRawSource()
    {
        var state=State();state.Instance=49;
        string before=JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true});
        string root=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root)!;
        using var file=File.OpenRead(Path.Combine(root,"data/mission_bound_poses/1/1195.json.gz"));
        using var gzip=new GZipStream(file,CompressionMode.Decompress);using var profile=JsonDocument.Parse(gzip);
        var row=profile.RootElement.GetProperty("Rows").EnumerateArray().Single(r=>r.GetProperty("NativeKey").GetUInt32()==Source);
        Assert.Equal("884d65c88bf68b43c176dcfb996501d034d82bf23f1ef1359446477c67bff5a1",row.GetProperty("SourceSha256").GetString());
        Assert.Equal(new[]{57230d,1745,14050},row.GetProperty("LegacyPosition").EnumerateArray().Select(v=>v.GetDouble()));
        var minimum=row.GetProperty("Minimum").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
        var maximum=row.GetProperty("Maximum").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
        Assert.Equal(new[]{57750d,1680,14570},minimum.Zip(maximum,(lo,hi)=>(double)((float)lo+(float)hi)*.5f));
        var scope=MervAfterCurrentBoundBody.Capture(state,1);
        var result=MervAfterCurrentBoundBody.Require(Stream(Own(),Convert.FromHexString(FailedBirth)),scope,state);
        Assert.Equal((ushort)41,result.View.ViewId);Assert.Equal((byte)39,result.View.SpawnId);
        Assert.Equal(new[]{57750d,1680,14570}.SelectMany(BitConverter.GetBytes),result.View.Attributes["Position"]);
        Assert.Equal(Convert.FromHexString("00000000F30435BF00000000F304353F"),result.View.Attributes["Orientation"]);
        Assert.Equal(before,JsonSerializer.Serialize(state,new JsonSerializerOptions{IncludeFields=true}));
    }
    [Theory][InlineData("raw-pose")][InlineData("binding")][InlineData("quaternion")][InlineData("mission-key")]
    public void CapturedNativePresentationRejectsRawMarkerImpostorOrWrongIdentity(string fault)
    {
        var state=State();state.Instance=49;byte[] birth=Convert.FromHexString(FailedBirth);
        if(fault=="raw-pose")new[]{57230d,1745,14050}.SelectMany(BitConverter.GetBytes).ToArray().CopyTo(birth,31);
        if(fault=="binding")birth[5]^=1;
        if(fault=="quaternion")birth[15]^=1;
        if(fault=="mission-key")birth[^6]^=1;
        Assert.Throws<InvalidDataException>(()=>MervAfterCurrentBoundBody.Require(Stream(Own(),birth),MervAfterCurrentBoundBody.Capture(state,1),state));
    }
    [Fact]public void TypedShotTargetSpawnAliasDoesNotPoisonBoundViewOrAdoptShooterPosition()
    {
        var shot=Convert.FromHexString("0200032802010c3881001d001b0000e24c4700f0344500da2c471127010080843280808080030000100021");
        Assert.Equal((ushort)27,BitConverter.ToUInt16(shot,12));
        Assert.Equal((ushort)27,Observe(Own(),Birth(view:27),shot).View.ViewId);
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(view:27),shot.Take(shot.Length-1).ToArray()));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),new byte[]{99,88},Birth()));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),new byte[]{99,88},Birth(5)));
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),new byte[]{99,88}.Concat(shot).ToArray()));
    }
    [Fact]public void DuplicateOwnBirthAndFramedOwnModeTeleportAndShotRetainTheSelectedCabinet()
    {
        var mode=Convert.FromHexString("020002802001000002");
        var teleport=PacketsUtils.createTeleportPacket(100,295,300,209,1,1,9,0);
        var shot=Convert.FromHexString("0200032802010c3881001d001b0000e24c4700f0344500da2c471127010080843280808080030000100021");
        var result=Observe(Own(),Birth(),Own(),mode,teleport,shot);
        Assert.Equal(Source,result.StaticBinding);Assert.Equal((ushort)9,result.View.ViewId);
        Assert.Equal(new[]{57750d,1680,14570}.SelectMany(BitConverter.GetBytes),result.View.Attributes["Position"]);
    }
    [Fact]public void SelectedBoundMovementStillRefusesAndCannotReplaceItsFixedPresentation()
    {
        byte[] move=Convert.FromHexString("0900020E00000000803F0000004000004040");
        Assert.Equal(18,move.Length);
        Assert.Throws<InvalidDataException>(()=>Observe(Own(),Birth(),move));
    }
}
