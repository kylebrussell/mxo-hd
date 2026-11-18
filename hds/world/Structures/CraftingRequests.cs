using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>Layouts read from 7.6005 client.dll, not a scan for a plausible recipe inside an ability request.</summary>
    public static class CraftingRequests
    {
        // Writer 81 2f dispatch at 6208c9c8. Zero is success; 1 would report lost fragments.
        public const byte WriterSuccess = 0, WriterNoSpace = 3, WriterNoInfo = 4, WriterRefused = 5;

        public sealed class Compile
        {
            public uint SourceCode;
            public byte ToolSlot;
            public readonly Dictionary<uint, int> Fragments = new Dictionary<uint, int>();
            public readonly Dictionary<(ushort Slot, uint Goid), int> SelectedIngredients = new Dictionary<(ushort, uint), int>();
            public uint Item => SourceItem(SourceCode);
        }
        public sealed class WrittenCode
        {
            public byte Result;
            public ushort Slot;
            public long Cash;
            public readonly List<(Crafting.InventoryItem Item, int Remaining)> Ingredients =
                new List<(Crafting.InventoryItem, int)>();
        }

        public sealed class InventoryAbility
        {
            public uint Ability;
            public byte Slot, ToolSlot;
            public bool IsRecipeKnown;
        }

        // 6208c13d..c169: known ability packing versus non-ability code (goid << 7 | charges).
        public static uint SourceItem(uint source) => (source & 0x84000000) == 0x80000000
            ? 0x80000000u | (source & 0x003ffc00u) : (source >> 7) & 0x7ffffu;

        /// <summary>81 2e: source u32, array offset u16, tool u8, count u16, then eight-byte ingredient rows.</summary>
        public static Compile ParseCompile(byte[] body, bool writer = false)
        {
            if (body == null || body.Length < 7) return null;
            var request = new Compile { SourceCode = BitConverter.ToUInt32(body, 0), ToolSlot = body[6] };
            int offset = BitConverter.ToUInt16(body, 4);
            if (offset == 0) return body.Length == 7 ? request : null;
            int at = offset - 2; // offsets include the two opcode bytes removed by RpcManager
            if (at < 7 || at + 2 > body.Length) return null;
            int count = BitConverter.ToUInt16(body, at);
            if (count > 96 || at + 2 + count * 8 != body.Length) return null;
            for (int i = 0; i < count; i++)
            {
                int entry = at + 2 + i * 8;
                uint item = BitConverter.ToUInt32(body, entry + 4);
                if (item == 0) return null;
                if (!writer)
                {
                    uint amount = BitConverter.ToUInt32(body, entry);
                    if (amount == 0 || amount > 128 || request.Fragments.ContainsKey(item)) return null;
                    request.Fragments[item] = (int)amount;
                    continue;
                }

                // 6208b451 / 6208b558: the high bits select bag or archive. The client leaves
                // the low 14 bits of this word alone. Each row accounts for one ingredient.
                int source = BitConverter.ToUInt16(body, entry) & 0xc000;
                ushort slot = BitConverter.ToUInt16(body, entry + 2);
                switch (source)
                {
                    case 0x8000 when Crafting.IsBagSlot(slot):
                        break;
                    case 0x4000 when slot < CentralStorage.Slots:
                        slot |= Crafting.StorageSlotFlag;
                        break;
                    default:
                        return null;
                }
                var selection = (slot, item);
                request.SelectedIngredients[selection] = request.SelectedIngredients.GetValueOrDefault(selection) + 1;
                request.Fragments[item] = request.Fragments.GetValueOrDefault(item) + 1;
            }
            return request;
        }

        /// <summary>621d3b00/621ed223: a base ability targeting an inventory slot, with the tool and recipe UI hint.</summary>
        public static InventoryAbility ParseInventoryAbility(byte[] body) => body?.Length == 7 && body[6] <= 1
            ? new InventoryAbility { Ability = BitConverter.ToUInt32(body, 0), Slot = body[4], ToolSlot = body[5],
                IsRecipeKnown = body[6] != 0 } : null;

        // 621ed177..193 calls62515bb0 (CastValidTarget) and requires7 before producing81 31.
        // In particular970 is a target0 tool toggle;37 is its parent skill, not a wire alias.
        public static bool IsInventoryTargetAbility(uint ability) => ability <= ushort.MaxValue &&
            AbilityCatalog.Find((ushort)ability)?.Number("Ability/CastValidTarget", -1) == 7;

        public static bool Matches(Compile request, Crafting.Recipe recipe)
        {
            var needed = recipe.Ingredients.GroupBy(i => i).ToDictionary(g => g.Key, g => g.Count());
            return request.Item == recipe.ResultGoid && request.Fragments.Count == needed.Count &&
                needed.All(pair => request.Fragments.TryGetValue(pair.Key, out int count) && count == pair.Value);
        }

        // 6214225d: twelve bytes; 6208cad8 passes XPGained as a float to the UI.
        public static byte[] BuildResult(byte result, byte slot, uint item, float xp = 0)
        {
            byte[] packet = new byte[12];
            packet[0] = 0x81; packet[1] = 0x2f; packet[2] = result; packet[3] = slot;
            BitConverter.GetBytes(item).CopyTo(packet, 4);
            BitConverter.GetBytes(xp).CopyTo(packet, 8);
            return packet;
        }
    }
}
