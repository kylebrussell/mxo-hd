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

        public MissionProgress(Mission mission)
        {
            this.mission = mission;
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
                if (objective.type == type && objective.npcId == npcId)
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
                if (objective.npcId == npcId)
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
                    if (!done.Contains(objective.goal))
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
            return Phase != null;
        }

        /// <summary>True the first time for each trigger, within a phase.</summary>
        public bool Fire(MissionTrigger trigger) => fired.Add(trigger);

        public bool IsExiting(double x, double y, double z)
        {
            return waitingForExit && Phase != null && MissionExit.HasLeft(Phase, x, y, z);
        }
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
            if (phase.waypoints.Count == 0)
            {
                return true;
            }

            double nearest = double.MaxValue;
            foreach (MissionWaypoint door in phase.waypoints)
            {
                double dx = door.x - x;
                double dy = door.y - y;
                double dz = door.z - z;
                nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }

            return nearest <= DoorRadiusUnits || nearest >= FarUnits;
        }
    }
}
