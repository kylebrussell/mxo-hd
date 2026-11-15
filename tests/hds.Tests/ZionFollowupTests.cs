using hds;
using HeadlessClient;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public class ZionFollowupTests
{
    private static List<Mission> Load()
    {
        string root=AppContext.BaseDirectory;while(!Directory.Exists(Path.Combine(root,"data","missions")))root=Path.GetDirectoryName(root)!;
        return Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    }
    [Fact] public void ExactContiguousStorySliceFollowsOnlySeededEarlierPrerequisites()
    {
        var all=Load();var selected=all.Where(m=>ZionFollowupFixture.Missions.Contains(m.id)).OrderBy(m=>m.order).ToList();
        Assert.Equal(ZionFollowupFixture.Missions,selected.Select(m=>m.id));Assert.Equal(new[]{1203,1204,1205,1206,1207},selected.Select(m=>m.order));
        Assert.Equal(8,selected.Sum(m=>m.phases.Count));Assert.Equal(27,selected.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        Assert.Empty(ZionFollowupFixture.Prerequisites.Intersect(ZionFollowupFixture.Missions));
        var contact=MissionContacts.ForOrg(1,0);var completed=ZionFollowupFixture.Prerequisites.ToHashSet();
        foreach(var mission in selected)
        {
            var offers=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),completed);
            Assert.Equal(mission.id,Assert.Single(offers.Where(m=>m.plot)).id);Assert.Contains(offers,m=>m.id==20001&&!m.plot);completed.Add(mission.id);
        }
    }
    [Fact] public void ActualListSelectionUsesPlotAndIndexWithStandardBeforeCritical()
    {
        byte[] Entry(ushort index,byte plot,string title){var b=new byte[18+title.Length];b[0]=0x80;b[1]=0x96;BitConverter.GetBytes(index).CopyTo(b,4);b[14]=plot;System.Text.Encoding.ASCII.GetBytes(title).CopyTo(b,17);return b;}
        var entries=new[]{Entry(0,0,"Courier"),Entry(7,1,"Redpill Rescue"),Entry(9,0,"Redpill Rescue")};
        Assert.Equal(7,MissionContactChoice.CriticalIndex(entries,"Redpill Rescue"));
        var history=new[]{Entry(2,1,"Redpill Rescue")}.ToList();int listStart=history.Count;history.AddRange(entries);
        Assert.Equal(7,MissionContactChoice.CriticalIndex(history.Skip(listStart),"Redpill Rescue"));
        Assert.Throws<InvalidOperationException>(()=>MissionContactChoice.CriticalIndex(history,"Redpill Rescue"));
        Assert.Throws<InvalidOperationException>(()=>MissionContactChoice.CriticalIndex(entries,"Unknown"));
        Assert.Throws<InvalidOperationException>(()=>MissionContactChoice.CriticalIndex(entries.Append(Entry(8,1,"Redpill Rescue")),"Redpill Rescue"));
    }
    [Fact] public void ModeOnlySeedsEarlierReceiptsAndLayoutWithNoRandomOverride()
    {
        var p=new ZionFollowupMode().Plan;Assert.Equal(ZionFollowupFixture.SeedPrerequisites(110),p.AfterHelpers[0]);
        Assert.True(p.DevCommands);Assert.False(p.FixedCallbackRolls);Assert.False(p.GmCharacter110);Assert.Equal("101203",Assert.Single(p.EnvironmentOverrides).Value);
    }
}
