namespace NativeQa.Modes;

/// <summary>Exercise native Abort after a real relog, using existing fixed mission acceptance.</summary>
public sealed class MissionTeamRelogMode : NativeQaMode
{
    public override string Name => "mission-team-relog";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] {2,17},23828,13984,
        Array.Empty<string>(),
        new[] { "INSERT IGNORE INTO char_missions(charId,missionId) VALUES(110,2201);" },
        Array.Empty<string>(),DevCommands:true,
        extraQaMetadata:new Dictionary<string,object?> { ["MissionTeamRelogQa"]=true,["MissionId"]=2202 },
        readyMessages:new[] { "Mission team relog: ?missionpersist start; Get from Herschel; logout/relog after normal reservation cleanup; Mission window Abort then Yes. Check the Abort prompt, actual 80a6 request, mission end and empty checkpoint. See docs/MISSION-TEAM-RELOG.md." });
}
