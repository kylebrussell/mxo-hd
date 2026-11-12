using System;
using System.Collections.Generic;
using hds.shared;

namespace hds
{
    /// <summary>
    /// Central storage: moving codes and code fragments one bag slot at a time between the bag and the code archive
    /// (see <see cref="CodeStorage"/>), from the Loading Area's "Central Storage" window. No retail capture has any of
    /// this; the records are client.dll's (7.6005), the rules are ours where the client is silent. See
    /// docs/CENTRAL-STORAGE.md.
    /// <list type="bullet">
    /// <item>Central storage is the known-abilities array (<c>0x629ea4e8 + 0x6dc</c>, 0x208 entries of 8 bytes, the bound every
    /// handler checks), shared with the abilities themselves. An entry is an ability when bit 31 is set and bit 26 clear,
    /// otherwise a stored code <c>0x84000000 | goid &lt;&lt; 7 | (count - 1)</c>. The server keeps it in char_abilities
    /// (slots below <see cref="CharacterAttributes.FirstSlot"/>, also 0x208).</item>
    /// <item><c>62 &lt;SourceSlot u16&gt; &lt;DestinationSlot u16&gt; &lt;PersonalInventoryItem u8&gt;</c> (class 0x621d2590, size 6).
    /// PersonalInventoryItem 1 (sender 0x621e2000): a bag slot into storage slot DestinationSlot, 6b for "any". The client
    /// refuses anything that isn't a code before sending ("Upload Failed: Only codes and code fragments may be placed in
    /// Central Storage."). PersonalInventoryItem 0 (sender 0x621d4c00): a storage slot into bag slot DestinationSlot (a byte),
    /// 6b when the client found no free slot.</item>
    /// <item>The answers are the existing item records: <c>5e</c>/<c>5f</c>/<c>69</c> with PersonalInventoryItem 1 for the bag and 0
    /// for storage (the s-&gt;c handlers 0x6217983f, 0x621798f1 branch on it), and for a bag-to-storage move the upload result
    /// <c>81 18 &lt;SingleItemUploaded u8&gt; &lt;AbilityCodeUploaded u8&gt; &lt;Status u32&gt;</c> (handler 0x621e1650).</item>
    /// </list>
    /// </summary>
    public static class CentralStorage
    {
        /// <summary>Entries in the client's array, abilities included.</summary>
        public const int Slots = 0x208;

        public const byte NoSlot = 0x6b;

        // Status codes: (success << 31) | (category << 24) | id, names from the client's registrations (0x622a6480).
        public const UInt32 Success = 0;
        public const UInt32 PartialUpload = 0x8e000085;              // LTOBJSRV_PARTIALUPLOAD
        public const UInt32 InvalidStorageLocation = 0x0c000259;     // LTPS_SPINVALIDCENTRALSTORAGELOCATION
        public const UInt32 ItemNotStackable = 0x0c00025f;           // LTPS_SPITEMNOTSTACKABLE
        public const UInt32 InvalidBagLocation = 0x0c000261;         // LTPS_SPINVALIDPERSONALINVLOCATION
        public const UInt32 InvalidBagItem = 0x0c00028e;             // LTPS_SPINVALIDPERSONALINVITEM
        public const UInt32 StorageEmpty = 0x0c000294;               // LTPS_SPCENTRALSTORAGEEMPTY
        public const UInt32 StorageNotEmpty = 0x0c000295;            // LTPS_SPCENTRALSTORAGENOTEMPTY
        public const UInt32 InvalidTransaction = 0x0c00029c;         // LTPS_SPINVALIDINVENTORYTRANSACTION
        public const UInt32 BagEmpty = 0x0c00029d;                   // LTPS_SPPERSONALINVEMPTY
        public const UInt32 BagNotEmpty = 0x0c00029e;                // LTPS_SPPERSONALINVNOTEMPTY
        public const UInt32 InvalidStorageItem = 0x0c00029f;         // LTPS_SPINVALIDCENTRALSTORAGEITEM
        public const UInt32 InvalidItemType = 0x0c0002a7;            // LTPS_SPINVALIDITEMTYPE
        public const UInt32 StorageFull = 0x0c0002c8;                // LTPS_SPCENTRALSTORAGEFULL
        public const UInt32 BagFull = 0x0c0002ca;                    // LTPS_SPPERSONALINVENTORYFULL

        public static string NameOf(UInt32 status)
        {
            switch (status)
            {
                case Success: return "SUCCESS";
                case PartialUpload: return "LTOBJSRV_PARTIALUPLOAD";
                case InvalidStorageLocation: return "LTPS_SPINVALIDCENTRALSTORAGELOCATION";
                case ItemNotStackable: return "LTPS_SPITEMNOTSTACKABLE";
                case InvalidBagLocation: return "LTPS_SPINVALIDPERSONALINVLOCATION";
                case InvalidBagItem: return "LTPS_SPINVALIDPERSONALINVITEM";
                case StorageEmpty: return "LTPS_SPCENTRALSTORAGEEMPTY";
                case StorageNotEmpty: return "LTPS_SPCENTRALSTORAGENOTEMPTY";
                case InvalidTransaction: return "LTPS_SPINVALIDINVENTORYTRANSACTION";
                case BagEmpty: return "LTPS_SPPERSONALINVEMPTY";
                case BagNotEmpty: return "LTPS_SPPERSONALINVNOTEMPTY";
                case InvalidStorageItem: return "LTPS_SPINVALIDCENTRALSTORAGEITEM";
                case InvalidItemType: return "LTPS_SPINVALIDITEMTYPE";
                case StorageFull: return "LTPS_SPCENTRALSTORAGEFULL";
                case BagFull: return "LTPS_SPPERSONALINVENTORYFULL";
                default: return "0x" + status.ToString("x8");
            }
        }

        /// <summary>Plain words for a refusal, for the chat line a storage-to-bag move gets (the client has no record for it).</summary>
        public static string Describe(UInt32 status)
        {
            switch (status)
            {
                case InvalidStorageLocation: return "that isn't a central storage slot for code";
                case ItemNotStackable: return "that item doesn't stack";
                case InvalidBagLocation: return "that isn't a bag slot";
                case InvalidBagItem: return "that bag item can't be stored";
                case StorageEmpty: return "that central storage slot is empty";
                case StorageNotEmpty: return "that central storage slot holds something else";
                case InvalidTransaction: return "central storage is only open in the Loading Area";
                case BagEmpty: return "that bag slot is empty";
                case BagNotEmpty: return "that bag slot holds something else";
                case InvalidStorageItem: return "that central storage slot holds an ability";
                case InvalidItemType: return "only codes and code fragments may be placed in central storage";
                case StorageFull: return "central storage is full";
                case BagFull: return "your inventory is full";
                default: return NameOf(status);
            }
        }

        public enum Kind
        {
            Empty,
            Ability,
            Code
        }

        /// <summary>A storage slot's contents.</summary>
        public struct Entry
        {
            public Kind Kind;
            public UInt32 Goid;
            public int Count;

            public static Entry Code(UInt32 goid, int count) => new Entry { Kind = Kind.Code, Goid = goid, Count = count };
            public static readonly Entry Ability = new Entry { Kind = Kind.Ability };
        }

        /// <summary>A 62 request.</summary>
        public class Move
        {
            public UInt16 Source, Dest;
            public bool FromBag;
        }

        public static bool TryParseMove(byte[] rpcData, out Move move)
        {
            move = null;
            if (rpcData == null || rpcData.Length < 5)
            {
                return false;
            }
            move = new Move
            {
                Source = BitConverter.ToUInt16(rpcData, 0), Dest = BitConverter.ToUInt16(rpcData, 2), FromBag = rpcData[4] != 0
            };
            return true;
        }

        /// <summary>
        /// What a request does. A refusal has a non-zero <see cref="Status"/> other than <see cref="PartialUpload"/>. Counts
        /// are after the request; 0 empties the slot.
        /// </summary>
        public class Plan
        {
            public UInt32 Status;
            public UInt16 BagSlot, StorageSlot;
            public int BagCount, StorageCount, Moved;
            public bool BagIsNew, StorageIsNew;

            /// <summary>An unknown ability's code uploaded: the character learns it in <see cref="StorageSlot"/> (the bulk upload does the same).</summary>
            public bool LearnAbility;

            /// <summary>Storage to storage: the source slot and its count after.</summary>
            public UInt16 SourceSlot;
            public int SourceCount;

            public bool Refused => Status != Success && Status != PartialUpload;

            public static Plan Refuse(UInt32 status) => new Plan { Status = status };
        }

        public static bool IsStorageSlot(int slot) => slot >= 0 && slot < Slots;

        public static bool IsBagSlot(int slot) => slot >= 0 && slot < StackMoves.FirstWornSlot;

        /// <summary>How many of an item a storage entry holds (seven count bits) and a bag slot holds.</summary>
        public static int StorageMax => CodeStorage.MaxCount;

        public static int BagMax(UInt32 goid) => ItemStacks.IsStackable(goid) ? ItemStacks.MaxStack : 1;

        /// <summary>
        /// 62 with PersonalInventoryItem 1: the bag slot's whole stack into storage. With no destination (6b) it joins the
        /// lowest entry of the same item that has room, otherwise the first free slot. Onto an entry of the same item it adds
        /// what fits (the rest stays in the bag: <see cref="PartialUpload"/>). An occupied slot of another kind is refused
        /// (retail's NOTEMPTY codes; the client can't show a swap inside storage, its 65 handler for central storage is an
        /// empty stub at 0x621cd390).
        /// </summary>
        public static Plan PlanUpload(Move move, Func<int, StackMoves.Stack> bagAt, Func<int, Entry> storageAt,
            Func<int?> firstFreeStorage, Func<UInt32, bool> knowsAbility)
        {
            if (!IsBagSlot(move.Source)) return Plan.Refuse(InvalidBagLocation);
            StackMoves.Stack bag = bagAt(move.Source);
            if (bag.Goid == 0) return Plan.Refuse(BagEmpty);
            if (move.Dest != NoSlot && !IsStorageSlot(move.Dest)) return Plan.Refuse(InvalidStorageLocation);

            if (AbilityProgression.IsAbilityCode(bag.Goid))
            {
                // A bought ability's code: the character learns it, as the bulk upload does
                if (knowsAbility(bag.Goid)) return Plan.Refuse(InvalidBagItem);
                int? slot = move.Dest == NoSlot ? firstFreeStorage() : (int?)move.Dest;
                if (slot == null) return Plan.Refuse(StorageFull);
                if (storageAt(slot.Value).Kind != Kind.Empty) return Plan.Refuse(StorageNotEmpty);
                return new Plan
                {
                    BagSlot = move.Source, BagCount = 0, StorageSlot = (UInt16)slot.Value, StorageCount = 1, StorageIsNew = true,
                    Moved = bag.Count, LearnAbility = true
                };
            }

            if (!CodeStorage.IsUploadable(bag.Goid)) return Plan.Refuse(InvalidItemType);

            int target;
            if (move.Dest == NoSlot)
            {
                int? same = null;
                for (int slot = 0; slot < Slots; slot++)
                {
                    Entry entry = storageAt(slot);
                    if (entry.Kind == Kind.Code && entry.Goid == bag.Goid && entry.Count < StorageMax)
                    {
                        same = slot;
                        break;
                    }
                }
                int? free = same ?? firstFreeStorage();
                if (free == null) return Plan.Refuse(StorageFull);
                target = free.Value;
            }
            else
            {
                target = move.Dest;
            }

            Entry dest = storageAt(target);
            if (dest.Kind == Kind.Ability) return Plan.Refuse(InvalidStorageLocation);
            if (dest.Kind == Kind.Code && dest.Goid != bag.Goid) return Plan.Refuse(StorageNotEmpty);
            int had = dest.Kind == Kind.Code ? dest.Count : 0;
            int moved = Math.Min(bag.Count, StorageMax - had);
            if (moved <= 0) return Plan.Refuse(StorageFull);
            return new Plan
            {
                Status = moved < bag.Count ? PartialUpload : Success,
                BagSlot = move.Source, BagCount = bag.Count - moved,
                StorageSlot = (UInt16)target, StorageCount = had + moved, StorageIsNew = dest.Kind == Kind.Empty, Moved = moved
            };
        }

        /// <summary>
        /// 62 with PersonalInventoryItem 0: as much of the storage entry as one bag slot holds, into the bag. With no destination
        /// (6b) it joins the lowest bag stack of the same item with room, otherwise the first free bag slot. Onto a stack of the
        /// same item it adds what fits; another item there is refused.
        /// </summary>
        public static Plan PlanDownload(Move move, Func<int, StackMoves.Stack> bagAt, Func<int, Entry> storageAt,
            IEnumerable<int> bagSlotsInOrder, Func<int?> firstFreeBag)
        {
            if (!IsStorageSlot(move.Source)) return Plan.Refuse(InvalidStorageLocation);
            Entry entry = storageAt(move.Source);
            if (entry.Kind == Kind.Empty) return Plan.Refuse(StorageEmpty);
            if (entry.Kind == Kind.Ability) return Plan.Refuse(InvalidStorageItem);
            int max = BagMax(entry.Goid);

            int target;
            if (move.Dest == NoSlot)
            {
                int? same = null;
                if (max > 1)
                {
                    foreach (int slot in bagSlotsInOrder)
                    {
                        StackMoves.Stack stack = bagAt(slot);
                        if (stack.Goid == entry.Goid && stack.Count < max)
                        {
                            same = slot;
                            break;
                        }
                    }
                }
                int? free = same ?? firstFreeBag();
                if (free == null) return Plan.Refuse(BagFull);
                target = free.Value;
            }
            else
            {
                if (!IsBagSlot(move.Dest)) return Plan.Refuse(InvalidBagLocation);
                target = move.Dest;
            }

            StackMoves.Stack dest = bagAt(target);
            if (dest.Goid != 0 && dest.Goid != entry.Goid) return Plan.Refuse(BagNotEmpty);
            if (dest.Goid != 0 && max == 1) return Plan.Refuse(ItemNotStackable);
            int moved = Math.Min(entry.Count, max - dest.Count);
            if (moved <= 0) return Plan.Refuse(BagFull);
            return new Plan
            {
                StorageSlot = move.Source, StorageCount = entry.Count - moved,
                BagSlot = (UInt16)target, BagCount = dest.Count + moved, BagIsNew = dest.Goid == 0, Moved = moved
            };
        }

        /// <summary>67 with IsCentralStorageItem: a storage stack dropped on a storage stack of the same item (0x620a0880).</summary>
        public static Plan PlanMerge(UInt16 source, UInt16 dest, Func<int, Entry> storageAt)
        {
            if (!IsStorageSlot(source) || !IsStorageSlot(dest) || source == dest) return Plan.Refuse(InvalidStorageLocation);
            Entry from = storageAt(source), to = storageAt(dest);
            if (from.Kind == Kind.Empty) return Plan.Refuse(StorageEmpty);
            if (from.Kind != Kind.Code || to.Kind == Kind.Ability) return Plan.Refuse(InvalidStorageItem);
            if (to.Kind == Kind.Empty) return Plan.Refuse(StorageEmpty);
            if (from.Goid != to.Goid) return Plan.Refuse(StorageNotEmpty);
            int moved = Math.Min(from.Count, StorageMax - to.Count);
            if (moved <= 0) return Plan.Refuse(StorageFull);
            return new Plan
            {
                SourceSlot = source, SourceCount = from.Count - moved, StorageSlot = dest, StorageCount = to.Count + moved, Moved = moved
            };
        }

        /// <summary>
        /// 68 with IsCentralStorageItem: the split dialog's amount from a storage stack dropped on a storage slot (0x620a07ba).
        /// The client writes both slots as bytes. Into an empty slot (or the first free one for 6b) it makes a new entry; onto
        /// the same item it adds while the entry has room.
        /// </summary>
        public static Plan PlanSplit(UInt16 source, UInt16 dest, UInt32 amount, Func<int, Entry> storageAt, Func<int?> firstFreeStorage)
        {
            if (!IsStorageSlot(source)) return Plan.Refuse(InvalidStorageLocation);
            Entry from = storageAt(source);
            if (from.Kind == Kind.Empty) return Plan.Refuse(StorageEmpty);
            if (from.Kind != Kind.Code) return Plan.Refuse(InvalidStorageItem);
            if (amount < 1 || amount > from.Count) return Plan.Refuse(InvalidTransaction);

            int target;
            if (dest == NoSlot || dest == source)
            {
                int? free = firstFreeStorage();
                if (free == null) return Plan.Refuse(StorageFull);
                target = free.Value;
            }
            else
            {
                if (!IsStorageSlot(dest)) return Plan.Refuse(InvalidStorageLocation);
                target = dest;
            }

            Entry to = storageAt(target);
            if (to.Kind == Kind.Ability) return Plan.Refuse(InvalidStorageLocation);
            if (to.Kind == Kind.Code && to.Goid != from.Goid) return Plan.Refuse(StorageNotEmpty);
            int had = to.Kind == Kind.Code ? to.Count : 0;
            if (had + amount > StorageMax) return Plan.Refuse(StorageFull);
            return new Plan
            {
                SourceSlot = source, SourceCount = from.Count - (int)amount, StorageSlot = (UInt16)target, StorageCount = had + (int)amount,
                StorageIsNew = to.Kind == Kind.Empty, Moved = (int)amount
            };
        }

        /// <summary>The lowest storage slot no ability or code holds.</summary>
        public static int? FirstFreeSlot(ICollection<int> used)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                if (!used.Contains(slot))
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary><c>5f &lt;slot u16&gt; 00</c>: a storage entry goes (the client clears it with 0x621e1b20 and drops it from the window).</summary>
        public static byte[] BuildRemove(UInt16 slot)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x5f);
            pak.AddUint16(slot, 1);
            pak.AddByte(0x00);
            return pak.ReturnFinalPacket();
        }

        /// <summary>
        /// <c>81 18 &lt;SingleItemUploaded&gt; &lt;AbilityCodeUploaded&gt; &lt;Status u32&gt;</c>. The client shows "Upload Status" with
        /// "Item successfully uploaded!" (one item, success), "Items successfully uploaded!", the partial text for
        /// <see cref="PartialUpload"/>, "Your upload exceeds the maximum capacity of the code archive." for
        /// <see cref="StorageFull"/>, or "Item(s) could not be uploaded!" for any other error.
        /// </summary>
        public static byte[] BuildUploadResult(int items, bool abilityCode, UInt32 status)
        {
            PacketContent pak = new PacketContent();
            pak.AddByte(0x81);
            pak.AddByte(0x18);
            pak.AddByte((byte)(items == 1 ? 1 : 0));
            pak.AddByte((byte)(abilityCode ? 1 : 0));
            pak.AddUint32(status, 1);
            return pak.ReturnFinalPacket();
        }
    }
}
