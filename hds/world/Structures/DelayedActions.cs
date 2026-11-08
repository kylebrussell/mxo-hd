using System;
using System.Collections.Generic;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Runs actions after a delay on one shared 100 ms timer (ability casts finishing, cooldowns ending).
    /// Actions run off the client's own thread, so they take the client they act on as an argument rather
    /// than using Store.currentClient.
    /// </summary>
    public static class DelayedActions
    {
        private class Pending
        {
            public double DueAt;
            public Action Action;
        }

        private static readonly List<Pending> pending = new List<Pending>();
        private static Timer timer;

        private static double Now()
        {
            return TimeUtils.GetSimTimeSeconds();
        }

        public static void After(double seconds, Action action)
        {
            if (seconds <= 0)
            {
                action();
                return;
            }

            lock (pending)
            {
                pending.Add(new Pending { DueAt = Now() + seconds, Action = action });
                if (timer == null)
                {
                    timer = new Timer(_ => RunDue(Now()), null, 100, 100);
                }
            }
        }

        public static void RunDue(double now)
        {
            List<Pending> due;
            lock (pending)
            {
                due = pending.FindAll(p => p.DueAt <= now);
                pending.RemoveAll(p => p.DueAt <= now);
            }

            foreach (Pending p in due)
            {
                try
                {
                    p.Action();
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("[TIMER] Delayed action failed: " + ex.Message);
                }
            }
        }
    }
}
