using System.Reflection;
using System.Text.Json;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
namespace hds.Tests;

[Collection("DelayedActions")]
public class MissionInteriorDoorTypesTests
{
    private static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact] public void TypedCatalogExactlyMatchesEveryPrimaryClass364Contract()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo,"tests/hds.Tests/Fixtures/Class364DoorContracts.json")));
        Assert.Equal("3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103",doc.RootElement.GetProperty("SourceGobSha256").GetString());
        var rows=doc.RootElement.GetProperty("Rows").EnumerateArray().ToArray();Assert.Equal(76,rows.Length);
        Assert.Equal(rows.Select(r=>r.GetProperty("Goid").GetUInt16()).Order(),MissionInteriorDoorTypes.SupportedGoids);
        foreach(var row in rows)
        {
            ushort id=row.GetProperty("Goid").GetUInt16();Assert.True(MissionInteriorDoorTypes.IsSupported(id));
            Assert.Equal(typeof(AttributeClass364),typeof(GameObjectDefinitions).GetField("Object"+id)!.FieldType);
            var components=row.GetProperty("Components").EnumerateArray().ToDictionary(c=>c.GetProperty("name").GetString()!,c=>c.GetProperty("type").GetInt32());
            Assert.Equal(6,components["Model"]);Assert.Equal(4,components["NavMeshController"]);Assert.Equal(52,components["PortalController"]);
            var f=row.GetProperty("Fields");Assert.Equal(4004,f.GetProperty("OpenUsage/ClientRuleID").GetInt32());Assert.Equal(4004,f.GetProperty("OpenUsage/ExecuteRuleID").GetInt32());
            Assert.Equal(1,f.GetProperty("OpenUsage/Defaultable").GetInt32());Assert.Equal(1000,f.GetProperty("OpenUsage/MaxDistance").GetDouble());
            Assert.StartsWith("3408000000000000140000000000000038080000",f.GetProperty("StateMachine/OpenedState").GetString());
            Assert.StartsWith("3808000000000000010000000000000035080000",f.GetProperty("StateMachine/ClosingState").GetString());
            Assert.Equal(0x835,f.GetProperty("StateMachine/CurrentState").GetInt32());
        }
    }
    [Theory][InlineData(343)][InlineData(346)][InlineData(580)][InlineData(6958)][InlineData(6959)]
    public void OldAndPreviouslyUnlistedClassMembersAreSupported(int goid)=>Assert.True(MissionInteriorDoorTypes.IsSupported((ushort)goid));
    [Theory][InlineData(0)][InlineData(3)][InlineData(326)][InlineData(417)][InlineData(419)][InlineData(65535)]
    public void ElevatorsAndOtherClassesAreSeparate(int goid)=>Assert.False(MissionInteriorDoorTypes.IsSupported((ushort)goid));
    [Fact] public void PublishedCatalogIsCachedAndCannotBeMutated()
    {
        var first=MissionInteriorDoorTypes.SupportedGoids;Assert.Same(first,MissionInteriorDoorTypes.SupportedGoids);
        Assert.Throws<NotSupportedException>(()=>((IList<ushort>)first)[0]=0);
        Assert.Equal(76,first.Count);Assert.False(MissionInteriorDoorTypes.IsSupported(0));
    }
    [Fact] public void EveryClassMemberUsesTheActualOwnerGuardBeforePublication()
    {
        var prior=Store.currentClient;var client=(WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        try
        {
            Store.currentClient=client;Assert.Null(client.playerData);
            foreach(ushort goid in MissionInteriorDoorTypes.SupportedGoids)
            {
                var raw=new StaticWorldObject{metrId=1,mxoStaticId=1,type=BitConverter.GetBytes(goid)};
                // Missing owner refuses before publication or dereferencing item; bypassing the
                // owner guard enters ProcessObjectInteractionCore and cannot satisfy this call.
                new ObjectInteractionHandler().processObjectInteraction(raw,null!);
                Assert.Null(client.playerData);
            }
        }
        finally{Store.currentClient=prior;}
    }
}
