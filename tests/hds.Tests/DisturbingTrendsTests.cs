using System.Xml;
using System.Xml.Linq;
using hds;
using HeadlessClient;
using NativeQa.Modes;
namespace hds.Tests;
public class DisturbingTrendsTests
{
    private static string Root {get{var p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    private static string MissionPath=>Path.Combine(Root,"data","missions","machines_1_2_1_disturbing_trends.xml");
    private static Mission Load(){var d=new XmlDocument();d.Load(MissionPath);return MissionXml.Parse(d.DocumentElement!);}
    [Fact] public void ExistingThreePhaseStoryAndSeparateOphiuchiDefinitionsRemain()
    {
        var mission=Load();Assert.Equal(201201,mission.id);Assert.Equal(3,mission.phases.Count);Assert.Equal(3,mission.phases.Sum(p=>p.missionObjectives.Count));
        Assert.Equal(new[]{MissionGoalType.Talk,MissionGoalType.Search,MissionGoalType.Give},mission.phases.Select(p=>Assert.Single(p.missionObjectives).type));
        var xml=XElement.Load(MissionPath);var npcs=xml.Elements("npc").Where(n=>(string?)n.Attribute("handle")=="Ophiuchi").ToArray();Assert.Equal(new[]{1,6},npcs.Select(n=>(int)n.Attribute("id")!));Assert.Equal(new[]{1,3},npcs.Select(n=>(int)n.Attribute("at")!));
        var objectives=xml.Elements("phase").SelectMany(p=>p.Elements("objective")).ToArray();Assert.Equal("1",(string?)objectives[0].Attribute("npc"));Assert.Equal("6",(string?)objectives[2].Attribute("npc"));Assert.Equal("6783",(string?)objectives[1].Attribute("item"));Assert.Equal("6783",(string?)objectives[2].Attribute("item"));
    }
    [Fact] public void OnlyTwentyEarlierReceiptsExposeTheNormalNextCritical()
    {
        Assert.Equal(20,DisturbingTrendsFixture.Prerequisites.Length);Assert.Equal(20,DisturbingTrendsFixture.Prerequisites.Distinct().Count());Assert.DoesNotContain(201201,DisturbingTrendsFixture.Prerequisites);Assert.DoesNotContain(201202,DisturbingTrendsFixture.Prerequisites);
        Assert.Equal(MachinesCriticalFixture.Prerequisites.Concat(MachinesCriticalFixture.Missions),DisturbingTrendsFixture.Prerequisites);
        var all=Directory.GetFiles(Path.Combine(Root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();var contact=MissionContacts.ForOrg(2,0);Assert.Equal(2001u,contact.id);
        var offers=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),DisturbingTrendsFixture.Prerequisites.ToHashSet());Assert.Equal(201201,Assert.Single(offers,m=>m.plot).id);
    }
    [Fact] public void ReviewedReference17HasIndependentLevel50Quote()
    {
        var mission=Load();Assert.Equal(17,mission.rewardLevel);Assert.Equal(54871,mission.exp);Assert.Equal(45339,mission.info);
        var quote=MissionRewards.Calculate(mission,50);Assert.Equal(DisturbingTrendsFixture.ExpectedXp,quote.Experience);Assert.Equal(DisturbingTrendsFixture.ExpectedInfo,quote.Info);
        var low=MissionRewards.Calculate(mission,17);Assert.Equal(54871u,low.Experience);Assert.Equal(45339u,low.Info);
        Assert.Equal(168042u,DisturbingTrendsFixture.ExpectedXp);Assert.Equal(133350u,DisturbingTrendsFixture.ExpectedInfo);
    }
    [Fact] public void FixedPlacementComparisonAllowsNormalUseStateIntroductionButRejectsChangedPrototype()
    {
        var item=new MissionProgress.SavedObject{Id=1,Goid=3,State=0};
        string fixedPlacement=DisturbingTrendsFixture.FixedDescriptor(item);
        item.State=2401;Assert.Equal(fixedPlacement,DisturbingTrendsFixture.FixedDescriptor(item));
        item.Goid=548;Assert.NotEqual(fixedPlacement,DisturbingTrendsFixture.FixedDescriptor(item));
    }
    [Fact] public void AuthoredStageIsIndependentOfTheNpcMissionKeyPhaseByte()
    {
        var state=new MissionProgress.SavedState{Mission=201201,Phase=1,Mobs=new(){
            new(){Npc=1,Phase=0,Health=100},new(){Npc=2,Phase=0,Health=100},new(){Npc=3,Phase=0,Health=100},new(){Npc=4,Phase=0,Health=100},new(){Npc=5,Phase=0,Health=100}}};
        Assert.Equal(new ushort[]{2,3,4,5},DisturbingTrendsFixture.StageNpcs(state,Load()).Select(m=>m.Npc));
        state.Phase=2;state.Mobs.Add(new(){Npc=6,Phase=0,Health=100});
        Assert.Equal((ushort)6,Assert.Single(DisturbingTrendsFixture.StageNpcs(state,Load())).Npc);
    }
    [Fact] public void DoorLookupUsesExactCurrentPoseAndRejectsAmbiguity()
    {
        string Row(string key,string x)=>"1,0,"+key+",0,A301,0,"+x+",495,2300,0,0";
        Assert.Equal(0x01020304u,DisturbingTrendsFixture.DoorAt(new[]{Row("04030201","17000"),Row("05030201","17001")},new[]{17000d,495d,2300d}));
        Assert.Throws<InvalidDataException>(()=>DisturbingTrendsFixture.DoorAt(new[]{Row("04030201","17000"),Row("05030201","17000")},new[]{17000d,495d,2300d}));
        Assert.Throws<InvalidDataException>(()=>DisturbingTrendsFixture.DoorAt(Array.Empty<string>(),new[]{17000d,495d,2300d}));
    }
    [Fact] public void NativeModeUsesMergedOpenFloorAndOnlyExistingLayoutControl()
    {
        var plan=new DisturbingTrendsMode().Plan;Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);Assert.Equal(DisturbingTrendsFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.Single(plan.EnvironmentOverrides);Assert.Equal("201101",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.True(plan.ExtraQaMetadata["NativePickingPending"].GetBoolean());
        Assert.True(plan.ExtraQaMetadata["SharedOpenFloorRouteAdmission"].GetBoolean());Assert.True(plan.ExtraQaMetadata["SharedInteriorAdmissionRequired"].GetBoolean());Assert.Equal("0490bff8fd8c75264be5ec8c9242e9e72fb0bfbe",plan.ExtraQaMetadata["QaBaseMaster"].GetString());Assert.Contains("exp="+PlayerProgression.ExperienceForLevel(50),plan.AfterHelpers[1]);
        var scenario=Scenarios.All.Single(s=>s.Name=="machines-disturbing-trends");
        Assert.Equal("1",scenario.Environment!["MXO_PACKET_LOG"]);
        var seed=DisturbingTrendsFixture.BeforeMargin(new CharacterSeed("QA"));
        Assert.Equal(DisturbingTrendsFixture.Prerequisites,seed.CompletedMissions);
        Assert.Equal(new byte[]{2,0,1,8,0,0,128,63,0,0,0,64,0,0,64,64,0,0},DisturbingTrendsFixture.ClientPosition(new[]{1d,2d,3d}));
        Assert.Throws<ArgumentException>(()=>DisturbingTrendsFixture.ClientPosition(new[]{double.NaN,2d,3d}));
    }
    [Fact] public void FriendlyMissionWireOmissionRequiresCurrentLiveIdentityAndLatestView()
    {
        var mob=new MissionProgress.SavedMob{Name="Ophiuchi",X=10,Y=95,Z=20,Phase=1,Friendly=true,Health=100};
        ViewCreation Body(byte spawn,string name="Ophiuchi",byte phase=1,int instance=47,double y=95,byte? enemy=null,byte? safe=1)
        {
            var attrs=new Dictionary<string,byte[]>{{"Position",BitConverter.GetBytes(10d).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(20d)).ToArray()},{"CharacterName",System.Text.Encoding.UTF8.GetBytes(name+"\0")},{"MissionKey",new[]{phase,(byte)instance,(byte)(instance>>8),(byte)2}}};
            if(enemy.HasValue)attrs["IsEnemy"]=new[]{enemy.Value};if(safe.HasValue)attrs["DontAttack"]=new[]{safe.Value};return new(21,599,spawn,attrs);
        }
        var actual=Body(1);Assert.False(actual.Attributes.ContainsKey("IsEnemy"));
        Assert.Same(actual,DisturbingTrendsFixture.CurrentNpc(new[]{actual},0,mob,1,47));
        foreach(var wrong in new[]{Body(2,name:"Other"),Body(2,phase:0),Body(2,instance:48),Body(2,y:195),Body(2,enemy:1),Body(2,safe:null)})
            Assert.Null(DisturbingTrendsFixture.CurrentNpc(new[]{actual,wrong},0,mob,1,47));
        Assert.Null(DisturbingTrendsFixture.CurrentNpc(new[]{actual},1,mob,1,47));mob.Dead=true;
        Assert.Null(DisturbingTrendsFixture.CurrentNpc(new[]{actual},0,mob,1,47));mob.Dead=false;mob.Health=0;
        Assert.Null(DisturbingTrendsFixture.CurrentNpc(new[]{actual},0,mob,1,47));mob.Health=100;mob.Phase=0;
        Assert.Null(DisturbingTrendsFixture.CurrentNpc(new[]{actual},0,mob,1,47));
        Assert.Null(new CharacterSeed("Legacy").CompletedMissions);
    }
}
