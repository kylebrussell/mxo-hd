using HeadlessClient;
namespace NativeQa.Modes;
public sealed class SatiPrefabDoorMode : NativeQaMode
{
    public override string Name => "sati-prefab-door";
    public override NativeQaModePlan Plan {get;} = WorldDoorModePlan.Create(NativeWorldDoorFixtures.SatiPrefab);
}
