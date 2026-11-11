using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// The animation part of a player's view update: emotes, the two halves of a paired emote, and stances. Layouts are
    /// retail's (no client.dll reader for them was found). The state part of an update is <c>01 &lt;flags&gt;</c>, then
    /// a field for each flag in the order 0x20, 0x01, 0x02, 0x04, 0x08; 0x20 is this animation block:
    /// <c>&lt;counter&gt; &lt;kind&gt; &lt;stance&gt; &lt;25 &lt;action u16&gt; | 00&gt; 00 10 [target u32] [16 bytes]</c>.
    /// <list type="bullet">
    /// <item>kind 40: a plain emote (<c>40 00 25 04 00 00 10</c>, /clap) or a stance (<c>40 0a 00 00 10</c>, sitting).</item>
    /// <item>kind 01: a paired emote's request, with the partner (<c>01 00 25 d5 00 00 10 17 00 96 00</c>, /manhug).</item>
    /// <item>kind 02: its accepted animation, the partner and four floats 0, 0, 140, pi (the same in every capture).</item>
    /// </list>
    /// Retail, both sides of a /manhug (two-person capture, docs/CLIENT-ACTIONS.md "Emotes and stances in detail"): the
    /// requester's own view <c>02 00 01 28 08 01 00 25 d5 00 00 10 17 00 96 00 &lt;pos&gt; &lt;time&gt;</c>, the target's
    /// view of the requester <c>0f 00 01 2a 08 01 00 25 d5 00 00 10 02 00 96 00 00 &lt;pos&gt;</c>. The own view has 28
    /// (block, position) and a u32 sim time in ms after the position; other views have 2a (block, a 00 action byte,
    /// position) and no time. The counter is one per character, the same in every view.
    /// </summary>
    internal static class AnimationPackets
    {
        public const byte KindPlain = 0x40;
        public const byte KindRequest = 0x01;
        public const byte KindAccepted = 0x02;

        public const byte StanceStanding = 0x00;
        public const byte StanceSitting = 0x0a;

        /// <summary>ConditionStateFlags bit set while sitting (retail own view 01 00 00 20 00 after 34 0a).</summary>
        public const UInt32 SittingFlag = 0x00200000;

        /// <summary>The four floats after the partner in an accepted paired emote: 0, 0, 140, pi.</summary>
        private static readonly byte[] AcceptedTail =
            {0, 0, 0, 0, 0, 0, 0, 0, 0x00, 0x00, 0x0c, 0x43, 0xdb, 0x0f, 0x49, 0x40};

        public struct Animation
        {
            public byte Counter;
            public byte Kind;
            public byte Stance;
            public UInt16? Action;
            public float X, Y, Z;
        }

        /// <summary>The 0x20 field: the block, with the partner as the receiving client knows them (kinds 01 and 02).</summary>
        public static byte[] Block(Animation a, UInt32 partner)
        {
            List<byte> block = new List<byte> {a.Counter, a.Kind, a.Stance};
            if (a.Action.HasValue)
            {
                block.Add(0x25);
                block.Add((byte) a.Action.Value);
                block.Add((byte) (a.Action.Value >> 8));
            }
            else
            {
                block.Add(0x00); // no action: 40 0a 00 00 10
            }

            block.Add(0x00);
            block.Add(0x10);
            if (a.Kind == KindRequest || a.Kind == KindAccepted)
            {
                block.AddRange(BitConverter.GetBytes(partner));
            }

            if (a.Kind == KindAccepted)
            {
                block.AddRange(AcceptedTail);
            }

            return block.ToArray();
        }

        private static void AddPosition(List<byte> bytes, Animation a)
        {
            bytes.AddRange(BitConverter.GetBytes(a.X));
            bytes.AddRange(BitConverter.GetBytes(a.Y));
            bytes.AddRange(BitConverter.GetBytes(a.Z));
        }

        /// <summary>
        /// The player's own view, as an object message: <c>02 00 &lt;01|03&gt; 28 &lt;block&gt; &lt;pos&gt; &lt;sim ms&gt;</c>,
        /// with ConditionStateFlags (own attribute 42, <c>80 80 80 80 80 80 01 &lt;u32&gt;</c>) when given.
        /// </summary>
        public static byte[] Own(Animation a, UInt32 partner, UInt32 simMs, UInt32? conditionStateFlags = null)
        {
            List<byte> bytes = new List<byte> {0x02, 0x00, (byte) (conditionStateFlags.HasValue ? 0x03 : 0x01), 0x28};
            bytes.AddRange(Block(a, partner));
            AddPosition(bytes, a);
            bytes.AddRange(BitConverter.GetBytes(simMs));
            if (conditionStateFlags.HasValue)
            {
                bytes.AddRange(new byte[] {0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01});
                bytes.AddRange(BitConverter.GetBytes(conditionStateFlags.Value));
            }

            return bytes.ToArray();
        }

        /// <summary>
        /// Another player's view of them, without the view id: <c>&lt;01|03&gt; 2a &lt;block&gt; 00 &lt;pos&gt;</c>, with
        /// ConditionStateFlags (attribute 30, <c>80 80 80 80 04 &lt;u32&gt;</c>) when given. Retail sitting:
        /// <c>04 00 03 2a 0b 40 0a 00 00 10 00 &lt;pos&gt; 80 80 80 80 04 00 00 20 00</c>.
        /// </summary>
        public static byte[] Other(Animation a, UInt32 partner, UInt32? conditionStateFlags = null)
        {
            List<byte> bytes = new List<byte> {(byte) (conditionStateFlags.HasValue ? 0x03 : 0x01), 0x2a};
            bytes.AddRange(Block(a, partner));
            bytes.Add(0x00);
            AddPosition(bytes, a);
            if (conditionStateFlags.HasValue)
            {
                bytes.AddRange(new byte[] {0x80, 0x80, 0x80, 0x80, 0x04});
                bytes.AddRange(BitConverter.GetBytes(conditionStateFlags.Value));
            }

            return bytes.ToArray();
        }

        /// <summary>31 &lt;EmoteID&gt; &lt;RequesterID&gt;: asks the target (handler 0x6217a35b → 0x62252c50 shows a yes/no
        /// dialog, "Interactive Emote"). Retail <c>31 f5 0d 00 58 0f 00 51 00</c>.</summary>
        public static byte[] EmoteRequest(UInt32 emoteId, UInt32 requester)
        {
            byte[] record = new byte[9];
            record[0] = 0x31;
            BitConverter.GetBytes(emoteId).CopyTo(record, 1);
            BitConverter.GetBytes(requester).CopyTo(record, 5);
            return record;
        }

        /// <summary>32 &lt;Result&gt; &lt;EmoteID&gt; &lt;Partner&gt; &lt;WaitTime f32&gt; &lt;IsRequester&gt; (15 bytes): the
        /// requester's client prints the outcome when IsRequester is 1. Retail <c>32 01 f5 0d 00 58 17 00 96 00 00 00 00 00 01</c>.</summary>
        public static byte[] EmoteAnswer(byte result, UInt32 emoteId, UInt32 partner, float waitTime, bool isRequester)
        {
            byte[] record = new byte[15];
            record[0] = 0x32;
            record[1] = result;
            BitConverter.GetBytes(emoteId).CopyTo(record, 2);
            BitConverter.GetBytes(partner).CopyTo(record, 6);
            BitConverter.GetBytes(waitTime).CopyTo(record, 10);
            record[14] = (byte) (isRequester ? 1 : 0);
            return record;
        }
    }
}
