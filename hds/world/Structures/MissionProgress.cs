using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// How far a player is through a mission, apart from the network: a phase's objectives can be open at once and in any
    /// order (Welcome to the Matrix opens three talks together), some open only after another completes, and once all are
    /// done the phase waits for the player to leave through a mission door.
    /// </summary>
    public class MissionProgress
    {
        public readonly Mission mission;
        public int phaseIndex;
        public bool waitingForExit;
        public int kills;
        public readonly long startedAt = TimeUtils.getUnixTimeUint32();

        private readonly HashSet<int> done = new HashSet<int>();
        private readonly HashSet<MissionTrigger> fired = new HashSet<MissionTrigger>();
        private readonly HashSet<int> everDone = new HashSet<int>();
        public readonly Dictionary<UInt32, UInt16> itemSlots = new Dictionary<UInt32, UInt16>();

        // A generated phase's placement, for this player: mission definitions are shared.
        public MissionSite site;
        public readonly List<MissionSite> sites = new List<MissionSite>();         // every site used so far, in phase order
        public List<MissionWaypoint> waypoints = new List<MissionWaypoint>();      // this phase's, as sent
        public readonly List<double[]> exits = new List<double[]>();              // other places that count as leaving
        public readonly Dictionary<ushort, MissionObjectState> objects = new Dictionary<ushort, MissionObjectState>();
        public readonly HashSet<ushort> following = new HashSet<ushort>();         // NPCs walking with the player
        public readonly List<Mob> hostiles = new List<Mob>();                       // the phase's generated hostiles
        public bool entered;  // the phase's enter lines have played
        public bool failed;
        public bool inventoryFullNotice;

        public MissionProgress(Mission mission)
        {
            this.mission = mission;
            waypoints = Phase?.waypoints ?? new List<MissionWaypoint>();
        }

        public MissionPhase Phase => phaseIndex < mission.phases.Count ? mission.phases[phaseIndex] : null;

        public bool IsDone(int goal) => done.Contains(goal);

        /// <summary>Whether this goal has ever completed, across phases: goal ids repeat between phases.</summary>
        public bool WasDoneInAnyPhase(int goal) => everDone.Contains(goal);

        public bool IsOpen(MissionObjective objective)
        {
            return !done.Contains(objective.goal) && (objective.afterGoal < 0 || done.Contains(objective.afterGoal));
        }

        public IEnumerable<MissionObjective> OpenObjectives()
        {
            MissionPhase phase = Phase;
            if (phase == null)
            {
                yield break;
            }

            foreach (MissionObjective objective in phase.missionObjectives)
            {
                if (IsOpen(objective))
                {
                    yield return objective;
                }
            }
        }

        public MissionObjective FindOpen(MissionGoalType type, ushort npcId)
        {
            foreach (MissionObjective objective in OpenObjectives())
            {
                if (objective.type == type && objective.Has(npcId))
                {
                    return objective;
                }
            }

            return null;
        }

        public MissionObjective FindOpenFor(ushort npcId)
        {
            foreach (MissionObjective objective in OpenObjectives())
            {
                if (objective.Has(npcId))
                {
                    return objective;
                }
            }

            return null;
        }

        /// <summary>Completes a goal. Returns the objectives this opened.</summary>
        public List<MissionObjective> Complete(MissionObjective objective)
        {
            done.Add(objective.goal);
            everDone.Add(objective.goal);

            List<MissionObjective> opened = new List<MissionObjective>();
            foreach (MissionObjective other in Phase.missionObjectives)
            {
                if (other.afterGoal == objective.goal && !done.Contains(other.goal))
                {
                    opened.Add(other);
                }
            }

            if (AllDone)
            {
                waitingForExit = true;
            }

            return opened;
        }

        public bool AllDone
        {
            get
            {
                MissionPhase phase = Phase;
                if (phase == null)
                {
                    return true;
                }

                foreach (MissionObjective objective in phase.missionObjectives)
                {
                    // Optional goals can be left; a Protect goal is kept by finishing the rest
                    if (!objective.optional && objective.type != MissionGoalType.Protect && !done.Contains(objective.goal))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Moves to the next phase. Returns false when that was the last one and the mission is over.</summary>
        public bool NextPhase()
        {
            phaseIndex++;
            waitingForExit = false;
            done.Clear();
            fired.Clear();
            entered = false;
            site = null;
            hostiles.Clear();
            exits.Clear();
            waypoints = Phase?.waypoints ?? new List<MissionWaypoint>();
            return Phase != null;
        }

        /// <summary>True the first time for each trigger, within a phase.</summary>
        public bool Fire(MissionTrigger trigger) => fired.Add(trigger);

        public bool IsExiting(double x, double y, double z)
        {
            return waitingForExit && Phase != null && MissionExit.HasLeft(waypoints, exits, x, y, z);
        }

        /// <summary>Whether (x, y, z) is at one of the phase's ways out, whether or not its goals are done.</summary>
        public bool AtExit(double x, double y, double z) => Phase != null && MissionExit.HasLeft(waypoints, exits, x, y, z);
    }

    /// <summary>
    /// When a finished phase counts as left. Nothing in the data states retail's rule; these numbers come from where the
    /// player stood when retail moved on (Welcome, Exile Extermination, The Internuncio's two phases): 2 to 5 m from a
    /// mission door they had just walked through. The far limit covers leaving another way, such as a hardline.
    /// </summary>
    public static class MissionExit
    {
        public const double DoorRadiusUnits = 600;
        public const double FarUnits = 6000;

        public static bool HasLeft(MissionPhase phase, double x, double y, double z)
        {
            return HasLeft(phase.waypoints, null, x, y, z);
        }

        /// <param name="exits">Other ways out, such as the street door of a generated site's elevator.</param>
        public static bool HasLeft(List<MissionWaypoint> waypoints, List<double[]> exits, double x, double y, double z)
        {
            if (waypoints.Count == 0 && (exits == null || exits.Count == 0))
            {
                return true;
            }

            double nearest = double.MaxValue;
            foreach (MissionWaypoint door in waypoints)
            {
                nearest = Math.Min(nearest, Distance(door.x, door.y, door.z, x, y, z));
            }

            double nearestExit = double.MaxValue;
            foreach (double[] exit in exits ?? new List<double[]>())
            {
                nearestExit = Math.Min(nearestExit, Distance(exit[0], exit[1], exit[2], x, y, z));
            }

            return Math.Min(nearest, nearestExit) <= DoorRadiusUnits || Math.Min(nearest, nearestExit) >= FarUnits;
        }

        private static double Distance(double ax, double ay, double az, double bx, double by, double bz)
        {
            double dx = ax - bx, dy = ay - by, dz = az - bz;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
