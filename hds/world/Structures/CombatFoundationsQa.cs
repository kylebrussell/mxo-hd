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
        private const int PickX = 21550, TestTargetX = 21750, TestCastX = 22350, GunFarX = 19600, TestZ = 16150;
        private const int DebuffDeflectZ = 16350;

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
            if (mode == "sight-far")
            {
                // Keep the target within view while exceeding the longest starter viral range (20 m).
                int x = TestTargetX + 2150;
                while (x <= TestTargetX + 3150 && !OutdoorStand(x)) x += 50;
                if (!OutdoorStand(TestTargetX) || x > TestTargetX + 3150)
                    return "No outdoor far stand for QA Sight Target.";
                Spawn("QA Sight Target", TestTargetX, SightY, TestZ);
                Move(caller, x, SightY, TestZ, yaw: 192);
                return $"Far stand {x},{SightY},{TestZ}. QA Sight Target is beyond 20 m.";
            }
            if (mode == "gun-far")
            {
                if (!OutdoorStand(GunFarX) || !OutdoorStand(TestTargetX) ||
                    !NavMesh.HasSightPath(1, GunFarX, SightY, TestZ, TestTargetX, SightY, TestZ))
                    return "Gun landing fixture refused: the clear far stand could not be verified.";
                Spawn("QA Sight Target", TestTargetX, SightY, TestZ);
                Move(caller, GunFarX, SightY, TestZ, yaw: 64);
                return $"Far stand {GunFarX},{SightY},{TestZ}. QA Sight Target is 21.5 m ahead with clear sight.";
            }
            if (mode == "sight-pick" || mode == "sight-blocked")
            {
                if (!OutdoorStand(PickX) || !OutdoorStand(TestTargetX) || !OutdoorStand(TestCastX) ||
                    !NavMesh.HasSightPath(1, PickX, SightY, TestZ, TestTargetX, SightY, TestZ) ||
                    NavMesh.HasSightPath(1, TestCastX, SightY, TestZ, TestTargetX, SightY, TestZ))
                    return "Hacker sight fixture refused: the clear picking stand or blocked cast stand could not be verified.";
                Spawn("QA Sight Target", TestTargetX, SightY, TestZ);
                if (mode == "sight-pick")
                {
                    if (Environment.GetEnvironmentVariable("MXO_HACKER_DEBUFF_QA") == "1")
                    {
                        if (!NavMesh.Locate(1, TestTargetX, SightY, DebuffDeflectZ, out var deflectStand) ||
                            deflectStand.indoors || Math.Abs(deflectStand.floorY + 95 - SightY) > 1 ||
                            !NavMesh.HasSightPath(1, PickX, SightY, TestZ, TestTargetX, SightY, DebuffDeflectZ))
                            return "Hacker debuff fixture refused: no clear deflect target stand.";
                        var deflect = Spawn("QA Deflect Target", TestTargetX, SightY, DebuffDeflectZ, level: 26);
                        TimedEffects.Apply(TimedEffects.ForMob(deflect), new TempMods.Effect
                        {
                            Ability = 23,
                            Mods = new[] { new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat,
                                Target = 183, MinValue = 250, MaxValue = 250, Seconds = ResistSeconds } }
                        }, TimeUtils.GetSimTimeSeconds());
                    }
                    Move(caller, PickX, SightY, TestZ, yaw: 64);
                    return $"Hacker sight picking stand {PickX},{SightY},{TestZ}. Select QA Sight Target 2 m ahead, then move to the blocked cast stand.";
                }
                Move(caller, TestCastX, SightY, TestZ, yaw: 192);
                return $"Hacker sight blocked cast stand {TestCastX},{SightY},{TestZ}. QA Sight Target is 6 m west across the mapped gap.";
            }
            if (mode == "cone")
            {
                if (!CombatFixtureWeapons.Equip(caller, DualSmgs)) return "Combat foundation cone requires the fixture dual SMGs.";
                Move(caller, 24800, 495, -1498, yaw: 64);
                Spawn("AB1 Aim", 25200, 495, -1498);
                Spawn("AB1 Inside", 25300, 495, -1378);
                Spawn("AB1 Outside", 25050, 495, -1048);
                var resisted = Spawn("AB1 Resisted", 24600, 495, -1498);
                SeedViralResistance(resisted);
                return "Dual SMGs equipped. Aim Direct Fire at AB1 Aim. AB1 Aim and AB1 Inside take damage. AB1 Outside stays full. AB1 Resisted is west and resists for 30 minutes.";
            }
            if (mode == "detonate")
            {
                Move(caller, 24800, 495, -1498, yaw: 64);
                Spawn("Detonate Neutral", 25200, 495, -1498, level: 7);
                SeedViralResistance(Spawn("Detonate Resisted", 24950, 495, -1898, level: 7));
                return "Detonate fixture ready. Neighbouring level-seven targets are neutral and +75 viral resistance.";
            }
            if (mode == "viral" || mode == "viral-peer")
            {
                Move(caller, 24800, 495, -1498, yaw: 64);
                ushort level = mode == "viral-peer" ? caller.playerInstance.Level.getValue()[0] : (ushort)18;
                Spawn("Viral Neutral", 25200, 495, -1498, level);
                SeedViralResistance(Spawn("Viral Resisted", 25120, 495, -1618, level));
                return $"Viral fixture ready. Level-{level} targets are within Tinkering's two-metre splash.";
            }
            if (mode == "code-infection" && Environment.GetEnvironmentVariable("MXO_CODE_INFECTION_QA") == "1")
            {
                ushort level = caller.playerInstance.Level.getValue()[0];
                Move(caller, 17000, 495, 6400, yaw: 64);
                Spawn("QA Infection Neutral", 17400, 495, 6400, level);
                SeedViralResistance(Spawn("QA Infection Resisted", 17300, 495, 6600, level));
                return "Code Infection targets ready on the open street. QA Infection Neutral is 4 m east and QA Infection Resisted is diagonally east with +75 Viral Resistance.";
            }
            return "Use ?combatdepth foundations-sight, foundations-cone or foundations-detonate.";
        }

        private static bool OutdoorStand(int x) => NavMesh.Locate(1, x, SightY, TestZ, out var location) &&
            !location.indoors && Math.Abs(location.floorY - (SightY - 95)) <= 100;

        private static void SeedViralResistance(Mob target) => TimedEffects.Apply(TimedEffects.ForMob(target), new TempMods.Effect
        {
            Ability = 8,
            Mods = new[] { new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat,
                Target = 157, MinValue = 75, MaxValue = 75, Seconds = ResistSeconds } }
        }, TimeUtils.GetSimTimeSeconds());

        private static void Move(WorldClient caller, double x, double y, double z, byte yaw)
        {
            CombatManager.GetHandler(caller).LeaveOtherFights();
            MissionStaging.Teleport(caller, x, y, z);
            caller.playerInstance.YawInterval.setValue(yaw);
            // Stock cross-site moves carry the destination NAV place in a teleport record.
            caller.messageQueue.addObjectMessage(PacketsUtils.createTeleportPacket(caller, (float)x, (float)y, (float)z), false);
        }

        private static Mob Spawn(string name, double x, double y, double z, ushort level = 50)
        {
            lock (WorldServer.mobs.SyncRoot)
            {
                foreach (Mob existing in WorldServer.mobs)
                    if (existing.getName() == name) return existing;

                var mob = new Mob { PassiveTarget = true };
                mob.setEntityId(WorldServer.entityIdCounter++);
                mob.setDistrict(1);
                mob.setDistrictName("slums");
                mob.setName(name);
                mob.setLevel(level);
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
