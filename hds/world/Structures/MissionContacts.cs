using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace hds
{
    /// <summary>
    /// One of the client's own mission contacts: <c>resource/missions/definitions/sponsors.xml</c>, the contact list of retail's
    /// "Missile" mission system, as <c>data/mission_contacts.csv</c> (research/missions/gen_mission_contacts.py writes it).
    /// </summary>
    public class MissionContact
    {
        /// <summary>Retail's id: the list header, each list entry, the info and the accept carry it (Tyndall 2000, The Newsie 47).</summary>
        public UInt32 id;
        /// <summary>The organisation (1 Zion, 2 Machines, 3 Merovingian, 4 Niobe, 5 Pluribus Neo, 6 Cypherites); 0 is a neighbourhood contact.</summary>
        public byte org;
        public string name;
        /// <summary>The name's id in the client's string table.</summary>
        public UInt32 nameId;
        /// <summary>sponsors.xml: "procedural text stringtable abbreviation". Only org contacts have one (tyn, gra, flo, ...).</summary>
        public string code;
        /// <summary>The portrait's resource id.</summary>
        public UInt32 face;
        /// <summary>Org contacts: the player needs at least minRep with the org; above maxRep the contact retires (0: never).</summary>
        public int minRep, maxRep;
        /// <summary>What the contact says when first met (ID_CONTACT_INTRO_TYNDALL ...); 0 for none.</summary>
        public UInt32 introTextId;

        public bool IsNeighbourhood => org == 0;

        public bool Serves(int reputation) => reputation >= minRep && (maxRep == 0 || reputation <= maxRep);
    }

    /// <summary>The mission contacts, from the client's own list.</summary>
    public static class MissionContacts
    {
        public const byte Zion = 1, Machines = 2, Merovingian = 3;

        /// <summary>Tyndall as retail's Zion list header carried her, for when the data file is missing.</summary>
        private static readonly MissionContact Fallback = new MissionContact
        {
            id = 2000, org = Zion, name = "Tyndall", nameId = 0xb4000031, code = "tyn", face = 0x28000cc0, introTextId = 0x6000011a
        };

        private static readonly object loadLock = new object();
        private static List<MissionContact> contacts;

        public static IReadOnlyList<MissionContact> All
        {
            get
            {
                Load();
                return contacts;
            }
        }

        /// <summary>Reads data/mission_contacts.csv, once; a path reloads from it.</summary>
        public static void Load(string path = null)
        {
            lock (loadLock)
            {
                if (contacts != null && path == null)
                {
                    return;
                }

                List<MissionContact> loaded = new List<MissionContact>();
                path = path ?? DataLoader.DataPath("mission_contacts.csv");
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadLines(path).Skip(1))
                    {
                        MissionContact contact = Parse(line);
                        if (contact != null)
                        {
                            loaded.Add(contact);
                        }
                    }
                }
                else
                {
                    Output.WriteLine("[MISSION] No contact data at " + path);
                }

                if (!loaded.Any(c => c.id == Fallback.id))
                {
                    loaded.Add(Fallback);
                }

                contacts = loaded;
            }
        }

        /// <summary>One line: id,org,name,name_id,code,face,min_rep,max_rep,intro_text_id (ids in hex).</summary>
        public static MissionContact Parse(string line)
        {
            string[] f = line.Split(',');
            if (f.Length < 9 || !UInt32.TryParse(f[0], out UInt32 id))
            {
                return null;
            }

            return new MissionContact
            {
                id = id,
                org = byte.Parse(f[1]),
                name = f[2],
                nameId = Hex(f[3]),
                code = f[4],
                face = Hex(f[5]),
                minRep = int.Parse(f[6]),
                maxRep = int.Parse(f[7]),
                introTextId = Hex(f[8])
            };
        }

        private static UInt32 Hex(string text) => string.IsNullOrEmpty(text) ? 0 : UInt32.Parse(text, NumberStyles.HexNumber);

        public static MissionContact ById(UInt32 id) => All.FirstOrDefault(c => c.id == id);

        /// <summary>
        /// A contact by name, as mission files name them ("Tyndall", "Flood", "The Newsie"); null if there's none. Of two with
        /// one name (The Bartender is 5 and 40), the later: 40 is the one standing in the world.
        /// </summary>
        public static MissionContact Named(string name)
        {
            return All.LastOrDefault(c => string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Who answers the mission list request (80 94 &lt;txn&gt; &lt;org&gt;): its org byte is the contact's (Tyndall 1, Agent Gray
        /// 2). The client only shows a list whose header names that contact; Agent Gray's stayed at "Retrieving mission header
        /// data..." under Tyndall's header. Of the org's contacts, the one whose reputation range holds the player's, the
        /// highest first; the client's file gives each org one contact with no limits, so the range doesn't decide anything
        /// yet. An org with no contact (0, or an unknown one) gets Tyndall, as before.
        /// </summary>
        public static MissionContact ForOrg(uint org, int reputation = 0)
        {
            return Pick(All, org, reputation) ?? Named("Tyndall") ?? Fallback;
        }

        /// <summary>Of the org's contacts, the one serving this reputation with the highest minimum; else the lowest. Null if none.</summary>
        public static MissionContact Pick(IEnumerable<MissionContact> candidates, uint org, int reputation)
        {
            List<MissionContact> ofOrg = candidates.Where(c => c.org == org && org != 0).OrderByDescending(c => c.minRep).ToList();
            return ofOrg.FirstOrDefault(c => c.Serves(reputation)) ?? ofOrg.LastOrDefault();
        }

        /// <summary>
        /// Whether this contact gives the mission: an org contact its own org's, and Tyndall the introduction (org 0); a
        /// neighbourhood contact the org-0 missions that name them as their contact.
        /// </summary>
        public static bool Offers(MissionContact contact, Mission mission)
        {
            if (contact == null)
            {
                return false;
            }

            MissionContact giver = Named(mission.contact);
            if (contact.IsNeighbourhood)
            {
                return mission.org == 0 && giver != null && giver.id == contact.id;
            }

            return mission.org == contact.org || (mission.org == 0 && contact.org == Zion && giver?.IsNeighbourhood != true);
        }

        /// <summary>The org the mission packets carry for this mission (80 99, 80 9c): 0 for a neighbourhood contact's, else
        /// its org, with the introduction (org 0) under Zion as before.</summary>
        public static byte PacketOrg(Mission mission)
        {
            if (Named(mission.contact)?.IsNeighbourhood == true)
            {
                return 0;
            }

            return mission.org == 0 ? Zion : mission.org;
        }

        public static bool Offers(uint org, Mission mission) => Offers(ForOrg(org), mission);

        /// <summary>
        /// A contact's ids as the packets carry them, by the name a mission file gives. Unknown names get Tyndall's, as
        /// before.
        /// </summary>
        public static (UInt32 id, UInt32 name, UInt32 image) For(string contact)
        {
            MissionContact found = Named(contact ?? "") ?? Named("Tyndall") ?? Fallback;
            return (found.id, found.nameId, found.face);
        }

        /// <summary>The player's reputation with an org, from the character's reputation attributes.</summary>
        public static int Reputation(WorldClient client, uint org)
        {
            PlayerCharacter player = client?.playerInstance;
            if (player == null)
            {
                return 0;
            }

            switch (org)
            {
                case Zion:
                    return Signed(player.ReputationZionMilitary);
                case Machines:
                    return Signed(player.ReputationMachines);
                case Merovingian:
                    return Signed(player.ReputationMerovingian);
                case 4:
                    return Signed(player.ReputationNiobe);
                case 5:
                    return Signed(player.ReputationPluribusNeo);
                case 6:
                    return Signed(player.ReputationCypherites);
                case 7:
                    return Signed(player.ReputationGMOrganization);
                default:
                    return 0;
            }
        }

        /// <summary>Reputation attributes are 2 bytes, little-endian, signed (?rep takes -32768 to 32767).</summary>
        private static int Signed(Attribute attribute)
        {
            byte[] value = attribute?.getValue();
            return value == null || value.Length < 2 ? 0 : BitConverter.ToInt16(value, 0);
        }
    }
}
