using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using hds.shared;
using hds.world.Skill;

namespace hds
{
    public class AbilityHandler
    {
        public AbilityItem currentAbility;
        public UInt16 currentTargetViewId;
        public Timer hyperjumpTimer;
        private List<JumpStep> Steps = new List<JumpStep>();


        public void ProcessUpgradeAbility(ref byte[] packet) => AbilityTraining.Handle(Store.currentClient, packet);

        public void ProcessAbility(ref byte[] packet)
        {
            double now = TimeUtils.GetSimTimeSeconds();
            AbilityCastInterrupts.FinishDue(Store.currentClient, now);
            PacketReader reader = new PacketReader(packet);
            UInt16 AbilityID = reader.ReadUInt16(1);
            AbilityID = AbilityCatalog.Canonical(AbilityID);
            if (PrisonDevices.Handles(AbilityID)) { PrisonDevices.Activate(Store.currentClient, AbilityID, packet); return; }
            UInt16 currentTargetViewId = reader.ReadUInt16(1);

            bool starterViralDebuffNeedsLoadout = AbilityID == 43 || AbilityID == 48 || AbilityID == 69;
            if (!CoderPets.IsCastLoaded(Store.currentClient, AbilityID) ||
                ((starterViralDebuffNeedsLoadout || CoderPets.StarterSummonRequiresLoaded(AbilityID)) &&
                    !AbilityPassives.IsLoaded(Store.currentClient, AbilityID)))
            {
                Deny(AbilityID, "Load this ability before using it.", AbilityRefusals.NotLoaded, sendServerText: false);
                return;
            }

            bool guardianSupport = AbilitySupport.IsGuardianSupport(AbilityID);
            if (guardianSupport && !AbilityPassives.IsLoaded(Store.currentClient, AbilityID))
            {
                Deny(AbilityID, "Load this ability before using it.", AbilityRefusals.NotLoaded, sendServerText: false);
                return;
            }
            if (AbilityID == AbilitySupport.PersonalFirewall2)
            {
                var caster = Store.currentClient;
                var fight = CombatManager.GetHandler(caster);
                if (fight.isCombatRunning || fight.HasActiveInterlock)
                {
                    Deny(AbilityID, "Personal Firewall 2.0 cannot be used during combat.", AbilityRefusals.MustBeOutsideCombat, sendServerText: false);
                    return;
                }
                if (CombatStyles.Active(caster) != CombatStyles.HackerStyle)
                {
                    Deny(AbilityID, "Personal Firewall 2.0 requires Hacker Style.", AbilityRefusals.WrongStyle, sendServerText: false);
                    return;
                }
            }

            bool concealment = AbilityID == Stealth.ConcealmentGrantAbility;
            if (concealment && !AbilityPassives.IsLoaded(Store.currentClient, AbilityID))
            {
                Deny(AbilityID, "Load Concealment before using it.", AbilityRefusals.NotLoaded, sendServerText: false);
                return;
            }

            // These selected viruses need a live enemy before any cast or reuse, including Code Nuke's splash centre.
            bool selectedViralAttack = AbilityID == 53 || (AbilityID >= 57 && AbilityID <= 60) ||
                AbilityID == 350 || AbilityID == 351 || AbilityID == 392 || AbilityID == 394;
            ushort? lockedDamageTargetSpawn = null;
            if (selectedViralAttack)
            {
                var caster = Store.currentClient;
                if (!AbilityPassives.IsLoaded(caster, AbilityID))
                {
                    Deny(AbilityID, "Load this ability before using it.", AbilityRefusals.NotLoaded, sendServerText: false);
                    return;
                }
                lockedDamageTargetSpawn = (ushort?)caster.viewMan.getViewById(currentTargetViewId)?.spawnId;
                ushort? requestedSpawn = packet.Length == 41 ? BitConverter.ToUInt16(packet, 4) : lockedDamageTargetSpawn;
                if (!lockedDamageTargetSpawn.HasValue ||
                    !AbilityTargets.HasLiveAttackSelection(caster, currentTargetViewId, requestedSpawn, requireOpponent: true))
                {
                    Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                        new AbilityRefusals.Record(AbilityID, AbilityRefusals.InvalidTarget)));
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog($"[ABILITY] Denied {AbilityID}: invalid starter viral target {currentTargetViewId}");
                    return;
                }
            }

            bool starterDebuffNeedsLoadout = AbilityID == 40 || AbilityID == 66 || AbilityID == 86;
            if (starterDebuffNeedsLoadout && !AbilityPassives.IsLoaded(Store.currentClient, AbilityID))
            {
                Deny(AbilityID, "Load this ability before using it.", AbilityRefusals.NotLoaded, sendServerText: false);
                return;
            }

            // Internal effect templates are reapplied by their sourced parent callbacks, never a free client cast.
            if(AbilityID == AbilityEmpBlast.Weakness) { Deny(AbilityID,"NPC-only ownership marker cannot be activated."); return; }
            if(AbilityID == CoderPets.StopProxyEffect) { Deny(AbilityID,"Use Detonate Proxy on your simulacrum."); return; }
            if(AbilityHealthCallbacks.InternalEffect(AbilityID)) { Deny(AbilityID,"Use the parent ability, not its internal effect."); return; }

            if (AbilityID == AbilityBuffs.SittingEffect) return; // Owned by the normal sit/stand stance requests.

            // These Hacker controls and debuffs require a live selected enemy.
            bool hostileControl = AbilityID == 43 || AbilityID == 48 || AbilityID == 315;
            bool starterMovementDebuff = AbilityID == 40 || AbilityID == 41 || AbilityID == 86;
            ushort? movementDebuffTargetSpawn = null;
            if (hostileControl || starterMovementDebuff || AbilityID == 66 || AbilityID == 69 ||
                AbilityID == 354 || AbilityID == 476)
            {
                if (AbilityID == 40 || AbilityID == 86)
                    movementDebuffTargetSpawn = (ushort?)Store.currentClient.viewMan.getViewById(currentTargetViewId)?.spawnId;
                ushort? spawn = packet.Length == 41 ? (ushort?)BitConverter.ToUInt16(packet, 4) : movementDebuffTargetSpawn;
                if (!AbilityTargets.HasLiveAttackSelection(Store.currentClient, currentTargetViewId, spawn, requireOpponent: hostileControl || starterMovementDebuff))
                {
                    // Stock reason 24 is ID_TARGET_NOT_VALID. The client supplies the only refusal line.
                    Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(AbilityID, AbilityRefusals.InvalidTarget)));
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog($"[ABILITY] Denied {AbilityID}: invalid selected enemy {currentTargetViewId}");
                    return;
                }
            }

            // These starter Coder casts require a live selected target, including the Tinkering area centre.
            bool starterCoderCast = AbilityID == 21 || AbilityID == 33 || AbilityID == 36 || AbilityID == 514;
            if (starterCoderCast)
            {
                var caster = Store.currentClient;
                if (!AbilityPassives.IsLoaded(caster, AbilityID))
                {
                    Deny(AbilityID, "Load this ability before using it.", AbilityRefusals.NotLoaded, sendServerText: false);
                    return;
                }
                lockedDamageTargetSpawn = (ushort?)caster.viewMan.getViewById(currentTargetViewId)?.spawnId;
                ushort? requestedSpawn = packet.Length == 41 ? BitConverter.ToUInt16(packet, 4) : lockedDamageTargetSpawn;
                if (!lockedDamageTargetSpawn.HasValue ||
                    !AbilityTargets.HasLiveAttackSelection(caster, currentTargetViewId, requestedSpawn, requireOpponent: true))
                {
                    const string notice = "Select a living enemy before using this ability.";
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, notice, "BROADCAST");
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog("[ABILITY] Denied " + AbilityID + ": " + notice);
                    return;
                }
            }

            ushort? overloadTargetSpawn = null;
            if (AbilityID == 66)
            {
                var caster = Store.currentClient;
                overloadTargetSpawn = packet.Length == 41 ? BitConverter.ToUInt16(packet, 4) :
                    (ushort?)caster.viewMan.getViewById(currentTargetViewId)?.spawnId;
                var player = PvpCombat.PlayerInView(caster, currentTargetViewId);
                if (!overloadTargetSpawn.HasValue || (player != null && PvpCombat.Check(caster, player) != PvpRules.Verdict.Allowed))
                {
                    Deny(AbilityID, "Select a living enemy before using Overload.", AbilityRefusals.InvalidTarget, sendServerText: false);
                    return;
                }
                if (!AbilityTargets.WithinSelectionRange(caster, AbilityID, currentTargetViewId))
                {
                    Deny(AbilityID, "The target is out of range.", AbilityRefusals.TargetOutOfRange, sendServerText: false);
                    return;
                }
            }

            // Timed gun specials may resolve in interlock or as a selected attack. Both use the same live target.
            bool starterGunCast = AbilityID == 126 || AbilityID == 176;
            ushort? gunTargetSpawn = null;
            if (starterGunCast)
            {
                var shooter = Store.currentClient;
                gunTargetSpawn = (ushort?)shooter.viewMan.getViewById(currentTargetViewId)?.spawnId;
                ushort? requestedSpawn = packet.Length == 41 ? BitConverter.ToUInt16(packet, 4) : gunTargetSpawn;
                if (!gunTargetSpawn.HasValue ||
                    !AbilityTargets.HasLiveAttackSelection(shooter, currentTargetViewId, requestedSpawn, requireOpponent: true))
                {
                    Deny(AbilityID, "Select a living enemy before using this ability.", AbilityRefusals.InvalidTarget, sendServerText: false);
                    return;
                }
            }

            // Hindering Shot is a selected hostile shot. An empty or obsolete handle must not start a cast.
            if (AbilityID == 130)
            {
                var shooter = Store.currentClient;
                var combat = CombatManager.GetHandler(shooter);
                if (combat.isCombatRunning || combat.HasActiveInterlock)
                {
                    shooter.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                        new AbilityRefusals.Record(AbilityID, AbilityRefusals.MustBeOutsideCombat)));
                    shooter.FlushQueue();
                    Output.WriteDebugLog("[ABILITY] Denied 130: requires outside combat");
                    return;
                }
                gunTargetSpawn = (ushort?)shooter.viewMan.getViewById(currentTargetViewId)?.spawnId;
                ushort? spawn = packet.Length == 41 ? BitConverter.ToUInt16(packet, 4) : gunTargetSpawn;
                if (!gunTargetSpawn.HasValue ||
                    !AbilityTargets.HasLiveAttackSelection(shooter, currentTargetViewId, spawn, requireOpponent: true))
                {
                    const string notice = "Select a living enemy before using this ability.";
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, notice, "BROADCAST");
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog("[ABILITY] Denied " + AbilityID + ": " + notice);
                    return;
                }
            }

            bool starterRangedControl = AbilityID == 43 || AbilityID == 48 || AbilityID == 130 || starterGunCast;
            if ((selectedViralAttack || starterMovementDebuff || starterCoderCast || starterRangedControl || AbilityID == 69) &&
                !AbilityTargets.WithinSelectionRange(Store.currentClient, AbilityID, currentTargetViewId))
            {
                Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                    new AbilityRefusals.Record(AbilityID, AbilityRefusals.TargetOutOfRange)));
                Store.currentClient.FlushQueue();
                Output.WriteDebugLog($"[ABILITY] Denied {AbilityID}: out-of-range selected target {currentTargetViewId}");
                return;
            }

            bool starterSelectedViralControl = AbilityID == 43 || AbilityID == 48 || AbilityID == 69;
            ushort? viralControlTargetSpawn = starterSelectedViralControl
                ? (packet.Length == 41 ? BitConverter.ToUInt16(packet, 4) :
                    (ushort?)Store.currentClient.viewMan.getViewById(currentTargetViewId)?.spawnId)
                : null;
            var definition = AbilityCatalog.Find(AbilityID);
            bool selectedGunShot = starterGunCast || AbilityID == 130;
            if (selectedGunShot && !AbilityTargets.HasSelectionSight(Store.currentClient, definition, currentTargetViewId))
            {
                Deny(AbilityID, $"Sight blocked for selected target {currentTargetViewId}.",
                    AbilityRefusals.TargetNotInSight, sendServerText: false);
                return;
            }

            // load the ability name from a list to see if we match the right ability
            DataLoader AbilityLoader = DataLoader.getInstance();
            currentAbility = AbilityLoader.getAbilityByID(AbilityID);
            if (definition?.Has("Ability/CastingTime") == true) currentAbility.setCastingTime(definition.CastSeconds);

            if (AbilityID == ConcealmentDetection.Ability && AbilityPassives.Level(Store.currentClient, AbilityID) > 0 &&
                !DisruptConnectionEligibility.HasTool(
                    Store.dbManager.WorldDbHandler.GetInventoryItems(Store.currentClient.playerData.getCharID()), AbilityID))
            {
                Store.currentClient.messageQueue.addRpcMessage(DuelPackets.BuildSystemString(UtilityAbilities.ToolFail));
                Store.currentClient.FlushQueue();
                return;
            }

            // Old1124 still requires an exact matching inventory tool. Tool470 names1125, not1124.
            // Refuse before duplicate/cast/cost/cooldown state; never route this template to passive384.
            if (AbilityID == DisruptConnectionEligibility.Ability && DisruptConnectionEligibility.Refused(AbilityID,
                Store.dbManager.WorldDbHandler.GetInventoryItems(Store.currentClient.playerData.getCharID())))
            {
                Deny(AbilityID, "A matching tool is required to activate this ability.");
                return;
            }

            // Gaussian Blur requires the live Evading state, including when shield suppression is active.
            if (AbilityID == 49 && !EvasionShield.HasState(Store.currentClient))
            {
                Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(
                    AbilityID, AbilityRefusals.CasterStateRequired, states: EvasionShield.StateFlag)));
                Store.currentClient.FlushQueue();
                Output.WriteDebugLog("[ABILITY] Denied 49: requires Evading");
                return;
            }

            // Compel Close Combat stores CastWhenUsed=1 and has no interlock move.
            if (AbilityID == 150 && CombatManager.GetHandler(Store.currentClient).HasActiveInterlock)
            {
                Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                    new AbilityRefusals.Record(AbilityID, AbilityRefusals.MustBeOutsideCombat)));
                Store.currentClient.FlushQueue();
                Output.WriteDebugLog("[ABILITY] Denied 150: active interlock");
                return;
            }

            string abilityName = CoderPets.DisplayName(AbilityID) ?? currentAbility.getAbilityName();
            Output.WriteDebugLog("[ABILITY] " + AbilityID + " (" + abilityName + ") on view " + currentTargetViewId);

            {
                ushort? spawn = packet.Length == 41 ? (ushort?)BitConverter.ToUInt16(packet, 4) : null;
                if (SpyAbilityRegistry.TryRefuse(Store.currentClient, AbilityID, currentTargetViewId, spawn, out uint refusalReason))
                {
                    Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(AbilityID, refusalReason)));
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog("[ABILITY] Denied " + AbilityID + ": native refusal " + refusalReason);
                    return;
                }
                if (SpyGroundBombs.TryRefuse(Store.currentClient, AbilityID, packet, out uint bombRefusal))
                {
                    Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(AbilityID, bombRefusal)));
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog("[ABILITY] Denied " + AbilityID + ": native refusal " + bombRefusal);
                    return;
                }
            }


            bool existingInterlockPath = AbilityID == AwakenedPowerShot.Id || AbilityID == 197 || AbilityID == 198 || AbilityID == 105 ||
                AbilityID == 145 || AbilityID == 605 || AbilityID == MartialArtsStarterAdmission.GuardBreaker || CombatSpecials.IsExileWeapon(AbilityID);
            bool martialInterlock = definition?.Number("Ability/CastWhenUsed") == 3 &&
                definition.Number("Ability/CombatSpecialMove") == 1 &&
                (CombatStyles.RequiredStyle(AbilityID) switch
                {
                    CombatStyles.Aikido => true,
                    CombatStyles.Karate => true,
                    CombatStyles.KungFu => true,
                    _ => false
                });
            uint stateMask = AbilityID == 197 ? 2u : martialInterlock ? (uint)definition.Number("Ability/CastRequiredStates") : 0u;
            ushort requiredState = stateMask switch
            {
                2 => TempMods.DazeAbility,
                4 => TempMods.OffBalanceAbility,
                8 => TempMods.StaggeredAbility,
                _ => 0
            };
            bool stateGatedInterlock = requiredState != 0 && !existingInterlockPath;
            bool strictInterlock = existingInterlockPath || stateGatedInterlock;
            uint? requiredOpponent = null;
            if (strictInterlock)
            {
                ushort? spawn = packet.Length == 41 ? (ushort?)BitConverter.ToUInt16(packet, 4) : null;
                requiredOpponent = CombatManager.GetHandler(Store.currentClient).PowerShotOpponent(currentTargetViewId, spawn);
                if (!requiredOpponent.HasValue)
                {
                    DenyMissingInterlock(AbilityID);
                    return;
                }
                if (requiredState != 0 && !CombatManager.GetHandler(Store.currentClient).CurrentOpponentHasState(currentTargetViewId, requiredState))
                {
                    Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                        new AbilityRefusals.Record(AbilityID, AbilityRefusals.TargetStateRequired, states: stateMask)));
                    Store.currentClient.FlushQueue();
                    return;
                }
            }

            bool lineUp = AbilityID == AbilityHealthCallbacks.LineUpAbility;
            if (lineUp && AbilityHealthCallbacks.LineUpRefusal(Store.currentClient, CombatManager.GetHandler(Store.currentClient)) is uint lineUpRefusal)
            {
                uint states = lineUpRefusal == AbilityRefusals.CasterStateRequired ? Stealth.ConcealmentFlag : 0;
                Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(
                    new AbilityRefusals.Record(AbilityID, lineUpRefusal, states: states)));
                Store.currentClient.FlushQueue();
                Output.WriteDebugLog($"[ABILITY] Denied {AbilityID}: Line Up The Shot refusal {lineUpRefusal}");
                return;
            }

            if (CombatSpecials.IsRifleOpener(AbilityID) || AbilityID == CombatSpecials.ConcealedShot)
            {
                var shooter = Store.currentClient;
                var fight = CombatManager.GetHandler(shooter);
                if (fight.isCombatRunning || fight.HasActiveInterlock)
                {
                    Deny(AbilityID, "This rifle shot cannot be used during combat.", AbilityRefusals.MustBeOutsideCombat);
                    return;
                }
                ushort? spawn = packet.Length == 41 ? (ushort?)BitConverter.ToUInt16(packet, 4) : null;
                if (!AbilityTargets.HasLiveAttackSelection(shooter, currentTargetViewId, spawn, requireOpponent: true))
                {
                    shooter.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(AbilityID, AbilityRefusals.InvalidTarget)));
                    shooter.FlushQueue();
                    return;
                }
                if (AbilityID == CombatSpecials.ConcealedShot)
                {
                    if (!Stealth.IsOn(shooter, Stealth.ConcealmentGrantAbility))
                    {
                        shooter.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(
                            AbilityID, AbilityRefusals.CasterStateRequired, states: Stealth.ConcealmentFlag)));
                        shooter.FlushQueue();
                        Output.WriteDebugLog("[ABILITY] Denied 504: requires Concealment");
                        return;
                    }
                    if (!AbilityTargets.WithinSelectionRange(shooter, AbilityID, currentTargetViewId))
                    {
                        Deny(AbilityID, $"Out-of-range selected target {currentTargetViewId}.",
                            AbilityRefusals.TargetOutOfRange, sendServerText: false);
                        return;
                    }
                    if (!AbilityTargets.HasSelectionSight(shooter, definition, currentTargetViewId))
                    {
                        Deny(AbilityID, $"Sight blocked for selected target {currentTargetViewId}.",
                            AbilityRefusals.TargetNotInSight, sendServerText: false);
                        return;
                    }
                }
            }

            if (SpyAbilityDamage.RequiresLiveEnemy(AbilityID) || DeadlyThrowDamage.RequiresLiveEnemy(AbilityID))
            {
                ushort? spawn = packet.Length == 41 ? (ushort?)BitConverter.ToUInt16(packet, 4) : null;
                if (!AbilityTargets.HasLiveAttackSelection(Store.currentClient, currentTargetViewId, spawn))
                {
                    const string notice = "Select a living enemy before using this ability.";
                    new ServerPackets().sendSystemChatMessage(Store.currentClient, notice, "BROADCAST");
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog("[ABILITY] Denied " + AbilityID + ": " + notice);
                    return;
                }
            }

            {
                ushort? spawn = packet.Length == 41 ? (ushort?)BitConverter.ToUInt16(packet, 4) : null;
                if (BullDogDamage.TryRefuse(Store.currentClient, AbilityID, currentTargetViewId, spawn, out uint refusalReason))
                {
                    Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(AbilityID, refusalReason)));
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog("[ABILITY] Denied " + AbilityID + ": native refusal " + refusalReason);
                    return;
                }
            }

            // The client sends a double-clicked ability twice in a row; count it once.
            ClientData data = Store.currentClient.playerData;
            bool nodeSearch = UtilityAbilities.IsDataNodeSearch(AbilityID);
            UInt16? locatorSlot = nodeSearch ? UtilityAbilities.LocatorBagSlot(packet) : null;
            if (!CraftingSession.Handles(AbilityID) && data.lastAbilityId == AbilityID &&
                (!nodeSearch || data.lastLocatorSlot == locatorSlot) && now - data.lastAbilityTime < DuplicateAbilityWindowSeconds)
            {
                Output.WriteDebugLog("[ABILITY] Duplicate use ignored");
                return;
            }
            if (!strictInterlock && !nodeSearch)
            {
                data.lastAbilityId = AbilityID;
                data.lastAbilityTime = now;
            }

            // CanUseWhileStunned belongs to the owned AntiStun item, never a free971/972 request.
            if (AntiStunPills.Handles(AbilityID))
            {
                var antiSlot = ConsumableItems.ItemSlotOfAbilityRequest(packet);
                if (!antiSlot.HasValue || !new InventoryHandler().UseItem(antiSlot.Value, AbilityID))
                    AntiStunPills.Deny(Store.currentClient, AbilityID);
                return;
            }

            if (data.isDead || data.duelDeath || ControlActions.Incapacitated(TimedEffects.ForPlayer(Store.currentClient).Effects))
            {
                Deny(AbilityID, "You cannot act while stunned or pacified.");
                return;
            }
            if (nodeSearch && UtilityAbilities.Level(Store.currentClient, AbilityID) <= 0)
            {
                Deny(AbilityID, "Load this data-node search ability before activating it.", AbilityRefusals.NotLoaded, sendServerText: false);
                return;
            }
            if (AbilityID == ConcealmentDetection.Ability && AbilityPassives.Level(Store.currentClient, AbilityID) == 0)
            {
                Deny(AbilityID, "Load Detection before activating it.");
                return;
            }
            if (AbilityID == SixthSense.Ability && !SixthSense.CanCast(Store.currentClient))
            {
                Deny(AbilityID, "Load Sixth Sense and enter the Matrix before activating it.");
                return;
            }

            if (!CraftingSession.Handles(AbilityID)) CraftingSession.End(Store.currentClient);

            // A combat style used from the Actions window (80 b9 <style>) switches the style, as a 3e does (CombatStyles)
            if (CombatStyles.UseStyle(Store.currentClient, AbilityID))
            {
                Store.currentClient.FlushQueue();
                return;
            }

            // Tool activation and window close are a distinct flag-bearing 80 b9 path.
            if (CraftingSession.Handles(AbilityID))
            { CraftingSession.Process(Store.currentClient, packet); return; }

            // Double-clicking a consumable sends its ability with the item's bag slot.
            UInt16? itemSlot = ConsumableItems.ItemSlotOfAbilityRequest(packet);
            if (itemSlot.HasValue && new InventoryHandler().UseItem(itemSlot.Value, AbilityID))
            {
                return;
            }
            UInt32? selectedLocator = null;

            // Internal pill abilities require their owned bag item, even if an ability was loaded.
            if (HealthBoostPills.Handles(AbilityID))
            { HealthBoostPills.Deny(Store.currentClient, AbilityID); return; }

            if (!ControlActions.CanUseAbilities(Store.currentClient))
            {
                Deny(AbilityID, "You cannot use abilities while powerless.");
                return;
            }
            if (!ControlActions.CanPerformSpecialMove(Store.currentClient, AbilityID))
            {
                Deny(AbilityID, "You cannot perform special moves while confused.");
                return;
            }
            if (nodeSearch)
            {
                // Turning off a running search does not start a cast or require enough IS to start another.
                var locator = Store.currentClient;
                if (UtilityAbilities.StopFindDataNode(locator))
                {
                    data.lastAbilityId = AbilityID;
                    data.lastAbilityTime = now;
                    data.lastLocatorSlot = locatorSlot;
                    locator.FlushQueue();
                    return;
                }
                selectedLocator = UtilityAbilities.ToolFor(locator, AbilityID, locatorSlot);
                if (selectedLocator == 0)
                {
                    locator.messageQueue.addRpcMessage(DuelPackets.BuildSystemString(UtilityAbilities.ToolFail));
                    locator.FlushQueue();
                    return;
                }
            }
            if (AbilityBuffs.TacticalConflict(Store.currentClient, AbilityID))
            {
                AwakenedTacticalToggles.Refuse(Store.currentClient, AbilityID);
                return;
            }
            if (AbilityID == AbilityDisguise.Ability && !Stealth.IsOn(Store.currentClient, AbilityID) &&
                AbilityDisguise.ModelFor(Store.currentClient, itemSlot) == 0)
            {
                Deny(AbilityID, "Carry a disguise tool, or select its inventory slot.");
                return;
            }
            if (AbilityID == ProxyTechnician.Ability && ProxyTechnician.Refusal(Store.currentClient) is string proxyRefusal)
            { Deny(AbilityID,proxyRefusal); return; }
            // A toggled aura used while it is on switches off (no cost, cooldown or cast)
            if (AbilityAuras.Stop(Store.currentClient, AbilityID)) return;
            // A special move works only in the style it belongs to (CastRequiredCombatStance, CombatStyles); a loaded
            // style is switched to (a server choice; docs/COMBAT-STYLES.md)
            if (!CombatStyles.EnsureStyleFor(Store.currentClient, AbilityID))
            {
                // The client shows ID_ABILITY_NOT_IN_CORRECT_STYLE for this 80 ba code
                Store.currentClient.messageQueue.addRpcMessage(AbilityCooldowns.BuildDenied(AbilityID, CombatStyles.DeniedWrongStyle));
                Store.currentClient.FlushQueue();
                Output.WriteDebugLog("[ABILITY] Denied " + AbilityID + ": needs the " + CombatStyles.Name(CombatStyles.RequiredStyle(AbilityID)) + " combat style");
                return;
            }
            // Stealth (docs/STEALTH.md): a state that is on switches off; otherwise the script's own test runs first.
            bool stealth = Stealth.Handles(AbilityID);
            if (stealth && Stealth.IsOn(Store.currentClient, AbilityID))
            {
                Stealth.End(Store.currentClient, "switched off");
                return;
            }
            if (stealth && Stealth.Refusal(Store.currentClient, AbilityID, currentTargetViewId, out uint stealthRefusalReason) is string refusal)
            {
                Deny(AbilityID, refusal, stealthRefusalReason);
                return;
            }
            if (StealthShieldAbilities.Refusal(Store.currentClient,AbilityID,currentTargetViewId) is string freeRefusal)
            { Output.WriteDebugLog("[STEALTH-FREE]"+AbilityID+" cast refused: "+freeRefusal);Deny(AbilityID,freeRefusal);return; }
            // Coder simulacra: summons, abilities on the caster's own simulacrum and Daemon Boost (CoderPets)
            bool pet = CoderPets.Handles(AbilityID);
            if (pet && CoderPets.IsToggledOn(Store.currentClient, AbilityID))
            {
                CoderPets.ToggleOff(Store.currentClient);
                return;
            }
            // The 43-byte stock request carries the view and spawn as one network handle.
            UInt16? targetSpawn = packet.Length == 41 && currentTargetViewId > 2 ? (UInt16?)BitConverter.ToUInt16(packet, 4) : null;
            CoderPets.OwnPetCast ownPet = null;
            uint petRefusalReason = AbilityRefusals.LegacyAmbiguous;
            string petRefusal = pet ? CoderPets.Prepare(Store.currentClient, AbilityID, currentTargetViewId, targetSpawn, out ownPet, out petRefusalReason) : null;
            if (petRefusal != null)
            {
                // The client supplies its own lines for range and combat-state refusals.
                Deny(AbilityID, petRefusal, petRefusalReason, sendServerText:
                    petRefusalReason != AbilityRefusals.TargetOutOfRange && petRefusalReason != AbilityRefusals.MustBeOutsideCombat);
                return;
            }
            bool derez = AbilityID == CoderPets.DerezAbility;
            CoderPets.Pet enemyPet = null;
            if (derez)
            {
                uint reason = CoderPets.PrepareDerez(Store.currentClient, currentTargetViewId, targetSpawn, out enemyPet);
                if (reason != 0)
                {
                    Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(AbilityID, reason)));
                    Store.currentClient.FlushQueue();
                    Output.WriteDebugLog($"[ABILITY] Denied {AbilityID}: native refusal {reason}");
                    return;
                }
            }
            bool support = !pet && !stealth && AbilitySupport.Handles(AbilityID);
            if (support)
            {
                var targets = AbilitySupport.Targets(Store.currentClient, AbilityID, currentTargetViewId, targetSpawn);
                if (targets.Count == 0)
                {
                    if (AbilityResurrection.Handles(AbilityID))
                        Deny(AbilityID, "Choose a dead friendly player within range.", AbilityRefusals.InvalidTarget, sendServerText: false);
                    else if (guardianSupport)
                        Deny(AbilityID, "Choose a living friendly target within range.", AbilityRefusals.InvalidTarget, sendServerText: false);
                    else
                        Deny(AbilityID, "Choose a living friendly target within range.");
                    return;
                }
                if (targets.Any(target => AbilitySupport.SightBlocked(Store.currentClient, target, AbilityID)))
                {
                    Deny(AbilityID, $"Sight blocked for selected target {currentTargetViewId}.",
                        AbilityRefusals.TargetNotInSight, sendServerText: false);
                    return;
                }
                if (AbilitySupport.RequiresBuffer(AbilityID) && targets.All(target => !AbilitySupport.BufferFits(target, AbilityID)))
                {
                    Deny(AbilityID, "The upgrade buffer is full.");
                    return;
                }
            }
            bool switchOff = AbilityBuffs.IsToggledOn(Store.currentClient, AbilityID);
            if (!switchOff)
            {
                // Snapshot the same existing admission inputs once; the response reports their exact positive deficit.
                UInt16 available=CurrentInnerStrength(Store.currentClient);
                UInt16 effective=AbilityCosts.Effective(Store.currentClient, AbilityID);
                if(available<effective)
                {
                    Deny(AbilityID,"You do not have enough Inner Strength.",AbilityRefusals.InsufficientInnerStrength,(uint)(effective-available));
                    return;
                }
            }

            // The stock client allows these active upgrades to be switched off during their activation timer.
            bool startingClassUpgrade = AbilityBuffs.IsStartingClassUpgrade(AbilityID);
            if (!(switchOff && startingClassUpgrade) && AbilityCooldowns.IsCoolingDown(data, AbilityID, now))
            {
                Output.WriteDebugLog("[ABILITY] Still cooling down; denied");
                Store.currentClient.messageQueue.addRpcMessage(
                    AbilityCooldowns.BuildDenied(AbilityID, AbilityCooldowns.DeniedTimerRunning));
                Store.currentClient.FlushQueue();
                return;
            }

            // Without this a second use during the cast lands too: the cooldown only starts when the cast ends
            if (AbilityCooldowns.IsCasting(data, now))
            {
                Output.WriteDebugLog("[ABILITY] A cast is running; denied");
                Store.currentClient.messageQueue.addRpcMessage(
                    AbilityCooldowns.BuildDenied(AbilityID, AbilityCooldowns.DeniedBusy));
                Store.currentClient.FlushQueue();
                return;
            }
            if (strictInterlock)
            {
                var caster = Store.currentClient;
                var handler = CombatManager.GetHandler(caster);
                bool queuedFromStealth = stateGatedInterlock && Stealth.IsHidden(caster);
                if (handler.PowerShotOpponent(currentTargetViewId, (ushort)(requiredOpponent.Value >> 16)) != requiredOpponent ||
                    !handler.QueueRoundAbility(currentAbility, currentTargetViewId, queuedFromStealth, requiredOpponent))
                {
                    DenyMissingInterlock(AbilityID);
                    return;
                }
                data.lastAbilityId = AbilityID;
                data.lastAbilityTime = now;
                if (stateGatedInterlock) Stealth.BreakForAction(caster, "used ability " + AbilityID);
                ProcessAbilityScript(currentAbility);
                AbilityCooldowns.Start(caster, AbilityID, now);
                return;
            }
            if (nodeSearch)
            {
                data.lastAbilityId = AbilityID;
                data.lastAbilityTime = now;
                data.lastLocatorSlot = locatorSlot;
            }
            // Switching a toggle off starts its reuse without reserving a cast or spending IS.
            if (switchOff)
            {
                AbilityBuffs.Remove(Store.currentClient, AbilityID);
                Store.currentClient.FlushQueue();
                return;
            }
            if (currentAbility.getCastingTime() > 0)
            {
                AbilityCooldowns.StartCast(data, now, currentAbility.getCastingTime());
            }

            var freeStealth=StealthShieldAbilities.Handles(AbilityID)?StealthShieldAbilities.Capture(Store.currentClient,currentTargetViewId):null;
            var ghostStrike=GhostStrikeCallbacks.Capture(Store.currentClient,AbilityID,currentTargetViewId);
            bool lineUpConcealed = lineUp && Stealth.IsOn(Store.currentClient, Stealth.ConcealmentGrantAbility);
            bool sneakOpener = Stealth.IsHidden(Store.currentClient);
            // Attacking or using another ability ends stealth (our rule; see Stealth.BreakForAction)
            if (AbilityID==StealthShieldAbilities.DisableEvasion) StealthShieldAbilities.ActionTaken(Store.currentClient);
            else if (!stealth && !lineUpConcealed && !SpyAbilityRegistry.RetainsStealthDuringCast(AbilityID)) Stealth.BreakForAction(Store.currentClient, "used ability " + AbilityID);

            if(freeStealth!=null)StealthShieldAbilities.Arm(Store.currentClient,freeStealth);
            GhostStrikeCallbacks.Arm(ghostStrike);
            var normalCast=AbilityCastInterrupts.Begin(Store.currentClient,AbilityID,currentAbility.getCastingTime());
            GhostStrikeCallbacks.Bind(ghostStrike,normalCast);
            var engageFoes=AbilityEngageFoes.Capture(Store.currentClient,AbilityID,currentTargetViewId,normalCast);
            var reverseBulldog=AbilityReverseBulldog.Capture(Store.currentClient,AbilityID,currentTargetViewId,normalCast);
            // Sneak ends aura generations before publishing its IS debit. A pending upkeep
            // tick then fails the generation check. The Belt still ends its auras when its cast lands.
            if (AbilityID == Stealth.SneakGrantAbility)
                Stealth.StopStealthAuras(Store.currentClient);
            var groundBomb = SpyGroundBombs.Capture(Store.currentClient, AbilityID, packet);
            ProcessAbilityScript(this.currentAbility);

            // When the cast finishes (right away for instant abilities): heals land and, as in retail, the
            // cooldown starts and buffs go on.
            WorldClient client = Store.currentClient;
            AbilityItem ability = currentAbility;
            WorldClient corpse = AbilityResurrection.Handles(AbilityID) ? AbilitySupport.PlayerInView(client, currentTargetViewId, targetSpawn) : null;
            int casterLife = data.lifeGeneration, casterWorld = data.worldGeneration, targetWorld = corpse?.playerData.worldGeneration ?? 0, targetLife = corpse?.playerData.lifeGeneration ?? 0;
            AbilityCastInterrupts.After(normalCast, () =>
            {
                if (!ControlActions.CanUseAbilities(client)) return;
                if (concealment && !AbilityPassives.IsLoaded(client, AbilityID)) return;
                // The archived class upgrades publish their activation timer before the exclusive-on record.
                if (startingClassUpgrade)
                    AbilityCooldowns.Start(client, AbilityID, TimeUtils.GetSimTimeSeconds());
                if (corpse != null) AbilityResurrection.Request(client, corpse, AbilityID, casterWorld, casterLife, targetWorld, targetLife);
                else if (lineUp)
                {
                    if (AbilityPassives.IsLoaded(client, AbilityID) &&
                        AbilityPassives.IsLoaded(client, Stealth.ConcealmentGrantAbility))
                        AbilityHealthCallbacks.LineUp(client, lineUpConcealed);
                    else
                        Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: Line Up The Shot or its Concealment grant was unloaded");
                }
                else if (support)
                {
                    if (guardianSupport && !AbilityPassives.IsLoaded(client, AbilityID))
                        Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the starter support ability was unloaded before landing");
                    else
                        AbilitySupport.Land(client, AbilityID, currentTargetViewId, spawn: targetSpawn);
                }
                if (AbilityAuras.Handles(AbilityID)) AbilityAuras.Start(client, AbilityID);
                if (derez) CoderPets.Derez(client, enemyPet, currentTargetViewId, targetSpawn);
                if (pet) CoderPets.Land(client, AbilityID, ownPet, normalCast.SummonUnloaded);
                if (AbilityID == AbilityEmpBlast.Ability) AbilityEmpBlast.Land(client);
                if (UtilityAbilities.Handles(AbilityID)) UtilityAbilities.Land(client, AbilityID, currentTargetViewId, locatorSlot, selectedLocator);
                if (CombatSpecials.ActsOnCast(AbilityID)) CombatSpecials.CastTaunt(client);
                if (!startingClassUpgrade && AbilityBuffs.CooldownStartsOnUse(ability.getAbilityID()))
                {
                    AbilityCooldowns.Start(client, ability.getAbilityID(), TimeUtils.GetSimTimeSeconds());
                }

            if (starterCoderCast && !AbilityPassives.IsLoaded(client, AbilityID))
            {
                Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the Coder ability was unloaded during the cast");
                return;
            }

            if ((starterCoderCast || selectedViralAttack) &&
                (!AbilityTargets.HasLiveAttackSelection(client, currentTargetViewId, lockedDamageTargetSpawn, requireOpponent: true) ||
                 !AbilityTargets.WithinSelectionRange(client, AbilityID, currentTargetViewId)))
            {
                string castKind = starterCoderCast ? "Coder" : "viral";
                Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the selected {castKind} target changed or left range");
                return;
            }

            if (selectedViralAttack && !AbilityPassives.IsLoaded(client, AbilityID))
            {
                Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the starter viral ability was unloaded before landing");
                return;
            }

            if (AbilityID == ConcealmentDetection.Ability)
            {
                ConcealmentDetection.Land(client);
                return;
            }

            if (AbilityID == SixthSense.Ability)
            {
                SixthSense.Land(client);
                return;
            }

            if (starterSelectedViralControl &&
                (!viralControlTargetSpawn.HasValue ||
                 !AbilityTargets.HasLiveAttackSelection(client, currentTargetViewId, viralControlTargetSpawn, requireOpponent: true) ||
                 !AbilityTargets.WithinSelectionRange(client, AbilityID, currentTargetViewId)))
            {
                Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the selected viral debuff target changed or left range");
                return;
            }

            if (stealth)
            {
                UInt16 stealthTarget = currentTargetViewId;
                if (ControlActions.CanUseAbilities(client)) Stealth.Land(client, AbilityID, stealthTarget, itemSlot);
                return;
            }

            if (starterDebuffNeedsLoadout && !AbilityPassives.IsLoaded(client, AbilityID))
            {
                Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the starter debuff was unloaded before landing");
                return;
            }

            if(freeStealth!=null)
            {
                ushort target=currentTargetViewId;
                StealthShieldAbilities.Execute(client,ability,target,freeStealth);
                return;
            }
            if (CasterHostileArea.Handles(AbilityID))
            {
                CasterHostileArea.Land(client, ability);
                return;
            }
            if (AbilityID == 66 &&
                (!AbilityTargets.HasLiveAttackSelection(client, currentTargetViewId, overloadTargetSpawn, requireOpponent: true) ||
                 !AbilityTargets.WithinSelectionRange(client, AbilityID, currentTargetViewId)))
            {
                Output.WriteDebugLog("[ABILITY] 66 fizzles: the selected Overload target changed or left range");
                return;
            }
            if (SpyGroundBombs.Handles(AbilityID))
            {
                SpyGroundBombs.Land(client, ability, groundBomb);
                return;
            }
            // Attack abilities on the current close-combat target hit it (buffs used with a target selected don't).
            ushort hostileView=engageFoes?.PrimaryView??currentTargetViewId;
            if (selectedGunShot && (!AbilityTargets.HasLiveAttackSelection(client, hostileView, gunTargetSpawn, requireOpponent: true) ||
                !AbilityTargets.WithinSelectionRange(client, AbilityID, hostileView) ||
                !AbilityTargets.HasSelectionSight(client, definition, hostileView)))
            {
                Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the selected gun target changed, left range or left sight");
                return;
            }
            if (hostileView > 2 && !support && !pet && !derez && definition?.FriendlyTarget != true &&
                (!ability.getIsBuff() || CombatSpecials.TargetsEnemy(AbilityID) || engageFoes!=null) &&
                (definition == null || definition.DirectDamage || definition.DamageOverTime || TempMods.Has(AbilityID) ||
                 AbilityHostile.Lands(AbilityID) || CombatSpecials.TargetsEnemy(AbilityID)))
            {
                if (movementDebuffTargetSpawn.HasValue &&
                    (!AbilityTargets.HasLiveAttackSelection(client, hostileView, movementDebuffTargetSpawn, requireOpponent: true) ||
                     !AbilityTargets.WithinSelectionRange(client, AbilityID, hostileView)))
                {
                    Output.WriteDebugLog($"[ABILITY] {AbilityID} fizzles: the selected movement-debuff target changed or left range");
                    return;
                }
                // When the cast ends it lands on that NPC (any NPC in view, starting a fight if there is none):
                // the retail damage and effect, stuns and damage over time (AbilityTargets). It used to land at once,
                // and only on the close-combat target.
                UInt16 targetView = hostileView;
                // Instant abilities and reviewed gun specials on the current interlock opponent become the next
                // move after their cast completes (RPC 47 kind 6, followed by the next round's RPC 45).
                bool queueWithRound = ability.getCastingTime() <= 0 || CombatAnimations.CanQueueGunCast(client, AbilityID);
                if (!queueWithRound || !CombatManager.GetHandler(client).QueueRoundAbility(ability, targetView, sneakOpener))
                {
                    if (ControlActions.CanUseAbilities(client)) AbilityTargets.Land(client, ability, engageFoes?.PrimaryView??targetView, sneakOpener, AbilityCastOrigin.NormalFreeCast,engageFoes:engageFoes,reverseBulldog:reverseBulldog,ghostStrike:ghostStrike);
                }
            }
            });
        }

        private static void DenyMissingInterlock(ushort ability)
        {
            if (ability == AwakenedPowerShot.Id)
            {
                Deny(ability, "Power Shot requires your current interlock opponent.");
                return;
            }
            Store.currentClient.messageQueue.addRpcMessage(
                AbilityRefusals.Encode(new AbilityRefusals.Record(ability, AbilityRefusals.InvalidTarget)));
            Store.currentClient.FlushQueue();
            Output.WriteDebugLog($"[ABILITY] Denied {ability}: requires the current interlock opponent");
        }

        public const double DuplicateAbilityWindowSeconds = 0.5;

        private static void Deny(UInt16 id, string message, uint reason = AbilityRefusals.LegacyAmbiguous, uint deficit = 0,
            bool sendServerText = true)
        {
            Store.currentClient.messageQueue.addRpcMessage(AbilityRefusals.Encode(new AbilityRefusals.Record(id,reason,deficit)));
            if (sendServerText) new ServerPackets().sendSystemChatMessage(Store.currentClient, message, "BROADCAST");
            Store.currentClient.FlushQueue();
            Output.WriteDebugLog("[ABILITY] Denied " + id + ": " + message);
        }

        /// <summary>Legacy healing magnitude retained until the native value function is reconstructed.</summary>
        public static bool IsSelfHeal(AbilityItem ability)
        {
            string name = ability.getAbilityName() ?? "";
            return AbilityCatalog.Find(ability.getAbilityID())?.Healing == true || name.StartsWith("RestoreHealth") || name.StartsWith("EmergencyRepairs");
        }

        /// <summary>Raises the player's health (never above the maximum) and sends the own-view health update.</summary>
        public static UInt16 HealSelf(WorldClient client, UInt16 amount)
        {
            UInt16 max = NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1);
            UInt16 current = NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1);
            if (client.playerData.isDead || client.playerData.duelDeath)
            {
                return current;
            }

            UInt16 healed = (UInt16)Math.Min(max, current + amount);
            client.playerInstance.Health.setValue(healed);
            if (healed != current) TimedEffects.HealthChanged(TimedEffects.ForPlayer(client), current - healed, healed < max);
            healed = PvpCombat.HealthOf(client); // synchronous health procs may change the result
            client.messageQueue.addObjectMessage(ServerPackets.BuildSelfHealthUpdate(healed), false);
            client.FlushQueue();
            PvpFlag.ToWatchers(client, PlayerViewAttributes.Build((PlayerViewAttributes.OtherHealth, PlayerViewAttributes.U16(healed))));
            Output.WriteDebugLog("[ABILITY] Healed " + current + " -> " + healed);
            return healed;
        }

        /// <summary>
        /// Damage of an attack ability. The data's ValueFrom/ValueTo are used when set; most combat abilities
        /// (Cheap Shot, Head Butt, ...) have none, so they hit for about twice a normal round (~30).
        /// </summary>
        public static UInt16 AbilityDamage(AbilityItem ability, Random random, bool allowTypedResistanceQa = false, int characterLevel = 0)
        {
            var source = AbilityCatalog.Find(ability.getAbilityID());
            if (source != null && !source.DirectDamage) return 0;
            if ((source?.Id == 126 || source?.Id == 138 || source?.Id == 139) &&
                (ability.getValueFrom() <= 0 || ability.getValueTo() < ability.getValueFrom()))
            {
                int expectedType = source.Id == 126 ? 1 : 0;
                var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
                if (health.Length == 1)
                {
                    var shipped = health[0];
                    if (shipped.Number("DamageType", -1) == expectedType &&
                        shipped.Number("DurationType", -1) == 1 && shipped.Number("ValueFunction") == 0 &&
                        shipped.Has("MinValue") && shipped.Has("MaxValue") &&
                        shipped.Number("MinValue") == shipped.Number("MaxValue") &&
                        AbilityValues.TryValue(shipped, 0, 0, out int amount) && amount > 0 && amount <= UInt16.MaxValue)
                        return (UInt16)amount;
                }
            }
            int from = ability.getValueFrom();
            int to = ability.getValueTo();
            // Static Blast 1.0 has a fixed viral GOB component and an incomplete legacy CSV range (22/0).
            if (source?.Id == 33)
            {
                var hit = source.Modifiers.Single(m => m.Target == 108 && m.Number("DamageType", -1) == 2 &&
                    m.Number("WhenEnabled", -1) == 2 && m.Number("DurationType", -1) == 1);
                if (AbilityValues.TryValue(hit, 0, 0, out int damage) && damage > 0) return (ushort)damage;
            }
            if (from > 0 && to >= from)
            {
                return (UInt16)random.Next(from, to + 1);
            }
            // These reviewed legacy rows omit a usable range and have one fixed, instant health modifier.
            int? fixedType = source?.Id switch
            {
                504 => 1,  // Concealed Shot
                36 => 2,   // Tinkering
                105 => 1,  // Body Shot
                130 => 1,  // Hindering Shot
                134 => 7,  // Leg Sweep
                152 => 7,  // Spinning Backsweep
                176 => 1,  // Covering Fire
                197 => 0,  // Head Butt
                198 => 0,  // Cheap Shot
                254 => 1,  // Immobilizing Shot
                255 => 1,  // Disabling Shot
                514 => 2,  // Advanced Tinkering
                605 => 0,  // Low Blow
                _ => null
            };
            if (fixedType.HasValue)
            {
                var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
                if (health.Length == 1)
                {
                    var shipped = health[0];
                    if (shipped.Number("DamageType", -1) == fixedType.Value &&
                        shipped.Number("DurationType", -1) == 1 &&
                        shipped.Number("ValueFunction") == 0 &&
                        shipped.Has("MinValue") && shipped.Has("MaxValue") &&
                        shipped.Number("MinValue") == shipped.Number("MaxValue") &&
                        AbilityValues.TryValue(shipped, 0, 0, out int amount) && amount > 0 && amount <= UInt16.MaxValue)
                        return (UInt16)amount;
                }
            }
            int? fixture = allowTypedResistanceQa ? TypedResistanceQa.Draw(ability.getAbilityID(),50,81) : null;
            if (fixture.HasValue) return (UInt16)fixture.Value;
            if (SpyAbilityRegistry.TryResolveDamage(source, characterLevel, random, out ushort registeredDamage))
                return registeredDamage;
            // Overhand Smash has one fixed physical health component and no usable legacy range.
            if (source?.Id == 145)
            {
                var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
                if (health.Length == 1)
                {
                    var damage = health[0];
                    if (damage.Number("DamageType", -1) == 0 && damage.Number("DurationType", -1) == 1 &&
                        damage.Number("ValueFunction") == 0 && damage.Number("MinValue", -1) == damage.Number("MaxValue", -2) &&
                        AbilityValues.TryValue(damage, 0, 0, out int amount) && amount > 0)
                        return (UInt16)amount;
                }
            }
            if (SpyAbilityDamage.TryResolve(source, characterLevel, random, out ushort spyDamage))
                return spyDamage;
            if (DeadlyThrowDamage.TryResolve(source, characterLevel, out ushort deadlyDamage))
                return deadlyDamage;
            if (BullDogDamage.TryResolve(source, out ushort bullDogDamage))
                return bullDogDamage;
            return (UInt16)(StarterHackerViralBase(source) ?? random.Next(50, 81));
        }

        // These early Hacker CSV rows have incomplete ranges. Read only the reviewed fixed viral modifiers.
        private static int? StarterHackerViralBase(AbilityCatalog.Entry source)
        {
            if (source == null || (source.Id != 53 && (source.Id < 57 || source.Id > 60))) return null;
            var health = source.Modifiers.Where(m => m.Target == 108 && m.Number("WhenEnabled", -1) == 2).ToArray();
            if (health.Length != 1) return null;

            var modifier = health[0];
            if (modifier.Number("DamageType", -1) != 2 || modifier.Number("DurationType", -1) != 1 ||
                modifier.Number("ValueFunction") != 0 || !modifier.Has("MinValue") || !modifier.Has("MaxValue") ||
                modifier.Number("MinValue") != modifier.Number("MaxValue") ||
                !AbilityValues.TryValue(modifier, 0, 0, out int amount) || amount <= 0 || amount > UInt16.MaxValue)
                return null;
            return amount;
        }

        /// <summary>Inner Strength cost of abilities without a retail measurement; see AbilityCosts.</summary>
        public const UInt16 DefaultAbilityCost = AbilityCosts.Default;

        /// <summary>Inner Strength regained per timer tick (2 s); retail regenerated about 4 a second.</summary>
        public const UInt16 InnerStrengthRegenPerTick = 8;

        public static UInt16 CurrentInnerStrength(WorldClient client)
        {
            return NumericalUtils.ByteArrayToUint16(client.playerInstance.InnerStrengthAvailable.getValue(), 1);
        }

        public static UInt16 MaxInnerStrength(WorldClient client)
        {
            return NumericalUtils.ByteArrayToUint16(client.playerInstance.InnerStrengthMax.getValue(), 1);
        }

        /// <summary>Takes the cost off Inner Strength (never below 0) and returns the new value.</summary>
        public static UInt16 SpendInnerStrength(WorldClient client, UInt16 cost)
        {
            UInt16 current = CurrentInnerStrength(client);
            UInt16 remaining = (UInt16)Math.Max(0, current - cost);
            client.playerInstance.InnerStrengthAvailable.setValue(remaining);
            return remaining;
        }

        /// <summary>
        /// Whole points to grant this tick. In a fight the rate is <see cref="CombatCalculator.FIGHT_REGEN_FACTOR"/> of normal
        /// (1.6 a tick), so the fraction is carried over to the next tick.
        /// </summary>
        public static int RegenAmount(bool inFight, ref double carry, int rateBonus = 0)
        {
            double total = carry + (inFight ? InnerStrengthRegenPerTick * CombatCalculator.FIGHT_REGEN_FACTOR : InnerStrengthRegenPerTick) *
                Math.Max(0, 1 + rateBonus / 100.0);
            int whole = (int)(total + 1e-9);
            carry = total - whole;
            return whole;
        }

        /// <summary>
        /// Regenerates Inner Strength towards the maximum; returns the new value, or null when nothing changed.
        /// </summary>
        public static UInt16? RegenerateInnerStrength(WorldClient client)
        {
            UInt16 current = CurrentInnerStrength(client);
            UInt16 max = MaxInnerStrength(client);
            if (max == 0 || current >= max)
            {
                client.playerData.innerStrengthRegenCarry = 0;
                return null;
            }
            int amount = RegenAmount(CombatManager.GetHandler(client).isCombatRunning, ref client.playerData.innerStrengthRegenCarry,
                AbilityStats.Influence(client, 114));
            if (amount == 0)
            {
                return null;
            }
            UInt16 next = (UInt16)Math.Min(max, current + amount);
            client.playerInstance.InnerStrengthAvailable.setValue(next);
            return next;
        }

        public void ProcessAbilityScript(AbilityItem abilityItem)
        {
            // The cost is paid when the ability is used: the retail cost (AbilityCosts), 20 when unknown. This used
            // to set Inner Strength to 50 after every ability (and Hyper-Speed and the cast animation sent their
            // own fixed values, 50 and 237), then cost a flat 20.
            UInt16 before = CurrentInnerStrength(Store.currentClient);
            UInt16 innerStrength = SpendInnerStrength(Store.currentClient, AbilityCosts.Effective(Store.currentClient, abilityItem.getAbilityID()));
            Output.WriteDebugLog($"[ABILITY-COST] char={Store.currentClient.playerData.getCharID()} ability={abilityItem.getAbilityID()} paid={before-innerStrength} IS={before}->{innerStrength}");

            // Display Cast Bar if it is necessary
            ServerPackets pak = new ServerPackets();
            if (this.currentAbility.getCastingTime() > 0)
            {
                pak.SendCastAbilityBar(currentAbility.getAbilityID(), currentAbility.getCastingTime());
                processSelfAnimation(abilityItem);
            }

            processCharacterAnimationSelf(abilityItem.getAbilityID());

            if (currentAbility.getAbilityID() == 12)
            {
                pak.SendHyperSpeed(innerStrength);
            }
            else
            {
                pak.sendISCurrent(Store.currentClient, innerStrength);
            }

        }

        public void processCharacterAnimationSelf(UInt16 abilityID)
        {
            ServerPackets pak = new ServerPackets();
            // 2904 0429 = Hacker_VirusLaunch_A
            // 2a04 042a = Hacker_VirusLaunch_D
            // see movementAnims.tx - its for codes something (0x31)
            if (currentAbility.getAbilityExecutionFX() > 0)
            {
                ServerPackets.SendPlayerAbilityEffect(Store.currentClient, currentAbility.getAbilityExecutionFX());
            }

            if (currentAbility.getCastingTime() > 0)
            {
                byte[] castAnimStart = currentAbility.getCastAnimStart();
                // Cast
                pak.sendPlayerAnimation(Store.currentClient, StringUtils.bytesToString_NS(castAnimStart));
                // Target visuals belong to the resolved recipients when the cast lands. The old demo timer
                // read a later selection, broadcast the caster as the target and dealt an extra 150 NPC damage.
            }
        }


        public void processSelfAnimation(AbilityItem ability)
        {
//            ServerPackets serverPackets = new ServerPackets();
//            serverPackets.sendAbilitySelfAnimation(2, ability.getAbilityID(), (UInt32) ability.getAbilityExecutionFX());

            ServerPackets serverPackets = new ServerPackets();
            serverPackets.SendAbilitySelfAnimation(2, ability.getAbilityID(),
                NumericalUtils.ByteArrayToUint32(ability.getCastAnimStart(), 1));
        }

        public void ProcessJump(ref byte[] rpcData)
        {
            if (!ControlActions.CanMove(Store.currentClient)) { ControlActions.CorrectPosition(Store.currentClient); return; }
            if (!JumpRecords.TryStartBody(rpcData, out var request)) return;
            if (request.IsHyperJump == 1 && !PayHyperJump(request)) return;
            double xDest = request.Destination.X, yDest = request.Destination.Y, zDest = request.Destination.Z;
            ushort clientSector = request.Destination.Sector, clientRegion = request.Destination.Prefab,
                clientPolygon = request.Destination.NavPoly;
            string ours = NavMesh.Locate((UInt16)Store.currentClient.playerData.getDistrictId(), xDest, yDest, zDest, out NavLocation server)
                ? server.sector + "/" + server.region + "/" + server.polygon : "none";
            Output.WriteDebugLog("[NAV] Jump to " + (int)xDest + "," + (int)yDest + "," + (int)zDest + ": client " + clientSector + "/" +
                                 clientRegion + "/" + clientPolygon + ", server " + ours);
            UInt32 peakHeight = request.PeakBits;
            UInt32 clientTickSample = request.TimeStamp;

            // Players current X Z Y
            double x = 0;
            double y = 0;
            double z = 0;
            byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            // JumpEndTime is server sim time in milliseconds (retail: the 0x82 packet sim time x 1000 plus
            // ~9 s for a hyperjump). This used to be Unix seconds, decades ahead of the sim clock, so the
            // client stayed in the air for good and stopped sending movement.
            float peak = BitConverter.ToSingle(BitConverter.GetBytes(peakHeight), 0);
            double dx = xDest - x, dz = zDest - z;
            float simSeconds = TimeUtils.GetSimTimeSeconds();
            double horizontalDistance = Math.Sqrt(dx * dx + dz * dz);
            UInt32 endTime = JumpTravelEndTime(simSeconds, peak, horizontalDistance, request.IsHyperJump == 1);
            if (request.IsHyperJump == 1)
                Output.WriteDebugLog(FormattableString.Invariant(
                    $"[NAV] Hyper-Jump peak={peak:F2} horizontal={horizontalDistance / 100:F2}m travel={endTime / 1000.0 - simSeconds:F3}s"));
            (float X, float Y, float Z, uint SimMillis)? fullPeakOrigin = null;
            ClientData data = Store.currentClient.playerData;
            JumpRecords.Takeoff? report = null;
            JumpRecords.Route? route = null;
            if (request.IsHyperJump == 1 && peak == CapturedFullPeak)
            {
                uint startMs = (uint)Math.Round(simSeconds * 1000.0);
                fullPeakOrigin = ((float)x, (float)y, (float)z, startMs);
                route = new JumpRecords.Route(startMs, (x, y, z), request.Destination, peak);
                if (NavMesh.Locate((ushort)data.getDistrictId(), x, y, z, out var origin) &&
                    Math.Abs(y - (origin.floorY + 95)) <= 4)
                {
                    var takeoff = new JumpRecords.Destination(unchecked((ulong)BitConverter.DoubleToInt64Bits(x)),
                        unchecked((ulong)BitConverter.DoubleToInt64Bits(y)), unchecked((ulong)BitConverter.DoubleToInt64Bits(z)),
                        origin.sector, origin.region, origin.polygon);
                    report = new JumpRecords.Takeoff(takeoff, simSeconds, data);
                }
            }

            data.PendingJump = new JumpRecords.Flight(endTime, data, report, route);
            ServerPackets packets = new ServerPackets();
            packets.SendHyperJumpWithOneStep((float)xDest, (float)yDest, (float)zDest, peakHeight, endTime,
                clientTickSample, fullPeakOrigin);
            Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(xDest, yDest, zDest));
        }

        private static bool PayHyperJump(JumpRecords.Start request)
        {
            var client = Store.currentClient;
            if (!double.IsFinite(request.Destination.X) || !double.IsFinite(request.Destination.Y) ||
                !double.IsFinite(request.Destination.Z) || !float.IsFinite(BitConverter.UInt32BitsToSingle(request.PeakBits)))
                return false;

            // The stock start builder chooses the beta variant when present. Both GOBs store 20 IS.
            ushort ability = AbilityPassives.IsLoaded(client, 566) ? (ushort)566 : (ushort)11;
            if (!AbilityPassives.IsLoaded(client, ability))
            {
                Deny(ability, "Load Hyper-Jump before using it.", AbilityRefusals.NotLoaded, sendServerText: false);
                return false;
            }
            if (CombatManager.GetHandler(client).isCombatRunning)
            {
                Deny(ability, "You cannot Hyper-Jump in combat.", AbilityRefusals.MustBeOutsideCombat, sendServerText: false);
                return false;
            }
            ushort cost = AbilityCosts.Cost(ability);
            ushort available = CurrentInnerStrength(client);
            if (available < cost)
            {
                Deny(ability, "You do not have enough Inner Strength.", AbilityRefusals.InsufficientInnerStrength,
                    (uint)(cost - available), sendServerText: false);
                return false;
            }

            // The retail start echo carries the post-debit value, also retained in later position reports.
            ushort remaining = SpendInnerStrength(client, cost);
            Output.WriteDebugLog($"[VITALS] char={client.playerData.getCharID()} Hyper-Jump {ability} IS {available}->{remaining}");
            return true;
        }

        private const float CapturedFullPeak = 10300;

        // Retail travelling hyperjumps use about 800 world units/s at both low and full peak.
        // Keep the existing timing for ordinary jumps and near-vertical starts.
        internal static UInt32 JumpTravelEndTime(float simTimeSeconds, float peakHeight, double horizontalDistance, bool hyperJump)
        {
            const double TravelUnitsPerSecond = 800;
            const double MaximumStockTravel = 8000;
            if (!hyperJump || !double.IsFinite(horizontalDistance) ||
                horizontalDistance < TravelUnitsPerSecond / 2 || horizontalDistance > MaximumStockTravel)
                return JumpEndTime(simTimeSeconds, peakHeight);
            double travelSeconds = horizontalDistance / TravelUnitsPerSecond;
            if (peakHeight != CapturedFullPeak)
                travelSeconds = Math.Max(travelSeconds, JumpArcSeconds(peakHeight));
            return (UInt32)Math.Round((simTimeSeconds + travelSeconds) * 1000.0);
        }

        /// <summary>Gravity in world units (centimetres) per second squared.</summary>
        private const double Gravity = 981.0;

        /// <summary>
        /// Jump end time in sim milliseconds: up to the peak height (a float the client sends, world units)
        /// and back down, 2·sqrt(2h/g), at least 0.5 s. A retail hyperjump with peak 10300 lasted 9.9 s;
        /// this gives 9.2 s.
        /// </summary>
        public static UInt32 JumpEndTime(float simTimeSeconds, float peakHeight) =>
            (UInt32)Math.Round((simTimeSeconds + JumpArcSeconds(peakHeight)) * 1000.0);

        private static double JumpArcSeconds(float peakHeight)
        {
            double h = Math.Max(0.0, double.IsFinite(peakHeight) ? peakHeight : 0.0);
            return Math.Max(0.5, 2.0 * Math.Sqrt(2.0 * h / Gravity));
        }
        
        

        private void HyperJumpCalculatedStepMovement(float xPos, float yPos, float zPos, double xDest, double yDest,
            double zDest, uint clientJumpIdUnknown, float maybeMaxHeight)
        {
            LtVector3f[] JumpMovements = Maths.ParabolicMovement(new LtVector3f(xPos, yPos, zPos),
                new LtVector3f((float) xDest, (float) yDest, (float) zDest), 50, 128);

            float distance = Maths.getDistance(xPos, yPos, zPos, (float) xDest, (float) yDest, (float) zDest);
            UInt16 duration = (UInt16) (distance * 0.5f);

            UInt32 startTime = TimeUtils.getUnixTimeUint32();
            UInt32 endTime = startTime + duration;

            ServerPackets packets = new ServerPackets();
            packets.SendHyperJumpID(clientJumpIdUnknown);
            Store.currentClient.playerData.isJumping = true;
            Store.currentClient.playerData.incrementJumpID();
            UInt32 maybeTimeBasedValue = 40384248;
            foreach (LtVector3f currentJumpPos in JumpMovements)
            {
                Steps.Add(new JumpStep(currentJumpPos, new LtVector3f((float) xDest, (float) yDest, (float) zDest),
                    maybeMaxHeight, endTime, Store.currentClient.playerData.getJumpID(), maybeTimeBasedValue));
                //packets.SendHyperJumpStepUpdate(currentJumpPos, xDest, yDest, zDest, maybeMaxHeight, endTime);
                maybeTimeBasedValue = maybeTimeBasedValue + 100;
            }

            hyperjumpTimer = new Timer(ProcessJumpStep, this, 0, 0);
        }

        private void ProcessJumpStep(Object e)
        {
            JumpStep Step = Steps[0];
            Steps.RemoveAt(0);
            ServerPackets packets = new ServerPackets();

            bool isLastStep = Steps.Count == 0;

            packets.SendHyperJumpStepUpdate(Step.FromPos, Step.ToPos.x, Step.ToPos.y, Step.ToPos.z, Step.JumpHeight,
                Step.endTime, Step.jumpId, Step.maybeTimeBasedValue, isLastStep);

            if (Steps.Count > 0)
            {
                hyperjumpTimer = new Timer(ProcessJumpStep, this, 50, 0);
            }
            else
            {
                Store.currentClient.playerData.isJumping = false;
                // ToDo: Check if we need to send some Message for the Clientview
            }    
        }

        public void processHyperJumpCancel(ref byte[] rpcData)
        {
            if (!JumpRecords.TryCancelBody(rpcData, out var request)) return;
            float simSeconds = TimeUtils.GetSimTimeSeconds();
            UInt32 startTime = (UInt32)Math.Round(simSeconds * 1000.0);
            ClientData data = Store.currentClient.playerData;
            // Stock can send the takeoff report even when it locally declines to start a jump.
            var flight = data.PendingJump;
            if (!flight.HasValue || !flight.Value.IsCurrent(startTime, data))
            {
                data.PendingJump = null;
                return;
            }
            if (!ControlActions.CanMove(Store.currentClient)) { ControlActions.CorrectPosition(Store.currentClient); return; }
            if (flight.Value.InitialReport is { } takeoff && takeoff.Matches(request.Destination, simSeconds, data))
            {
                Output.WriteDebugLog("[NAV] Jump takeoff report " + request.Destination.X + "," +
                    request.Destination.Y + "," + request.Destination.Z);
                return;
            }
            double xDest = request.Destination.X, yDest = request.Destination.Y, zDest = request.Destination.Z;
            if (!double.IsFinite(xDest) || !double.IsFinite(yDest) || !double.IsFinite(zDest)) return;
            data.PendingJump = null;
            Output.WriteDebugLog("[NAV] Jump cancel to " + xDest + "," + yDest + "," + zDest + ": client " +
                request.Destination.Sector + "/" + request.Destination.Prefab + "/" + request.Destination.NavPoly);

            (float x, float y, float z) from;
            uint endTime;
            if (flight.Value.FullPeakRoute is { } route)
            {
                // Retail sends an airborne origin and keeps almost all of the original deadline in both
                // captured redirects. Retain the accepted deadline instead of starting a new fall timer.
                endTime = flight.Value.EndMilliseconds;
                from = route.PositionAt(startTime, endTime);
                Output.WriteDebugLog(FormattableString.Invariant(
                    $"[NAV] Hyper-Jump redirect from {from.x:F2},{from.y:F2},{from.z:F2} with {(endTime - startTime) / 1000.0:F3}s left"));
            }
            else
            {
                double x = 0, y = 0, z = 0;
                NumericalUtils.LtVector3dToDoubles(Store.currentClient.playerInstance.Position.getValue(), ref x, ref y, ref z);
                from = ((float)x, (float)y, (float)z);
                endTime = JumpEndTime(simSeconds, (float)Math.Max(0.0, y - yDest) / 4f);
            }

            // Cancel carries a navigation tail, without a client clock value to echo.
            new ServerPackets().SendHyperJumpUpdate(from.x, from.y, from.z, xDest, yDest, zDest, startTime, endTime);
            Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(xDest, yDest, zDest));
        }
    }
}
