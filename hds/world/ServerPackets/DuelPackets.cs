using System;
using System.Text;

namespace hds
{
    /// <summary>
    /// The duel records and the PvP notices. Layouts are client.dll's RPC classes (constructor, printer and handler of
    /// each: docs/PVP.md has the addresses); the examples are retail's, from the three captures that hold duels.
    /// A handle is the object as the receiving client knows it: <c>&lt;view u16&gt; &lt;spawn id u8&gt; 00</c>.
    /// Strings are a u16 offset from the record's start (0 = none) to <c>&lt;length with the 0, u16&gt; text 0</c>.
    /// </summary>
    public static class DuelPackets
    {
        // 53 DuelRequestFailed reasons (client switch table 0x6223682c). FromTarget picks the "your target is..." text.
        public const byte FailUnknownOrDeclined = 0, FailUnavailable = 1, FailTooFar = 2, FailNotPlayer = 3, FailOnTeam = 4,
            FailOnTeamAutoReject = 5, FailInCombat = 6, FailAlreadyDueling = 7, FailChallengingOther = 8,
            FailConsideringOther = 9, FailChallengingSame = 10, FailAlreadyDuelingSame = 11, FailMustWait = 12,
            FailDisabled = 13;

        // 56 DuelStop and 57 DuelBroadcast reasons (client table 0x62236d60)
        public const byte StopUnknown = 0, StopDefeated = 1, StopSurrendered = 2, StopDeath = 3, StopUnavailable = 4,
            StopTooFar = 5;

        /// <summary>String ids the server sends itself (stringtable_client).</summary>
        public const UInt32 KillNoticeString = 0x60000086, DuelDeathMessageString = 0x3a0000de;

        public static UInt32 Handle(UInt16 viewId, uint spawnId) => (UInt32)(viewId | ((spawnId & 0xff) << 16));

        private static void AddHandle(PacketContent pak, UInt32 handle) => pak.AddUint32(handle, 1);

        private static byte[] SizedString(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            byte[] result = new byte[bytes.Length + 3];
            result[0] = (byte)(bytes.Length + 1);
            result[1] = (byte)((bytes.Length + 1) >> 8);
            Array.Copy(bytes, 0, result, 2, bytes.Length);
            return result;
        }

        /// <summary>51 DuelChallenged (size 5): the challenger. The client opens its Yes/No dialog (15 s, then it declines).</summary>
        public static byte[] BuildChallenged(UInt32 challenger)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x51);
            AddHandle(pak, challenger);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 53 DuelRequestFailed (size 0x0d): reason, from-target, opponent, name offset, wait seconds f32 (reason 12 only).
        /// Retail: <c>53 04 01 35 00 68 00 00 00 00 00 00 00</c>; with a wait <c>53 0c 00 39 00 2c 00 00 00 07 01 34 42</c>.
        /// The name is for an opponent the receiver has no view of; retail left it out.
        /// </summary>
        public static byte[] BuildRequestFailed(byte reason, bool fromTarget, UInt32 opponent, string opponentName = null, float waitSeconds = 0)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x53);
            pak.AddByte(reason);
            pak.AddByte(fromTarget ? (byte)1 : (byte)0);
            AddHandle(pak, opponent);
            pak.AddUint16(string.IsNullOrEmpty(opponentName) ? (UInt16)0 : (UInt16)0x0d, 1);
            pak.AddFloat(waitSeconds, 1);
            if (!string.IsNullOrEmpty(opponentName))
            {
                pak.AddByteArray(SizedString(opponentName));
            }
            return pak.ReturnFinalPacket();
        }

        /// <summary>54 DuelChallengeSent (size 7): the target, name offset. Retail: <c>54 35 00 68 00 00 00</c>.</summary>
        public static byte[] BuildChallengeSent(UInt32 target, string targetName = null)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x54);
            AddHandle(pak, target);
            pak.AddUint16(string.IsNullOrEmpty(targetName) ? (UInt16)0 : (UInt16)0x07, 1);
            if (!string.IsNullOrEmpty(targetName))
            {
                pak.AddByteArray(SizedString(targetName));
            }
            return pak.ReturnFinalPacket();
        }

        /// <summary>55 DuelBegin (size 5): the opponent. The client notes its duel opponent and selects them.</summary>
        public static byte[] BuildBegin(UInt32 opponent)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x55);
            AddHandle(pak, opponent);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 56 DuelStop (size 9): reason, from-target, opponent, name offset. From-target 1 is the opponent's doing
        /// ("You have defeated %s"), 0 the receiver's ("%s has defeated you"). Retail, the winner's:
        /// <c>56 01 01 0d 00 07 00 00 00</c>; the loser's <c>56 01 00 2d 00 ee 00 00 00</c>.
        /// </summary>
        public static byte[] BuildStop(byte reason, bool fromTarget, UInt32 opponent, string opponentName = null)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x56);
            pak.AddByte(reason);
            pak.AddByte(fromTarget ? (byte)1 : (byte)0);
            AddHandle(pak, opponent);
            pak.AddUint16(string.IsNullOrEmpty(opponentName) ? (UInt16)0 : (UInt16)0x09, 1);
            if (!string.IsNullOrEmpty(opponentName))
            {
                pak.AddByteArray(SizedString(opponentName));
            }
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 57 DuelBroadcast (size 0x0e): reason, player, opponent, both names. The player is the one who lost or
        /// surrendered ("%2s has defeated %1s"). Retail sent it inside 81 9d with both handles 0:
        /// <c>57 01 00 00 00 00 00 00 00 00 0e 00 17 00 07 00 NlOBEE\0 07 00 SteveB\0</c>.
        /// </summary>
        public static byte[] BuildBroadcast(byte reason, string loserName, string winnerName)
        {
            byte[] loser = SizedString(loserName);
            PacketContent pak = new PacketContent();
            pak.AddByte(0x57);
            pak.AddByte(reason);
            pak.AddUint32(0, 1);
            pak.AddUint32(0, 1);
            pak.AddUint16(0x0e, 1);
            pak.AddUint16((UInt16)(0x0e + loser.Length), 1);
            pak.AddByteArray(loser);
            pak.AddByteArray(SizedString(winnerName));
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 81 9d: a record about two objects, wrapped for everyone nearby: <c>81 9d &lt;first u32&gt; &lt;second u32&gt; 0c 00
        /// &lt;length u16&gt; &lt;record&gt;</c>. Retail wrapped the kill notice (killer, victim) and the duel result (loser,
        /// winner) this way; a handle the receiver is itself is its own view, <c>02 00 &lt;self spawn id&gt; 00</c>.
        /// </summary>
        public static byte[] BuildAboutTwo(UInt32 first, UInt32 second, byte[] record)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("819d");
            AddHandle(pak, first);
            AddHandle(pak, second);
            pak.AddUint16(0x0c, 1);
            pak.AddUint16((UInt16)record.Length, 1);
            pak.AddByteArray(record);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// The kill notice's inner record, an 81 67 line with string 0x60000086 and the two names. Retail (died_in_pvp):
        /// <c>81 67 17 00 22 00 86 00 00 60 00 00 10 00 00 00 00 00 00 00 00 00 08 09 00 f1rewa11\0 0e 00 AfterWhoruNeo\0</c>.
        /// </summary>
        public static byte[] BuildKillLine(string killerName, string victimName)
        {
            byte[] killer = SizedString(killerName);
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("8167");
            pak.AddUint16(0x17, 1);
            pak.AddUint16((UInt16)(0x17 + killer.Length), 1);
            pak.AddUint32(KillNoticeString, 1);
            pak.AddHexBytes("00" + "00100000" + "00000000" + "0000" + "00" + "08");
            pak.AddByteArray(killer);
            pak.AddByteArray(SizedString(victimName));
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// 2e 67 with a string id: the system message form of the death message. Retail sent 0x3a0000de to a duel's loser
        /// ("...type /reconstruct to stand up") where a real death has 0x60000092.
        /// </summary>
        public static byte[] BuildSystemString(UInt32 stringId)
        {
            PacketContent pak = new PacketContent();
            pak.AddHexBytes("2e67" + "0000000000000000000000");
            pak.AddUint32(stringId, 1);
            pak.AddHexBytes("00000000000000000000000000000000000000");
            return pak.ReturnFinalPacket();
        }

        /// <summary>50 DuelChallenge: the target's handle; 0 is "/duel stop". Null when the request is too short.</summary>
        public static UInt32? ParseChallenge(byte[] data)
        {
            return data != null && data.Length >= 4 ? BitConverter.ToUInt32(data, 0) : (UInt32?)null;
        }

        /// <summary>52 DuelResponse: the challenger's handle, accepts, reason (0 answered or timed out, 2 challenger not in view).</summary>
        public static bool TryParseResponse(byte[] data, out UInt32 challenger, out bool accepts, out byte reason)
        {
            challenger = 0;
            accepts = false;
            reason = 0;
            if (data == null || data.Length < 6)
            {
                return false;
            }
            challenger = BitConverter.ToUInt32(data, 0);
            accepts = data[4] != 0;
            reason = data[5];
            return true;
        }
    }
}
