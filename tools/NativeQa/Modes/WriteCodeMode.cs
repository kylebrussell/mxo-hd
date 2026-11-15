namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class WriteCodeMode : NativeQaMode
{
    public override string Name => "write-code";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,37,277 }, 23828, 13984,
        new string[] {  },
        new string[] { @"
DELETE FROM inventory WHERE charId=110 AND slot<97;
INSERT INTO inventory(charId,goid,slot,count,purity) VALUES
(110,1271,1,1,0),(110,531,2,1,0),(110,2399,3,1,0),
(110,7332,4,20,0),(110,7333,5,20,0),(110,7334,6,20,0),(110,7335,7,20,0),
(110,7336,8,20,0),(110,7337,9,20,0),(110,7338,10,20,0),(110,7339,11,20,0);
",
            $@"
UPDATE characters SET cash=100000,healthC=500,healthM=500 WHERE charId=110;
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name)
 VALUES(110,400,{NativeQaFixturePlan.CompileArchiveValue},1,0,'Antibiotic item code: native Compile UI refusal fixture');
" },
        new string[] { @"UPDATE characters SET innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114)",
            $@"UPDATE characters SET level=20,exp={hds.PlayerProgression.ExperienceForLevel(20)} WHERE charId=6111" },
        DevCommands: true, GmCharacter110: false, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false,
        extraQaMetadata: new Dictionary<string,object?> { ["WriteCodeQa"]=true });
}
