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
                title = title,
                description = Attr(node, "description") ?? "",
                plot = Int(node, "plot", 0) != 0,
                order = Int(node, "order", 0),
                district = (ushort)Int(node, "district", 1),
                districtName = Attr(node, "districtName") ?? "slums",
                exp = Int(node, "exp", 0),
                info = Int(node, "info", 0),
                finishLine = node.SelectSingleNode("finish")?.InnerText
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
                weapon = (UInt32)Int(node, "weapon", 0)
            };
            foreach (XmlNode line in Elements(node, "say"))
            {
                npc.lines.Add(ParseLine(line, MissionSpeaker.Npc, npc.id));
            }

            return npc;
        }

        private static MissionPhase ParsePhase(XmlNode node)
        {
            MissionPhase phase = new MissionPhase();
            XmlNode briefing = node.SelectSingleNode("briefing");
            if (briefing != null)
            {
                phase.briefing = briefing.InnerText;
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
                description = Attr(node, "text") ?? "",
                type = (MissionGoalType)Enum.Parse(typeof(MissionGoalType), Attr(node, "type") ?? "Talk", true),
                npcId = (ushort)Int(node, "npc", 0),
                item = (UInt32)Int(node, "item", 0),
                afterGoal = Int(node, "after", -1)
            };
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
                text = node.InnerText,
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
