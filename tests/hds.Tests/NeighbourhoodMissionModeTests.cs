using HeadlessClient;
using NativeQa;
using hds;
namespace hds.Tests;
public class NeighbourhoodMissionModeTests
{
    [Fact] public void DiscoveredProfileUsesGroundedContactNormalLevelAndOnlyLayoutSeed()
    {
        var mode=NativeQaModes.Find("neighbourhood-retrieval");Assert.Equal("neighbourhood-retrieval",mode.Name);
        var p=mode.Plan;Assert.Equal(76015,p.X);Assert.Equal(15845,p.Z);
        Assert.Equal(1282500L,PlayerProgression.ExperienceForLevel(10));
        Assert.Contains("level=10,exp=1282500",Assert.Single(p.AfterHelpers));Assert.Contains("y=485,rotation=128",p.AfterHelpers[0]);
        Assert.True(p.DevCommands);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);Assert.Empty(p.Bootstrap);
        Assert.Equal(new ushort[]{2,17},p.Abilities);Assert.Equal(3072,p.ExtraQaMetadata["NeighbourhoodMissionId"].GetInt32());
        var env=NativeQaFixturePlan.EnvironmentFor(mode.Name);Assert.Equal("3072",env["MXO_MISSION_SITE_QA_SEED"]);
        Assert.Equal("",env["MXO_CALLBACK_QA_ROLL15"]);Assert.Equal("",env["MXO_CALLBACK_QA_ROLL100"]);
        Assert.Equal("",NativeQaFixturePlan.EnvironmentFor("unknown-mode")["MXO_MISSION_SITE_QA_SEED"]);
    }
    [Fact] public void StoredCourierOrOtherAuthoredIdsRemainAnIndependentAuthority()
    {
        var authored=new Mission{id=20001,title="Stored"};Assert.Same(authored,MissionDefinitions.Find(20001,new[]{authored}));
        Assert.Null(MissionDefinitions.Find(20001,Array.Empty<Mission>()));
        Assert.Equal(3072,NeighbourhoodMissionFixture.Retrieval);
    }
}
