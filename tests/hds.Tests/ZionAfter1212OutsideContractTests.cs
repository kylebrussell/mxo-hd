using HeadlessClient;
namespace hds.Tests;
public class ZionAfter1212OutsideContractTests
{
    [Fact] public void ExistingTripsStillWaitForCommittedPhaseAndHaveNoRequestHookByDefault()
    {
        var m=typeof(Scenarios).GetMethod("ZionAfter1212Ride",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        Assert.Equal(true,m.GetParameters().Single(p=>p.Name=="waitForPhaseAdvance").DefaultValue);
        Assert.Null(m.GetParameters().Single(p=>p.Name=="beforeRequest").DefaultValue);
    }
}
