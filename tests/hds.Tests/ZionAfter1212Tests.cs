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
    [Fact]public void EarlierReceiptsAreInstalledOnThePreMarginSeedOnly()
    {
        var seed=ZionAfter1212Fixture.BeforeMargin(new CharacterSeed("BeforeMargin"),ZionAfter1212Fixture.Missions,ZionAfter1212Fixture.Prerequisites);
        Assert.Equal(ZionAfter1212Fixture.Prerequisites,seed.CompletedMissions);
        ZionAfter1212Fixture.RequirePin(new string('a',64),new string('A',64));Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.RequirePin(null,new string('a',64)));Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.RequirePin(new string('b',64),new string('a',64)));
        Assert.Throws<InvalidOperationException>(()=>ZionAfter1212Fixture.BeforeMargin(seed,ZionAfter1212Fixture.Missions,new[]{101213}));
        Assert.Throws<InvalidOperationException>(()=>ZionAfter1212Fixture.BeforeMargin(seed,ZionAfter1212Fixture.Missions,new[]{101301}));
        Assert.Throws<InvalidOperationException>(()=>ZionAfter1212Fixture.BeforeMargin(seed,ZionAfter1212Fixture.Missions,new[]{101212,101212}));
    }
    [Fact]public void ActualFriendlyOmissionAndReusedViewCannotMatchAnOldNpc()
    {
        var mob=new MissionProgress.SavedMob{Name="Fissure",Friendly=true,X=1,Y=495,Z=2};
        ViewCreation Body(string name,byte? enemy=null,bool safe=true)=>new(7,599,1,new Dictionary<string,byte[]>{["CharacterName"]=System.Text.Encoding.ASCII.GetBytes(name+"\0"),["Position"]=NumericalUtils.doublesToLtVector3d(1,495,2),["DontAttack"]=new byte[]{(byte)(safe?1:0)}}.Concat(enemy.HasValue?new[]{new KeyValuePair<string,byte[]>("IsEnemy",new[]{enemy.Value})}:Array.Empty<KeyValuePair<string,byte[]>>()).ToDictionary(x=>x.Key,x=>x.Value));
        Assert.NotNull(ZionAfter1212Fixture.CurrentNpc(new[]{Body("Fissure")},0,mob));
        Assert.Null(ZionAfter1212Fixture.CurrentNpc(new[]{Body("Fissure",1)},0,mob));
        Assert.Null(ZionAfter1212Fixture.CurrentNpc(new[]{Body("Fissure",null,false)},0,mob));
        Assert.Null(ZionAfter1212Fixture.CurrentNpc(new[]{Body("Fissure"),Body("New actor")},0,mob));
    }

    [Fact]public void FreeBodyRequiresCurrentLifeInstanceOrientationAndLatestView()
    {
        string q=Convert.ToHexString(new byte[16]);double[] centre={1,2,3};
        var placement=new MissionOpenFloorPlacement(4,1,101213,0,1,5,1412,548,new string('a',64),new string('b',64),q,centre,centre,centre,Array.Empty<ushort>(),Array.Empty<ushort>(),Array.Empty<ushort>());
        var obj=new MissionProgress.SavedObject{Id=1,Goid=548,OpenFloor=placement};
        var own=new ViewCreation(2,12,1,new Dictionary<string,byte[]>());
        ViewCreation Body(ushort goid=548,ushort instance=42)=>new(7,goid,2,new Dictionary<string,byte[]>{["MissionKey"]=new byte[]{255,(byte)instance,0,2},["Position"]=NumericalUtils.doublesToLtVector3d(1,2,3),["Orientation"]=new byte[16]});
        Assert.NotNull(ZionAfter1212Fixture.CurrentBody(new[]{own,Body()},obj,42));
        Assert.Null(ZionAfter1212Fixture.CurrentBody(new[]{own,Body()},obj,42,2));
        Assert.Null(ZionAfter1212Fixture.CurrentBody(new[]{own,Body(instance:43)},obj,42));
        Assert.Null(ZionAfter1212Fixture.CurrentBody(new[]{own,Body(),own},obj,42));
        Assert.Null(ZionAfter1212Fixture.CurrentBody(new[]{own,Body(),Body(goid:55)},obj,42));
        var rotated=Body();rotated.Attributes["Orientation"][0]=1;Assert.Null(ZionAfter1212Fixture.CurrentBody(new[]{own,rotated},obj,42));
    }

    [Fact]public void ActualRegisteredScenarioHasInitialEnvironmentRatherThanPartialClassNullCapture()
    {
        var actual=Scenarios.All.Single(s=>s.Name=="zion-critical-after-101212");Assert.NotNull(actual.Environment);Assert.Equal(TimeSpan.FromMinutes(15),actual.Timeout);
        Assert.Equal("1",actual.Environment!["MXO_DEV_COMMANDS"]);Assert.Equal("101213",actual.Environment["MXO_MISSION_SITE_QA_SEED"]);Assert.Equal("1",actual.Environment["MXO_PACKET_LOG"]);
    }

    [Fact]public void LiteralCurrentNpcLevel32IsOneByte50NotAnUInt16Field()
    {
        var actual=new ViewCreation(8,599,6,new Dictionary<string,byte[]>{["Level"]=Convert.FromHexString("32")});Assert.Equal(50,ZionAfter1212Fixture.NpcLevel(actual));Assert.Equal(19204u,ZionAfter1212Fixture.ExpectedKill(ZionAfter1212Fixture.NpcLevel(actual)));
        var wrong=new ViewCreation(8,599,6,new Dictionary<string,byte[]>{["Level"]=Convert.FromHexString("3200")});Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.NpcLevel(wrong));
    }

    [Fact]public void StoryStage2SelectsCurrentGeneratedAndAuthoredEnemiesWithWirePhase0()
    {
        MissionProgress.SavedMob Mob(ushort id,string name,bool generated)=>new(){Npc=id,Name=name,Phase=0,Hostile=generated,Friendly=false,Health=100,X=id+10,Y=495,Z=1};
        var generated=Mob(0,"Machine Program",true);var named=Mob(2,"Imposter Agent",false);var prior=Mob(1,"Earlier actor",false);
        var saved=new MissionProgress.SavedState{Mission=101213,Phase=2,Mobs=new(){generated,named,prior}};
        ViewCreation View(ushort id,MissionProgress.SavedMob mob)=>new(id,599,6,new Dictionary<string,byte[]>{["CharacterName"]=System.Text.Encoding.ASCII.GetBytes(mob.Name+"\0"),["Position"]=NumericalUtils.doublesToLtVector3d(mob.X,mob.Y,mob.Z),["IsEnemy"]=new byte[]{1},["IsDead"]=new byte[]{0}});
        var views=new[]{View(8,generated),View(9,named),View(10,prior)};
        Assert.Equal(new ushort[]{8,9},ZionAfter1212Fixture.CurrentHostiles(views,0,saved,101213,2,new HashSet<ushort>{2}).Select(v=>v.ViewId));
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.CurrentHostiles(views,0,saved,101213,1,new HashSet<ushort>{2}));
        generated.Dead=true;Assert.Equal((ushort)9,Assert.Single(ZionAfter1212Fixture.CurrentHostiles(views,0,saved,101213,2,new HashSet<ushort>{2})).ViewId);
    }
    [Fact]public void CompletionScopeExcludesEarlierMissionAndSpansCarryRelogSessions()
    {
        byte[] Summary(){var b=new byte[26];b[0]=0x80;b[1]=0xa2;BitConverter.GetBytes(68750u).CopyTo(b,15);BitConverter.GetBytes(91000u).CopyTo(b,11);return b;}
        var old=Summary();var current=Summary();var previous=new[]{old,new byte[]{5}};int preOfferCursor=1;
        Assert.Empty(ZionAfter1212Fixture.CompletionSummaries(previous,Array.Empty<byte[]>(),preOfferCursor));
        var scoped=ZionAfter1212Fixture.CompletionSummaries(previous,new[]{current},preOfferCursor);Assert.Same(current,ZionAfter1212Fixture.RequireCompletion(scoped,68750,91000));
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.RequireCompletion(ZionAfter1212Fixture.CompletionSummaries(previous,new[]{current,current},preOfferCursor),68750,91000));
    }

    [Fact]public void CompletedReceiptMayRetainANullPayloadCasRowButNeverActiveProgress()
    {
        const string token="7B7D"; // Exact opaque prior-checkpoint token, kept separately from NULL payload.
        var retained=System.Text.Json.JsonSerializer.Deserialize<ZionAfter1212Fixture.CompletionCheckpointRow>("""{"PayloadHex":null,"CasReceiptHex":"7B7D"}""")!;var rows=new[]{retained};
        Assert.Single(rows);ZionAfter1212Fixture.RequireCompletedCheckpoint(rows,1);Assert.Equal(token,retained.CasReceiptHex);
        ZionAfter1212Fixture.RequireCompletedCheckpoint(Array.Empty<ZionAfter1212Fixture.CompletionCheckpointRow>(),1);
        var active=retained with {PayloadHex=token};Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.RequireCompletedCheckpoint(new[]{active},1));
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.RequireCompletedCheckpoint(rows,0));Assert.Throws<InvalidDataException>(()=>ZionAfter1212Fixture.RequireCompletedCheckpoint(rows,2));
    }

}
