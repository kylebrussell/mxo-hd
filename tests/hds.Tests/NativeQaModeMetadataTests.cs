using System.Text.Json;
using NativeQa;
namespace hds.Tests;

public class NativeQaModeMetadataTests
{
    public static IEnumerable<object[]> AllModes() => NativeQaModes.Names.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(AllModes))]
    public void EveryDiscoveredModeSerializesTheExecutorsStandardState(string mode)
    {
        var plan = NativeQaModes.Find(mode).Plan;
        var standard = new NativeQaFixtureState
        {
            Database = "mxotest_state_serialization", Directory = "fixture", Account = "CSR_AbilityQA",
            Character = "CSR_Tester", CharId = 110, BuildCommit = "test", Configuration = "Release",
            SourceAssembly = "hds.dll", SourceAssemblySha256 = "source", SnapshotAssemblySha256 = "source",
            Status = "Ready", Abilities = plan.Abilities, StealthShieldQa = plan.StealthShieldQa,
            ShieldReady = plan.StealthShieldQa, CasterOrganization = plan.StealthShieldQa ? 1 : null,
            CasterPvpFlag = plan.StealthShieldQa, HealthCallbackQa = plan.HealthCallbackQa,
            CompileItemsQa = plan.CompileItemsQa, CallbackRoll15 = plan.HealthCallbackQa ? 0 : null,
            CallbackRoll100 = plan.HealthCallbackQa ? 0 : null
        };
        var state = plan.StateWithMetadata(standard);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(state));
        var actual = document.RootElement;
        var standardFields = JsonSerializer.SerializeToElement(standard);
        foreach (var field in standardFields.EnumerateObject())
            Assert.Equal(field.Value.GetRawText(), actual.GetProperty(field.Name).GetRawText());
        foreach (var field in plan.ExtraQaMetadata)
            Assert.Equal(field.Value.GetRawText(), actual.GetProperty(field.Key).GetRawText());
        Assert.Equal(standardFields.EnumerateObject().Count() + plan.ExtraQaMetadata.Count, actual.EnumerateObject().Count());
    }

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
