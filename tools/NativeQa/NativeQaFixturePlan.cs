namespace NativeQa;

/// <summary>The actual environment passed to the disposable native-QA server, also available without setup.</summary>
public static class NativeQaFixturePlan
{
    public static Dictionary<string, string> EnvironmentFor(string mode) => new()
    {
        ["MXO_DEV_COMMANDS"] = mode is "remaining" or "ignored-actions" or "coder-pets" or "styles" or
            "data-mining" or "death-presentation" or "actions" or "health-callbacks" or "compile-items" ? "1" : "0",
        ["MXO_AUTH_PORT"] = "11000", ["MXO_MARGIN_PORT"] = "10000", ["MXO_WORLD_PORT"] = "10000",
        ["MXO_CONSOLE_PORT"] = "55557", ["MXO_MOBS"] = "test", ["MXO_MOB_WANDER"] = "0",
        ["MXO_AGGRO"] = "0", ["MXO_PACKET_LOG"] = "1",
        ["MXO_GM_CHARACTER_IDS"] = mode == "ignored-actions" ? "110" : ""
        , ["MXO_CALLBACK_QA_ROLL15"] = mode == "health-callbacks" ? "0" : ""
        , ["MXO_CALLBACK_QA_ROLL100"] = mode == "health-callbacks" ? "0" : ""
    };
}
