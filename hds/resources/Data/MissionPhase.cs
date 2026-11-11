using System.Collections.Generic;

namespace hds
{
    public class MissionPhase
    {
        public string briefing;
        public string briefingHeaderHex; // 80 a3's 16 header bytes as retail sent them
        public float[] areaA = new float[3];
        public float[] areaB = new float[3];
        public List<MissionWaypoint> waypoints = new List<MissionWaypoint>();
        public List<MissionObjective> missionObjectives = new List<MissionObjective>();
        public List<MissionTrigger> triggers = new List<MissionTrigger>();
        public List<MissionLine> completeLines = new List<MissionLine>(); // when all its goals are done

        // Generated phases: the site is picked when the phase starts, and its waypoint and area come from there.
        public string site;            // "Normal", "Any" or a style (SlumApt, GenericApt, Office, RedPillExtraction, ...)
        public string enemy;           // Machines, Merovingian or Zion: the org of the hostiles it spawns
        public int enemies;            // how many
        public int enemiesAfter = -1;  // they arrive when this goal completes, instead of when the phase starts
        public bool? leave;            // whether its end needs the player to leave the area; null: they do unless already out
        public List<string> neighbourhoods = new List<string>(); // overrides the mission's for this phase
        public List<MissionLine> enterLines = new List<MissionLine>(); // on reaching the site's floor
        public List<MissionObjectDef> objects = new List<MissionObjectDef>();
    }
}
