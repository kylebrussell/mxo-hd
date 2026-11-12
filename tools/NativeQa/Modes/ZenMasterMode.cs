namespace NativeQa.Modes;

/// <summary>Exact native-PASS legacy99 fixture; production level99 prerequisite remains authoritative.</summary>
public sealed class ZenMasterMode : NativeQaMode
{
    public override string Name => "zen-master";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] {2,17,871},23828,13984,
        new string[] { @"UPDATE characters SET level=99,healthC=100,healthM=500,innerStrC=100,innerStrM=200 WHERE charId=110" },
        new string[] {  },
        new string[] { @"UPDATE characters SET innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111,6112,6113,6114); DELETE FROM inventory WHERE charId IN (110,6111,6112,6113,6114) AND slot=1; INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,615,1,1,0),(6111,615,1,1,0),(6112,615,1,1,0),(6113,615,1,1,0),(6114,615,1,1,0)",
        @"UPDATE characters SET innerStrC=100,innerStrM=200 WHERE charId=110",
        $@"UPDATE characters SET level=20,exp={hds.PlayerProgression.ExperienceForLevel(20)} WHERE charId=6111" },
        DevCommands:true, SeedDisguiseTool:true, FixedCallbackRolls:true, HealthCallbackQa:true,
        extraQaMetadata:new Dictionary<string,object?> { ["ZenMasterForcedLevel"]=99 });
}
