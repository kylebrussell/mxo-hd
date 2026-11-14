using System.Text.Json;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>QA movement only: preserve an adopted Safe and follow its published route.
    /// Untouched legacy/custom fixtures retain their original object-relocation behavior.</summary>
    private static async Task ApproachMissionObjectFixture(ScenarioContext context, HeadlessPlayer player, ushort objectId)
    {
        string payload=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={player.CharId}");
        using var json=JsonDocument.Parse(payload);
        var root=json.RootElement;
        var obj=root.GetProperty("Objects").EnumerateArray().Single(x=>x.GetProperty("Id").GetUInt16()==objectId);
        if(!obj.TryGetProperty("OpenFloor",out var value)||value.ValueKind==JsonValueKind.Null)
        {
            player.SendChat($"?missionobject {objectId}");
            await player.WaitForTextAsync("Object moved");
            return;
        }
        var witness=MissionOpenFloorWitness.Read(value);
        var source=JsonSerializer.Deserialize<StaticWorldObject>(obj.GetProperty("Spot").GetRawText(),new JsonSerializerOptions{IncludeFields=true})!;
        ushort sourceGoid=source.type.Length>=2 ? BitConverter.ToUInt16(source.type,0) : (ushort)0;
        witness.Require(root.GetProperty("Mission").GetInt32(),root.GetProperty("Phase").GetInt32(),objectId,
            source.mxoStaticId,sourceGoid,obj.GetProperty("Goid").GetUInt16());
        Check.Equal((ushort)552,witness.ConcreteGoid,"Only the published adopted Safe replaces fixture relocation");
        string savedSource=obj.GetProperty("Spot").GetRawText();
        // This uses the existing QA teleport helper; it is not a native walking/navigation claim.
        for(int i=0;i<witness.Route.Count;i+=3)
            await MoveForMissionTest(player,witness.Route[i],witness.Route[i+1],witness.Route[i+2]);
        string after=await context.Database.ScalarAsync($"SELECT payload FROM char_active_missions WHERE charId={player.CharId}");
        using var current=JsonDocument.Parse(after);
        var currentObject=current.RootElement.GetProperty("Objects").EnumerateArray().Single(x=>x.GetProperty("Id").GetUInt16()==objectId);
        Check.Equal(savedSource,currentObject.GetProperty("Spot").GetRawText(),"QA approach leaves checkpoint source unchanged");
        Check.Equal(witness.RawJson,currentObject.GetProperty("OpenFloor").GetRawText(),"QA approach does not replan or replace the fixed witness");
    }
}
