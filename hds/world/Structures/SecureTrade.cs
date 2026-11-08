using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace hds
{
    /// <summary>
    /// Secure trade records, read from client.dll's RPC classes and handlers: no capture of a trade exists, so
    /// see docs/SECURE-TRADE.md for what is certain (layouts, status codes, what each record does on arrival)
    /// and what is a guess (who is sent what and when, the server's checks).
    /// </summary>
    public static class TradePackets
    {
        /// <summary>Slot value meaning "no item" in the trade records.</summary>
        public const byte NoSlot = 0x6b;

        // 81 83: answer to a trade request
        public const UInt32 RequestOk = 0;
        public const UInt32 RequestTooFar = 0x18000002;
        public const UInt32 RequestNoSuchItem = 0x18000003;
        public const UInt32 RequestNotTradeable = 0x18000004;
        public const UInt32 RequestNotAPlayer = 0x18000005;
        public const UInt32 RequestInCombat = 0x18000006;
        public const UInt32 RequestDead = 0x18000007;
        public const UInt32 RequestInProgress = 0x18000008;
        public const UInt32 RequestEquipped = 0x18000009;

        // 81 87: session status. The client closes its window on Complete, Cancelled and Aborted only.
        public const UInt32 StatusComplete = 0x8e000019;
        public const UInt32 StatusCancelled = 0x8e00001b;
        public const UInt32 StatusYouAccepted = 0x8e00001c;
        public const UInt32 StatusTheyAccepted = 0x8e00001d;
        public const UInt32 StatusYouReset = 0x8e00001e;
        public const UInt32 StatusTheyReset = 0x8e00001f;
        public const UInt32 StatusAborted = 0x0e000023;

        private static byte[] Record(byte opcode, Action<BinaryWriter> fields, string text = null)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x81);
                writer.Write(opcode);
                fields(writer);
                if (text != null)
                {
                    // The string field holds an offset from the record's start; there: u16 length with the NUL, then the bytes
                    byte[] bytes = Encoding.ASCII.GetBytes(text);
                    writer.Write((UInt16)(bytes.Length + 1));
                    writer.Write(bytes);
                    writer.Write((byte)0);
                }
                return stream.ToArray();
            }
        }

        /// <summary><c>81 83</c>, to the requester: status 0 opens their window and echoes what they offered.</summary>
        public static byte[] BuildRequestResult(string targetName, UInt32 status, UInt32 info, byte slot, byte itemQuantity)
        {
            const UInt16 fixedSize = 0x0f;
            return Record(0x83, w =>
            {
                w.Write(fixedSize);
                w.Write(status);
                w.Write((byte)(info != 0 ? 1 : 0));
                w.Write(info);
                w.Write(slot);
                w.Write(itemQuantity);
            }, targetName);
        }

        /// <summary><c>81 84</c>, to the target: opens their window with the partner's first offer. Class 0 = no item.</summary>
        public static byte[] BuildOffered(string partnerName, UInt32 info, UInt32 classData, UInt32 instanceData, byte itemQuantity)
        {
            const UInt16 fixedSize = 0x12;
            return Record(0x84, w =>
            {
                w.Write(fixedSize);
                w.Write((byte)(info != 0 ? 1 : 0));
                w.Write(info);
                w.Write(classData);
                w.Write(instanceData);
                w.Write(itemQuantity);
            }, partnerName);
        }

        /// <summary><c>81 86</c>: an offer changed. The client also unticks its accept box. Own items go by slot, the partner's by class + instance.</summary>
        public static byte[] BuildChanged(bool add, bool yours, UInt32 info, byte slot, UInt32 classData, UInt32 instanceData, byte itemQuantity)
        {
            return Record(0x86, w =>
            {
                w.Write((byte)(add ? 1 : 0));
                w.Write((byte)(yours ? 1 : 0));
                w.Write((byte)(info != 0 ? 1 : 0));
                w.Write(info);
                w.Write(slot);
                w.Write(classData);
                w.Write(instanceData);
                w.Write(itemQuantity);
            });
        }

        /// <summary><c>81 87</c>: session status; the counts and the signed info change feed the client's trade summary.</summary>
        public static byte[] BuildStatus(UInt32 status, UInt32 itemsGiven = 0, UInt32 itemsReceived = 0, long infoChange = 0)
        {
            return Record(0x87, w =>
            {
                w.Write(status);
                w.Write(itemsGiven);
                w.Write(itemsReceived);
                w.Write(infoChange);
            });
        }

        /// <summary>The item's 4-byte descriptor with its stack amount, as the inventory records carry it (00 00 amount type).</summary>
        public static UInt32 Descriptor(UInt32 purity, int count)
        {
            UInt32 descriptor = purity != 0 ? purity : 0x0c000000;
            return (descriptor & 0xff00ffff) | ((UInt32)ItemStacks.AmountByte(count) << 16);
        }
    }

    /// <summary>What the client's 81 82 and 81 85 say. <c>rpcData</c> is the record after its two opcode bytes.</summary>
    public static class TradeRequests
    {
        public const byte ActionAdd = 0, ActionRemove = 1, ActionClear = 2, ActionCancel = 3, ActionAccept = 4, ActionResetAccept = 5;

        public struct Request
        {
            public UInt32 TargetId;
            public UInt32 Info;
            public byte Slot;
            public byte ItemQuantity;
        }

        public struct Change
        {
            public byte Action;
            public bool InfoTrade;
            public UInt32 Info;
            public byte Slot;
            public byte ItemQuantity;
        }

        public static bool TryParseRequest(byte[] rpcData, out Request request)
        {
            request = new Request();
            if (rpcData == null || rpcData.Length < 11)
            {
                return false;
            }
            request.TargetId = BitConverter.ToUInt32(rpcData, 0);
            request.Info = rpcData[4] != 0 ? BitConverter.ToUInt32(rpcData, 5) : 0;
            request.Slot = rpcData[9];
            request.ItemQuantity = rpcData[10];
            return true;
        }

        public static bool TryParseChange(byte[] rpcData, out Change change)
        {
            change = new Change();
            if (rpcData == null || rpcData.Length < 8)
            {
                return false;
            }
            change.Action = rpcData[0];
            change.InfoTrade = rpcData[1] != 0;
            change.Info = BitConverter.ToUInt32(rpcData, 2);
            change.Slot = rpcData[6];
            change.ItemQuantity = rpcData[7];
            return true;
        }
    }

    public sealed class TradeItem
    {
        public byte Slot;
        public UInt32 Goid;
        public UInt32 Purity;
        public int Quantity;
    }

    /// <summary>One side's offer. Any change to either offer clears both accepts, as the client's window does.</summary>
    public sealed class TradeOffer
    {
        public long Info;
        public bool Accepted;
        public readonly Dictionary<byte, TradeItem> Items = new Dictionary<byte, TradeItem>();
        public bool IsEmpty => Info == 0 && Items.Count == 0;
    }

    public sealed class TradeSession
    {
        public readonly WorldClient Requester, Target;
        public readonly TradeOffer RequesterOffer = new TradeOffer(), TargetOffer = new TradeOffer();

        public TradeSession(WorldClient requester, WorldClient target)
        {
            Requester = requester;
            Target = target;
        }

        public WorldClient PartnerOf(WorldClient client) => client == Requester ? Target : Requester;
        public TradeOffer OfferOf(WorldClient client) => client == Requester ? RequesterOffer : TargetOffer;
        public bool BothAccepted => RequesterOffer.Accepted && TargetOffer.Accepted;

        public void ClearAccepts()
        {
            RequesterOffer.Accepted = false;
            TargetOffer.Accepted = false;
        }
    }
}
