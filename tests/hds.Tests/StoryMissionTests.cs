using System.Text.RegularExpressions;
using System.Xml;
using hds;

namespace hds.Tests;

/// <summary>
/// Every file in data/missions: it parses, everything it names exists, and every goal can be done in the order the
/// file allows. The story missions transcribed from the screenshot logs (ids 100000 and up: org x 100000 + chapter x
/// 1000 + sub x 100 + number) are also held to clean game text.
/// </summary>
public class StoryMissionTests
{
    private static readonly string Root = FindRoot();
    private static readonly HashSet<string> Sites = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Normal", "Any", "SlumApt", "GenericApt", "Office", "RedPillExtraction", "ZionOutpost", "Derelict", "Warehouse", "Bank",
        "Hotel", "Skyscraper"
    };

    private static readonly HashSet<string> Enemies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Machines", "Merovingian", "Zion" };
    private static readonly HashSet<string> ObjectKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Computer", "Safe", "Mainframe", "Box", "Phone", "Furniture"
    };

    static StoryMissionTests()
    {
        MissionContacts.Load(Path.Combine(Root, "data", "mission_contacts.csv"));
    }

    private static string FindRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(dir, "data", "missions")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        return dir;
    }

    public static IEnumerable<object[]> Files()
    {
        return Directory.GetFiles(Path.Combine(FindRoot(), "data", "missions"), "*.xml").OrderBy(f => f)
            .Select(f => new object[] { Path.GetFileName(f) });
    }

    private static Mission Load(string file)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(Path.Combine(Root, "data", "missions", file));
        return MissionXml.Parse(doc.DocumentElement!);
    }

    private static HashSet<uint> GameObjectIds()
    {
        HashSet<uint> ids = new HashSet<uint>();
        foreach (string line in File.ReadLines(Path.Combine(Root, "data", "gameobjects.csv")))
        {
            string[] parts = line.Split(',');
            if (parts.Length > 1 && uint.TryParse(parts[1], out uint id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    [Fact]
    public void MissionIdsAreUnique()
    {
        List<(string file, int id)> ids = Files().Select(f => (string)f[0]).Select(f => (f, Load(f).id)).ToList();
        foreach (var group in ids.GroupBy(i => i.id))
        {
            Assert.True(group.Count() == 1, "id " + group.Key + " is used by " + string.Join(", ", group.Select(g => g.file)));
        }
    }

    [Theory]
    [InlineData("merovingian_12_1_1_hothouse.xml", 48125u, 48134u, 30, 20, 1)]
    [InlineData("merovingian_12_1_2_deep_cover.xml", 48128u, 48137u, 40, 40, 2)]
    [InlineData("merovingian_12_1_3_courtship.xml", 48131u, 48140u, 50, 60, 2)]
    public void Chapter12UsesTheShippedMissionTicketsAndGates(string file, uint ticket, uint completion,
        int level, int reputation, int district)
    {
        Mission mission = Load(file);
        Assert.Equal(ticket, mission.entryItem);
        Assert.Equal(completion, mission.completionItem);
        Assert.Equal(level, mission.minLevel);
        Assert.Equal(reputation, mission.minReputation);
        Assert.Equal(district, mission.district);
        Assert.False(MissionContacts.Offers(MissionContacts.Named("Flood"), mission));
    }

    [Fact]
    public void StoryMissionsComeInChapterOrder()
    {
        foreach (string file in Files().Select(f => (string)f[0]))
        {
            Mission mission = Load(file);
            if (mission.id < 100000)
            {
                continue;
            }

            // id = org x 100000 + chapter x 1000 + sub x 100 + number; order = chapter x 1000 + sub x 100 + number
            Assert.Equal(mission.org, mission.id / 100000);
            Assert.Equal(mission.id % 100000, mission.order);
            Match name = Regex.Match(file, @"^(zion|machines|merovingian)_(\d+)_(\d+)_(\d+)_[a-z0-9_]+\.xml$");
            Assert.True(name.Success, file + " isn't named <org>_<chapter>_<sub>_<number>_<title>.xml");
            int order = int.Parse(name.Groups[2].Value) * 1000 + int.Parse(name.Groups[3].Value) * 100 + int.Parse(name.Groups[4].Value);
            Assert.Equal(order, mission.order);
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EverythingAMissionNamesExists(string file)
    {
        Mission mission = Load(file);
        HashSet<uint> gameObjects = GameObjectIds();

        Assert.NotNull(mission);
        Assert.False(string.IsNullOrWhiteSpace(mission.title));
        Assert.NotNull(MissionContacts.Named(mission.contact));
        Assert.NotEmpty(mission.phases);
        Assert.Equal(mission.npcs.Count, mission.npcs.Select(n => n.id).Distinct().Count());

        foreach (MissionNpc npc in mission.npcs)
        {
            Assert.True(npc.at >= 0 && npc.at <= mission.phases.Count, file + ": " + npc.handle + " is placed in a phase that doesn't exist");
            Assert.False(string.IsNullOrEmpty(npc.rsiHex), file + ": " + npc.handle + " has no rsi");
            if (mission.generated)
            {
                Assert.True(npc.at > 0, file + ": " + npc.handle + " isn't placed (at) in a generated mission");
            }
        }

        for (int p = 0; p < mission.phases.Count; p++)
        {
            MissionPhase phase = mission.phases[p];
            string where = file + " phase " + (p + 1);
            if (phase.site != null)
            {
                Assert.Contains(phase.site, Sites);
                Assert.True(phase.enemies == 0 || Enemies.Contains(phase.enemy ?? ""), where + ": enemy '" + phase.enemy + "'");
            }

            Assert.NotEmpty(phase.missionObjectives);
            Assert.Equal(phase.missionObjectives.Count, phase.missionObjectives.Select(o => o.goal).Distinct().Count());
            Assert.Equal(phase.objects.Count, phase.objects.Select(o => o.id).Distinct().Count());
            foreach (MissionTrigger trigger in phase.triggers.Where(t => t.npcId != 0))
            {
                MissionNpc? near = mission.npcs.Find(n => n.id == trigger.npcId);
                Assert.True(near != null && (!mission.generated || near.at == p + 1 || FollowsInto(mission, near.id, p)),
                    where + ": a near trigger follows NPC " + trigger.npcId + ", who isn't in this phase");
            }

            foreach (MissionObjectDef obj in phase.objects)
            {
                Assert.Contains(obj.kind, ObjectKinds);
            }

            foreach (MissionObjective goal in phase.missionObjectives)
            {
                string what = where + " goal " + goal.goal + " (" + goal.description + ")";
                Assert.False(string.IsNullOrWhiteSpace(goal.description), what);
                if (goal.afterGoal >= 0)
                {
                    Assert.Contains(phase.missionObjectives, o => o.goal == goal.afterGoal);

                    // A kill only counts for a goal that is open when it happens
                    Assert.False(mission.id >= 100000 && (goal.type == MissionGoalType.Defeat || goal.type == MissionGoalType.DefeatAll),
                        what + ": a kill goal that waits for another can miss its kill");
                }

                switch (goal.type)
                {
                    case MissionGoalType.Talk:
                    case MissionGoalType.Get:
                    case MissionGoalType.Give:
                    case MissionGoalType.Defeat:
                    case MissionGoalType.Loot:
                    case MissionGoalType.Escort:
                    case MissionGoalType.Protect:
                        Assert.NotEmpty(goal.Npcs);
                        foreach (ushort id in goal.Npcs)
                        {
                            MissionNpc npc = mission.npcs.Find(n => n.id == id);
                            Assert.True(npc != null, what + ": no NPC " + id);
                            if (mission.generated)
                            {
                                Assert.True(npc!.at == p + 1 || FollowsInto(mission, id, p),
                                    what + ": " + npc.handle + " isn't in this phase");
                            }

                            if (goal.type == MissionGoalType.Defeat || goal.type == MissionGoalType.Loot)
                            {
                                Assert.False(npc!.friendly, what + ": " + npc.handle + " is friendly");
                            }

                            if (goal.type == MissionGoalType.Escort || goal.type == MissionGoalType.Protect || goal.type == MissionGoalType.Give)
                            {
                                Assert.True(npc!.friendly, what + ": " + npc.handle + " is hostile");
                            }

                            // A hostile can be found (reached) or hand something over only while it waits to be attacked
                            if ((goal.type == MissionGoalType.Talk || goal.type == MissionGoalType.Get) && !npc!.friendly)
                            {
                                Assert.False(npc.attacksOnSight, what + ": talking to a hostile who attacks on sight");
                            }
                        }

                        break;
                    case MissionGoalType.Search:
                    case MissionGoalType.Put:
                    case MissionGoalType.Use:
                        Assert.Contains(phase.objects, o => o.id == goal.objectId);
                        break;
                    case MissionGoalType.DefeatAll:
                        Assert.True(phase.enemies > 0 || mission.npcs.Any(n => !n.friendly && n.at == p + 1) ||
                                    (!mission.generated && mission.npcs.Any(n => !n.friendly)), what + ": nobody to defeat");
                        break;
                }

                // A Search may turn up nothing ("Search the computer", then "Get the data disk from the computer")
                if (goal.type == MissionGoalType.Get || goal.type == MissionGoalType.Give || goal.type == MissionGoalType.Loot ||
                    (goal.type == MissionGoalType.Search && goal.item != 0) || goal.type == MissionGoalType.Put ||
                    goal.type == MissionGoalType.Upload)
                {
                    Assert.True(gameObjects.Contains(goal.item), what + ": item " + goal.item + " isn't a game object");
                }

                if (goal.type == MissionGoalType.Escort)
                {
                    Match to = Regex.Match(goal.escortTo, @"^(exit|hardline|npc:(\d+))$");
                    Assert.True(to.Success, what + ": to=\"" + goal.escortTo + "\"");
                    if (to.Groups[2].Success)
                    {
                        ushort target = ushort.Parse(to.Groups[2].Value);
                        MissionNpc npc = mission.npcs.Find(n => n.id == target);
                        Assert.True(npc != null && (!mission.generated || npc.at == p + 1 || FollowsInto(mission, target, p)),
                            what + ": escort target isn't in this phase");
                    }
                }
            }
        }
    }

    /// <summary>An NPC from an earlier phase is still with the player when an earlier phase had them follow.</summary>
    private static bool FollowsInto(Mission mission, ushort npcId, int phaseIndex)
    {
        for (int p = 0; p < phaseIndex; p++)
        {
            if (mission.phases[p].missionObjectives.Any(o => o.type == MissionGoalType.Escort && o.Has(npcId)))
            {
                return true;
            }
        }

        return false;
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EveryGoalCanBeDone(string file)
    {
        Mission mission = Load(file);
        MissionProgress progress = new MissionProgress(mission);
        HashSet<uint> held = new HashSet<uint>();

        do
        {
            // As MissionRuntime does: what the phase hands over or uploads and no goal so far provides, the player is given
            foreach (MissionObjective goal in progress.Phase!.missionObjectives)
            {
                bool handsOver = goal.type == MissionGoalType.Give || goal.type == MissionGoalType.Put || goal.type == MissionGoalType.Upload;
                if (handsOver && goal.item != 0 && !MissionRuntime.Provided(mission, progress.phaseIndex, goal.item))
                {
                    held.Add(goal.item);
                }
            }

            string where = file + " phase " + (progress.phaseIndex + 1);
            while (!progress.AllDone)
            {
                MissionObjective? next = progress.OpenObjectives()
                    .FirstOrDefault(o => o.type != MissionGoalType.Protect && CanDo(o, held, progress));
                Assert.True(next != null, where + ": stuck with " +
                                          string.Join(", ", progress.Phase!.missionObjectives.Where(o => !progress.IsDone(o.goal))
                                              .Select(o => o.goal + " " + o.description)));
                Do(next!, held);
                progress.Complete(next!);
            }

            // Optional goals the player may still do before leaving
            foreach (MissionObjective optional in progress.OpenObjectives().Where(o => o.optional && CanDo(o, held, progress)).ToList())
            {
                Do(optional, held);
                progress.Complete(optional);
            }
        } while (progress.NextPhase());
    }

    private static bool CanDo(MissionObjective goal, HashSet<uint> held, MissionProgress? progress = null)
    {
        switch (goal.type)
        {
            case MissionGoalType.Talk:
                // Talking to a friendly NPC settles the first open goal that names it (MissionRuntime.OnTalk), so a
                // Talk listed after that NPC's Protect never completes
                MissionNpc? npc = progress?.mission.npcs.Find(n => n.id == goal.npcId);
                return npc == null || !npc.friendly || progress!.FindOpenFor(goal.npcId) == goal;
            case MissionGoalType.Give:
            case MissionGoalType.Put:
            case MissionGoalType.Upload:
                return held.Contains(goal.item);
            default:
                return true;
        }
    }

    private static void Do(MissionObjective goal, HashSet<uint> held)
    {
        switch (goal.type)
        {
            case MissionGoalType.Get:
            case MissionGoalType.Loot:
            case MissionGoalType.Search:
                held.Add(goal.item);
                break;
            case MissionGoalType.Give:
            case MissionGoalType.Put:
                held.Remove(goal.item);
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void StoryMissionTextIsClean(string file)
    {
        Mission mission = Load(file);
        if (mission.id < 100000)
        {
            return;
        }

        List<string> texts = new List<string> { mission.title, mission.description, mission.finishLine };
        texts.AddRange(mission.npcs.Select(n => n.handle));
        texts.AddRange(mission.npcs.SelectMany(n => n.lines).Select(l => l.text));
        foreach (MissionPhase phase in mission.phases)
        {
            texts.Add(phase.briefing);
            texts.AddRange(phase.enterLines.Select(l => l.text));
            texts.AddRange(phase.completeLines.Select(l => l.text));
            texts.AddRange(phase.triggers.SelectMany(t => t.lines).Select(l => l.text));
            foreach (MissionObjective goal in phase.missionObjectives)
            {
                texts.Add(goal.description);
                texts.AddRange(goal.talkLines.Select(l => l.text));
                texts.AddRange(goal.doneLines.Select(l => l.text));
            }
        }

        Assert.False(string.IsNullOrWhiteSpace(mission.description), file + ": no description");
        Assert.False(string.IsNullOrWhiteSpace(mission.phases[0].briefing), file + ": no briefing");
        foreach (string text in texts.Where(t => t != null))
        {
            Assert.DoesNotContain("[?", text);
            Assert.DoesNotContain("TiAmo", text);
            Assert.DoesNotContain("Vesuveus", text);
            Assert.True(text.All(c => c == '\n' || c == '\r' || (c >= ' ' && c < 127)), file + ": not plain ASCII: " + text);
            Assert.True(text.Length < 1000, file + ": too long for one line: " + text.Substring(0, Math.Min(60, text.Length)));
        }
    }

    [Fact]
    public void TheChecksCatchAStuckMission()
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(@"<mission id=""1"" title=""T""><npc id=""1"" handle=""A"" type=""FRIENDLY"" rsi=""1"" />
            <phase><objective goal=""1"" text=""Give"" type=""Give"" npc=""1"" item=""6794"" /></phase></mission>");
        MissionProgress progress = new MissionProgress(MissionXml.Parse(doc.DocumentElement!));

        Assert.False(progress.OpenObjectives().Any(o => o.type != MissionGoalType.Protect && CanDo(o, new HashSet<uint>())));
    }
}
