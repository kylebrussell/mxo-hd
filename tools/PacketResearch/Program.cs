using MxoHd.PacketResearch;

Dictionary<string, List<string>> options = ParseArgs(args);

if (HasFlag(options, "--help") || HasFlag(options, "-h"))
{
    PrintUsage();
    return 0;
}

string? abilityUseHex=GetSingle(options,"--ability-use-hex");
string? abilityUseBodyHex=GetSingle(options,"--ability-use-body-hex");
if(abilityUseHex!=null || abilityUseBodyHex!=null)
{
    if(abilityUseHex!=null && abilityUseBodyHex!=null){Console.Error.WriteLine("Select one full RPC or body input.");return 2;}
    try
    {
        byte[] bytes=Convert.FromHexString(abilityUseHex??abilityUseBodyHex!);
        AbilityUseRecord? record;
        bool valid=abilityUseHex!=null?AbilityUseRecord.TryDecode(bytes,out record):AbilityUseRecord.TryDecodePayload(bytes,out record);
        if(!valid){Console.Error.WriteLine("Expected exactly43 bytes80b9 or41 body bytes; short harness records are not full native records.");return 2;}
        byte[] rpc=record!.Encode();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Fields=record,RpcHex=Convert.ToHexString(rpc),BodyHex=Convert.ToHexString(rpc.AsSpan(2)),Semantics="Native printer names only; ground words/flags are raw, no activation/admission inference."},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return 0;
    }
    catch(FormatException){Console.Error.WriteLine("Input must be exact hexadecimal bytes without separators.");return 2;}
}

string repo = GetSingle(options, "--repo") ?? Directory.GetCurrentDirectory();
string fullRepo = Path.GetFullPath(repo);

List<string> packetDumpRoots = GetMany(options, "--packet-dumps");
if (packetDumpRoots.Count == 0)
{
    packetDumpRoots.Add(Path.Combine(fullRepo, "research", "packet-dumps"));
}

string? rajko = GetSingle(options, "--rajko");
List<string> externalSources = GetMany(options, "--external-source");
string? json = GetSingle(options, "--json");
string? markdown = GetSingle(options, "--markdown");

string? familiesJson = GetSingle(options, "--families-json");
if (familiesJson is not null)
{
    var headers = PacketResearcher.LoadLocalHeaders(fullRepo).ToArray();
    var summaries = packetDumpRoots.SelectMany(root => PacketResearcher.LoadPacketDumpSummaries(root, headers, fullRepo));
    File.WriteAllText(familiesJson, System.Text.Json.JsonSerializer.Serialize(
        ProtocolFamilyAudit.Summarize(summaries), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Wrote bounded family accounting to {familiesJson}");
    return 0;
}

PacketResearchReport report = PacketResearcher.BuildReport(fullRepo, packetDumpRoots, rajko, externalSources);

if (json is not null)
{
    ReportWriter.WriteJson(report, json);
    Console.WriteLine($"Wrote JSON report to {json}");
}

if (markdown is not null)
{
    ReportWriter.WriteMarkdown(report, markdown);
    Console.WriteLine($"Wrote markdown report to {markdown}");
}

if (json is null && markdown is null)
{
    Console.WriteLine(ReportWriter.ToMarkdown(report));
}

return 0;

static Dictionary<string, List<string>> ParseArgs(string[] args)
{
    Dictionary<string, List<string>> options = new(StringComparer.OrdinalIgnoreCase);

    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        if (!arg.StartsWith("-", StringComparison.Ordinal))
        {
            continue;
        }

        if (!options.ContainsKey(arg))
        {
            options[arg] = new List<string>();
        }

        if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
        {
            options[arg].Add(args[++i]);
        }
    }

    return options;
}

static bool HasFlag(Dictionary<string, List<string>> options, string name)
{
    return options.ContainsKey(name);
}

static string? GetSingle(Dictionary<string, List<string>> options, string name)
{
    return options.TryGetValue(name, out List<string>? values) && values.Count > 0
        ? values[^1]
        : null;
}

static List<string> GetMany(Dictionary<string, List<string>> options, string name)
{
    return options.TryGetValue(name, out List<string>? values)
        ? values.ToList()
        : new List<string>();
}

static void PrintUsage()
{
    Console.WriteLine("""
    Usage:
      dotnet run --project tools/PacketResearch -- [options]

    Options:
      --ability-use-hex <hex> Decode one exact full43-byte80b9 record and roundtrip.
      --ability-use-body-hex <hex> Decode one exact41-byte80b9 body.
      --repo <path>            Repository root. Defaults to current directory.
      --families-json <path>  Bounded lossless 80bc / conservative 03 accounting; skips full report.
      --packet-dumps <path>    Packet dump root. Can be provided more than once.
                               Defaults to research/packet-dumps under --repo.
      --rajko <path>           Optional rajkosto/mxoemu clone root.
      --external-source <path> Optional extra source root to mine for hardcoded packet examples.
                               Can be provided more than once.
      --json <path>            Write JSON summary.
      --markdown <path>        Write markdown summary.
      --help                   Show this help.
    """);
}
