using NativeQa.Modes;

namespace hds.Tests;

[CollectionDefinition("CatalogSessionEnvironment",DisableParallelization=true)]
public class CatalogSessionEnvironment { }

[Collection("CatalogSessionEnvironment")]
public class MissionCatalogSessionTests
{
    [Fact]
    public void ThreeOrganisationsShareOneFixtureWithoutGrantingTheirSelectedCompletions()
    {
        string? prior=Environment.GetEnvironmentVariable("MXO_QA_MISSION_ID");
        try
        {
            Environment.SetEnvironmentVariable("MXO_QA_MISSION_ID","101216,201209,301210");
            var mode=new MissionCatalogSampleMode();var plan=mode.Plan;
            Assert.Same(plan,mode.Plan);Assert.True(plan.DevCommands);
            Assert.Equal("catalog",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
            string receipts=Assert.Single(plan.AfterHelpers.Where(s=>s.StartsWith("INSERT IGNORE INTO char_missions")));
            foreach(int id in new[]{101216,201209,301210})Assert.DoesNotContain($"(110,{id})",receipts);
            Assert.Contains("(110,101212)",receipts);
            foreach(int org in new[]{1,2,3})Assert.Contains(plan.ReadyMessages,s=>s.Contains("?org "+org));
            Environment.SetEnvironmentVariable("MXO_QA_MISSION_ID","101216,101216");
            Assert.Throws<ArgumentException>(()=>mode.Plan);
        }
        finally{Environment.SetEnvironmentVariable("MXO_QA_MISSION_ID",prior);}
    }
}
