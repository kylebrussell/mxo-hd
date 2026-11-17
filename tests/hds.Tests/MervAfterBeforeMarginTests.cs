using HeadlessClient;
using hds;
namespace hds.Tests;
public sealed class MervAfterBeforeMarginTests
{
    [Fact]public void ExactEarlierTwentySevenAreClonedBeforeAnyContextDatabaseOrMarginIsNeeded()
    {
        var original=new CharacterSeed("MervAfter207"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=3};
        var seed=MervAfter301207Fixture.BeforeMargin(original);
        Assert.Null(original.CompletedMissions);Assert.Equal(27,seed.CompletedMissions!.Count);Assert.Equal(27,seed.CompletedMissions.Distinct().Count());
        Assert.Equal(MervAfter301207Fixture.Prerequisites,seed.CompletedMissions);
        Assert.Empty(seed.CompletedMissions.Intersect(MervAfter301207Fixture.Missions.Append(MervAfter301207Fixture.NextMission)));
        Assert.NotSame(MervAfter301207Fixture.Prerequisites,seed.CompletedMissions);
        Assert.Equal(original.Experience,seed.Experience);Assert.Equal(original.Level,seed.Level);Assert.Empty(seed.Items);
    }
    [Theory][InlineData("duplicate-prior")][InlineData("duplicate-request")][InlineData("selected-prior")]
    [InlineData("next-prior")][InlineData("next-request")][InlineData("nonpositive")][InlineData("already-seeded")]
    public void InvalidReceiptSetsRefuseBeforeAnyOwnedMutation(string fault)
    {
        var prior=MervAfter301207Fixture.Prerequisites.ToArray();var requested=MervAfter301207Fixture.Missions.ToArray();var seed=new CharacterSeed("Merv");
        switch(fault){case "duplicate-prior":prior[1]=prior[0];break;case "duplicate-request":requested[1]=requested[0];break;
            case "selected-prior":prior[0]=requested[0];break;case "next-prior":prior[0]=MervAfter301207Fixture.NextMission;break;
            case "next-request":requested[0]=MervAfter301207Fixture.NextMission;break;case "nonpositive":prior[0]=0;break;
            case "already-seeded":seed=seed with{CompletedMissions=new[]{1201}};break;}
        Assert.Throws<ArgumentException>(()=>MervAfter301207Fixture.BeforeMargin(seed,requested,prior,MervAfter301207Fixture.NextMission));
    }
    [Fact]public void ActualProfileRequiresPacketEvidenceWithoutChangingOtherDriverDefaults()
    {
        var scenario=Assert.Single(Scenarios.All,s=>s.Name=="merovingian-after-301207");
        Assert.Equal("1",scenario.Environment!["MXO_PACKET_LOG"]);Assert.Equal("1",scenario.Environment["MXO_DEV_COMMANDS"]);
        Assert.Equal("301208",scenario.Environment["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal(TimeSpan.FromMinutes(15),scenario.Timeout);
        var opening=Assert.Single(Scenarios.All,s=>s.Name=="merovingian-critical-opening");Assert.False(opening.Environment!.ContainsKey("MXO_PACKET_LOG"));
    }
    [Fact]public void MandatorySourcePinRejectsUnpinnedOrWrongActualSource()
    {
        string? old=Environment.GetEnvironmentVariable(MervAfter301207Fixture.SourcePin);string actual=new('A',64);
        try
        {
            Environment.SetEnvironmentVariable(MervAfter301207Fixture.SourcePin,null);Assert.Throws<InvalidDataException>(()=>MervAfter301207Fixture.RequireSource(actual));
            Environment.SetEnvironmentVariable(MervAfter301207Fixture.SourcePin,new string('B',64));Assert.Throws<InvalidDataException>(()=>MervAfter301207Fixture.RequireSource(actual));
            Environment.SetEnvironmentVariable(MervAfter301207Fixture.SourcePin,actual);MervAfter301207Fixture.RequireSource(actual);
        }
        finally{Environment.SetEnvironmentVariable(MervAfter301207Fixture.SourcePin,old);}
    }
}
