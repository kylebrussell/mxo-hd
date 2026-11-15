namespace NativeQa.Modes;
/// <summary>Low-client native diagnostics only; authenticated capacity runs use LoadTest in a separate quiet window.</summary>
public sealed class ScaleDiagnosticsMode : NativeQaMode
{
    public override string Name=>"scale-diagnostics";
    public override NativeQaModePlan Plan {get;}=new(new ushort[]{2,17},23828,13984,Array.Empty<string>(),Array.Empty<string>(),Array.Empty<string>(),
        DevCommands:true,environmentOverrides:new Dictionary<string,string>{{hds.ScaleDiagnostics.Variable,"1"}},
        extraQaMetadata:new Dictionary<string,object?>{{"ScaleDiagnostics",true},{"CapacityBenchmark",false}},
        readyMessages:new[]{"Low-client fixture diagnostics only: aggregate[SCALE] windows in disposable server output. Walk, use Actions and an NPC radial, then actual hardline radial Exit to Loading Area and return; confirm menus/responding and no visibility errors. This mode does not start50/100/200 load or establish capacity."});
}
