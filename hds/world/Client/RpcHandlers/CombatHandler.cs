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
        /// Attempts to find a mob by its combined view+spawn ID.
        /// TODO: Implement proper lookup in world's mob list.
        /// </summary>
        private Mob FindMobByViewSpawnId(UInt32 viewSpawnId)
        {
            // Extract view ID (lower 16 bits) and spawn ID (upper 16 bits)
            UInt16 viewId = (UInt16)(viewSpawnId & 0xFFFF);
            UInt16 spawnId = (UInt16)((viewSpawnId >> 16) & 0xFFFF);

            Output.WriteDebugLog($"[COMBAT] Looking for mob with viewId={viewId}, spawnId={spawnId}");

            // TODO: Search Store.world.mobs or similar for the target
            // For now, return null - combat will work but damage won't apply
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
        /// This is where damage calculation and application should happen.
        /// </summary>
        private void HandleCombatTick(CombatSession session)
        {
            if (!isCombatRunning || session == null) return;

            Output.WriteDebugLog($"[COMBAT] Tick - Round {session.RoundNumber}");

            // TODO: Phase 2 implementation:
            // 1. Calculate damage based on player stats/weapon
            // 2. Apply damage to defender via session.ApplyDamageToDefender(damage, fxId)
            // 3. Check if defender died
            // 4. Have mob attack back if still alive

            // For now, send the legacy update packet
            SendCombatUpdatePacket(session);

            // Check if defender died (once mob lookup is working)
            if (session.IsDefenderDead())
            {
                Output.WriteDebugLog("[COMBAT] Defender died!");
                session.Stop(CombatSession.CombatEndReason.DefenderDied);
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

            // If defender died, trigger loot/death sequence
            if (reason == CombatSession.CombatEndReason.DefenderDied && session.DefenderMob != null)
            {
                packets.SendNpcDies(session.CombatHandlerViewId, Store.currentClient, session.DefenderMob);
                // TODO: Trigger loot window
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
