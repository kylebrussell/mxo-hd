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
            // 01 0c 00 | NoiseLevel 0 | ConditionStateFlags: in combat (0x00100000), with the player's PvP flag kept
            client.playerData.conditionStateFlags |= PvpFlag.InCombatBit;
            UInt32 flags = PvpFlag.FlagsOf(client);
            client.playerInstance.ConditionStateFlags.setValue(flags);
            client.messageQueue.addObjectMessage(BuildEnterCombatMode(flags), false);
            // Everyone who sees the player: retail's views of other players carried the in-combat bit too
            PvpFlag.ToWatchers(client, PvpFlag.BuildOtherFlags(flags));
        }

        /// <summary>Retail own-view combat state plus NoiseLevel and ConditionStateFlags. The final group mask
        /// 01 is followed immediately by the u32 flags; an extra zero shifts the following view ID by a byte.</summary>
        public static byte[] BuildEnterCombatMode(UInt32 flags)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("020003010C008084008080808001");
            pak.AddUint32(flags, 1);
            return pak.ReturnFinalPacket();
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
            // The PvP flag (0x01000000) isn't a combat state: it stays
            client.playerData.conditionStateFlags = 0;
            UInt32 flags = PvpFlag.FlagsOf(client);
            client.playerInstance.ConditionStateFlags.setValue(flags);
            pak.AddHexBytes("01000080808080808001");
            pak.AddUint32(flags, 1);

            client.messageQueue.addObjectMessage(pak.ReturnFinalPacket(), false);
            PvpFlag.ToWatchers(client, PvpFlag.BuildOtherFlags(flags));
        }

        /// <summary>
        /// Sends the initial combat setup packet that links attacker and defender.
        /// </summary>
        /// <param name="client">The attacking player</param>
        /// <param name="combatViewId">View ID of the ILCombatHandler object</param>
        /// <param name="targetViewSpawnId">Combined view+spawn ID of the target</param>
        public void SendCombatInitialize(WorldClient client, UInt16 combatViewId, UInt32 targetViewSpawnId)
        {
            // Combatant IDs. Retail (player-started fight): 02 00 44 00 | 2f 00 22 00 = attacker (player, view 2)
            // first, then the defender. This used to send the target first, telling the client the NPC attacked.
            UInt32 self = (UInt32)(2 | (client.playerData.selfSpawnIdCounter << 16));
            client.messageQueue.addObjectMessage(BuildCombatInitialize(combatViewId,
                client.playerInstance.Position.getValue(), self, targetViewSpawnId), false);
        }

        /// <summary>
        /// The sized master-view record that links fighter slots to the combat view. Its u16 offset includes
        /// its own two bytes: 167 (a7 00) with one 121-byte entry, 46 without an entry. The attacker is always
        /// first, including on the attacked player's client (died_in_pvp 19:41:11).
        /// </summary>
        public static byte[] BuildCombatInitialize(UInt16 combatViewId, byte[] centre, UInt32 attacker, UInt32 defender,
            CombatRoundEntry firstEntry = null)
        {
            PacketContent pak = new PacketContent();

            // Combat handler reference
            pak.AddUint16(combatViewId, 1);   // Combat handler view ID
            pak.AddByte(0x01);                // Flag

            pak.AddByteArray(centre);

            // Combat relationship data
            pak.AddUint32(1, 1);              // Update counter
            pak.AddUint32(3, 1);              // Changed fighter slots 1 and 2

            pak.AddUint32(attacker, 1);
            pak.AddUint32(defender, 1);
            pak.AddByte(firstEntry == null ? (byte)0 : (byte)1);
            if (firstEntry != null) pak.AddByteArray(firstEntry.Serialize());
            byte[] body = pak.ReturnFinalPacket();
            PacketContent wrapper = new PacketContent();
            wrapper.AddUint16(1, 1); // master view
            wrapper.AddByte(2); // sized view message
            wrapper.AddUint16(checked((ushort)(body.Length + 2)), 1); // offset includes this length field
            wrapper.AddByteArray(body);
            return wrapper.ReturnFinalPacket();
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
            byte[] combatCentre = null, UInt32 rounds = 0, bool releaseTarget = true)
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

            // A duel's loser got no 46 00 (niobe3dualduality 03:04:00: only the final record and the delete)
            if (releaseTarget)
            {
                client.messageQueue.addRpcMessage(BuildCombatTargetFlag(targetViewSpawnId, 0x00));
            }

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

            // DelayedActions keeps the action until it runs. The plain Timer this used, referenced by nothing, could be
            // garbage-collected before it fired; the reset then never went out and the client stayed in combat
            // (no Emergency Repairs: "cannot be used during combat").
            ScheduleLeaveCombatMode(client);
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

        /// <summary>ILCombatHandler subview 1: centre, update counter, fighter-change mask, entry count and packed entries.</summary>
        public static byte[] BuildCombatViewRound(byte[] centre, uint counter, CombatRoundEntry entry)
        {
            if (centre == null || centre.Length != 24) throw new ArgumentException("Combat centre must be three doubles");
            PacketContent pak = new PacketContent();
            pak.AddByte(1); pak.AddByteArray(centre); pak.AddUint32(counter, 1);
            pak.AddUint32(0, 1); // no change to the init's fighter slots
            pak.AddByte(1); pak.AddByteArray(entry.Serialize());
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
        /// Own view: the player fires at a target. Part 1 is an action record (flag 0x20) with position and time
        /// (flag 0x08): <c>28 &lt;action counter&gt; 01 0c 38 81 00 &lt;target view u16&gt; &lt;spawn u16&gt;
        /// &lt;x y z f32&gt; &lt;sim ms u32&gt;</c>. Part 2 sets NoiseLevel (attribute 9); on the first shot it also
        /// sets ConditionStateFlags 0x00100000 (in combat) and CombatantMode 0x21 (weapon drawn).
        /// Retail, tutorial capture (crashover2k ..._uriah.log.txt, 13:30:17):
        /// <c>02 00 03 28 02 01 0c 38 81 00 06 00 f2 00 &lt;pos&gt; 33 be e7 1e 80 04 4b</c>; first shot in the luizoe
        /// old-sniffer Unknown log, line 11790: <c>... 80 84 32 80 80 80 80 03 00 00 10 00 21</c>.
        /// The bytes <c>01 0c</c> are copied, not understood; the next byte is the weapon's attack class
        /// (<see cref="RangedWeapons"/>: 0x38 pistols such as the Redpill Special, 0x37 rifles, 0x39 SMGs). <c>81 00</c> is a hit and <c>00 10</c> a miss: in the
        /// tutorial fight all ten shots (five each way) match the hit/miss result of their 81 68 combat-log line.
        /// </summary>
        public static byte[] BuildRangedShot(byte actionCounter, UInt32 targetViewSpawnId, float x, float y, float z,
            UInt32 simMs, byte noiseLevel, bool firstShot, bool hit = true, byte attackClass = RangedWeapons.ClassPistol,
            UInt32 flags = CombatInterlock.InCombatFlag)
        {
            PacketContent pak = new PacketContent();
            pak.AddUint16(2, 1);
            pak.AddByte(0x03);
            pak.AddByte(0x28);
            pak.AddByte(actionCounter);
            pak.AddHexBytes("010c");
            pak.AddByte(attackClass);
            pak.AddHexBytes(hit ? "8100" : "0010");
            pak.AddUint32(targetViewSpawnId, 1);
            pak.AddFloatLtVector3f(x, y, z);
            pak.AddUint32(simMs, 1);
            if (firstShot)
            {
                pak.AddHexBytes("8084");
                pak.AddByte(noiseLevel);
                pak.AddHexBytes("80808080" + "03");
                pak.AddUint32(flags, 1);
                pak.AddByte(CombatHandler.CombatantModeReady);
            }
            else
            {
                pak.AddHexBytes("8004");
                pak.AddByte(noiseLevel);
            }
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// RPC 81 68: one combat-log line. Layout after the header (offsets from its first byte):
        /// <c>&lt;offset of attacker name u16&gt; &lt;offset of defender name u16 = 0x1d&gt; &lt;result u32: 3 hit, 4 miss&gt;
        /// &lt;damage u16&gt; &lt;modifier i16&gt; &lt;attack roll u16&gt; &lt;defense roll u16&gt; 00000000 &lt;attacker is you u8&gt;
        /// &lt;weapon goid u32&gt; &lt;attacker's tactic u16&gt; &lt;defender name&gt; &lt;attacker name&gt;</c>, names as u16 length (with the 0) + text + 0.
        /// The client's log names the attack after the tactic ("Speed Redpill Special"): retail lines sent after a
        /// <c>42 01</c> have 1 there (<c>05 0a 00 00 01 00</c>), after <c>42 00</c> 0.
        /// In the retail tutorial fight (crashover2k ..._uriah.log.txt 13:30:13-13:30:33) every hit had attack roll &gt;
        /// defense roll and every miss the reverse; damage matched the health lost. The player's hits had modifier -7
        /// (meaning unknown), the NPC's 0. Example, the player hits "Alex" for 19:
        /// <c>81 68 24 00 1d 00 03 00 00 00 13 00 f9 ff 47 00 2d 00 00 00 00 00 01 05 0a 00 00 00 00 05 00 Alex 00 07 00 mxoemu 00</c>.
        /// </summary>
        public static byte[] BuildCombatLog(bool hit, UInt16 damage, Int16 modifier, UInt16 attackRoll, UInt16 defenseRoll,
            bool attackerIsYou, UInt32 weaponGoid, string defenderName, string attackerName,
            CombatTactic attackerTactic = CombatTactic.Speed)
        {
            const UInt16 defenderNameOffset = 0x1d;
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8168");
            pak.AddUint16((UInt16)(defenderNameOffset + defenderName.Length + 1 + 2), 1);
            pak.AddUint16(defenderNameOffset, 1);
            pak.AddUint32(hit ? 3u : 4u, 1);
            pak.AddUint16(damage, 1);
            pak.AddInt16(modifier, 1);
            pak.AddUint16(attackRoll, 1);
            pak.AddUint16(defenseRoll, 1);
            pak.AddUint32(0, 1);
            pak.AddByte(attackerIsYou ? (byte)1 : (byte)0);
            pak.AddUint32(weaponGoid, 1);
            pak.AddUint16((UInt16)attackerTactic, 1);
            pak.AddSizedTerminatedString(defenderName);
            pak.AddSizedTerminatedString(attackerName);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// NPC view part 02: the NPC fires at a player. <c>02 2a &lt;counter&gt; 01 0c &lt;class&gt; &lt;81 00 hit | 00 10 miss&gt;
        /// &lt;target view u16&gt; &lt;spawn u16&gt; 00 &lt;x y z f32&gt;</c>; flags 2a = action (0x20), a one-byte field (0x02,
        /// always 00 in these captures) and position (0x08). The target is the player as the receiving client sees
        /// them: own view 2 and their self-spawn id. Retail, tutorial capture (crashover2k ..._uriah.log.txt line
        /// 14258): <c>06 00 02 2a 04 01 0c 38 81 00 02 00 ea 00 00 a0 60 e7 45 00 60 c7 44 76 b4 36 c6</c>; an NPC with
        /// a rifle used class 0x37 (luizoe old-sniffer Unknown log line 32719).
        /// </summary>
        public static byte[] BuildNpcRangedShot(byte counter, byte attackClass, bool hit, UInt32 targetViewSpawnId,
            float x, float y, float z)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("022a");
            pak.AddByte(counter);
            pak.AddHexBytes("010c");
            pak.AddByte(attackClass);
            pak.AddHexBytes(hit ? "8100" : "0010");
            pak.AddUint32(targetViewSpawnId, 1);
            pak.AddByte(0x00);
            pak.AddFloatLtVector3f(x, y, z);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Object12 player views use state part 03; NPC Object599 uses 02. The action body is shared.
        /// died_in_pvp: 0f 00 03 2a 71 01 0c 37 81 00 02 00 d3 00 00 followed by the shooter's position.
        /// Sending 02 to a player view instead parses 2a as an attribute mask and corrupts its appearance.
        /// </summary>
        public static byte[] BuildOtherPlayerRangedShot(byte counter, byte attackClass, bool hit, UInt32 targetViewSpawnId,
            float x, float y, float z)
        {
            byte[] shot = BuildNpcRangedShot(counter, attackClass, hit, targetViewSpawnId, x, y, z);
            shot[0] = 0x03;
            return shot;
        }

        /// <summary>RPC 4e 00, sent by retail with every shot the player fired (meaning unknown).</summary>
        public static byte[] BuildRangedShotRpc()
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x4e);
            pak.AddByte(0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// Clears the combat stance and flags <see cref="LeaveCombatDelayMs"/> after a fight, unless a new fight
        /// started meanwhile (DelayedActions: a plain Timer could be collected before it fired).
        /// </summary>
        public void ScheduleLeaveCombatMode(WorldClient client)
        {
            int generation = client.playerData.combatGeneration;
            DelayedActions.After(LeaveCombatDelayMs / 1000.0, () =>
            {
                if (client.playerData.combatGeneration == generation)
                {
                    SendLeaveCombatMode(client);
                    client.FlushQueue();
                }
            });
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
