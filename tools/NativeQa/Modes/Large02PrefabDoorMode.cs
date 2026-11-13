namespace NativeQa.Modes;
using HeadlessClient;
public sealed class Large02PrefabDoorMode : NativeQaMode
{
    public override string Name => "large02-prefab-door";
    public override NativeQaModePlan Plan { get; } = WorldDoorModePlan.Create(NativeWorldDoorFixtures.Large02Prefab);
}
