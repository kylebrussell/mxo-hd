namespace NativeQa.Modes;
public sealed class DesperationInitiativeMode : NativeQaMode
{
    public override string Name=>"desperation-initiative";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{1149},23828,13984,
        Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),DevCommands:true,
        environmentOverrides:new Dictionary<string,string>{["MXO_DESPERATION_QA_HOLD_HEALTH"]="1",
            ["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"});
}
