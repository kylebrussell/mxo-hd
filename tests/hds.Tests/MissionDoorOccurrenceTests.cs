using System.IO.Compression;
using System.Text.Json;
using HeadlessClient;

namespace hds.Tests;

public class MissionDoorOccurrenceTests
{
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Theory]
    [InlineData(49283281u,(ushort)6959,38,7,2545d)]
    [InlineData(49284087u,(ushort)6958,39,21,2145d)]
    public void NativePortalCanDeclareItsDoorSchemaOnTheFarSide(uint key,ushort goid,int from,int to,double y)
    {
        using var packed=File.OpenRead(Path.Combine(Repo,"data/mission_interior_evidence_v2/1/47.json.gz"));
        using var zip=new GZipStream(packed,CompressionMode.Decompress);using var doc=JsonDocument.Parse(zip);
        var group=doc.RootElement.GetProperty("Groups").EnumerateArray().Single(g=>g.GetProperty("Key").GetUInt32()==key);
        var own=Assert.Single(MissionDoorOccurrences.ForPortal(group,goid,from,to));
        Assert.Equal(to,own.GetProperty("Region").GetInt32());
        Assert.Equal(new[]{11700d,y,2400},own.GetProperty("ModelPosition").EnumerateArray().Select(v=>v.GetDouble()));
        Assert.Empty(MissionDoorOccurrences.ForPortal(group,goid,from,999));
        Assert.Empty(MissionDoorOccurrences.ForPortal(group,9999,from,to));
    }
    [Fact] public void OppositeFacesWithTheSameSchemaAreQualifiedBySourceRoom()
    {
        using var doc=JsonDocument.Parse("""{"Occurrences":[{"Goid":6958,"Region":20,"Quaternion":"a"},{"Goid":6958,"Region":38,"Quaternion":"b"}]}""");
        Assert.Equal("a",Assert.Single(MissionDoorOccurrences.ForPortal(doc.RootElement,6958,20,38)).GetProperty("Quaternion").GetString());
        Assert.Equal("b",Assert.Single(MissionDoorOccurrences.ForPortal(doc.RootElement,6958,38,20)).GetProperty("Quaternion").GetString());
    }

}
