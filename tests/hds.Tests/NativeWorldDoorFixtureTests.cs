using System.Globalization;
using System.Text.Json;
using HeadlessClient;
using NativeQa;
using hds;
namespace hds.Tests;

public class NativeWorldDoorFixtureTests
{
    private static string Root
    {
        get { var p = AppContext.BaseDirectory; while (!File.Exists(Path.Combine(p, "hds.sln"))) p = Path.GetDirectoryName(p)!; return p; }
    }

    public static IEnumerable<object[]> Fixtures()
    {
        yield return new object[] { NativeWorldDoorFixtures.Large01 };
        yield return new object[] { NativeWorldDoorFixtures.Large02 };
    }

    [Theory, MemberData(nameof(Fixtures))]
    public void DiagnosticMatchesOneSafeNativeKeyAndAnActualNearbyInteriorFloor(NativeWorldDoorFixture f)
    {
        var row = Assert.Single(File.ReadLines(Path.Combine(Root, "data", $"staticObjects_{f.District}.csv"))
            .Skip(1).Select(l => l.Split(',')), r => BitConverter.ToUInt32(Convert.FromHexString(r[2])) == f.StaticId);
        Assert.Equal(f.World.ToString(CultureInfo.InvariantCulture), row[0]);
        Assert.Equal(f.Sector.ToString(CultureInfo.InvariantCulture), row[1]);
        Assert.Equal((uint)f.LocalId, BitConverter.ToUInt32(Convert.FromHexString(row[3])));
        Assert.Equal((uint)f.Goid, BitConverter.ToUInt32(Convert.FromHexString(row[4])));
        Assert.Equal(f.DoorX, double.Parse(row[6], CultureInfo.InvariantCulture));
        Assert.Equal(f.DoorY, double.Parse(row[7], CultureInfo.InvariantCulture));
        Assert.Equal(f.DoorZ, double.Parse(row[8], CultureInfo.InvariantCulture));
        Assert.Equal("0000000000000000000000000000803f", row[10]);
        using var audit = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "research", "world-data", $"{f.District}-static-audit.json")));
        var raw = Assert.Single(audit.RootElement.GetProperty("raw").EnumerateArray(),
            r => r[0].GetInt32() == f.Sector && r[1].GetInt32() == f.LocalId);
        Assert.Equal(0, raw[5].GetInt32());
        Assert.DoesNotContain(audit.RootElement.GetProperty("excludedKeys").EnumerateArray(),
            r => r[0].GetInt32() == f.Sector && r[1].GetInt32() == f.LocalId);
        NavMesh.UseDirectory(Path.Combine(Root, "data", "navmesh"));
        Assert.True(NavMesh.Locate(f.World, f.X, f.Y, f.Z, out var floor));
        Assert.Equal(f.Sector, floor.sector); Assert.True(floor.indoors); Assert.Equal(f.Y, floor.floorY + 95);
    }

    [Theory, MemberData(nameof(Fixtures))]
    public void ExistingContractOffersOpenAndOnlyItsNaturalClosingSequence(NativeWorldDoorFixture f)
    {
        ObjectStates.Load(Path.Combine(Root, "data"));
        Assert.Equal(typeof(AttributeClass364), typeof(GameObjectDefinitions).GetField("Object" + f.Goid)!.FieldType);
        Assert.Equal(0x835u, ObjectStates.DefaultState(f.Goid));
        var open = ObjectStates.FindUsable(f.Goid, 3);
        Assert.NotNull(open); Assert.Equal("Open", open.label); Assert.Equal(4004u, open.executeRule);
        Assert.Contains((byte)3, ObjectStates.Offered(f.Goid, 0x835));
        Assert.True(Math.Sqrt(Math.Pow(f.DoorX - f.X, 2) + Math.Pow(f.DoorY - f.Y, 2) + Math.Pow(f.DoorZ - f.Z, 2)) < open.maxDistance);
        Assert.DoesNotContain(ObjectStates.Usables(f.Goid), u => u.label == "Close");
        var opened = ObjectStates.Find(f.Goid, 0x834); var closing = ObjectStates.Find(f.Goid, 0x838);
        Assert.NotNull(opened); Assert.NotNull(closing);
        Assert.Equal(20u, opened.seconds); Assert.Equal(0x838u, opened.next);
        Assert.Equal(1u, closing.seconds); Assert.Equal(0x835u, closing.next);
    }

    [Theory, MemberData(nameof(Fixtures))]
    public void ActualDiscoveredProfileHasSafeSeedAndNoAdditionalPrivileges(NativeWorldDoorFixture f)
    {
        Assert.Contains(f.Mode, NativeQaModes.Names);
        var plan = NativeQaModes.Find(f.Mode).Plan;
        Assert.Equal(f.X, plan.X); Assert.Equal(f.Z, plan.Z);
        Assert.Equal(new ushort[] { 2, 17 }, plan.Abilities);
        Assert.False(plan.DevCommands); Assert.False(plan.GmCharacter110); Assert.False(plan.FixedCallbackRolls);
        Assert.Empty(plan.BeforeLoadout); Assert.Empty(plan.AfterHelpers); Assert.Empty(plan.EnvironmentOverrides);
        var seed = Assert.Single(plan.AfterLoadout);
        Assert.Contains($"district='{f.District}',districtId={f.World},y={f.Y},rotation=0", seed);
        Assert.Equal(f.StaticId, plan.ExtraQaMetadata["DoorStaticId"].GetUInt32());
        Assert.True(plan.ExtraQaMetadata["NaturalClosingOnly"].GetBoolean());
        Assert.False(plan.ExtraQaMetadata["WalkRouteProven"].GetBoolean());
        Assert.False(plan.ExtraQaMetadata["LoadingAreaUiReturnProven"].GetBoolean());
        var env = NativeQaFixturePlan.EnvironmentFor(f.Mode);
        Assert.Equal("0", env["MXO_DEV_COMMANDS"]); Assert.Equal("", env["MXO_GM_CHARACTER_IDS"]);
        Assert.Equal("", env["MXO_CALLBACK_QA_ROLL15"]); Assert.Equal("", env["MXO_CALLBACK_QA_ROLL100"]);
        var normal = NativeQaModes.Find(f.District).Plan;
        Assert.NotSame(normal, plan); Assert.False(normal.ExtraQaMetadata.ContainsKey("WorldDoorDiagnostic"));
    }
}
