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
        private CoderPets.Pet engagedPet;

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

        internal void Reset()
        {
            DisplacePet();
            Volatile.Write(ref health, byte.MaxValue);
        }

        internal bool HasPet(CoderPets.Pet pet) => ReferenceEquals(Volatile.Read(ref engagedPet), pet);

        internal bool CanRetaliateAgainst(CoderPets.Pet pet)
        {
            var engaged = Volatile.Read(ref engagedPet);
            return engaged == null || ReferenceEquals(engaged, pet);
        }

        internal void ReleasePet(CoderPets.Pet pet) => Interlocked.CompareExchange(ref engagedPet, null, pet);

        internal void DisplacePet() => Interlocked.Exchange(ref engagedPet, null);

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

                byte after = shield.Absorb(target, before, damage);
                Output.WriteDebugLog($"[NPC EVASION] {target.getName()} view={view.ViewID} spawn={view.spawnId} " +
                    $"shield {before}->{after} damage={damage} released={after == 0}");
                return Refuse(source, 14, false, handle, force: true);
            }
        }

        // Reached only after a melee simulacrum's ordinary Attack order moves it into reach.
        internal bool TryOpenPet(CoderPets.Pet pet, Mob target, double now)
        {
            lock (WorldServer.Clients)
            {
                if (target.getIsDead() || !target.getIsSpawned() || target.DeathGeneration != pet.TargetDeathGeneration)
                    return false;
                // A player fight can accept ordinary pet assistance and owns the NPC's retaliation clock.
                if (target.CombatantCount != 0 || HasPet(pet)) return true;
                if (Volatile.Read(ref engagedPet) != null) return false;

                byte before = Health;
                if (before != 0)
                {
                    // Stock pet orders have no combat-tactic input. Use neutral NPC inputs and the existing entry comparison.
                    int force = CoderPets.ActivePowerForce(pet, now);
                    var scores = CombatInsightEngagement.Calculate(0, force, 0, 0, 0);
                    if (!scores.Allows)
                    {
                        int damage = EvasionShield.ShieldDamage((byte)pet.Mob.getLevel(), (byte)target.getLevel(), 0);
                        byte after = Absorb(target, before, damage);
                        Output.WriteDebugLog($"[PET ENTRY] {target.getName()} owner={TimedEffects.PlayerName(pet.Owner)} " +
                            $"force519={force} force={scores.Force} evade={scores.Evade} resisted shield={before}->{after}");
                        return false;
                    }
                    Output.WriteDebugLog($"[PET ENTRY] {target.getName()} owner={TimedEffects.PlayerName(pet.Owner)} " +
                        $"force519={force} force={scores.Force} evade={scores.Evade} admitted shield={before}");
                }

                Volatile.Write(ref engagedPet, pet);
                if (target.CombatantCount != 0) ReleasePet(pet);
                return true;
            }
        }

        private byte Absorb(Mob target, byte before, int damage)
        {
            byte after = EvasionShield.ReduceHealth(before, damage);
            Volatile.Write(ref health, after);
            var update = new PacketContent();
            update.AddByteArray(BuildUpdate(after, target.ConditionFlags));
            Store.world.SendViewUpdateToClientsWhoHasMobSpawned(update, target);
            return after;
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
