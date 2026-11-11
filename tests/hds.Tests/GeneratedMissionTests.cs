using System.Xml;
using hds;

namespace hds.Tests;

/// <summary>Missions placed at accept time from the world's mission markup (docs/MISSIONS.md, "Generated phases").</summary>
public class GeneratedMissionTests
{
    private const string Generated = @"<mission id=""2101"" title=""G"" description=""D"" plot=""1"" order=""11"" org=""3"" contact=""Flood"" exp=""22000"" info=""42000"">
        <npc id=""1"" handle=""Lucien"" type=""FRIENDLY"" at=""1"" rsi=""c6020058"" />
        <npc id=""2"" handle=""Odile"" type=""FRIENDLY"" at=""1"" rsi=""a8060090"" />
        <npc id=""3"" handle=""Marcel"" type=""FRIENDLY"" at=""2"" rsi=""c5020058"" />
        <phase site=""Office"" enemy=""Machines"" enemies=""5"">
          <briefing>Go.</briefing>
          <enter><say>You're there.</say></enter>
          <object id=""1"" kind=""Safe"" />
          <objective goal=""1"" text=""Kill all of the Machine programs"" type=""DefeatAll"" />
          <objective goal=""2"" text=""Get the dossier from the safe"" type=""Search"" object=""1"" item=""6799"" />
          <objective goal=""3"" text=""Lead both hostages out of the area"" type=""Escort"" npc=""1,2"" to=""exit"" />
          <objective goal=""4"" text=""Do not let either hostage die"" type=""Protect"" npc=""1, 2"" />
          <objective goal=""5"" text=""Get mapdisk (optional)"" type=""Search"" object=""1"" item=""6783"" optional=""1"" />
        </phase>
        <phase site=""Normal"">
          <objective goal=""1"" text=""Lead both hostages to Marcel"" type=""Escort"" npc=""1,2"" to=""npc:3"" />
          <objective goal=""2"" text=""Upload the dossier at a hardline"" type=""Upload"" item=""6799"" after=""1"" />
        </phase>
      </mission>";

    private static Mission Parse(string xml = Generated)
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xml);
        return MissionXml.Parse(doc.DocumentElement!);
    }

    [Fact]
    public void ReadsAGeneratedMission()
    {
        Mission mission = Parse();

        Assert.Equal(3, mission.org);
        Assert.Equal("Flood", mission.contact);
        Assert.True(mission.generated);
        Assert.Equal(1, mission.npcs[0].at);
        Assert.Equal(2, mission.npcs[2].at);

        MissionPhase phase = mission.phases[0];
        Assert.Equal("Office", phase.site);
        Assert.Equal("Machines", phase.enemy);
        Assert.Equal(5, phase.enemies);
        Assert.Equal("You're there.", Assert.Single(phase.enterLines).text);
        Assert.Equal("Safe", Assert.Single(phase.objects).kind);
        Assert.Empty(phase.waypoints);

        MissionObjective search = phase.missionObjectives[1];
        Assert.Equal(MissionGoalType.Search, search.type);
        Assert.Equal(1, search.objectId);
        Assert.Equal(6799u, search.item);

        MissionObjective escort = phase.missionObjectives[2];
        Assert.Equal(MissionGoalType.Escort, escort.type);
        Assert.Equal(new ushort[] { 1, 2 }, escort.Npcs);
        Assert.Equal(1, escort.npcId);
        Assert.True(escort.Has(2));
        Assert.Equal("exit", escort.escortTo);
        Assert.Equal(new ushort[] { 1, 2 }, phase.missionObjectives[3].Npcs);
        Assert.True(phase.missionObjectives[4].optional);
        Assert.Equal("npc:3", mission.phases[1].missionObjectives[0].escortTo);
    }

    [Fact]
    public void AMissionWithoutAnOrgIsOfferedToEveryone()
    {
        Mission mission = Parse(@"<mission id=""1"" title=""T""><phase><objective goal=""1"" text=""x"" type=""Talk"" npc=""1"" /></phase></mission>");

        Assert.Equal(0, mission.org);
        Assert.Equal("Tyndall", mission.contact);
        Assert.False(mission.generated);
        Assert.Equal(new ushort[] { 1 }, mission.phases[0].missionObjectives[0].Npcs);
    }

    [Fact]
    public void FindsTheOpenGoalForAnyNpcItNames()
    {
        MissionProgress progress = new MissionProgress(Parse());

        Assert.Equal(3, progress.FindOpen(MissionGoalType.Escort, 2)!.goal);
        Assert.Equal(4, progress.FindOpen(MissionGoalType.Protect, 1)!.goal);
        Assert.Null(progress.FindOpen(MissionGoalType.Escort, 3));
    }

    [Fact]
    public void OptionalAndProtectGoalsDontHoldUpThePhase()
    {
        MissionProgress progress = new MissionProgress(Parse());
        List<MissionObjective> goals = progress.Phase!.missionObjectives;

        progress.Complete(goals[0]);
        progress.Complete(goals[1]);
        Assert.False(progress.AllDone);

        progress.Complete(goals[2]);
        Assert.True(progress.AllDone);
        Assert.True(progress.waitingForExit);
    }

    [Fact]
    public void LeavingByTheStreetDoorCountsAsLeaving()
    {
        List<MissionWaypoint> doors = new List<MissionWaypoint> { new MissionWaypoint { x = 1000, y = 2545, z = 0 } };
        List<double[]> exits = new List<double[]> { new double[] { 1000, 145, 0 } };

        Assert.False(MissionExit.HasLeft(doors, exits, 1000, 1345, 0)); // a floor in between
        Assert.True(MissionExit.HasLeft(doors, exits, 1200, 145, 100));  // beside the street door
        Assert.True(MissionExit.HasLeft(doors, null, 1300, 2545, 0));    // beside the floor's door
    }

    private static StaticWorldObject Static(ushort goid, double x, double y, double z, uint id = 0)
    {
        return new StaticWorldObject
        {
            metrId = 1, sectorID = 405, mxoStaticId = id, type = BitConverter.GetBytes(goid),
            pos_x = x, pos_y = y, pos_z = z, quat = "0000000000000000000000000000803f"
        };
    }

    /// <summary>
    /// One GenericApt floor at 2545 (its pointers 320 above, as in Welcome to the Matrix's building), four NPC spawners, a
    /// filing cabinet, a computer spawner, and two elevators: one stopping from the street up, one only from 545.
    /// </summary>
    private static List<StaticWorldObject> AFloor()
    {
        return new List<StaticWorldObject>
        {
            Static(2539, 1000, 2865, 1000), Static(2539, 1600, 2865, 1000), Static(2539, 1300, 2875, 1400),
            Static(2539, 1300, 2865, 1000), // a repeated row, as the district files have
            Static(MissionSites.NpcSpawner, 1000, 2545, 1100), Static(MissionSites.NpcSpawner, 1500, 2545, 1100),
            Static(MissionSites.NpcSpawner, 1200, 2542, 1300), Static(MissionSites.NpcSpawner, 1600, 2545, 1300),
            Static(326, 1100, 2545, 1200), Static(1415, 1400, 2625, 1250),
            Static(MissionSites.ElevatorDoor1, 500, 145, 1000, 0x10), Static(MissionSites.ElevatorDoor1, 500, 2545, 1000, 0x11),
            Static(MissionSites.ElevatorDoor2, 1250, 545, 1200, 0x20), Static(MissionSites.ElevatorDoor2, 1250, 2545, 1200, 0x21),
            Static(2539, 1000, 865, 1000), // a floor above the street without NPC spawners: not a site
            Static(326, 9000, 2545, 9000)  // furniture somewhere else
        };
    }

    [Fact]
    public void AFloorOfStylePointersIsASite()
    {
        MissionSite site = Assert.Single(MissionSites.Build(AFloor(), 1));

        Assert.Equal("GenericApt", site.style);
        Assert.Equal(2545, site.floorY);
        Assert.Equal(4, site.npcSpots.Count);
        Assert.All(site.npcSpots, spot => Assert.True(spot[1] <= 2495)); // 50 below their spawners
        Assert.Single(site.containers);
        Assert.Single(site.objectSpots);

        // The car that also stops at the street, though the other is nearer the middle
        Assert.Equal(500, site.elevatorDoor.pos_x);
        Assert.Equal(2545, site.elevatorDoor.pos_y);
        Assert.Equal(145, site.lobbyDoor.pos_y);
        Assert.Equal("11000000", MissionSites.DoorHex(site.elevatorDoor));
        Assert.True(MissionSites.InShaft(site, 550, 1050));
        Assert.False(MissionSites.InShaft(site, 5000, 1000));
    }

    [Fact]
    public void SitesArePickedByStyleAndNotReused()
    {
        List<MissionSite> sites = MissionSites.Build(AFloor(), 1);
        Random rng = new Random(1);

        Assert.Same(sites[0], MissionSites.Choose(sites, "GenericApt", 0, 0, 3, 1, rng));
        Assert.Same(sites[0], MissionSites.Choose(sites, "Normal", 0, 0, 3, 1, rng));
        Assert.Null(MissionSites.Choose(sites, "Office", 0, 0, 3, 1, rng));
        Assert.Null(MissionSites.Choose(sites, "GenericApt", 0, 0, 3, 5, rng)); // not enough to search
        Assert.Null(MissionSites.Choose(sites, "Any", 0, 0, 3, 1, rng, exclude: new List<MissionSite> { sites[0] }));
    }

    [Fact]
    public void ASafeNeedsAPlaceOnTheFloor()
    {
        // A spawned kind (Safe) counts: a floor with no furniture and no spawner was picked for it, and it was never placed
        MissionPhase phase = new MissionPhase();
        phase.objects.Add(new MissionObjectDef { id = 1, kind = "Safe" });
        Assert.Equal(1, MissionStaging.PlacesNeeded(phase));

        MissionSite bare = SiteAt(0, 0, "Pearlridge");
        Assert.Null(MissionSites.Choose(new[] { bare }, "Any", 0, 0, 1, MissionStaging.PlacesNeeded(phase), new Random(1)));
        Assert.Same(bare, MissionSites.Choose(new[] { bare }, "Any", 0, 0, 1, 0, new Random(1)));
    }

    private static MissionSite SiteAt(double x, double z, string hood)
    {
        MissionSite site = new MissionSite
        {
            style = "SlumApt", neighbourhood = hood, minX = x - 100, maxX = x + 100, minZ = z - 100, maxZ = z + 100,
            elevatorDoor = Static(MissionSites.ElevatorDoor1, x, 545, z)
        };
        site.npcSpots.Add(new double[] { x, 495, z, 0 });
        return site;
    }

    [Fact]
    public void SitesAreAboutAsFarAsRetailsAndInTheGuidesNeighbourhoods()
    {
        MissionSite near = SiteAt(3000, 0, "Mara"), retailFar = SiteAt(15800, 0, "Magog"), retailFarInMara = SiteAt(0, 16000, "Mara"),
            tooFar = SiteAt(40000, 0, "Mara");
        List<MissionSite> sites = new List<MissionSite> { near, retailFar, retailFarInMara, tooFar };
        Random rng = new Random(3);

        // Retail's 150 to 165 m, and of those the one in the guide's neighbourhoods
        Assert.Same(retailFarInMara, MissionSites.Choose(sites, "Normal", 0, 0, 1, 0, rng, neighbourhoods: new List<string> { "Mara" }));
        // No neighbourhood in the band: still the distance
        Assert.Contains(MissionSites.Choose(sites, "Normal", 0, 0, 1, 0, rng, neighbourhoods: new List<string> { "Zia" }),
            new[] { retailFar, retailFarInMara });
        // Nothing in the band: the nearest to it
        Assert.Same(near, MissionSites.Choose(new List<MissionSite> { near, tooFar }, "Normal", 0, 0, 1, 0, rng));
    }

    [Fact]
    public void TheNeighbourhoodMapLoads()
    {
        Dictionary<ushort, string> hoods = MissionSites.LoadNeighbourhoods(Path.Combine(MissionsDir(), "..", "slums_neighbourhoods.csv"));

        Assert.Equal(1493, hoods.Count);
        Assert.Equal("Tabor Park", hoods[405]); // Welcome to the Matrix's building
        Assert.Equal("Achan", hoods[1349]);     // The Internuncio's first
    }

    [Fact]
    public void StylesMatchTheGuidesAreaTypes()
    {
        Assert.True(MissionSites.Matches("Normal", "SlumApt"));
        Assert.True(MissionSites.Matches("normal", "Warehouse"));
        Assert.False(MissionSites.Matches("Normal", "RedPillExtraction"));
        Assert.True(MissionSites.Matches("RedPillExtraction", "RedPillExtraction"));
        Assert.True(MissionSites.Matches("Any", "Bank"));
    }

    [Fact]
    public void EachContactOffersItsOwnMissionsAndTyndallTheIntroduction()
    {
        Mission intro = new Mission { org = 0 }, zion = new Mission { org = 1 }, machines = new Mission { org = 2 }, exile = new Mission { org = 3 };

        Assert.Equal("Tyndall", MissionContacts.ForOrg(1).name);
        Assert.Equal("Agent Gray", MissionContacts.ForOrg(2).name);
        Assert.Equal("Flood", MissionContacts.ForOrg(3).name);
        Assert.True(MissionContacts.Offers(1, intro));
        Assert.True(MissionContacts.Offers(1, zion));
        Assert.False(MissionContacts.Offers(1, machines));
        Assert.True(MissionContacts.Offers(2, machines));
        Assert.False(MissionContacts.Offers(2, intro));
        Assert.True(MissionContacts.Offers(3, exile));
        Assert.False(MissionContacts.Offers(3, zion));
    }

    [Fact]
    public void ContactsHaveRetailsIds()
    {
        Assert.Equal((2000u, 0xb4000031u, 0x28000cc0u), MissionContacts.For("Tyndall"));
        Assert.Equal((2001u, 0xb4000032u, 0x28000de7u), MissionContacts.For("Agent Gray"));
        Assert.Equal((2002u, 0xb4000038u, 0x28000de6u), MissionContacts.For("Flood"));
    }

    private static string MissionsDir()
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "data", "missions")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        return Path.Combine(dir, "data", "missions");
    }

    public static IEnumerable<object[]> MissionFiles()
    {
        return Directory.GetFiles(MissionsDir(), "*.xml").Select(f => new object[] { Path.GetFileName(f) });
    }

    /// <summary>Every shipped mission refers only to NPCs, objects and goals it has, and a player can finish each phase.</summary>
    [Theory]
    [MemberData(nameof(MissionFiles))]
    public void EveryMissionFileHangsTogether(string file)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(Path.Combine(MissionsDir(), file));
        Mission mission = MissionXml.Parse(doc.DocumentElement!);

        Assert.NotNull(mission);
        Assert.NotEmpty(mission.phases);
        Assert.True(mission.exp > 0 && mission.info > 0);
        HashSet<ushort> npcs = mission.npcs.Select(n => n.id).ToHashSet();
        Assert.Equal(mission.npcs.Count, npcs.Count);
        foreach (MissionNpc npc in mission.npcs)
        {
            Assert.InRange(npc.at, 0, mission.phases.Count);
            Assert.False(string.IsNullOrEmpty(npc.rsiHex), file + ": " + npc.handle + " has no appearance");
        }

        for (int p = 0; p < mission.phases.Count; p++)
        {
            MissionPhase phase = mission.phases[p];
            string where = file + " phase " + (p + 1);
            Assert.True(phase.site != null || phase.waypoints.Count > 0, where + ": nowhere to go");
            Assert.Contains(phase.missionObjectives, o => !o.optional && o.type != MissionGoalType.Protect);
            HashSet<int> goals = phase.missionObjectives.Select(o => o.goal).ToHashSet();
            HashSet<ushort> objects = phase.objects.Select(o => o.id).ToHashSet();
            foreach (MissionObjective objective in phase.missionObjectives)
            {
                string goal = where + " goal " + objective.goal + " (" + objective.description + ")";
                Assert.False(string.IsNullOrWhiteSpace(objective.description), goal);
                Assert.True(objective.afterGoal < 0 || goals.Contains(objective.afterGoal), goal + ": after a goal that isn't there");
                switch (objective.type)
                {
                    case MissionGoalType.DefeatAll:
                        Assert.True(phase.enemies > 0 || mission.npcs.Any(n => !n.friendly), goal + ": nobody to defeat");
                        break;
                    case MissionGoalType.Search:
                    case MissionGoalType.Put:
                    case MissionGoalType.Use:
                        Assert.True(objects.Contains(objective.objectId), goal + ": no object " + objective.objectId);
                        Assert.True(objective.type != MissionGoalType.Put || objective.item != 0, goal + ": nothing to put");
                        break;
                    case MissionGoalType.Upload:
                        break;
                    default:
                        Assert.All(objective.Npcs, id => Assert.True(npcs.Contains(id), goal + ": no NPC " + id));
                        break;
                }

                if (objective.type == MissionGoalType.Get || objective.type == MissionGoalType.Give || objective.type == MissionGoalType.Loot)
                {
                    Assert.True(objective.item != 0, goal + ": no item");
                }

                if (objective.type == MissionGoalType.Escort && objective.escortTo.StartsWith("npc:"))
                {
                    Assert.Contains(ushort.Parse(objective.escortTo.Substring(4)), npcs);
                }

                if (objective.type == MissionGoalType.Escort)
                {
                    Assert.True(objective.escortTo == "exit" || objective.escortTo == "hardline" || objective.escortTo.StartsWith("npc:") ||
                                (objective.escortTo.StartsWith("object:") && objects.Contains(ushort.Parse(objective.escortTo.Substring(7)))), goal);
                }
            }
        }
    }

    [Fact]
    public void MissionIdsAndChainPlacesAreUnique()
    {
        List<Mission> missions = Directory.GetFiles(MissionsDir(), "*.xml").Select(f =>
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(f);
            return MissionXml.Parse(doc.DocumentElement!);
        }).ToList();

        Assert.Equal(missions.Count, missions.Select(m => m.id).Distinct().Count());
        Assert.Equal(missions.Count, missions.Select(m => (m.org, m.contact, m.order)).Distinct().Count()); // each contact's chain
    }

    [Fact]
    public void TypographicPunctuationIsSentPlain()
    {
        Assert.Equal("I'm \"here\"... -- now - go", MissionXml.Plain("I’m “here”… — now – go"));
    }
}
