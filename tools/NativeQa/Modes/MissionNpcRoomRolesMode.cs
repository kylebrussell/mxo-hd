using hds;
namespace NativeQa.Modes;
public sealed class MissionNpcRoomRolesMode : NativeQaMode
{
    public override string Name => "mission-npc-room-roles";
    public static readonly int[] EarlierReceipts = {1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207,101208,101209,101210,101211,101212};
    public override NativeQaModePlan Plan { get; } = new(new ushort[]{2,17,1006,77},17000,2300,
        new[]{"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",EarlierReceipts.Select(id=>$"(110,{id})"))},
        Array.Empty<string>(),new[]{"UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=495,alignment=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101213",["MXO_PACKET_LOG"]="1"},
        extraQaMetadata:new(){["MissionId"]=101213,["EarlierReceiptsOnly"]=true,["SelectedProgressSeeded"]=false,["RolePlan"]=1,["SceneDoorClass"]=364,["SceneDoorTypes"]=MissionInteriorDoorTypes.SupportedGoids.Count,["NativePassageProven"]=false},
        readyMessages:new[]{"CSR110. Tyndall Call > Critical It's Hard To Find Good Help. Same normal origin/layout profile as the earlier Zion wave, earlier receipts only. Accept normally; do not force842 or another site. Follow current saved Floor/Lobby/Standing. Require metal Door/Open at its measured position, ordinary entry on building side, then actual supported door Open and passage. Talk/escort/combat as authored. On the partner phase, record all current NPC positions: RolePlan1 must keep the authored role order in distinct measured source-marker spots in a sufficient-capacity original room (or its documented certified expansion). Verify Antilope/Reitman/Bornmann are findable via ordinary doors; stop on missing hover/passage, retain checkpoint and PacketLog. No missionfloor, TP, source/seed retry, progress/item grants or hidden Open qualifies native proof. See docs/MISSION-NPC-ROOM-ROLES.md. The separate mission-npc-interior-route mode covers normal201201."});
}
