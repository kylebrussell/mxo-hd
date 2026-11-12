using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hds
{
    /// <summary>7.6005 fixed records and bounded UTF-8 strings. Offsets include the opcode.</summary>
    public static class ClientActionRequests
    {
        public sealed class Petition
        {
            public uint Ticket;
            public string Text;
            public bool Cancel;
        }
        public sealed class StoredCompile
        {
            public uint Item;
            public ushort Slot;
            public byte Tool;
            public bool FromCrafting;
        }
        public sealed class Materialized
        {
            public byte Result = 1;
            public ushort Slot;
            public int BagCount, Remaining;
            public long Cash;
        }
        // 6208375a..782: ObjectID is the decoded goid, not the packed archive entry.
        public static StoredCompile ParseStoredCompile(byte[] body) => body?.Length == 9 && body[6] == 0 && body[8] <= 1
            ? new StoredCompile { Item = BitConverter.ToUInt32(body, 0), Slot = BitConverter.ToUInt16(body, 4), Tool = body[7], FromCrafting = body[8] != 0 }
            : null;

        public static Petition ParsePetition(byte[] body)
        {
            if (body == null || body.Length < 8 || body[6] != 0 || body[7] > 1) return null;
            uint ticket = BitConverter.ToUInt32(body, 0);
            if (body.Length == 8 && body[7] == 1 && BitConverter.ToUInt16(body, 4) == 0)
                return new Petition { Ticket = ticket, Text = "", Cancel = true };
            if (!ReadString(body, 4, 8, 3500, out string text)) return null;
            return new Petition { Ticket = ticket, Text = text, Cancel = body[7] != 0 };
        }

        public static bool ReadString(byte[] body, int pointer, int minimum, int maximum, out string text)
        {
            text = null;
            if (body == null || pointer < 0 || pointer + 2 > body.Length) return false;
            int at = BitConverter.ToUInt16(body, pointer) - 2;
            if (at < minimum || at + 2 > body.Length) return false;
            int size = BitConverter.ToUInt16(body, at);
            if (size < 1 || size > maximum + 1 || at + 2 + size != body.Length || body[at + 1 + size] != 0) return false;
            try { text = new UTF8Encoding(false, true).GetString(body, at + 2, size - 1); }
            catch (DecoderFallbackException) { return false; }
            return !text.Contains('\0');
        }

        public static bool TrustedGm(uint charId, string configuredIds) => charId != 0 &&
            (configuredIds ?? "").Split(',').Any(s => uint.TryParse(s.Trim(), out uint id) && id == charId);

        public static byte[] Echo(byte[] body)
        {
            if (body?.Length != 4) return null;
            return new byte[] { 0x81, 0x4d }.Concat(body).ToArray();
        }

        public static byte[] Packet(byte opcode, int length)
        {
            var bytes = new byte[length]; bytes[0] = 0x81; bytes[1] = opcode; return bytes;
        }
        public static void U32(byte[] bytes, int at, uint value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
        public static void U16(byte[] bytes, int at, ushort value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
        public static byte[] String(byte[] frame, int pointer, string value, int relativeBase = 0)
        {
            byte[] text = Encoding.UTF8.GetBytes(value ?? "");
            U16(frame, pointer, checked((ushort)(frame.Length - relativeBase)));
            var result = new byte[frame.Length + text.Length + 3];
            frame.CopyTo(result, 0); U16(result, frame.Length, checked((ushort)(text.Length + 1)));
            text.CopyTo(result, frame.Length + 2); return result;
        }
    }
}
