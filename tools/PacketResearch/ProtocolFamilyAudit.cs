namespace MxoHd.PacketResearch;

/// <summary>Accounting over existing framed parsers. Partial 03 records remain partial; no byte-pattern rescan.</summary>
public static class ProtocolFamilyAudit
{
    public static object Summarize(IEnumerable<PacketDumpFileSummary> files)
    {
        var rows = files.OrderBy(file => file.File, StringComparer.Ordinal).Select(file => new
        {
            File = file.File.Replace('\\','/'),
            file.PacketLikeLines,
            file.ProtocolCounts,
            ManageBonusTotal = file.ManageBonusPayloads.Count,
            ManageBonusDecoded = file.ManageBonusPayloads.Count(p => p.NativeFields != null),
            ManageBonusOpaque = file.ManageBonusPayloads.Count(p => p.NativeFields == null),
            ManageBonusFamilies = file.ManageBonusPayloads.GroupBy(p => p.NativeFields is {} n
                ? $"{n.Family:x4}" : $"opaque-length-{p.PayloadLength}").OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count()),
            NativeExamples = file.ManageBonusPayloads.Where(p => p.NativeFields != null)
                .GroupBy(p => p.NativeFields!.Family).OrderBy(g => g.Key)
                .Select(g => new { g.First().Line, g.First().PayloadHex, Fields = g.First().NativeFields }).ToArray(),
            Protocol03Total = file.Protocol03ObjectViews.Count,
            Protocol03Complete = file.Protocol03ObjectViews.Count(p => p.Complete),
            Protocol03Partial = file.Protocol03ObjectViews.Count(p => !p.Complete),
            Protocol03Classifications = file.Protocol03ObjectViews.GroupBy(p => p.Classification)
                .OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            PlayerCreationRecords = file.Protocol03ObjectViews.Sum(p => p.PlayerCharacterCreationRecords.Count),
            NamedProfiles = file.Protocol03ObjectViews.Sum(p => p.NamedProfileRecords.Count)
        }).ToArray();
        return new { Contract = "80bc-native22/03-existing-conservative-parser", Files = rows,
            ManageBonusTotal = rows.Sum(r => r.ManageBonusTotal), ManageBonusDecoded = rows.Sum(r => r.ManageBonusDecoded),
            ManageBonusOpaque = rows.Sum(r => r.ManageBonusOpaque), Protocol03Total = rows.Sum(r => r.Protocol03Total),
            Protocol03Complete = rows.Sum(r => r.Protocol03Complete), Protocol03Partial = rows.Sum(r => r.Protocol03Partial) };
    }
}
