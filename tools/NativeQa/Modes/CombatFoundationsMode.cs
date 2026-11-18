using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class CombatFoundationsMode : NativeQaMode
{
    public override string Name => "combat-foundations";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 158, 330, 493, 1006 }, 17000, 2300,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,y=495 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND slot IN (1,2,105); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,692,1,1,0),(110,7899,2,1,0)"
        }, DevCommands:true, FixedCallbackRolls:false, environmentOverrides:new()
        {
            ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, readyMessages:new[]
        {
            "Enter ?combatdepth foundations-sight, equip the bag slot 1 rifle, and try ranged fire and Logic Barrage 4.0 against AB1 Blocked. Try Logic Barrage against AB1 Clear and check for 424 damage.",
            "Enter ?combatdepth foundations-cone. Equip the bag slot 2 dual SMGs and aim Direct Fire at AB1 Aim. Only AB1 Aim and AB1 Inside should take damage. Logic Barrage against AB1 Resisted should deal 348 damage. See docs/COMBAT-FOUNDATIONS-QA.md."
        });
}
