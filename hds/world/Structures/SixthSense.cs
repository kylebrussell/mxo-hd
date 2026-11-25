using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>SixthSense_Subject and ObjectCheckForSixthSense in ability/spy/misc.py.</summary>
    public static class SixthSense
    {
        public const ushort Ability = 149, Detection = 646;
        internal const int Radius = 1500;
        internal const byte DisplaySeconds = 10, Enemy = 2, Neutral = 0;
        internal const uint ScanLine = 0x60000081, ResistLine = 0x60000080, DiscoveryLine = 0x7a00023c;
        private const uint CastFx = 0x7a000003, TargetFx = 0x7a000004;

        // The shipped Python 2.3 bytecode also compares Stealth to zero before adding 50.
        internal static bool Detects(int roll, int detection, int stealth) => roll + detection > (stealth == 0 ? 50 : 0);

        internal static bool CanCast(WorldClient client) => Ready(client) && AbilityPassives.Level(client, Ability) > 0;

        private static bool Ready(WorldClient client) => client.Alive && client.playerData.getOnWorld() &&
            !client.playerData.worldChangePending && !client.playerData.DistrictIsLA() &&
            !client.playerData.isDead && !client.playerData.duelDeath && PvpCombat.HealthOf(client) > 0;

        private static (double X, double Y, double Z) Position(WorldClient client)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            return (x, y, z);
        }

        public static bool WithinRadius((double X, double Y, double Z) source, (double X, double Y, double Z) target)
        {
            var (x, y, z) = (target.X - source.X, target.Y - source.Y, target.Z - source.Z);
            return x * x + y * y + z * z <= Radius * Radius;
        }

        internal static void Land(WorldClient caster)
        {
            if (!CanCast(caster)) return;
            var origin = Position(caster);
            uint district = caster.playerData.getDistrictId();
            int detection = AbilityStats.Player(caster, Detection);
            int roll = SixthSenseQa.RollFor(caster) ?? Random.Shared.Next(100);
            string name = SocialHandler.HandleOf(caster);
            var pings = new List<DataMiningPackets.Ping>();
            int detectedPlayers = 0, resistedPlayers = 0;
            ServerPackets.SendPlayerAbilityEffect(caster, CastFx);

            foreach (WorldClient target in SocialHandler.OnlineWhere(c => c != caster &&
                c.playerData.getDistrictId() == district))
            {
                int world = target.playerData.worldGeneration;
                long transport = target.TransportFence.Capture();
                if (!Ready(target) || target.playerData.getDistrictId() != district ||
                    SocialHandler.ShareGroup(caster, target)) continue;
                var point = Position(target);
                if (!WithinRadius(origin, point)) continue;
                bool detected = Detects(roll, detection, Stealth.StealthLevelOf(target));
                if (target.playerData.worldGeneration != world || !target.TransportFence.IsCurrent(transport)) continue;
                if (detected)
                {
                    detectedPlayers++;
                    byte relation = PvpCombat.Check(caster, target) == PvpRules.Verdict.Allowed ? Enemy : Neutral;
                    AddPing(point, relation);
                }
                else
                {
                    resistedPlayers++;
                }
                target.messageQueue.AddWorldBoundRpc(DuelPackets.BuildSystemString(ScanLine), world, transport);
                target.messageQueue.AddWorldBoundRpc(AbilityMessages.AboutAbility(detected ? DiscoveryLine : ResistLine,
                    false, Ability, name, SocialHandler.HandleOf(target), 0), world, transport);
                target.FlushQueue();
            }

            lock (WorldServer.mobs.SyncRoot)
                foreach (Mob target in WorldServer.mobs)
                {
                    if (target.isFriendly || target.getIsDead() || target.getDistrict() != district) continue;
                    var point = (target.getXPos(), target.getYPos(), target.getZPos());
                    if (!WithinRadius(origin, point)) continue;
                    // Require the authorized world view before exposing a mission NPC's position.
                    var view = caster.viewMan.FindViewForEntity(target.getEntityId());
                    if (view?.viewCreated == true && !view.viewNeedsToBeDeleted &&
                        Detects(roll, detection, AbilityStats.Npc(target, Stealth.StealthAbility))) AddPing(point, Enemy);
                }

            if (pings.Count > 0) ServerPackets.SendPlayerAbilityEffect(caster, TargetFx);
            Output.WriteDebugLog($"[SIXTH-SENSE] caster={name} roll={roll} detection={detection} detected={detectedPlayers} resisted={resistedPlayers} pings={pings.Count} castFx=0x{CastFx:x} targetFx={(pings.Count > 0 ? $"0x{TargetFx:x}" : "none")}");
            caster.messageQueue.AddWorldBoundRpc(DataMiningPackets.Map(pings, DisplaySeconds),
                caster.playerData.worldGeneration, caster.TransportFence.Capture());
            caster.FlushQueue();

            void AddPing((double X, double Y, double Z) point, byte relation)
            {
                if (DataMiningPackets.Relative(origin.X, origin.Z, point.X, point.Z, relation) is { } ping)
                    pings.Add(ping);
            }
        }
    }
}
