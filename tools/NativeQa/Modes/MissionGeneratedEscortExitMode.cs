using hds;

namespace NativeQa.Modes;

public sealed class MissionGeneratedEscortExitMode : NativeQaMode
{
    public override string Name=>"mission-generated-escort-exit";
    // Actual earlier receipt closure for a fresh Tyndall 101210 offer. No selected/future receipt.
    public static IReadOnlyList<int> EarlierMissions {get;}=Array.AsReadOnly(new[]{1201,1204,1203,1205,1206,1207,1208,
        2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,
        101201,101202,101203,101204,101205,101206,101207,101208,101209});
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17,45,1006,77},38525,-20696,
        Array.Empty<string>(),Array.Empty<string>(),new[]{
            "INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",EarlierMissions.Select(id=>$"(110,{id})")),
            "UPDATE characters SET level=50,exp="+PlayerProgression.ExperienceForLevel(50)+",y=95,alignment=1,healthC=healthM,innerStrC=innerStrM WHERE charId=110"},
        DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="101208"},
        extraQaMetadata:new(){["GeneratedEscortExitQa"]=true,["MissionId"]=101210,["EarlierReceiptCount"]=29,
            ["NoActiveProgressSeed"]=true,["LayoutSeedOnly"]=true},
        readyMessages:new[]{"Generated escort exit: Phone > Tyndall > Critical > Interview with a Bluepill. Accept normally; use the ordinary waypoint door, Talk to Maynard and confirm following. Walk/hover the same floor door with Maynard nearby and following. Confirm the ordinary downride reaches the street BEFORE the exit goal completes or the next waypoint appears. Continue ordinary Ride to Lonny and Talk. Earlier 29 receipts only; no active progress, mission item or selected completion seeded. No mission-floor teleport, forced source, GM or combat roll override. See docs/MISSION-GENERATED-ESCORT-EXIT.md."});
}
