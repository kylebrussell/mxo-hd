# Authenticated scale baseline: preparation

No 50/100/200 capacity result is claimed by this preparation. Those runs require a coordinated quiet window; five-client canary validation is separate.

## Workload and safety boundary

The original `tools/LoadTest` mode sends one-byte UDP datagrams from separate sockets. It remains available and is explicitly labelled unauthenticated traffic, not logged-in player capacity.

The new explicit `--authenticated` mode reuses HeadlessClient auth, margin and encrypted world sessions. It creates one existing-harness disposable server and database per invocation, on OS-selected loopback ports, and tears down only its owned resources. It cannot target an arbitrary existing server.50/100/200 are separate serial invocations, bounded to200 clients and1..8 concurrent logins (default4). No MySQL global setting, security, main database, native client or client lock is changed. Connection/capacity failures remain INCOMPLETE with observed entered counts and raw errors.

All clients enter the same Mara footprint, X17020+(index%20)*80 and Z2693+(index/20)*80,Y495: the dense200-client footprint is15.2 by7.2 metres, within the existing50-metre view radius. The small existing test NPC catalog is used, wandering and aggro disabled. Full static data still loads. This is an authenticated dense-view/idle/ping baseline, not combat, mission, mob-population or geographic-world capacity.

Default30-second warmup follows a bounded login ramp, then60 seconds of measurement. Each client sends tokenized `81 4d` echo requests in a closed loop: response or5-second timeout, then1-second delay. Normal encrypted acknowledgements/heartbeats remain active. This is not a guaranteed fixed offered packet rate under overload. Generator and server run on the same host; CPU and memory for both are recorded, and database/container/background load remains a confounder. Large runs wait for root's quiet-window grant.

## Metrics

Diagnostics are dormant unless opt-in`MXO_SCALE_METRICS=1`, exact`MXO_DEV_COMMANDS=1`, and the **actual configured** database is a valid ASCII`mxotest_...` name. They only aggregate timings/counts, never handles. No development RPC, ability grant or timing reset is added.

- **CPU:** owned server and generator process CPU deltas sampled about once per second. Core-percent100 means one saturated logical core; host-normalized server CPU divides by reported logical processor count. Weighted averages use actual elapsed intervals. Working-set bytes are sampled peaks, not total host memory or exact instantaneous peak.
- **Visibility duration:** each complete visibility pass, including ordinary view/gameplay checks, excluding its intentional500ms sleep and the diagnostic snapshot/logging after it. It is not a unified combat-server tick; this server has several independent timers/threads.
- **Flush processing:** the completed FlushQueue work after throttle/reentrancy admission, including packet building, encryption and asynchronous UDP dispatch. Empty admitted flushes and raw-message work are included. Lock-wait/throttle time and asynchronous socket completion are not included.
- **Queue-to-first-dispatch:** monotonic time from sequenced-message creation at actual queue admission to the first accepted BeginSendTo for a packet containing it. This is **not** socket completion or wire-delivery time. Retransmits count once, dropped/refused sends do not. Timed entries are included from queue creation; pre-enqueue ability timers and mission transaction output staging are excluded. RPC and object samples are separate.
- **Queue depth:** once-per-window snapshots of retained reliable RPC/object queues (including sent-but-unacknowledged messages), plus unsent subsets. Queue locks are separate and sampling is not atomic across clients/queues. Sampled maxima can miss between-sample bursts.
- **RTT:** actual tokenized echo response elapsed time, including generator scheduling and receive/parser work, separate from server timings.

Fixed millisecond histogram upper bounds are0.125,0.25,0.5,1,2,4,8,16,32,64,128,256,512,1000,2000,5000,10000,30000 plus overflow. P50/P95 are nearest-rank **upper bucket bounds**, not exact quantiles. Count, sum, mean and max are retained; an overflow percentile is null with nonzero overflow count. Empty metrics are null, not invented zeros. Raw per-echo RTTs and all per-window histograms are retained.

Only metric windows wholly contained within the measured UTC period are summarized, so their actual covered duration is reported separately from the CPU/RTT period. Boundary windows are retained in raw data but excluded from aggregate tables. An empty object-latency sample during an idle phase is unavailable, not zero-latency delivery. The instrumentation itself adds timestamp, bounded histogram-lock, queue-snapshot and JSON/logging overhead; this is an instrumented baseline, not an uninstrumented production throughput claim.

## Reproduction

Build NativeQa Debug first if needed, then rebuild Release last. The existing harness selects the newest built server DLL; authenticated mode checks that it is Release before creating resources.

```
dotnet build tools/NativeQa -c Debug
dotnet build tools/NativeQa -c Release -t:Rebuild
dotnet build tools/LoadTest -c Release
dotnet run --project tools/LoadTest -c Release --no-build -- --authenticated --clients 200 --plan
```

After the quiet-window grant, run50/100/200 serially with `--clients N --warmup-seconds 30 --measure-seconds 60 --output <owned output directory>`. The plan JSON records environment/provenance; raw server-metrics NDJSON, CPU samples, RTTs, failures and summary are separate artifacts. Full owned-server stdout stays in the local proof directory, not a public credential dump. Never invoke the old UDP mode against a native fixture to approximate players.

## Native diagnostic checklist

Discovered mode`scale-diagnostics` is a low-client disposable profile, not a capacity run. Claude alone performs native setup/QA. It enables guarded aggregate metrics, normal Actions2/17, no forced RNG and no GM. Walk near Mara, open Actions and an NPC radial, use the actual hardline radial Exit to Loading Area and return, and verify responsiveness plus aggregate[SCALE] windows/no visibility errors. Do not launch50/100/200 concurrently with native QA.

Results and exact source/environment/raw artifact links will be added after granted execution. Missing or failed cases remain explicit.
