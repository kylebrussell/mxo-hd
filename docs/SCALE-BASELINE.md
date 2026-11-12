# Authenticated scale baseline — 2026-11-12

All **50/100/200 authenticated clients** entered and remained active through their measured windows, with no login, ping, protocol or server failures. This is an idle dense-view/ping baseline with test NPCs, **not a capacity ceiling or combat/mission benchmark**. At 200 clients the server used 24.47% of one logical processor (1.529% of the 16-logical-processor host); visibility passes averaged 85.55ms and flush processing 0.048ms.

The three serial cases used frozen source [`ab4c0a4125508d17810b1478e05cb67d55774846`](https://github.com/kylebrussell/mxo-hd/tree/ab4c0a4125508d17810b1478e05cb67d55774846), all with clean Git status and identical Release hds SHA256 `669066860147C1CD3DABB2F835EBDBAC939ED8297F17311317C672E098285BA7`. Root verified no native clients/client lock at the quiet-window start 21:02:23UTC; workers held builds/heavy work until END 21:11UTC. No contamination was reported. Normal background services were not independently profiled. Runtime reported .NET8.0.34 on Microsoft Windows 10.0.26300, 16 logical processors; physical core count/model was not recorded.

## Observed results

Each case has its own server/database, 30s warmup and about 60s CPU/RTT measurement. Metric windows are wholly contained subsets:

| Clients requested/entered | Min active in metric windows | Login ramp s | Metric windows / covered s | CPU measured s | Outcome |
|---|---:|---:|---|---:|---|
|50/50|50|6.57|57 / 59.44|60.33|PASS, exit0|
|100/100|100|11.66|55 / 59.05|60.22|PASS, exit0|
|200/200|200|23.41|50 / 59.44|60.22|PASS, exit0|

| Clients | Server CPU % of one logical CPU | Server % of logical host | Generator % of one logical CPU | Sampled server working set MiB | Sampled generator working set MiB |
|---:|---:|---:|---:|---:|---:|
|50|5.46|0.342|4.33|2083.0|183.2|
|100|9.03|0.564|5.92|2096.5|216.1|
|200|24.47|1.529|10.48|2147.7|364.9|

The raw field `ServerCpuCorePercent` means one **logical processor**, not a whole physical core.100% means one CPU-second per elapsed second; host percent divides by 16. CPU averages are weighted by actual sample intervals. Working set is not GC live heap size.

| Clients | Metric | Samples | Mean ms | P95 bucket upper bound ms | Observed max ms |
|---:|---|---:|---:|---:|---:|
|50|Visibility pass|114|13.339|32|23.576|
|50|Flush processing|13562|0.041|0.125|2.492|
|50|RPC enqueue → first accepted dispatch|3000|18.677|128|67.316|
|50|Echo RTT|2900|32.574|128|79.196|
|100|Visibility pass|110|29.510|64|94.062|
|100|Flush processing|26151|0.043|0.125|3.376|
|100|RPC enqueue → first accepted dispatch|6000|10.971|64|68.159|
|100|Echo RTT|5882|24.214|64|78.564|
|200|Visibility pass|100|85.549|128|409.435|
|200|Flush processing|51694|0.048|0.125|13.647|
|200|RPC enqueue → first accepted dispatch|12155|4.619|64|73.145|
|200|Echo RTT|11800|18.211|32|80.928|

P95 columns are coarse histogram bucket bounds; they can exceed the exact observed maximum (for example the 50-client RPC P95 bin ends at 128ms while the observed maximum is 67.316ms). They are not measured 128ms stalls. Server RPC counts include background RPCs and a different contained-window period from echo RTTs, so the counts need not match. Lower RTT at higher client counts is an observation, not proof that load improves latency; scheduling/coalescing and sampling phase vary.

| Clients | Sampled RPC retained max | RPC unpacketized max | Object retained / unpacketized max | Object first-dispatch events / latency |
|---:|---:|---:|---|---|
|50|10|10|0 / 0|0 / unavailable|
|100|19|19|0 / 0|0 / unavailable|
|200|1|1|0 / 0|0 / unavailable|

Object creations happen during login/warmup; clients do not move or fight during measurement. Thus zero sampled object queues and **no object latency samples** describe absent measured object-update work, not zero-latency object delivery. Queue maxima are sampled and phase-sensitive; they are not total queue bounds. The existing RPC`rpcNumber<0`/object`sendCounter==0` subsets mean not yet packetized, which can differ from actual dispatch if a send fails.

Visibility duration grew from 13.34 to 85.55ms as player count quadrupled; the 200-client maximum was 409.44ms. This identifies visibility maintenance as a useful follow-up profiling target. No optimization or stronger capacity claim follows from three idle cases.

### Raw evidence

- **50 clients:** 2026-11-12T21:04:09.1778977Z through 2026-11-12T21:05:09.5158091Z; [summary](../research/scale/2026-11-12/50/summary.json), [source/environment/arguments](../research/scale/2026-11-12/50/plan.json), [CPU samples](../research/scale/2026-11-12/50/cpu-samples.json), [per-window metrics](../research/scale/2026-11-12/50/server-metrics.ndjson), [raw RTTs](../research/scale/2026-11-12/50/rtt-ms.json), [exit0](../research/scale/2026-11-12/50/exit-code.txt).
- **100 clients:** 2026-11-12T21:06:25.5932722Z through 2026-11-12T21:07:25.8210643Z; [summary](../research/scale/2026-11-12/100/summary.json), [source/environment/arguments](../research/scale/2026-11-12/100/plan.json), [CPU samples](../research/scale/2026-11-12/100/cpu-samples.json), [per-window metrics](../research/scale/2026-11-12/100/server-metrics.ndjson), [raw RTTs](../research/scale/2026-11-12/100/rtt-ms.json), [exit0](../research/scale/2026-11-12/100/exit-code.txt).
- **200 clients:** 2026-11-12T21:09:17.1735915Z through 2026-11-12T21:10:17.4087958Z; [summary](../research/scale/2026-11-12/200/summary.json), [source/environment/arguments](../research/scale/2026-11-12/200/plan.json), [CPU samples](../research/scale/2026-11-12/200/cpu-samples.json), [per-window metrics](../research/scale/2026-11-12/200/server-metrics.ndjson), [raw RTTs](../research/scale/2026-11-12/200/rtt-ms.json), [exit0](../research/scale/2026-11-12/200/exit-code.txt).

[SHA256 manifest](../research/scale/2026-11-12/SHA256.json) covers the exact copied raw files. The summary field`ServerExited=false` is captured at measurement end before normal teardown; exit0 is recorded after awaited cleanup. Full owned-server stdout remains in the local Temp proof directories; it is not published as a credential/log dump. All three normal async disposals completed before process exit0, releasing their owned server/database. The five-client canary passed before the quiet window; it is a correctness check, not an additional60s capacity point.

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
- **Queue depth:** once-per-window snapshots of retained reliable RPC/object queues (including sent-but-unacknowledged messages), plus not-yet-packetized subsets. Queue locks are separate and sampling is not atomic across clients/queues. Sampled maxima can miss between-sample bursts.
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

Validation at the frozen benchmark pin: 15 guard/histogram/decoder/CPU/CLI tests and full Release 4186 tests passed; five-client canary and all three serial cases exited0. NativeQa Debug/Release builds and the actual read-only diagnostic plan/provenance passed. Later master merges retain the exact instrumented runner/executor source; benchmark DLL hashes remain pinned to the measured build rather than replaced with a later report-build hash.
