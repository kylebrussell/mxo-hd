using System.Security.Cryptography;
using System.Text.Json;
namespace hds.Tests;
public class MissionOpenFloorSourceTests
{
    private static string Repo {get {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds","Hardline Dreams MxO server.csproj")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact]
    public void PinnedNativeAndGobEvidenceDistinguishesStoredRangeFromInitializedFallback()
    {
        using var native=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"research/protocol/mission-open-floor-usable-range.json")));
        Assert.Equal("cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a",native.RootElement.GetProperty("ClientSha256").GetString());
        Assert.Equal("UsableExecuteMaxDist",native.RootElement.GetProperty("Strings").GetProperty("0x628dc43c").GetString());
        Assert.Equal(65,native.RootElement.GetProperty("Constants").GetProperty("0x628dc328").GetProperty("Float").GetDouble());
        foreach(var r in native.RootElement.GetProperty("Ranges").EnumerateArray())
        {
            byte[] bytes=Convert.FromHexString(r.GetProperty("Hex").GetString()!);Assert.Equal(r.GetProperty("Length").GetInt32(),bytes.Length);
            Assert.Equal(r.GetProperty("Sha256").GetString(),Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
        var init=native.RootElement.GetProperty("Ranges").EnumerateArray().Single(r=>r.GetProperty("Address").GetString()=="0x62828f30");
        Assert.Contains("680000c843",init.GetProperty("Hex").GetString()); // push initial400 float bits
        using var gob=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"research/protocol/mission-open-floor-proxy-sources.json")));
        Assert.Equal("3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103",gob.RootElement.GetProperty("sourceGobSha256").GetString());
        var objects=gob.RootElement.GetProperty("objects").EnumerateArray().ToDictionary(r=>r.GetProperty("id").GetInt32());
        JsonElement Field(int id,string name)=>objects[id].GetProperty("fields").EnumerateArray().Single(f=>f.GetProperty("name").GetString()==name);
        var computer=Field(3,"GetUsable/MaxDistance");Assert.False(computer.GetProperty("stored").GetBoolean());Assert.Equal(387,computer.GetProperty("offset").GetInt32());Assert.Equal(327,objects[3].GetProperty("storedSize").GetInt32());Assert.Equal(648,objects[3].GetProperty("storageSize").GetInt32());
        foreach(int id in new[]{548,552}) {var field=Field(id,"OpenUse/MaxDistance");Assert.True(field.GetProperty("stored").GetBoolean());Assert.Equal("0000c842",field.GetProperty("rawHex").GetString());Assert.Equal(100,field.GetProperty("value").GetDouble());}
        foreach(int id in new[]{3,548,552,1407,1410,1412}) {var field=Field(id,"Model/HalfExtents");Assert.Equal(12,field.GetProperty("width").GetInt32());Assert.Equal(24,field.GetProperty("rawHex").GetString()!.Length);}
        Assert.DoesNotContain(objects[1415].GetProperty("components").EnumerateArray(),c=>c.GetProperty("type").GetInt32()==6);
    }
}

public class MissionOpenFloorProfileTests
{
    [Fact]
    public void SharedRegistryIsExactlyTheApprovedAssetEnvelopeAndPreservesExactSupport()
    {
        string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds","Hardline Dreams MxO server.csproj")))p=Path.GetDirectoryName(p)!;
        byte[] bytes=File.ReadAllBytes(Path.Combine(p,"research/protocol/mission-open-floor-proxy-models.json"));
        Assert.Equal(hds.MissionOpenFloorProfiles.ModelSourceSha256,Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var doc=JsonDocument.Parse(bytes);
        foreach(var row in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            Assert.True(hds.MissionOpenFloorProfiles.TryModel(row.GetProperty("goid").GetUInt16(),out var model));
            Assert.Equal(row.GetProperty("supportMinY").GetDouble(),model.SupportMinY);
            Assert.Equal(row.GetProperty("conservativeMin").EnumerateArray().Select(x=>x.GetDouble()),new[]{model.Min.X,model.Min.Y,model.Min.Z});
            Assert.Equal(row.GetProperty("conservativeMax").EnumerateArray().Select(x=>x.GetDouble()),new[]{model.Max.X,model.Max.Y,model.Max.Z});
            Assert.True(hds.MissionOpenFloorProfiles.TryUsable(model.Goid,out var usable));Assert.Equal(model.Goid,usable.Goid);
        }
        Assert.False(hds.MissionOpenFloorProfiles.TryModel(1415,out _));Assert.False(hds.MissionOpenFloorProfiles.TryUsable(1415,out _));
        Assert.True(hds.MissionOpenFloorProfiles.TryUsable(3,out var computer));Assert.True(computer.AssumesInitialClientConfiguration);Assert.Equal(400,computer.LowerBound);
        Assert.True(hds.MissionOpenFloorProfiles.TryUsable(548,out var box));Assert.False(box.AssumesInitialClientConfiguration);Assert.Equal(135,box.LowerBound);
        Assert.True(hds.MissionOpenFloorProfiles.TryUsable(552,out var safe));Assert.Equal(100,safe.LowerBound);
    }
}
