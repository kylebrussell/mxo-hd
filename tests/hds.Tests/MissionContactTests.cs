using hds;

namespace hds.Tests;

/// <summary>The client's own mission contacts (data/mission_contacts.csv, from sponsors.xml) and the list packets that name them.</summary>
public class MissionContactTests
{
    static MissionContactTests()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "mission_contacts.csv")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        MissionContacts.Load(Path.Combine(dir, "data", "mission_contacts.csv"));
    }

    [Fact]
    public void AllOfTheClientsContactsLoad()
    {
        Assert.Equal(67, MissionContacts.All.Count);
        Assert.Equal(53, MissionContacts.All.Count(c => c.IsNeighbourhood));

        MissionContact newsie = MissionContacts.ById(47)!;
        Assert.Equal("The Newsie", newsie.name);
        Assert.Equal(0x5800140bu, newsie.nameId);
        Assert.Equal(0x0a0011b7u, newsie.face);

        MissionContact veil = MissionContacts.ForOrg(6);
        Assert.Equal(("Veil", 2005u, 0xdc0000a8u, "vei", 0x6000011du), (veil.name, veil.id, veil.nameId, veil.code, veil.introTextId));
    }

    [Fact]
    public void ContactsMatchRetailsCaptures()
    {
        // Every contact id, name id and portrait retail sent in a list header or a mission contact NPC
        (uint id, uint nameId, uint face)[] retail =
        {
            (2000, 0xb4000031, 0x28000cc0), (2001, 0xb4000032, 0x28000de7), (47, 0x5800140b, 0x0a0011b7),
            (48, 0x5800140d, 0x0a0011b1), (50, 0x58001411, 0x0a001186)
        };
        foreach (var (id, nameId, face) in retail)
        {
            MissionContact contact = MissionContacts.ById(id)!;
            Assert.Equal((nameId, face), (contact.nameId, contact.face));
        }
    }

    [Fact]
    public void AnOrgsContactIsPickedByReputation()
    {
        MissionContact junior = new MissionContact { id = 1, org = 1, name = "Junior", maxRep = 999 };
        MissionContact senior = new MissionContact { id = 2, org = 1, name = "Senior", minRep = 1000 };
        MissionContact gray = new MissionContact { id = 3, org = 2, name = "Gray" };
        MissionContact[] contacts = { junior, senior, gray };

        Assert.Same(junior, MissionContacts.Pick(contacts, 1, 0));
        Assert.Same(junior, MissionContacts.Pick(contacts, 1, -500));
        Assert.Same(senior, MissionContacts.Pick(contacts, 1, 1000));
        Assert.Same(gray, MissionContacts.Pick(contacts, 2, 5000));
        Assert.Null(MissionContacts.Pick(contacts, 3, 0));
        Assert.Null(MissionContacts.Pick(contacts, 0, 0));
        Assert.Equal("Tyndall", MissionContacts.ForOrg(0).name);
    }

    [Fact]
    public void AgentGraysListIsRetails()
    {
        // Retail (Machines list): 80 95 header, then 80 96 for each of the 7 generic missions, then 80 97
        string[] titles = { "Assassination", "Courier", "Retrieval", "Rescue", "Escort", "Infiltration", "Recruitment" };
        List<Mission> missions = titles.Select(t => new Mission { title = t, org = 2 }).ToList();

        List<byte[]> list = ServerPackets.BuildMissionList(1, MissionContacts.ForOrg(2), missions);

        Assert.Equal("8095000000000100070000" + "02d1070000320000b4e70d0028", Hex(list[0]));
        Assert.Equal("8096010000000f0002d107000000000e00" + Hex("Assassination") + "00", Hex(list[1]));
        Assert.Equal("80970100", Hex(list[^1]));
        Assert.Equal(9, list.Count);
    }

    [Fact]
    public void TheNewsiesListIsRetails()
    {
        List<Mission> missions = new List<Mission> { new Mission { title = "Unhealthy Competition", plot = true } };

        List<byte[]> list = ServerPackets.BuildMissionList(0, MissionContacts.ById(47)!, missions);

        Assert.Equal("809500000000000001000100" + "2f0000000b140058b711000a", Hex(list[0]));
        Assert.Equal("8096000000000f00002f00000000011600" + Hex("Unhealthy Competition") + "00", Hex(list[1]));
        Assert.Equal("80970000", Hex(list[2]));
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private static string Hex(string text) => Hex(System.Text.Encoding.ASCII.GetBytes(text));
}
