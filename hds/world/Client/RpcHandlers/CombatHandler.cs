using System;
using System.Collections.Generic;
using System.Timers;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Handles combat-related RPC requests from clients.
    ///
    /// COMBAT SYSTEM STATUS: Phase 1 complete - structured with CombatSession tracking.
    /// See docs/COMBAT-SYSTEM-ROADMAP.md for improvement plan.
    ///
    /// Combat flow:
    /// 1. Client sends CLIENT_CLOSE_COMBAT (0x40) with target view ID
    /// 2. Server creates CombatSession and spawns ILCombatHandler (Object55)
    /// 3. Server sends combat state updates on timer via CombatSession
    /// 4. Combat ends via CLIENT_LEAVE_COMBAT (0x44) or death
    /// </summary>
    public class CombatHandler
    {
        private readonly WorldClient client;

        /// <summary>
        /// Active combat session for this handler instance.
        /// </summary>
        private CombatSession currentSession;

        /// <summary>
        /// Legacy fields - kept for backwards compatibility during transition.
        /// TODO: Remove once CombatSession is fully integrated.
        /// </summary>
        public Timer combatTimer;
        public bool isCombatRunning = false;
        public UInt16 ilCombatViewId = 0;

        public CombatHandler(WorldClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public void StopCombatIfActive(CombatSession.CombatEndReason reason)
        {
            if (currentSession != null && currentSession.IsActive)
            {
                currentSession.Stop(reason);
            }
        }

        /// <summary>
        /// Drops the fight without the combat teardown packets, for a world reload (hardline teleport, Loading Area,
        /// jack-out): the client has already dropped every view, so the teardown would address views it no longer
        /// has, and an NPC that aggroed must not keep hitting the player in the next world.
        /// </summary>
        public void AbandonCombat()
        {
            CombatSession session = currentSession;
            if (session == null)
            {
                isCombatRunning = false;
                return;
            }

            Mob defender = session.DefenderMob;
            session.OnCombatEnd -= HandleCombatEnd;
            session.OnCombatTick -= HandleCombatTick;
            session.Stop(CombatSession.CombatEndReason.AttackerFled);
            defender?.clearCombatTarget();
            RemoveCombatHandlerEntity(session);
            isCombatRunning = false;
            currentSession = null;
            client.playerData.combatGeneration++; // the pending leave-combat reset belongs to the old world
            Output.WriteDebugLog("[COMBAT] Fight dropped: the player's world reloaded");
        }

        /// <summary>CombatantMode with a weapon drawn / ready to fight (retail).</summary>
        public const byte CombatantModeReady = 0x21;

        /// <summary>CombatantMode at rest (retail; also what the spawn sends).</summary>
        public const byte CombatantModeIdle = 0x22;

        /// <summary>
        /// RPC 0x3d: the client asks to draw its weapon (01) or stand down (00), e.g. before firing at a
        /// target. Retail answered with the own-view CombatantMode (0x21 / 0x22); without an answer the
        /// client gave up (it sent 00 shortly after) and never sent its ranged attack.
        /// </summary>
        public void ProcessCombatStance(ref byte[] packet)
        {
            byte mode = packet.Length > 0 && packet[0] != 0 ? CombatantModeReady : CombatantModeIdle;
            client.playerInstance.CombatantMode.setValue(mode);
            client.messageQueue.addObjectMessage(ServerPackets.BuildCombatantModeUpdate(mode), false);
            client.FlushQueue();
            Output.WriteDebugLog($"[COMBAT] Combat stance {(mode == CombatantModeReady ? "ready" : "idle")}");
        }

        public void ProcessChangeTactic(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);
            uint state = reader.ReadUint8();

            // Map packet state to CombatTactic enum
            // Based on research: 0=None, 1=Speed, 2=Power, 3=Grab/Block
            CombatTactic newTactic = (CombatTactic)Math.Min(state, 3);

            Output.WriteDebugLog($"[COMBAT] Tactic change requested: state={state} -> {newTactic}");

            if (currentSession != null && currentSession.IsActive)
            {
                // Try to change tactic (costs IS)
                bool success = currentSession.SetAttackerTactic(newTactic);

                if (success)
                {
                    Output.WriteDebugLog($"[COMBAT] Tactic changed to {newTactic}");

                    // Send IS update to client
                    ServerPackets packets = new ServerPackets();
                    byte[] isBytes = client.playerInstance.InnerStrengthAvailable.getValue();
                    UInt16 currentIS = isBytes != null && isBytes.Length >= 2
                        ? NumericalUtils.ByteArrayToUint16(isBytes, 1)
                        : (UInt16)0;
                    packets.sendISCurrent(client, currentIS);

                    // Send tactic confirmation (using the system chat for now)
                    string tacticName = newTactic == CombatTactic.None ? "Balanced" : newTactic.ToString();
                    packets.sendSystemChatMessage(client,
                        $"Combat tactic: {tacticName}", "SYSTEM");
                }
                else
                {
                    Output.WriteDebugLog("[COMBAT] Tactic change failed - insufficient IS");
                    ServerPackets packets = new ServerPackets();
                    packets.sendSystemChatMessage(client,
                        "Not enough Inner Strength to change tactics!", "SYSTEM");
                }
            }
            else
            {
                Output.WriteDebugLog("[COMBAT] Tactic change ignored - not in combat");
            }
        }

        /// <summary>
        /// ILCombatHandler StartTime: u32 sim time in milliseconds. Retail: 55 d3 9a 03 = 60,478,293 ms while its
        /// packet clock read 60,479.35 s. This used to send the float-seconds bytes instead (e.g. 11.1 s read as
        /// ~1.09e9 ms, days in the client's future), so the client never saw its close-combat fight end and kept
        /// the player locked in the fight stance afterwards.
        /// </summary>
        public static UInt32 CombatStartTime()
        {
            return (UInt32)Math.Round(TimeUtils.GetSimTimeSeconds() * 1000.0);
        }
        /// <summary>40 &lt;target u32&gt; 00 &lt;rest of the request&gt;: the server's answer to a 0x40 close-combat request.</summary>
        public static byte[] BuildCloseCombatEcho(byte[] request)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x40);
            for (int i = 0; i < request.Length; i++)
            {
                pak.AddByte(i == 4 ? (byte)0x00 : request[i]);
            }
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Retail's ability hit on an NPC: Health together with EffectID 0x280001b7 and its counter
        /// (04 80 80 80 c0 75 03 c0 b7 01 00 28 01 01). Used when an ability has no effect of its own.
        /// </summary>
        public const UInt32 DefaultAbilityHitFx = 0x280001b7;

        /// <summary>
        /// An attack ability used on the player's current close-combat target hits it. Returns false when the
        /// target isn't the one being fought (the ability then only costs Inner Strength, as before).
        /// </summary>
        public bool ApplyAbilityToTarget(UInt16 targetViewId, UInt16 damage, UInt32 fxId)
        {
            CombatSession session = currentSession;
            if (session == null || !session.IsActive || session.DefenderMob == null ||
                session.GetDefenderViewId() != targetViewId)
            {
                return false;
            }

            Output.WriteDebugLog($"[COMBAT] Ability hits {session.DefenderMob.getName()} for {damage}");
            bool died = session.ApplyDamageToDefender(damage, fxId == 0 ? DefaultAbilityHitFx : fxId);
            client.FlushQueue();
            if (died)
            {
                Output.WriteDebugLog($"[COMBAT] {session.DefenderMob.getName()} died from an ability!");
                session.Stop(CombatSession.CombatEndReason.DefenderDied);
            }
            return true;
        }

        public void ProcessRequestCloseCombat(ref byte[] packet)
        {
#if DEBUG
            Output.WriteRpcLog($"[COMBAT] Close combat rpc len={packet.Length} data={StringUtils.bytesToString_NS(packet)}");
#endif
            PacketReader reader = new PacketReader(packet);
            UInt32 targetViewWithSpawnId = reader.ReadUInt32(1);

            Output.WriteDebugLog($"[COMBAT] Close combat requested against target: {targetViewWithSpawnId}");

            // A 41 (fire) comes with a 40 for the same target; retail didn't answer that 40, and starting close
            // combat here would end the ranged fight the 41 just started.
            if (IsRangedFightWith(targetViewWithSpawnId))
            {
                Output.WriteDebugLog("[COMBAT] Close combat request ignored: firing at this target");
                return;
            }

            // TODO: Look up the target Mob from the view ID
            // For now, create session without mob reference
            Mob targetMob = FindMobByViewSpawnId(targetViewWithSpawnId);

            if (targetMob == null)
            {
                Output.WriteDebugLog("[COMBAT] Close combat aborted - target mob not found");
                return;
            }

            StopCombatIfActive(CombatSession.CombatEndReason.AttackerFled);

            // Retail echoed the request with its flag byte cleared: client 40 2f 00 22 00 01 6b -> server
            // 40 2f 00 22 00 00 6b. Without it the client never confirmed the fight on its side.
            client.messageQueue.addRpcMessage(BuildCloseCombatEcho(packet));

            // Create combat session
            currentSession = new CombatSession(
                client,
                targetMob,
                targetViewWithSpawnId,
                CombatSession.CombatType.Melee
            );

            // Set this player as the mob's combat target so mob can attack back
            if (targetMob != null)
            {
                targetMob.setCombatTarget(client);
            }

            // Subscribe to combat events
            currentSession.OnCombatTick += HandleCombatTick;
            currentSession.OnCombatEnd += HandleCombatEnd;

            // Send combat mode packet to player
            ServerPackets packets = new ServerPackets();
            packets.SendEnterCombatMode(client);

            // Spawn the ILCombatHandler game object
            var ilCombatHandler = new GameObjectDefinitions().Object55;
            ilCombatHandler.DisableAllAttributes();
            ilCombatHandler.StartTime.enable();
            ilCombatHandler.Position.enable();

            ilCombatHandler.StartTime.setValue(CombatStartTime());
            ilCombatHandler.Position.setValue(client.playerInstance.Position.getValue());
            currentSession.CombatHandlerObject = ilCombatHandler;

            UInt64 currentEntityId = WorldServer.entityIdCounter;
            WorldServer.entityIdCounter++;
            WorldServer.gameServerEntities.Add(ilCombatHandler);

            packets.SendSpawnGameObject(client, ilCombatHandler, currentEntityId);

            ClientView theView = client.viewMan.GetViewForEntityAndGo(currentEntityId,
                NumericalUtils.ByteArrayToUint16(ilCombatHandler.GetGoid(), 1));

            // Store view IDs in session
            currentSession.CombatHandlerViewId = theView.ViewID;
            currentSession.CombatHandlerEntityId = currentEntityId;
            ilCombatViewId = theView.ViewID; // Legacy field

            // Send combat initialization packet
            packets.SendCombatInitialize(client, theView.ViewID, targetViewWithSpawnId);
            client.playerData.combatGeneration++;
            client.messageQueue.addRpcMessage(ServerPackets.BuildCombatTargetFlag(targetViewWithSpawnId, 0x01));

            // Send the combat state data (still using hardcoded blob until we understand it better)
            SendCombatStateBlob(client, theView.ViewID);

            client.FlushQueue();

            // Start combat session
            currentSession.Start(3000); // 3 second ticks
            isCombatRunning = true;
        }

        /// <summary>
        /// Finds a mob by its combined view+spawn ID from the client's perspective.
        /// The viewSpawnId is packed as: [spawnId:16][viewId:16]
        /// </summary>
        private Mob FindMobByViewSpawnId(UInt32 viewSpawnId)
        {
            // Extract view ID (lower 16 bits) and spawn ID (upper 16 bits)
            UInt16 viewId = (UInt16)(viewSpawnId & 0xFFFF);
            UInt16 spawnId = (UInt16)((viewSpawnId >> 16) & 0xFFFF);

            Output.WriteDebugLog($"[COMBAT] Looking for mob with viewId={viewId}, spawnId={spawnId}");

            // Look up the view from the client's view manager
            ClientView targetView = client.viewMan.getViewById(viewId);
            if (targetView == null)
            {
                Output.WriteDebugLog($"[COMBAT] No view found for viewId={viewId}");
                return null;
            }

            UInt64 entityId = targetView.entityId;
            Output.WriteDebugLog($"[COMBAT] Found view with entityId={entityId}");

            // Search WorldServer.mobs for the mob with this entityId
            lock (WorldServer.mobs.SyncRoot)
            {
                for (int i = 0; i < WorldServer.mobs.Count; i++)
                {
                    Mob mob = (Mob)WorldServer.mobs[i];
                    if (mob.getEntityId() == entityId)
                    {
                        Output.WriteDebugLog($"[COMBAT] Found mob: {mob.getName()} (HP: {mob.getHealthC()}/{mob.getHealthM()})");
                        return mob;
                    }
                }
            }

            Output.WriteDebugLog($"[COMBAT] No mob found with entityId={entityId}");
            return null;
        }

        /// <summary>
        /// Sends the complex combat state blob (still hardcoded until understood).
        /// </summary>
        private void SendCombatStateBlob(WorldClient client, UInt16 combatViewId)
        {
            PacketContent pak = new PacketContent();

            // COMBAT DATA BLOB - Captured from original game, structure partially known:
            // See detailed comments in docs/COMBAT-SYSTEM-ROADMAP.md
            //
            // TODO: Replace with dynamically built packet using actual combat state
            pak.AddByteArray(StringUtils.hexStringToBytes(
                "0703070300bafc42000020c1801baf4200803e40000020c1e0b319430000010013010000f40134059a02233c5200008b0b0024145200008b0b0024262000008b0b00240000000000000000000000000000000021000000700000000010001000000000000000000000000000010000022b600000000000"));

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
        }

        /// <summary>
        /// Called by CombatSession on each combat tick.
        /// Calculates damage, applies it, and checks for death.
        /// </summary>
        private void HandleCombatTick(CombatSession session)
        {
            if (!isCombatRunning || session == null) return;

            Output.WriteDebugLog($"[COMBAT] Tick - Round {session.RoundNumber}, Tactic: {session.AttackerTactic}");

            // Drain IS for combat round
            DrainCombatIS(session.Attacker);

            // No per-round update to the combat handler view: retail sends its state once, at combat start
            // (see docs/COMBAT-SYSTEM-ROADMAP.md). The old hard-coded update here didn't match the object's
            // layout, and the client dropped the rest of the packet with it, including the health updates.

            // Apply damage to defender if we have a valid target
            if (session.DefenderMob != null)
            {
                // Get player level (stored as byte array, typically single byte)
                byte[] levelBytes = session.Attacker.playerInstance.Level.getValue();
                UInt16 playerLevel = levelBytes != null && levelBytes.Length > 0 ? levelBytes[0] : (UInt16)1;
                UInt16 mobLevel = session.DefenderMob.getLevel();

                bool defenderDied;
                if (session.Type == CombatSession.CombatType.Melee)
                {
                    UInt16 damage = CombatCalculator.CalculateMeleeDamageWithTactics(
                        playerLevel, mobLevel,
                        session.AttackerTactic, session.DefenderTactic);
                    Output.WriteDebugLog($"[COMBAT] Player (Lv{playerLevel}, {session.AttackerTactic}) hits {session.DefenderMob.getName()} (Lv{mobLevel}) for {damage} damage");
                    // Health-only hit update, as retail sent for plain attacks; the FX.cs hit ids
                    // aren't valid effect ids (see CombatCalculator.MeleeHitFxIds).
                    defenderDied = session.ApplyDamageToDefender(damage, 0);
                }
                else
                {
                    if (!TargetInGunRange(session))
                    {
                        // Out of the gun's range: no shot. Retail's client only fires within range (Tool/Range); the
                        // fight ends when the target stays out of reach (our choice: three shots' time).
                        rangedOutOfRangeTicks++;
                        Output.WriteDebugLog($"[COMBAT] Target out of range ({rangedOutOfRangeTicks})");
                        if (rangedOutOfRangeTicks >= MaxOutOfRangeTicks)
                        {
                            session.Stop(CombatSession.CombatEndReason.AttackerFled);
                        }
                        return;
                    }
                    rangedOutOfRangeTicks = 0;
                    defenderDied = ResolveRangedShot(session, false);
                }

                if (defenderDied)
                {
                    Output.WriteDebugLog($"[COMBAT] {session.DefenderMob.getName()} died!");
                    session.Stop(CombatSession.CombatEndReason.DefenderDied);
                    return;
                }

                // Phase 3: Mob attacks the player back (with player's defensive tactic applied). An NPC with a gun
                // shoots back in a gunfight; others keep the close-combat counterattack.
                bool playerDied = session.Type == CombatSession.CombatType.Ranged &&
                                  RangedWeapons.IsGun(session.DefenderMob.getWeapon())
                    ? NpcReturnFire(session)
                    : session.DefenderMob.updateCombat(session.AttackerTactic, session.Attacker);
                session.Attacker.FlushQueue();
                if (playerDied)
                {
                    Output.WriteDebugLog("[COMBAT] Player died in combat!");
                    session.Stop(CombatSession.CombatEndReason.AttackerDied);
                    HandlePlayerDeath(session.Attacker);
                    return;
                }
            }
            else
            {
                // No valid mob target - check if defender died anyway (shouldn't happen)
                if (session.IsDefenderDead())
                {
                    Output.WriteDebugLog("[COMBAT] Defender died!");
                    session.Stop(CombatSession.CombatEndReason.DefenderDied);
                }
            }
        }

        /// <summary>
        /// Drains Inner Strength for combat round.
        /// </summary>
        private void DrainCombatIS(WorldClient player)
        {
            byte[] isBytes = player.playerInstance.InnerStrengthAvailable.getValue();
            if (isBytes == null || isBytes.Length < 2) return;

            UInt16 currentIS = NumericalUtils.ByteArrayToUint16(isBytes, 1);
            if (currentIS > 0)
            {
                int cost = CombatCalculator.COMBAT_ROUND_IS_COST;
                UInt16 newIS = (UInt16)Math.Max(0, currentIS - cost);
                player.playerInstance.InnerStrengthAvailable.setValue(newIS);

                // Send IS update to client
                ServerPackets packets = new ServerPackets();
                packets.sendISCurrent(player, newIS);
            }
        }

        /// <summary>
        /// Called when combat ends for any reason.
        /// </summary>
        private void HandleCombatEnd(CombatSession session, CombatSession.CombatEndReason reason)
        {
            Output.WriteDebugLog($"[COMBAT] Combat ended: {reason}");

            isCombatRunning = false;

            // Clear the mob's combat target
            if (session.DefenderMob != null)
            {
                session.DefenderMob.clearCombatTarget();
            }

            ServerPackets packets = new ServerPackets();
            bool defenderDied = reason == CombatSession.CombatEndReason.DefenderDied && session.DefenderMob != null;

            // The death goes out first, in its own packet, so nothing sent with the combat teardown can
            // keep the client from seeing it.
            if (defenderDied)
            {
                session.MarkDefenderDead();
                UInt16 mobViewId = session.GetDefenderViewId();
                Output.WriteDebugLog($"[COMBAT] Sending death for mob view {mobViewId}");
                packets.SendNpcDies(mobViewId, session.Attacker, session.DefenderMob);
            }

            if (session.Type == CombatSession.CombatType.Ranged)
            {
                // No combat view to tear down. The client stands down by itself (3d 00, answered in
                // ProcessCombatStance); the stance and combat flag reset follows as after close combat.
                packets.ScheduleLeaveCombatMode(session.Attacker);
            }
            else
            {
                byte[] combatCentre = null;
                if (session.CombatHandlerObject is AttributeClass3664 combatObject)
                {
                    combatCentre = combatObject.Position.getValue();
                }
                packets.SendCombatEnd(session.Attacker, session.CombatHandlerViewId, session.TargetViewSpawnId,
                    combatCentre, session.RoundNumber);
                RemoveCombatHandlerEntity(session);
            }

            // If defender died, trigger the experience/loot sequence
            if (defenderDied)
            {

                // Award experience
                UInt16 mobLevel = session.DefenderMob.getLevel();
                UInt32 expGained = (UInt32)(mobLevel * 100); // Simple exp formula
                PlayerRewards.GrantExperience(session.Attacker, expGained);
                Output.WriteDebugLog($"[COMBAT] Awarded {expGained} experience");

                MissionRuntime.OnMobDefeated(session.Attacker, session.DefenderMob);
                UInt32 missionItem = MissionRuntime.LootItemFor(session.Attacker.playerData, session.DefenderMob);

                // Calculate loot money based on mob level (50-150 per level)
                Random rand = new Random();
                UInt32 lootMoney = (UInt32)(mobLevel * (50 + rand.Next(100)));

                // Store pending loot for when player accepts
                session.Attacker.playerData.pendingLootMoney = lootMoney;
                session.Attacker.playerData.hasLootPending = true;

                // Loot window: the money, a mission's LOOT item when this mob carries one, and the mob's drops
                session.Attacker.playerData.pendingLootItem = missionItem;
                List<UInt32> drops = session.DefenderMob.missionInstance == 0
                    ? NpcLoot.Roll(mobLevel, rand)
                    : new List<UInt32>();
                lock (session.Attacker.playerData.pendingLootDrops)
                {
                    session.Attacker.playerData.pendingLootDrops.Clear();
                    session.Attacker.playerData.pendingLootDrops.AddRange(drops);
                }

                List<UInt32> lootItems = new List<UInt32>();
                List<byte> lootFlags = new List<byte>();
                if (missionItem != 0)
                {
                    lootItems.Add(missionItem);
                    lootFlags.Add(0x08);
                }
                foreach (UInt32 drop in drops)
                {
                    lootItems.Add(drop);
                    lootFlags.Add(0x00);
                }
                session.Attacker.messageQueue.addRpcMessage(NpcLoot.BuildLootWindow(lootMoney, lootItems, lootFlags));
                Output.WriteDebugLog($"[COMBAT] Loot window sent with {lootMoney} info and {lootItems.Count} items");
            }

            session.Attacker.FlushQueue();
            currentSession = null;
        }

        /// <summary>
        /// Legacy timer callback - forwards to HandleCombatTick.
        /// TODO: Remove once CombatSession timer is fully working.
        /// </summary>
        public void UpdateCloseCombat(Object source, ElapsedEventArgs e)
        {
            if (currentSession != null)
            {
                HandleCombatTick(currentSession);
            }
        }

        /// <summary>
        /// RPC 41 &lt;target view u16&gt; &lt;spawn u16&gt;: fire at a target with the hotbar's Free Fire weapon. The client
        /// sends it together with 3d 01 and a 40 for the same target. Retail answered with a shot at once and kept
        /// firing about every 5 s without further requests until the target died (tutorial capture, crashover2k
        /// ..._uriah.log.txt, 13:30:12-13:30:32). Unlike close combat there is no combat handler object, no 40 echo
        /// and no RPC 46.
        /// </summary>
        public void ProcessRangeCombatRequest(ref byte[] packet)
        {
#if DEBUG
            Output.WriteRpcLog($"[COMBAT] Range combat rpc len={packet.Length} data={StringUtils.bytesToString_NS(packet)}");
#endif
            PacketReader reader = new PacketReader(packet);
            UInt32 targetViewWithSpawnId = reader.ReadUInt32(1);

            Output.WriteDebugLog($"[COMBAT] Range combat requested against target: {targetViewWithSpawnId}");

            if (IsRangedFightWith(targetViewWithSpawnId))
            {
                Output.WriteDebugLog("[COMBAT] Already firing at this target");
                return;
            }

            Mob targetMob = FindMobByViewSpawnId(targetViewWithSpawnId);
            if (targetMob == null || targetMob.getIsDead())
            {
                Output.WriteDebugLog("[COMBAT] Range combat aborted - target mob not found or dead");
                return;
            }

            StopCombatIfActive(CombatSession.CombatEndReason.AttackerFled);

            currentSession = new CombatSession(
                client,
                targetMob,
                targetViewWithSpawnId,
                CombatSession.CombatType.Ranged
            );
            targetMob.setCombatTarget(client);

            currentSession.OnCombatTick += HandleCombatTick;
            currentSession.OnCombatEnd += HandleCombatEnd;

            client.playerData.combatGeneration++;
            client.playerInstance.CombatantMode.setValue(CombatantModeReady);
            rangedNoiseLevel = 0;
            rangedOutOfRangeTicks = 0;

            // First shot now, then one per tick.
            isCombatRunning = true;
            bool killed = ResolveRangedShot(currentSession, true);
            client.FlushQueue();

            currentSession.Start(RangedWeapons.FireIntervalMs(PlayerGun(client)));
            if (killed)
            {
                currentSession.Stop(CombatSession.CombatEndReason.DefenderDied);
            }
        }

        /// <summary>True while this player is firing at the given target (view u16 + spawn u16).</summary>
        private bool IsRangedFightWith(UInt32 targetViewSpawnId)
        {
            CombatSession session = currentSession;
            return session != null && session.IsActive && session.Type == CombatSession.CombatType.Ranged &&
                   session.TargetViewSpawnId == targetViewSpawnId;
        }

        /// <summary>NoiseLevel (own-view attribute 9): retail sent 0x32 on the first shot and more on later ones.</summary>
        private byte rangedNoiseLevel;

        private readonly Random rangedRng = new Random();

        /// <summary>
        /// One gunshot of the player at the session's target, in retail's order: the own-view shot (hit or miss)
        /// with RPC 4e 00, the NPC's health on a hit, then the 81 68 combat-log line. Returns true when the shot
        /// killed the target.
        /// </summary>
        private bool ResolveRangedShot(CombatSession session, bool firstShot)
        {
            WorldClient player = session.Attacker;
            Mob target = session.DefenderMob;
            UInt32 weapon = PlayerGun(player);
            rangedNoiseLevel = RangedWeapons.NextNoiseLevel(rangedNoiseLevel, RangedWeapons.Noise(weapon));

            UInt16 attackRoll, defenseRoll;
            bool hit = CombatCalculator.RollRangedHit(rangedRng, out attackRoll, out defenseRoll);

            byte attackClass = PlayerGunClass(player);

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(player.playerInstance.Position.getValue(), ref x, ref y, ref z);
            player.playerData.emoteCounter++;
            player.messageQueue.addObjectMessage(ServerPackets.BuildRangedShot(player.playerData.emoteCounter,
                session.TargetViewSpawnId, (float)x, (float)y, (float)z, CombatStartTime(), rangedNoiseLevel,
                firstShot, hit, attackClass), false);
            player.messageQueue.addRpcMessage(ServerPackets.BuildRangedShotRpc());

            if (target == null)
            {
                return false;
            }

            UInt16 damage = 0;
            bool died = false;
            if (hit)
            {
                byte[] levelBytes = player.playerInstance.Level.getValue();
                UInt16 playerLevel = levelBytes != null && levelBytes.Length > 0 ? levelBytes[0] : (UInt16)1;
                float distance = CombatCalculator.CalculateDistanceMetres(player.playerInstance.Position.getValue(),
                    target.getXPos(), target.getYPos(), target.getZPos());
                damage = CombatCalculator.CalculateRangedDamageWithTactics(playerLevel, target.getLevel(), distance,
                    session.AttackerTactic, session.DefenderTactic);
                damage = (UInt16)Math.Max(1, Math.Round(damage * RangedWeapons.DamageFactor(weapon)));
                died = session.ApplyDamageToDefender(damage, 0);
            }
            Output.WriteDebugLog($"[COMBAT] Shot at {target.getName()}: {(hit ? "hit for " + damage : "miss")} (roll {attackRoll} vs {defenseRoll})");

            string playerName = StringUtils.charBytesToString_NZ(player.playerInstance.CharacterName.getValue());
            player.messageQueue.addRpcMessage(ServerPackets.BuildCombatLog(hit, damage, 0, attackRoll, defenseRoll,
                true, weapon, target.getName(), playerName));
            return died;
        }

        /// <summary>Consecutive ticks the target was out of the gun's range.</summary>
        private int rangedOutOfRangeTicks;

        /// <summary>After this many ticks out of range, the ranged fight ends.</summary>
        public const int MaxOutOfRangeTicks = 3;

        /// <summary>True when the session's target is within the player's gun range (Tool/Range, metres).</summary>
        private bool TargetInGunRange(CombatSession session)
        {
            Mob target = session.DefenderMob;
            if (target == null)
            {
                return true;
            }
            float metres = CombatCalculator.CalculateDistanceMetres(session.Attacker.playerInstance.Position.getValue(),
                target.getXPos(), target.getYPos(), target.getZPos());
            return metres <= RangedWeapons.RangeMetres(PlayerGun(session.Attacker));
        }

        /// <summary>The gun the player fires: their Free Fire pick (RPC 3f), else the equipped item.</summary>
        private static UInt32 PlayerGun(WorldClient player)
        {
            UInt32 weapon = player.playerData.freeFireWeaponGoid;
            byte[] equipped = player.playerInstance.EquippedItemID.getValue();
            if (weapon == 0 && equipped != null && equipped.Length >= 4)
            {
                weapon = NumericalUtils.ByteArrayToUint32(equipped, 1);
            }
            return weapon;
        }

        /// <summary>Attack class of the player's gun; a pistol when it isn't known.</summary>
        private static byte PlayerGunClass(WorldClient player)
        {
            byte attackClass = RangedWeapons.AttackClass(PlayerGun(player));
            return attackClass == 0 ? RangedWeapons.ClassPistol : attackClass;
        }

        /// <summary>
        /// An NPC with a gun fires back at the player, as retail NPCs did in a gunfight (tutorial capture: Alex shot
        /// mxoemu every ~5 s with hits and misses; luizoe Unknown log line 32719: "Chopper Jumper" with a rifle):
        /// the NPC's shot on its view (<see cref="ServerPackets.BuildNpcRangedShot"/>), the player's health on a
        /// hit, then the 81 68 log line with the NPC as attacker. Returns true when the player died.
        /// </summary>
        private bool NpcReturnFire(CombatSession session)
        {
            WorldClient player = session.Attacker;
            Mob mob = session.DefenderMob;
            byte attackClass = RangedWeapons.AttackClass(mob.getWeapon());

            float metres = CombatCalculator.CalculateDistanceMetres(player.playerInstance.Position.getValue(),
                mob.getXPos(), mob.getYPos(), mob.getZPos());
            if (metres > RangedWeapons.RangeMetres(mob.getWeapon()))
            {
                Output.WriteDebugLog($"[COMBAT] {mob.getName()} can't reach the player ({metres:F1} m)");
                return false;
            }

            UInt16 attackRoll, defenseRoll;
            bool hit = CombatCalculator.RollRangedHit(rangedRng, out attackRoll, out defenseRoll);

            mob.rangedActionCounter++;
            ClientView mobView = player.viewMan.FindViewForEntity(mob.getEntityId());
            if (mobView != null && mobView.viewCreated)
            {
                UInt32 playerAsTarget = (UInt32)(2 | (player.playerData.selfSpawnIdCounter << 16));
                new ServerPackets().SendNpcUpdateData(mobView.ViewID, player, ServerPackets.BuildNpcRangedShot(
                    mob.rangedActionCounter, attackClass, hit, playerAsTarget,
                    (float)mob.getXPos(), (float)mob.getYPos(), (float)mob.getZPos()));
            }

            UInt16 damage = 0;
            bool died = false;
            if (hit)
            {
                byte[] levelBytes = player.playerInstance.Level.getValue();
                UInt16 playerLevel = levelBytes != null && levelBytes.Length > 0 ? levelBytes[0] : (UInt16)1;
                damage = CombatCalculator.CalculateMobDamageWithTactics(mob.getLevel(), playerLevel, session.AttackerTactic);
                damage = (UInt16)Math.Max(1, Math.Round(damage * RangedWeapons.DamageFactor(mob.getWeapon())));
                died = mob.HitCombatTarget(damage, 0, player);
            }
            Output.WriteDebugLog($"[COMBAT] {mob.getName()} fires back: {(hit ? "hit for " + damage : "miss")} (roll {attackRoll} vs {defenseRoll})");

            string playerName = StringUtils.charBytesToString_NZ(player.playerInstance.CharacterName.getValue());
            player.messageQueue.addRpcMessage(ServerPackets.BuildCombatLog(hit, damage, 0, attackRoll, defenseRoll,
                false, mob.getWeapon(), playerName, mob.getName()));
            return died;
        }

        /// <summary>
        /// RPC 3f &lt;inventory slot u16&gt; &lt;kind u32&gt;: the client's Free Fire weapon choice. Kind 0x10 is an inventory
        /// item; <c>3f 6b 00 00 00 00 00</c> (slot 0x6b, kind 0) is none, i.e. fists. Retail's client sent it when a
        /// gun arrived (tutorial capture, after loot: <c>3f 04 00 10 00 00 00</c>), when the picked gun moved, at
        /// world entry and from the hotbar dropdown. The server only remembers the gun for the combat log.
        /// </summary>
        public void ProcessFreeFireChoice(ref byte[] packet)
        {
            if (packet.Length < 6)
            {
                return;
            }
            UInt16 slot = (UInt16)(packet[0] | (packet[1] << 8));
            UInt32 kind = (UInt32)(packet[2] | (packet[3] << 8) | (packet[4] << 16) | (packet[5] << 24));
            UInt32 goid = kind == FreeFireKindItem ? Store.dbManager.WorldDbHandler.GetItemGOIDAtInventorySlot(slot) : 0;
            client.playerData.freeFireWeaponGoid = goid;
            Output.WriteDebugLog($"[COMBAT] Free Fire weapon: slot {slot}, kind {kind}, goid {goid}");
        }

        /// <summary>Kind value of a Free Fire choice that names an inventory item.</summary>
        public const UInt32 FreeFireKindItem = 0x10;

        /// <summary>
        /// Handles player death - sets death state and triggers respawn flow.
        /// </summary>
        public static void HandlePlayerDeath(WorldClient player)
        {
            Output.WriteDebugLog("[COMBAT] Processing player death");

            player.playerInstance.IsDead.setValue(true);
            player.playerInstance.Health.setValue((UInt16)0);
            player.playerData.isDead = true;
            player.playerData.respawnPending = false;

            // Retail death: own view IsDead/Health/ConditionStateFlags, then the death system message. The client
            // shows its own death UI and asks to respawn with 81 4c (TeleportHandler.ProcessRespawnRequest).
            player.messageQueue.addObjectMessage(ServerPackets.BuildSelfDeathUpdate(), false);
            AbilityBuffs.RemoveAll(player);
            player.messageQueue.addRpcMessage(ServerPackets.BuildDeathMessage());
            player.FlushQueue();

            Output.WriteDebugLog("[COMBAT] Player died; waiting for the respawn request");
        }

        private void RemoveCombatHandlerEntity(CombatSession session)
        {
            if (session == null || session.CombatHandlerObject == null)
            {
                return;
            }

            lock (WorldServer.gameServerEntities)
            {
                WorldServer.gameServerEntities.Remove(session.CombatHandlerObject);
            }
        }

        /// <summary>
        /// Handle player leaving combat (fleeing or disengaging).
        /// </summary>
        public void ProcessLeaveCloseCombat(ref byte[] rpcData)
        {
            Output.WriteDebugLog("[COMBAT] Leave combat requested");

            if (currentSession != null && currentSession.IsActive)
            {
                currentSession.Stop(CombatSession.CombatEndReason.AttackerFled);
            }
            else if (isCombatRunning)
            {
                // Legacy fallback
                isCombatRunning = false;
                combatTimer?.Stop();
                combatTimer?.Dispose();

                ServerPackets packets = new ServerPackets();
                packets.SendCombatEnd(client, ilCombatViewId, 0);
                client.FlushQueue();
            }
        }
    }
}
