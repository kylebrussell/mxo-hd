using System;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Combat-related packet builders.
    /// These methods construct the packets needed for the combat system.
    ///
    /// See docs/COMBAT-SYSTEM-ROADMAP.md for combat system documentation.
    /// </summary>
    public partial class ServerPackets
    {
        /// <summary>
        /// Sets a player into combat mode (enables combat stance/animations).
        /// </summary>
        /// <param name="client">The player entering combat</param>
        public void SendEnterCombatMode(WorldClient client)
        {
            // Original hex: "020003010C00808400808080800100001000"
            // Structure: [ViewID:2][UpdateType:1][AttributeMask][CombatantMode]
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);              // View ID 2 (player's self view)
            pak.AddByte(0x03);                // Update type (attribute update)
            pak.AddHexBytes("010C00808400808080800100001000"); // Attribute mask + combat mode
            // TODO: Build this properly using CombatantMode attribute

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
        }

        /// <summary>
        /// Removes combat mode from a player.
        /// </summary>
        /// <param name="client">The player leaving combat</param>
        public void SendLeaveCombatMode(WorldClient client)
        {
            // TODO: Determine the correct packet to exit combat mode
            // Likely similar to SendEnterCombatMode but with different CombatantMode value
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);              // View ID 2 (player's self view)
            pak.AddByte(0x03);                // Update type
            pak.AddHexBytes("010C00808400808080800000001000"); // CombatantMode = 0?

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
        }

        /// <summary>
        /// Sends the initial combat setup packet that links attacker and defender.
        /// </summary>
        /// <param name="client">The attacking player</param>
        /// <param name="combatViewId">View ID of the ILCombatHandler object</param>
        /// <param name="targetViewSpawnId">Combined view+spawn ID of the target</param>
        public void SendCombatInitialize(WorldClient client, UInt16 combatViewId, UInt32 targetViewSpawnId)
        {
            PacketContent pak = new PacketContent();

            // Header: Master view update with combat init message type
            pak.AddUint16(1, 1);              // View ID 1 (master view)
            pak.AddByte(0x02);                // Update type
            pak.AddUint16(0x00A7, 1);         // Message type (167 = combat init)

            // Combat handler reference
            pak.AddUint16(combatViewId, 1);   // Combat handler view ID
            pak.AddByte(0x01);                // Flag

            // Player position
            pak.AddByteArray(client.playerInstance.Position.getValue());

            // Combat relationship data
            pak.AddUint32(1, 1);              // Attacker slot/round (1)
            pak.AddUint32(3, 1);              // Combat type (3 = melee?)

            // Combatant IDs
            pak.AddUint32(targetViewSpawnId, 1);                  // Target view+spawn ID
            pak.AddUint16(2, 1);                                   // Player view ID
            pak.AddUint16(client.playerData.selfSpawnIdCounter, 1); // Player spawn ID
            pak.AddHexBytes("010102");                             // State flags

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
        }

        /// <summary>
        /// Sends combat state data (animations, positions, health).
        /// This is the complex blob that needs more research.
        /// </summary>
        /// <param name="client">The player in combat</param>
        /// <param name="combatViewId">View ID of the ILCombatHandler</param>
        /// <param name="attackerHealth">Attacker's current health</param>
        /// <param name="attackerMaxHealth">Attacker's max health</param>
        /// <param name="defenderHealth">Defender's current health</param>
        /// <param name="defenderMaxHealth">Defender's max health</param>
        public void SendCombatStateData(WorldClient client, UInt16 combatViewId,
            UInt16 attackerHealth, UInt16 attackerMaxHealth,
            UInt16 defenderHealth, UInt16 defenderMaxHealth)
        {
            // TODO: Build this packet properly once structure is fully understood
            // For now, this uses hardcoded data with some dynamic values spliced in

            PacketContent pak = new PacketContent();

            // Combat data header
            pak.AddHexBytes("0703");          // Combat data type
            pak.AddHexBytes("0703");          // Mirrored for both combatants

            // Attacker position (floats) - should be dynamic
            pak.AddByteArray(client.playerInstance.Position.getValue());

            // TODO: Rest of combat state
            // This requires more research into the packet structure

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
        }

        /// <summary>
        /// Sends a combat update on the timer tick.
        /// </summary>
        /// <param name="client">The player in combat</param>
        /// <param name="combatViewId">View ID of the ILCombatHandler</param>
        /// <param name="roundNumber">Current combat round</param>
        public void SendCombatUpdate(WorldClient client, UInt16 combatViewId, UInt32 roundNumber)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(combatViewId, 1);   // Combat handler view ID
            pak.AddUShort(3);                  // Position update flag (0x0003)
            pak.AddByteArray(client.playerInstance.Position.getValue()); // Current position

            // Combat update data
            pak.AddUint32(roundNumber, 1);    // Round/update number
            pak.AddUint32(0, 1);              // Padding/timestamp

            // TODO: Add proper combat state data
            // Currently using placeholder - needs real implementation

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
        }

        /// <summary>
        /// Sends a hit notification to all clients who can see the combat.
        /// Uses the existing Mob.HitEnemyWithDamage packet format.
        /// </summary>
        /// <param name="viewId">View ID of the entity being hit</param>
        /// <param name="newHealth">Health after damage</param>
        /// <param name="fxId">Visual effect ID for the hit</param>
        /// <param name="hitCounter">Hit sequence counter</param>
        public void SendCombatHit(WorldClient client, UInt16 viewId, UInt16 newHealth, UInt32 fxId, UInt16 hitCounter)
        {
            // Format from Mob.HitEnemyWithDamage:
            // 04 80 80 80 c0 [health:2] c0 [fxId:4] 01 [hitCounter:2]
            PacketContent pak = new PacketContent();
            pak.AddUint16(viewId, 1);
            pak.AddByte(0x04);
            pak.AddByte(0x80);
            pak.AddByte(0x80);
            pak.AddByte(0x80);
            pak.AddByte(0xc0);
            pak.AddUint16(newHealth, 1);
            pak.AddByte(0xc0);
            pak.AddUint32(fxId, 1);
            pak.AddByte(0x01);
            pak.AddUint16(hitCounter, 1);

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
        }

        /// <summary>
        /// Ends combat and cleans up the combat handler view.
        /// </summary>
        /// <param name="client">The player whose combat is ending</param>
        /// <param name="combatViewId">View ID of the ILCombatHandler to delete</param>
        public void SendCombatEnd(WorldClient client, UInt16 combatViewId)
        {
            // Remove combat mode from player
            SendLeaveCombatMode(client);

            // Delete the combat handler view
            sendDeleteViewPacket(client, combatViewId);
        }
    }
}
