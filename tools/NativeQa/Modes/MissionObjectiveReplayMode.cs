namespace NativeQa.Modes;

/// <summary>Check distinct completed/unfinished objective rows after ordinary relog.</summary>
public sealed class MissionObjectiveReplayMode : NativeQaMode
{
    public override string Name => "mission-objectives-relog";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] {2,17},23828,13984,Array.Empty<string>(),
        new[] { "INSERT IGNORE INTO char_missions(charId,missionId) VALUES(110,2201);" },
        Array.Empty<string>(),DevCommands:true,
        extraQaMetadata:new Dictionary<string,object?> { ["MissionObjectiveReplayQa"]=true,["MissionId"]=2202 },
        readyMessages:new[] { "Objective replay: ?missionpersist start; Herschel right-click Get once. Compare the two identical-text objective rows (first checked, second unchecked), quit/relog after normal cleanup, then compare again. Get once more must grant only the second device. Finish with Mission Status Abort > Yes. See docs/MISSION-OBJECTIVE-REPLAY.md. No GM or forced RNG." });
}
