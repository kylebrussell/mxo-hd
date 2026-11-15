using HeadlessClient;
namespace NativeQa.Modes;

public sealed class Large02DoorMode : NativeQaMode
{
    private static readonly NativeWorldDoorFixture Fixture = NativeWorldDoorFixtures.Large02;
    public override string Name => Fixture.Mode;
    public override NativeQaModePlan Plan { get; } = WorldDoorModePlan.Create(Fixture);
}
