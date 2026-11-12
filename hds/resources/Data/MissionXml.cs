using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using hds.shared;

namespace hds
{
    /// <summary>Reads data/missions/*.xml. docs/MISSIONS.md describes the format.</summary>
    public static class MissionXml
    {
        public static Mission Parse(XmlNode node)
        {
            string title = Attr(node, "title");
            if (string.IsNullOrEmpty(title))
            {
                return null;
            }

            Mission mission = new Mission
            {
                id = Int(node, "id", 0),
                title = Plain(title),
                description = Plain(Attr(node, "description") ?? ""),
                plot = Int(node, "plot", 0) != 0,
                order = Int(node, "order", 0),
                district = (ushort)Int(node, "district", 1),
                districtName = Attr(node, "districtName") ?? "slums",
                exp = Int(node, "exp", 0),
                info = Int(node, "info", 0),
                finishLine = Plain(node.SelectSingleNode("finish")?.InnerText),
                org = (byte)Int(node, "org", 0),
                contact = Attr(node, "contact") ?? "Tyndall",
                entryItem = (UInt32)Int(node, "entryItem", 0),
                minLevel = (byte)Int(node, "minLevel", 0),
                minReputation = (short)Int(node, "minReputation", 0),
                completionItem = (UInt32)Int(node, "completionItem", 0),
                neighbourhoods = Names(Attr(node, "neighbourhoods"))
            };

            foreach (XmlNode npcNode in Elements(node, "npc"))
            {
                mission.npcs.Add(ParseNpc(npcNode));
            }

            foreach (XmlNode phaseNode in Elements(node, "phase"))
            {
                mission.phases.Add(ParsePhase(phaseNode));
            }

            return mission;
        }

        private static MissionNpc ParseNpc(XmlNode node)
        {
            MissionNpc npc = new MissionNpc
            {
                id = (ushort)Int(node, "id", 0),
                handle = Attr(node, "handle") ?? "NPC",
                friendly = string.Equals(Attr(node, "type"), "FRIENDLY", StringComparison.OrdinalIgnoreCase),
                attacksOnSight = Int(node, "attacksOnSight", 1) != 0,
                dead = Int(node, "dead", 0) != 0,
                phase = (byte)Int(node, "phase", 0),
                x = Dbl(node, "x"),
                y = Dbl(node, "y"),
                z = Dbl(node, "z"),
                rsiHex = Attr(node, "rsi") ?? "",
                level = (ushort)Int(node, "level", 1),
                maxHealth = (ushort)Int(node, "maxHP", 500),
                weapon = (UInt32)Int(node, "weapon", 0),
                at = Int(node, "at", 0),
                arrival = Arrival(node, "spawnAfter", "spawnNear", "spawnRadius", "spawnDelay", "spawnAt"),
                wave = Int(node, "wave", 0),
                hostileAfter = Int(node, "hostileAfter", -1),
                friendlyAfter = Int(node, "friendlyAfter", -1)
            };
            foreach (XmlNode line in Elements(node, "say"))
            {
                npc.lines.Add(ParseLine(line, MissionSpeaker.Npc, npc.id));
            }

            return npc;
        }

        private static MissionPhase ParsePhase(XmlNode node)
        {
            MissionPhase phase = new MissionPhase
            {
                site = Attr(node, "site"),
                enemy = Attr(node, "enemy"),
                enemies = Int(node, "enemies", 0),
                enemiesAfter = Int(node, "enemiesAfter", -1),
                enemiesAt = Place(Attr(node, "enemiesAt")),
                leave = Attr(node, "leave") == null ? (bool?)null : Int(node, "leave", 1) != 0,
                neighbourhoods = Names(Attr(node, "neighbourhoods"))
            };

            foreach (XmlNode group in Elements(node, "enter"))
            {
                foreach (XmlNode line in Elements(group, "say"))
                {
                    phase.enterLines.Add(ParseLine(line, MissionSpeaker.Operator, 0));
                }
            }

            foreach (XmlNode child in Elements(node, "object"))
            {
                phase.objects.Add(new MissionObjectDef { id = (ushort)Int(child, "id", 0), kind = Attr(child, "kind") ?? "Furniture",
                    locked = Int(child, "locked", 0) != 0 });
            }

            foreach (XmlNode child in Elements(node, "wave"))
            {
                // Numbered 1, 2, ... in order unless they say; wave 0 is the phase's own enemies
                MissionWave wave = new MissionWave
                {
                    id = Int(child, "id", phase.waves.Count + 1),
                    enemy = Attr(child, "enemy") ?? phase.enemy,
                    count = Int(child, "count", 1),
                    arrival = Arrival(child, "after", "near", "radius", "delay", "spawnAt")
                };
                foreach (XmlNode line in Elements(child, "say"))
                {
                    wave.lines.Add(ParseLine(line, MissionSpeaker.Operator, 0));
                }

                phase.waves.Add(wave);
            }
            XmlNode briefing = node.SelectSingleNode("briefing");
            if (briefing != null)
            {
                phase.briefing = Plain(briefing.InnerText);
                phase.briefingHeaderHex = Attr(briefing, "header");
                phase.areaA = Vector(Attr(briefing, "a"));
                phase.areaB = Vector(Attr(briefing, "b"));
            }

            foreach (XmlNode child in Elements(node, "waypoint"))
            {
                phase.waypoints.Add(new MissionWaypoint
                {
                    x = Dbl(child, "x"),
                    y = Dbl(child, "y"),
                    z = Dbl(child, "z"),
                    doorHex = Attr(child, "door")
                });
            }

            foreach (XmlNode child in Elements(node, "objective"))
            {
                phase.missionObjectives.Add(ParseObjective(child));
            }

            foreach (XmlNode group in Elements(node, "complete"))
            {
                foreach (XmlNode line in Elements(group, "say"))
                {
                    phase.completeLines.Add(ParseLine(line, MissionSpeaker.Operator, 0));
                }
            }

            foreach (XmlNode child in Elements(node, "near"))
            {
                MissionTrigger trigger = new MissionTrigger
                {
                    x = Dbl(child, "x"),
                    y = Dbl(child, "y"),
                    z = Dbl(child, "z"),
                    radius = Dbl(child, "radius"),
                    npcId = (ushort)Int(child, "npc", 0)
                };
                foreach (XmlNode line in Elements(child, "say"))
                {
                    trigger.lines.Add(ParseLine(line, MissionSpeaker.Operator, 0));
                }

                phase.triggers.Add(trigger);
            }

            return phase;
        }

        private static MissionObjective ParseObjective(XmlNode node)
        {
            MissionObjective objective = new MissionObjective
            {
                goal = Int(node, "goal", 0),
                description = Plain(Attr(node, "text") ?? ""),
                type = (MissionGoalType)Enum.Parse(typeof(MissionGoalType), Attr(node, "type") ?? "Talk", true),
                npcIds = Ids(Attr(node, "npc")),
                item = (UInt32)Int(node, "item", 0),
                afterGoal = Int(node, "after", -1),
                objectId = (ushort)Int(node, "object", 0),
                escortTo = Attr(node, "to") ?? "exit",
                optional = Int(node, "optional", 0) != 0,
                waves = Numbers(Attr(node, "wave")),
                seconds = Dbl(node, "seconds"),
                limit = Dbl(node, "limit")
            };
            objective.npcId = objective.npcIds.Count > 0 ? objective.npcIds[0] : (ushort)0;
            foreach (XmlNode group in Elements(node, "talk"))
            {
                foreach (XmlNode line in Elements(group, "say"))
                {
                    objective.talkLines.Add(ParseLine(line, MissionSpeaker.Operator, 0));
                }
            }

            foreach (XmlNode group in Elements(node, "done"))
            {
                foreach (XmlNode line in Elements(group, "say"))
                {
                    objective.doneLines.Add(ParseLine(line, MissionSpeaker.Operator, 0));
                }
            }

            return objective;
        }

        /// <summary>&lt;say npc="3"&gt; is that NPC speaking, &lt;say&gt; the operator; &lt;say contact="1"&gt; the mission contact.</summary>
        private static MissionLine ParseLine(XmlNode node, MissionSpeaker defaultSpeaker, ushort defaultNpc)
        {
            MissionLine line = new MissionLine
            {
                speaker = defaultSpeaker,
                npcId = defaultNpc,
                text = Plain(node.InnerText),
                afterGoal = Int(node, "after", -1),
                display = (byte)Int(node, "display", 1)
            };
            if (Attr(node, "npc") != null)
            {
                line.speaker = MissionSpeaker.Npc;
                line.npcId = (ushort)Int(node, "npc", 0);
            }
            else if (Attr(node, "contact") != null)
            {
                line.speaker = MissionSpeaker.Contact;
            }

            return line;
        }

        private static IEnumerable<XmlNode> Elements(XmlNode node, string name)
        {
            return node.ChildNodes.Cast<XmlNode>().Where(c => c.NodeType == XmlNodeType.Element && c.Name == name);
        }

        private static string Attr(XmlNode node, string name) => node.Attributes?[name]?.Value;

        /// <summary>
        /// The client draws curly quotes, ellipses and long dashes as "?" (the Prima guide's text has them), so they're sent
        /// as their plain forms.
        /// </summary>
        public static string Plain(string text)
        {
            return text?.Replace("’", "'").Replace("‘", "'").Replace("“", "\"").Replace("”", "\"")
                .Replace("…", "...").Replace("—", "--").Replace("–", "-");
        }

        /// <summary>"Achan, Mara".</summary>
        private static List<string> Names(string text)
        {
            return (text ?? "").Split(',').Select(n => n.Trim()).Where(n => n.Length > 0).ToList();
        }

        /// <summary>
        /// When an NPC or wave turns up: after="3" (a goal) or after="enter" (reaching the area), near="object:1" (with
        /// radius), delay="30" (seconds), and where: spawnAt="player" or "object:1".
        /// </summary>
        private static MissionArrival Arrival(XmlNode node, string after, string near, string radius, string delay, string at)
        {
            string afterText = Attr(node, after);
            MissionArrival arrival = new MissionArrival
            {
                onEnter = string.Equals(afterText, "enter", StringComparison.OrdinalIgnoreCase),
                afterGoal = Int(node, after, -1),
                near = Place(Attr(node, near)),
                radius = Attr(node, radius) == null ? MissionArrival.DefaultRadius : Dbl(node, radius),
                delay = Dbl(node, delay),
                at = Place(Attr(node, at))
            };
            return arrival;
        }

        /// <summary>"player", "object:1", "npc:2" or "x,y,z", trimmed and in lower case where a word; null if not given.</summary>
        private static string Place(string text)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return char.IsLetter(text[0]) ? text.ToLowerInvariant() : text;
        }

        /// <summary>"0" or "1,2", where 0 counts.</summary>
        private static List<int> Numbers(string text)
        {
            List<int> numbers = new List<int>();
            foreach (string part in (text ?? "").Split(','))
            {
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                {
                    numbers.Add(number);
                }
            }

            return numbers;
        }

        /// <summary>"2" or "2,3".</summary>
        private static List<ushort> Ids(string text)
        {
            List<ushort> ids = new List<ushort>();
            foreach (string part in (text ?? "").Split(','))
            {
                if (ushort.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort id) && id != 0)
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static int Int(XmlNode node, string name, int fallback)
        {
            return int.TryParse(Attr(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        private static double Dbl(XmlNode node, string name)
        {
            double.TryParse(Attr(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value);
            return value;
        }

        private static float[] Vector(string text)
        {
            float[] vector = new float[3];
            string[] parts = (text ?? "").Split(',');
            for (int i = 0; i < parts.Length && i < 3; i++)
            {
                float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out vector[i]);
            }

            return vector;
        }
    }
}
