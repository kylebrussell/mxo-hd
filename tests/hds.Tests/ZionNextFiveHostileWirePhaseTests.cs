using System.Text.Json;
using System.Xml.Linq;
using hds;
using HeadlessClient;
namespace hds.Tests;
public class ZionNextFiveHostileWirePhaseTests
{
    // Retained0b run/schema17667844c5: real03 sections24–26 and saved current NPC1100–1103; no live state injection.
    static readonly string[] ActualSections={
        "01000C57020FCDAB15AE4D616368696E652050726F6772616D0000000000000000000000000000000000001000002200001000D4008F08005801A10001C18F080058000000403B64E2400000000000F07E40000000A009E8D3C0C001860100A8326608B88F080058000000000000000000000066080608000081080A0000021111000001000C570210CDAB15AE4D616368696E652050726F6772616D0000000000000000000000000000000000001000002200001000D4008F08005801A10001C18F08005800000000E071E2400000000000F07E400000000040D9D3C0C001860100A8326608B88F080058000000000000000000000066080608000081080A00000211120000",
        "01000C570211CDAB15AE4D616368696E652050726F6772616D0000000000000000000000000000000000001000002200001000D4008F08005801A10001C18F080058000000C0FFABE2400000000000F07E40000000A0A7D4D4C0C001860100A8326608B88F080058000000000000000000000066080608000081080A00000211130000",
        "01000C570212CDAB15AE4D616368696E652050726F6772616D0000000000000000000000000000000000001000002200001000D4008F08005801A10001C18F08005800000040ECCEE2400000000000F07E40000000A0A7D4D4C0C001860100A8326608B88F080058000000000000000000000066080608000081080A000002111400000100085002E702301913CDAB0101000000004011E340000000000040604000000000C0F3D4C0150000"
    };
    const string ActualSaved=@"[{""Identity"":""dd548a0604de4b14837249bc55efeb0d"",""RewardExperience"":0,""Lootable"":false,""Loot"":null,""Npc"":1100,""Phase"":0,""Name"":""Machine Program"",""Rsi"":""8f080058"",""Level"":50,""Health"":2150,""MaxHealth"":2150,""Weapon"":2568,""Rotation"":0,""X"":37665.8515625,""Y"":495,""Z"":-20384.150390625,""Dead"":false,""ScriptedCorpse"":false,""Aggro"":0,""Friendly"":false,""Hostile"":true,""Giver"":false,""Taker"":false,""Wave"":0},{""Identity"":""2db3024f92a4459fa014bd8bd4f35f09"",""RewardExperience"":0,""Lootable"":false,""Loot"":null,""Npc"":1101,""Phase"":0,""Name"":""Machine Program"",""Rsi"":""8f080058"",""Level"":50,""Health"":2150,""MaxHealth"":2150,""Weapon"":2568,""Rotation"":0,""X"":37775,""Y"":495,""Z"":-20325,""Dead"":false,""ScriptedCorpse"":false,""Aggro"":0,""Friendly"":false,""Hostile"":true,""Giver"":false,""Taker"":false,""Wave"":0},{""Identity"":""e64771d3883f469a9da7d5eec760a81f"",""RewardExperience"":0,""Lootable"":false,""Loot"":null,""Npc"":1102,""Phase"":0,""Name"":""Machine Program"",""Rsi"":""8f080058"",""Level"":50,""Health"":2150,""MaxHealth"":2150,""Weapon"":2568,""Rotation"":0,""X"":38239.9921875,""Y"":495,""Z"":-21330.619140625,""Dead"":false,""ScriptedCorpse"":false,""Aggro"":0,""Friendly"":false,""Hostile"":true,""Giver"":false,""Taker"":false,""Wave"":0},{""Identity"":""3bf09e1389e74f1d929be79edcfe2ecc"",""RewardExperience"":0,""Lootable"":false,""Loot"":null,""Npc"":1103,""Phase"":0,""Name"":""Machine Program"",""Rsi"":""8f080058"",""Level"":50,""Health"":2150,""MaxHealth"":2150,""Weapon"":2568,""Rotation"":0,""X"":38519.3828125,""Y"":495,""Z"":-21330.619140625,""Dead"":false,""ScriptedCorpse"":false,""Aggro"":0,""Friendly"":false,""Hostile"":true,""Giver"":false,""Taker"":false,""Wave"":0}]";
    static List<ViewCreation> Views()=>ActualSections.SelectMany(s=>WireFormat.FindCreations(Convert.FromHexString(s))).Where(v=>v.Goid==599).ToList();
    static List<MissionProgress.SavedMob> Mobs()=>JsonSerializer.Deserialize<List<MissionProgress.SavedMob>>(ActualSaved,new JsonSerializerOptions{IncludeFields=true})!;
    static MissionProgress.SavedState Saved(params MissionProgress.SavedMob[] mobs)=>new(){Mission=101209,Phase=1,Instance=48,Mobs=mobs.ToList()};
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    static XElement ActualDefinition()=>XElement.Load(Path.Combine(Repo,"data/missions/zion_1_2_9_hardline_hunter.xml"));
    static ViewCreation Change(ViewCreation v,string key,byte[] value,byte? spawn=null)
    {var a=v.Attributes.ToDictionary(x=>x.Key,x=>x.Value);a[key]=value;return new(v.ViewId,v.Goid,spawn??v.SpawnId,a);}
    static ViewCreation Own(byte spawn)=>new(2,12,spawn,new Dictionary<string,byte[]>());
    [Fact] public void ActualCurrentSavedPhaseOneMatchesAllFourDefaultWireZeroHostiles()
    {
        var mobs=Mobs();var views=Views();Assert.Equal(4,views.Count);Assert.All(mobs,m=>Assert.Equal((byte)0,m.Phase));
        Assert.All(views,v=>{Assert.Equal(50,ZionNextFiveFixture.NpcLevel(v));Assert.False(v.Attributes.ContainsKey("MissionKey"));});
        var result=ZionNextFiveFixture.CurrentHostiles(views,0,Saved(mobs.ToArray()),101209,1,ActualDefinition());
        Assert.Equal(new ushort[]{17,18,19,20},result.Select(v=>v.ViewId));Assert.Equal(new byte[]{15,16,17,18},result.Select(v=>v.SpawnId));
        Assert.Equal(new ushort[]{1100,1101,1102,1103},ZionNextFiveFixture.HostileWirePhases(ActualDefinition(),101209,1).Keys.Where(id=>id>=1000));
    }
    [Theory][InlineData(0,2)][InlineData(1,3)][InlineData(1,2)]
    public void GeneratedWireByteRemainsZeroOnFirstNonfinalAndFinalStages(int stage,int stages)
    {
        var xml=XElement.Parse("<mission id='101209' title='Wire byte'>"+string.Concat(Enumerable.Range(0,stages).Select(i=>i==stage?"<phase enemies='4'/>":"<phase/>"))+"</mission>");
        var mob=Mobs()[0];mob.Npc=checked((ushort)(1000+(stage+1)*50));var state=Saved(mob);state.Phase=stage;var view=Views()[0];
        Assert.Same(view,Assert.Single(ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101209,stage,xml)));
        mob.Phase=1;Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101209,stage,xml));
    }
    [Fact] public void AuthoredMissionKeyByteIsIndependentOfAtStageAndOptionalHostileKeyMustMatchOwner()
    {
        var xml=XElement.Parse("<mission id='101209' title='Authored'><npc id='7' handle='Machine Program' type='HOSTILE' at='2' phase='3'/><phase/><phase/></mission>");
        var mob=Mobs()[0];mob.Npc=7;mob.Phase=3;var state=Saved(mob);var view=Views()[0];
        Assert.Same(view,Assert.Single(ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101209,1,xml)));
        var keyed=Change(view,"MissionKey",new byte[]{3,48,0,2});Assert.Single(ZionNextFiveFixture.CurrentHostiles(new[]{keyed},0,state,101209,1,xml));
        foreach(var key in new[]{new byte[]{3,49,0,2},new byte[]{0,48,0,2},new byte[]{3,48,0,1},new byte[]{3,48,0}})
            Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{Change(view,"MissionKey",key)},0,state,101209,1,xml));
        mob.Phase=0;Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101209,1,xml));
    }
    [Fact] public void WrongStageForeignMissionAndOffstageGeneratedDomainRefuseActualView()
    {
        var mob=Mobs()[0];var view=Views()[0];var state=Saved(mob);var xml=ActualDefinition();
        state.Phase=0;Assert.Throws<InvalidDataException>(()=>ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101209,1,xml));state.Phase=1;
        Assert.Throws<InvalidDataException>(()=>ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101208,1,xml));
        Assert.Throws<InvalidDataException>(()=>ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101209,1,XElement.Parse("<mission id='101208' title='Other'><phase/><phase/></mission>")));
        foreach(ushort id in new ushort[]{1050,1099,1104,1149,1150}){mob.Npc=id;Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{view},0,state,101209,1,xml));}
        Assert.Throws<InvalidDataException>(()=>ZionNextFiveFixture.HostileWirePhases(XElement.Parse("<mission id='101209' title='Domain'><phase enemies='50'/></mission>"),101209,0));
    }
    [Fact] public void SavedAndWireDeadFriendlyNameSideAndExactPositionChecksRemainStrong()
    {
        var view=Views()[0];var xml=ActualDefinition();
        foreach(Action<MissionProgress.SavedMob> change in new Action<MissionProgress.SavedMob>[] {m=>m.Dead=true,m=>m.Health=0,m=>m.Friendly=true,m=>m.Phase=1,m=>m.X+=2})
        {var mob=Mobs()[0];change(mob);Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{view},0,Saved(mob),101209,1,xml));}
        foreach(var bad in new[]{Change(view,"IsDead",new byte[]{1}),Change(view,"IsEnemy",new byte[]{0}),Change(view,"CharacterName",System.Text.Encoding.UTF8.GetBytes("Other\0"))})
            Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{bad},0,Saved(Mobs()[0]),101209,1,xml));
    }
    [Fact] public void LaterOwnLifeBirthAndReusedViewCannotResurrectAnOldGeneration()
    {
        var view=Views()[0];var state=Saved(Mobs()[0]);var xml=ActualDefinition();var own=Own(1);var resetOwn=Own(2);
        Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{own,view,resetOwn},0,state,101209,1,xml));
        var fresh=Change(view,"IsEnemy",new byte[]{1},33);Assert.Same(fresh,Assert.Single(ZionNextFiveFixture.CurrentHostiles(new[]{own,view,resetOwn,fresh},0,state,101209,1,xml)));
        Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{own,fresh},2,state,101209,1,xml));
        var reused=Change(view,"CharacterName",System.Text.Encoding.UTF8.GetBytes("Different\0"),34);
        Assert.Empty(ZionNextFiveFixture.CurrentHostiles(new[]{own,view,reused},1,state,101209,1,xml));
        Assert.Same(fresh,Assert.Single(ZionNextFiveFixture.CurrentHostiles(new[]{own,view,reused,fresh},1,state,101209,1,xml)));
    }
}
