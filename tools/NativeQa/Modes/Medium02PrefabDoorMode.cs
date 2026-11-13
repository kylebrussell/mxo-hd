using HeadlessClient;
namespace NativeQa.Modes;
public sealed class Medium02PrefabDoorMode : NativeQaMode
{
    public override string Name => "medium02-prefab-door";
    public override NativeQaModePlan Plan {get;} = WorldDoorModePlan.Create(NativeWorldDoorFixtures.Medium02Prefab);
}
