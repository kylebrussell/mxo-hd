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
    }
}
