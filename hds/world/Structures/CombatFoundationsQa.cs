using System;
using hds.shared;

namespace hds
{
    internal static class CombatFoundationsQa
    {
        private const string HumanoidRsi = "B3010058"; // Ordinary Blackwoods from the Slums test roster.
        private const uint Rifle = 692, DualSmgs = 7899;
        private const int ResistSeconds = 1800;
        private const int SightX = 22700, SightY = 1095, SightZ = 16100, BlockedX = 21700, ClearX = 22900;

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
                if (!NavMesh.Locate(1, SightX, SightY, SightZ, out var stand) || stand.indoors ||
                    !NavMesh.Locate(1, BlockedX, SightY, SightZ, out var blocked) || blocked.indoors ||
                    NavMesh.HasSightPath(1, SightX, SightY, SightZ, BlockedX, SightY, SightZ) ||
                    !NavMesh.HasSightPath(1, SightX, SightY, SightZ, ClearX, SightY, SightZ))
                    return "Combat foundation sight refused: the outdoor gap or the clear comparison could not be verified.";
                if (!CombatFixtureWeapons.Equip(caller, Rifle)) return "Combat foundation sight requires the fixture rifle.";
                Move(caller, SightX, SightY, SightZ, yaw: 192);
                Spawn("AB1 Blocked", BlockedX, SightY, SightZ);
                Spawn("AB1 Clear", ClearX, SightY, SightZ);
                return $"Stand at {SightX},{SightY},{SightZ} facing west (yaw 192). AB1 Blocked is 10 m ahead across a mapped gap. AB1 Clear is 2 m east. Rifle equipped.";
            }
            if (mode == "cone")
            {
                if (!CombatFixtureWeapons.Equip(caller, DualSmgs)) return "Combat foundation cone requires the fixture dual SMGs.";
                Move(caller, 24800, 495, -1498, yaw: 64);
                Spawn("AB1 Aim", 25200, 495, -1498);
                Spawn("AB1 Inside", 25300, 495, -1378);
                Spawn("AB1 Outside", 25050, 495, -1048);
                var resisted = Spawn("AB1 Resisted", 24600, 495, -1498);
                TimedEffects.Apply(TimedEffects.ForMob(resisted), new TempMods.Effect
                {
                    Ability = 8,
                    Mods = new[] { new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat,
                        Target = 157, MinValue = 75, MaxValue = 75, Seconds = ResistSeconds } }
                }, TimeUtils.GetSimTimeSeconds());
                return "Dual SMGs equipped. Aim Direct Fire at AB1 Aim. AB1 Aim and AB1 Inside take damage. AB1 Outside stays full. AB1 Resisted is west and resists for 30 minutes. Logic Barrage deals 348 on a hit.";
            }
            return "Use ?combatdepth foundations-sight or foundations-cone.";
        }

        private static void Move(WorldClient caller, double x, double y, double z, byte yaw)
        {
            CombatManager.GetHandler(caller).LeaveOtherFights();
            MissionStaging.Teleport(caller, x, y, z);
            caller.playerInstance.YawInterval.setValue(yaw);
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
                mob.setMobId(0x01b3);
                mob.setRsiHex(HumanoidRsi);
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
