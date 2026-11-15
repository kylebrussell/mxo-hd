namespace HeadlessClient;

public static partial class Scenarios
{
    // Reuse the previously audited source/world/quaternion fixture; no mission source is forced.
    private static Task Class364Doors(ScenarioContext context) => NativeWorldDoorDiagnostic(context,
        NativeWorldDoorFixtures.Medium03Prefab with { Mode="class364-doors" });
}
