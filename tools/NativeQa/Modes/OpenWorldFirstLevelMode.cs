using HeadlessClient;
namespace NativeQa.Modes;
public sealed class OpenWorldFirstLevelMode:NativeQaMode
{
    public override string Name=>"open-world-first-level";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2},19403,60401,new[]{"DELETE FROM inventory WHERE charId=110"},
        new[]{"UPDATE char_abilities SET level=1 WHERE char_id=110"},
        new[]{"UPDATE characters SET level=1,exp=0,cash=1000,y=-705,healthC=150,healthM=150,innerStrC=100,innerStrM=100,pvpflag=0 WHERE charId=110"},
        environmentOverrides:OpenWorldFirstLevelFixture.Environment,
        readyMessages:new[]{"First earned level: level1/XP0/HP150/IS100, only Awakened2 at1. Existing Chopper Runner1/75HP at19403,-705,60701 and20097,-705,61491. Normal melee, no weaken/kill/addexp/forced spawns; two391XP kills should earn level2/782XP. Loot each corpse, then /quit, wait33s and relog SAME fixture without reseed. Allow up to8min; STOP on death, inaccessible target or refusal. See docs/OPEN-WORLD-FIRST-LEVEL.md. Native difficulty, walking/picking and presentation unproved; inherited mode bootstrap does not earn the level."});
}
