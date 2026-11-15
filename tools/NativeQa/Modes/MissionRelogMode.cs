namespace NativeQa.Modes;

/// <summary>Mission persistence uses ordinary mission NPC Get/Put and relog; no combat RNG is forced.</summary>
public sealed class MissionRelogMode : NativeQaMode
{
    public override string Name => "mission-relog";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] {2,17},23828,13984,
        Array.Empty<string>(),
        new string[] { "INSERT IGNORE INTO char_missions(charId,missionId) VALUES(110,2201);" },
        Array.Empty<string>(),DevCommands:true,
        extraQaMetadata:new Dictionary<string,object?> { ["MissionRelogQa"]=true,["MissionId"]=2202,["InitialPhase"]=1 },
        readyMessages:new[] { "Mission relog: ?missionpersist start begins Watchful Eyes phase1 through normal acceptance; ?missionpersist status reports checkpoint. Get from Herschel, relog, Get again, leave and Put; see docs/MISSION-PERSISTENCE.md." });
}
