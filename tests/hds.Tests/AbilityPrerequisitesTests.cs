using System.Text.Json;
using hds;
namespace hds.Tests;
public class AbilityPrerequisitesTests
{
    static object Component(string name,int type)=>new{name,type};
    static object Row(ushort id,object[] components,Dictionary<string,object> fields)=>new{id,components,fields};
    static AbilityPrerequisites.Graph Graph(params object[] rows)=>AbilityPrerequisites.Parse(JsonSerializer.Serialize(new{schema_version=1,abilities=rows}));
    static string Root()
    {string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}
    [Fact] public void ActualCatalogCoversEveryStoredEdgeAndKnownNativeExamples()
    {
        var graph=AbilityPrerequisites.Read(Path.Combine(Root(),"data","ability_catalog.json"));
        Assert.Equal(1404,graph.All.Count);Assert.Equal(963,graph.All.Values.Count(x=>x.Direct.Count!=0));
        Assert.Equal(1429,graph.All.Values.Sum(x=>x.Direct.Count));Assert.Equal(53,graph.All.Values.Count(x=>x.CharacterLevel!=0));
        Assert.All(graph.All.Values.SelectMany(e=>e.Direct.Keys),id=>Assert.NotNull(graph.Find(id)));
        Assert.Equal(1,Assert.Single(graph.Find(217)!.Direct).Value);Assert.Equal((ushort)2,Assert.Single(graph.Find(217)!.Direct).Key);
        Assert.Equal(6,graph.Find(75)!.Direct[95]);Assert.Equal(22,graph.Find(314)!.Direct[309]);
        Assert.Equal(50,graph.Find(356)!.Direct[358]);Assert.Equal((byte)33,graph.Find(358)!.CharacterLevel);
        Assert.Equal((byte)99,graph.Find(7)!.CharacterLevel);Assert.Equal((byte)0,graph.Find(217)!.CharacterLevel);
    }
    [Fact] public void ComponentTypeNotSuggestiveNameAndAbsentLevelDefaultsToZero()
    {
        var g=Graph(Row(1,new[]{Component("Misleading",39),Component("AbilityPrerequisite",40)},new(){
            ["Misleading/ReqAbilityID"]=0x80000800u,["AbilityPrerequisite/ReqAbilityID"]=0x80000c00u,
            ["Ability/DisciplineBelongsTo"]=0x80001000u}));
        Assert.Equal(new KeyValuePair<ushort,int>(2,0),Assert.Single(g.Find(1)!.Direct));
        Assert.Equal((byte)0,g.Find(1)!.CharacterLevel);Assert.Null(g.Find(2));
    }
    [Fact] public void RepeatedParentsUseMaximumAndCyclesTerminateWithoutDroppingRequirements()
    {
        var g=Graph(
            Row(1,new[]{Component("A",39),Component("B",39)},new(){["A/ReqAbilityID"]=0x80000800u,["A/ReqAbilityLevel"]=2,["B/ReqAbilityID"]=0x80000c00u}),
            Row(2,new[]{Component("A",39)},new(){["A/ReqAbilityID"]=0x80000c00u,["A/ReqAbilityLevel"]=7}),
            Row(3,new[]{Component("A",39)},new(){["A/ReqAbilityID"]=0x80000400u,["A/ReqAbilityLevel"]=4}));
        var result=g.Closure(4097);Assert.Equal(3,result.Count);Assert.Equal(4,result[1]);Assert.Equal(2,result[2]);Assert.Equal(7,result[3]);
        Assert.Same(g.Find(1),g.Find(8193));
        Assert.Throws<NotSupportedException>(()=>((IDictionary<ushort,int>)result).Add(9,1));
    }
    [Fact] public void DuplicateDirectReferencesKeepGreatestTrainingLevelAndIgnoreGroupFlag()
    {
        var g=Graph(Row(1,new[]{Component("A",39),Component("B",39)},new(){
            ["A/ReqAbilityID"]=0x80400800u,["A/ReqAbilityLevel"]=5,["A/ReqAbilityGroupRequired"]=0,
            ["B/ReqAbilityID"]=0x80000800u,["B/ReqAbilityLevel"]=3,["B/ReqAbilityGroupRequired"]=1}));
        Assert.Equal(5,Assert.Single(g.Find(1)!.Direct).Value);
    }
    [Fact] public void CodesAndZeroCannotMasqueradeAsPrerequisitesAndFoldersAreNotImplicitEdges()
    {
        var g=Graph(Row(1,new[]{Component("A",39),Component("B",39),Component("C",39)},new(){
            ["A/ReqAbilityID"]=0x84000800u,["B/ReqAbilityID"]=0x80000000u,["C/ReqAbilityID"]=0x88000800u,
            ["Ability/DisciplineBelongsTo"]=0x80000800u}));
        Assert.Empty(g.Find(1)!.Direct);
        var actual=AbilityPrerequisites.Read(Path.Combine(Root(),"data","ability_catalog.json"));
        Assert.Equal(new ushort[]{27,36,37,1008},actual.Find(18)!.Direct.Keys.Order().ToArray());
        Assert.Equal(new ushort[]{2},actual.Closure(217).Keys);
    }
    [Fact] public void CharacterByteGetterDefaultsAreDistinctFromTrainingLevelAndStrictMalformedCatalog()
    {
        var g=Graph(Row(1,Array.Empty<object>(),new(){["CharStatsPrerequisite/ReqCharLevel"]=99}),Row(2,Array.Empty<object>(),new(){["Other/ReqAbilityLevel"]=50}));
        Assert.Equal((byte)99,g.Find(1)!.CharacterLevel);Assert.Equal((byte)0,g.Find(2)!.CharacterLevel);
        Assert.Throws<InvalidDataException>(()=>Graph(Row(1,Array.Empty<object>(),new(){["A/ReqCharLevel"]=1,["B/ReqCharLevel"]=2})));
        Assert.Throws<InvalidDataException>(()=>AbilityPrerequisites.Parse("{\"schema_version\":2,\"abilities\":[]}"));
        Assert.Empty(g.Closure(999));Assert.Null(g.Find(999)); //Unknown lookup is not a positive admission decision.
    }
}
