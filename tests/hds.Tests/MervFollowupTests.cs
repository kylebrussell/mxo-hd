using System.Xml;
using hds;
using HeadlessClient;
using NativeQa.Modes;
namespace hds.Tests;
public class MervFollowupTests
{
    private static List<Mission> Load()=>Directory.GetFiles(DataLoader.DataPath("missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    [Fact] public void ContiguousFiveOffersRequireOnlyTwentyTwoEarlierReceipts()
    {
        var all=Load();var contact=MissionContacts.ForOrg(3,0);Assert.Equal(2002u,contact.id);
        var done=MervFollowupFixture.Prerequisites.ToHashSet();Assert.Equal(22,done.Count);Assert.Empty(done.Intersect(MervFollowupFixture.Missions));
        var selected=all.Where(m=>MervFollowupFixture.Missions.Contains(m.id)).OrderBy(m=>m.order).ToArray();
        Assert.Equal(MervFollowupFixture.Missions,selected.Select(m=>m.id));Assert.Equal(new[]{1203,1204,1205,1206,1207},selected.Select(m=>m.order));
        Assert.Equal(11,selected.Sum(m=>m.phases.Count));Assert.Equal(40,selected.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));Assert.Equal(3,selected.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count(o=>o.type==MissionGoalType.Protect))));
        foreach(var m in selected){Assert.Equal("Flood",m.contact);Assert.Equal((byte)3,m.org);var offers=DataLoader.ContactOfferWithStandards(all.Where(x=>MissionContacts.Offers(contact,x)).OrderBy(x=>x.order).ToList(),done);Assert.Equal(m.id,Assert.Single(offers.Where(x=>x.plot)).id);done.Add(m.id);}
        var next=DataLoader.ContactOfferWithStandards(all.Where(x=>MissionContacts.Offers(contact,x)).OrderBy(x=>x.order).ToList(),done);
        Assert.Equal(MervFollowupFixture.NextMission,Assert.Single(next.Where(x=>x.plot)).id);
    }
    [Fact] public void SafeAlarmWaveWaitsForActualUnlockAndDiskStillBlocksCompletion()
    {
        var m=Load().Single(m=>m.id==301204);var p=new MissionProgress(m){phaseIndex=1};p.UpdateArrivals();Assert.False(p.enemiesJustArrived);
        foreach(int id in new[]{1,2})p.Complete(p.Phase.missionObjectives.Single(o=>o.goal==id));Assert.False(p.enemiesJustArrived);Assert.False(p.AllDone);
        p.Complete(p.Phase.missionObjectives.Single(o=>o.goal==3));Assert.True(p.enemiesJustArrived);Assert.Equal(4,Assert.Single(p.wavesJustArrived).count);Assert.False(p.AllDone);Assert.Contains(p.OpenObjectives(),o=>o.goal==4&&o.type==MissionGoalType.Search);
    }
    [Fact] public void NativeProfileSeedsNoSelectedCompletionAndPreservesExistingPolicies()
    {
        var plan=new MervCriticalFollowupMode().Plan;Assert.Equal(MervFollowupFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);Assert.Equal("301101",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Equal(new ushort[]{2,17,45,1006,77},plan.Abilities);
        Assert.Equal("08d961eb47810f754d3ab4cd55cd23f7dcc8b897",plan.ExtraQaMetadata["QaBaseMaster"].GetString());
        Assert.Equal("PR249",plan.ExtraQaMetadata["SharedPlacementDependency"].GetString());
        Assert.Equal("measured introduction/native floor preselection/cache/fresh generated Safe assignment",plan.ExtraQaMetadata["SharedPlacementScope"].GetString());
        Assert.Contains("merovingian-critical-followup",NativeQa.NativeQaModes.Names);
        Assert.DoesNotContain("merovingian-after-301207",NativeQa.NativeQaModes.Names);
    }
    [Fact] public void HeadlessArrivalOracleEnablesPacketLoggingOnlyForItsOwnProfile()
    {
        var profile=Scenarios.All.Single(s=>s.Name=="merv-critical-followup");
        Assert.Equal("1",profile.Environment!["MXO_PACKET_LOG"]);
    }
    [Fact] public void EarlierReceiptsAreCopiedIntoTheCharacterSeedBeforeMarginWithoutSelectedOrNextOffer()
    {
        var original=new CharacterSeed("Merv"){Level=50,Experience=(int)PlayerProgression.ExperienceForLevel(50)};
        var prior=MervFollowupFixture.Prerequisites.ToArray();
        var seed=MervFollowupFixture.BeforeMargin(original,MervFollowupFixture.Missions,prior,MervFollowupFixture.NextMission);
        Assert.Null(original.CompletedMissions);Assert.Equal(22,seed.CompletedMissions!.Count);
        Assert.Equal(prior,seed.CompletedMissions);prior[0]=999;Assert.DoesNotContain(999,seed.CompletedMissions);
        Assert.Equal(original.Level,seed.Level);Assert.Equal(original.Experience,seed.Experience);Assert.Empty(seed.Items);
    }
    [Theory][InlineData("duplicate-prior")][InlineData("duplicate-request")][InlineData("selected-prior")][InlineData("next-prior")][InlineData("next-request")][InlineData("invalid")][InlineData("already-seeded")]
    public void InvalidReceiptPackagesRefuseBeforeAnyContextOrDatabaseIsNeeded(string fault)
    {
        var prior=MervFollowupFixture.Prerequisites.ToArray();var requested=MervFollowupFixture.Missions.ToArray();var seed=new CharacterSeed("Merv");
        switch(fault){case "duplicate-prior":prior[1]=prior[0];break;case "duplicate-request":requested[1]=requested[0];break;
            case "selected-prior":prior[0]=requested[0];break;case "next-prior":prior[0]=MervFollowupFixture.NextMission;break;
            case "next-request":requested[0]=MervFollowupFixture.NextMission;break;case "invalid":prior[0]=0;break;
            case "already-seeded":seed=seed with {CompletedMissions=new[]{1}};break;}
        Assert.Throws<ArgumentException>(()=>MervFollowupFixture.BeforeMargin(seed,requested,prior,MervFollowupFixture.NextMission));
    }
}
