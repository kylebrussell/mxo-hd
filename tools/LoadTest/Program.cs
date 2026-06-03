using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

Options options = Options.Parse(args);
byte[] payload = new byte[Math.Max(1, options.PayloadBytes)];
payload[0] = 0x00;

Console.WriteLine(
    $"Sending UDP load to {options.Host}:{options.Port} with {options.Clients} clients, " +
    $"{options.MessagesPerClient} messages/client, {payload.Length} bytes/message.");

IPEndPoint target = new IPEndPoint(IPAddress.Parse(options.Host), options.Port);
UdpClient[] clients = Enumerable.Range(0, options.Clients)
    .Select(_ => new UdpClient(0))
    .ToArray();

try
{
    long sentMessages = 0;
    Stopwatch stopwatch = Stopwatch.StartNew();

    Task[] tasks = clients.Select(client => Task.Run(async () =>
    {
        for (int i = 0; i < options.MessagesPerClient; i++)
        {
            await client.SendAsync(payload, payload.Length, target);
            Interlocked.Increment(ref sentMessages);

            if (options.DelayMs > 0)
            {
                await Task.Delay(options.DelayMs);
            }
        }
    })).ToArray();

    await Task.WhenAll(tasks);
    stopwatch.Stop();

    double seconds = Math.Max(0.001, stopwatch.Elapsed.TotalSeconds);
    Console.WriteLine($"Sent messages: {sentMessages}");
    Console.WriteLine($"Elapsed: {stopwatch.Elapsed}");
    Console.WriteLine($"Send rate: {sentMessages / seconds:N1} messages/sec");
}
finally
{
    foreach (UdpClient client in clients)
    {
        client.Dispose();
    }
}

internal sealed class Options
{
    public string Host { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = 10000;
    public int Clients { get; private set; } = 50;
    public int MessagesPerClient { get; private set; } = 20;
    public int PayloadBytes { get; private set; } = 1;
    public int DelayMs { get; private set; } = 10;

    public static Options Parse(string[] args)
    {
        Options options = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string NextValue()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException("Missing value for " + arg);
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--host":
                    options.Host = NextValue();
                    break;
                case "--port":
                    options.Port = int.Parse(NextValue());
                    break;
                case "--clients":
                    options.Clients = int.Parse(NextValue());
                    break;
                case "--messages":
                    options.MessagesPerClient = int.Parse(NextValue());
                    break;
                case "--payload-bytes":
                    options.PayloadBytes = int.Parse(NextValue());
                    break;
                case "--delay-ms":
                    options.DelayMs = int.Parse(NextValue());
                    break;
                case "--help":
                    PrintUsageAndExit();
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arg);
            }
        }

        if (options.Clients <= 0 || options.MessagesPerClient <= 0 || options.Port <= 0)
        {
            throw new ArgumentException("Clients, messages, and port must be positive.");
        }

        return options;
    }

    private static void PrintUsageAndExit()
    {
        Console.WriteLine("Usage: load-test [--host 127.0.0.1] [--port 10000] [--clients 50] [--messages 20] [--payload-bytes 1] [--delay-ms 10]");
        Environment.Exit(0);
    }
}
