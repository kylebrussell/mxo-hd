using System;
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
        /// <summary>
        /// Active combat session for this handler instance.
        /// </summary>
        private CombatSession currentSession;

        /// <summary>
        /// Legacy fields - kept for backwards compatibility during transition.
        /// TODO: Remove once CombatSession is fully integrated.
        /// </summary>
        public Timer combatTimer;
        public bool isCombatRunning = true;
        public UInt16 ilCombatViewId = 0;

        public void ProcessChangeTactic(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);
            string hexString = StringUtils.bytesToString_NS(packet);
            uint state = reader.ReadUint8();

            // TODO: Implement tactic changes - should affect damage/defense calculations
            // Tactic values and their effects are unknown - needs research
            Output.WriteDebugLog($"[COMBAT] Tactic change requested: state={state}");

            if (isCombatRunning && combatTimer != null)
            {
                combatTimer.Stop();
                combatTimer.Dispose();
            }
        }

        public void ProcessRequestCloseCombat(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);
            UInt32 targetViewWithSpawnId = reader.ReadUInt32(1);

            Output.WriteDebugLog($"[COMBAT] Close combat requested against target: {targetViewWithSpawnId}");

            // TODO: Look up the target Mob from the view ID
            // For now, create session without mob reference
            Mob targetMob = FindMobByViewSpawnId(targetViewWithSpawnId);

            // Create combat session
            currentSession = new CombatSession(
                Store.currentClient,
                targetMob,
                targetViewWithSpawnId,
                CombatSession.CombatType.Melee
            );

            // Subscribe to combat events
            currentSession.OnCombatTick += HandleCombatTick;
            currentSession.OnCombatEnd += HandleCombatEnd;

            // Send combat mode packet to player
            ServerPackets packets = new ServerPackets();
            packets.SendEnterCombatMode(Store.currentClient);

            // Spawn the ILCombatHandler game object
            var ilCombatHandler = new GameObjectDefinitions().Object55;
            ilCombatHandler.DisableAllAttributes();
            ilCombatHandler.StartTime.enable();
            ilCombatHandler.Position.enable();

            ilCombatHandler.StartTime.setValue(TimeUtils.getCurrentSimTime());
            ilCombatHandler.Position.setValue(Store.currentClient.playerInstance.Position.getValue());

            UInt64 currentEntityId = WorldServer.entityIdCounter;
            WorldServer.entityIdCounter++;
            WorldServer.gameServerEntities.Add(ilCombatHandler);

            packets.SendSpawnGameObject(Store.currentClient, ilCombatHandler, currentEntityId);

            ClientView theView = Store.currentClient.viewMan.GetViewForEntityAndGo(currentEntityId,
                NumericalUtils.ByteArrayToUint16(ilCombatHandler.GetGoid(), 1));

            // Store view IDs in session
            currentSession.CombatHandlerViewId = theView.ViewID;
            currentSession.CombatHandlerEntityId = currentEntityId;
            ilCombatViewId = theView.ViewID; // Legacy field

            // Send combat initialization packet
            packets.SendCombatInitialize(Store.currentClient, theView.ViewID, targetViewWithSpawnId);

            // Send the combat state data (still using hardcoded blob until we understand it better)
            SendCombatStateBlob(Store.currentClient, theView.ViewID);

            Store.currentClient.FlushQueue();

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
            ClientView targetView = Store.currentClient.viewMan.getViewById(viewId);
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

            Output.WriteDebugLog($"[COMBAT] Tick - Round {session.RoundNumber}");

            // Send position update packet
            SendCombatUpdatePacket(session);

            // Apply damage to defender if we have a valid target
            if (session.DefenderMob != null)
            {
                // Get player level (stored as byte array, typically single byte)
                byte[] levelBytes = session.Attacker.playerInstance.Level.getValue();
                UInt16 playerLevel = levelBytes != null && levelBytes.Length > 0 ? levelBytes[0] : (UInt16)1;
                UInt16 mobLevel = session.DefenderMob.getLevel();

                // Calculate damage based on combat type
                UInt16 damage;
                uint fxId;
                if (session.Type == CombatSession.CombatType.Melee)
                {
                    damage = CombatCalculator.CalculateMeleeDamage(playerLevel, mobLevel);
                    fxId = CombatCalculator.GetRandomMeleeHitFx();
                }
                else
                {
                    damage = CombatCalculator.CalculateRangedDamage(playerLevel, mobLevel);
                    fxId = CombatCalculator.GetRandomRangedHitFx();
                }

                Output.WriteDebugLog($"[COMBAT] Player (Lv{playerLevel}) hits {session.DefenderMob.getName()} (Lv{mobLevel}) for {damage} damage");

                // Apply damage to mob - this broadcasts to all nearby clients
                bool defenderDied = session.ApplyDamageToDefender(damage, fxId);

                if (defenderDied)
                {
                    Output.WriteDebugLog($"[COMBAT] {session.DefenderMob.getName()} died!");
                    session.Stop(CombatSession.CombatEndReason.DefenderDied);
                    return;
                }

                // TODO Phase 3: Mob attacks back here
                // session.DefenderMob.updateCombat() or similar
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
        /// Sends combat update packet on timer tick.
        /// </summary>
        private void SendCombatUpdatePacket(CombatSession session)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(session.CombatHandlerViewId, 1);
            pak.AddUShort(3); // Position update flag
            pak.AddByteArray(Store.currentClient.playerInstance.Position.getValue());

            // COMBAT UPDATE BLOB - Still hardcoded until structure is understood
            // See CombatHandler documentation for byte breakdown
            pak.AddHexBytes("030000000000000001020100000000002869C00000000000CF8B42002869400000000000CF8BC2000003006E290000A6009C0FF60E0200000000000000000000000000000000E54E00008B0B002400000000000000000000000000000000350000005200000000100010000000000000000000000000000000000000000000000000010000005864EBD9C000000000004491C000000040AF08EA400000");

            Store.currentClient.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            Store.currentClient.FlushQueue();
        }

        /// <summary>
        /// Called when combat ends for any reason.
        /// </summary>
        private void HandleCombatEnd(CombatSession session, CombatSession.CombatEndReason reason)
        {
            Output.WriteDebugLog($"[COMBAT] Combat ended: {reason}");

            isCombatRunning = false;

            ServerPackets packets = new ServerPackets();
            packets.SendCombatEnd(Store.currentClient, session.CombatHandlerViewId);

            // If defender died, trigger death/loot sequence
            if (reason == CombatSession.CombatEndReason.DefenderDied && session.DefenderMob != null)
            {
                // Mark the mob as dead and lootable
                session.MarkDefenderDead();

                // Send death animation using the mob's view ID (not combat handler view ID)
                UInt16 mobViewId = session.GetDefenderViewId();
                Output.WriteDebugLog($"[COMBAT] Sending death for mob view {mobViewId}");
                packets.SendNpcDies(mobViewId, Store.currentClient, session.DefenderMob);

                // Award experience
                UInt16 mobLevel = session.DefenderMob.getLevel();
                UInt32 expGained = (UInt32)(mobLevel * 100); // Simple exp formula
                Output.WriteDebugLog($"[COMBAT] Awarded {expGained} experience");
                // TODO: Actually add exp to player via Store.currentClient.playerData or similar

                // TODO: Trigger loot window
                // packets.SendLootWindow(...);
            }

            Store.currentClient.FlushQueue();
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
        /// Handle ranged combat (guns) request from client.
        /// </summary>
        public void ProcessRangeCombatRequest(ref byte[] packet)
        {
            PacketReader reader = new PacketReader(packet);
            UInt32 targetViewWithSpawnId = reader.ReadUInt32(1);

            Output.WriteDebugLog($"[COMBAT] Range combat requested against target: {targetViewWithSpawnId}");

            // TODO: Implement ranged combat properly
            // For now, could fall back to melee combat
            // ProcessRequestCloseCombat(ref packet);
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
                packets.SendCombatEnd(Store.currentClient, ilCombatViewId);
                Store.currentClient.FlushQueue();
            }
        }
    }
}
