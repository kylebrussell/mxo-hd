using System.Reflection;
using HeadlessClient;
namespace hds.Tests;
public class MervFollowupStandaloneScopeTests
{
    [Fact] public void PrimaExtensionsAreDefaultOffForEveryExistingCaller()
    {
        var method=typeof(Scenarios).GetMethod("RunPrimaChain",BindingFlags.NonPublic|BindingFlags.Static)!;
        foreach(string name in new[]{"relogAfterChain","checkpointRecreatedCarriedNpcs","strictFollowupApproach"})
            Assert.Equal(false,method.GetParameters().Single(p=>p.Name==name).DefaultValue);
        Assert.Null(method.GetParameters().Single(p=>p.Name=="actorLevel").DefaultValue);
        Assert.Single(Scenarios.All,s=>s.Name=="merv-critical-followup");
    }
}
