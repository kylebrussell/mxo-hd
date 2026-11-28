using System;
using System.Collections.Generic;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Dead NPCs: the corpse stays for <see cref="CorpseSeconds"/> (long enough to loot it), then the NPC is
    /// unspawned, and after <see cref="RespawnSeconds"/> it comes back at its spawn point with full health.
    /// The visibility thread does the view work: it deletes the views of unspawned NPCs and spawns the NPC
    /// again as a new view once it is back. Mission NPCs are left alone; their mission removes them.
    /// MXO_MOB_CORPSE_SECONDS and MXO_MOB_RESPAWN_SECONDS override the timings (handy for testing).
    /// </summary>
    public static class MobLifecycle
    {
        public static readonly double CorpseSeconds = ReadSeconds("MXO_MOB_CORPSE_SECONDS", 30);
        public static readonly double RespawnSeconds = ReadSeconds("MXO_MOB_RESPAWN_SECONDS", 90);

        private class DeadMob
        {
            public Mob Mob;
            public double DiedAt;
            public bool Unspawned;
        }

        private static readonly List<DeadMob> dead = new List<DeadMob>();
        private static Timer timer;
        private static int tickInFlight;

        private static double ReadSeconds(string name, double fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double seconds) && seconds >= 0
                ? seconds
                : fallback;
        }

        private static double Now()
        {
            return TimeUtils.GetSimTimeSeconds();
        }

        /// <summary>Starts the corpse and respawn countdown for an NPC that just died.</summary>
        public static void Died(Mob mob)
        {
            Died(mob, Now());
            EnsureTimer();
        }

        public static void Died(Mob mob, double now)
        {
            if (mob == null || mob.missionInstance != 0)
            {
                return;
            }

            lock (dead)
            {
                dead.RemoveAll(d => d.Mob == mob);
                dead.Add(new DeadMob { Mob = mob, DiedAt = now });
            }
        }

        /// <summary>NPCs waiting to respawn.</summary>
        public static int PendingCount
        {
            get
            {
                lock (dead)
                {
                    return dead.Count;
                }
            }
        }

        /// <summary>Unspawns corpses whose time is up and brings back NPCs whose respawn time has come.</summary>
        public static void Tick(double now)
        {
            List<Mob> respawned = null;
            double respawnAfter = Math.Max(RespawnSeconds, CorpseSeconds);
            lock (dead)
            {
                if (dead.Count == 0)
                {
                    return;
                }

                for (int i = dead.Count - 1; i >= 0; i--)
                {
                    DeadMob entry = dead[i];
                    double since = now - entry.DiedAt;
                    if (!entry.Unspawned && since >= CorpseSeconds)
                    {
                        entry.Mob.isUpdateable = false;
                        entry.Mob.setIsSpawned(false);
                        entry.Unspawned = true;
                    }

                    if (entry.Unspawned && since >= respawnAfter)
                    {
                        if (respawned == null) respawned = new List<Mob>(4);
                        respawned.Add(entry.Mob);
                        dead.RemoveAt(i);
                    }
                }
            }

            if (respawned == null)
            {
                return;
            }

            for (int i = 0; i < respawned.Count; i++)
            {
                Mob mob = respawned[i];
                mob.ResetForRespawn();
                mob.setIsSpawned(true);
                Output.WriteDebugLog("[MOB] " + mob.getName() + " respawned at its spawn point");
            }
        }

        private static void EnsureTimer()
        {
            lock (dead)
            {
                if (timer == null)
                {
                    timer = new Timer(_ =>
                    {
                        if (Interlocked.Exchange(ref tickInFlight, 1) != 0)
                        {
                            return;
                        }
                        try
                        {
                            Tick(Now());
                        }
                        catch (Exception ex)
                        {
                            Output.WriteDebugLog("[MOB] Respawn tick failed: " + ex.Message);
                        }
                        finally
                        {
                            Volatile.Write(ref tickInFlight, 0);
                        }
                    }, null, 1000, 1000);
                }
            }
        }
    }
}
