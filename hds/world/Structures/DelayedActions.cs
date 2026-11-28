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
        private static int timerRunning;

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
                    timer = new Timer(_ => RunDueFromTimer(), null, 100, 100);
                }
            }
        }

        private static void RunDueFromTimer()
        {
            if (Interlocked.CompareExchange(ref timerRunning, 1, 0) != 0)
            {
                return;
            }
            try
            {
                RunDue(Now());
            }
            finally
            {
                Volatile.Write(ref timerRunning, 0);
            }
        }

        /// <summary>
        /// Like <see cref="After"/>, with Store.currentClient set to the client while the action runs. Store.currentClient
        /// is per thread, so code written for the client's own packet thread finds it null on the timer thread.
        /// </summary>
        public static void AfterFor(WorldClient client, double seconds, Action action)
        {
            After(seconds, () =>
            {
                WorldClient previous = Store.currentClient;
                Store.currentClient = client;
                try
                {
                    action();
                }
                finally
                {
                    Store.currentClient = previous;
                }
            });
        }

        public static void RunDue(double now)
        {
            List<Pending> due = null;
            lock (pending)
            {
                int count = pending.Count;
                if (count == 0)
                {
                    return;
                }

                int write = 0;
                for (int i = 0; i < count; i++)
                {
                    Pending item = pending[i];
                    if (item.DueAt <= now)
                    {
                        if (due == null) due = new List<Pending>(4);
                        due.Add(item);
                    }
                    else
                    {
                        if (write != i) pending[write] = item;
                        write++;
                    }
                }
                if (write < count)
                {
                    pending.RemoveRange(write, count - write);
                }
            }

            if (due == null)
            {
                return;
            }

            for (int i = 0; i < due.Count; i++)
            {
                try
                {
                    due[i].Action();
                }
                catch (Exception ex)
                {
                    Output.WriteDebugLog("[TIMER] Delayed action failed: " + ex.Message);
                }
            }
        }
    }
}
