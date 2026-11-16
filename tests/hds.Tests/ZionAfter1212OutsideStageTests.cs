using System.Xml;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class ZionAfter1212OutsideStageTests
{
    static Mission Mission()
    {
        string root=AppContext.BaseDirectory;
        while(!File.Exists(Path.Combine(root,"data","missions","zion_1_2_14_lost_fragments.xml")))root=Path.GetDirectoryName(root)!;
        var doc=new XmlDocument();doc.Load(Path.Combine(root,"data","missions","zion_1_2_14_lost_fragments.xml"));return MissionXml.Parse(doc.DocumentElement!);
    }
    static MissionProgress.SavedState State()=>new(){Version=2,Mission=101214,Phase=1,Identity=new string('a',32),Started=1,OpenFloorCurrentSiteIndex=-1};
    [Fact] public void FourFragmentsUploadStaysOutdoorsWithOriginalGoalsItemsAndRewards()
    {
        var m=Mission();Assert.Null(m.phases[1].site);Assert.True(ZionAfter1212OutsideStage.Matches(m,1));Assert.False(ZionAfter1212OutsideStage.Matches(m,0));
        Assert.Equal(new uint[]{6785,6787,6831,6782},m.phases[1].missionObjectives.Select(g=>g.item));Assert.Equal(68750,m.exp);Assert.Equal(91000,m.info);
        ZionAfter1212OutsideStage.Require(m,1,State());
        var progress=new MissionProgress(m){phaseIndex=1};Assert.False(MissionRuntime.NeedsToLeave(progress,m.phases[1].missionObjectives[^1],17000,495,2200));
    }
    [Theory] [InlineData("site")] [InlineData("actor")] [InlineData("object")] [InlineData("wave")] [InlineData("goal")]
    public void AnInteriorOrSpawnedStageCannotUseTheOutdoorException(string fault)
    {
        var m=Mission();if(fault=="site")m.phases[1].site="Normal";
        if(fault=="actor")m.npcs.Add(new MissionNpc{at=2});if(fault=="object")m.phases[1].objects.Add(new MissionObjectDef());
        if(fault=="wave")m.phases[1].waves.Add(new MissionWave{count=1});if(fault=="goal")m.phases[1].missionObjectives[0].type=MissionGoalType.Talk;
        Assert.False(ZionAfter1212OutsideStage.Matches(m,1));Assert.Throws<InvalidDataException>(()=>ZionAfter1212OutsideStage.Require(m,1,State()));
    }
    [Theory] [InlineData("owner")] [InlineData("mission")] [InlineData("phase")] [InlineData("site")] [InlineData("waypoint")] [InlineData("actor")] [InlineData("object")]
    public void CurrentCheckpointMustHaveNoGeneratedArea(string fault)
    {
        var s=State();if(fault=="owner")s.Identity="foreign";if(fault=="mission")s.Mission++;if(fault=="phase")s.Phase++;
        if(fault=="site")s.OpenFloorCurrentSiteIndex=0;if(fault=="waypoint")s.Waypoints=new[]{new MissionWaypoint()};
        if(fault=="actor")s.Mobs.Add(new MissionProgress.SavedMob());if(fault=="object")s.Objects.Add(new MissionProgress.SavedObject());
        Assert.Throws<InvalidDataException>(()=>ZionAfter1212OutsideStage.Require(Mission(),1,s));
    }
}
