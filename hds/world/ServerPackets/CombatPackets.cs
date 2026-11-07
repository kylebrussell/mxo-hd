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
            // Retail combat end: state field 0 and ConditionStateFlags 0 (enter sets 0x000c / 0x00100000).
            // This used to end group 6 with no attribute bits and then four stray bytes.
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);              // View ID 2 (player's self view)
            pak.AddByte(0x03);                // Update type
            pak.AddHexBytes("01000080808080808001" + "00000000");

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

            // Combatant IDs. Retail (player-started fight): 02 00 44 00 | 2f 00 22 00 = attacker (player, view 2)
            // first, then the defender. This used to send the target first, telling the client the NPC attacked.
            pak.AddUint16(2, 1);                                   // Player view ID
            pak.AddUint16(client.playerData.selfSpawnIdCounter, 1); // Player spawn ID
            pak.AddUint32(targetViewSpawnId, 1);                  // Target view+spawn ID
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
        public void SendCombatEnd(WorldClient client, UInt16 combatViewId, UInt32 targetViewSpawnId,
            byte[] combatCentre = null, UInt32 rounds = 0)
        {
            // Retail releases the attacker with a final record on the combat view, RPC 46 <target> 00, an own-view
            // position handoff and then the delete of the combat view; the out-of-combat reset follows ~10 s
            // later. Sending that reset at once and nothing else left the client locked in its fight stance.
            if (combatCentre != null && combatCentre.Length == 24)
            {
                PacketContent final = new PacketContent();
                final.AddUint16(combatViewId, 1);
                final.AddByteArray(BuildCombatViewFinalRecord(combatCentre, rounds));
                final.AddUint16(combatViewId, 1);
                final.AddByteArray(BuildCombatViewAttackerPosition(client.playerInstance.Position.getValue()));
                client.messageQueue.addObjectMessage(final.ReturnFinalPacket(), false);
            }

            client.messageQueue.addRpcMessage(BuildCombatTargetFlag(targetViewSpawnId, 0x00));

            // Delete the combat view and hand the player back their position in the same packet, as retail did
            // (01 00 01 01 00 <view> followed by 02 00 01 0c <yaw> <pos> <time>).
            PacketContent delete = new PacketContent();
            delete.AddUint16(1, 1);
            delete.AddByte(0x01);
            delete.AddUint16(1, 1);
            delete.AddUint16(combatViewId, 1);
            client.messageQueue.addObjectMessage(delete.ReturnFinalPacket(), false);

            double x = 0, y = 0, z = 0;
            NumericalUtils.LtVector3dToDoubles(client.playerInstance.Position.getValue(), ref x, ref y, ref z);
            byte yaw = client.playerInstance.YawInterval.getValue()[0];
            UInt32 simMs = (UInt32)Math.Round(TimeUtils.GetSimTimeSeconds() * 1000.0);
            client.messageQueue.addObjectMessage(BuildSelfPositionHandoff(yaw, (float)x, (float)y, (float)z, simMs), false);
            client.FlushQueue();

            int generation = client.playerData.combatGeneration;
            System.Threading.Timer timer = null;
            timer = new System.Threading.Timer(_ =>
            {
                timer?.Dispose();
                if (client.playerData.combatGeneration == generation)
                {
                    SendLeaveCombatMode(client);
                    client.FlushQueue();
                }
            }, null, LeaveCombatDelayMs, System.Threading.Timeout.Infinite);
        }

        /// <summary>
        /// Combat view (ILCombatHandler) part 01 closing the fight. Retail ticks were
        /// <c>01 &lt;centre f64×3&gt; &lt;counter u32&gt; 00000000 00</c> (no round entries); the last one set flags bit 31,
        /// added a u32 result of 2 and carried the finishing round entry. We send it without the entry.
        /// </summary>
        public static byte[] BuildCombatViewFinalRecord(byte[] centre, UInt32 counter)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x01);
            pak.AddByteArray(centre);
            pak.AddUint32(counter, 1);
            pak.AddUint32(0x80000000, 1);
            pak.AddUint32(2, 1);
            pak.AddByte(0x00); // round entries
            return pak.ReturnFinalPacket();
        }

        /// <summary>Combat view part 02: <c>02 01 &lt;attacker position f64×3&gt;</c>, sent right after the final record.</summary>
        public static byte[] BuildCombatViewAttackerPosition(byte[] position)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("0201");
            pak.AddByteArray(position);
            return pak.ReturnFinalPacket();
        }

        /// <summary>Retail cleared the combat stance and flags about 10 s after the kill (11 s in another capture).</summary>
        public const int LeaveCombatDelayMs = 10000;

        /// <summary>
        /// RPC 46 &lt;target view u16&gt; &lt;spawn u16&gt; &lt;flag&gt;: retail bracketed every close-combat fight with
        /// flag 01 at the start and 00 when it ended (46 2f 00 22 00 01 ... 46 2f 00 22 00 00).
        /// </summary>
        public static byte[] BuildCombatTargetFlag(UInt32 targetViewSpawnId, byte flag)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x46);
            pak.AddUint32(targetViewSpawnId, 1);
            pak.AddByte(flag);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Own-view state part with yaw (flag 0x04) and position (flag 0x08) as f32, then the sim time in ms:
        /// retail 02 00 01 0c 66 &lt;x y z f32&gt; &lt;time u32&gt; just before deleting the combat view.
        /// </summary>
        public static byte[] BuildSelfPositionHandoff(byte yaw, float x, float y, float z, UInt32 simMs)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("0200010c");
            pak.AddByte(yaw);
            pak.AddFloatLtVector3f(x, y, z);
            pak.AddUint32(simMs, 1);
            return pak.ReturnFinalPacket();
        }
    }
}
