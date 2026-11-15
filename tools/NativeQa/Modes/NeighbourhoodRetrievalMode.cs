using HeadlessClient;
namespace NativeQa.Modes;
public sealed class NeighbourhoodRetrievalMode : NativeQaMode
{
    public override string Name=>"neighbourhood-retrieval";
    public override NativeQaModePlan Plan {get;}=new(NeighbourhoodMissionFixture.Abilities,NeighbourhoodMissionFixture.X,NeighbourhoodMissionFixture.Z,
        Array.Empty<string>(),Array.Empty<string>(),new[]{NeighbourhoodMissionFixture.NativeActorSql},
        DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,
        environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]="3072"},
        extraQaMetadata:new(){["NeighbourhoodContactId"]=7,["NeighbourhoodMissionId"]=3072,["NeighbourhoodMissionLevel"]=10,["MissionSiteSeed"]=3072,["MissionDefinitionQa"]=true},
        readyMessages:new[]{"The Chessman is two metres ahead. Right-click Get Mission, choose Retrieval, accept, search the marked computer with its ordinary Take action, then leave the area. Relog after taking the data: the same mission/item/site should return. Finish and repeat once; XP/info should pay once per run. Layout-only seed; no completion or reward bypass. See docs/MISSION-NEIGHBOURHOOD-PERSISTENCE.md."});
}
