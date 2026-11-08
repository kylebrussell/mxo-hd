using System;
using System.Text;

namespace hds
{
    /// <summary>
    /// Parsers for the client's social RPCs. <c>rpcData</c> is what RpcManager hands a handler: the bytes after the
    /// one-byte RPC header. Offsets inside these RPCs count from the header byte, so offset N is rpcData[N-1].
    /// Byte strings in the comments are the retail client's (see SocialPackets for the capture names).
    /// </summary>
    public static class SocialRequests
    {
        public struct GroupInvite
        {
            public byte GroupType;
            public string InviteeHandle;
            public string GroupName;
        }

        public struct InviteAnswer
        {
            public byte GroupType;
            public UInt32 Result;
            public string InviterHandle;
            public bool Accepted => Result == 0;
        }

        public struct ChatLine
        {
            public byte Channel;
            public string Text;
        }

        /// <summary>
        /// 6e &lt;type&gt; &lt;invitee offset u16&gt; &lt;group name offset u16&gt; then both sized strings, in either order:
        /// team <c>6e 03 06 00 10 00 08 00 "mxoemu3" 1d 00 "AfterWhoruNeo's Mission Team"</c>,
        /// crew <c>6e 02 17 00 06 00 0f 00 "Reservoir Dogs" 08 00 "mxoemu3"</c>.
        /// </summary>
        public static bool TryParseGroupInvite(byte[] rpcData, out GroupInvite invite)
        {
            invite = new GroupInvite();
            if (rpcData == null || rpcData.Length < 5)
            {
                return false;
            }

            invite.GroupType = rpcData[0];
            int inviteeOffset = rpcData[1] | (rpcData[2] << 8);
            int nameOffset = rpcData[3] | (rpcData[4] << 8);
            return TryReadSized(rpcData, inviteeOffset - 1, out invite.InviteeHandle) &
                   TryReadSized(rpcData, nameOffset - 1, out invite.GroupName);
        }

        /// <summary>The rpcData of a client 6e (invitee first, like the team invite) for server-side reuse.</summary>
        public static byte[] BuildGroupInvite(byte groupType, string inviteeHandle, string groupName)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(groupType);
            pak.AddUint16(6, 1);
            pak.AddUint16((UInt16) (6 + 2 + inviteeHandle.Length + 1), 1);
            pak.AddSizedTerminatedString(inviteeHandle);
            pak.AddSizedTerminatedString(groupName);
            return pak.ReturnFinalPacket();
        }

        /// <summary>6f &lt;type&gt; 08 00 &lt;result u32&gt; &lt;sized inviter&gt;; result 0 accepts,
        /// e.g. <c>6f 02 08 00 00 00 00 00 0e 00 "AfterWhoruNeo"</c>; a decline carried 0x0d00002e.</summary>
        public static bool TryParseInviteAnswer(byte[] rpcData, out InviteAnswer answer)
        {
            answer = new InviteAnswer();
            if (rpcData == null || rpcData.Length < 9)
            {
                return false;
            }

            answer.GroupType = rpcData[0];
            int offset = rpcData[1] | (rpcData[2] << 8);
            answer.Result = BitConverter.ToUInt32(rpcData, 3);
            return TryReadSized(rpcData, offset - 1, out answer.InviterHandle);
        }

        /// <summary>28 &lt;channel&gt; 00 08 00 00 00 00 &lt;sized text&gt;, e.g. crew chat <c>28 12 00 08 00 00 00 00 09 00 "finished"</c>.</summary>
        public static bool TryParseChat(byte[] rpcData, out ChatLine line)
        {
            line = new ChatLine();
            if (rpcData == null || rpcData.Length < 9)
            {
                return false;
            }

            line.Channel = rpcData[0];
            int offset = rpcData[2] | (rpcData[3] << 8);
            return TryReadSized(rpcData, offset - 1, out line.Text);
        }

        /// <summary>A u16 length (counting the terminator) then the text and a 00.</summary>
        public static bool TryReadSized(byte[] data, int index, out string value)
        {
            value = null;
            if (index < 0 || index + 2 > data.Length)
            {
                return false;
            }

            int size = data[index] | (data[index + 1] << 8);
            int start = index + 2;
            if (size < 1 || start + size > data.Length)
            {
                return false;
            }

            int length = size - 1;
            // Tolerate a missing terminator or one placed early.
            int nul = Array.IndexOf(data, (byte) 0, start, length);
            if (nul >= 0)
            {
                length = nul - start;
            }

            value = Encoding.UTF8.GetString(data, start, length);
            return true;
        }
    }
}
