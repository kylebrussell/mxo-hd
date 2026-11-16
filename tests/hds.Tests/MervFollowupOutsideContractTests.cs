using HeadlessClient;
namespace hds.Tests;
public class MervFollowupOutsideContractTests
{
    [Fact] public void ExistingCallersKeepCompletedPhaseDepartureByDefault()
    {
        var method=typeof(Scenarios).GetMethod("MervFollowupRide",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        Assert.Equal(false,method.GetParameters().Single(p=>p.Name=="unfinishedOutside").DefaultValue);
    }
    [Fact] public void RequestHookIsDefaultNullForExistingTrips()
    {
        var m=typeof(Scenarios).GetMethod("MervFollowupRide",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        Assert.Null(m.GetParameters().Single(p=>p.Name=="beforeRequest").DefaultValue);
    }
}
