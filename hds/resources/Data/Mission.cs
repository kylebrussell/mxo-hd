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
        public List<MissionLine> lines = new List<MissionLine>(); // what it says when talked to
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

    /// <summary>Lines the player sets off by walking near a place, once each.</summary>
    public class MissionTrigger
    {
        public double x, y, z;
        public double radius;
        public ushort npcId; // when set, the trigger follows this NPC's position instead
        public List<MissionLine> lines = new List<MissionLine>();
    }
}
