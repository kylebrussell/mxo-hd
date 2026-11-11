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
        public string enemiesAt;       // "player" or "object:1": late ones appear beside that instead of on the site's spots
        public bool? leave;            // whether its end needs the player to leave the area; null: they do unless already out
        public List<string> neighbourhoods = new List<string>(); // overrides the mission's for this phase
        public List<MissionLine> enterLines = new List<MissionLine>(); // on reaching the site's floor
        public List<MissionObjectDef> objects = new List<MissionObjectDef>();
        public List<MissionWave> waves = new List<MissionWave>(); // further groups of hostiles, each arriving on its own

        /// <summary>Every group of generated hostiles: the phase's own (wave 0, from enemy and enemies), then its &lt;wave&gt;s.</summary>
        public List<MissionWave> Waves()
        {
            List<MissionWave> all = new List<MissionWave>();
            if (enemies > 0)
            {
                all.Add(new MissionWave
                {
                    id = 0,
                    enemy = enemy,
                    count = enemies,
                    arrival = new MissionArrival { afterGoal = enemiesAfter, at = enemiesAt }
                });
            }

            all.AddRange(waves);
            return all;
        }
    }
}
