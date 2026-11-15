using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using hds;
using E=hds.MissionInteriorEvidence;
using S=hds.MissionStreetAccessData;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;

public sealed class MissionStreetAccessTests
{
    static string Repo{get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static string DirectoryFor(string name)=>Path.Combine(Repo,"data",name,"1");
    static (E Evidence,S Outdoor,JsonObject Raw,JsonElement Interior) Load(ushort sector)
    {
        E evidence=E.LoadActive(DirectoryFor("mission_interior_evidence_v2"),1,sector);
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(DirectoryFor("mission_street_access"),"manifest.json")));
        var row=manifest.RootElement.GetProperty("Sectors").EnumerateArray().Single(r=>r.GetProperty("Sector").GetUInt16()==sector);
        byte[] packed=File.ReadAllBytes(Path.Combine(DirectoryFor("mission_street_access"),sector+".json.gz"));
        var outside=S.Read(packed,row.GetProperty("Sha256").GetString()!,row.GetProperty("Digest").GetString()!,evidence);
        byte[] Decode(byte[] value){using var input=new MemoryStream(value);using var zip=new GZipStream(input,CompressionMode.Decompress);using var output=new MemoryStream();zip.CopyTo(output);return output.ToArray();}
        var raw=JsonNode.Parse(Decode(packed))!.AsObject();using var interior=JsonDocument.Parse(Decode(File.ReadAllBytes(Path.Combine(DirectoryFor("mission_interior_evidence_v2"),sector+".json.gz"))));
        return(evidence,outside,raw,interior.RootElement.Clone());
    }
    static StaticWorldObject Raw(JsonObject document,uint key)
    {
        var row=document["Legacy"]!.AsArray().Single(g=>g!["Key"]!.GetValue<uint>()==key)!["Rows"]![0]!["Signature"]!;
        return new(){metrId=1,sectorID=document["Sector"]!.GetValue<ushort>(),mxoStaticId=key,staticId=key&0xfffff,type=BitConverter.GetBytes((uint)row["Goid"]!.GetValue<ushort>()),
            exterior=row["Exterior"]!.GetValue<bool>(),pos_x=row["Position"]![0]!.GetValue<double>(),pos_y=row["Position"]![1]!.GetValue<double>(),pos_z=row["Position"]![2]!.GetValue<double>(),
            rot=row["Rotation"]!.GetValue<double>(),quat=row["Quaternion"]!.GetValue<string>()};
    }
    static E.Entry Lobby(E evidence,JsonElement interior,uint key,ushort region,ushort polygon,double x,double y,double z)
    {
        var group=interior.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==key);
        var own=group.GetProperty("Occurrences").EnumerateArray().Single(o=>o.GetProperty("Region").GetUInt16()==region);
        var occurrence=JsonSerializer.Deserialize<E.Occurrence>(own.GetRawText())!;
        return new(evidence,new E.DoorPose(key,occurrence),new E.Node(region,polygon),new G.Position(x,y,z));
    }
    [Theory][InlineData((ushort)209)][InlineData((ushort)879)]
    public void OriginalPublicApproachReachesTheCapturedLobbyWithoutSendingFrontDoorToFloor(ushort sector)
    {
        var c=Load(sector);string before=c.Raw.ToJsonString();bool mara=sector==209;uint shaft=mara?0x0d100028u:0x36f00469u;
        // Verbatim Claude261 pre-entry checkpoint; this is an offline diagnostic, not an earned mission replay.
        var saved=JsonSerializer.Deserialize<MissionProgress.SavedState>(File.ReadAllText(Path.Combine(Repo,"tests","hds.Tests","Fixtures","Native261FirstDoorCheckpoint.json")),new JsonSerializerOptions{IncludeFields=true})!;
        var captured=saved.NpcInterior.GetProperty("Witnesses")[0].GetProperty("Lobby");var at=captured.GetProperty("Standing");var node=captured.GetProperty("Node");
        var lobby=mara?Lobby(c.Evidence,c.Interior,shaft,node.GetProperty("Region").GetUInt16(),node.GetProperty("Polygon").GetUInt16(),at[0].GetDouble(),at[1].GetDouble(),at[2].GetDouble()):Lobby(c.Evidence,c.Interior,shaft,35,10,-2600,95,-17300);
        E.Node publicStart=mara?new(13,0):new(14,22);
        Assert.True(c.Evidence.TryStreetAccess(c.Outdoor,lobby,publicStart,shaft,key=>Raw(c.Raw,key),out var proof,out var why),why);
        Assert.Equal("OriginalPublicDoor",proof.Kind);Assert.NotNull(proof.Door);Assert.True(MissionInteriorDoorTypes.IsSupported(proof.Door.Goid));
        Assert.NotEqual(shaft,proof.Door.Key);Assert.DoesNotContain(proof.Door.Goid,new ushort[]{417,419});
        Assert.Equal(publicStart,proof.OutsidePath[0]);Assert.Equal(lobby.Node,proof.InsidePath[^1]);Assert.Equal(c.Evidence.Digest,proof.InteriorDigest);
        Assert.Equal(mara?545:145,proof.Door.Position.Y); // Public ground-floor door, never the mission's upper-floor target.
        if(mara)Assert.NotEqual(MissionSites.Goid(Raw(c.Raw,proof.Door.Key)),proof.Door.Goid); // Captured359/416 aliases must not select the created schema.
        else Assert.Equal((ushort)365,proof.Door.Goid);
        Assert.Equal(before,c.Raw.ToJsonString());Assert.Equal(101213,saved.Mission);Assert.False(saved.Flags["entered"]);Assert.Empty(saved.Items);
    }
    [Theory][InlineData((ushort)209,9)][InlineData((ushort)879,4)]
    public void FreshPublicAccessUsesReciprocalOriginalNetworkRatherThanFixedWorldStart(ushort sector,int expected)
    {
        var c=Load(sector);bool mara=sector==209;uint shaft=mara?0x0d100028u:0x36f00469u;
        Assert.Equal(expected,c.Outdoor.NetworkAnchors().Length);
        var lobby=mara?Lobby(c.Evidence,c.Interior,shaft,49,283,28500,495,14200):Lobby(c.Evidence,c.Interior,shaft,35,10,-2600,95,-17300);
        Assert.True(c.Evidence.TryStreetAccess(c.Outdoor,lobby,shaft,key=>Raw(c.Raw,key),out var proof,out var why),why);
        Assert.Contains(proof.OutsidePath[0],c.Outdoor.NetworkAnchors());Assert.Equal(lobby.Node,proof.InsidePath[^1]);
        c.Raw["Neighbors"]=new JsonArray();var disconnected=Modified(c.Raw,c.Evidence);
        Assert.Empty(disconnected.NetworkAnchors());Assert.False(c.Evidence.TryStreetAccess(disconnected,lobby,shaft,key=>Raw(c.Raw,key),out _,out _));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void MissingOrCorruptNeighborsCannotAuthorizeEdgesOrRejectAnIndependentGoodPublicComponent(bool corrupt)
    {
        var c=Load(209);var neighbors=c.Raw["Neighbors"]!.AsArray();
        foreach(var neighbor in neighbors.Where(n=>n!["Sector"]!.GetValue<int>()!=508))
        {
            if(corrupt)neighbor!["Outdoor"]!["RawSnav"]="00";
            else{neighbor!["Refusal"]="MissingOriginalNeighborNav";neighbor["NavLength"]=0;neighbor["NavSha256"]=null;neighbor["Outdoor"]=null;}
        }
        var partial=Modified(c.Raw,c.Evidence);Assert.Equal(new[]{new E.Node(35,0)},partial.NetworkAnchors());
        var lobby=Lobby(c.Evidence,c.Interior,0x0d100028,49,283,28500,495,14200);
        Assert.True(c.Evidence.TryStreetAccess(partial,lobby,0x0d100028,key=>Raw(c.Raw,key),out var proof,out var why),why);
        Assert.Equal(new E.Node(35,0),proof.OutsidePath[0]);
    }
    [Theory][InlineData((ushort)209,0x0d100003u)][InlineData((ushort)879,0x36f00018u)]
    public void CanonicalPublicDoorKeepsRawIdentityAndUsesItsOwnNativeSchema(ushort sector,uint key)
    {
        var c=Load(sector);var raw=Raw(c.Raw,key);string before=MissionNpcInteriorPlanning.RawJson(raw);
        Assert.True(c.Evidence.TryCanonicalPublicDoor(c.Outdoor,raw,out var pose,out var why),why);
        Assert.Equal(key,pose.Key);Assert.True(MissionInteriorDoorTypes.IsSupported(pose.Goid));
        Assert.Equal(before,MissionNpcInteriorPlanning.RawJson(raw));
        raw.pos_x+=1;Assert.False(c.Evidence.TryCanonicalPublicDoor(c.Outdoor,raw,out _,out _));
        raw=Raw(c.Raw,key);raw.metrId=2;Assert.False(c.Evidence.TryCanonicalPublicDoor(c.Outdoor,raw,out _,out _));
    }
    [Fact]public void ASecondOriginalOccurrenceOfTheStaticKeyCannotBorrowThePublicDoorPose()
    {
        var c=Load(209);uint key=0x0d100003;var other=c.Raw["NativeObjects"]!.AsArray().First(n=>n!["Class"]!.GetValue<int>()==0)!;
        byte[] raw=Convert.FromHexString(other["Raw"]!.GetValue<string>());BitConverter.GetBytes(key&0xfffff).CopyTo(raw,4);
        other["Key"]=key;other["Raw"]=Convert.ToHexString(raw);other["RecordSha256"]=Convert.ToHexString(SHA256.HashData(raw));
        var ambiguous=Modified(c.Raw,c.Evidence);Assert.False(ambiguous.UniqueNativeKey(key));
        Assert.False(c.Evidence.TryCanonicalPublicDoor(ambiguous,Raw(c.Raw,key),out _,out _));
    }
    static WorldClient ActivePublicClient()
    {
        var client=(WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData=new ClientData();client.playerData.setCharID(123);client.playerData.setDistrictId(1);client.playerData.setOnWorld(true);client.messageQueue=new MessageQueue();client.Alive=true;
        typeof(WorldClient).GetField("lastUsedTime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(client,TimeUtils.getUnixTimeUint32());
        typeof(WorldClient).GetField("<TransportFence>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(client,new WorldSendFence());return client;
    }
    [Theory][InlineData("world")][InlineData("life")][InlineData("owner")][InlineData("transport")][InlineData("district")][InlineData("character")][InlineData("dead")][InlineData("pending")]
    public void CapturedPublicRequestCannotPublishAfterItsLazyResolutionOutlivesTheClient(string change)
    {
        var c=Load(209);var raw=Raw(c.Raw,0x0d100003);var client=ActivePublicClient();var request=MissionPublicDoors.CaptureRequest(client);Assert.True(request.Current);
        var result=new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2")).Resolve(raw);
        Assert.Equal(MissionPublicDoors.Kind.Resolved,result.State);
        switch(change){case "world":client.playerData.worldGeneration++;break;case "life":client.playerData.lifeGeneration++;break;
            case "owner":client.playerData=new ClientData();break;case "transport":client.TransportFence.PublishIncoming(true,()=>{});break;
            case "district":client.playerData.setDistrictId(2);break;case "character":client.playerData.setCharID(124);break;
            case "dead":client.playerData.isDead=true;break;case "pending":client.playerData.worldChangePending=true;break;}
        bool published=false;Assert.False(request.Publish(raw,result,()=>published=true));Assert.False(published);
        Assert.Empty(client.messageQueue.ObjectMessagesQueue);Assert.Empty(client.messageQueue.RPCMessagesQueue);
    }
    [Fact]public void PublicOutputUsesExistingBatchLifetimeAndResetCancelsItsUnpublishedMessages()
    {
        var c=Load(209);var raw=Raw(c.Raw,0x0d100003);var result=new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2")).Resolve(raw);
        var client=ActivePublicClient();var request=MissionPublicDoors.CaptureRequest(client);
        using(var output=MissionOutput.Begin(client.messageQueue))
        {
            Assert.True(request.Publish(raw,result,()=>{Assert.True(System.Threading.Monitor.IsEntered(client.playerData.missionSync));client.messageQueue.addObjectMessage(new byte[]{1,2,3},false);client.messageQueue.addRpcMessage(new byte[]{0x80,0xa0,1});}));
            client.TransportFence.PublishIncoming(true,()=>{});output.Commit();
        }
        Assert.Empty(client.messageQueue.ObjectMessagesQueue);Assert.Empty(client.messageQueue.RPCMessagesQueue);
        client=ActivePublicClient();request=MissionPublicDoors.CaptureRequest(client);
        Assert.Throws<MissionCheckpointException>(()=>request.Publish(raw,result,()=>{client.messageQueue.addObjectMessage(new byte[]{1,2,3},false);client.TransportFence.PublishIncoming(true,()=>{});}));
        Assert.Empty(client.messageQueue.ObjectMessagesQueue);Assert.Empty(client.messageQueue.RPCMessagesQueue);
    }
    [Fact]public async Task PublicRequestRejectsOwnerReplacedWhileWaitingForPublicationLock()
    {
        var c=Load(209);var raw=Raw(c.Raw,0x0d100003);var result=new MissionPublicDoors.Dataset(DirectoryFor("mission_street_access"),DirectoryFor("mission_interior_evidence_v2")).Resolve(raw);
        var client=ActivePublicClient();var request=MissionPublicDoors.CaptureRequest(client);var owner=client.playerData;bool published=false;
        using var started=new System.Threading.ManualResetEventSlim();Task<bool> waiting;
        lock(owner.missionSync){waiting=Task.Run(()=>{started.Set();return request.Publish(raw,result,()=>published=true);});Assert.True(started.Wait(TimeSpan.FromSeconds(30)));client.playerData=new ClientData();}
        Assert.False(await waiting.WaitAsync(TimeSpan.FromSeconds(30)));Assert.False(published);
    }
    [Fact]public void DeclaredPublicPayloadMissingTruncatedOrReplacedNeverFallsBackToLegacy()
    {
        var c=Load(209);var raw=Raw(c.Raw,0x0d100003);string temp=Path.Combine(Path.GetTempPath(),"mxo-public-door-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            var missing=new MissionPublicDoors.Dataset(temp,DirectoryFor("mission_interior_evidence_v2"));
            Assert.Equal(MissionPublicDoors.Kind.Refused,missing.Resolve(raw).State);
            var unknown=Raw(c.Raw,0x0d100003);unknown.mxoStaticId=0x0d1fffff;unknown.staticId=0xfffff;
            Assert.Equal(MissionPublicDoors.Kind.Legacy,missing.Resolve(unknown).State);
            unknown=Raw(c.Raw,0x0d100003);unknown.metrId=2;Assert.Equal(MissionPublicDoors.Kind.Legacy,missing.Resolve(unknown).State);
            string file=Path.Combine(temp,"209.json.gz");File.WriteAllBytes(file,new byte[]{0x1f,0x8b});
            Assert.Equal(MissionPublicDoors.Kind.Refused,new MissionPublicDoors.Dataset(temp,DirectoryFor("mission_interior_evidence_v2")).Resolve(raw).State);
            byte[] packed=File.ReadAllBytes(Path.Combine(DirectoryFor("mission_street_access"),"209.json.gz"));packed[^1]^=1;File.WriteAllBytes(file,packed);
            Assert.Equal(MissionPublicDoors.Kind.Refused,new MissionPublicDoors.Dataset(temp,DirectoryFor("mission_interior_evidence_v2")).Resolve(raw).State);
            File.Copy(Path.Combine(DirectoryFor("mission_street_access"),"209.json.gz"),file,true);
            var valid=new MissionPublicDoors.Dataset(temp,DirectoryFor("mission_interior_evidence_v2"));var result=valid.Resolve(raw);
            Assert.Equal(MissionPublicDoors.Kind.Resolved,result.State);Assert.Equal((ushort)346,result.Pose.Goid);Assert.Equal(raw.mxoStaticId,result.Pose.Key);
            raw.pos_z+=1;Assert.Equal(MissionPublicDoors.Kind.Refused,valid.Resolve(raw).State);
        }
        finally{Directory.Delete(temp,true);}
    }
    [Fact]public void UnknownRawSignatureOtherWorldOtherFloorAndMissingPublicNodeCannotSupplyAccess()
    {
        var c=Load(209);var lobby=Lobby(c.Evidence,c.Interior,0x0d100028,49,283,28500,495,14200);
        Assert.False(c.Evidence.TryStreetAccess(c.Outdoor,lobby,new E.Node(65000,0),0x0d100028,key=>Raw(c.Raw,key),out _,out _));
        Assert.False(c.Evidence.TryStreetAccess(c.Outdoor,new E.Entry(c.Evidence,lobby.Door,lobby.Node,new G.Position(28500,895,14200)),new E.Node(13,0),0x0d100028,key=>Raw(c.Raw,key),out _,out _));
        Assert.False(c.Evidence.TryStreetAccess(c.Outdoor,lobby,new E.Node(13,0),0x0d100028,key=>{var raw=Raw(c.Raw,key);raw.pos_x++;return raw;},out _,out _));
        Assert.False(c.Evidence.TryStreetAccess(c.Outdoor,lobby,new E.Node(13,0),0x0d100028,key=>{var raw=Raw(c.Raw,key);raw.metrId=2;return raw;},out _,out _));
    }
    static S Modified(JsonObject document,E evidence)
    {
        byte[] raw=JsonSerializer.SerializeToUtf8Bytes(document);using var output=new MemoryStream();using(var zip=new GZipStream(output,CompressionLevel.Optimal,true))zip.Write(raw);
        byte[] packed=output.ToArray();return S.Read(packed,Convert.ToHexString(SHA256.HashData(packed)),Convert.ToHexString(SHA256.HashData(raw)),evidence);
    }
    [Theory][InlineData("world")][InlineData("interior")][InlineData("nav")][InlineData("ordinal")][InlineData("count")][InlineData("offset")][InlineData("native")][InlineData("dropDoor")]
    public void RawCountSourceIndexAndNativeSchemaChangesRefuse(string change)
    {
        var c=Load(209);var d=c.Raw;var region=d["Outdoor"]!["Regions"]![16]!;var polygon=region["Polygons"]![18]!;
        switch(change)
        {
            case "world":d["World"]=2;break;case "interior":d["InteriorDigest"]=new string('A',64);break;case "nav":d["NavSha256"]=new string('A',64);break;
            case "ordinal":polygon["Index"]=19;break;case "count":polygon["RawHeader"]="00000000000080bf000000800000c84305000000";break;
            case "offset":polygon["SourceOffset"]=int.MaxValue;break;case "native":d["NativeDoors"]![0]!["Goid"]=d["NativeDoors"]![0]!["Goid"]!.GetValue<int>()+1;break;
            case "dropDoor":d["NativeDoors"]!.AsArray().RemoveAt(0);break;
        }
        Assert.Throws<InvalidDataException>(()=>Modified(d,c.Evidence));
    }
    [Fact]public void MissingOriginalReverseLinksCannotBeTreatedAsDoorlessOpenings()
    {
        var c=Load(209);
        foreach(var region in c.Raw["Outdoor"]!["Regions"]!.AsArray())foreach(var polygon in region!["Polygons"]!.AsArray())foreach(var edge in polygon!["Edges"]!.AsArray())
        {
            byte[] raw=Convert.FromHexString(edge!["Raw"]!.GetValue<string>());ushort target=BitConverter.ToUInt16(raw);
            if(target>=38&&target<88){Array.Fill(raw,(byte)255,0,4);edge["Raw"]=Convert.ToHexString(raw);}
        }
        var outside=Modified(c.Raw,c.Evidence);var lobby=Lobby(c.Evidence,c.Interior,0x0d100028,49,283,28500,495,14200);
        Assert.False(c.Evidence.TryStreetAccess(outside,lobby,new E.Node(13,0),0x0d100028,key=>Raw(c.Raw,key),out _,out _));
    }
    [Theory][InlineData("count")][InlineData("source")][InlineData("reverse")][InlineData("truncated")]
    public void CountedOriginalSectorBoundaryRowsRetainExactSourceAndReverseReferences(string change)
    {
        var c=Load(209);byte[] snav=Convert.FromHexString(c.Raw["Outdoor"]!["RawSnav"]!.GetValue<string>());
        Assert.Equal("1F000100000000000000003A0000C8430048B7450000003A0000C84300A8B645",Convert.ToHexString(snav.AsSpan(60,32)));
        switch(change)
        {
            case "count":snav[56]++;break;
            case "source":snav[60]=255;break;
            case "reverse":snav[64]=1;break;
            case "truncated":snav=snav[..^1];break;
        }
        c.Raw["Outdoor"]!["RawSnav"]=Convert.ToHexString(snav);
        Assert.Throws<InvalidDataException>(()=>Modified(c.Raw,c.Evidence));
    }
    [Fact]public void OriginalNonFfffBoundarySublinkIsNotReinterpretedAsALocalRegionReference()
    {
        var c=Load(1283);var node=new E.Node(34,7);var poly=c.Outdoor.At(node);
        Assert.Equal("10000800e0259e3e4c7b733f0080533e3fe66844",poly.Edges[4].Raw);
        Assert.True(poly.Boundary[4]);Assert.False(c.Outdoor.Has(new E.Node(16,8)));
        Assert.DoesNotContain(node,c.Outdoor.NetworkAnchors());
        Assert.False(c.Outdoor.TryOutsidePath(node,new E.Node(16,8),out _));
    }
    [Fact]public void OriginalTerminalReferenceIsPreservedWithoutBecomingPolygon65535()
    {
        var c=Load(209);var edge=c.Outdoor.At(new E.Node(31,1)).Edges[0];
        Assert.Equal("0000ffff000080bf000000000000003a0048b745",edge.Raw);
        Assert.False(c.Outdoor.Has(new E.Node(0,ushort.MaxValue)));
        Assert.False(c.Outdoor.TryOutsidePath(new E.Node(31,1),new E.Node(0,ushort.MaxValue),out _));
    }
}
