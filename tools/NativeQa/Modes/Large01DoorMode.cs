using HeadlessClient;
namespace NativeQa.Modes;

public sealed class Large01DoorMode : NativeQaMode
{
    private static readonly NativeWorldDoorFixture Fixture = NativeWorldDoorFixtures.Large01;
    public override string Name => Fixture.Mode;
    public override NativeQaModePlan Plan { get; } = WorldDoorModePlan.Create(Fixture);
}
