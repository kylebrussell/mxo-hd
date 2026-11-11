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

        // NPCs and hostiles that arrive when a goal completes (spawnAfter, enemiesAfter)
        private readonly HashSet<ushort> arrived = new HashSet<ushort>();
        public readonly List<MissionNpc> justArrived = new List<MissionNpc>();     // arrived, for the runtime to spawn
        public readonly Dictionary<ushort, double[]> reservedSpots = new Dictionary<ushort, double[]>(); // their site spots
        public readonly List<double[]> reservedEnemySpots = new List<double[]>();  // the phase's late hostiles' spots
        public bool enemiesArrived;      // this phase's enemiesAfter goal has completed
        public bool enemiesJustArrived;  // and the runtime has yet to spawn them

        public readonly HashSet<ushort> killed = new HashSet<ushort>(); // mission NPCs the player has killed, for kill goals that open later
        public bool goalsCompleteSent;   // this phase's end line has gone out
        public bool leaveNotNeeded;      // this phase ended without the player having to leave: the next comes at once

        public MissionProgress(Mission mission)
        {
            this.mission = mission;
            waypoints = Phase?.waypoints ?? new List<MissionWaypoint>();
        }

        public MissionPhase Phase => phaseIndex < mission.phases.Count ? mission.phases[phaseIndex] : null;

        public bool IsDone(int goal) => done.Contains(goal);

        /// <summary>Whether a mission NPC is in the world as far as its goals go: it doesn't arrive late, or it has arrived.</summary>
        public bool HasArrived(MissionNpc npc) => npc == null || npc.spawnAfter < 0 || arrived.Contains(npc.id);

        public bool HasArrived(ushort npcId) => HasArrived(mission.npcs.Find(n => n.id == npcId));

        /// <summary>
        /// Late arrivals of the current phase that haven't come yet but will: their goal is one of this phase's and isn't optional.
        /// A DefeatAll waits for them.
        /// </summary>
        public bool HostilesStillToArrive()
        {
            MissionPhase phase = Phase;
            if (phase == null)
            {
                return false;
            }

            bool Expected(int goal) => phase.missionObjectives.Exists(o => o.goal == goal && !o.optional && !done.Contains(o.goal));
            if (phase.enemies > 0 && phase.enemiesAfter >= 0 && !enemiesArrived && Expected(phase.enemiesAfter))
            {
                return true;
            }

            int at = phaseIndex + 1;
            return mission.npcs.Exists(n => !n.friendly && !n.dead && n.spawnAfter >= 0 && !arrived.Contains(n.id) &&
                                            (n.at == at || (n.at == 0 && !mission.generated)) && Expected(n.spawnAfter));
        }

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

            // Who arrives now: an NPC placed in this phase, or one of the mission's own the first time its goal completes
            foreach (MissionNpc npc in mission.npcs)
            {
                if (npc.spawnAfter == objective.goal && (npc.at == 0 || npc.at == phaseIndex + 1) && arrived.Add(npc.id))
                {
                    justArrived.Add(npc);
                }
            }

            if (Phase.enemiesAfter == objective.goal && !enemiesArrived)
            {
                enemiesArrived = true;
                enemiesJustArrived = Phase.enemies > 0;
            }

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
            goalsCompleteSent = false;
            leaveNotNeeded = false;
            enemiesArrived = false;
            enemiesJustArrived = false;
            reservedSpots.Clear();
            reservedEnemySpots.Clear();
            justArrived.RemoveAll(n => n.at != 0);
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
            return waitingForExit && Phase != null && (leaveNotNeeded || MissionExit.HasLeft(waypoints, exits, x, y, z));
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

        /// <summary>Whether the player is out of the area already: it has no ways out, or they are far from all of them.</summary>
        public static bool IsOutside(List<MissionWaypoint> waypoints, List<double[]> exits, double x, double y, double z)
        {
            double nearest = double.MaxValue;
            foreach (MissionWaypoint door in waypoints)
            {
                nearest = Math.Min(nearest, Distance(door.x, door.y, door.z, x, y, z));
            }

            foreach (double[] exit in exits ?? new List<double[]>())
            {
                nearest = Math.Min(nearest, Distance(exit[0], exit[1], exit[2], x, y, z));
            }

            return nearest >= FarUnits;
        }

        private static double Distance(double ax, double ay, double az, double bx, double by, double bz)
        {
            double dx = ax - bx, dy = ay - by, dz = az - bz;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
