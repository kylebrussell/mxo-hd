using MxoHd.PacketResearch;

Dictionary<string, List<string>> options = ParseArgs(args);

if (HasFlag(options, "--help") || HasFlag(options, "-h"))
{
    PrintUsage();
    return 0;
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
      --repo <path>            Repository root. Defaults to current directory.
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
