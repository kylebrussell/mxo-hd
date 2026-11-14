using System.Xml;
using hds;
using HeadlessClient;
using NativeQa.Modes;
using NativeQa;
namespace hds.Tests;
public class ZionAfter1212Tests
{
    static List<Mission> Load()
    {
        string root=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(root,"data","missions")))root=Path.GetDirectoryName(root)!;
        return Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    }
    [Fact] public void ActualTyndallOrderSelectsFiveThenCrossesChapterBoundary()
    {
        var all=Load();var contact=MissionContacts.ForOrg(1,0);var history=ZionAfter1212Fixture.Prerequisites.ToHashSet();
        Assert.Equal(32,history.Count);Assert.Empty(history.Intersect(ZionAfter1212Fixture.Missions));
        foreach(int id in ZionAfter1212Fixture.Missions.Append(ZionAfter1212Fixture.NextMission))
        {
            var offers=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),history);
            Assert.Equal(id,Assert.Single(offers.Where(m=>m.plot)).id);Assert.Contains(offers,m=>m.id==20001&&!m.plot);history.Add(id);
        }
        var selected=all.Where(m=>ZionAfter1212Fixture.Missions.Contains(m.id)).OrderBy(m=>m.order).ToList();
        Assert.Equal(new[]{1213,1214,1215,1216,1217},selected.Select(m=>m.order));
        Assert.Equal(18,selected.Sum(m=>m.phases.Count));Assert.Equal(50,selected.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        Assert.Equal(ZionAfter1212Fixture.MissionExperience,selected.Select(m=>(uint)m.exp));
        Assert.Equal(ZionAfter1212Fixture.MissionInformation,selected.Select(m=>(uint)m.info));
    }
    [Fact] public void DuplicateFragmentHoldersAndItemSubstitutionRemainAuthored()
    {
        var m=Load().Single(m=>m.id==101214);Assert.Equal(2,m.npcs.Count(n=>n.handle=="Agent Lee"));
        var loot=m.phases[0].missionObjectives;var uploads=m.phases[1].missionObjectives;
        Assert.Equal(new uint[]{6785,6787,6831,6782},loot.Select(x=>x.item));
        Assert.Equal(loot.Select(x=>x.item),uploads.Select(x=>x.item));Assert.All(uploads,g=>Assert.Equal(MissionGoalType.Upload,g.type));
    }
    [Fact] public void DuplicateNamesUseCurrentPhasePoseAndSideAndKillOracleIsIndependent()
    {
        ViewCreation View(ushort view,double x,double y,bool enemy)=>new(view,599,1,new Dictionary<string,byte[]>{["CharacterName"]=System.Text.Encoding.ASCII.GetBytes("Agent Lee\0"),["Position"]=NumericalUtils.doublesToLtVector3d(x,y,100),["IsEnemy"]=new byte[]{(byte)(enemy?1:0)}});
        var mob=new MissionProgress.SavedMob{Name="Agent Lee",X=100,Y=495,Z=100,Hostile=false,Friendly=false,Npc=3};
        // Hostile is generated-wave membership. Scripted hostile NPCs can be Friendly=false/Hostile=false.
        var records=new[]{View(4,100,495,true),View(5,200,495,true),View(6,100,895,true),View(7,100,495,false),View(8,100,495,true)};
        Assert.Equal((ushort)8,ZionAfter1212Fixture.CurrentNpc(records,1,mob)!.ViewId);
        Assert.Null(ZionAfter1212Fixture.CurrentNpc(records,records.Length,mob));Assert.Null(ZionAfter1212Fixture.CurrentNpc(records,-1,mob));
        Assert.Equal(19204u,ZionAfter1212Fixture.ExpectedKill(50));
    }
    [Fact] public void NativeModeSeedsOnlyEarlierReceiptsAndNormalLevelCapProfile()
    {
        var mode=new ZionAfter1212Mode();Assert.IsType<ZionAfter1212Mode>(NativeQaModes.Find(mode.Name));
        var p=mode.Plan;Assert.Equal(ZionAfter1212Fixture.SeedPrerequisites(110),p.AfterHelpers[0]);
        Assert.False(p.FixedCallbackRolls);Assert.False(p.GmCharacter110);Assert.True(p.DevCommands);
        Assert.Equal("101213",Assert.Single(p.EnvironmentOverrides).Value);Assert.Equal(new ushort[]{2,17,1006,77},p.Abilities);
    }
}
