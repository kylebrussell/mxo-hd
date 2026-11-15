using System.Reflection;
using System.Xml;
using hds;
using HeadlessClient;
using NativeQa.Modes;
namespace hds.Tests;
public class ZionNextFiveTests
{
    [Fact] public void KillOracleReadsTheActualNpcSingleByteLevelAndRejectsMalformedWidths()
    {
        ViewCreation Body(ushort goid,byte[] level)=>new(8,goid,6,new Dictionary<string,byte[]>{{"Level",level}});
        Assert.Equal(50,ZionNextFiveFixture.NpcLevel(Body(599,new byte[]{0x32})));
        Assert.Equal(60,ZionNextFiveFixture.NpcLevel(Body(599,new byte[]{0x3c})));
        foreach(var level in new[]{Array.Empty<byte>(),new byte[]{0},new byte[]{50,0}})
            Assert.Throws<InvalidDataException>(()=>ZionNextFiveFixture.NpcLevel(Body(599,level)));
        Assert.Throws<InvalidDataException>(()=>ZionNextFiveFixture.NpcLevel(Body(12,new byte[]{50})));
    }
    [Fact] public void ExactProfileSeedsEarlierReceiptsBeforeMarginAndRejectsSelectedOrNextGrants()
    {
        var seed=ZionNextFiveFixture.BeforeMargin(new CharacterSeed("QA"),ZionNextFiveFixture.Missions,ZionNextFiveFixture.Prerequisites);
        Assert.Equal(27,seed.CompletedMissions!.Count);
        Assert.Equal("1",Scenarios.All.Single(s=>s.Name=="zion-critical-next-five").Environment!["MXO_PACKET_LOG"]);
        foreach(var bad in new[]{new[]{101208},new[]{101213},new[]{1,1},new[]{0}})
            Assert.Throws<InvalidOperationException>(()=>ZionNextFiveFixture.BeforeMargin(new CharacterSeed("QA"),ZionNextFiveFixture.Missions,bad));
    }

    static List<Mission> Load()=>Directory.GetFiles(DataLoader.DataPath("missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    [Fact] public void NormalContactOffersEachOfFiveOnlyAfterExactEarlierTwentySeven()
    {
        var all=Load();var contact=MissionContacts.ForOrg(1,0);Assert.Equal(2000u,contact.id);
        var done=ZionNextFiveFixture.Prerequisites.ToHashSet();Assert.Equal(27,done.Count);Assert.Empty(done.Intersect(ZionNextFiveFixture.Missions));
        var selected=all.Where(m=>ZionNextFiveFixture.Missions.Contains(m.id)).OrderBy(m=>m.order).ToArray();
        Assert.Equal(ZionNextFiveFixture.Missions,selected.Select(m=>m.id));Assert.Equal(new[]{1208,1209,1210,1211,1212},selected.Select(m=>m.order));
        Assert.Equal(7,selected.Sum(m=>m.phases.Count));Assert.Equal(20,selected.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        Assert.Equal(new[]{68625,90750,137500,68625,68750},selected.Select(m=>m.exp));Assert.Equal(434250,selected.Sum(m=>m.exp));
        foreach(var m in selected)
        {
            var offers=DataLoader.ContactOfferWithStandards(all.Where(x=>MissionContacts.Offers(contact,x)).OrderBy(x=>x.order).ToList(),done);
            Assert.Equal(m.id,Assert.Single(offers.Where(x=>x.plot)).id);Assert.Contains(offers,m=>!m.plot&&m.title=="Courier");done.Add(m.id);
        }
        var next=DataLoader.ContactOfferWithStandards(all.Where(x=>MissionContacts.Offers(contact,x)).OrderBy(x=>x.order).ToList(),done);
        Assert.Equal(101213,Assert.Single(next.Where(x=>x.plot)).id);
    }
    [Fact] public void SecondJunctionBoxCannotFinishBeforeHostilesAndBurdetteLootStillRequiresUpload()
    {
        var all=Load();var hunter=all.Single(m=>m.id==101209);Assert.All(hunter.phases,p=>Assert.Equal("Box",Assert.Single(p.objects).kind));
        var progress=new MissionProgress(hunter){phaseIndex=1};progress.Complete(progress.Phase.missionObjectives.Single(o=>o.goal==1));
        Assert.False(progress.AllDone);Assert.Contains(progress.OpenObjectives(),o=>o.type==MissionGoalType.DefeatAll);
        var burdette=new MissionProgress(all.Single(m=>m.id==101212));foreach(int goal in new[]{1,2,3})burdette.Complete(burdette.Phase.missionObjectives.Single(o=>o.goal==goal));
        Assert.False(burdette.AllDone);Assert.Equal(MissionGoalType.Upload,Assert.Single(burdette.OpenObjectives()).type);
        var witness=all.Single(m=>m.id==101211);Assert.Single(witness.phases.Single().missionObjectives.Where(o=>o.type==MissionGoalType.Protect));
    }
    [Fact] public void NewProfileUsesConsistentLevelExperienceAndOnlyEarlierReceiptSeeds()
    {
        var plan=new ZionNextFiveMode().Plan;Assert.Equal(ZionNextFiveFixture.SeedPrerequisites(110),plan.AfterHelpers[0]);
        Assert.Contains("level=50,exp="+PlayerProgression.ExperienceForLevel(50),plan.AfterHelpers[1]);Assert.Contains("alignment=1",plan.AfterHelpers[1]);
        Assert.Equal("101208",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        Assert.Equal(new ushort[]{2,17,45,1006,77},plan.Abilities);
    }
    [Fact] public void ExistingPrimaCallersHaveNoImplicitEvidenceRestartOrExperienceOverride()
    {
        var method=typeof(Scenarios).GetMethod("RunPrimaChain",BindingFlags.NonPublic|BindingFlags.Static)!;
        var option=Assert.Single(method.GetParameters().Where(p=>p.Name=="evidenceOptions"));Assert.True(option.IsOptional);Assert.Null(option.DefaultValue);
        var defaults=new PrimaChainEvidenceOptions();Assert.Null(defaults.StartingExperience);Assert.False(defaults.ColdRestart);Assert.Null(defaults.RestartEnvironment);Assert.Null(defaults.ExpectedExperienceDelta);Assert.Null(defaults.ExpectedInformationDelta);Assert.False(defaults.FreshUploadBooth);Assert.Null(defaults.MissionExperience);Assert.Null(defaults.MissionInformation);Assert.False(defaults.ExactZionNextFive);
    }
    [Fact] public void NativeLoadedAbilitiesOwnEveryStoredConcretePrerequisite()
    {
        var loaded=new ZionNextFiveMode().Plan.Abilities.ToHashSet();var owned=loaded.Append((ushort)52).ToHashSet();
        using var j=System.Text.Json.JsonDocument.Parse(File.ReadAllText(DataLoader.DataPath("ability_catalog.json")));
        foreach(var row in j.RootElement.GetProperty("abilities").EnumerateArray().Where(r=>loaded.Contains(r.GetProperty("id").GetUInt16())))
        foreach(var component in row.GetProperty("components").EnumerateArray().Where(c=>c.GetProperty("type").GetInt32()==39))
        {
            var fields=row.GetProperty("fields");string prefix=component.GetProperty("name").GetString()+"/";
            if(fields.TryGetProperty(prefix+"ReqAbilityID",out var raw)&&raw.GetUInt32()!=0)
                Assert.Contains(AbilityCatalog.Canonical(AbilityCatalog.IdOf(raw.GetUInt32())),owned);
        }
    }

    [Fact] public void FreshUploadSelectionRequiresNewExactReceivedBoothAndPreservesActualHandle()
    {
        var method=typeof(Scenarios).GetMethod("FreshPrimaUploadBooth",BindingFlags.NonPublic|BindingFlags.Static)!;
        ViewCreation? Select(ViewCreation[] views,int cursor)=>(ViewCreation?)method.Invoke(null,new object[]{views,cursor});
        ViewCreation Booth(ushort view,byte spawn,ushort goid=592,double x=17000,double y=530,double z=2200)=>
            new(view,goid,spawn,new Dictionary<string,byte[]>{{"Position",BitConverter.GetBytes(x).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(z)).ToArray()}});
        var old=Booth(28,26);Assert.Null(Select(new[]{old},1));
        foreach(var wrong in new[]{Booth(65,61,599),Booth(65,61,x:17001),Booth(65,61,y:545),Booth(65,61,z:2201),Booth(2,61),Booth(65,61,x:double.NaN)})
            Assert.Null(Select(new[]{old,wrong},1));
        var actual=Booth(65,61);Assert.Same(actual,Select(new[]{old,actual},1));
        Assert.Equal((ushort)65,Select(new[]{old,actual},1)!.ViewId);Assert.Equal((byte)61,Select(new[]{old,actual},1)!.SpawnId);
        Assert.Null(Select(new[]{old,actual},-1));Assert.Null(Select(new[]{old,actual},3));
        Assert.Null(Select(new[]{old,new ViewCreation(65,592,61,new Dictionary<string,byte[]>())},1));
    }

    [Fact] public void CurrentNpcUsesSavedFriendlySideRatherThanWaveMembership()
    {
        var mob=new MissionProgress.SavedMob{Name="Current",X=1,Y=95,Z=2,Friendly=false,Hostile=false};
        ViewCreation Body(double y,byte side)=>new(42,599,17,new Dictionary<string,byte[]>{{"Position",BitConverter.GetBytes(1d).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(2d)).ToArray()},{"IsEnemy",new[]{side}},{"CharacterName",System.Text.Encoding.UTF8.GetBytes("Current\0")}});
        var right=Body(95,1);Assert.Same(right,ZionNextFiveFixture.CurrentNpc(new[]{Body(195,1),Body(95,0),right},0,mob));
        Assert.Null(ZionNextFiveFixture.CurrentNpc(new[]{right},1,mob));Assert.Null(ZionNextFiveFixture.CurrentNpc(new[]{Body(95,0)},0,mob));
    }
    [Fact] public void ActualFriendlyMissionCreationOmitsIsEnemyAndRequiresDontAttack()
    {
        var mob=new MissionProgress.SavedMob{Name="Maynard",X=10,Y=95,Z=20,Friendly=true,Hostile=false};
        ViewCreation Body(byte? enemy,byte? safe,double y=95)
        {
            var attrs=new Dictionary<string,byte[]>{{"Position",BitConverter.GetBytes(10d).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(20d)).ToArray()},{"CharacterName",System.Text.Encoding.UTF8.GetBytes("Maynard\0")}};
            if(enemy.HasValue)attrs["IsEnemy"]=new[]{enemy.Value};if(safe.HasValue)attrs["DontAttack"]=new[]{safe.Value};return new(21,599,4,attrs);
        }
        var actual=Body(null,1);Assert.False(actual.Attributes.ContainsKey("IsEnemy"));
        Assert.Same(actual,ZionNextFiveFixture.CurrentNpc(new[]{Body(1,1),Body(null,1,195),actual},0,mob));
        Assert.NotNull(ZionNextFiveFixture.CurrentNpc(new[]{Body(0,1)},0,mob));
        foreach(var bad in new[]{Body(null,null),Body(1,1),Body(null,0),Body(0,null),Body(2,1)})Assert.Null(ZionNextFiveFixture.CurrentNpc(new[]{bad},0,mob));
        Assert.Null(ZionNextFiveFixture.CurrentNpc(new[]{actual},1,mob));mob.Friendly=false;
        Assert.Null(ZionNextFiveFixture.CurrentNpc(new[]{actual},0,mob));Assert.NotNull(ZionNextFiveFixture.CurrentNpc(new[]{Body(1,null)},0,mob));
    }
    [Fact] public void ReusedViewCannotResurrectHistoricalNpcAndDefeatAllRequiresCurrentSavedHostile()
    {
        var mob=new MissionProgress.SavedMob{Name="Enemy",X=1,Y=95,Z=2,Friendly=false,Hostile=false,Health=100,Phase=0,Npc=1100};
        ViewCreation Body(string name,byte spawn,double x=1)=>new(42,599,spawn,new Dictionary<string,byte[]>{{"CharacterName",System.Text.Encoding.UTF8.GetBytes(name+"\0")},{"IsEnemy",new byte[]{1}},{"Position",BitConverter.GetBytes(x).Concat(BitConverter.GetBytes(95d)).Concat(BitConverter.GetBytes(2d)).ToArray()}});
        var original=Body("Enemy",1);var reused=Body("Other",2);
        Assert.Null(ZionNextFiveFixture.CurrentNpc(new[]{original,reused},0,mob));
        var state=new MissionProgress.SavedState{Mission=101209,Phase=1,Mobs=new(){mob}};
        var definition=System.Xml.Linq.XElement.Parse("<mission id='101209' title='Hostiles'><phase/><phase enemies='1'/></mission>");
        Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{original,reused},0,state,101209,1,definition));
        var actual=Body("Enemy",3);Assert.Same(actual,Assert.Single(ZionNextFiveFixture.CurrentHostiles(new[]{original,reused,actual},0,state,101209,1,definition)));
        foreach(var change in new Action[]{()=>mob.Friendly=true,()=>{mob.Friendly=false;mob.Dead=true;},()=>{mob.Dead=false;mob.Health=0;},()=>{mob.Health=100;mob.Phase=1;}})
        {change();Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{actual},0,state,101209,1,definition));}
        Assert.Throws<InvalidDataException>(()=>ZionNextFiveFixture.CurrentHostiles(new[]{actual},0,state,101208,1,definition));
    }
}
