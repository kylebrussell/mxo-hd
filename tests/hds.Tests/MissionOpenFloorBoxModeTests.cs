using NativeQa.Modes;
using HeadlessClient;
namespace hds.Tests;
public class MissionOpenFloorBoxModeTests
{
    [Fact] public void NormalOfferOriginAndPrerequisitesDoNotInjectAPlacement()
    {
        var mode=new MissionOpenFloorBoxMode();var p=mode.Plan;
        Assert.Equal("mission-open-floor-box",mode.Name);
        Assert.Equal((17000,2300),(p.X,p.Z));Assert.Empty(p.Bootstrap);
        Assert.Equal(PosterHuntFixture.SeedPrerequisites(110),p.AfterHelpers[0]);
        Assert.Equal("201202",p.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
        Assert.Single(p.EnvironmentOverrides);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.True(p.ExtraQaMetadata["OpenFloorWitnessRequired"].GetBoolean());
        Assert.Equal(3,p.ExtraQaMetadata["ExpectedFirstPhaseObjects"].GetInt32());
        Assert.Contains("never Centre",p.ReadyMessages.Single());
        Assert.DoesNotContain(p.AfterHelpers,s=>s.Contains("char_active_missions")||s.Contains("0d1003dc"));
        Assert.DoesNotContain(201202,PosterHuntFixture.Prerequisites);
    }
}
