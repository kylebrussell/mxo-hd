namespace NativeQa.Modes;
using HeadlessClient;
public sealed class DataMinePrefabDoorMode : NativeQaMode
{
    public override string Name => "datamine-prefab-door";
    public override NativeQaModePlan Plan { get; } = WorldDoorModePlan.Create(NativeWorldDoorFixtures.DataMinePrefab);
}
