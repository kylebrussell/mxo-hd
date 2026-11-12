using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace hds
{
    /// <summary>Corpse-targeted Hacker callbacks and the client's 81 88 application approval. See ABILITY-RESURRECTION.md.</summary>
    public static class AbilityResurrection
    {
        public const UInt32 RestoreEffect = 0x280006af;
        public static bool Handles(UInt16 id) => id == 325 || id == 326 || id == 375 || id == 416;
        private static int nextOffer;

        /// <summary>Native displays this offer, then rebuilds a fixed54-byte reply with ability/flags/locators only.</summary>
        public static byte[] BuildApproval(UInt16 ability, string caster)
        {
            byte[] name = Encoding.ASCII.GetBytes(caster ?? "");
            if (name.Length > 255) throw new ArgumentOutOfRangeException(nameof(caster));
            byte[] bytes = new byte[57 + name.Length];
            bytes[0] = 0x81; bytes[1] = 0x88;
            Array.Copy(BitConverter.GetBytes((UInt32)ability), 0, bytes, 2, 4);
            // Nonzero offer field restores the natively working dialog frame. Its full semantics are
            // unknown and native replies do not echo it: never use it to authenticate a reply.
            UInt32 offerId;
            do { offerId = unchecked((UInt32)Interlocked.Increment(ref nextOffer)); } while (offerId == 0);
            Array.Copy(BitConverter.GetBytes(offerId), 0, bytes, 8, 4);
            // Remaining opaque padding and the two default locators stay zero.
            bytes[52] = 54;
            bytes[54] = (byte)(name.Length + 1); bytes[55] = (byte)((name.Length + 1) >> 8);
            Array.Copy(name, 0, bytes, 56, name.Length);
            return bytes;
        }

        public sealed class Approval
        {
            private readonly UInt32 ability;
            private readonly double expires;
            private bool completed;
            public Approval(byte[] request, double expires) { ability = BitConverter.ToUInt32(request, 2); this.expires = expires; }
            /// <summary>Payload excludes81 88. Native621d51d0 allocates54 bytes and621d0330 copies only ability, flags and locators.</summary>
            public bool TryComplete(byte[] payload, double now, out bool accepted)
            {
                accepted = false;
                lock (this)
                {
                    if (completed || now >= expires) { completed = true; return false; }
                    if (payload == null || payload.Length != 52 || payload[4] > 1 || payload[5] > 1 ||
                        BitConverter.ToUInt32(payload, 0) != ability) return false;
                    // The padding is uninitialised in native replies (observed ca at raw11); neither it nor
                    // a caster name is a correlation token. The authenticated recipient + live offer + ability match.
                    completed = true;
                    accepted = payload[4] == 0 && payload[5] == 0;
                    return true;
                }
            }
            public bool Pending(double now) { lock (this) return !completed && now < expires; }
        }

        private sealed class State { public readonly object Sync = new object(); public Offer Pending; }
        private sealed class Offer
        {
            public WorldClient Caster;
            public UInt16 Ability;
            public int CasterWorld, CasterLife, TargetWorld, TargetLife;
            public Approval Approval;
        }
        private static readonly ConditionalWeakTable<WorldClient, State> states = new ConditionalWeakTable<WorldClient, State>();

        private static bool InWorld(WorldClient c) => c != null && c.Alive && c.playerData.getOnWorld() &&
            !c.playerData.DistrictIsLA() && !c.playerData.worldChangePending && !c.playerData.waitForRPCShutDown;

        public static bool CanTarget(WorldClient caster, WorldClient target, UInt16 ability)
        {
            if (!Handles(ability) || caster == target || !InWorld(caster) || !InWorld(target) ||
                !ControlActions.CanUseAbilities(caster) || !target.playerData.isDead || target.playerData.duelDeath ||
                target.playerData.respawnPending || caster.playerData.getDistrictId() != target.playerData.getDistrictId()) return false;
            // Check() returns TargetDead before hostility. Use IsOpponent directly for corpses.
            if (PvpRules.IsOpponent(PvpCombat.SubjectOf(caster), PvpCombat.SubjectOf(target),
                PvpFlag.ModeFor(caster), PvpFlag.MaxSafeLevel)) return false;
            return PvpCombat.MetresBetween(caster, target) <= AbilityCatalog.Find(ability).Number("Ability/SelectionRange");
        }

        public static bool Request(WorldClient caster, WorldClient target, UInt16 ability,
            int casterWorld, int casterLife, int targetWorld, int targetLife)
        {
            if (!CanTarget(caster, target, ability) || caster.playerData.lifeGeneration != casterLife || caster.playerData.worldGeneration != casterWorld ||
                target.playerData.worldGeneration != targetWorld || target.playerData.lifeGeneration != targetLife) return false;
            var state = states.GetOrCreateValue(target);
            double now = TimeUtils.GetSimTimeSeconds();
            lock (state.Sync)
            {
                if (state.Pending?.Approval.Pending(now) == true)
                {
                    new ServerPackets().sendSystemChatMessage(caster, "That player already has a resurrection request pending.", "BROADCAST");
                    caster.FlushQueue();
                    return false;
                }
                byte[] request = BuildApproval(ability, TimedEffects.PlayerName(caster));
                state.Pending = new Offer { Caster = caster, Ability = ability, CasterWorld = casterWorld, CasterLife = casterLife,
                    TargetWorld = targetWorld, TargetLife = targetLife,
                    Approval = new Approval(request, now + AbilityCatalog.Find(ability).Number("Ability/TargetAbilityApprovalTime", 60)) };
                target.messageQueue.addRpcMessage(request);
                target.FlushQueue();
            }
            Output.WriteDebugLog("[RESURRECTION] Offered " + ability + " to " + TimedEffects.PlayerName(target));
            return true;
        }

        public static void Reply(WorldClient target, byte[] payload)
        {
            if (!states.TryGetValue(target, out var state)) return;
            lock (state.Sync)
            {
                Offer offer = state.Pending;
                if (offer == null || !offer.Approval.TryComplete(payload, TimeUtils.GetSimTimeSeconds(), out bool accepted)) return;
                state.Pending = null;
                lock (target.playerData.lifeSync)
                {
                    if (!accepted || !CanTarget(offer.Caster, target, offer.Ability) ||
                        offer.Caster.playerData.lifeGeneration != offer.CasterLife || offer.Caster.playerData.worldGeneration != offer.CasterWorld || target.playerData.worldGeneration != offer.TargetWorld ||
                        target.playerData.lifeGeneration != offer.TargetLife) return;
                    RestoreVitals(target, offer.Ability);
                }
                // Combat teardown takes fight locks, so it must run outside the lifecycle lock used by death.
                Present(offer.Caster, target, offer.Ability);
            }
        }

        /// <summary>Script callback resources. Ordinary resurrection retains Death Effect and the actual PvP choice.</summary>
        public static void RestoreVitals(WorldClient target, UInt16 ability)
        {
            if (!Handles(ability)) throw new ArgumentOutOfRangeException(nameof(ability));
            target.playerData.isDead = false;
            target.playerData.respawnPending = false;
            target.playerData.lifeGeneration++;
            target.playerInstance.IsDead.setValue(false);
            target.playerInstance.Health.setValue(ability == 325 ? (UInt16)1 : NumericalUtils.ByteArrayToUint16(target.playerInstance.MaxHealth.getValue(), 1));
            target.playerInstance.InnerStrengthAvailable.setValue(ability == 325 ? (UInt16)1 : AbilityHandler.MaxInnerStrength(target));
            lock (target.playerData.conditionTokens) target.playerData.conditionStateFlags = 0;
            target.playerInstance.ConditionStateFlags.setValue(PvpFlag.FlagsOf(target));
        }

        private static void Present(WorldClient caster, WorldClient target, UInt16 ability)
        {
            PvpCombat.OnPlayerGone(target, true);
            CombatManager.GetHandler(target).StopCombatIfActive(CombatSession.CombatEndReason.AttackerFled);
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(target.playerInstance.Position.getValue(), ref x, ref y, ref z);
            // Existing verified stand transition; script-specific Action_Resurrect still needs a native capture.
            target.messageQueue.addObjectMessage(DuelHandler.BuildStandUp(++target.playerData.emoteCounter,
                (float)x, (float)y, (float)z, CombatHandler.CombatStartTime()), false);
            UInt16 health = PvpCombat.HealthOf(target);
            target.messageQueue.addObjectMessage(PlayerViewAttributes.BuildSelf(
                (PlayerViewAttributes.SelfIsDead, PlayerViewAttributes.U8(0)),
                (PlayerViewAttributes.SelfHealth, PlayerViewAttributes.U16(health)),
                (PlayerViewAttributes.SelfInnerStrength, PlayerViewAttributes.U16(AbilityHandler.CurrentInnerStrength(target))),
                (PlayerViewAttributes.SelfConditionStateFlags, PlayerViewAttributes.U32(PvpFlag.FlagsOf(target)))), false);
            PvpFlag.ToWatchers(target, PlayerViewAttributes.Build(
                (PlayerViewAttributes.OtherIsDead, PlayerViewAttributes.U8(0)),
                (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(health)),
                (PlayerViewAttributes.OtherConditionStateFlags, PlayerViewAttributes.U32(PvpFlag.FlagsOf(target)))));
            string sourceName = TimedEffects.PlayerName(caster), targetName = TimedEffects.PlayerName(target);
            caster.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(0xc6002840, true, ability, sourceName, targetName));
            target.messageQueue.addRpcMessage(AbilityMessages.AboutAbility(0xc600283e, false, ability, sourceName, targetName));
            caster.FlushQueue();
            ServerPackets.SendPlayerAbilityEffect(target, RestoreEffect);
            target.FlushQueue();
            Output.WriteDebugLog("[RESURRECTION] Restored " + TimedEffects.PlayerName(target) + " with " + ability +
                " HP=" + health + " IS=" + AbilityHandler.CurrentInnerStrength(target));
        }
    }
}
