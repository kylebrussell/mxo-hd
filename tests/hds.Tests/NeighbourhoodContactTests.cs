using System.Xml;
using hds;

namespace hds.Tests;

/// <summary>The neighbourhood contacts in the world (data/mission_contact_npcs.csv) and what they offer.</summary>
public class NeighbourhoodContactTests
{
    static NeighbourhoodContactTests()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "mission_contact_npcs.csv")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        MissionContacts.Load(Path.Combine(dir, "data", "mission_contacts.csv"));
        MissionContactNpcs.Load(Path.Combine(dir, "data", "mission_contact_npcs.csv"));
    }

    [Fact]
    public void EveryContactStaticHasItsContact()
    {
        IReadOnlyList<MissionContactSpot> spots = MissionContactNpcs.All;
        Assert.Equal(51, spots.Count);
        Assert.Equal(51, spots.Select(s => s.contactId).Distinct().Count());
        Assert.All(spots, s => Assert.True(s.Contact!.IsNeighbourhood));
        Assert.Equal((23, 17, 11), (spots.Count(s => s.metrId == 1), spots.Count(s => s.metrId == 2), spots.Count(s => s.metrId == 3)));
    }

    [Fact]
    public void ContactsStandWhereRetailSpawnedThem()
    {
        // Retail creation records: static id -> contact id
        (uint staticId, uint contact, string rsi)[] retail =
        {
            (0x04b0230f, 47, "7e0b0058"), (0x14d02e71, 48, "320a0058"), (0x2d5027e3, 50, "7a0b0058"), (0x01200086, 33, "7c0b0058"),
            (0x032027bf, 44, "730a0058"), (0x03402080, 51, "240d0098"), (0x094000ca, 2, "340a0058"), (0x09f0002d, 26, "480a0058"),
            (0x0c5005ad, 35, "8f0a0058"), (0x0d7021e5, 34, "790b0058"), (0x17f00357, 29, "7f0b0058"), (0x2ba03d67, 36, "7d0a0058"),
            (0x2e300d82, 3, "350a0058"), (0x46500a95, 4, "7a0a0058")
        };
        foreach (var (staticId, contact, rsi) in retail)
        {
            MissionContactSpot spot = MissionContactNpcs.SpotOf(contact)!;
            Assert.Equal((staticId, rsi), (spot.staticId, spot.rsiHex));
        }
    }

    [Fact]
    public void TheNewsieIsCreatedAsRetailCreatedHim()
    {
        GameObject newsie = MissionContactNpcs.BuildContactObject(MissionContactNpcs.SpotOf(47)!);

        Assert.Equal(0x04b0230fu, newsie.GetRelatedStaticObjId());
        // Retail: ... cd ab 05 3d <position (-51650, 1295, 13350)> "The Newsie" in 32 bytes, 7e0b0058, 2f000000, <orientation>
        string expected = "053d" + "000000004038e9c0" + "00000000003c9440" + "000000000013ca40" +
                          Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes("The Newsie")).ToLowerInvariant() + new string('0', 44) +
                          "7e0b0058" + "2f000000" + "00000000f20435bf00000000f304353f";
        Assert.Equal(expected, Convert.ToHexString(newsie.GetCreationAttributes()).ToLowerInvariant());
    }

    [Fact]
    public void NeighbourhoodContactsOfferTheirOwnMissions()
    {
        Mission newsies = new Mission { org = 0, contact = "The Newsie" };
        Mission intro = new Mission { org = 0, contact = "Tyndall" };
        MissionContact newsie = MissionContacts.ById(47)!, chef = MissionContacts.ById(48)!, tyndall = MissionContacts.ForOrg(1);

        Assert.True(MissionContacts.Offers(newsie, newsies));
        Assert.False(MissionContacts.Offers(chef, newsies));
        Assert.False(MissionContacts.Offers(tyndall, newsies));
        Assert.False(MissionContacts.Offers(newsie, intro));
        Assert.True(MissionContacts.Offers(tyndall, intro));
        Assert.Equal(0, MissionContacts.PacketOrg(newsies));
        Assert.Equal(1, MissionContacts.PacketOrg(intro));
        Assert.Equal(40u, MissionContacts.Named("The Bartender")!.id);
    }

    [Fact]
    public void TheNewsiesAcceptIsRetails()
    {
        // Retail (Unhealthy Competition, txn 9, instance 0x330): org 0000 and The Newsie's ids
        string expected = "809c" + "00000000" + "0900" + "2b00" + "0000" + "3003" + "02" + "2f000000" + "0b140058" + "b711000a" +
                          "14000026" + "700a0028" + "010000" + "4600" + "4300" + "00" + "1600" +
                          Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes("Unhealthy Competition")).ToLowerInvariant() + "00" +
                          "010000" + "0100" + "e0060000";
        byte[] message = ServerPackets.BuildMissionSetName(9, 0x330, "Unhealthy Competition", 1760, "The Newsie", 0);
        Assert.Equal(expected, Convert.ToHexString(message).ToLowerInvariant());
    }

    [Fact]
    public void TheCapturedNeighbourhoodMissionsLoad()
    {
        foreach (var (file, contact, id) in new[] { ("hood_newsie_unhealthy_competition.xml", "The Newsie", 1760),
                     ("hood_chef_paging.xml", "The Chef", 1430), ("hood_mr_black_off_white.xml", "Mr. Black", 1674) })
        {
            XmlDocument document = new XmlDocument();
            document.Load(Path.Combine(FindData(), "missions", file));
            Mission mission = MissionXml.Parse(document.DocumentElement!)!;
            Assert.Equal((id, (ushort)2, (byte)0), (mission.id, mission.district, mission.org));
            Assert.True(MissionContacts.Offers(MissionContacts.Named(contact)!, mission));
            Assert.True(mission.generated);
        }
    }

    [Fact]
    public void EveryNeighbourhoodContactHasGenericMissions()
    {
        foreach (MissionContactSpot spot in MissionContactNpcs.All)
        {
            List<Mission> missions = NeighbourhoodMissions.Build(spot.Contact!, spot);
            Assert.Equal(new[] { "Assassination", "Retrieval", "Rescue" }, missions.Select(m => m.title));
            Assert.All(missions, m =>
            {
                Assert.Equal(spot.metrId, m.district);
                Assert.Equal(spot.Contact!.name, m.contact);
                Assert.True(MissionContacts.Offers(spot.Contact, m));
                Assert.False(m.plot);
                Assert.True(m.generated);
                Assert.InRange(m.id, 3000, 3999);
            });
        }

        Mission rescue = NeighbourhoodMissions.Build(MissionContacts.ById(7)!, MissionContactNpcs.SpotOf(7))[2];
        Assert.Equal(new[] { "Tabor Park" }, rescue.neighbourhoods);
        Assert.Equal(new[] { MissionGoalType.Escort, MissionGoalType.Protect }, rescue.phases[0].missionObjectives.Select(o => o.type));
    }

    private static string FindData()
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "data", "missions")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        return Path.Combine(dir, "data");
    }
}
