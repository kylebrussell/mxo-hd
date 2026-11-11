using System;
using System.Collections.Generic;
using System.Linq;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Fights between players: who may be attacked, damage and death, abilities on players, and the entry points the
    /// combat RPCs use when their target is a player. The interlock itself is <see cref="PvpFight"/>, gunfire
    /// <see cref="PvpGunfire"/>, duels <see cref="DuelHandler"/>. See docs/PVP.md for what is retail's and what is ours.
    /// </summary>
    public static class PvpCombat
    {
        /// <summary>How far apart two players may be for a close-combat request to be taken (ours; the client walks up).</summary>
        public const float CloseCombatRequestMetres = 15f;

        public static PvpRules.Subject SubjectOf(WorldClient client)
        {
            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            byte[] org = client.playerInstance.OrganizationID.getValue();
            byte[] team = client.playerInstance.MissionTeamID.getValue();
            return new PvpRules.Subject
            {
                CharId = client.playerData.getCharID(),
                Level = PvpFlag.LevelOf(client),
                Organization = org != null && org.Length > 0 ? org[0] : 0u,
                PvpFlag = client.playerData.pvpFlag,
                DuelId = client.playerData.duelId,
                InPvpZone = InPvpZone(client, x, z),
                MissionTeamId = team != null && team.Length >= 4 ? NumericalUtils.ByteArrayToUint32(team, 1) : 0u,
                Dead = client.playerData.isDead || client.playerData.duelDeath,
            };
        }

        private static bool InPvpZone(WorldClient client, double x, double z)
        {
            try
            {
                return SectorProperties.IsPvpZone((UInt16)client.playerData.getDistrictId(), x, z);
            }
            catch (Exception)
            {
                return false; // no sector data loaded (unit tests)
            }
        }

        public static PvpRules.Verdict Check(WorldClient attacker, WorldClient target)
        {
            if (TutorialMission.Of(attacker) != null || TutorialMission.Of(target) != null)
            {
                return PvpRules.Verdict.NotOpponent;
            }
            if (attacker.playerData.getDistrictId() != target.playerData.getDistrictId()) return PvpRules.Verdict.NotOpponent;
            return PvpRules.Check(SubjectOf(attacker), SubjectOf(target), PvpFlag.ModeFor(attacker), PvpFlag.MaxSafeLevel);
        }

        /// <summary>The other player a view of this client shows, or null.</summary>
        public static WorldClient PlayerInView(WorldClient client, UInt16 viewId)
        {
            if (viewId <= 2)
            {
                return null;
            }
            ClientView view = client.viewMan.getViewById(viewId);
            if (view == null || !view.viewCreated)
            {
                return null;
            }
            return SocialHandler.OnlineWhere(c => c != client && c.playerData.getEntityId() == view.entityId).FirstOrDefault();
        }

        /// <summary>
        /// A player as a client knows them: its own view 2 with its self-spawn id, or its view of the other player with
        /// that view's spawn id. 0 when the client has no view of them.
        /// </summary>
        public static UInt32 HandleOf(WorldClient receiver, WorldClient subject)
        {
            if (receiver == subject)
            {
                return DuelPackets.Handle(2, receiver.playerData.selfSpawnIdCounter);
            }
            ClientView view = receiver.viewMan.FindViewForEntity(subject.playerData.getEntityId());
            return view == null || !view.viewCreated ? 0u : DuelPackets.Handle(view.ViewID, view.spawnId);
        }

        public static float MetresBetween(WorldClient a, WorldClient b)
        {
            double ax = 0, ay = 0, az = 0, bx = 0, by = 0, bz = 0;
            NumericalUtils.LtVector3dToDoubles(a.playerInstance.Position.getValue(), ref ax, ref ay, ref az);
            NumericalUtils.LtVector3dToDoubles(b.playerInstance.Position.getValue(), ref bx, ref by, ref bz);
            return (float)(Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by) + (az - bz) * (az - bz)) / CombatCalculator.WorldUnitsPerMetre);
        }

        public static UInt16 HealthOf(WorldClient client)
        {
            byte[] health = client.playerInstance.Health.getValue();
            return health != null && health.Length >= 2 ? NumericalUtils.ByteArrayToUint16(health, 1) : (UInt16)0;
        }

        // RPC 4d CombatRequestResult reasons (client.dll 0x6219c410)
        public const byte RefusedDead = 2, RefusedFull = 6, RefusedOutOfRange = 9, RefusedNotOpponent = 24;

        /// <summary>
        /// RPC 4d CombatRequestResult (size 9): reason, is-target-reason, is-force-combat, start-recast-timer, target.
        /// Retail, a refused interlock: <c>4d 0e 00 01 01 0d 00 07 00</c>. Reason 24 prints "Target is not an opponent.",
        /// 2 "You cannot fight while you are dead" or, with is-target-reason, "Your target is dead".
        /// </summary>
        public static byte[] BuildCombatRefused(byte reason, bool targetReason, UInt32 target, bool forceCombat = false, bool startRecast = false)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x4d);
            pak.AddByte(reason);
            pak.AddByte(targetReason ? (byte)1 : (byte)0);
            pak.AddByte(forceCombat ? (byte)1 : (byte)0);
            pak.AddByte(startRecast ? (byte)1 : (byte)0);
            pak.AddUint32(target, 1);
            return pak.ReturnFinalPacket();
        }

        private static bool Refuse(WorldClient attacker, PvpRules.Verdict verdict, UInt32 target)
        {
            byte reason = verdict == PvpRules.Verdict.NotOpponent ? RefusedNotOpponent : RefusedDead;
            attacker.messageQueue.addRpcMessage(BuildCombatRefused(reason, verdict == PvpRules.Verdict.TargetDead, target));
            attacker.FlushQueue();
            Output.WriteDebugLog("[PVP] Attack by " + PvpFlag.Name(attacker) + " refused: " + verdict);
            return true;
        }

        /// <summary>
        /// RPC 40 at a player. Returns false when the target isn't a player (the request is then an NPC's business).
        /// </summary>
        public static bool TryCloseCombat(WorldClient attacker, byte[] request, UInt32 targetHandle)
        {
            WorldClient target = PlayerInView(attacker, (UInt16)targetHandle);
            if (target == null)
            {
                return false;
            }

            CombatHandler handler = CombatManager.GetHandler(attacker);
            // A 41 (fire) comes with a 40 for the same target, which retail didn't answer
            if (handler.pvpGunfire != null && handler.pvpGunfire.Target == target)
            {
                return true;
            }
            if (handler.pvpFight != null && handler.pvpFight.Involves(target))
            {
                PvpRules.Verdict sameFight = Check(attacker, target);
                if (sameFight != PvpRules.Verdict.Allowed) return Refuse(attacker, sameFight, targetHandle);
                return handler.pvpFight.ChangeTarget(attacker, target, request);
            }

            PvpRules.Verdict verdict = Check(attacker, target);
            if (verdict != PvpRules.Verdict.Allowed)
            {
                return Refuse(attacker, verdict, targetHandle);
            }
            if (MetresBetween(attacker, target) > CloseCombatRequestMetres)
            {
                attacker.messageQueue.addRpcMessage(BuildCombatRefused(RefusedOutOfRange, false, targetHandle));
                attacker.FlushQueue();
                return true;
            }
            PvpFight theirs = CombatManager.GetHandler(target).pvpFight;
            if (theirs != null)
            {
                if (handler.pvpFight == null && theirs.Join(attacker, target, request)) return true;
                attacker.messageQueue.addRpcMessage(BuildCombatRefused(RefusedFull, false, targetHandle));
                attacker.FlushQueue();
                return true;
            }

            PvpFight.Start(attacker, target, request);
            return true;
        }

        /// <summary>RPC 41 at a player. Returns false when the target isn't a player.</summary>
        public static bool TryRangedCombat(WorldClient shooter, UInt32 targetHandle)
        {
            WorldClient target = PlayerInView(shooter, (UInt16)targetHandle);
            if (target == null)
            {
                return false;
            }
            CombatHandler handler = CombatManager.GetHandler(shooter);
            if (handler.pvpGunfire != null && handler.pvpGunfire.Target == target)
            {
                return true;
            }
            PvpRules.Verdict verdict = Check(shooter, target);
            if (verdict != PvpRules.Verdict.Allowed)
            {
                return Refuse(shooter, verdict, targetHandle);
            }
            PvpGunfire.Start(shooter, target, targetHandle);
            return true;
        }

        /// <summary>The hit's effect on other players' views of the victim, when the hit has one.</summary>
        private static byte[] OtherHealthUpdate(WorldClient victim, UInt16 health, UInt32 fx)
        {
            if (fx == 0)
            {
                // Retail: <view> 02 80 80 10 <hp> (died_in_pvp: 05 00 02 80 80 10 0e 06)
                return PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(health)));
            }
            // Retail: <view> 02 80 80 90 <hp> 0c <EffectID> <EffectCounter> (05 00 02 80 80 90 19 06 0c ff 06 00 28 cf)
            victim.playerData.emoteCounter++;
            return PlayerViewAttributes.Build(
                (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(health)),
                (PlayerViewAttributes.OtherEffectId, PlayerViewAttributes.U32(fx)),
                (PlayerViewAttributes.OtherEffectCounter, PlayerViewAttributes.U8(victim.playerData.emoteCounter)));
        }

        /// <summary>
        /// Takes health off a player: their own view (<c>02 00 02 80 80 80 40 &lt;hp&gt;</c>) and everyone's view of them.
        /// Returns true when it took the last of it. The death itself is <see cref="Defeated"/>.
        /// </summary>
        public static bool Damage(WorldClient victim, int damage, UInt32 fx = 0)
        {
            if (damage <= 0 || victim.playerData.isDead || victim.playerData.duelDeath)
            {
                return false;
            }
            UInt16 health, before;
            lock (victim.playerData)
            {
                before = HealthOf(victim);
                health = (UInt16)Math.Max(0, before - damage);
                victim.playerInstance.Health.setValue(health);
            }
            victim.messageQueue.addObjectMessage(ServerPackets.BuildSelfHealthUpdate(health), false);
            victim.FlushQueue();
            PvpFlag.ToWatchers(victim, OtherHealthUpdate(victim, health, fx));
            TimedEffects.HealthChanged(TimedEffects.ForPlayer(victim), before - health,
                health < NumericalUtils.ByteArrayToUint16(victim.playerInstance.MaxHealth.getValue(), 1));
            return health == 0;
        }

        /// <summary>
        /// A player has no health left because of another player: the end of a duel if they are duelling each other,
        /// otherwise a death.
        /// </summary>
        public static void Defeated(WorldClient victim, WorldClient killer)
        {
            if (killer != null && DuelHandler.AreDuelling(victim, killer))
            {
                DuelHandler.Defeat(victim, killer);
                return;
            }
            Kill(victim, killer);
        }

        /// <summary>
        /// A player dies at another player's hand, as in retail's PvP deaths (died_in_pvp and some_pvp): the fights they
        /// are in end, their own view dies (the same update a death by an NPC sends: that one was taken from these
        /// captures), everyone who sees them gets IsDead and Health 0 on their view (<c>03 00 02 80 80 12 01 00 00</c>), and
        /// victim, killer and onlookers get the kill notice. The respawn is the usual one (81 4c, the Loading Area).
        /// No experience, information or loot changes hands: the captures show none.
        /// </summary>
        public static void Kill(WorldClient victim, WorldClient killer)
        {
            if (victim.playerData.isDead)
            {
                return;
            }
            Output.WriteDebugLog("[PVP] " + PvpFlag.Name(victim) + " killed by " + (killer != null ? PvpFlag.Name(killer) : "nobody"));
            DuelHandler.OnKilledByOutsider(victim);
            EndFightsOf(victim, PvpFight.EndReason.Death);

            CombatHandler.HandlePlayerDeath(victim);
            if (!victim.playerData.isDead)
            {
                return; // the tutorial's immortality
            }
            PvpFlag.ToWatchers(victim, PlayerViewAttributes.Build(
                (PlayerViewAttributes.OtherIsDead, PlayerViewAttributes.U8(1)),
                (PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(0))));
            if (killer != null)
            {
                SendKillNotice(victim, killer);
            }
            DuelBot.OnKilled(victim);
        }

        /// <summary>
        /// The kill notice: an 81 67 line with both names, routed (81 9d) with the killer's and the victim's handles.
        /// Retail sent it to the victim and, in the same packet as the death, to an onlooker.
        /// </summary>
        private static void SendKillNotice(WorldClient victim, WorldClient killer)
        {
            byte[] line = DuelPackets.BuildKillLine(PvpFlag.Name(killer), PvpFlag.Name(victim));
            foreach (WorldClient receiver in Onlookers(victim, killer))
            {
                receiver.messageQueue.addRpcMessage(DuelPackets.BuildAboutTwo(HandleOf(receiver, killer), HandleOf(receiver, victim), line));
                receiver.FlushQueue();
            }
        }

        /// <summary>The two players and everyone who has a view of either.</summary>
        public static List<WorldClient> Onlookers(WorldClient a, WorldClient b)
        {
            List<WorldClient> result = SocialHandler.OnlineWhere(c => c != a && c != b &&
                (HasView(c, a) || HasView(c, b)));
            result.Insert(0, a);
            if (b != a)
            {
                result.Insert(1, b);
            }
            return result;
        }

        private static bool HasView(WorldClient watcher, WorldClient subject)
        {
            ClientView view = watcher.viewMan.FindViewForEntity(subject.playerData.getEntityId());
            return view != null && view.viewCreated;
        }

        /// <summary>Ends the interlock and the gunfire a player is part of (their own, and anyone's shooting at them).</summary>
        public static void EndFightsOf(WorldClient client, PvpFight.EndReason reason)
        {
            CombatHandler handler = CombatManager.GetHandler(client);
            handler.pvpFight?.End(reason, client);
            handler.pvpGunfire?.Stop();
            foreach (WorldClient shooter in SocialHandler.OnlineWhere(c => c != client))
            {
                PvpGunfire fire = CombatManager.GetHandler(shooter).pvpGunfire;
                if (fire != null && fire.Target == client)
                {
                    fire.Stop();
                }
            }
        }

        /// <summary>
        /// A player disconnected or their world reloaded: their fights end without a teardown for them, and their duel
        /// stops (the other side gets the duel's end first and its teardown a second later). With
        /// <paramref name="tearDownOthersNow"/> the other side's interlock comes down at once, before the player's view
        /// is deleted.
        /// </summary>
        public static void OnPlayerGone(WorldClient client, bool tearDownOthersNow = false)
        {
            EndFightsOf(client, PvpFight.EndReason.Left);
            DuelHandler.OnUnavailable(client);
            PvpFight.OnPlayerGone(client, tearDownOthersNow);
        }

        private static readonly Random random = new Random();

        /// <summary>
        /// An attack ability lands on the player in one of the caster's views: the retail damage and effect where
        /// known (<see cref="AbilityTargets.Retail"/>), its timed effects (<see cref="TimedEffects.ApplyToPlayer"/>) and
        /// the flag abilities. Returns false when the view isn't a player's.
        /// </summary>
        public static bool LandAbility(WorldClient caster, AbilityItem ability, UInt16 targetViewId)
        {
            WorldClient target = PlayerInView(caster, targetViewId);
            if (target == null)
            {
                return false;
            }
            UInt32 handle = HandleOf(caster, target);
            double range = AbilityCatalog.Find(ability.getAbilityID())?.Number("Ability/SelectionRange") ?? 0;
            if (range > 0 && MetresBetween(caster, target) > range)
            {
                caster.messageQueue.addRpcMessage(BuildCombatRefused(RefusedOutOfRange, false, handle));
                caster.FlushQueue();
                return true;
            }
            PvpRules.Verdict verdict = Check(caster, target);
            if (verdict != PvpRules.Verdict.Allowed)
            {
                Refuse(caster, verdict, handle);
                return true;
            }
            LandAbilityOn(caster, ability, target);
            return true;
        }

        public static void LandAbilityOn(WorldClient caster, AbilityItem ability, WorldClient target)
        {
            if (!ControlActions.CanUseAbilities(caster)) return;
            UInt16 abilityId = ability.getAbilityID();
            UInt16 damage;
            UInt32 fx;
            lock (random)
            {
                if (AbilityTargets.Retail.TryGetValue(abilityId, out AbilityTargets.Effect effect))
                {
                    damage = AbilityTargets.RollDamage(effect, random);
                    fx = effect.HitFx;
                }
                else
                {
                    damage = TempMods.TryGet(abilityId, out TempMods.Effect timed) && timed.Virus
                        ? (UInt16)0
                        : AbilityHandler.AbilityDamage(ability, random);
                    fx = ability.getActivationFX();
                }
            }
            if (fx == 0)
            {
                fx = CombatHandler.DefaultAbilityHitFx;
            }
            string casterName = PvpFlag.Name(caster), targetName = PvpFlag.Name(target);
            Output.WriteDebugLog("[PVP] " + casterName + "'s " + ability.getAbilityName() + " lands on " + targetName + " for " + damage);
            PvpFlag.OnFightStarts(caster, true);
            PvpFlag.ApplyAbility(target, abilityId);
            TimedEffects.ApplyToPlayer(target, abilityId, casterName, caster);
            if (damage > 0)
            {
                // Retail's lines: "Your %1z does %4I damage to %3s." and "%2s's %1z does %4I damage to you."
                caster.messageQueue.addRpcMessage(AbilityMessages.Build(AbilityMessages.DamageCaster, true, AbilityMessages.ByGameObject,
                    AbilityMessages.AbilityGameObject(abilityId), AbilityMessages.TypeGameObject, casterName, targetName, damage, 0));
                target.messageQueue.addRpcMessage(AbilityMessages.Build(AbilityMessages.DamageTarget, false, AbilityMessages.ByGameObject,
                    AbilityMessages.AbilityGameObject(abilityId), AbilityMessages.TypeGameObject, casterName, targetName, damage, 0));
                caster.FlushQueue();
            }
            // A non-damaging control still has an activation effect on its victim. Send that effect to the
            // victim and their watchers; the caster's execution animation is a separate view update.
            ServerPackets.SendPlayerAbilityEffect(target, fx);
            if (Damage(target, damage))
            {
                Defeated(target, caster);
            }
        }

        // Tactic conditions on a player (CombatInterlock.ConditionBit), each for its own ten seconds

        public static void ApplyCondition(WorldClient victim, UInt32 bit)
        {
            if (bit == 0)
            {
                return;
            }
            ClientData data = victim.playerData;
            int token;
            lock (data.conditionTokens)
            {
                data.conditionTokens.TryGetValue(bit, out token);
                data.conditionTokens[bit] = ++token;
                data.conditionStateFlags |= bit;
            }
            PvpFlag.SendFlags(victim);
            int mine = token;
            DelayedActions.After(CombatInterlock.ConditionSeconds, () =>
            {
                lock (data.conditionTokens)
                {
                    if (!data.conditionTokens.TryGetValue(bit, out int now) || now != mine || (data.conditionStateFlags & bit) == 0)
                    {
                        return;
                    }
                    data.conditionStateFlags &= ~bit;
                }
                PvpFlag.SendFlags(victim);
            });
        }

        /// <summary>Staggered adds 5% to the damage taken, as for NPCs (Mob.IncomingDamageMultiplier).</summary>
        public static float IncomingDamageMultiplier(WorldClient victim)
        {
            return (victim.playerData.conditionStateFlags & CombatInterlock.ConditionBit(CombatTactic.Power)) != 0 ? 1.05f : 1f;
        }
    }
}
