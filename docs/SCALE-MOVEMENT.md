# Scale under movement and world NPCs — 2026-11-25

Follow-up to the [idle baseline](SCALE-BASELINE.md). That baseline used idle clients only. This round adds two workloads, times each stage of the visibility pass, and fixes three server faults that the new workloads exposed.

## What changed

- **Per-stage timing.** `ScaleDiagnostics.Stage` records each stage of the visibility pass into its own histogram. The stages are snapshot, player-views, mob-views, mob-motion, per-player, aggro, subways, static-objects and contacts-probes. Every `[SCALE]` window carries them as `Stages`. Like the other diagnostics, this stays dormant unless `MXO_SCALE_METRICS=1`, `MXO_DEV_COMMANDS=1` and a `mxotest_` database are all set.
- **Moving workload (`--move`).** `LoadTest --authenticated --move` has every client walk a 3 m circle around its spawn at 1.7 m/s. Each client sends its own-view position (`02 00 01 08 <x y z>`) every 250 ms, on top of the echo pings.
- **World NPCs (`--world-mobs`).** Loads the full world NPC catalog with wandering on, in place of the small test catalog. Aggro stays off.
- **Fix 1: stack overflow in the UDP receive loop.** Before the fix, the 200-client moving run ended the server process with `Stack overflow.` (`before-move-200`). `finalReceiveFrom` re-armed `BeginReceiveFrom` from inside its own callback. Under load, receives completed synchronously, so the callback ran inside `BeginReceiveFrom` and the stack kept growing. `WorldServer.ReceiveNext` now loops over synchronous completions, and the callback handles only asynchronous ones. Datagram order is unchanged. A datagram that fails is logged, and the receive loop keeps running.
- **Fix 2: the static-object scan.** The static visibility check acts only on hardline booths and listed signposts. It used to scan every static object within 50 m of every player on every pass. At 200 idle clients that scan cost about 65 ms of the pass's 125 ms. The index now holds only those two kinds of object (`InStaticVisibilityIndex`).
- **Fix 3: NPC creation cost.** `Mob.getCreationData`, the interlock-handler builders and the simulacrum creation each built `GameObjectDefinitions`, which runs about 40,000 field initializers, just to read one object. Each NPC view creation cost about 88 ms. At 60 clients, 97 creations in one pass took 8.5 s. In the first 200-client world-NPC run, one pass took 108 s. They now construct the single `AttributeClass` directly.

## Results

All cases ran serially from one Release source tree, 598dbc23. That is this PR's tree before it was rebased onto master after PR1209 and PR1210; the rebase applied without conflicts. `before-move-200` ran on b07bbeda, which has the instrumentation but none of the fixes. Each case used its own disposable server and database, a 30 s warmup and about 60 s of measurement. All clients sat in the same dense Mara footprint as the baseline (15.2 by 7.2 m at 200 clients). Generator and server shared one 16-logical-processor host. Native QA sessions ran at times during these runs, so CPU and latency figures carry that confounder.

| Case | Status | Server CPU (% of one logical CPU) | Visibility mean / max ms | Object updates sent | Echo RTT mean ms |
|---|---|---:|---|---:|---:|
| idle 200 (baseline 2026-11-12) | PASS | 24.5 | 85.5 / 409.4 | 0 | 18.2 |
| idle 200 | PASS | 16.7 | 44.7 / 114.7 | 0 | 21.3 |
| move 50 | PASS | 29.1 | 6.9 / 36.2 | 561,005 | 35.0 |
| move 100 | PASS | 89.4 | 27.2 / 182.3 | 2,267,100 | 51.4 |
| move 200, before the fixes | server crashed | n/a | no windows | n/a | n/a |
| move 200 | INCOMPLETE | 151.6 | 239.7 / 609.4 | 3,155,867 | 1273 |
| move 200 + world NPCs | INCOMPLETE | 149.1 | 649.1 / 1270.4 | 3,316,484 | 1070 |

Stage means in ms:

| Case | player-views | contacts-probes | snapshot | mob-views | static-objects |
|---|---:|---:|---:|---:|---:|
| idle 200 | 32.5 | 4.7 | 0.4 | 1.8 | 1.1 |
| move 50 | 1.7 | 4.6 | 0.2 | 0.1 | 0.1 |
| move 100 | 8.5 | 14.0 | 2.7 | 0.5 | 0.3 |
| move 200 | 60.5 | 42.0 | 85.5 | 2.9 | 2.3 |
| move 200 + world NPCs | 55.8 | 44.0 | 93.1 | 15.3 | 2.7 |

Raw evidence is under [research/scale/2026-11-25](../research/scale/2026-11-25), with a [SHA256 manifest](../research/scale/2026-11-25/SHA256.json). In each `summary.json`, failures are grouped by error. The raw list repeated every client's view list for every timeout.

## Reading it

- **Up to 100 dense moving clients hold.** At 50 and 100 moving players, every client stays active through the measurement, with no ping timeouts and no protocol or server problems.
- **200 dense moving clients is past saturation.** All 200 stay logged in and active, but echo round trips average about 1.3 s and many pings time out after 5 s. In this footprint every player sees every other one, so 200 × 4 positions/s fan out to about 160,000 viewer updates per second (about 3.2 million object sends a minute). That is an extreme, unrealistic density: everyone is inside 15 m of everyone else. It is not a geographic capacity figure.
- **Next profiling targets.** Under that fan-out the next costs are the snapshot stage and player-views. The snapshot stage copies the client and NPC lists and builds the grids; it rises to 85 ms. It is most likely waiting on `WorldServer.Clients`, which the receive path also locks once per datagram. Coalescing queued movement updates per viewer is the other lever. Neither is changed here.
- **World NPCs no longer stall the pass.** With the full NPC catalog, logins no longer freeze the visibility pass. Mob-motion reaches about 300 ms per pass while 100 to 175 clients are logging in, and the steady mob-views stage averages 15 ms.

## Broadcast lock (follow-up)

The snapshot stage turned out to be lock waiting, not GC. Server GC alone (`DOTNET_gcServer=1`) left it at 83 ms. Every own-view movement datagram went through `WorldServer.SendViewPacketToAllPlayers`, which held the global `Clients` lock while it built, queued and flushed an update for every viewer. NPC updates, NPC view deletes, static-object updates and self-view-to-target updates did the same. Those broadcasts now loop over a `ClientSnapshot()` copy instead.

A/B on master 96d883c0, run back to back. The fix runs used the uncommitted diff, so their plans record GitDirty. Raw data is in [research/scale/2026-11-25-broadcast-lock](../research/scale/2026-11-25-broadcast-lock), with a [SHA256 manifest](../research/scale/2026-11-25-broadcast-lock/SHA256.json).

| Case | Status | Ping timeouts | Echo RTT mean ms | Object updates sent | Snapshot stage mean ms | Server CPU % |
|---|---|---:|---:|---:|---:|---:|
| move 100, master | INCOMPLETE | 171 | 589 | 2,019,105 | 64.3 | 129 |
| move 100, fix | PASS | 0 | 45.6 | 2,239,072 | 0.3 | 104 |
| move 200, master | INCOMPLETE | 1679 | 1604 | 2,034,724 | 168.5 | 127 |
| move 200, fix | PASS | 0 | 112.9 | 8,990,881 | 0.8 | 404 |

The fixed 200-client case now delivers about 150,000 viewer updates per second with no ping timeouts. The server CPU figure rises to about four logical processors because broadcasters no longer queue on one lock. The largest remaining stages are player-views (127 ms) and contacts-probes (110 ms).

On today's master, the 100-client moving case no longer passes. The earlier round passed it on a tree from before PR1210. With the fix it passes.

## Reproduce

Build Release last, because the tool refuses to run when a newer Debug server DLL exists. Then run each case serially:

```
dotnet tools/LoadTest/bin/Release/net8.0/LoadTest.dll --authenticated --clients 200 --move [--world-mobs] --output <dir>
```

## View lifecycle

The `view-range-lifecycle` headless scenario covers the NEXT-STEPS visibility check for players, NPCs and hardline booths. Subway cars already have their own `subway-*` scenarios. The scenario checks:

- a player who leaves the view radius gets the delete records for the other player, the NPC and the Mara booth;
- the other player loses the walker's view;
- standing still never creates a view twice;
- coming back recreates each view exactly once.
