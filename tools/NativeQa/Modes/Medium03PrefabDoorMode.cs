using HeadlessClient;
namespace NativeQa.Modes;
public sealed class Medium03PrefabDoorMode : NativeQaMode
{
    public override string Name => "medium03-prefab-door";
    public override NativeQaModePlan Plan {get;} = WorldDoorModePlan.Create(NativeWorldDoorFixtures.Medium03Prefab);
}
