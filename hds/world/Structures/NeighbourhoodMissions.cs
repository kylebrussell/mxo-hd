using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Xml;

namespace hds
{
    /// <summary>
    /// What a neighbourhood contact offers: their own story missions (data/missions with <c>contact="The Newsie"</c>), one at a
    /// time as retail's lists did, then repeatable generic missions built here on generated sites.
    ///
    /// Retail's generic missions were assembled from server-side templates, with each org contact's text in a "procedural
    /// text stringtable" (sponsors.xml's <c>code</c>). The client has none of it: its only mission files are sponsors.xml
    /// and the two optional scripts, and every string table it ships is decoded in mxo-research. So the goal lines below
    /// are retail's own generic phrasing from the captures ("Kill {npc}", "Lead {npc} out of the area", "Do not let {npc}
    /// die", "Search the computer"), but the titles, briefings, descriptions and target names are ours. Retail's
    /// neighbourhood lists in the captures held only the contact's next story mission; the generic ones are our addition,
    /// so every contact has something to give.
    /// </summary>
    public static class NeighbourhoodMissions
    {
        private const int IdBase = 3000; // 3000 + contact * 10 + kind; retail's ids for these are unknown
        private const int Exp = 22000, Info = 31500;

        private static readonly string[] Targets =
        {
            "Avarice", "Cutter", "Lockjaw", "Mortise", "Rasp", "Sallow", "Tinder", "Vesper", "Whitlow", "Yarrow"
        };

        private static readonly string[] Hostages = { "Ambrose", "Delphine", "Ivo", "Marisol", "Tobiah", "Wren" };

        private static readonly Dictionary<UInt32, List<Mission>> cache = new Dictionary<UInt32, List<Mission>>();

        /// <summary>The contact's list: their next story mission (or all of them once done), then the generic ones.</summary>
        public static List<Mission> For(MissionContact contact, HashSet<int> completed)
        {
            List<Mission> list = DataLoader.getInstance().FindMissions(contact, completed ?? new HashSet<int>());
            list.AddRange(Generic(contact));
            return list;
        }

        public static List<Mission> Generic(MissionContact contact)
        {
            lock (cache)
            {
                if (!cache.TryGetValue(contact.id, out List<Mission> missions))
                {
                    missions = Build(contact, MissionContactNpcs.SpotOf(contact.id));
                    cache[contact.id] = missions;
                }

                return missions;
            }
        }

        public static List<Mission> Build(MissionContact contact, MissionContactSpot spot)
        {
            UInt16 district = spot?.metrId ?? 1;
            string hood = spot?.neighbourhood ?? "";
            string where = string.IsNullOrEmpty(hood) ? "the neighbourhood" : hood;
            string target = Targets[contact.id % Targets.Length];
            string hostage = Hostages[contact.id % Hostages.Length];
            string name = contact.name;
            string districtName = district == 2 ? "dt" : district == 3 ? "it" : "slums";
            // Generated sites in the Slums know their neighbourhood; the other districts' don't yet
            string hoods = district == 1 && !string.IsNullOrEmpty(hood) ? " neighbourhoods=\"" + X(hood) + "\"" : "";
            string head(int kind, string title, string description) =>
                "<mission id=\"" + (IdBase + contact.id * 10 + kind) + "\" title=\"" + X(title) + "\" description=\"" + X(description) +
                "\" plot=\"0\" org=\"0\" contact=\"" + X(name) + "\" district=\"" + district + "\" districtName=\"" + districtName +
                "\" exp=\"" + Exp + "\" info=\"" + Info + "\"" + hoods + ">";

            List<string> xml = new List<string>
            {
                head(1, "Assassination", "Kill one of " + name + "'s rivals.") +
                "<npc id=\"1\" handle=\"" + X(target) + "\" type=\"HOSTILE\" at=\"1\" rsi=\"c5020058\" weapon=\"2568\" />" +
                "<phase site=\"Normal\" enemy=\"Merovingian\" enemies=\"3\">" +
                "<briefing>" + X(target + " has been moving in on my business in " + where + ", and I want it stopped. Go to this address and kill " + target + ".") + "</briefing>" +
                "<objective goal=\"1\" text=\"" + X("Kill " + target) + "\" type=\"Defeat\" npc=\"1\" />" +
                "<complete><say contact=\"1\">" + X("That's one less problem for me, {player}.") + "</say></complete>" +
                "</phase></mission>",

                head(2, "Retrieval", "Recover stolen data for " + name + ".") +
                "<phase site=\"Normal\" enemy=\"Merovingian\" enemies=\"3\">" +
                "<object id=\"1\" kind=\"Computer\" />" +
                "<briefing>" + X("Someone broke into my files and copied what they found onto a computer not far from " + where + ". Get in there and search it before they move the data.") + "</briefing>" +
                "<objective goal=\"1\" text=\"Search the computer\" type=\"Search\" object=\"1\" item=\"6831\" />" +
                "<complete><say contact=\"1\">" + X("Good. Hold on to that disk; I'll send someone for it.") + "</say></complete>" +
                "</phase></mission>",

                head(3, "Rescue", "Rescue one of " + name + "'s people.") +
                "<npc id=\"1\" handle=\"" + X(hostage) + "\" type=\"FRIENDLY\" at=\"1\" rsi=\"58010058\" />" +
                "<phase site=\"Normal\" enemy=\"Merovingian\" enemies=\"3\">" +
                "<briefing>" + X("One of my people, " + hostage + ", was grabbed off the street in " + where + " and is being held nearby. Get " + hostage + " out of there alive.") + "</briefing>" +
                "<objective goal=\"1\" text=\"" + X("Lead " + hostage + " out of the area") + "\" type=\"Escort\" npc=\"1\" to=\"exit\" />" +
                "<objective goal=\"2\" text=\"" + X("Do not let " + hostage + " die") + "\" type=\"Protect\" npc=\"1\" />" +
                "<complete><say contact=\"1\">" + X(hostage + " is safe. I owe you, {player}.") + "</say></complete>" +
                "</phase></mission>"
            };

            List<Mission> missions = new List<Mission>();
            foreach (string text in xml)
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(text);
                Mission mission = MissionXml.Parse(document.DocumentElement);
                if (mission != null)
                {
                    missions.Add(mission);
                }
            }

            return missions;
        }

        private static string X(string text) => SecurityElement.Escape(text ?? "");
    }
}
