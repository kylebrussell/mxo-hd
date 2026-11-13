using hds;
namespace NativeQa.Modes;
/// <summary>Normal Tyndall Courier UI; the strict QA seed changes site selection only.</summary>
public sealed class MissionCourierMode : NativeQaMode
{
    public override string Name=>"mission-courier";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},23828,13984,
        Array.Empty<string>(),Array.Empty<string>(),
        new[]{"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",alignment=1,y=495,cash=1000 WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="20001"},
        extraQaMetadata:new(){["MissionCourierQa"]=true,["CourierMissionId"]=20001,["CourierDefinitionPolicy"]="OURS; retail title/contact/plot0 only",
            ["CourierLayoutSeedOnly"]=true,["CourierExpectedXp"]=22000,["CourierExpectedInfo"]=31500},
        readyMessages:new[]{"Contacts > Tyndall > Call > Courier; next critical remains available. Normal Talk/Get Data Disk from Leto, leave, Talk/Give to Neris, leave. Relog after Get; repeat after completion. No mission/items/receipts pregranted; see docs/MISSION-COURIER.md."});
}
