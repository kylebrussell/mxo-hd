using HeadlessClient;
using NativeQa;
namespace hds.Tests;

public class Class364DoorsModeTests
{
    [Fact] public void DiagnosticUsesTheExistingPrimaryWorldFixtureAndOrdinaryProtocol()
    {
        Assert.Single(Scenarios.All,s=>s.Name=="class364-doors");
        Assert.Single(NativeQaModes.Names,n=>n=="class364-doors");
        var plan=NativeQaModes.Find("class364-doors").Plan;var fixture=NativeWorldDoorFixtures.Medium03Prefab;
        Assert.Equal(16,fixture.World);Assert.Equal(580,fixture.Goid);
        Assert.True(hds.MissionInteriorDoorTypes.IsSupported(fixture.Goid));
        Assert.Equal(fixture.X,plan.X);Assert.Equal(fixture.Z,plan.Z);
        Assert.True(plan.ExtraQaMetadata["NaturalClosingOnly"].GetBoolean());
        Assert.False(plan.ExtraQaMetadata["WalkRouteProven"].GetBoolean());
        Assert.Contains(plan.ReadyMessages,s=>s.Contains("Right-click Open")&&s.Contains("direct start"));
    }
}
