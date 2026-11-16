using hds;
namespace NativeQa.Modes;
public sealed class MissionNpcSceneCoverageMode : NativeQaMode
{
    public override string Name => "mission-npc-scene-coverage";
    public override NativeQaModePlan Plan { get; } = Create();
    static NativeQaModePlan Create()
    {
        var p=new MissionNpcRoomRolesMode().Plan;
        return new(p.Abilities.ToArray(),p.X,p.Z,p.BeforeLoadout.ToArray(),p.AfterLoadout.ToArray(),p.AfterHelpers.ToArray(),DevCommands:p.DevCommands,GmCharacter110:p.GmCharacter110,FixedCallbackRolls:p.FixedCallbackRolls,
            environmentOverrides:new(p.EnvironmentOverrides),
            extraQaMetadata:new(){["MissionId"]=101213,["EarlierReceiptsOnly"]=true,["SelectedProgressSeeded"]=false,["RolePlan"]=1,["SceneFormat"]=2,["AuditedScenes"]=370,["SelectableFloorScenes"]=260,["CertifiedSiteRows"]=3427,["FreshWorld1NpcOnly"]=true,["NativePassageProven"]=false},
            readyMessages:new[]{"FORMAT2 GENERAL COVERAGE: fresh101213 Critical offer via Missions/Phone > Tyndall; the original17000/495/2300 origin and101213 layout profile are unchanged. Only earlier receipts are seeded. Record the ordinarily selected scene; no site/seed retry. Follow persisted Floor/Lobby keys and Standing. Require actual metal Door/Open hover and building-side entry, then ordinary supported door Open/passage and visible current actors. On the partner phase, verify Antilope/Reitman/Bornmann are findable in the certified room/expansion through ordinary doors. For a newly covered scene record its sector, entry context, door keys and partner marker positions; if the normal choice is an older pilot, report that control honestly. No missionfloor/TP/progress/item grants. Stop and retain PacketLog/checkpoint on missing hover, passage or actors. See docs/MISSION-NPC-FORMAT2.md."});
    }
}
