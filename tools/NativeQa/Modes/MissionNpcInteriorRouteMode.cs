using HeadlessClient;
namespace NativeQa.Modes;
public sealed class MissionNpcInteriorRouteMode:NativeQaMode
{
    public override string Name=>MissionNpcInteriorProof.Name;
    public override NativeQaModePlan Plan {get;}
    public MissionNpcInteriorRouteMode()
    {
        var original=InteriorAdmissionPlans.For(InteriorAdmissionFixture.NpcInteriorRoute);
        Plan=new NativeQaModePlan(original.Abilities.ToArray(),original.X,original.Z,original.BeforeLoadout.ToArray(),original.AfterLoadout.ToArray(),original.AfterHelpers.ToArray(),
            DevCommands:original.DevCommands,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:original.EnvironmentOverrides.ToDictionary(x=>x.Key,x=>x.Value),
            extraQaMetadata:new(){["MissionId"]=201201,["EarlierReceiptsOnly"]=true,["SelectedProgressSeeded"]=false,["NpcInteriorVersion"]=2,["ExpectedStoryPhase"]=2,["ExpectedNpc"]=6,["WireNpcPhase"]=0,["NativeDoorHoverRequired"]=true,["NoPreclickPublicationChange"]=true,["OuterMinutes"]=6,["FullCompletionProven"]=false},
            readyMessages:new[]{"CSR110. Missions/Phone > Agent Gray > Critical Disturbing Trends; normal offer/accept. Earlier receipts only, level50 normal abilities. Ordinary entry/Talk Ophiuchi1/exit; ordinary operations-post entry, current Computer Take MapDisk6783, ordinary exit. Require ACTUAL selected phase2 NPC-v2 witness; no forced Tabor, source, pose or old-witness injection. Before clicking, inspect actual metal Door/Open at the CURRENT persisted Lobby/Floor position from its saved Standing. STOP/HOLD if absent; no missionfloor/TP/synthetic Open. Ordinary carry logout/relog (allow normal33s reservation cleanup), exact disk owner/slot, actual measured entry. Follow only current certified topology and required own-GO wooden Door/Open; native walking/clearance is a separate observed gate. Reach current Ophiuchi6, Inventory disk/Give, ordinary measured exit. Require one168042XP/133350Info mission summary, retained NULL-payload CAS/one receipt and next actual Poster Hunt201202 offer. No whole-site/native success is claimed by headless. See docs/MISSION-NPC-INTERIOR-ROUTE.md. --fixture-plan is read-only inspection."});
    }
}
