using System.Text;
namespace HeadlessClient;

public static partial class Scenarios
{
    private static Task SatiPrefabDoorDiagnostic(ScenarioContext context) =>
        NativeWorldDoorDiagnostic(context, NativeWorldDoorFixtures.SatiPrefab);
    private static Task DataMinePrefabDoorDiagnostic(ScenarioContext context) => NativeWorldDoorDiagnostic(context, NativeWorldDoorFixtures.DataMinePrefab);
    private static Task Medium03PrefabDoorDiagnostic(ScenarioContext context) =>
        NativeWorldDoorDiagnostic(context, NativeWorldDoorFixtures.Medium03Prefab);
    private static Task Large01DoorDiagnostic(ScenarioContext context) =>
        NativeWorldDoorDiagnostic(context, NativeWorldDoorFixtures.Large01);
    private static Task Large02DoorDiagnostic(ScenarioContext context) =>
        NativeWorldDoorDiagnostic(context, NativeWorldDoorFixtures.Large02);

    private static async Task NativeWorldDoorDiagnostic(ScenarioContext context, NativeWorldDoorFixture f)
    {
        var actor = (await context.AddPlayersInWorldAsync(new CharacterSeed(f.District + "DoorQA")
        {
            Level = 50, District = f.District, DistrictId = f.World,
            X = f.X, Y = f.Y, Z = f.Z, Rotation = f.Heading
        }))[0];
        Check.That(actor.Received("06").Any(r => Encoding.Latin1.GetString(r.Bytes).Contains(f.MetrResource)),
            "ordinary login loads the actual source world");
        Check.Equal($"{f.X} {f.Y} {f.Z}", await context.Database.ScalarAsync(
            $"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={actor.CharId}"), "exact diagnostic standing point");

        // Normal static-object Open, not a developer command or direct state mutation.
        actor.SendRpc($"80 c8 {WireFormat.ToHex(BitConverter.GetBytes(f.StaticId))} 03 00");
        bool IsSourceDoor(ViewCreation v) => v.Goid == f.Goid &&
            v.Attributes.TryGetValue("Position", out var p) && p.Length == 24 &&
            BitConverter.ToDouble(p) == f.DoorX && BitConverter.ToDouble(p, 8) == f.DoorY && BitConverter.ToDouble(p, 16) == f.DoorZ;
        await actor.WaitUntilAsync(() => actor.ViewsOfGoid(f.Goid).Any(IsSourceDoor),
            "the exact existing source door opens", TimeSpan.FromSeconds(10));
        var door = actor.ViewsOfGoid(f.Goid).Last(IsSourceDoor);
        if (f.ExpectedQuaternion != null)
            Check.That(door.Attributes["Orientation"].SequenceEqual(Convert.FromHexString(f.ExpectedQuaternion)),
                "actual wire contains the independent primary quaternion bytes, without normalization");
        Check.Equal(0x834u, BitConverter.ToUInt32(door.Attributes["CurrentState"]), "native OpenedState");
        byte[] closing = await actor.WaitForViewUpdateAsync(door.ViewId, "01 80 02 38 08 00 00", TimeSpan.FromSeconds(25));
        byte[] closed = await actor.WaitForViewUpdateAsync(door.ViewId, "01 80 02 35 08 00 00", TimeSpan.FromSeconds(5));
        Check.Equal(0, actor.World.Problems.Count, "door protocol parser");
        if (f.ExpectedQuaternion != null)
            await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), $"mxo-{f.Mode}-actual-wire.json"),
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    SourceAssembly = context.Server.SourceAssembly,
                    SourceConfiguration = new DirectoryInfo(Path.GetDirectoryName(context.Server.SourceAssembly)!).Parent!.Name,
                    SourceAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(context.Server.SourceAssembly))),
                    StaticId = f.StaticId, door.Goid, door.ViewId,
                    Position = Convert.ToHexString(door.Attributes["Position"]),
                    Orientation = Convert.ToHexString(door.Attributes["Orientation"]),
                    OpenedState = Convert.ToHexString(door.Attributes["CurrentState"]),
                    ClosingViewList = Convert.ToHexString(closing), ClosedViewList = Convert.ToHexString(closed),
                    ReceivedViewLists = actor.World.ViewSnapshot().Select(v=>Convert.ToHexString(v)).ToArray(),
                    NativeRenderingProven = false
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(), $"mxo-{f.Mode}-success-debug.log"),
            context.Server.FullOutput() + "\n--- DebugLog ---\n" + context.Server.DebugLog());
        Console.WriteLine($"  {f.Mode}: ordinary login; exact source{f.Goid}/{f.Sector}/{f.LocalId}; Open3/state834; natural838 then835. Direct diagnostic start, no walk-route/LA UI proof.");
    }
}
