using hds;
using NativeQa;
namespace hds.Tests;
public class MissionLootClaimCloseModeTests
{
    [Fact]
    public void DiscoveredModeSeedsOnlyPrerequisitesAndOrdinaryBagFiller()
    {
        var mode=NativeQaModes.Find("mission-loot-claim-close"); Assert.Equal("mission-loot-claim-close",mode.Name);
        var p=mode.Plan; Assert.False(p.GmCharacter110); Assert.False(p.FixedCallbackRolls); Assert.True(p.DevCommands);
        Assert.Equal("220110",p.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
        Assert.Equal(96,p.ExtraQaMetadata["BagFillerSlots"].GetInt32());
        Assert.False(p.ExtraQaMetadata["NativeUiClosureProven"].GetBoolean());
        string sql=string.Join("\n",p.AfterHelpers);
        Assert.Contains("(110,1,2568,1,0)",sql); Assert.Contains("(110,96,2568,1,0)",sql);
        Assert.DoesNotContain("6799",sql); Assert.DoesNotContain("char_active_missions",sql);
        Assert.DoesNotContain("201201",sql); Assert.DoesNotContain("TheNeo",sql);
        Assert.Contains("Hover/radial Open",Assert.Single(p.ReadyMessages));
        Assert.Single(HeadlessClient.Scenarios.All,s=>s.Name=="mission-loot-claim-close");
    }
}
