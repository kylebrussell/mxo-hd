using hds;
using HeadlessClient;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public class MervAfter301207Tests
{
    private static string Root {get{string p=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(p,"data","missions")))p=Path.GetDirectoryName(p)!;return p;}}
    private static List<Mission> Load()=>Directory.GetFiles(Path.Combine(Root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    [Fact] public void ActualFloodCriticalOffersDefineFiveMissionsAndNextModBox()
    {
        var all=Load();var contact=MissionContacts.ForOrg(3,0);var history=MervAfter301207Fixture.Prerequisites.ToHashSet();
        Assert.Equal(27,history.Count);Assert.Empty(history.Intersect(MervAfter301207Fixture.Missions));
        foreach(int id in MervAfter301207Fixture.Missions.Append(301214))
        {
            var offers=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),history);
            Assert.Equal(id,Assert.Single(offers,m=>m.plot).id);history.Add(id);
        }
        Assert.Equal(new[]{1208,1209,1210,1211,1212},MervAfter301207Fixture.Missions.Select(id=>all.Single(m=>m.id==id).order));
    }
    [Fact] public void SelectedStoriesKeepLiteralPayoutAndAuthoredActionProperties()
    {
        var all=Load();foreach(var id in MervAfter301207Fixture.Missions)
        {
            var m=all.Single(m=>m.id==id);var expected=MervAfter301207Fixture.Payouts[id];Assert.Equal((int)expected.Xp,m.exp);Assert.Equal((int)expected.Info,m.info);Assert.Equal(3,m.org);
        }
        var hive=all.Single(m=>m.id==301210);Assert.Equal(3,hive.phases.Count);
        Assert.Contains(hive.phases[0].missionObjectives,o=>o.type==MissionGoalType.Loot&&o.item==6854);
        Assert.Contains(hive.phases[1].missionObjectives,o=>o.type==MissionGoalType.Search&&o.item==6782&&o.optional);
        var box=all.Single(m=>m.id==301211);Assert.Contains(box.phases[0].missionObjectives,o=>o.type==MissionGoalType.Loot&&o.item==6835);
        var research=all.Single(m=>m.id==301212);Assert.Equal(3,research.phases.Count);
        Assert.Contains(research.phases[1].missionObjectives,o=>o.type==MissionGoalType.Search&&o.item==6827);
        Assert.Contains(research.phases[2].missionObjectives,o=>o.type==MissionGoalType.Give&&o.item==6827&&o.npcId==2);
        var goals=MervAfter301207Fixture.Missions.SelectMany(id=>all.Single(m=>m.id==id).phases).SelectMany(p=>p.missionObjectives);
        Assert.DoesNotContain(goals,o=>o.type==MissionGoalType.Upload||o.type==MissionGoalType.Put||o.type==MissionGoalType.Escort);
    }
    [Fact] public void DiscoveredNativeModeSeedsOnlyEarlierReceiptsAndLayout()
    {
        var mode=NativeQa.NativeQaModes.Find("merovingian-after-301207");Assert.IsType<MervAfter301207Mode>(mode);var p=mode.Plan;
        Assert.Equal(MervAfter301207Fixture.SeedPrerequisites(110),p.AfterHelpers[0]);Assert.Empty(p.Bootstrap);Assert.False(p.GmCharacter110);Assert.False(p.FixedCallbackRolls);
        Assert.Equal("301208",Assert.Single(p.EnvironmentOverrides).Value);Assert.Equal((17000,2300),(p.X,p.Z));Assert.Equal(new ushort[]{2,17,1006,77},p.Abilities);
        Assert.DoesNotContain(p.AfterHelpers,s=>s.Contains("char_active_missions")||s.Contains("INSERT INTO inventory"));
        Assert.Single(Scenarios.All,s=>s.Name==mode.Name);
        Assert.Equal("c7984328d5f8af46c0e57c218ff1d4ab9a061a46",p.ExtraQaMetadata["QaBaseMaster"].GetString());
        Assert.Equal("PR241 measured-introduction (MissionInteriorSites/MissionProxyIntroduction)",p.ExtraQaMetadata["CombinedCoreDependency"].GetString());
    }
}
