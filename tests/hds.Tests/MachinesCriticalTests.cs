using hds;
using HeadlessClient;
using NativeQa.Modes;
using System.Xml;
namespace hds.Tests;
public class MachinesCriticalTests
{
    private static List<Mission> Load()
    {
        return Directory.GetFiles(DataLoader.DataPath("missions"),"*.xml").Select(f=>{var d=new XmlDocument();d.Load(f);return MissionXml.Parse(d.DocumentElement!);}).ToList();
    }
    [Fact] public void CompleteChapterFollowsExactEarlierReceiptsThroughAgentGray()
    {
        var all=Load();var selected=all.Where(m=>MachinesCriticalFixture.Missions.Contains(m.id)).OrderBy(m=>m.order).ToList();
        Assert.Equal(MachinesCriticalFixture.Missions,selected.Select(m=>m.id));Assert.Equal(new[]{1101,1102,1103},selected.Select(m=>m.order));
        Assert.Equal(5,selected.Sum(m=>m.phases.Count));Assert.Equal(14,selected.Sum(m=>m.phases.Sum(p=>p.missionObjectives.Count)));
        Assert.Equal(17,MachinesCriticalFixture.Prerequisites.Length);Assert.Empty(MachinesCriticalFixture.Prerequisites.Intersect(MachinesCriticalFixture.Missions));
        var contact=MissionContacts.ForOrg(2,0);Assert.Equal(2001u,contact.id);var completed=MachinesCriticalFixture.Prerequisites.ToHashSet();
        foreach(var mission in selected)
        {
            Assert.Equal("Agent Gray",mission.contact);Assert.Equal((byte)2,mission.org);
            var offers=DataLoader.ContactOfferWithStandards(all.Where(m=>MissionContacts.Offers(contact,m)).OrderBy(m=>m.order).ToList(),completed);
            Assert.Equal(mission.id,Assert.Single(offers.Where(m=>m.plot)).id);completed.Add(mission.id);
        }
    }
    [Fact] public void NativeModeOnlySeedsEarlierReceiptsAndSiteSelection()
    {
        var p=new MachinesCriticalMode().Plan;Assert.Equal(MachinesCriticalFixture.SeedPrerequisites(110),p.AfterHelpers[0]);
        Assert.True(p.DevCommands);Assert.False(p.FixedCallbackRolls);Assert.False(p.GmCharacter110);
        Assert.Equal("201101",Assert.Single(p.EnvironmentOverrides).Value);Assert.Equal(new ushort[]{2,17,1006,77},p.Abilities);
    }
    [Fact] public void CurrentListSelectionRejectsStaleSameTitleAndStandardNames()
    {
        byte[] Entry(ushort index,byte plot,string title){var b=new byte[18+title.Length];b[0]=0x80;b[1]=0x96;BitConverter.GetBytes(index).CopyTo(b,4);b[14]=plot;System.Text.Encoding.ASCII.GetBytes(title).CopyTo(b,17);return b;}
        var history=new List<byte[]>{Entry(2,1,"Interference")};int start=history.Count;
        history.Add(Entry(9,0,"Interference"));history.Add(Entry(7,1,"Interference"));
        Assert.Equal(7,MissionContactChoice.CriticalIndex(history.Skip(start),"Interference"));
        Assert.Throws<InvalidOperationException>(()=>MissionContactChoice.CriticalIndex(history,"Interference"));
    }
}
