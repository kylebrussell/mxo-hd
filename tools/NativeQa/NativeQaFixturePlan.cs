namespace NativeQa;

/// <summary>The actual environment passed to the disposable native-QA server, also available without setup.</summary>
public static class NativeQaFixturePlan
{
    // CompileUI 620827d0 excludes0x84 bit/fragment entries; low seven bits store charges.
    public const uint CompileArchiveValue=2399u << 7;
    public static bool SeedDisguiseTool(string mode) => NativeQaModes.Find(mode).Plan.SeedDisguiseTool;

    public static Dictionary<string, string> EnvironmentFor(string mode)
    {
        var plan=NativeQaModes.Find(mode).Plan;
        var environment=new Dictionary<string,string>()
    {
        ["MXO_DEV_COMMANDS"] = plan.DevCommands ? "1" : "0",
        ["MXO_AUTH_PORT"] = "11000", ["MXO_MARGIN_PORT"] = "10000", ["MXO_WORLD_PORT"] = "10000",
        ["MXO_CONSOLE_PORT"] = "55557", ["MXO_MOBS"] = "test", ["MXO_MOB_WANDER"] = "0",
        ["MXO_AGGRO"] = "0", ["MXO_PACKET_LOG"] = "1",
        ["MXO_GM_CHARACTER_IDS"] = plan.GmCharacter110 ? "110" : ""
        , ["MXO_CALLBACK_QA_ROLL15"] = plan.FixedCallbackRolls ? "0" : ""
        , ["MXO_CALLBACK_QA_ROLL100"] = plan.FixedCallbackRolls ? "0" : ""
    };
        foreach(string key in NativeQaModes.EnvironmentKeys) environment.TryAdd(key,"");
        foreach(var pair in plan.EnvironmentOverrides) environment[pair.Key]=pair.Value;
        return environment;
    }
}
