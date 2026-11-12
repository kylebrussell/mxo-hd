using HeadlessClient;
using System.Text.Json;
namespace NativeQa.Modes;

public abstract class BuffDispelFixtureMode : NativeQaMode
{
    protected BuffDispelFixtureMode(int roll)
    {
        Plan=new(BuffDispelFixture.Abilities,23828,13984,Array.Empty<string>(),Array.Empty<string>(),
            new[]{"UPDATE characters SET alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END,pvpflag=1,healthC=10000,healthM=10000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)"},
            DevCommands:true,FixedCallbackRolls:true,Bootstrap:"buff-dispel-records",
            environmentOverrides:new(){["MXO_CALLBACK_QA_ROLL100"]=roll.ToString(System.Globalization.CultureInfo.InvariantCulture)},
            extraQaMetadata:new(){["BuffDispelQa"]=true,["BuffDispelProcRoll100"]=roll,["BuffDispelExpectedProc"]=roll<20,
                ["BuffDispelTarget"]="QAEnemy",["BuffDispelBuffIds"]="49,327",["BuffDispelAura"]=42,["BuffDispelAuraEffect"]=695,["BuffDispelOnRecords"]=5});
    }
    public override NativeQaModePlan Plan { get; }
    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        foreach(string name in context.HelperNames)
        {
            var helper=await HeadlessPlayer.LoginAsync(new ServerAddress("127.0.0.1",11000,10000,10000),name,name);
            context.Helpers.Add(name,helper); await helper.EnterWorldAsync();
        }
        var records=await BuffDispelFixture.PrepareTargetAsync(context.Helpers["QAEnemy"]);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory,"buff-dispel-ready.json"),JsonSerializer.Serialize(new {
            Target="QAEnemy",Organization=2,PvpFlag=true,Buffs=new[]{49,327},Aura=42,AuraEffect=695,
            ForcedRoll=Plan.ExtraQaMetadata["BuffDispelProcRoll100"].GetInt32(),OnRecords=records.Select(WireFormat.ToHex).ToArray(),At=DateTime.UtcNow}));
        return Array.Empty<byte>();
    }
}

public sealed class BuffDispelMode : BuffDispelFixtureMode
{
    public BuffDispelMode():base(0) {}
    public override string Name=>"buff-dispel";
}
