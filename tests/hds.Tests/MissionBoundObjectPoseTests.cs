using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using hds;
using hds.resources.gameobjects;
using HeadlessClient;

namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionBoundObjectPoseTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static StaticWorldObject Spot()=>new(){metrId=1,sectorID=209,staticId=418,mxoStaticId=0x0d1001a2,
        type=new byte[]{0x46,1},rot=90,pos_x=30765,pos_y=1347,pos_z=14160,quat="0000000000000000000000000000803f"};
    private static JsonObject Profile()
    {
        using var file=File.OpenRead(Path.Combine(Root,"data/mission_bound_poses/1/209.json.gz"));using var gz=new GZipStream(file,CompressionMode.Decompress);
        var node=JsonNode.Parse(gz)!.AsObject();var row=node["Rows"]!.AsArray().Single(n=>n!["Static"]!.GetValue<int>()==418)!.DeepClone();
        node["Rows"]=new JsonArray(row);return node;
    }
    private static MissionBoundObjectPoses Read(JsonObject? profile=null)
    {using var doc=JsonDocument.Parse((profile??Profile()).ToJsonString());return MissionBoundObjectPoses.Read(doc.RootElement,209);}
    private static Mission Definition(bool two=false)
    {
        var xml=new XmlDocument();xml.LoadXml("<mission id='9203' title='Bound' description='D'><phase><object id='1' kind='Folder'/>"+
            (two?"<object id='2' kind='Folder'/>":"")+"<objective goal='1' text='Search' type='Search' object='1' item='6795'/></phase></mission>");
        return MissionXml.Parse(xml.DocumentElement!);
    }
    private static MissionProgress Progress(Mission mission)
    {var p=new MissionProgress(mission);p.objects[1]=new MissionObjectState{def=mission.phases[0].objects[0],spot=Spot(),goid=326};p.itemSlots[6796]=7;return p;}

    [Fact] public void ExactOriginal326ResolvesWithoutMutatingRawAndPoseCannotBeMutatedByCaller()
    {
        var raw=Spot();string before=JsonSerializer.Serialize(raw,MissionProgress.SaveJson);
        Assert.True(Read().TryResolve(raw,out var pose,out int region,out _));
        Assert.Equal((28040d,1282d,11435d),(pose.X,pose.Y,pose.Z));Assert.Equal(47,region);
        Assert.Equal("00000000F304353F00000000F304353F",Convert.ToHexString(pose.Orientation));
        var copy=pose.Orientation;copy[0]=99;Assert.Equal(0,pose.Orientation[0]);
        Assert.Equal(before,JsonSerializer.Serialize(raw,MissionProgress.SaveJson));
        raw.pos_x++;Assert.False(Read().TryResolve(raw,out _,out _,out string why));Assert.Equal("BoundPoseRawSignature",why);
    }
    [Fact] public void NativeMidpointStoreIsNotDoubleAverage()
    {
        double lo=BitConverter.Int32BitsToSingle(0x3f800001),hi=BitConverter.Int32BitsToSingle(0x3f800002);
        Assert.Equal(hi,MissionBoundObjectPoses.Midpoint(lo,hi));Assert.NotEqual((lo+hi)*.5,MissionBoundObjectPoses.Midpoint(lo,hi));
    }
    [Theory][InlineData("valid")][InlineData("missing-manifest")][InlineData("manifest-world")][InlineData("manifest-format")]
    [InlineData("filename")][InlineData("sector")][InlineData("rows")][InlineData("duplicate")][InlineData("missing-payload")]
    [InlineData("hash")][InlineData("coherent-payload-edit")][InlineData("truncated-with-declared-hash")]
    public void RuntimeLoaderRequiresDeclaredAuditedCompressedPayload(string fault)
    {
        string directory=Path.Combine(Path.GetTempPath(),"mxo-bound-profile-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var profile=Profile();byte[] Compress()
            {using var output=new MemoryStream();using(var gz=new GZipStream(output,CompressionLevel.SmallestSize,true))JsonSerializer.Serialize(gz,profile);return output.ToArray();}
            byte[] payload=Compress();string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));
            var file=new JsonObject{["Sector"]=209,["File"]="209.json.gz",["Rows"]=1,["Sha256"]=hash};
            var manifest=new JsonObject{["Format"]=1,["World"]=1,["Goid"]=326,["Files"]=new JsonArray(file)};
            switch(fault)
            {
                case "manifest-world":manifest["World"]=2;break;
                case "manifest-format":manifest["Format"]=2;break;
                case "filename":file["File"]="../209.json.gz";break;
                case "sector":file["Sector"]=210;break;
                case "rows":file["Rows"]=2;break;
                case "duplicate":manifest["Files"]!.AsArray().Add(file.DeepClone());break;
                case "hash":file["Sha256"]=new string('a',64);break;
                case "coherent-payload-edit":
                    var row=profile["Rows"]![0]!;row["Minimum"]![1]=1212;row["Maximum"]![1]=1372;row["Centre"]![1]=1292;payload=Compress();break;
                case "truncated-with-declared-hash":payload=payload[..(payload.Length/2)];file["Sha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload));break;
            }
            if(fault!="missing-manifest")File.WriteAllText(Path.Combine(directory,"manifest.json"),manifest.ToJsonString());
            if(fault!="missing-payload")File.WriteAllBytes(Path.Combine(directory,"209.json.gz"),payload);
            if(fault=="valid")Assert.Equal(1,MissionBoundObjectPoses.LoadVerified(directory,209).Count);
            else
            {
                var error=Record.Exception(()=>MissionBoundObjectPoses.LoadVerified(directory,209));
                Assert.NotNull(error);Assert.True(error is InvalidDataException or IOException or JsonException,"Only explicit malformed/unavailable data refusal is expected: "+error);
            }
        }
        finally {Directory.Delete(directory,true);}
    }
    [Fact] public void EveryCommittedSectorProfileMatchesItsManifestAndNativeMidpointSchema()
    {
        string directory=Path.Combine(Root,"data/mission_bound_poses/1");using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"manifest.json")));
        int total=0;
        foreach(var file in manifest.RootElement.GetProperty("Files").EnumerateArray())
        {
            byte[] bytes=File.ReadAllBytes(Path.Combine(directory,file.GetProperty("File").GetString()!));
            Assert.Equal(file.GetProperty("Sha256").GetString(),Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant());
            using var input=new MemoryStream(bytes);using var gz=new GZipStream(input,CompressionMode.Decompress);
            var profile=MissionBoundObjectPoses.Read(gz,file.GetProperty("Sector").GetUInt16());Assert.Equal(file.GetProperty("Rows").GetInt32(),profile.Count);total+=profile.Count;
        }
        Assert.Equal(32305,total);
    }
    [Theory][InlineData("centre")][InlineData("bounds")][InlineData("hash")][InlineData("region")][InlineData("goid")][InlineData("duplicate")]
    public void CorruptOrAmbiguousProfileCannotBecomeLegacyFallback(string corruption)
    {
        var profile=Profile();var rows=profile["Rows"]!.AsArray();var row=rows[0]!.AsObject();
        switch(corruption)
        {
            case "centre":row["Centre"]![1]=1202;break;
            case "bounds":row["Minimum"]![1]=2000;break;
            case "hash":row["RecordSha256"]="";break;
            case "region":row["Region"]=-1;break;
            case "goid":row["Goid"]=325;break;
            case "duplicate":rows.Add(row.DeepClone());break;
        }
        Assert.Throws<InvalidDataException>(()=>Read(profile));
    }
    [Fact] public void MissingWrongIdentityAndMovedSupportedSourcesExplicitlyRefuse()
    {
        var s=Spot();s.staticId=419;s.mxoStaticId=0x0d1001a3;
        Assert.False(Read().TryResolve(s,out _,out _,out string missing));Assert.Equal("MissingOrAmbiguousBoundPose",missing);
        s=Spot();s.type=new byte[]{0x47,1};Assert.False(Read().TryResolve(s,out _,out _,out _));
        s=Spot();s.mxoStaticId++;Assert.False(Read().TryResolve(s,out _,out _,out _));
        s=Spot();s.quat="bad";Assert.False(Read().TryResolve(s,out _,out _,out _));
        var state=new MissionObjectState{goid=326,spot=Spot()};state.spot.sectorID=2047;state.spot.mxoStaticId=(2047u<<20)|418;
        Assert.False(MissionBoundObjectPoses.TryCurrent(state,out _,out string why));Assert.Equal("UnavailableOrInvalidBound326Profile",why);
    }
    [Theory][InlineData(1)][InlineData(2)]
    public void RestoredRawIdentityAndItemOwnershipRemainExactAndUnknownRecordsQuarantine(int version)
    {
        var mission=Definition();var original=Progress(mission);var saved=original.Capture(48,Array.Empty<Mob>());
        if(version==2){saved.Version=2;saved.OpenFloorSites=Array.Empty<MissionOpenFloorSite>();saved.OpenFloorCurrentSiteIndex=-1;}
        string payload=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);
        var restored=MissionPersistence.RestoreForWorld(mission,saved);Assert.Null(restored.restoreRefusal);
        Assert.Equal(original.persistenceId,restored.persistenceId);Assert.Equal(original.startedAt,restored.startedAt);Assert.Equal((ushort)7,restored.itemSlots[6796]);
        Assert.Equal((28040d,1282d,11435d),Pose(MissionObjectPoses.For(restored.objects[1])));
        Assert.Equal(payload,JsonSerializer.Serialize(saved,MissionProgress.SaveJson));
        saved.Objects[0].Spot.pos_x++;string bad=JsonSerializer.Serialize(saved,MissionProgress.SaveJson);
        var blocked=MissionPersistence.RestoreForWorld(mission,saved);Assert.Contains("Bound326 restore pose refused",blocked.restoreRefusal);
        Assert.Equal((ushort)7,blocked.itemSlots[6796]);Assert.Equal(original.persistenceId,blocked.persistenceId);
        Assert.Equal(bad,JsonSerializer.Serialize(saved,MissionProgress.SaveJson));
    }
    private static (double,double,double) Pose(MissionComputerAnchors.Pose p)=>(p.X,p.Y,p.Z);
    [Fact] public void SameBoundIdentityDrivesActualSpawnAttributesAndObjectRelativeRange()
    {
        var mission=Definition();var p=Progress(mission);var state=p.objects[1];
        var obj=new AttributeClass46("Object326",326);MissionStaging.ConfigureSourceBinding(obj,state);MissionStaging.ApplyObjectPose(obj,state);
        Assert.Equal(0x0d1001a2u,obj.GetRelatedStaticObjId());
        Assert.Equal(new[]{28040d,1282d,11435d},Enumerable.Range(0,3).Select(i=>BitConverter.ToDouble(obj.Position.getValue(),i*8)));
        Assert.Equal(MissionObjectPoses.For(state).Orientation,obj.Orientation.getValue());
        obj.DisableAllAttributes();MissionStaging.ApplyObjectPose(obj,state);
        byte[] section=new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,7,2).getBytes()).Concat(new byte[]{7,0,0}).ToArray();
        var view=WireFormat.FindCreations(section).Single();Assert.Equal((ushort)7,view.ViewId);Assert.Equal((byte)2,view.SpawnId);
        Assert.Equal(Convert.ToHexString(section),MissionBoundCabinetFixture.RequireBoundRecord(new[]{section},view));
        obj.UnsetRelatedStaticObjId();byte[] free=new byte[]{1,0}.Concat(new ObjectManager().GenerateCreationPacket(obj,7,2).getBytes()).Concat(new byte[]{7,0,0}).ToArray();
        Assert.Throws<ScenarioFailure>(()=>MissionBoundCabinetFixture.RequireBoundRecord(new[]{free},view));
        var near=typeof(MissionRuntime).GetMethod("Near",BindingFlags.Static|BindingFlags.NonPublic,null,
            new[]{typeof(WorldClient),typeof(MissionProgress),typeof(string),typeof(double),typeof(double),typeof(double),typeof(double)},null)!;
        Assert.True((bool)near.Invoke(null,new object[]{null!,p,"object:1",600d,28160d,1295d,11435d})!);
        Assert.False((bool)near.Invoke(null,new object[]{null!,p,"object:1",600d,30765d,1347d,14160d})!);
    }
    [Fact] public void ExactMeasuredApproachIsGroundedAndPassesUnchangedNativeAndServerRanges()
    {
        SectorProperties.Load(Path.Combine(Root,"data/sector_properties.csv"));NavMesh.UseDirectory(Path.Combine(Root,"data/navmesh"));
        Assert.True(NavMesh.Locate(1,28160,1295,11435,out var nav));Assert.Equal((ushort)209,nav.sector);Assert.Equal((ushort)47,nav.region);Assert.Equal((ushort)321,nav.polygon);Assert.Equal(1200,nav.floorY);
        Assert.True(ContainerActions.InReach(new[]{28160d,1295d,11435d}.SelectMany(BitConverter.GetBytes).ToArray(),28040,1282,11435));
        var range=new MissionOpenFloorUsableRange.Profile(326,3,100,new OpenFloorPoint(25,80,30),"OriginalGO326 OpenUse100/ModelHalfExtents25,80,30");
        Assert.True(MissionOpenFloorUsableRange.Test(range,new OpenFloorPoint(28160,1295,11435),new OpenFloorPoint(28040,1282,11435),new OpenFloorOrientation(MissionBoundCabinetFixture.Quaternion)).Within);
        Assert.False(ContainerActions.InReach(new[]{28160d,1295d,11435d}.SelectMany(BitConverter.GetBytes).ToArray(),30765,1347,14160));
    }
    private sealed class StableRandom:Random {public override int Next()=>0;}
    [Fact] public void WholeChosenBoundSetIsValidatedBeforeStagingAndBufferedSuccessIsDiscarded()
    {
        var mission=Definition(true);var p=Progress(mission);p.objects.Clear();p.site=new MissionSite();
        var bad=Spot();bad.pos_x++;p.site.containers.Add(Spot());p.site.containers.Add(bad);
        var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData=new();client.messageQueue=new();var before=JsonSerializer.Serialize(p.Capture(48,Array.Empty<Mob>()),MissionProgress.SaveJson);
        using(MissionOutput.Begin(client.messageQueue))
        {
            client.messageQueue.addRpcMessage(new byte[]{0x80,0xa0}); // earlier staging output remains unpublished
            Assert.Throws<MissionCheckpointException>(()=>MissionStaging.PrepareObjectChoices(p.Phase,p.site,new StableRandom()));
            Assert.Empty(p.objects);
            Assert.Empty(client.messageQueue.RPCMessagesQueue);Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        }
        Assert.Empty(client.messageQueue.RPCMessagesQueue);Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        Assert.Equal(before,JsonSerializer.Serialize(p.Capture(48,Array.Empty<Mob>()),MissionProgress.SaveJson));
    }
    [Fact] public void OrdinaryStageRefusesInvalidChosenRecordBeforeSiteHistoryNpcOrObjectPublication()
    {
        var mission=Definition(true);mission.phases[0].site="Any";
        mission.npcs.Add(new MissionNpc{id=1,at=1,handle="MustNotPublish"});
        var p=Progress(mission);p.objects.Clear();
        var site=new MissionSite();var bad=Spot();bad.pos_x++;
        site.containers.Add(Spot());site.containers.Add(bad);site.npcSpots.Add(new[]{28040d,1295d,11435d,0d});
        var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData=new();client.playerInstance=new PlayerCharacter();client.messageQueue=new();
        client.playerData.missionPreview=site;client.playerData.missionPreviewFor=mission;
        var before=JsonSerializer.Serialize(p.Capture(48,Array.Empty<Mob>()),MissionProgress.SaveJson);
        var catalog=(Dictionary<ushort,List<MissionSite>>)typeof(MissionSites).GetField("byDistrict",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        var gate=typeof(MissionSites).GetField("buildLock",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
        lock(gate)
        {
        bool existed=catalog.TryGetValue(mission.district,out var previous);catalog[mission.district]=new(){site};
        try
        {
        using(MissionOutput.Begin(client.messageQueue))
        {
            Assert.Throws<MissionCheckpointException>(()=>MissionStaging.Stage(client,p));
            Assert.Null(p.site);Assert.Empty(p.sites);Assert.Empty(p.objects);Assert.Empty(p.hostiles);
            Assert.Empty(client.playerData.missionMobs);
            Assert.Empty(client.messageQueue.RPCMessagesQueue);Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        }
        Assert.Equal(before,JsonSerializer.Serialize(p.Capture(48,Array.Empty<Mob>()),MissionProgress.SaveJson));
        Assert.Empty(client.messageQueue.RPCMessagesQueue);Assert.Empty(client.messageQueue.ObjectMessagesQueue);
        }
        finally {if(existed)catalog[mission.district]=previous!;else catalog.Remove(mission.district);}
        }
    }
    [Fact] public void UnrelatedAndExplicitFreeCustom326RetainExistingPolicy()
    {
        var state=new MissionObjectState{goid=326,spot=new StaticWorldObject{pos_x=1,pos_y=2,pos_z=3,quat="0000000000000000000000000000803f",type=new byte[4]}};
        Assert.False(MissionBoundObjectPoses.Supported(state));Assert.Equal((1d,2d,3d),Pose(MissionObjectPoses.For(state)));
        state.spot=Spot();state.goid=325;Assert.False(MissionBoundObjectPoses.Supported(state));Assert.Equal((30765d,1347d,14160d),Pose(MissionObjectPoses.For(state)));
    }
}
