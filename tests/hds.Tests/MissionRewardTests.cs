using hds;
using System.Xml;
namespace hds.Tests;
public class MissionRewardTests
{
    [Theory]
    [InlineData(2,5500u,21000u)] [InlineData(3,11000u,31500u)]
    [InlineData(1,5500u,10500u)] [InlineData(0,5500u,10500u)]
    [InlineData(50,269500u,525000u)] [InlineData(99,269500u,525000u)]
    public void ExplicitWelcomePolicyMatchesCaptureAnchorsAndBounds(int level,uint xp,uint info)
    {
        var reward=MissionRewards.Calculate(new Mission{exp=5500,info=21000,rewardLevel=2},level);
        Assert.Equal(xp,reward.Experience);Assert.Equal(info,reward.Info);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(51)]
    public void UnknownReferenceUsesExplicitArchiveFiftyBaselineWithoutHugeEarlyReward(int reference)
    {
        var mission=new Mission{exp=440000,info=115500,rewardLevel=reference};
        var cap=MissionRewards.Calculate(mission,50);Assert.Equal(440000u,cap.Experience);Assert.Equal(115500u,cap.Info);
        var low=MissionRewards.Calculate(mission,1);Assert.Equal(8979u,low.Experience);Assert.Equal(2310u,low.Info);
        uint previousXp=0,previousInfo=0;
        for(int level=1;level<=50;level++)
        {
            var reward=MissionRewards.Calculate(mission,level);
            Assert.InRange(reward.Experience,previousXp,440000u);Assert.InRange(reward.Info,previousInfo,115500u);
            previousXp=reward.Experience;previousInfo=reward.Info;
        }
    }
    [Fact]
    public void IntegerFloorZeroAndDatabaseIntegerCapAreExplicitPolicy()
    {
        var floor=MissionRewards.Calculate(new Mission{exp=239105,info=105000,rewardLevel=30},31);
        Assert.Equal(247350u,floor.Experience);Assert.Equal(108500u,floor.Info);
        var zero=MissionRewards.Calculate(new Mission{exp=-1,info=-2,rewardLevel=2},50);
        Assert.Equal(0u,zero.Experience);Assert.Equal(0u,zero.Info);
        var cap=MissionRewards.Calculate(new Mission{exp=int.MaxValue,info=int.MaxValue,rewardLevel=2},50);
        Assert.Equal((uint)int.MaxValue,cap.Experience);Assert.Equal((uint)int.MaxValue,cap.Info);
    }
    [Theory]
    [InlineData("",0)] [InlineData(" rewardLevel='30'",30)]
    public void XmlReferenceIsOptInRatherThanInferredFromRewardAmount(string attribute,int reference)
    {
        var doc=new XmlDocument();doc.LoadXml("<mission title='Fixture' exp='239105' info='105000'"+attribute+"><phase/></mission>");
        Assert.Equal(reference,MissionXml.Parse(doc.DocumentElement!).rewardLevel);
    }
    [Fact]
    public void AllAuthoredReferencesAreBoundedAndKnownTicketGatesStayConsistent()
    {
        string folder=DataLoader.DataPath("missions");int anchored=0,unknown=0;
        foreach(string path in Directory.GetFiles(folder,"*.xml"))
        {
            var doc=new XmlDocument();doc.Load(path);var mission=MissionXml.Parse(doc.DocumentElement!);
            if(mission.rewardLevel==0){unknown++;continue;}
            anchored++;Assert.InRange(mission.rewardLevel,1,50);
            if(mission.id==1201)Assert.Equal(2,mission.rewardLevel);
            else if(mission.id is 312101 or 312102 or 312103)
                Assert.Equal((int)mission.minLevel,mission.rewardLevel);
            var reward=MissionRewards.Calculate(mission,mission.rewardLevel);
            Assert.Equal((uint)mission.exp,reward.Experience);Assert.Equal((uint)mission.info,reward.Info);
        }
        Assert.True(anchored>=4);Assert.True(unknown>0);
    }
    [Theory]
    [InlineData("mission-rewards",2,5500u,21000u)]
    [InlineData("mission-rewards-higher",3,11000u,31500u)]
    public void DiscoveredNativePlansUseMatchingActualXpLevelWithoutCombatRngOverride(string mode,int level,uint xp,uint info)
    {
        var plan=NativeQa.NativeQaModes.Find(mode).Plan;
        Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(level,plan.ExtraQaMetadata["MissionRewardsStartLevel"].GetInt32());
        Assert.Equal(xp,plan.ExtraQaMetadata["MissionRewardsExpectedXp"].GetUInt32());
        Assert.Equal(info,plan.ExtraQaMetadata["MissionRewardsExpectedInfo"].GetUInt32());
        Assert.Contains("exp="+PlayerProgression.ExperienceForLevel(level),Assert.Single(plan.AfterHelpers));
        var env=NativeQa.NativeQaFixturePlan.EnvironmentFor(mode);
        Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);
        Assert.Empty(plan.EnvironmentOverrides);
    }

}
