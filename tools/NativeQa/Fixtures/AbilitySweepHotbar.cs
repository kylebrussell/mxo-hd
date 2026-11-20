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

    public static byte[]? FromEnvironment(IReadOnlyList<ushort> loadout, bool required, bool includeWithdraw = false)
    {
        string? path = Environment.GetEnvironmentVariable(TemplateVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Combine(Path.GetTempPath(), "mxo-qa", DisposableTemplate);
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
        return Seed(template, loadout, includeWithdraw);
    }

    internal static void Validate(IReadOnlyList<ushort> abilities, bool includeWithdraw = false)
    {
        int slotCount = abilities.Count + (includeWithdraw ? 1 : 0);
        if (slotCount is < 1 or > Slots || abilities.Distinct().Count() != abilities.Count ||
            abilities.Any(id => id is 0 or >= 4096))
            throw new ArgumentException("The hotbar requires one to ten slots and distinct canonical ability IDs.", nameof(abilities));
    }

    public static byte[] Seed(byte[] template, IReadOnlyList<ushort> abilities, bool includeWithdraw = false)
    {
        Validate(abilities, includeWithdraw);
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

        for (byte slot = 0; slot < abilities.Count; slot++)
        {
            byte[] record = new byte[10];
            record[0] = Page;
            record[1] = slot;
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(6), abilities[slot]);
            records[(Page, slot)] = record;
        }
        if (includeWithdraw)
        {
            byte slot = (byte)abilities.Count;
            // Stock tactic type 7 stores mode, setting and tactic. The Withdraw button selects 2/0/4.
            // client.dll 62262380 reads the tuple, and 62262310 -> 6219ece0 sends 44 01 while engaged.
            records[(Page, slot)] = new byte[] { Page, slot, 7, 0, 0, 0, 2, 0, 4 };
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
