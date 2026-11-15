using HeadlessClient;
using hds;
namespace hds.Tests;
public class MissionReceiptSeedGuardTests
{
    [Fact] public void EarlierReceiptsAreCopiedIntoTheCharacterSeedBeforeMarginWithoutSelectedOrNextOffer()
    {
        var original=new CharacterSeed("Merv"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50)};
        var prior=MervAfter301207Fixture.Prerequisites.ToArray();
        var seed=MissionReceiptSeedGuard.BeforeMargin(original,MervAfter301207Fixture.Missions,prior,MervAfter301207Fixture.NextMission);
        Assert.Null(original.CompletedMissions);Assert.Equal(27,seed.CompletedMissions!.Count);
        Assert.Equal(prior,seed.CompletedMissions);prior[0]=999;Assert.DoesNotContain(999,seed.CompletedMissions);
        Assert.Equal(original.Level,seed.Level);Assert.Equal(original.Experience,seed.Experience);Assert.Empty(seed.Items);
    }
    [Theory][InlineData("duplicate-prior")][InlineData("duplicate-request")][InlineData("selected-prior")][InlineData("next-prior")][InlineData("next-request")][InlineData("invalid")][InlineData("already-seeded")]
    public void InvalidReceiptPackagesRefuseBeforeAnyContextOrDatabaseIsNeeded(string fault)
    {
        var prior=MervAfter301207Fixture.Prerequisites.ToArray();var requested=MervAfter301207Fixture.Missions.ToArray();var seed=new CharacterSeed("Merv");
        switch(fault){case "duplicate-prior":prior[1]=prior[0];break;case "duplicate-request":requested[1]=requested[0];break;
            case "selected-prior":prior[0]=requested[0];break;case "next-prior":prior[0]=MervAfter301207Fixture.NextMission;break;
            case "next-request":requested[0]=MervAfter301207Fixture.NextMission;break;case "invalid":prior[0]=0;break;
            case "already-seeded":seed=seed with {CompletedMissions=new[]{1}};break;}
        Assert.Throws<ArgumentException>(()=>MissionReceiptSeedGuard.BeforeMargin(seed,requested,prior,MervAfter301207Fixture.NextMission));
    }
}
