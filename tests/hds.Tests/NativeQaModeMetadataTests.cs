using System.Text.Json;
using NativeQa;
namespace hds.Tests;

public class NativeQaModeMetadataTests
{
    private static NativeQaModePlan Plan(Dictionary<string,object?>? values=null,string[]? messages=null) =>
        new(new ushort[]{2},0,0,Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),extraQaMetadata:values,readyMessages:messages);

    [Fact]
    public void PrimitiveMetadataIsCopiedAndMergedWithoutMutableReferences()
    {
        var values=new Dictionary<string,object?> { ["WriteCodeQa"]=true,["ZenMasterForcedLevel"]=99,["Note"]="fixture",["Optional"]=null };
        var messages=new[]{"original"};
        var plan=Plan(values,messages);
        values["ZenMasterForcedLevel"]=50; messages[0]="changed";
        Assert.Equal(99,plan.ExtraQaMetadata["ZenMasterForcedLevel"].GetInt32());
        Assert.Equal("original",Assert.Single(plan.ReadyMessages));
        Assert.Throws<NotSupportedException>(()=>((IDictionary<string,JsonElement>)plan.ExtraQaMetadata).Add("extra",default));
        var merged=plan.StateWithMetadata(new {CharId=110,SourceAssemblySha256="source",SnapshotAssemblySha256="source"});
        Assert.Equal(110,merged["CharId"]!.GetValue<int>());
        Assert.Equal("source",merged["SourceAssemblySha256"]!.GetValue<string>());
        Assert.True(merged["WriteCodeQa"]!.GetValue<bool>());
        Assert.Equal(99,merged["ZenMasterForcedLevel"]!.GetValue<int>());
        Assert.Null(merged["Optional"]);
    }

    [Theory]
    [InlineData("CharId")]
    [InlineData("SourceAssemblySha256")]
    [InlineData("SnapshotAssemblySha256")]
    [InlineData("HealthCallbackQa")]
    public void MetadataCannotReplaceStandardOrProvenanceFields(string key)
    {
        var plan=Plan(new(){[key]="replacement"});
        Assert.Throws<ArgumentException>(()=>plan.StateWithMetadata(new {CharId=110,SourceAssemblySha256="source",SnapshotAssemblySha256="source",HealthCallbackQa=false}));
    }

    [Fact]
    public void MutableObjectsAreRejectedAndOldModesKeepExactDefaultStateSerialization()
    {
        Assert.Throws<ArgumentException>(()=>Plan(new(){["mutable"]=new List<int>{1}}));
        var standard=new {CharId=110,SourceAssemblySha256="source",HealthCallbackQa=false};
        foreach(string mode in NativeQaFixturePlanTests.LegacyModes().Select(row=>(string)row[0]))
        {
            var plan=NativeQaModes.Find(mode).Plan;
            Assert.Empty(plan.ExtraQaMetadata);
            Assert.Equal(JsonSerializer.Serialize(standard),JsonSerializer.Serialize(plan.StateWithMetadata(standard)));
        }
    }
}
