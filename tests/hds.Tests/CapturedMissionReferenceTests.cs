using System.Reflection;
using System.Text.Json;
using System.Xml;
using hds;
using HeadlessClient;
namespace hds.Tests;
public class CapturedMissionReferenceTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static Mission Load(string name){var d=new XmlDocument();d.Load(Path.Combine(Root,"data","missions",name));return MissionXml.Parse(d.DocumentElement!);}
    [Theory]
    [InlineData("machines_1_2_1_disturbing_trends.xml",54871u,45339u,17917u,15415u,168042u,133350u)]
    [InlineData("machines_1_2_2_poster_hunt.xml",132000u,59500u,43102u,20230u,404250u,175000u)]
    public void ObservedArchivePointAnchorsExistingPolicyAndExtrapolationIsExplicit(string file,uint xp,uint info,uint old17Xp,uint old17Info,uint new50Xp,uint new50Info)
    {
        var mission=Load(file);Assert.Equal(17,mission.rewardLevel);Assert.Equal(xp,(uint)mission.exp);Assert.Equal(info,(uint)mission.info);
        var captured=MissionRewards.Calculate(mission,17);Assert.Equal(xp,captured.Experience);Assert.Equal(info,captured.Info);
        var extrapolated=MissionRewards.Calculate(mission,50);Assert.Equal(new50Xp,extrapolated.Experience);Assert.Equal(new50Info,extrapolated.Info);
        Assert.True(extrapolated.Experience>xp);Assert.True(extrapolated.Info>info);
        mission.rewardLevel=0;var before=MissionRewards.Calculate(mission,17);Assert.Equal(old17Xp,before.Experience);Assert.Equal(old17Info,before.Info);
        before=MissionRewards.Calculate(mission,50);Assert.Equal(xp,before.Experience);Assert.Equal(info,before.Info);
    }
    [Theory]
    [InlineData("machines_1_2_3_hard_time.xml",88000,59500,28734u,20230u)]
    [InlineData("zion_1_2_2_the_power_of_propaganda.xml",137500,91000,44897u,30940u)]
    public void BorrowedAndUnknownLevelMissionsRetainTheDefaultPolicy(string file,int xp,int info,uint lowXp,uint lowInfo)
    {
        var mission=Load(file);Assert.Equal(0,mission.rewardLevel);Assert.Equal(xp,mission.exp);Assert.Equal(info,mission.info);
        var quote=MissionRewards.Calculate(mission,17);Assert.Equal(lowXp,quote.Experience);Assert.Equal(lowInfo,quote.Info);
    }
    private static CharacterSeed Seed(int? level)=>(CharacterSeed)typeof(Scenarios).GetMethod("PrimaActorSeed",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object?[]{"Compatibility",(byte)2,17000d,2300d,level})!;
    [Fact] public void OptionalProfilePreservesEveryLegacySeedValueByDefault()
    {
        var old=new CharacterSeed("Compatibility"){Level=50,X=17000,Z=2300,Health=5000,MaxHealth=5000,InnerStrength=2000,MaxInnerStrength=2000,Organization=2};
        Assert.Equal(JsonSerializer.Serialize(old),JsonSerializer.Serialize(Seed(null)));
        var parameter=typeof(Scenarios).GetMethod("RunPrimaChain",BindingFlags.NonPublic|BindingFlags.Static)!.GetParameters().Last();Assert.Equal("actorLevel",parameter.Name);Assert.True(parameter.HasDefaultValue);Assert.Null(parameter.DefaultValue);
        foreach(int level in new[]{17,50}){var seed=Seed(level);Assert.Equal(level,seed.Level);Assert.Equal((int)PlayerProgression.ExperienceForLevel(level),seed.Experience);}
    }
    [Theory][InlineData(0)][InlineData(51)]
    public void ExplicitProfileRejectsInvalidLevelInsteadOfChangingUnrelatedPolicy(int level)
    {var error=Assert.Throws<TargetInvocationException>(()=>Seed(level));Assert.IsType<ArgumentOutOfRangeException>(error.InnerException);}
    [Theory][InlineData("captured-rewards-17",17,132000u,59500u)][InlineData("captured-rewards-50",50,404250u,175000u)]
    public void NativeProfilesUseActualLevelAndNormalPrerequisitesOnly(string name,int level,uint xp,uint info)
    {
        var plan=NativeQa.NativeQaModes.Find(name).Plan;Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Equal(PosterHuntFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.DoesNotContain(201202,PosterHuntFixture.Prerequisites);Assert.Equal(21,PosterHuntFixture.Prerequisites.Length);
        Assert.Equal(CapturedMissionRewardsFixture.ActorSql(level),plan.AfterHelpers[1]);Assert.Equal("201202",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
        Assert.Equal(xp,plan.ExtraQaMetadata["CapturedRewardExpectedXp"].GetUInt32());Assert.Equal(info,plan.ExtraQaMetadata["CapturedRewardExpectedInfo"].GetUInt32());
        Assert.NotNull(Scenarios.All.Single(s=>s.Name==name));
    }
}
