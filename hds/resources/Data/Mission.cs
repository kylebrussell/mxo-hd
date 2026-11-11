using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>A mission as data/missions/*.xml defines it. See docs/MISSIONS.md for the file format.</summary>
    public class Mission
    {
        public int id; // retail's template id (80 9c list), e.g. 1201 for Welcome to the Matrix
        public string title;
        public string description;
        public bool plot;   // retail's 80 96 flag: story missions rather than the repeatable generic types
        public int order;   // position in the contact's chain of story missions
        public ushort district = 1;
        public string districtName = "slums";
        public int exp = 500;
        public int info = 1000;
        public string finishLine; // what the contact says when the mission ends
        public byte org;          // 0: offered to everyone; 1 Zion, 2 Machines, 3 Merovingian (the mission list request's org)
        public string contact = "Tyndall"; // who gives it: MissionContacts has their ids
        public List<string> neighbourhoods = new List<string>(); // where its generated sites should be, when known
        public bool generated => phases.Exists(p => p.site != null); // placed at accept time in a site from the world's markup
        public List<MissionPhase> phases = new List<MissionPhase>();
        public List<MissionNpc> npcs = new List<MissionNpc>();

        /// <summary>Waypoint distance the mission info window shows: from the player to the first and last locations.</summary>
        public MissionWaypoint FirstWaypoint => phases.Count > 0 && phases[0].waypoints.Count > 0 ? phases[0].waypoints[0] : null;

        public MissionWaypoint LastWaypoint
        {
            get
            {
                for (int i = phases.Count - 1; i >= 0; i--)
                {
                    if (phases[i].waypoints.Count > 0)
                    {
                        return phases[i].waypoints[phases[i].waypoints.Count - 1];
                    }
                }

                return null;
            }
        }
    }

    /// <summary>An NPC a mission spawns when accepted. Position is in world units.</summary>
    public class MissionNpc
    {
        public ushort id;
        public string handle;
        public bool friendly;
        public bool attacksOnSight = true; // hostile NPCs only; false waits for the player to start the fight
        public bool dead;                  // a corpse: retail sent these with 0 health
        public byte phase;                 // first byte of retail's MissionKey on this NPC
        public double x, y, z;
        public string rsiHex;
        public ushort level = 1;
        public ushort maxHealth = 500;
        public UInt32 weapon;
        public int at;                     // placed in this phase's site (1-based) when that phase starts, instead of x, y, z
        public MissionArrival arrival = new MissionArrival(); // when it turns up, if not from the start (spawnAfter, spawnDelay, ...)
        public int wave;                   // which of its phase's waves a DefeatAll with wave= counts it in (0: the phase's own)
        public int hostileAfter = -1;      // a friendly NPC turns hostile when this goal completes
        public int friendlyAfter = -1;     // a hostile NPC turns friendly when this goal completes
        public List<MissionLine> lines = new List<MissionLine>(); // what it says when talked to

        /// <summary>Arrives when this goal completes (phase at's goal, or any phase's without at).</summary>
        public int spawnAfter
        {
            get => arrival.afterGoal;
            set => arrival.afterGoal = value;
        }

        /// <summary>An arrival appears beside the player instead of at its spot (spawnAt="player").</summary>
        public bool spawnAtPlayer
        {
            get => arrival.AtPlayer;
            set => arrival.at = value ? "player" : arrival.AtPlayer ? null : arrival.at;
        }
    }

    /// <summary>
    /// When an NPC or a wave of hostiles turns up, if not when its phase starts: once a goal completes (after), once the
    /// player reaches the area (after="enter") or comes near a place (near), and then after a delay. All that are given
    /// have to happen, in that order; the delay counts from the last of them, or from the phase's start.
    /// </summary>
    public class MissionArrival
    {
        public const double DefaultRadius = 1000;

        public int afterGoal = -1;   // a goal completes
        public bool onEnter;         // the player reaches the phase's area (a generated phase's floor; the phase's start otherwise)
        public string near;          // "object:1", "npc:2" or "x,y,z": the player comes within radius of it
        public double radius = DefaultRadius;
        public double delay;         // seconds after the above
        public string at;            // "player" or "object:1": appears beside that, instead of at its own spot

        /// <summary>Whether it waits for anything at all, rather than standing there when its phase starts.</summary>
        public bool Late => afterGoal >= 0 || onEnter || near != null || delay > 0;

        public bool AtPlayer => at == "player";

        /// <summary>The phase object it appears beside (spawnAt="object:1"), or 0.</summary>
        public ushort AtObject => at != null && at.StartsWith("object:") && ushort.TryParse(at.Substring(7), out ushort id) ? id : (ushort)0;
    }

    /// <summary>
    /// A group of generated hostiles: the phase's own (enemy, enemies, enemiesAfter: wave 0) or a &lt;wave&gt; of its own,
    /// each with its own arrival. A DefeatAll with wave="n" counts only these.
    /// </summary>
    public class MissionWave
    {
        public int id;
        public string enemy;       // Machines, Merovingian or Zion
        public int count;
        public MissionArrival arrival = new MissionArrival();
        public List<MissionLine> lines = new List<MissionLine>(); // said when it arrives
    }

    /// <summary>The door a mission waypoint marks (80 9e): where the waypoint points and the static object it names.</summary>
    public class MissionWaypoint
    {
        public double x, y, z;
        public string doorHex; // 4 bytes as sent, kept as retail sent them
    }

    /// <summary>Who says a line. Portraits and names are the ids retail sent for each.</summary>
    public enum MissionSpeaker
    {
        Operator,
        Contact,
        Npc
    }

    public class MissionLine
    {
        public MissionSpeaker speaker = MissionSpeaker.Operator;
        public ushort npcId;      // Npc speaker
        public string text;
        public int afterGoal = -1; // only once this goal is done (an NPC's lines: the latest that applies is used)
        public byte display = 1;  // 80 a1 display flag; 5 on what bystanders say
    }

    /// <summary>A thing a phase's goals use: a computer, safe or mainframe it spawns, or furniture already on the floor.</summary>
    public class MissionObjectDef
    {
        public ushort id;
        public string kind = "Furniture"; // Computer, Safe, Mainframe, Box, Phone or Furniture
    }

    /// <summary>Lines the player sets off by walking near a place, once each.</summary>
    public class MissionTrigger
    {
        public double x, y, z;
        public double radius;
        public ushort npcId; // when set, the trigger follows this NPC's position instead
        public List<MissionLine> lines = new List<MissionLine>();
    }
}
