using System;
using System.Threading;
using hds.shared;

namespace hds
{
    /// <summary>The captured shield on one ordinary Crossbones Tar placement.</summary>
    internal sealed class NpcEvasion
    {
        private const uint CastFx = 0x28000a1d;
        private const byte EvadingMode = 0x12, OrdinaryMode = 0x22;
        private int health = byte.MaxValue;

        internal byte Health => (byte)Volatile.Read(ref health);

        internal static NpcEvasion FromSpawnRow(string[] row, Mob mob)
        {
            // The archived Object599 creation and OpenWorld.csv row have the same RSI, level and rounded position.
            if (row.Length > 22 && row[13] == "18" && row[22] == "671091229" &&
                mob.getDistrict() == 1 && mob.getName() == "Crossbones Tar" && mob.getLevel() == 7 &&
                mob.getRsiHex() == "59030008" && (mob.xBase, mob.yBase, mob.zBase) == (97619d, 695d, 10761d))
                return new NpcEvasion();
            return null;
        }

        internal void Reset() => Volatile.Write(ref health, byte.MaxValue);

        internal void ApplyCreation(AttributeClass3827 view, uint flags)
        {
            byte meter = Health;
            if (meter == 0) return;

            view.EvadeShieldHealth.enable();
            view.EvadeShieldHealth.setValue(meter);
            view.EvadeShieldDamageScale.enable();
            view.EvadeShieldDamageScale.setValue(BitConverter.Int32BitsToSingle(0x4023d70a));
            view.ConditionStateFlags.setValue(flags | EvasionShield.StateFlag);
            view.CombatantMode.setValue(EvadingMode);
            view.EffectCounter.enable();
            view.EffectCounter.setValue((byte)1);
            view.EffectID.enable();
            view.EffectID.setValue(CastFx);
        }

        internal static bool InterceptEntry(WorldClient source, Mob target, uint handle)
        {
            NpcEvasion shield = target.CapturedEvasion;
            if (shield == null) return false;
            if (!source.Alive || !source.playerData.getOnWorld() || source.playerData.isDead ||
                source.playerData.duelDeath || source.playerData.worldChangePending)
                return Refuse(source, PvpCombat.RefusedDead, false, handle);
            // NPC shield accounting uses the shipped level curve and the current server's byte-meter conversion.
            int damage = EvasionShield.ShieldDamage(source.playerInstance.Level.getValue()[0], (byte)target.getLevel(),
                AbilityStats.Player(source, 1048));

            // Use the same publication order as NPC view updates. The last absorbed attempt still refuses entry.
            lock (WorldServer.Clients)
            {
                ClientView view = source.viewMan.getViewById((ushort)handle);
                if (view == null || !view.viewCreated || view.viewNeedsToBeDeleted || view.spawnId != handle >> 16 ||
                    view.entityId != target.getEntityId() || !target.getIsSpawned() || target.getIsDead() ||
                    target.getDistrict() != source.playerData.getDistrictId())
                    return Refuse(source, PvpCombat.RefusedDead, true, handle);

                byte before = shield.Health;
                if (before == 0) return false;

                var at = CombatSight.Position(source);
                if (!AbilityTargets.WithinArea(at.X, at.Y, at.Z, target.getXPos(), target.getYPos(), target.getZPos(),
                    PvpCombat.CloseCombatRequestMetres) || !CombatSight.Between(source, target))
                    return Refuse(source, PvpCombat.RefusedOutOfRange, false, handle);

                byte after = EvasionShield.ReduceHealth(before, damage);
                Volatile.Write(ref shield.health, after);

                var update = new PacketContent();
                update.AddByteArray(BuildUpdate(after, target.ConditionFlags));
                Store.world.SendViewUpdateToClientsWhoHasMobSpawned(update, target);
                Output.WriteDebugLog($"[NPC EVASION] {target.getName()} view={view.ViewID} spawn={view.spawnId} " +
                    $"shield {before}->{after} damage={damage} released={after == 0}");
                return Refuse(source, 14, false, handle, force: true);
            }
        }

        internal static byte[] BuildUpdate(byte meter, uint flags)
        {
            byte[] update = meter == 0
                ? PlayerViewAttributes.Build((32, PlayerViewAttributes.U8(meter)),
                    (43, PlayerViewAttributes.U32(flags)), (44, PlayerViewAttributes.U8(OrdinaryMode)))
                : PlayerViewAttributes.Build((32, PlayerViewAttributes.U8(meter)));
            update[0] = 0x04;
            return update;
        }

        private static bool Refuse(WorldClient source, byte reason, bool targetReason, uint handle, bool force = false)
        {
            source.messageQueue.addRpcMessage(PvpCombat.BuildCombatRefused(reason, targetReason, handle, force, false));
            source.FlushQueue();
            return true;
        }
    }
}
