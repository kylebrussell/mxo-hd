using System.Diagnostics;
using HeadlessClient;

// Runs the scenarios, each against its own server and throwaway database, as many at a time as --parallel says.
//   dotnet run --project tools/HeadlessClient -- [--parallel 6] [--filter trade] [--repeat 1] [--logs dir] [--list] [--drop-leftovers]
// Build first: it runs the hds.dll under hds/bin, newest build first.
// Exit code 0 only if everything passed. Needs the mxo-hd-mysql container running. See README.md.

int parallel = 6;
int repeat = 1;
string? filter = null;
string logDirectory = Path.Combine(Path.GetTempPath(), "mxo-headless-logs");
bool listOnly = false;
bool dropLeftovers = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--parallel": parallel = int.Parse(args[++i]); break;
        case "--repeat": repeat = int.Parse(args[++i]); break;
        case "--filter": filter = args[++i]; break;
        case "--logs": logDirectory = args[++i]; break;
        case "--list": listOnly = true; break;
        case "--drop-leftovers": dropLeftovers = true; break;
        default:
            Console.Error.WriteLine("Unknown argument " + args[i]);
            return 2;
    }
}

string root = FindRepositoryRoot();
List<Scenario> selected = Scenarios.All
    .Where(s => filter == null || s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
    .SelectMany(s => Enumerable.Repeat(s, repeat))
    .ToList();

if (listOnly)
{
    selected.ForEach(s => Console.WriteLine(s.Name));
    return 0;
}

// The scenarios and the server both write logs to the working directory and read these from the environment
Environment.SetEnvironmentVariable("MXO_SERVER_LOG", "0");
Directory.CreateDirectory(logDirectory);
if (dropLeftovers)
{
    await ThrowawayDatabase.DropLeftoversAsync();
}

int running = 0, peak = 0;
object countLock = new();
SemaphoreSlim slots = new(parallel);
Stopwatch total = Stopwatch.StartNew();
List<Task<(string Name, string? Failure, TimeSpan Time)>> tasks = new();
int number = 0;
foreach (Scenario scenario in selected)
{
    int run = ++number;
    tasks.Add(Task.Run(async () =>
    {
        await slots.WaitAsync();
        lock (countLock)
        {
            running++;
            peak = Math.Max(peak, running);
        }
        Stopwatch stopwatch = Stopwatch.StartNew();
        string? failure = null;
        try
        {
            string label = "run_" + run;
            await using ScenarioContext context = await ScenarioContext.StartAsync(root, label, scenario.Environment, scenario.MissionFixture);
            try
            {
                await scenario.Run(context).WaitAsync(scenario.Timeout ?? TimeSpan.FromMinutes(6));
                string problems = string.Join("; ", context.ClientProblems().Concat(context.ServerProblems()));
                if (problems.Length > 0)
                {
                    throw new ScenarioFailure("Passed its checks, but: " + problems);
                }
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(logDirectory, scenario.Name.Replace(' ', '-') + "-" + run + ".log");
                await File.WriteAllTextAsync(logPath, context.Server.FullOutput() + "\n--- DebugLog ---\n" + context.Server.DebugLog());
                failure = $"{ex.GetType().Name}: {ex.Message}\n    server log: {logPath}";
            }
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
        }
        finally
        {
            lock (countLock)
            {
                running--;
            }
            slots.Release();
        }
        Console.WriteLine($"{(failure == null ? "PASS" : "FAIL")}  {scenario.Name} ({stopwatch.Elapsed.TotalSeconds:0.0}s)");
        if (failure != null)
        {
            Console.WriteLine("    " + failure);
        }
        return (scenario.Name, failure, stopwatch.Elapsed);
    }));
}

var results = await Task.WhenAll(tasks);
int failed = results.Count(r => r.Failure != null);
Console.WriteLine($"\n{results.Length - failed} passed, {failed} failed in {total.Elapsed.TotalSeconds:0.0}s; " +
                  $"{peak} servers at once at the peak (--parallel {parallel}).");
return failed == 0 ? 0 : 1;

static string FindRepositoryRoot()
{
    string? directory = AppContext.BaseDirectory;
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory, "hds.sln")))
        {
            return directory;
        }
        directory = Path.GetDirectoryName(directory);
    }
    throw new DirectoryNotFoundException("Can't find hds.sln above " + AppContext.BaseDirectory);
}
