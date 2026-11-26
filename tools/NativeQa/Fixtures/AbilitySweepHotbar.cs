using System.Buffers.Binary;
using hds;

namespace NativeQa;

public static class AbilitySweepHotbar
{
    public const string TemplateVariable = "MXO_NATIVE_QA_CUI_TEMPLATE";
    private const string DisposableTemplate = "cui-template-CSR_AbilityQA-CSR_Tester_6E.cfg";
    private const byte Page = 1; // The stock UI displays zero-based page 1 as page 2.
    private const int Slots = 10;
    private const int SlotRecordsStart = 16;
    private const int SelectionLength = 32;
    internal const uint SingleItemDescriptor = 0x0c000000; // Margin 05 descriptor for one ordinary bag item.
    public static string DefaultTemplatePath => Path.Combine(Path.GetTempPath(), "mxo-qa", DisposableTemplate);

    public sealed record InventoryShortcut(byte BagSlot, uint Goid, string Label);

    public static byte[]? FromEnvironment(IReadOnlyList<ushort> loadout, bool required, bool includeWithdraw = false,
        IReadOnlyList<ushort>? pageThree = null, IReadOnlyList<InventoryShortcut>? inventory = null)
    {
        string? path = Environment.GetEnvironmentVariable(TemplateVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = DefaultTemplatePath;
            if (!File.Exists(path))
            {
                if (required)
                    throw new InvalidOperationException($"Set {TemplateVariable} to a copy of the disposable stock client's cui.cfg.");
                return null;
            }
        }

        using var file = File.OpenRead(path);
        if (file.Length is <= 0 or > QaCharacterUi.MaxLength)
            throw new InvalidDataException("The stock UI template does not fit the client's character UI buffer.");
        byte[] template = new byte[(int)file.Length];
        file.ReadExactly(template);
        return Seed(template, loadout, includeWithdraw, pageThree, inventory);
    }

    /// <summary>
    /// Validates a stock client cui.cfg (or MxO client directory containing Profiles/CSR_AbilityQA/CSR_Tester_6E/cui.cfg)
    /// and installs a disposable copy at <paramref name="destinationPath"/> (default: <see cref="DefaultTemplatePath"/>).
    /// </summary>
    public static (string DestinationPath, int Bytes, uint ShortcutCount) CaptureTemplate(string sourcePath, string? destinationPath = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Supply a stock cui.cfg path or an MxO client directory.", nameof(sourcePath));

        string resolvedSource = Path.GetFullPath(sourcePath);
        if (Directory.Exists(resolvedSource))
        {
            resolvedSource = Path.Combine(resolvedSource, "Profiles", "CSR_AbilityQA", "CSR_Tester_6E", "cui.cfg");
        }
        if (!File.Exists(resolvedSource))
            throw new FileNotFoundException("Stock client cui.cfg not found.", resolvedSource);

        byte[] bytes = File.ReadAllBytes(resolvedSource);
        uint shortcutCount = ValidateTemplate(bytes);

        string destination = Path.GetFullPath(string.IsNullOrWhiteSpace(destinationPath) ? DefaultTemplatePath : destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temp = destination + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, destination, overwrite: true);
        }
        catch
        {
            if (File.Exists(temp)) File.Delete(temp);
            throw;
        }
        return (destination, bytes.Length, shortcutCount);
    }

    public static uint ValidateTemplate(byte[] template)
    {
        if (template.Length is < SlotRecordsStart + SelectionLength or > QaCharacterUi.MaxLength ||
            Read(template, 0) != 2 || Read(template, 8) != 1)
            throw new InvalidDataException("Expected stock cui.cfg version 2 with shortcuts version 1.");

        uint size = Read(template, 4);
        uint count = Read(template, 12);
        if (size < 12 + SelectionLength || size >= template.Length - 4 || count > 200)
            throw new InvalidDataException("Invalid shortcut section size or count.");
        int endOfRecords = 4 + (int)size - SelectionLength;

        var seen = new HashSet<(byte Page, byte Slot)>();
        int cursor = SlotRecordsStart;
        for (int i = 0; i < count; i++)
        {
            RequireBytes(cursor, 6, endOfRecords);
            byte page = template[cursor];
            byte slot = template[cursor + 1];
            int length = RecordLength(template, cursor, endOfRecords);
            if (page >= 20 || slot >= 10 || !seen.Add((page, slot)))
                throw new InvalidDataException("Invalid or duplicate stock shortcut slot.");
            cursor += length;
        }
        if (cursor != endOfRecords)
            throw new InvalidDataException("Unexpected bytes between stock shortcuts and selection state.");
        return count;
    }

    public static IReadOnlyList<string> ManualSetupMessages(NativeQaModePlan plan, string? expectedTemplatePath = null)
    {
        if (!plan.HasHotbar) return Array.Empty<string>();

        string path = expectedTemplatePath ?? DefaultTemplatePath;
        var lines = new List<string>
        {
            $"No disposable stock UI template found at {path} (and {TemplateVariable} is unset). Starting without pre-seeded Margin character UI (type 0c); place shortcuts manually:"
        };

        var page2 = new List<string>();
        for (int i = 0; i < plan.HotbarAbilities.Count; i++)
            page2.Add($"slot {i + 1} = ability {plan.HotbarAbilities[i]} (Actions > Abilities)");
        if (plan.HotbarWithdraw)
            page2.Add($"slot {plan.HotbarAbilities.Count + 1} = Withdraw (Actions > Combat Tactics)");
        int itemStart = plan.HotbarAbilities.Count + (plan.HotbarWithdraw ? 1 : 0) + 1;
        for (int i = 0; i < plan.HotbarInventory.Count; i++)
        {
            var item = plan.HotbarInventory[i];
            page2.Add($"slot {itemStart + i} = {item.Label} (Inventory bag slot {item.BagSlot}, GOID {item.Goid} -> right-click > Add Item to Hotbar)");
        }
        if (page2.Count > 0)
            lines.Add("Manual hotbar page 2: " + string.Join("; ", page2) + ".");

        if (plan.HotbarPageThree.Count > 0)
        {
            var page3 = plan.HotbarPageThree.Select((id, index) => $"slot {index + 1} = ability {id}");
            lines.Add("Manual hotbar page 3: " + string.Join("; ", page3) + ".");
        }

        lines.Add("To restore pre-seeded hotbars after a stock client run: dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll --capture-cui-template \"$env:USERPROFILE\\Developer\\mxo-client\\MxO\"");
        return lines;
    }

    internal static void Validate(IReadOnlyList<ushort> abilities, bool includeWithdraw = false,
        IReadOnlyList<InventoryShortcut>? inventory = null)
    {
        int slotCount = abilities.Count + (includeWithdraw ? 1 : 0) + (inventory?.Count ?? 0);
        if (slotCount is < 1 or > Slots || abilities.Distinct().Count() != abilities.Count ||
            abilities.Any(id => id is 0 or >= 4096))
            throw new ArgumentException("The hotbar requires one to ten shortcuts with distinct canonical ability IDs.", nameof(abilities));
        if (inventory != null && (inventory.Select(item => item.BagSlot).Distinct().Count() != inventory.Count ||
            inventory.Any(item => item.BagSlot is < 1 or > 96 || item.Goid == 0 || string.IsNullOrWhiteSpace(item.Label))))
            throw new ArgumentException("Inventory shortcuts require named items in distinct backpack slots 1 through 96.", nameof(inventory));
    }

    public static byte[] Seed(byte[] template, IReadOnlyList<ushort> abilities, bool includeWithdraw = false,
        IReadOnlyList<ushort>? pageThree = null, IReadOnlyList<InventoryShortcut>? inventory = null)
    {
        Validate(abilities, includeWithdraw, inventory);
        if (pageThree is { Count: > 0 }) Validate(pageThree);
        if (template.Length is < SlotRecordsStart + SelectionLength or > QaCharacterUi.MaxLength ||
            Read(template, 0) != 2 || Read(template, 8) != 1)
            throw new InvalidDataException("Expected stock cui.cfg version 2 with shortcuts version 1.");

        uint size = Read(template, 4);
        uint count = Read(template, 12);
        if (size < 12 + SelectionLength || size >= template.Length - 4 || count > 200)
            throw new InvalidDataException("Invalid shortcut section size or count.");
        int endOfRecords = 4 + (int)size - SelectionLength;

        var records = new SortedDictionary<(byte Page, byte Slot), byte[]>();
        int cursor = SlotRecordsStart;
        for (int i = 0; i < count; i++)
        {
            RequireBytes(cursor, 6, endOfRecords);
            byte page = template[cursor];
            byte slot = template[cursor + 1];
            int length = RecordLength(template, cursor, endOfRecords);
            if (page >= 20 || slot >= 10 || !records.TryAdd((page, slot), template.AsSpan(cursor, length).ToArray()))
                throw new InvalidDataException("Invalid or duplicate stock shortcut slot.");
            cursor += length;
        }
        if (cursor != endOfRecords)
            throw new InvalidDataException("Unexpected bytes between stock shortcuts and selection state.");

        WritePage(Page, abilities);
        if (pageThree is { Count: > 0 }) WritePage(Page + 1, pageThree);
        if (includeWithdraw)
        {
            byte slot = (byte)abilities.Count;
            // Stock tactic type 7 stores mode, setting and tactic. The Withdraw button selects 2/0/4.
            // client.dll 62262380 reads the tuple, and 62262310 -> 6219ece0 sends 44 01 while engaged.
            records[(Page, slot)] = new byte[] { Page, slot, 7, 0, 0, 0, 2, 0, 4 };
        }
        if (inventory != null)
        {
            byte slot = (byte)(abilities.Count + (includeWithdraw ? 1 : 0));
            foreach (var item in inventory)
            {
                // client.dll 62262c90 constructs type 1. 62262970/622629b0 write/read slot, GOID and instance data.
                byte[] record = new byte[18];
                record[0] = Page;
                record[1] = slot;
                record[2] = 1;
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(6), item.BagSlot);
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(10), item.Goid);
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(14), SingleItemDescriptor);
                records[(Page, slot)] = record;
                slot++;
            }
        }

        int recordBytes = records.Values.Sum(record => record.Length);
        int outputLength = SlotRecordsStart + recordBytes + template.Length - endOfRecords;
        if (outputLength > QaCharacterUi.MaxLength)
            throw new InvalidDataException("The seeded UI would exceed the stock character UI buffer.");
        byte[] output = new byte[outputLength];
        template.AsSpan(0, SlotRecordsStart).CopyTo(output);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4), (uint)(12 + recordBytes + SelectionLength));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(12), (uint)records.Count);
        int outputOffset = SlotRecordsStart;
        foreach (byte[] record in records.Values)
        {
            record.CopyTo(output, outputOffset);
            outputOffset += record.Length;
        }
        template.AsSpan(endOfRecords).CopyTo(output.AsSpan(outputOffset));
        return output;

        void WritePage(byte page, IReadOnlyList<ushort> shortcuts)
        {
            for (byte slot = 0; slot < shortcuts.Count; slot++)
            {
                byte[] record = new byte[10];
                record[0] = page;
                record[1] = slot;
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(6), shortcuts[slot]);
                records[(page, slot)] = record;
            }
        }
    }

    private static int RecordLength(byte[] template, int start, int end)
    {
        uint type = Read(template, start + 2);
        int length = type switch
        {
            0 => 10,
            1 or 2 or 3 or 5 or 6 => 18,
            4 => MacroLength(template, start, end),
            7 => 9,
            _ => throw new InvalidDataException($"Unknown stock shortcut type {type}.")
        };
        RequireBytes(start, length, end);
        return length;
    }

    private static int MacroLength(byte[] template, int start, int end)
    {
        int offset = start + 6;
        RequireBytes(offset, 4, end);
        uint marker = Read(template, offset);
        if (marker == uint.MaxValue)
            return SkipWideString(template, SkipWideString(template, offset + 4, end), end) - start;
        if (marker >= 0x80000000u)
            throw new InvalidDataException("Invalid stock macro marker.");
        return SkipWideString(template, offset, end) - start;
    }

    private static int SkipWideString(byte[] template, int offset, int end)
    {
        RequireBytes(offset, 4, end);
        uint units = Read(template, offset);
        offset += 4;
        if (units > (uint)((end - offset) / 2))
            throw new InvalidDataException("Truncated stock macro string.");
        return offset + 2 * (int)units;
    }

    private static uint Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));

    private static void RequireBytes(int cursor, int size, int end)
    {
        if (size > end - cursor)
            throw new InvalidDataException("Truncated stock shortcut record.");
    }
}
