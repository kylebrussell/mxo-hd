# Headless client

Plays Matrix Online over the real protocol with no game client, so server gameplay can be tested in parallel: two-player flows that the real client can't reach on one machine, many servers at once, and pass or fail in seconds.

**What a pass means.** The server does what we believe the protocol is. It doesn't show the real client accepts it: `tools/RealClient/Run-Regression.ps1` stays the final check. The client records in the scenarios are hand-written from `docs/SECURE-TRADE.md`, the handlers' comments and retail captures, so a wrong belief about a layout passes here and fails there.

## Running

Needs the `mxo-hd-mysql` container. Builds use `hds/bin`, so build first.

```bash
dotnet build tools/HeadlessClient/HeadlessClient.csproj
```

```bash
dotnet tools/HeadlessClient/bin/Debug/net8.0/HeadlessClient.dll --parallel 6
```

| Option | Meaning |
|---|---|
| `--parallel N` | Servers at once (default 6). The limit is MySQL, not the machine: see below. |
| `--filter text` | Only scenarios with this in their name. |
| `--repeat N` | Run each scenario N times, to shake out races. |
| `--logs dir` | Where a failed scenario's server output goes. |
| `--list` | List the scenarios. |
| `--drop-leftovers` | Drop `mxotest_run_*` databases first, after a run that was killed. Don't use it while another run is going. |

Each scenario gets its own server process, working directory, ports and throwaway database. A run that is killed outright can leave servers behind (they listen only on 127.0.0.1) and a database.

The container allows 151 connections, shared with whatever else uses it. The whole suite runs fine at `--parallel 12`; 16 servers all running `many-logins-at-once` hit `Too many connections`. A server opens one connection per thread doing database work, so that is a test-capacity limit, not a leak.

## Side-by-side servers

The server takes these, all with today's values as defaults, so nothing changes for the real client:

| Variable | Default | |
|---|---|---|
| `MXO_AUTH_PORT` | 11000 | TCP |
| `MXO_MARGIN_PORT` | 10000 | TCP |
| `MXO_WORLD_PORT` | 10000 | UDP |
| `MXO_CONSOLE_PORT` | 55557 | TCP, only with the admin console on |
| `MXO_DB_NAME` | from `Config.xml` | database name |

`scripts/throwaway-db.sh create mxotest_x` makes a database from `SQL/reality_hd.sql`, `drop` removes it, `list` shows them. Names must start with `mxotest_`. The app user has no rights outside `reality_hd`, so these use the container's root account, and creating one gives the app user rights on `mxotest_*` only. The harness does the same from C# (`ThrowawayDatabase`).

## Scripting a player

```csharp
HeadlessPlayer alice = await context.AddPlayerAsync(new CharacterSeed("Alice") { Items = new[] { (3, goid, 1) } });
await alice.EnterWorldAsync();                               // RPC 05, then wait for the self spawn
ViewCreation bob = await alice.WaitForViewNamedAsync("Bob"); // a creation record: view id, spawn id, attributes
alice.SendRpc($"81 82 {viewAndSpawn} ...");                  // hex, or bytes
alice.SendChat("?addexp 500");                               // chat line or command
await alice.WaitForRpcAsync("81 87 19 00 00 8e");            // hex with ?? wildcards, from the start of the record
await alice.WaitForViewUpdateAsync(bob.ViewId, "02 80 86");  // <view id> <bytes> anywhere in a 03 list
```

Waits on records move forward through what has arrived, so a sequence of waits asserts order. `Received(pattern)` looks at everything regardless.

Records have lengths; **view updates don't**, because their layout depends on the object. So a `03` list stays raw: creation records are found by their start and `cd ab` mark and read by the server's object definitions (`ObjectDefinitions`), and updates are searched for as bytes. Use patterns long enough not to match by accident.

Each scenario also fails if a real client would have tripped: a corrupt packet, a gap in the server's RPC counter, or a record that doesn't parse.

## What it can't tell you

- Whether the real client accepts any of this: layouts, the order of records, or timing.
- `81 82`'s real shape. Nothing has ever sent one. TargetID is `<view u16> <spawn id u8> 00` from reading `client.dll` and from `81 51`, which shares the code path and has retail captures.
- Behaviour under packet loss: on loopback nothing is lost, and the client only resends when told to.
- Anything the client decides for itself: range checks, slot locks, what it shows.
