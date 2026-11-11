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

            // It arrives, or changes side, when a goal of its phase completes (any phase's, for an NPC placed by x, y, z); a
            // Protect goal only completes as its phase ends
            IEnumerable<MissionObjective> goals = npc.at > 0
                ? mission.phases[npc.at - 1].missionObjectives
                : mission.phases.SelectMany(p => p.missionObjectives);
            foreach ((string what, int goal) in new[] { ("arrives", npc.spawnAfter), ("turns hostile", npc.hostileAfter), ("turns friendly", npc.friendlyAfter) })
            {
                Assert.True(goal < 0 || goals.Any(o => o.goal == goal && o.type != MissionGoalType.Protect),
                    file + ": " + npc.handle + " " + what + " after goal " + goal + ", which its phase doesn't have");
            }

            Assert.True(npc.hostileAfter < 0 || npc.friendly, file + ": " + npc.handle + " turns hostile but is hostile already");
            Assert.True(npc.friendlyAfter < 0 || !npc.friendly || npc.hostileAfter >= 0,
                file + ": " + npc.handle + " turns friendly but is friendly already");
            CheckArrival(file + ": " + npc.handle, npc.arrival, npc.at > 0 ? mission.phases[npc.at - 1] : null);
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

            if (phase.enemiesAfter >= 0)
            {
                Assert.True(phase.site != null && phase.enemies > 0, where + ": enemiesAfter without generated enemies");
                Assert.True(phase.missionObjectives.Any(o => o.goal == phase.enemiesAfter && o.type != MissionGoalType.Protect),
                    where + ": the enemies arrive after goal " + phase.enemiesAfter + ", which the phase doesn't have");
            }

            Assert.True(phase.enemiesAt == null || phase.enemies > 0, where + ": enemiesAt without enemies");
            foreach (MissionWave wave in phase.Waves())
            {
                string what = where + " wave " + wave.id;
                Assert.True(wave.count > 0, what + ": no hostiles");
                Assert.True(Enemies.Contains(wave.enemy ?? ""), what + ": enemy '" + wave.enemy + "'");
                Assert.True(phase.site != null || wave.arrival.at != null, what + ": no site to stand in; give it spawnAt");
                CheckArrival(what, wave.arrival, phase);
            }

            Assert.True(phase.waves.All(w => w.id > 0), where + ": a <wave> numbered 0, which is the phase's own enemies");
            Assert.Equal(phase.waves.Count, phase.waves.Select(w => w.id).Distinct().Count());

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
                    // A kill goal may wait: a kill made before it opens completes it when it does (MissionRuntime.Complete)
                    Assert.Contains(phase.missionObjectives, o => o.goal == goal.afterGoal);
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

                            // On the side the goal needs, at least once a goal has turned it
                            if (goal.type == MissionGoalType.Defeat || goal.type == MissionGoalType.Loot)
                            {
                                Assert.False(npc!.friendly && npc.hostileAfter < 0, what + ": " + npc.handle + " is friendly");
                            }

                            if (goal.type == MissionGoalType.Escort || goal.type == MissionGoalType.Protect || goal.type == MissionGoalType.Give)
                            {
                                Assert.True(npc!.friendly || npc.friendlyAfter >= 0, what + ": " + npc.handle + " is hostile");
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
                        bool Hostile(MissionNpc n) => !n.friendly || n.hostileAfter >= 0;
                        Assert.True(phase.Waves().Any() || mission.npcs.Any(n => Hostile(n) && n.at == p + 1) ||
                                    (!mission.generated && mission.npcs.Any(Hostile)), what + ": nobody to defeat");
                        foreach (int wave in goal.waves)
                        {
                            Assert.True(phase.Waves().Any(w => w.id == wave) ||
                                        mission.npcs.Any(n => Hostile(n) && n.wave == wave && (n.at == p + 1 || (!mission.generated && n.at == 0))),
                                what + ": no wave " + wave);
                        }

                        break;
                    case MissionGoalType.Survive:
                        Assert.True(goal.seconds > 0, what + ": no seconds to hold out");
                        break;
                }

                Assert.True(goal.limit >= 0, what + ": a negative limit");
                Assert.True(goal.waves.Count == 0 || goal.type == MissionGoalType.DefeatAll, what + ": wave= is for DefeatAll");

                // A Search may turn up nothing ("Search the computer", then "Get the data disk from the computer")
                if (goal.type == MissionGoalType.Get || goal.type == MissionGoalType.Give || goal.type == MissionGoalType.Loot ||
                    (goal.type == MissionGoalType.Search && goal.item != 0) || goal.type == MissionGoalType.Put ||
                    goal.type == MissionGoalType.Upload)
                {
                    Assert.True(gameObjects.Contains(goal.item), what + ": item " + goal.item + " isn't a game object");
                }

                if (goal.type == MissionGoalType.Escort)
                {
                    Match to = Regex.Match(goal.escortTo, @"^(exit|hardline|npc:(\d+)|object:(\d+))$");
                    Assert.True(to.Success, what + ": to=\"" + goal.escortTo + "\"");
                    if (to.Groups[3].Success)
                    {
                        ushort target = ushort.Parse(to.Groups[3].Value);
                        Assert.True(phase.objects.Any(o => o.id == target), what + ": no object " + target + " in this phase");
                    }

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

    /// <summary>When something arrives and where: after a goal of its phase, near a place it has, beside the player or an object of it.</summary>
    private static void CheckArrival(string what, MissionArrival arrival, MissionPhase? phase)
    {
        Assert.True(arrival.delay >= 0, what + ": a negative delay");
        if (phase != null && arrival.afterGoal >= 0)
        {
            Assert.True(phase.missionObjectives.Any(o => o.goal == arrival.afterGoal && o.type != MissionGoalType.Protect),
                what + " arrives after goal " + arrival.afterGoal + ", which its phase doesn't have");
        }

        foreach ((string attribute, string? place) in new[] { ("spawnAt", arrival.at), ("near", arrival.near) })
        {
            if (place == null)
            {
                continue;
            }

            Match match = Regex.Match(place, @"^(player|object:(\d+)|npc:(\d+)|-?[\d.]+,-?[\d.]+,-?[\d.]+)$");
            Assert.True(match.Success, what + ": " + attribute + "=\"" + place + "\"");
            Assert.False(attribute == "spawnAt" && (match.Groups[3].Success || place.Contains(',')), what + ": spawnAt is player or object:n");
            Assert.False(attribute == "near" && place == "player", what + ": near the player");
            if (match.Groups[2].Success)
            {
                Assert.True(phase != null && phase.objects.Any(o => o.id == ushort.Parse(match.Groups[2].Value)),
                    what + ": no object " + match.Groups[2].Value + " in its phase");
            }
        }
    }

    /// <summary>
    /// An NPC's own line with after="n" plays once goal n is done in any phase (MissionRuntime.LatestLine), so goal ids
    /// restarting in each phase can set it off early. Its goal has to be one of its own phase's or a later one's, with no
    /// earlier phase using the same id. after= on any other line does nothing, a line's speaker has to be one of the
    /// mission's NPCs, and every phase needs a goal that has to be done.
    /// </summary>
    [Theory]
    [MemberData(nameof(Files))]
    public void LinesWaitOnTheGoalTheyMean(string file)
    {
        List<string> problems = GoalReferenceProblems(Load(file));
        Assert.True(problems.Count == 0, file + ": " + string.Join("; ", problems));
    }

    [Fact]
    public void TheChecksCatchALineSetOffByAnEarlierPhase()
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(@"<mission id=""1"" title=""T""><npc id=""1"" handle=""A"" type=""FRIENDLY"" rsi=""1"" at=""2""><say after=""1"">Done</say></npc>
            <phase site=""Normal""><object id=""1"" kind=""Computer"" /><objective goal=""1"" text=""Use"" type=""Use"" object=""1"" /></phase>
            <phase site=""Normal""><objective goal=""1"" text=""Talk"" type=""Talk"" npc=""1"" /></phase></mission>");
        Mission mission = MissionXml.Parse(doc.DocumentElement!);

        // As the runtime sees it: phase 2's talk isn't done, but the line already counts as due
        MissionProgress progress = new MissionProgress(mission);
        progress.Complete(mission.phases[0].missionObjectives[0]);
        progress.NextPhase();
        Assert.False(progress.IsDone(1));
        Assert.True(progress.WasDoneInAnyPhase(mission.npcs[0].lines[0].afterGoal));

        Assert.Single(GoalReferenceProblems(mission));
        mission.phases[1].missionObjectives[0].goal = 2;
        mission.npcs[0].lines[0].afterGoal = 2;
        Assert.Empty(GoalReferenceProblems(mission));
    }

    private static List<string> GoalReferenceProblems(Mission mission)
    {
        List<string> problems = new List<string>();
        List<int> PhasesWith(int goal) => Enumerable.Range(1, mission.phases.Count)
            .Where(p => mission.phases[p - 1].missionObjectives.Any(o => o.goal == goal)).ToList();

        foreach (MissionNpc npc in mission.npcs)
        {
            foreach (MissionLine line in npc.lines.Where(l => l.afterGoal >= 0))
            {
                string what = npc.handle + "'s line after goal " + line.afterGoal;
                List<int> phases = PhasesWith(line.afterGoal);
                if (phases.Count == 0)
                {
                    problems.Add(what + ": no phase has that goal");
                }
                else if (npc.at == 0 && phases.Count > 1)
                {
                    // Present for the whole mission: the id has to be the mission's only one
                    problems.Add(what + ": phases " + string.Join(", ", phases) + " all have it");
                }
                else if (npc.at > 0 && phases.All(p => p < npc.at))
                {
                    problems.Add(what + ": only phases before the NPC's have it");
                }
                else if (npc.at > 0 && phases.Any(p => p < npc.at))
                {
                    problems.Add(what + ": phase " + phases.First(p => p < npc.at) + "'s goal of that id sets it off in phase " + npc.at);
                }
            }
        }

        List<MissionLine> others = new List<MissionLine>();
        foreach (MissionPhase phase in mission.phases)
        {
            others.AddRange(phase.enterLines);
            others.AddRange(phase.completeLines);
            others.AddRange(phase.triggers.SelectMany(t => t.lines));
            others.AddRange(phase.missionObjectives.SelectMany(o => o.talkLines.Concat(o.doneLines)));
        }

        foreach (MissionLine line in others)
        {
            if (line.afterGoal >= 0)
            {
                problems.Add("after= on \"" + line.text.Substring(0, Math.Min(40, line.text.Length)) + "\" does nothing: only an NPC's own lines wait");
            }

            if (line.speaker == MissionSpeaker.Npc && !mission.npcs.Any(n => n.id == line.npcId))
            {
                problems.Add("a line is said by NPC " + line.npcId + ", who isn't in the mission");
            }
        }

        for (int p = 0; p < mission.phases.Count; p++)
        {
            if (!mission.phases[p].missionObjectives.Any(o => !o.optional && o.type != MissionGoalType.Protect))
            {
                problems.Add("phase " + (p + 1) + " has no goal that has to be done, so it ends as it starts");
            }
        }

        return problems;
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
        double now = 0;
        MissionProgress progress = new MissionProgress(mission) { clock = () => now };
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
                MissionObjective? Next() => progress.OpenObjectives()
                    .FirstOrDefault(o => o.type != MissionGoalType.Protect && CanDo(o, held, progress));
                MissionObjective? next = Next();
                if (next == null)
                {
                    // Nothing to do but wait: the player reaches the area, walks everywhere, and time passes
                    progress.entered = true;
                    progress.UpdateArrivals((_, _) => true);
                    now += 100000;
                    progress.UpdateArrivals((_, _) => true);
                    next = Next();
                }

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
        // An NPC that arrives late (spawnAfter) can't be talked to, killed or led before it has arrived
        if (progress != null && goal.type != MissionGoalType.Protect && goal.Npcs.Any(id => id != 0 && !progress.HasArrived(id)))
        {
            return false;
        }

        if (progress != null && goal.escortTo.StartsWith("npc:") && !progress.HasArrived(ushort.Parse(goal.escortTo.Substring(4))))
        {
            return false;
        }

        switch (goal.type)
        {
            case MissionGoalType.DefeatAll:
                // Late hostiles it counts have to arrive before everyone can be dead
                return progress == null || !progress.HostilesStillToArrive(goal);
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
    public void TheChecksCatchAGoalWhoseNpcHasNotArrived()
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(@"<mission id=""1"" title=""T""><npc id=""1"" handle=""A"" type=""FRIENDLY"" rsi=""1"" spawnAfter=""2"" />
            <phase>
              <objective goal=""1"" text=""Talk to A"" type=""Talk"" npc=""1"" />
              <objective goal=""2"" text=""Then this"" type=""Talk"" npc=""1"" after=""1"" />
            </phase></mission>");
        MissionProgress progress = new MissionProgress(MissionXml.Parse(doc.DocumentElement!));

        Assert.False(progress.OpenObjectives().Any(o => o.type != MissionGoalType.Protect && CanDo(o, new HashSet<uint>(), progress)));
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
