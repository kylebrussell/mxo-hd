namespace NativeQa.Modes;

/// <summary>Actual compiler60 timing pair. No recipe, combat or random overrides.</summary>
public sealed class OrdinaryIcCompileMode : NativeQaMode
{
    public override string Name => "ordinary-ic-compile";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2,17,277 },23828,13984,Array.Empty<string>(),
        new[] { @"
DELETE FROM inventory WHERE charId=110 AND slot<97;
INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,1271,1,1,0);
DELETE FROM char_abilities WHERE char_id=110 AND slot IN (400,401);
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES
(110,400,5044864,2,0,'Max Health Boost Pill 3 code: two direct charges'),
(110,401,307072,1,0,'Health pill code: one direct charge');
UPDATE characters SET cash=200000 WHERE charId=110;
" },
        new[] { $@"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)} WHERE charId=110;
UPDATE char_abilities SET level=50 WHERE char_id=110 AND ability_id={0x80000000L | 277L << 10};
DELETE FROM char_abilities WHERE char_id=110 AND (slot IN (300,301,302,303) OR ability_id IN ({0x80000000L | 24L << 10},{0x80000000L | 36L << 10},{0x80000000L | 37L << 10},{0x80000000L | 1008L << 10}));
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(110,300,{0x80000000L | 24L << 10},1,0,'Minor Repairs');
INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES
(110,301,{0x80000000L | 36L << 10},1,0,'Tinkering'),(110,302,{0x80000000L | 37L << 10},1,0,'Write Code prerequisite (unloaded)'),
(110,303,{0x80000000L | 1008L << 10},1,0,'Coder Combat Style prerequisite (unloaded)');
UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={0x80000000L | 515L << 10};" },
        // Load owned37,1008,36 normally before the baseline;24 remains off.
        // Guarded ?qaload routes owned24 through the normal load handler in this disposable mode.
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,SeedDisguiseTool:false,
        Bootstrap:"",StealthShieldQa:false,HealthCallbackQa:false,CompileItemsQa:false,
        extraQaMetadata:new Dictionary<string,object?> { ["OrdinaryIcCompileQa"]=true });
}
