using System.Xml;
using HeadlessClient;
using hds;
namespace hds.Tests;
public class MachinesNoSiteUploadTests
{
    static Mission Load()
    {
        var d=new DirectoryInfo(AppContext.BaseDirectory);while(d!=null&&!File.Exists(Path.Combine(d.FullName,"data/missions/machines_1_2_3_hard_time.xml")))d=d.Parent;
        var xml=new XmlDocument();xml.Load(Path.Combine(d!.FullName,"data/missions/machines_1_2_3_hard_time.xml"));return MissionXml.Parse(xml.DocumentElement!);
    }
    [Fact] public void PrimaryHardlineSequenceHasFourPhasesAndOnlyTwoInteriorSites()
    {
        var m=Load();Assert.Equal(4,m.phases.Count);Assert.Equal(new[]{"Normal",null,"Normal",null},m.phases.Select(p=>p.site));
        Assert.Equal(new uint[]{6796,6795},m.phases[1].missionObjectives.Select(o=>o.item));Assert.Equal(6817u,Assert.Single(m.phases[3].missionObjectives).item);
        Assert.Equal(88000,m.exp);Assert.Equal(59500,m.info);
        foreach(int phase in new[]{1,3})
        {
            Assert.True(MachinesNoSiteUploadFixture.IsNoSiteUpload(m,phase));
            var progress=new MissionProgress(m){phaseIndex=phase-1};Assert.True(progress.NextPhase());
            Assert.Null(progress.site);Assert.Empty(progress.waypoints);Assert.Empty(progress.exits);
            Assert.False(MissionRuntime.NeedsToLeave(progress,progress.Phase.missionObjectives[0],0,0,0));
        }
    }
    [Theory] [InlineData("site")] [InlineData("enemy")] [InlineData("negative")] [InlineData("wave")] [InlineData("object")] [InlineData("actor")] [InlineData("goal")]
    public void NoSiteSkipNeverHidesGeometryOrOtherAuthoredGoals(string fault)
    {
        var m=Load();var p=m.phases[1];
        if(fault=="site")p.site="Normal";if(fault=="enemy")p.enemies=1;if(fault=="negative")p.enemies=-1;
        if(fault=="wave")p.waves.Add(new MissionWave{count=1});if(fault=="object")p.objects.Add(new MissionObjectDef());
        if(fault=="actor")m.npcs[0].at=2;if(fault=="goal")p.missionObjectives[0].type=MissionGoalType.Talk;
        Assert.False(MachinesNoSiteUploadFixture.IsNoSiteUpload(m,1));
    }
    static MissionProgress.SavedState Before()=>new(){Version=2,Identity=new string('a',32),Mission=201203,Instance=48,Started=1,Phase=0,Items=new(){{6796,1},{6795,2}}};
    static MissionProgress.SavedState After(){var s=Before();s.Phase=1;s.OpenFloorCurrentSiteIndex=-1;return s;}
    [Fact] public void CompletedDownHandoffOnlyBindsExactNextNoSitePhaseWithEarnedItems()
    {
        var m=Load();MachinesNoSiteUploadFixture.RequireTransition(Before(),After(),m);
        var after=After();after.Identity="other";Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
        after=After();after.Items.Clear();Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
        after=After();after.Phase=3;Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
        after=After();after.Waypoints=new[]{new MissionWaypoint()};Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
        after=After();after.OpenFloorCurrentSiteIndex=0;Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
        after=After();after.OpenFloorCurrentSiteIndex=null;Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
        after=After();after.Mobs.Add(new MissionProgress.SavedMob());Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
        after=After();after.Version=1;Assert.Throws<InvalidDataException>(()=>MachinesNoSiteUploadFixture.RequireTransition(Before(),after,m));
    }
}
