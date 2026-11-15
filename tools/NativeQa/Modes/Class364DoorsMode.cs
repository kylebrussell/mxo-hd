using HeadlessClient;
namespace NativeQa.Modes;

public sealed class Class364DoorsMode : NativeQaMode
{
    public override string Name=>"class364-doors";
    public override NativeQaModePlan Plan {get;}=WorldDoorModePlan.Create(
        NativeWorldDoorFixtures.Medium03Prefab with { Mode="class364-doors" });
}
