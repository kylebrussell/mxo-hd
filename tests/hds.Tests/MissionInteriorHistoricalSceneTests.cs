using System.Text.Json;
using hds;
using E=hds.MissionInteriorEvidence;
using G=hds.MissionOpenFloorResolver;
namespace hds.Tests;
public partial class MissionNpcInteriorCheckpointTests
{
    private const string Old550="9C34CC3D271C62779C403EFCC95CF7F6ADAF88CB7CF4740C1A30C64455E18CA4";
    private static string SceneDirectory=>Path.Combine(Repo,"data","mission_interior_evidence","1");
    private sealed class VersionProvider:IMissionNpcInteriorEvidenceProvider
    {
        private readonly Provider basic=new Provider();internal readonly string Directory;private readonly bool old;internal int Entries=>basic.EntryCalls;
        internal VersionProvider(string directory,bool old=false){Directory=directory;this.old=old;}
        public E Evidence(ushort world,ushort sector)=>old?E.LoadForDigest(Directory,world,sector,Old550):E.LoadAudited(Directory,world,sector);
        public E SavedEvidence(ushort world,ushort sector,string digest)=>E.LoadForDigest(Directory,world,sector,digest);
        public StaticWorldObject[] Markers(ushort world,ushort sector)=>basic.Markers(world,sector);
        public StaticWorldObject Door(ushort world,uint key)=>basic.Door(world,key);
        public MissionOpenFloorEntry.Result Entry(ushort world,G.Position door,double x,double z,string why)=>basic.Entry(world,door,x,z,why);
    }
    private static (MissionProgress P,MissionSite Site,MissionProgress.SavedState Saved) OldSceneCheckpoint()
    {
        var p=new MissionProgress(Definition()){phaseIndex=1,persistenceId="historical-scene",startedAt=123};var site=Site();var provider=new VersionProvider(SceneDirectory,true);
        Assert.True(MissionNpcInteriorPlanning.Prepare(Client(p),p,2,site,provider,out _,out var pending,out var why),why);
        Assert.Equal(Old550,pending.Witness.Floor.EvidenceDigest);p.npcInteriorPending=pending;p.itemSlots[6783]=1;
        return(p,site,p.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{site}));
    }
    [Fact] public void OldFixedSceneRestoresExactlyWhileActiveDoorCatalogIsExpanded()
    {
        var f=OldSceneCheckpoint();Assert.NotEqual(Old550,E.LoadAudited(SceneDirectory,1,550).Digest);
        string before=JsonSerializer.Serialize(f.Saved,MissionProgress.SaveJson);var provider=new VersionProvider(SceneDirectory);
        var restored=MissionProgress.RestoreWithNpcCatalog(f.P.mission,f.Saved,new[]{f.Site},provider);
        Assert.Equal(before,JsonSerializer.Serialize(restored.CaptureWithNpcCatalog(47,Array.Empty<Mob>(),new[]{f.Site}),MissionProgress.SaveJson));
        Assert.Equal(0,provider.Entries);Assert.Equal((ushort)1,restored.itemSlots[6783]);Assert.Equal(Old550,restored.npcInteriorPending.Witness.Floor.EvidenceDigest);
    }
    [Fact] public void UnknownSavedSceneDigestQuarantinesAfterOwnedItemsWithoutChangingPayload()
    {
        var f=OldSceneCheckpoint();var node=System.Text.Json.Nodes.JsonNode.Parse(f.Saved.NpcInterior.GetRawText())!;node["Pending"]!["Witness"]!["Floor"]!["EvidenceDigest"]=new string('0',64);
        f.Saved.NpcInterior=JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());string before=JsonSerializer.Serialize(f.Saved,MissionProgress.SaveJson);
        var provider=new VersionProvider(SceneDirectory);var error=Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreWithNpcCatalog(f.P.mission,f.Saved,new[]{f.Site},provider));
        Assert.Equal((ushort)1,error.Progress.itemSlots[6783]);Assert.Equal(before,JsonSerializer.Serialize(f.Saved,MissionProgress.SaveJson));Assert.Equal(0,provider.Entries);
    }
    [Fact] public void MissingHistoricalPayloadRefusesWithoutBorrowingActiveScene()
    {
        var f=OldSceneCheckpoint();string tempRoot=Path.GetFullPath(Path.GetTempPath()),temp=Path.Combine(tempRoot,"mxo-history-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(temp,"history"));
        try
        {
            File.Copy(Path.Combine(SceneDirectory,"manifest.json"),Path.Combine(temp,"manifest.json"));File.Copy(Path.Combine(SceneDirectory,"550.json.gz"),Path.Combine(temp,"550.json.gz"));
            File.Copy(Path.Combine(SceneDirectory,"history","manifest.json"),Path.Combine(temp,"history","manifest.json"));
            var provider=new VersionProvider(temp);var error=Assert.Throws<MissionPlacementRestoreException>(()=>MissionProgress.RestoreWithNpcCatalog(f.P.mission,f.Saved,new[]{f.Site},provider));
            Assert.Equal((ushort)1,error.Progress.itemSlots[6783]);Assert.Equal(0,provider.Entries);
        }
        finally{Assert.StartsWith(tempRoot,Path.GetFullPath(temp),StringComparison.OrdinalIgnoreCase);Directory.Delete(temp,true);}
    }
    [Theory] [InlineData(580)] [InlineData(6958)] [InlineData(6959)]
    public void GenericClass364SceneDoorUsesItsOwnAuditedPose(int goid)
    {
        var index=E.LoadAudited(SceneDirectory,1,842);using var file=File.OpenRead(Path.Combine(SceneDirectory,"842.json.gz"));using var gz=new System.IO.Compression.GZipStream(file,System.IO.Compression.CompressionMode.Decompress);var doc=JsonSerializer.Deserialize<E.Document>(gz)!;
        uint key=goid==6959?0x34a0028cu:0x34a00246u;var group=doc.Groups.Single(g=>g.Key==key);var signature=group.Legacy.First(l=>l.Signature.Goid==goid).Signature;
        var raw=Raw(key,(ushort)goid,signature.Position[0],signature.Position[1],signature.Position[2],signature.Rotation,signature.Quaternion);
        Assert.True(MissionInteriorDoorTypes.IsSupported((ushort)goid));Assert.True(index.TryWoodDoor(raw,out var pose,out var why),why);
        Assert.Equal(goid,pose.Goid);Assert.Equal(goid==6959?10300:11700,pose.Position.X);Assert.Equal(545,pose.Position.Y);Assert.Equal(goid==6959?9600:10400,pose.Position.Z);
        var own=group.Occurrences.Single(o=>o.Goid==goid);Assert.Equal(own.Quaternion,pose.Quaternion);
        raw.pos_x++;Assert.False(index.TryWoodDoor(raw,out _,out _));Assert.False(MissionInteriorDoorTypes.IsSupported(417));
    }
    [Fact] public void PairedClass364ModelsNeverBorrowOneAnothersPositionOrYaw()
    {
        var index=E.LoadAudited(SceneDirectory,1,842);using var f=File.OpenRead(Path.Combine(SceneDirectory,"842.json.gz"));using var z=new System.IO.Compression.GZipStream(f,System.IO.Compression.CompressionMode.Decompress);var doc=JsonSerializer.Deserialize<E.Document>(z)!;
        var group=doc.Groups.Single(g=>g.Key==0x34a0028a);
        foreach(ushort goid in new ushort[]{6958,6959})
        {
            var s=group.Legacy.Single(l=>l.Signature.Goid==goid).Signature;var raw=Raw(group.Key,goid,s.Position[0],s.Position[1],s.Position[2],s.Rotation,s.Quaternion);
            Assert.True(index.TryWoodDoor(raw,out var pose,out var why),why);var own=group.Occurrences.Single(o=>o.Goid==goid);
            Assert.Equal(own.ModelPosition,new[]{pose.Position.X,pose.Position.Y,pose.Position.Z});Assert.Equal(own.Quaternion,pose.Quaternion);
        }
        var ambiguous=doc.Groups.Single(g=>g.Key==0x34a00268);var signature=ambiguous.Legacy[0].Signature;
        Assert.False(index.TryWoodDoor(Raw(ambiguous.Key,6958,signature.Position[0],signature.Position[1],signature.Position[2],signature.Rotation,signature.Quaternion),out _,out _));
    }
    [Fact] public void FixedFurnitureRoutesChooseTheDoorFaceInTheCrossedRoom()
    {
        var index=E.LoadAudited(SceneDirectory,1,47);
        using var f=File.OpenRead(Path.Combine(SceneDirectory,"47.json.gz"));
        using var z=new System.IO.Compression.GZipStream(f,System.IO.Compression.CompressionMode.Decompress);
        var doc=JsonSerializer.Deserialize<E.Document>(z)!;var group=doc.Groups.Single(g=>g.Key==0x02f003bc);
        var signature=group.Legacy.First(l=>l.Signature.Goid==6958).Signature;
        var raw=Raw(group.Key,6958,signature.Position[0],signature.Position[1],signature.Position[2],signature.Rotation,signature.Quaternion);
        Assert.False(index.TryWoodDoor(raw,out _,out _));
        foreach(var own in group.Occurrences.Where(o=>o.Goid==6958))
        {
            Assert.True(index.TryWoodDoorFromRoom(raw,own.Region,ushort.MaxValue,out var pose,out var why),why);
            Assert.Equal(own.Quaternion,pose.Quaternion);Assert.Equal(own.ModelPosition,new[]{pose.Position.X,pose.Position.Y,pose.Position.Z});
        }
        Assert.False(index.TryWoodDoorFromRoom(raw,ushort.MaxValue,ushort.MaxValue,out _,out _));
        raw.pos_x++;Assert.False(index.TryWoodDoorFromRoom(raw,20,38,out _,out _));
    }

}
