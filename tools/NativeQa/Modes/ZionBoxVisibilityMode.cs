using hds;
using HeadlessClient;
namespace NativeQa.Modes;
public sealed class ZionBoxVisibilityMode:NativeQaMode
{
    public override string Name=>"zion-box-visibility-diagnostic";
    public static Dictionary<string,string> Environment()=>InteriorAdmissionFixture.Environment(InteriorAdmissionFixture.Zion).Concat(new[]{new KeyValuePair<string,string>(ZionBoxVisibilityProbe.EnvironmentKey,"1")}).ToDictionary(x=>x.Key,x=>x.Value);
    private static NativeQaModePlan Create()
    {
        var p=InteriorAdmissionPlans.For(InteriorAdmissionFixture.Zion);
        return new(p.Abilities.ToArray(),p.X,p.Z,p.BeforeLoadout.ToArray(),p.AfterLoadout.ToArray(),p.AfterHelpers.ToArray(),
            DevCommands:true,GmCharacter110:false,FixedCallbackRolls:false,environmentOverrides:Environment(),
            extraQaMetadata:new(){["VisibilityDiagnosticOnly"]=true,["SelectedMissionInjected"]=false,["RequiredMission"]=101209,["RequiredPhase"]=0,["RequiredSource"]="0d100397",["SouthOffsets"]="0,-50,-100,-150,-200,-300",["PrivateMissionKeyControls"]="zero or same-current-instance, no executor"},
            readyMessages:new[]{"CSR110 only. Preserve ordinary Zion profile7800/95/13795, layoutseed101208, earlier receipts. Call Tyndall > Critical Hardline Hunter > Download/Accept; use ordinary elevator. Do not Open anything yet. If actual101209phase0 object1 is not raw0d100397 at23814.611328125/840/14241.5712890625, STOP; do not reroll/force source. Compare original alone, then ?zionbox 0 private key0 duplicate at identical pose; off restores original-alone. Next ?zionbox same duplicates at offset0 with exact current original instance key; capture beforeOpen, then off. This late creation control separates key filtering from room-entry/creation timing. Then ?zionbox -50, -100, -150, -200, -300, one private lease at a time, unchanged height/quaternion/state/defaults. Fixed clear camera/avatar, before ANY Open, record visibility/hover at every step plus ordinary avatar walk/?save wall measurements. Off removes only duplicate. No probe executor; do not claim genericOpen behavior. See docs/ZION-BOX-VISIBILITY-DIAGNOSTIC.md."});
    }
    public override NativeQaModePlan Plan {get;}=Create();
}
