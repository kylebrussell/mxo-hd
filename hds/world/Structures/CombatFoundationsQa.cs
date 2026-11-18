using System;
using hds.shared;

namespace hds
{
    internal static class CombatFoundationsQa
    {
        internal static string Setup(WorldClient caller, string mode)
        {
            if (!QaLoadout.Allowed(Environment.GetEnvironmentVariable("MXO_DEV_COMMANDS"),
                Store.config?.dbParams?.DatabaseName, caller.playerData.getCharID(),
                caller.playerData.getOnWorld(), caller.playerData.isDead) ||
                Environment.GetEnvironmentVariable("MXO_COMBAT_FOUNDATIONS_QA") != "1" ||
                caller.playerData.getDistrictId() != 1)
                return "Combat foundation fixture requires a disposable Slums test world.";

            if (mode == "sight")
            {
                Move(caller, 23828.3, 1295, 13984.9);
                Spawn("AB1 Blocked", 23540.7, 1295, 15548.3);
                Spawn("AB1 Clear", 23828.3, 1295, 14184.9);
                return "Combat foundation sight: AB1 Blocked is 15.9m away across a mapped gap. AB1 Clear is 2m north.";
            }
            if (mode == "cone")
            {
                Move(caller, 17000, 495, 2300);
                Spawn("AB1 Aim", 17400, 495, 2300);
                Spawn("AB1 Inside", 17500, 495, 2420);
                Spawn("AB1 Outside", 17250, 495, 2750);
                var resisted = Spawn("AB1 Resisted", 16800, 495, 2300);
                TimedEffects.Apply(TimedEffects.ForMob(resisted), new TempMods.Effect
                {
                    Ability = 8,
                    Mods = new[] { new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat,
                        Target = 157, MinValue = 75, MaxValue = 75, Seconds = 300 } }
                }, TimeUtils.GetSimTimeSeconds());
                return "Combat foundation cone: aim Direct Fire at AB1 Aim. AB1 Aim and AB1 Inside take damage. AB1 Outside stays full. Logic Barrage deals 348 to AB1 Resisted on a hit.";
            }
            return "Use ?combatdepth foundations-sight or foundations-cone.";
        }

        private static void Move(WorldClient caller, double x, double y, double z)
        {
            CombatManager.GetHandler(caller).LeaveOtherFights();
            MissionStaging.Teleport(caller, x, y, z);
            byte yaw = caller.playerInstance.YawInterval.getValue()[0];
            caller.messageQueue.addObjectMessage(ServerPackets.BuildSelfPositionHandoff(yaw, (float)x, (float)y,
                (float)z, CombatHandler.CombatStartTime()), false);
        }

        private static Mob Spawn(string name, double x, double y, double z)
        {
            lock (WorldServer.mobs.SyncRoot)
            {
                foreach (Mob existing in WorldServer.mobs)
                    if (existing.getName() == name) return existing;

                var mob = new Mob();
                mob.setEntityId(WorldServer.entityIdCounter++);
                mob.setDistrict(1);
                mob.setDistrictName("slums");
                mob.setName(name);
                mob.setLevel(50);
                mob.setHealthM(5000);
                mob.setHealthC(5000);
                mob.setMobId(0x1203);
                mob.setRsiHex("12030004");
                mob.setXPos(x);
                mob.setYPos(y);
                mob.setZPos(z);
                mob.xBase = x;
                mob.yBase = y;
                mob.zBase = z;
                WorldServer.mobs.Add(mob);
                lock (WorldServer.gameServerEntities.SyncRoot) WorldServer.gameServerEntities.Add(mob);
                return mob;
            }
        }
    }
}
