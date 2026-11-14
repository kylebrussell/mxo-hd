using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace hds
{
    /// <summary>
    /// How far a player is through a mission, apart from the network: a phase's objectives can be open at once and in any
    /// order (Welcome to the Matrix opens three talks together), some open only after another completes, and once all are
    /// done the phase waits for the player to leave through a mission door.
    /// </summary>
    public partial class MissionProgress
    {
        public readonly Mission mission;
        public int phaseIndex;
        public bool waitingForExit;
        public int kills;
        public string persistenceId = Guid.NewGuid().ToString("N");
        public long startedAt = TimeUtils.getUnixTimeUint32();

        private readonly HashSet<int> done = new HashSet<int>();
        private readonly HashSet<MissionTrigger> fired = new HashSet<MissionTrigger>();
        private readonly HashSet<int> everDone = new HashSet<int>();
        public readonly Dictionary<UInt32, UInt16> itemSlots = new Dictionary<UInt32, UInt16>();

        // A generated phase's placement, for this player: mission definitions are shared.
        public MissionSite site;
        internal MissionOpenFloorPendingAdmission pendingOpenFloor;
        internal bool openFloorCheckpoint;
        internal int? openFloorResolvedPhase;
        internal int? openFloorLegacyPhase;
        public readonly List<MissionSite> sites = new List<MissionSite>();         // every site used so far, in phase order
        public List<MissionWaypoint> waypoints = new List<MissionWaypoint>();      // this phase's, as sent
        public readonly List<double[]> exits = new List<double[]>();              // other places that count as leaving
        public readonly Dictionary<ushort, MissionObjectState> objects = new Dictionary<ushort, MissionObjectState>();
        public readonly HashSet<ushort> following = new HashSet<ushort>();         // NPCs walking with the player
        public readonly List<Mob> hostiles = new List<Mob>();                       // the phase's generated hostiles
        public bool entered;  // the phase's enter lines have played
        public bool failed;
        public bool inventoryFullNotice;
        public bool completionNotice;
        public bool entryItemExchanged;

        // NPCs and hostiles that arrive late (spawnAfter, enemiesAfter, <wave>): on a goal, the area, a place or a timer
        private readonly HashSet<ushort> arrived = new HashSet<ushort>();
        public readonly List<MissionNpc> justArrived = new List<MissionNpc>();     // arrived, for the runtime to spawn
        public readonly Dictionary<ushort, double[]> reservedSpots = new Dictionary<ushort, double[]>(); // their site spots
        private readonly Dictionary<ushort, double> npcTriggered = new Dictionary<ushort, double>(); // when their delay began
        public readonly HashSet<int> wavesArrived = new HashSet<int>();           // this phase's waves that have come
        public readonly List<MissionWave> wavesJustArrived = new List<MissionWave>(); // and the runtime has yet to spawn
        public readonly HashSet<ushort> pendingNpcPlacements = new HashSet<ushort>();
        public readonly HashSet<int> pendingWavePlacements = new HashSet<int>();
        public readonly Dictionary<int, List<double[]>> reservedWaveSpots = new Dictionary<int, List<double[]>>(); // site spots
        private readonly Dictionary<int, double> waveTriggered = new Dictionary<int, double>();
        public readonly Dictionary<Mob, int> hostileWave = new Dictionary<Mob, int>(); // which wave each generated hostile came with

        // NPCs that change side on a goal (hostileAfter, friendlyAfter)
        private readonly Dictionary<ushort, bool> sides = new Dictionary<ushort, bool>(); // id -> friendly now
        public readonly List<ushort> justSwitched = new List<ushort>();            // for the runtime to respawn

        // Timed goals (Survive, limit): when each began counting
        private readonly Dictionary<int, double> goalClock = new Dictionary<int, double>();

        /// <summary>Seconds, for delays and timed goals. Tests set their own.</summary>
        public Func<double> clock = () => Watch.Elapsed.TotalSeconds;
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        /// <summary>The phase's own hostiles (wave 0) have arrived.</summary>
        public bool enemiesArrived => wavesArrived.Contains(0);

        /// <summary>The phase's own hostiles have arrived and the runtime has yet to spawn them.</summary>
        public bool enemiesJustArrived => wavesJustArrived.Exists(w => w.id == 0);

        /// <summary>The site spots kept for the phase's own late hostiles.</summary>
        public List<double[]> reservedEnemySpots => SpotsFor(0);

        public List<double[]> SpotsFor(int wave)
        {
            if (!reservedWaveSpots.TryGetValue(wave, out List<double[]> spots))
            {
                spots = new List<double[]>();
                reservedWaveSpots[wave] = spots;
            }

            return spots;
        }

        public readonly HashSet<ushort> killed = new HashSet<ushort>(); // mission NPCs the player has killed, for kill goals that open later
        public bool goalsCompleteSent;   // this phase's end line has gone out
        public bool leaveNotNeeded;      // this phase ended without the player having to leave: the next comes at once

        public MissionProgress(Mission mission)
        {
            this.mission = mission;
            waypoints = Phase?.waypoints ?? new List<MissionWaypoint>();
        }

        /// <summary>Whether the NPC is on the player's side now: as the file has it, unless a goal turned it.</summary>
        public bool IsFriendly(MissionNpc npc) => npc != null && (sides.TryGetValue(npc.id, out bool friendly) ? friendly : npc.friendly);

        public MissionPhase Phase => phaseIndex < mission.phases.Count ? mission.phases[phaseIndex] : null;

        public bool IsDone(int goal) => done.Contains(goal);

        /// <summary>Whether a mission NPC is in the world as far as its goals go: it doesn't arrive late, or it has arrived.</summary>
        public bool HasArrived(MissionNpc npc) => npc == null || !npc.arrival.Late || arrived.Contains(npc.id);

        public bool HasArrived(ushort npcId) => HasArrived(mission.npcs.Find(n => n.id == npcId));

        /// <summary>
        /// Late arrivals of the current phase that haven't come yet but will: on their way already (a delay running), waiting
        /// on the area, a place or a timer, or on a goal of this phase that isn't optional. A DefeatAll waits for them.
        /// Given the DefeatAll, only the waves it counts, and not those that wait on it or on a goal that opens after it.
        /// </summary>
        public bool HostilesStillToArrive(MissionObjective defeatAll = null)
        {
            MissionPhase phase = Phase;
            if (phase == null)
            {
                return false;
            }

            if (pendingWavePlacements.Any(wave => Counts(defeatAll, wave)) ||
                mission.npcs.Exists(n => pendingNpcPlacements.Contains(n.id) && !IsFriendly(n) && !n.dead && Counts(defeatAll, n.wave))) return true;

            bool Expected(MissionArrival arrival, bool triggered)
            {
                if (triggered || arrival.afterGoal < 0)
                {
                    return true;
                }

                return phase.missionObjectives.Exists(o => o.goal == arrival.afterGoal && !o.optional && !done.Contains(o.goal)) &&
                       (defeatAll == null || !WaitsOn(arrival.afterGoal, defeatAll.goal));
            }

            foreach (MissionWave wave in phase.Waves())
            {
                if (wave.count > 0 && wave.arrival.Late && !wavesArrived.Contains(wave.id) && Counts(defeatAll, wave.id) &&
                    Expected(wave.arrival, waveTriggered.ContainsKey(wave.id)))
                {
                    return true;
                }
            }

            int at = phaseIndex + 1;
            return mission.npcs.Exists(n => !IsFriendly(n) && !n.dead && n.arrival.Late && !arrived.Contains(n.id) &&
                                            (n.at == at || (n.at == 0 && !mission.generated)) && Counts(defeatAll, n.wave) &&
                                            Expected(n.arrival, npcTriggered.ContainsKey(n.id)));
        }

        /// <summary>Whether a DefeatAll counts this wave: it names it, or it names none.</summary>
        public static bool Counts(MissionObjective defeatAll, int wave) =>
            defeatAll == null || defeatAll.waves.Count == 0 || defeatAll.waves.Contains(wave);

        /// <summary>Whether this goal can only complete once that one has: it is that goal, or opens after it, directly or not.</summary>
        public bool WaitsOn(int goal, int on)
        {
            HashSet<int> seen = new HashSet<int>();
            while (seen.Add(goal))
            {
                if (goal == on)
                {
                    return true;
                }

                MissionObjective objective = Phase?.missionObjectives.Find(o => o.goal == goal);
                if (objective == null || objective.afterGoal < 0)
                {
                    return false;
                }

                goal = objective.afterGoal;
            }

            return false;
        }

        /// <summary>
        /// Moves late NPCs and waves whose time has come into justArrived and wavesJustArrived. near says whether the player is
        /// within a radius of a place ("object:1", "npc:2", "x,y,z"); without it, arrivals that wait on a place wait on.
        /// </summary>
        public void UpdateArrivals(Func<string, double, bool> near = null)
        {
            MissionPhase phase = Phase;
            if (phase == null)
            {
                return;
            }

            double now = clock();
            int at = phaseIndex + 1;
            foreach (MissionNpc npc in mission.npcs)
            {
                if (!npc.arrival.Late || arrived.Contains(npc.id) || (npc.at != 0 && npc.at != at))
                {
                    continue;
                }

                // An NPC placed in a phase waits on that phase's goals; one of the mission's own on the first such goal of any
                if (!npcTriggered.TryGetValue(npc.id, out double since))
                {
                    if (!Met(npc.arrival, npc.at != 0, near))
                    {
                        continue;
                    }

                    since = now;
                    npcTriggered[npc.id] = now;
                }

                if (now - since >= npc.arrival.delay && arrived.Add(npc.id))
                {
                    justArrived.Add(npc);
                }
            }

            foreach (MissionWave wave in phase.Waves())
            {
                if (!wave.arrival.Late || wavesArrived.Contains(wave.id))
                {
                    continue;
                }

                if (!waveTriggered.TryGetValue(wave.id, out double since))
                {
                    if (!Met(wave.arrival, true, near))
                    {
                        continue;
                    }

                    since = now;
                    waveTriggered[wave.id] = now;
                }

                if (now - since >= wave.arrival.delay && wavesArrived.Add(wave.id) && wave.count > 0)
                {
                    wavesJustArrived.Add(wave);
                }
            }
        }

        private bool Met(MissionArrival arrival, bool ofThisPhase, Func<string, double, bool> near)
        {
            if (arrival.afterGoal >= 0 && !(ofThisPhase ? done.Contains(arrival.afterGoal) : everDone.Contains(arrival.afterGoal)))
            {
                return false;
            }

            if (arrival.onEnter && !entered && Phase?.site != null)
            {
                return false;
            }

            return arrival.near == null || (near != null && near(arrival.near, arrival.radius));
        }

        /// <summary>
        /// Starts the clocks of open timed goals (Survive, limit): from when they open, or for those open from the start of a
        /// generated phase, from reaching its area. Returns the Survive goals whose time is up.
        /// </summary>
        public List<MissionObjective> DueSurvivals()
        {
            List<MissionObjective> due = new List<MissionObjective>();
            double now = clock();
            foreach (MissionObjective objective in OpenObjectives())
            {
                if (objective.type != MissionGoalType.Survive && objective.limit <= 0)
                {
                    continue;
                }

                if (!goalClock.TryGetValue(objective.goal, out double since))
                {
                    if (!entered && Phase.site != null)
                    {
                        continue;
                    }

                    since = now;
                    goalClock[objective.goal] = now;
                }

                if (objective.type == MissionGoalType.Survive && now - since >= objective.seconds)
                {
                    due.Add(objective);
                }
            }

            return due;
        }

        /// <summary>An open goal whose limit has run out, or null. DueSurvivals starts the clocks.</summary>
        public MissionObjective Overdue()
        {
            double now = clock();
            foreach (MissionObjective objective in OpenObjectives())
            {
                if (objective.limit > 0 && goalClock.TryGetValue(objective.goal, out double since) && now - since >= objective.limit)
                {
                    return objective;
                }
            }

            return null;
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

            // Who changes side: an NPC placed in this phase, or one of the mission's own. One that arrives on the same goal
            // comes on its new side.
            foreach (MissionNpc npc in mission.npcs)
            {
                if (npc.at != 0 && npc.at != phaseIndex + 1)
                {
                    continue;
                }

                bool? turn = npc.hostileAfter == objective.goal ? false : npc.friendlyAfter == objective.goal ? true : (bool?)null;
                if (turn != null && IsFriendly(npc) != turn.Value)
                {
                    sides[npc.id] = turn.Value;
                    justSwitched.Add(npc.id);
                }
            }

            // Who arrives now: an NPC placed in this phase, or one of the mission's own the first time its goal completes;
            // and the phase's waves. Those with a delay or a place to wait on come later (UpdateArrivals, from the tick).
            UpdateArrivals();

            List<MissionObjective> opened = new List<MissionObjective>();
            foreach (MissionObjective other in Phase.missionObjectives)
            {
                if (other.afterGoal == objective.goal && !done.Contains(other.goal))
                {
                    opened.Add(other);
                    if (other.type == MissionGoalType.Survive || other.limit > 0)
                    {
                        // An action opens this goal now, even if the next area-entry tick hasn't run yet.
                        goalClock[other.goal] = clock();
                    }
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
            openFloorResolvedPhase=null;openFloorLegacyPhase=null;
            waitingForExit = false;
            departedSite = null;
            goalsCompleteSent = false;
            leaveNotNeeded = false;
            wavesArrived.Clear();
            wavesJustArrived.Clear();
            pendingNpcPlacements.Clear();
            pendingWavePlacements.Clear();
            waveTriggered.Clear();
            reservedWaveSpots.Clear();
            hostileWave.Clear();
            goalClock.Clear();
            reservedSpots.Clear();
            justArrived.RemoveAll(n => n.at != 0);
            justSwitched.Clear();
            foreach (MissionNpc npc in mission.npcs)
            {
                if (npc.at != 0)
                {
                    npcTriggered.Remove(npc.id);
                }
            }
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
            return waitingForExit && Phase != null && (leaveNotNeeded || (UsesGeneratedDeparture ? HasGeneratedDeparture || MissionExit.IsOutside(waypoints, exits, x, y, z) : MissionExit.HasLeft(waypoints, exits, x, y, z)));
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
