namespace NativeQa.Modes;
public sealed class MissionPublicStreetAccessMode:NativeQaMode
{
    public override string Name=>"mission-public-street-access";
    public override NativeQaModePlan Plan{get;}=Create();
    static NativeQaModePlan Create()
    {
        var p=new MissionNpcRoomRolesMode().Plan;
        return new(p.Abilities.ToArray(),p.X,p.Z,p.BeforeLoadout.ToArray(),p.AfterLoadout.ToArray(),p.AfterHelpers.ToArray(),DevCommands:p.DevCommands,GmCharacter110:p.GmCharacter110,
            FixedCallbackRolls:false,environmentOverrides:new(p.EnvironmentOverrides),
            extraQaMetadata:new(){["MissionId"]=101213,["EarlierReceiptsOnly"]=true,["SelectedProgressSeeded"]=false,["PublicStreetPolicy"]=1,["AuditedWorld1Scenes"]=370,["NativePassageProven"]=false},
            readyMessages:new[]{"CSR110: Missions/Phone > Tyndall > Critical101213. Earlier receipts only; accept normally. Follow the PUBLIC street-door waypoint, not a car point inside the facade. From the street require actual Door/Open hover at the saved StreetAccess public key; Open normally and WALK through the original opening. Once inside the lobby, follow the car waypoint and use the ordinary elevator. Verify visible mission actors. On return, ride down and walk out through the public door even if the phase already advanced. No missionfloor, TP, source/seed retry, hidden Open or selected progress/item grants qualifies. Stop on brick/no hover/blocked passage and retain full PacketLog + checkpoint before cleanup. This first native gate is unproved. See docs/MISSION-PUBLIC-STREET-ACCESS.md."});
    }
}
