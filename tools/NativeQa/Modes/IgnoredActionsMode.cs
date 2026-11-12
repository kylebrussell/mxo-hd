namespace NativeQa.Modes;

/// <summary>Disposable fixture configuration; loaded by NativeQaModes without a shared mode switch.</summary>
public sealed class IgnoredActionsMode : NativeQaMode
{
    public override string Name => "ignored-actions";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,18,52,128,137,155,1006,77,967,169,371,1064,1067,1089,383,315,333,254,1135,48,352,359,466,329,355,870,450,433,12,184,217,37,277,276,133,1112,147 }, 23828, 13984,
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
 VALUES(110,400,{hds.CodeStorage.BaseValue(7332)},0,0,'P7 native archive fixture');
" },
        new string[] {  },
        DevCommands: true, GmCharacter110: true, FixedCallbackRolls: false, SeedDisguiseTool: false,
        Bootstrap: "", StealthShieldQa: false, HealthCallbackQa: false, CompileItemsQa: false);
}
