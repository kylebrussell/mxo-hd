namespace NativeQa.Modes;

/// <summary>Compare goal rows after the real client completes loading a restored world.</summary>
public sealed class MissionObjectiveUiReplayMode : NativeQaMode
{
    public override string Name => "mission-objective-ui-replay";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] {2,17},23828,13984,Array.Empty<string>(),
        new[] { "INSERT IGNORE INTO char_missions(charId,missionId) VALUES(110,2201);" },
        Array.Empty<string>(),DevCommands:true,
        extraQaMetadata:new Dictionary<string,object?> { ["MissionObjectiveUiReplayQa"]=true,["MissionId"]=2202 },
        readyMessages:new[] { "?missionpersist start; Herschel right-click Get once; compare two same-label objective rows, first checked. Quit/relog after cleanup, wait one second after loading, compare rows again; Get again grants only second device; Abort > Yes then relog. Native display remains pending. See docs/MISSION-OBJECTIVE-UI-REPLAY.md." });
}
