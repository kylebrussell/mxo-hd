using HeadlessClient;
namespace NativeQa.Modes;
public sealed class SatiDoorMode : NativeQaMode
{
    private static readonly NativeWorldDoorFixture Fixture=NativeWorldDoorFixtures.Sati;
    public override string Name=>Fixture.Mode;
    public override NativeQaModePlan Plan{get;}=WorldDoorModePlan.Create(Fixture);
}
