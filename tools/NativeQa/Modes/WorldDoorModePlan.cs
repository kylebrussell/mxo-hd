using HeadlessClient;
namespace NativeQa.Modes;

internal static class WorldDoorModePlan
{
    internal static NativeQaModePlan Create(NativeWorldDoorFixture f) => new(new ushort[] { 2, 17 }, f.X, f.Z,
        Array.Empty<string>(),
        new[] { $"UPDATE characters SET district='{f.District}',districtId={f.World},y={f.Y},rotation={f.Heading} WHERE charId=110" },
        Array.Empty<string>(),
        extraQaMetadata: new()
        {
            ["WorldDoorDiagnostic"] = true, ["WorldMap"] = f.World, ["DoorGoid"] = f.Goid,
            ["DoorStaticId"] = f.StaticId, ["StandingY"] = f.Y, ["PlayerHeadingByte"] = f.Heading,
            ["NaturalClosingOnly"] = true, ["WalkRouteProven"] = false, ["LoadingAreaUiReturnProven"] = false
        },
        readyMessages: new[]
        {
            $"DIAGNOSTIC START: {f.District} at {f.X},{f.Y},{f.Z}, beside source door{f.Goid} {f.Sector}/{f.LocalId}. Right-click Open; observe natural closing after20s and closed1s later. No manual Close usable is sourced. This direct start proves neither a walk route nor Loading Area UI return. See NATIVE-WORLD-DOORS.md." + (f.ExpectedQuaternion == null ? "" : $" Primary quaternion bytes {f.ExpectedQuaternion}; verify native frame/pivot alignment. See WORLD-MEDIUM03-PREFAB-ROTATIONS.md.")
        });
}
