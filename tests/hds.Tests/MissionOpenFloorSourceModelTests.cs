using System.Text;
using System.Text.Json;
using hds;
namespace hds.Tests;
public class MissionOpenFloorSourceModelTests
{
    private static object Entry(ushort goid,string status,int[] components)=>new{goid,status,recordSha256=new string('a',64),componentTypes=components};
    private static IReadOnlyDictionary<ushort,MissionOpenFloorSourceModels.Entry> Read(params object[] entries)
    {
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{format=1,world=1,entries,sourceHashes=new{gob="pinned"}})));
        return MissionOpenFloorSourceModels.Read(stream,1);
    }
    [Fact]
    public void AbsenceIsPositiveCatalogEvidenceRatherThanMissingObstacleInference()
    {
        var rows=Read(Entry(1415,"Model6Absent",new[]{11,1}),Entry(1410,"AuthoredStaticBounds",new[]{6,1}));
        Assert.Equal(MissionOpenFloorResolver.SourceModelPresence.Model6Absent,rows[1415].Presence);Assert.Equal(MissionOpenFloorResolver.SourceModelPresence.AuthoredStaticBounds,rows[1410].Presence);
        Assert.False(rows.ContainsKey(1444));Assert.Contains("GOB:1415:Model6Absent:",rows[1415].Evidence);
    }
    [Theory][InlineData("Model6Absent",true)][InlineData("AuthoredStaticBounds",false)][InlineData("Unknown",false)]
    public void ContradictoryOrUnknownEvidenceRefuses(string status,bool model)=>Assert.Throws<InvalidDataException>(()=>Read(Entry(1415,status,model?new[]{6}:Array.Empty<int>())));
    [Fact]
    public void DuplicateCatalogRowsCannotSelectFirstOrLast()=>Assert.Throws<InvalidDataException>(()=>Read(Entry(1415,"Model6Absent",new[]{1}),Entry(1415,"Model6Absent",new[]{1})));
}
