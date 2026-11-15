using System.Text.Json;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class AbilityMemoryUiFixtureTests
{
    [Fact] public void NativeProfileIsLevelOneClosedAndFitsExactlyFifteenBeforeTwoOneCostLeaves()
    {
        Assert.Equal(14,AbilityMemoryUiFixture.Loaded.Count);Assert.Equal(19,AbilityMemoryUiFixture.Known.Count);Assert.Equal(19,AbilityMemoryUiFixture.Known.Distinct().Count());
        Assert.Equal(15,AbilityMemoryUiFixture.Loaded.Sum(AbilityMemory.Cost));Assert.Equal(1,AbilityMemory.Cost(217));Assert.Equal(1,AbilityMemory.Cost(628));Assert.Equal(16,AbilityMemory.ForExperience(0));
        using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"data","ability_catalog.json")));
        var rows=catalog.RootElement.GetProperty("abilities").EnumerateArray().ToDictionary(e=>e.GetProperty("id").GetUInt16());
        foreach(ushort id in AbilityMemoryUiFixture.Loaded.Concat(new ushort[]{217,628}))
        {
            var row=rows[id];var fields=row.GetProperty("fields");
            foreach(var component in row.GetProperty("components").EnumerateArray().Where(c=>c.GetProperty("type").GetInt32()==39))
            {
                string name=component.GetProperty("name").GetString()!;
                if(!fields.TryGetProperty(name+"/ReqAbilityID",out var required))continue;
                ushort parent=AbilityCatalog.IdOf(required.GetUInt32());Assert.Contains(parent,AbilityMemoryUiFixture.Loaded);
                if(fields.TryGetProperty(name+"/ReqAbilityLevel",out var level))Assert.InRange(level.GetDouble(),0,1);
            }
            foreach(var field in fields.EnumerateObject().Where(f=>f.Name.EndsWith("/ReqCharLevel")))Assert.InRange(field.Value.GetDouble(),0,1);
        }
        var plan=NativeQa.NativeQaModes.Find("ability-memory").Plan;Assert.Equal(AbilityMemoryUiFixture.Known,plan.Abilities);Assert.Contains(plan.AfterHelpers,sql=>sql.Contains("level=1,exp=0"));Assert.False(plan.DevCommands);Assert.False(plan.FixedCallbackRolls);Assert.False(plan.GmCharacter110);
        Assert.Contains("slot<520",AbilityMemoryUiFixture.SeedSql(110));
    }
    [Fact] public void MarginTupleDecoderPreservesSlotFamilyAndActualTrainingBits()
    {
        var row=Assert.Single(AbilityMemoryUiFixture.Decode(Convert.FromHexString("0e0001640380")));
        Assert.Equal((ushort)14,row.Slot);Assert.Equal(0x80036400u,row.Descriptor);Assert.Equal((ushort)1,row.Level);
        Assert.Throws<ArgumentException>(()=>AbilityMemoryUiFixture.Decode(new byte[5]));
    }
}
