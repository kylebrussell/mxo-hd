using System;

namespace hds
{
    /// <summary>
    /// The state part of a client's update of its own view: <c>&lt;flags&gt; [action] [yaw] [b20] [x y z]</c>.
    /// The flag byte is a bitmask; each set bit adds a field, in this order:
    /// 0x02 action (u8), 0x04 yaw (u8), 0x20 an unidentified byte, 0x08 position (3 x f32 LE).
    /// Seen from the 7.6005 client while walking, turning and strafing (02, 04, 06, 08, 0a, 26) and in
    /// the retail captures under research/ (also 0c, 0e, 20, 22, 24, 2a, 2c, 2e). Bits 0x01, 0x10, 0x40
    /// and 0x80 carry fields whose layout isn't known, so updates using them are not parsed.
    /// </summary>
    public sealed class SelfViewUpdate
    {
        public const byte FlagAction = 0x02;
        public const byte FlagYaw = 0x04;
        public const byte FlagPosition = 0x08;
        public const byte Flag20 = 0x20;
        public const byte KnownFlags = FlagAction | FlagYaw | FlagPosition | Flag20;

        public byte Flags { get; private set; }
        public byte? Action { get; private set; }
        public byte? Yaw { get; private set; }
        public byte? Unknown20 { get; private set; }
        public bool HasPosition { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }

        /// <summary>Offset of the flag byte.</summary>
        public int Start { get; private set; }

        /// <summary>Offset just past the last field.</summary>
        public int End { get; private set; }

        /// <summary>
        /// Parses the state fields whose flag byte is at <paramref name="offset"/>. Returns false for
        /// flags with fields of unknown layout or when the data is cut short.
        /// </summary>
        public static bool TryParse(byte[] buffer, int offset, out SelfViewUpdate update)
        {
            update = null;
            if (buffer == null || offset < 0 || offset >= buffer.Length)
            {
                return false;
            }

            byte flags = buffer[offset];
            if ((flags & ~KnownFlags) != 0)
            {
                return false;
            }

            var result = new SelfViewUpdate { Flags = flags, Start = offset };
            int pos = offset + 1;

            if ((flags & FlagAction) != 0)
            {
                if (pos + 1 > buffer.Length) return false;
                result.Action = buffer[pos++];
            }

            if ((flags & FlagYaw) != 0)
            {
                if (pos + 1 > buffer.Length) return false;
                result.Yaw = buffer[pos++];
            }

            if ((flags & Flag20) != 0)
            {
                if (pos + 1 > buffer.Length) return false;
                result.Unknown20 = buffer[pos++];
            }

            if ((flags & FlagPosition) != 0)
            {
                if (pos + 12 > buffer.Length) return false;
                result.X = ReadSingle(buffer, pos);
                result.Y = ReadSingle(buffer, pos + 4);
                result.Z = ReadSingle(buffer, pos + 8);
                result.HasPosition = true;
                pos += 12;
            }

            result.End = pos;
            update = result;
            return true;
        }

        /// <summary>
        /// The update as other players' views receive it: <c>01 &lt;flags&gt; &lt;fields&gt;</c>, the same layout
        /// (retail entries for other players look like <c>&lt;view&gt; 01 0a 3e &lt;x y z&gt;</c>).
        /// </summary>
        public byte[] ToForwardedUpdate(byte[] buffer)
        {
            byte[] update = new byte[1 + End - Start];
            update[0] = SelfViewEntry.PartState;
            Array.Copy(buffer, Start, update, 1, End - Start);
            return update;
        }

        private static float ReadSingle(byte[] buffer, int pos)
        {
            byte[] value = { buffer[pos], buffer[pos + 1], buffer[pos + 2], buffer[pos + 3] };
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(value);
            }
            return BitConverter.ToSingle(value, 0);
        }
    }

    /// <summary>
    /// One entry for the player's own view (view id 2) in a client protocol 03 section, starting at the
    /// sub-view byte. The sub-view byte is a bitmask of the parts that follow, in order:
    /// 0x01 a <see cref="SelfViewUpdate"/>, 0x02 the real names (<c>60 &lt;len&gt; last &lt;len&gt; first</c>,
    /// sent once after the self-spawn). The client can send both at once (sub-view 03).
    /// </summary>
    public sealed class SelfViewEntry
    {
        public const byte PartState = 0x01;
        public const byte PartNames = 0x02;
        public const byte NamesFlags = 0x60;

        public byte SubView { get; private set; }
        public SelfViewUpdate State { get; private set; }
        public byte[] LastName { get; private set; }
        public byte[] FirstName { get; private set; }

        /// <summary>Offset of the sub-view byte.</summary>
        public int Start { get; private set; }

        /// <summary>Offset just past the entry, where the next view id (00 00 ends the list) starts.</summary>
        public int End { get; private set; }

        /// <summary>
        /// Parses the entry at <paramref name="offset"/>. Fails, with a reason for the log, when a part's
        /// layout isn't known or when the entry isn't followed by another view id or the end of the packet:
        /// a wrong guess about a field's length must not be applied as a position.
        /// </summary>
        public static bool TryParse(byte[] buffer, int offset, out SelfViewEntry entry, out string failure)
        {
            entry = null;
            failure = null;
            if (buffer == null || offset < 0 || offset >= buffer.Length)
            {
                failure = "no data";
                return false;
            }

            byte subView = buffer[offset];
            if (subView == 0 || (subView & ~(PartState | PartNames)) != 0)
            {
                failure = "unknown sub-view " + subView.ToString("x2");
                return false;
            }

            var result = new SelfViewEntry { SubView = subView, Start = offset };
            int pos = offset + 1;

            if ((subView & PartState) != 0)
            {
                if (!SelfViewUpdate.TryParse(buffer, pos, out SelfViewUpdate state))
                {
                    failure = "state flags " + (pos < buffer.Length ? buffer[pos].ToString("x2") : "missing");
                    return false;
                }
                result.State = state;
                pos = state.End;
            }

            if ((subView & PartNames) != 0)
            {
                if (pos >= buffer.Length || buffer[pos] != NamesFlags)
                {
                    failure = "name flags " + (pos < buffer.Length ? buffer[pos].ToString("x2") : "missing");
                    return false;
                }
                pos++;
                result.LastName = ReadPrefixed(buffer, ref pos);
                result.FirstName = result.LastName == null ? null : ReadPrefixed(buffer, ref pos);
                if (result.FirstName == null)
                {
                    failure = "names cut short";
                    return false;
                }
            }

            // Another view id must follow (or the packet ends here); anything else means a field length
            // above was wrong.
            if (pos != buffer.Length)
            {
                if (pos + 2 > buffer.Length)
                {
                    failure = "trailing byte after entry";
                    return false;
                }
                int nextView = buffer[pos] | (buffer[pos + 1] << 8);
                if (nextView != 0 && nextView != 2)
                {
                    failure = "unexpected bytes after entry";
                    return false;
                }
            }

            result.End = pos;
            entry = result;
            return true;
        }

        private static byte[] ReadPrefixed(byte[] buffer, ref int pos)
        {
            if (pos >= buffer.Length)
            {
                return null;
            }
            int length = buffer[pos];
            if (pos + 1 + length > buffer.Length)
            {
                return null;
            }
            byte[] value = new byte[length];
            Array.Copy(buffer, pos + 1, value, 0, length);
            pos += 1 + length;
            return value;
        }
    }
}
