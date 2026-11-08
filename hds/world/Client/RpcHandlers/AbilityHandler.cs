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


        public void ProcessUpgradeAbility(ref byte[] packet)
        {
            AbilityProgression.UpgradeRequest? parsed = AbilityProgression.ParseUpgrade(packet);
            if (!parsed.HasValue)
            {
                return;
            }

            AbilityProgression.UpgradeRequest request = parsed.Value;
            WorldClient client = Store.currentClient;
            UInt32 charId = client.playerData.getCharID();
            ServerPackets packets = new ServerPackets();

            AbilityItem abilityItem = DataLoader.getInstance().getAbilityByID(request.Ability);
            int? owned = Store.dbManager.WorldDbHandler.GetAbilityLevelAtSlot(charId, request.Slot, abilityItem.getGOID());
            if (!owned.HasValue)
            {
                Output.WriteDebugLog("[ABILITY] Upgrade of " + request.Ability + " in slot " + request.Slot + " refused: not owned");
                return;
            }

            int newLevel = owned.Value + request.Levels;
            int characterLevel = client.playerInstance.Level.getValue()[0];
            if (request.Levels == 0 || newLevel > characterLevel)
            {
                Output.WriteDebugLog("[ABILITY] Upgrade of " + request.Ability + " to " + newLevel + " refused at character level " + characterLevel);
                packets.sendSystemChatMessage(client, "Your character level is too low for that ability level.", "MODAL");
                client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUpgraded(request.Ability, (UInt16)owned.Value, ServerPackets.StatusAbilityUpgradeRefused));
                client.FlushQueue();
                return;
            }

            UInt32 cost = AbilityProgression.UpgradeCost(request.Ability, owned.Value, request.Levels);
            long cash = client.playerData.getInfo();
            if (cash < cost)
            {
                Output.WriteDebugLog("[ABILITY] Upgrade of " + request.Ability + " refused: costs " + cost + ", has " + cash);
                packets.sendSystemChatMessage(client, "You do not have enough information for that upgrade.", "MODAL");
                client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUpgraded(request.Ability, (UInt16)owned.Value, ServerPackets.StatusAbilityUpgradeRefused));
                client.FlushQueue();
                return;
            }

            long newCash = cash - cost;
            Store.dbManager.WorldDbHandler.SaveInfo(client, newCash);
            client.playerData.setInfo(newCash);
            Store.dbManager.WorldDbHandler.SetAbilityLevel(charId, request.Slot, newLevel);

            // Retail order: 80 e7 (-cost), 80 e4 (new cash), the hidden ability again at its new level, 80 b8.
            packets.SendInfoDelta(client, -(long)cost, ServerPackets.InfoReasonAbilityUpgrade);
            packets.SendInfoCurrent(client, (UInt32)newCash, ServerPackets.InfoReasonAbilityUpgrade);
            UInt16? grant = AbilityProgression.GrantOf(request.Ability);
            if (grant.HasValue)
            {
                client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUnload(grant.Value));
                client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityLoad(grant.Value,
                    AbilityProgression.GrantLevel(request.Ability, (UInt16)newLevel), AbilityProgression.HiddenSlot));
            }
            client.messageQueue.addRpcMessage(ServerPackets.BuildAbilityUpgraded(request.Ability, (UInt16)newLevel));
            client.FlushQueue();

            Output.WriteDebugLog("[ABILITY] Upgraded " + request.Ability + " to level " + newLevel + " for " + cost + " info");
        }
        
        public void ProcessAbility(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);
            UInt16 AbilityID = reader.ReadUInt16(1);
            UInt16 currentTargetViewId = reader.ReadUInt16(1);


            // load the ability name from a list to see if we match the right ability
            DataLoader AbilityLoader = DataLoader.getInstance();
            currentAbility = AbilityLoader.getAbilityByID(AbilityID);

            Output.WriteDebugLog("[ABILITY] " + AbilityID + " (" + currentAbility.getAbilityName() + ") on view " +
                                 currentTargetViewId);

            // The client sends a double-clicked ability twice in a row; count it once.
            ClientData data = Store.currentClient.playerData;
            double now = TimeUtils.GetSimTimeSeconds();
            if (data.lastAbilityId == AbilityID && now - data.lastAbilityTime < DuplicateAbilityWindowSeconds)
            {
                Output.WriteDebugLog("[ABILITY] Duplicate use ignored");
                return;
            }
            data.lastAbilityId = AbilityID;
            data.lastAbilityTime = now;

            // Double-clicking a consumable sends its ability with the item's bag slot.
            UInt16? itemSlot = ConsumableItems.ItemSlotOfAbilityRequest(packet);
            if (itemSlot.HasValue && new InventoryHandler().UseItem(itemSlot.Value, AbilityID))
            {
                return;
            }

            // The Code Writer's Compile (a guess: no capture shows it).
            if (AbilityID == CraftingHandler.CompileActivateAbility && new CraftingHandler().HandleCompileRequest(packet))
            {
                return;
            }

            if (AbilityCooldowns.IsCoolingDown(data, AbilityID, now))
            {
                Output.WriteDebugLog("[ABILITY] Still cooling down; denied");
                Store.currentClient.messageQueue.addRpcMessage(
                    AbilityCooldowns.BuildDenied(AbilityID, AbilityCooldowns.DeniedTimerRunning));
                Store.currentClient.FlushQueue();
                return;
            }

            // Evade Combat used while it is on switches it off (its cooldown starts then); no cost, no cast.
            if (AbilityBuffs.IsToggledOn(Store.currentClient, AbilityID))
            {
                AbilityBuffs.Remove(Store.currentClient, AbilityID);
                Store.currentClient.FlushQueue();
                return;
            }

            ProcessAbilityScript(this.currentAbility);

            // When the cast finishes (right away for instant abilities): heals land and, as in retail, the
            // cooldown starts and buffs go on.
            WorldClient client = Store.currentClient;
            AbilityItem ability = currentAbility;
            bool selfTarget = currentTargetViewId <= 2;
            DelayedActions.AfterFor(client, ability.getCastingTime(), () =>
            {
                if (selfTarget && IsSelfHeal(ability))
                {
                    HealSelf(client, HealAmount(client));
                }
                if (AbilityBuffs.CooldownStartsOnUse(ability.getAbilityID()))
                {
                    AbilityCooldowns.Start(client, ability.getAbilityID(), TimeUtils.GetSimTimeSeconds());
                }
                if (AbilityBuffs.Apply(client, ability.getAbilityID()))
                {
                    client.FlushQueue();
                }
            });

            // Attack abilities on the current close-combat target hit it (buffs used with a target selected don't).
            if (currentTargetViewId > 2 && !currentAbility.getIsBuff() && !AbilityBuffs.IsBuff(AbilityID))
            {
                // When the cast ends it lands on that NPC (any NPC in view, starting a fight if there is none):
                // the retail damage and effect, stuns and damage over time (AbilityTargets). It used to land at once,
                // and only on the close-combat target.
                UInt16 targetView = currentTargetViewId;
                // An instant ability on the current close-combat target is the player's next move in the fight, as in
                // retail (RPC 47 kind 6 at once, the 45 of the next round names it); it lands with that round.
                if (ability.getCastingTime() > 0 ||
                    !CombatManager.GetHandler(client).QueueRoundAbility(ability, targetView))
                {
                    DelayedActions.AfterFor(client, ability.getCastingTime(),
                        () => AbilityTargets.Land(client, ability, targetView));
                }
            }
        }

        public const double DuplicateAbilityWindowSeconds = 0.5;

        /// <summary>Restore Health / Repair RSI and Emergency Repairs heal the caster.</summary>
        public static bool IsSelfHeal(AbilityItem ability)
        {
            string name = ability.getAbilityName() ?? "";
            return name.StartsWith("RestoreHealth") || name.StartsWith("EmergencyRepairs");
        }

        /// <summary>
        /// How much a heal restores. The data files have no amounts for these abilities, so it is a quarter of
        /// the caster's maximum health (a placeholder until a retail capture shows real numbers).
        /// </summary>
        public const double HealFraction = 0.25;

        public static UInt16 HealAmount(WorldClient client)
        {
            UInt16 max = NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1);
            return (UInt16)Math.Max(1, Math.Round(max * HealFraction));
        }

        /// <summary>Raises the player's health (never above the maximum) and sends the own-view health update.</summary>
        public static UInt16 HealSelf(WorldClient client, UInt16 amount)
        {
            UInt16 max = NumericalUtils.ByteArrayToUint16(client.playerInstance.MaxHealth.getValue(), 1);
            UInt16 current = NumericalUtils.ByteArrayToUint16(client.playerInstance.Health.getValue(), 1);
            if (client.playerData.isDead)
            {
                return current;
            }

            UInt16 healed = (UInt16)Math.Min(max, current + amount);
            client.playerInstance.Health.setValue(healed);
            client.messageQueue.addObjectMessage(ServerPackets.BuildSelfHealthUpdate(healed), false);
            client.FlushQueue();
            Output.WriteDebugLog("[ABILITY] Healed " + current + " -> " + healed);
            return healed;
        }

        /// <summary>
        /// Damage of an attack ability. The data's ValueFrom/ValueTo are used when set; most combat abilities
        /// (Cheap Shot, Head Butt, ...) have none, so they hit for about twice a normal round (~30).
        /// </summary>
        public static UInt16 AbilityDamage(AbilityItem ability, Random random)
        {
            int from = ability.getValueFrom();
            int to = ability.getValueTo();
            if (from > 0 && to >= from)
            {
                return (UInt16)random.Next(from, to + 1);
            }
            return (UInt16)random.Next(50, 81);
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
        public static int RegenAmount(bool inFight, ref double carry)
        {
            double total = carry + (inFight ? InnerStrengthRegenPerTick * CombatCalculator.FIGHT_REGEN_FACTOR : InnerStrengthRegenPerTick);
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
            int amount = RegenAmount(CombatManager.GetHandler(client).isCombatRunning, ref client.playerData.innerStrengthRegenCarry);
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
            UInt16 innerStrength = SpendInnerStrength(Store.currentClient, AbilityCosts.Cost(abilityItem.getAbilityID()));

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
            else if (AbilityBuffs.Retail.TryGetValue(currentAbility.getAbilityID(), out AbilityBuffs.Buff buff) &&
                     buff.MovementScale > 0)
            {
                // Hyper-Sprint: its own speed (0.15) and effect; it used to get Hyper-Speed's.
                byte counter = (byte)Store.currentClient.playerData.assignSpawnIdCounter();
                Store.currentClient.messageQueue.addObjectMessage(
                    AbilityBuffs.BuildMovementBuffUpdate(buff, innerStrength, counter), false);
                Store.currentClient.FlushQueue();
            }
            else
            {
                pak.sendISCurrent(Store.currentClient, innerStrength);
            }

            if (currentAbility.getAbilityID() == 970)
            {
                // Spawn Code Write Window
                pak.SendCodeWriterWindow();
            }
        }

        public void abilityAnimateTheTarget(object e)
        {
            ServerPackets pak = new ServerPackets();
            UInt32 targetAnim = this.currentAbility.getActivationFX();
            if (targetAnim == 0)
            {
                targetAnim = currentAbility.getAbilityExecutionFX();
            }

            pak.SendCastAbilityOnEntityId(currentTargetViewId, targetAnim, 150);
        }

        public void processCharacterAnimationSelf(UInt16 abilityID)
        {
            ServerPackets pak = new ServerPackets();
            // 2904 0429 = Hacker_VirusLaunch_A
            // 2a04 042a = Hacker_VirusLaunch_D
            // see movementAnims.tx - its for codes something (0x31)
            if (currentAbility.getAbilityExecutionFX() > 0)
            {
                pak.SendCastAbilityOnEntityId(2, currentAbility.getAbilityExecutionFX(), 200);
            }

            if (currentAbility.getCastingTime() > 0)
            {
                byte[] castAnimStart = currentAbility.getCastAnimStart();
                // Cast
                pak.sendSystemChatMessage(Store.currentClient,
                    "Animation Starts with Byte ID " + StringUtils.bytesToString(castAnimStart), "BROADCAST");
                pak.sendPlayerAnimation(Store.currentClient, StringUtils.bytesToString_NS(castAnimStart));

                // And Time a "Damage" or "Buff" Animation
                int castingTime = (int) currentAbility.getCastingTime() * 1000;
                currentTargetViewId = Store.currentClient.playerData.currentSelectedTargetViewId;
                // On the shared timer with the caster as current client: a plain Timer ran this with no current
                // client, threw, and took the whole server down at the end of every cast.
                DelayedActions.AfterFor(Store.currentClient, castingTime / 1000.0, () => abilityAnimateTheTarget(null));
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
            double xDest = 0;
            double yDest = 0;
            double zDest = 0;
            PacketReader reader = new PacketReader(rpcData);
            xDest = reader.ReadDouble(1);
            yDest = reader.ReadDouble(1);
            zDest = reader.ReadDouble(1);
            
            byte[] unknownJumpBytes = reader.ReadBytes(6);
            UInt32 peakHeight = reader.ReadUInt32(1);
            uint maybeFlag = reader.ReadUint8(); // is it everytime 01 ? maybe must check 
            
            UInt32 jumpId = reader.ReadUInt32(1); // JumpId

            // Players current X Z Y
            double x = 0;
            double y = 0;
            double z = 0;
            byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            int rotation = (int) Store.currentClient.playerInstance.YawInterval.getValue()[0];
            float xPos = (float) x;
            float yPos = (float) y;
            float zPos = (float) z;

            // ToDo: maybe remove or improve (i am not sure how this is written)
            //HyperJumpCalculatedStepMovement(xPos, yPos, zPos, xDest, yDest, zDest, clientJumpIdUnknown, maybeMaxHeight);

            // JumpEndTime is server sim time in milliseconds (retail: the 0x82 packet sim time x 1000 plus
            // ~9 s for a hyperjump). This used to be Unix seconds, decades ahead of the sim clock, so the
            // client stayed in the air for good and stopped sending movement.
            float peak = BitConverter.ToSingle(BitConverter.GetBytes(peakHeight), 0);
            UInt32 endTime = JumpEndTime(TimeUtils.GetSimTimeSeconds(), peak);

            ServerPackets packets = new ServerPackets();
            packets.SendHyperJumpID(jumpId);
            packets.SendHyperJumpWithOneStep((float)xDest,(float)yDest,(float)zDest, peakHeight, endTime);
            Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(xDest, yDest, zDest));
        }

        /// <summary>Gravity in world units (centimetres) per second squared.</summary>
        private const double Gravity = 981.0;

        /// <summary>
        /// Jump end time in sim milliseconds: up to the peak height (a float the client sends, world units)
        /// and back down, 2·sqrt(2h/g), at least 0.5 s. A retail hyperjump with peak 10300 lasted 9.9 s;
        /// this gives 9.2 s.
        /// </summary>
        public static UInt32 JumpEndTime(float simTimeSeconds, float peakHeight)
        {
            double h = Math.Max(0.0, double.IsFinite(peakHeight) ? peakHeight : 0.0);
            double seconds = Math.Max(0.5, 2.0 * Math.Sqrt(2.0 * h / Gravity));
            return (UInt32)Math.Round((simTimeSeconds + seconds) * 1000.0);
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
            double xDest = 0;
            double yDest = 0;
            double zDest = 0;
            PacketReader reader = new PacketReader(rpcData);
            xDest = reader.ReadDouble(1);
            yDest = reader.ReadDouble(1);
            zDest = reader.ReadDouble(1);

            // ToDo: figure out what this 6 bytes are could be
            // Skip 6 bytes as we currently didnt knew
            UInt32 clientJumpIdUnknown = reader.ReadUInt32(1);

            // Players current X Z Y
            double x = 0;
            double y = 0;
            double z = 0;
            byte[] Ltvector3d = Store.currentClient.playerInstance.Position.getValue();
            NumericalUtils.LtVector3dToDoubles(Ltvector3d, ref x, ref y, ref z);
            int rotation = (int) Store.currentClient.playerInstance.YawInterval.getValue()[0];
            float xPos = (float) x;
            float yPos = (float) y;
            float zPos = (float) z;

            // Start/end are server sim time in ms, like JumpEndTime (these used to be Unix seconds). The fall to
            // the new destination is timed as a drop from the current height above it.
            float simSeconds = TimeUtils.GetSimTimeSeconds();
            UInt32 startTime = (UInt32)Math.Round(simSeconds * 1000.0);
            // A straight drop of H takes sqrt(2H/g), which is JumpEndTime's up-and-down time for a peak of H/4.
            UInt32 endTime = JumpEndTime(simSeconds, (float)Math.Max(0.0, yPos - yDest) / 4f);

            ServerPackets packets = new ServerPackets();
            packets.SendHyperJumpID(clientJumpIdUnknown);
            packets.SendHyperJumpUpdate(xPos, yPos, zPos, (float) xDest, (float) yDest, (float) zDest, startTime,
                endTime);
            Store.currentClient.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(xDest, yDest, zDest));
        }
    }
}