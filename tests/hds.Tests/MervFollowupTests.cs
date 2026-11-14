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
    }
}
