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

## Player-view and contact costs (follow-up)

After the broadcast-lock fix, the largest stages at 200 dense moving players were player-views and contacts-probes. The pair loop decoded every neighbour's position and goid once per pair, which allocated about 40,000 times a pass. It also found each view with a linear search of the player's view list. The contact check resolved every contact with a linear search, and looked up a view for every spot in the district.

Changes:

- **Positions are decoded once.** `NumericalUtils.LtVector3dToDoubles` now reads the doubles in place, without allocating.
- **Each player is sampled once per pass.** Position, liveness, district, stealth, entity and goid are read once, not once per pair.
- **View lookups are indexed.** `ViewList` indexes views by entity and view id, and keeps the list's own first-match order.
- **Unchanged players are skipped.** A player is skipped when its own sample, its view list (tracked by a version counter) and every neighbour, current or from its last evaluation, are unchanged. Any join, leave, district or world change, or a hidden player anywhere, makes it a full pass.
- **Contacts are bucketed.** Contacts are grouped by district once per pass, and a view is looked up only for spots within the despawn range.
- **PvP archive checks stop waiting on the transport fence when nothing changes.** They no longer wait on the fence for objects already shown in their current state, and no longer flush a client every pass when nothing was queued.

The in-process benchmark is `VisibilityPassBenchmark` (`MXO_VISIBILITY_BENCH=1`). It runs 200 players in the scale footprint who already see each other and the contacts in range, needs no MySQL, and reports pass time on one thread:

| Stage (ms per pass) | master a8a79fe0 | this change |
|---|---:|---:|
| player-views, everyone moving | 28.3 mean / 87-91 max | 10.6-11.2 / 13-14 |
| player-views, nobody moving | 24.9-28.1 / 35 | 2.1-2.3 / 3.2-3.6 |
| contacts, everyone moving | 4.0-4.4 / 17-21 | 1.1-1.2 / 2.1-2.5 |

PvP archive checks need the world data loader and are not in the in-process benchmark.

Server-side, `LoadTest --authenticated`, master a8a79fe0 against this change, run back to back on 2026-11-26. Mean ms per pass. On master the contact, floor-probe and PvP-archive checks were one stage; here they are split into three. One more master move run failed during the login ramp, with auth connections forcibly closed, and was rerun.

| Case | Echo RTT | Visibility mean / max | player-views | contacts + probes + pvp-archives |
|---|---:|---|---:|---:|
| idle 200, master (2 runs) | 21.7 / 19.7 | 39.7 / 101, 39.6 / 223 | 28.2 / 28.9 | 4.6 / 4.4 |
| idle 200, this change | 15.7 / 15.4 | 9.8 / 34, 9.5 / 80 | 1.4 / 1.5 | 1.4 / 1.3 |
| move 200, master (2 runs) | 98.7 / 60.1 | 254.7 / 883, 168.8 / 562 | 121.4 / 74.3 | 107.6 / 81.5 |
| move 200, this change | 212.3 / 60.3 | 160.1 / 698, 49.5 / 280 | 44.9 / 17.7 | 84.2 / 19.5 |

All runs PASS except the master run that failed at login. Under movement the run-to-run variance is large; the first moving run of this change had the worst echo RTT of the set. Even so, its pass stages were lower than both master runs. PvP archives is now the largest remaining stage under movement (77.7 and 17.9 ms).

## Movement coalescing and PvP-archive cost (follow-up)

After the player-view and contact optimizations, the two remaining scaling bottlenecks under 200 dense moving clients were:

1. **`PvpArchives.Check` (`77.7 / 17.9 ms` under movement).** `Check` called `c.TransportFence.Capture()` unconditionally on every client every pass, blocking on the same lock held by `WorldClient.FlushQueue` during packet build/encrypt/send even when no archive sender was within 50 m. It also allocated `double[3]`, `HashSet<ulong>`, and LINQ stale-view arrays per client per pass, rescanned `MissionSites.Goid` / `Stage(now)` inside the loop, and used per-spawn reflection.
2. **Per-viewer movement fan-out and flush contention.** `PlayerCharacter.parseAutoView` forwarded every incoming movement packet through `SendViewPacketToAllPlayers`, allocating `PacketContent`/`DynamicArray` + a throwaway byte array inside `SequencedMessage` and synchronously calling `viewer.FlushQueue()` across all 199 other clients on the mover's packet thread. `DeletedAckedPackets` and `CleanNakPackets` also removed acked messages one by one in an O(N^2) loop, and `WorldClient` allocated `Stopwatch` + `.ToString()` strings per UDP packet even with `MXO_PACKET_LOG=0`.

Changes:

- **PvP archives fast path (`hds/world/Structures/PvpArchives.cs`).** Precomputes district-1 archive senders once as a flat struct array, caches spawn `FieldInfo` metadata per GOID, decodes client position in place without allocating, captures `TransportFence` lazily only when a spawn, state change, or delete is actually required, and skips `ViewManager` / `ObjectStates` lookups when the client's view list version and sender stage are unchanged.
- **Per-viewer movement coalescing (`MessageQueue`, `WorldServer`, `PlayerCharacter`, `WorldClient`, `PlayerPackets`).** Forwarded `SelfViewUpdate` movement frames coalesce in place per `(viewer, viewId)` while unsent, merging `(Action, Yaw, Position)` flags so a newer position-only update never drops a pending `Action` or `Yaw` transition, and defer sending to `FlushThread`'s 50 ms tick via `MarkFlushPending()` instead of contending on 199 other clients' `flushLock` / `TransportFence` locks on the mover's receive thread. View deletion and world reset clear pending coalescing slots.
- **Queue compaction and hot-path allocation cleanup (`WorldClient`, `SequencedMessage`, `Output`).** `DeletedAckedPackets` and `CleanNakPackets` compact in a single O(N) pass under one lock, `FlushQueue` tracks the unsent object-queue prefix when `ObjectResendEnabled` is off, `SequencedMessage` drops its dead duplicate byte-array allocation, and packet-log `Stopwatch`/`.ToString()` formatting is guarded by `Output.PacketLogsEnabled`.
- **`Clients`-lock broadcast audit (`WorldServer`, `ChatPackets`).** Switched the remaining RPC broadcast helpers (`SendRPCToOnePlayer`, `SendRPCToOnePlayerByHandle`, `SendRPCToAllOtherPlayers`, crew/faction/mission-team/player-list/all-player RPCs, and area chat) to `WorldServer.ClientSnapshot()`, which copies directly without LINQ.

Server-side `LoadTest --authenticated` (Release build, 200 dense clients, 30 s warmup + 60 s measure, two runs per case on 2026-11-26; prior `de85f822f` moving baseline is the PR1221 row above, since a fresh unmodified `move-200` run hit the unlocked `MarginServer.clientList` ramp crash fixed separately in PR1224):

| Case (2 runs each) | Status | Echo RTT mean ms | Visibility mean / max ms | Server CPU % | player-views ms | contacts + floor-probes ms | pvp-archives mean ms |
|---|---|---:|---|---:|---:|---:|---:|
| idle 200, master `de85f822f` | PASS / PASS | 16.4 / 19.5 | 14.6 / 38.7, 11.0 / 19.5 | 14.1 / 12.2 | 1.9 / 1.8 | 2.1 / 1.2 | 0.45 / 0.24 |
| idle 200, this change | PASS / PASS | 16.0 / 15.6 | 14.4 / 30.1, 11.3 / 32.6 | 13.8 / 10.1 | 2.0 / 1.6 | 1.7 / 1.3 | 0.18 / 0.14 |
| move 200, master (PR1221) | PASS / PASS | 212.3 / 60.3 | 160.1 / 698, 49.5 / 280 | ~404 | 44.9 / 17.7 | 6.5 / 1.6 | 77.7 / 17.9 |
| move 200, this change | PASS / PASS | 89.0 / 81.1 | 66.4 / 342.4, 51.6 / 229.9 | 361.3 / 362.2 | 32.5 / 26.1 | 5.9 / 3.6 | **0.13 / 0.29** |

`pvp-archives` drops from `77.7 / 17.9 ms` to `0.13 / 0.29 ms` under movement, `snapshot` stays at `0.53 / 0.60 ms`, both moving runs pass with 0 ping timeouts and steadier echo RTT (`89.0 / 81.1 ms`), and `view-range-lifecycle` + `VisibilityPassBenchmark` remain green.

## Twofish packet crypto and player-views batching (follow-up)

After movement coalescing and the `PvpArchives` fast path, profiling `move 200` on master `0654b9366` showed two dominant remaining costs:

1. **Per-datagram Twofish and packet-framing overhead (`Server CPU 342.3%`, `FlushProcessing 4.63 ms` mean, `366.8 s` total flush time over 60 s).** `MxoTwofish.Encrypt` and `Decrypt` recreated `TwofishEncryption` and ran `reKey()` from scratch on every UDP packet. Inside `TwofishBase`, every 16-byte block allocated `new uint[4]` and called `f32` 32 times per block, with every `f32` call allocating `new byte[4]` and executing 16 inline GF(256) multiplies (~2,700 heap allocations per 1,300-byte packet). `WorldEncryption.encrypt` also built three `PacketContent`/`MemoryStream` objects and called `ToArray()` six times per packet, `WorldClient.DecryptReceivedPacket` copied the packet into a discarded buffer before calling `decrypt`, and `DynamicArray.append(byte)` allocated a 1-byte array per byte.
2. **Visibility pass lock and scan overhead (`Visibility 40.0 ms` mean / `178.4 ms` max; `player-views 21.67 ms` mean / `126.2 ms` max — over half the pass).** `Maths.IsInCircle` called `Math.Pow(dx, 2)` twice per pair (~80,000 `Math.Pow` calls/pass). `CheckPlayerViews` acquired `lock(views)` ~39,800 times/pass inside the inner pair loop (`GetViewForEntityAndGo`), allocated a fresh `HashSet<ulong>` and copied the full view list via `Snapshot()` per player, and ran a second full 9-cell scan (`Near`) per player. `CheckPlayerMobViews`, `CheckForStaticObjectsViewsInRange`, and `CheckForStaticSubways` also allocated `yield return` grid iterators, per-player `HashSet`s, and `Snapshot()` copies every pass.

Changes:

- **Precomputed Twofish S-box tables and keyed cipher reuse (`TwofishBase`, `TwofishEncryption`, `MxoTwofish`).** `reKey` precomputes the four 256×32-bit keyed S-box/MDS tables once per key so `blockEncrypt` and `blockDecrypt` run with four table lookups, three XORs, and stack scalar registers (zero per-block heap allocations). `MxoTwofish` caches the keyed `TwofishEncryption` across IV changes and invalidates it on `SetKey`, verified bit-for-bit against the official Schneier Twofish-128/192/256 ECB test vectors.
- **Single-buffer `WorldEncryption` and hot-path allocation cleanup (`WorldEncryption`, `Crc32`, `WorldClient`, `DynamicArray`).** `WorldEncryption.encrypt` and `decrypt` assemble/verify packets in a single `ArrayPool<byte>` work buffer using `BinaryPrimitives` and `Crc32.checksum(byte[], int, int)`, preserving exact wire layout and `IV[4] != 0x00` while dropping outbound `encrypt` to one allocation (the returned wire buffer). Removed the discarded copy in `WorldClient.DecryptReceivedPacket` and switched `DynamicArray.append(byte)` to `MemoryStream.WriteByte`.
- **Single-lock visibility reconciliation (`ViewManager`, `ViewVisibleThread`, `SpatialGrid`, `Maths`).** Replaced `Math.Pow` in `Maths.IsInCircle` with direct double multiplication, added `SpatialGrid<T>.CollectNeighborCells`, indexed per-pass `PlayerSample`s directly on `WorldClient.VisibilitySampleIndex` (verified by reference), eliminated the duplicate `Near` scan and per-player `HashSet`/`Snapshot()` allocations, and reconciled each player's views under a single `lock(views)` acquisition (`ReconcilePlayerViews`) before spawning or deleting outside the lock. Applied the same non-copying stale-view scan to mob, booth, and subway checks.

In-process `VisibilityPassBenchmark` (Release, 200 dense players in Mara):

| Stage (ms per pass) | master `0654b9366` | this change |
|---|---:|---:|
| player-views, everyone moving | 10.29 mean / 13.67 max | **4.19 mean / 7.19 max** |
| player-views, nobody moving | 1.588 mean / 2.030 max | **0.914 mean / 1.125 max** |

Server-side `LoadTest --authenticated` (Release build, 200 dense clients, 30 s warmup + 60 s measure, run serially on 2026-11-26):

| Case | Status | Ramp s | Server CPU % | Echo RTT mean / max ms | Flush mean / max ms | ObjectFirstSend mean ms | Visibility mean / max ms | player-views mean / max ms | mob / static / subways mean ms |
|---|---|---:|---:|---|---:|---:|---|---|---|
| move 200, master `0654b9366` | PASS | 22.5 | 342.3 | 67.2 / 442.0 | 4.63 / 62.9 | 103.8 | 40.0 / 178.4 | 21.67 / 126.2 | 3.15 / 3.07 / 2.79 |
| move 200, this change (run 1) | PASS | 16.1 | **71.7** | **39.8 / 249.0** | **0.23 / 30.3** | **36.3** | **11.8 / 66.2** | **4.71 / 18.9** | **1.32 / 0.84 / 0.64** |
| move 200, this change (run 2) | PASS | 16.2 | **73.8** | **34.3 / 124.5** | **0.32 / 31.4** | **32.1** | **11.0 / 50.0** | **4.55 / 22.2** | **1.11 / 0.64 / 0.59** |
| idle 200, this change | PASS | 15.8 | **5.8** | **15.7 / 79.4** | **0.03 / 3.7** | — | **5.9 / 10.9** | **1.13 / 1.9** | **1.04 / 0.53 / 0.50** |

Under 200 dense moving clients (~9.0–9.2 million object updates delivered per 60 s window, 0 failures, 0 ping timeouts), server CPU drops by **4.7×** (from `342.3%` down to `71.7% / 73.8%`, less than one logical core), `FlushProcessing` drops **15–20×** (`4.63 ms` -> `0.23 / 0.32 ms`), `ObjectFirstSend` drops **3×** (`103.8 ms` -> `36.3 / 32.1 ms`), Echo RTT drops to `39.8 / 34.3 ms`, and `player-views` drops **4.7×** (`21.67 ms` -> `4.71 / 4.55 ms`).

## World-NPC motion, NavMesh projection, and 12-bit SSEQ wraparound (follow-up)

Profiling `move 200 --world-mobs` on master `098f15526` showed `mob-motion` dominating the visibility pass (`15.62 ms` mean, `64 ms` P95, `158.1 ms` max — over half of the `31.0 ms` mean / `179.2 ms` max pass):

1. **O(S²) NavMesh projection and unindexed `Locate` scans (`NavMesh`, `NavMeshProjection`).** Every wander-goal attempt in `MobMotion.Tick` (up to 12 per mob) called `NavMesh.Project`, which ran an O(S²) all-pairs BFS calling `Connects()`/`Clip()` across every surface in the sector (including thousands of indoor building-prefab floors even though outdoor wandering rejects `indoors`), then ran `Closest()` + LINQ `Concat`/`Enumerable.Range` across every polygon regardless of `maxDistance`. `NavMesh.Locate` also took `loadLock` on every call and linearly scanned every outdoor polygon in the sector with per-edge `Math.Sqrt`.
2. **Mob visibility and wander broadcast churn (`MobMotion`, `Mob`, `WorldServer`, `MobPackets`, `ViewManager`, `ViewVisibleThread`).** `MobMotion.Tick` copied every client's full view list (`Snapshot()`) every pass; `Mob.updateAnimation` called `packet.ReturnFinalPacket()` and synchronously called `FlushQueue()` for all 200 viewers on `ViewVisibleThread` per wandering mob; `CheckPlayerMobViews` locked `viewMan.views` once per nearby mob instead of batching per player; and `WorldPacket.content` grew a 0-capacity `MemoryStream` on every outbound packet.
3. **12-bit SSEQ wraparound and pre-SSEQ retirement race (`SequencedMessage.IsAckedBy`).** `IsAckedBy` used non-wrapping `neededAck <= ackSeq`, stranding pre-wrap object messages (`neededAck` near `4095`) across the 12-bit wrap (`4095 -> 0`) and prematurely retiring post-wrap messages on stale `4095` ACKs; it also matched `neededAck == 0` if an ACK arrived between `MarkObjectSent` and `WorldPacket.getFinalData` assigning `neededAck`.

Changes:

- **Indexed `NavMesh.Locate` and cached `NavMesh.Project` (`NavMesh.cs`, `NavMeshProjection.cs`).** Switched `districts` to lock-free reads (`ConcurrentDictionary`), precomputed polygon outward unit edge normals on load, added a lazy per-sector 500-unit outdoor polygon spatial grid preserving exact `(region, polygon)` tie-breaking order, cached `Surface` windings and 3D `(x, y, z)`-bucketed same-sector adjacency graphs, added AABB pre-filtering and zero-LINQ candidate evaluation in `ProjectSurfaces`/`Covered`, and separated `OutdoorSurfaces` (`outdoorsOnly: true`) so outdoor mob wandering never builds or scans indoor skyscraper prefabs.
- **Zero-copy mob motion, batched mob views, and deferred wander flushes (`MobMotion`, `Mob`, `WorldServer`, `MobPackets`, `ViewManager`, `ViewVisibleThread`, `WorldPacket`, `DynamicArray`).** Added `ViewManager.CollectVisibleMobs` and single-lock `ViewManager.ReconcileMobViews`, serialized mob updates once per broadcast via `SendNpcUpdateDataFast` with `deferFlush: true` (`MarkFlushPending`) for `Mob.updateAnimation`, pre-sized `SnapshotMobs`/`BuildMobByEntity`, and pre-sized `WorldPacket`'s `DynamicArray` to `bufferSize + 32`.
- **12-bit modular SSEQ ACK check (`SequencedMessage.cs`).** Tracked `hasNeededAck` on `setNeededAck` (masked to 12 bits) and compared with `((ackSeq - neededAck) & 0xFFF) < 0x800`.

Server-side `LoadTest --authenticated --clients 200` (Release build, 30 s warmup + 60 s measure, run serially on 2026-11-26):

| Case | Status | Ramp s | Server CPU % | Echo RTT mean / max ms | Flush mean / max ms | Visibility mean / P95 / max ms | mob-motion mean / P95 / max ms | mob-views mean / max ms |
|---|---|---:|---:|---|---:|---|---|---|
| move 200 + world NPCs, master `098f15526` | PASS | 18.3 | 84.9 | 43.9 / 203.7 | 0.31 / 48.2 | 31.0 / 128 / 179.2 | 15.62 / 64 / 158.1 | 4.14 / 29.8 |
| move 200 + world NPCs, this change (run 1) | PASS | 16.9 | 82.0 | **40.2 / 157.0** | **0.29 / 45.6** | **21.3 / 64 / 97.6** | **3.14 / 8 / 35.4** | 4.44 / 30.3 |
| move 200 + world NPCs, this change (run 2) | PASS | 17.2 | 95.2 | **43.1 / 155.2** | **0.28 / 40.9** | **23.9 / 64 / 71.0** | **3.20 / 8 / 19.4** | 4.97 / 24.7 |
| move 200 (test catalog), this change | PASS | 21.4 | 91.5 | 43.7 / 141.4 | **0.23 / 22.9** | **14.9 / 32 / 37.6** | **1.41 / 4 / 2.8** | **1.80 / 11.0** |

Under 200 dense moving clients with the full world NPC catalog (~9.1–9.2 million object updates per 60 s window, 0 failures, 0 ping timeouts), `mob-motion` drops **5.0×** in mean (`15.62 ms` -> `3.14 / 3.20 ms`), **8×** in P95 (`64 ms` -> `8 ms`), and **4.5–8.1×** in max (`158.1 ms` -> `35.4 / 19.4 ms`), bringing overall visibility max from `179.2 ms` down to `97.6 / 71.0 ms` and Echo RTT max from `203.7 ms` down to `157.0 / 155.2 ms`.

## Lock-free view lookup, partitioned `ViewList` indexes, and incremental mob snapshot (follow-up)

Profiling `move 200 --world-mobs` on master `3bdc52c79` (`18.53 ms` visibility mean / `136.3 ms` max, `83.8%` CPU) showed the remaining non-flush costs concentrated in view lock contention and per-pass view/grid scans:

1. **Cross-thread `viewMan.views` lock contention and UDP endpoint string churn (`ViewManager`, `WorldServer`).** `SendMovementUpdateToAllPlayers` called `client.viewMan.FindViewForEntity(entityId)` ~160,000 times/s under 200 moving players, acquiring `lock(views)` across every viewer on the mover's packet thread and contending with `ViewVisibleThread`. `WorldServer.ProcessDatagram` also formatted `Remote.ToString()` and locked `WorldServer.Clients` on every inbound UDP packet.
2. **Scanning ~200 player views in non-player visibility stages (`ViewManager`, `ViewVisibleThread`, `MissionContactNpcs`, `DataMining`).** In a 200-player crowd each client holds ~199 `GoID == 12` player views and only ~8 non-player views, yet `ReconcileMobViews` (`3.81 ms`), `CollectVisibleMobs` (`mob-motion`, `3.14 ms`), `CollectStaleViewsByGoId` (`static-objects 1.22 ms`, `subways 1.02 ms`), `MissionContactNpcs.CheckViews` (`1.52 ms`, calling `Snapshot()` + `GroupBy` + `Math.Sqrt`), and `DataMining.Check` (`per-player 1.19 ms`, calling `Snapshot().Where(...).ToList()`) scanned or copied all ~210 views per client on every 500 ms pass.
3. **Full 7,000-mob grid rebuilds and `HashSet<ulong>` reconciliation (`ViewVisibleThread`, `SpatialGrid`).** `snapshot` (`1.96 ms`) copied all ~7,000 world mobs and rebuilt fresh `SpatialGrid<Mob>` / `Dictionary<ulong, Mob>` structures every 500 ms even when only a handful of visible mobs wandered, and `CheckPlayerViews` / `CheckPlayerMobViews` built and queried `HashSet<ulong>` sets on every pass.

Changes:

- **Lock-free `FindViewForEntity` and partitioned `ViewList` indexes (`ViewManager.cs`, `ClientView.cs`).** `ViewList` maintains a lock-step `ConcurrentDictionary<ulong, ClientView>` for first-match lock-free `FindViewForEntity`, plus `byGoId` (`Dictionary<uint, List<ClientView>>`) and `nonPlayerViews` (`GoID != 12`). `ReconcilePlayerViews` and `ReconcileMobViews` use a per-`ViewManager` `ReconcileStamp` instead of `HashSet<ulong>`, `CollectStaleViewsByGoId` scans only `byGoId[goId]`, and mob/DataMining scans touch only `nonPlayerViews`.
- **Incremental mob visibility cache and zero-alloc stages (`ViewVisibleThread.cs`, `SpatialGrid.cs`, `MissionContactNpcs.cs`, `DataMining.cs`).** Added `SpatialGrid<T>.AddToCell`/`RemoveFromCell`/`Clear` and `MobVisibilityCache` to update moved mobs in place without reallocating 7,000-mob grids/dictionaries when membership is unchanged; cached `MissionContactNpcs` district buckets with squared-distance checks and `CollectStaleViewsByGoId`; replaced `DataMining.Check` LINQ/`Snapshot()` copies with scratch lists and `CollectNonPlayerViewsInEntityRange`; checked lock-free `FindViewForEntity` before locking for already-spawned booths/subways.
- **Single-receive-thread UDP endpoint cache (`WorldServer.cs`).** Caches active `EndPoint -> WorldClient` entries in `ProcessDatagram`, skipping `Remote.ToString()` and `lock(Clients)` on steady-state datagrams while falling back to the locked registry on miss, disconnect, or dictionary swap.

In-process `VisibilityPassBenchmark` (Release, 200 dense players in Mara):

| Stage (ms per pass) | master `3bdc52c79` | this change |
|---|---:|---:|
| player-views, everyone moving | 4.19 mean / 7.19 max | **3.45 mean / 5.26 max** |
| contacts, everyone moving | 1.1–1.2 mean / 2.1–2.5 max | **0.34 mean / 1.09 max** |

Server-side `LoadTest --authenticated --clients 200` (Release build, 30 s warmup + 60 s measure, run serially on 2026-11-27):

| Case | Status | Ramp s | Server CPU % | Echo RTT mean / max ms | Visibility mean / P50 / max ms | snapshot ms | mob-motion ms | contacts / static / subways / per-player ms |
|---|---|---:|---:|---|---|---|---|---|
| move 200 + world NPCs, master `3bdc52c79` | PASS | 18.7 | 83.8 | 42.6 / 260.7 | 18.53 / 32 / 136.3 | 1.96 | 3.14 | 1.52 / 1.22 / 1.02 / 1.19 |
| move 200 + world NPCs, this change (run 1) | PASS | 16.2 | **75.8** | **35.4 / 110.1** | **10.53 / 16 / 64.3** | **0.90** | **1.65** | **0.11 / 0.15 / 0.10 / 0.28** |
| move 200 + world NPCs, this change (run 2\*) | PASS | 16.2 | **76.9** | 47.6 / 385.7 | 21.35 / **16** / 197.1 | 1.84 | 5.39 | **0.14 / 0.22 / 0.13** / 1.67 |
| move 200 + world NPCs, this change (run 3) | PASS | 16.5 | **77.0** | **34.4 / 167.6** | **10.52 / 16 / 29.2** | **0.85** | **1.33** | **0.11 / 0.23 / 0.10 / 0.22** |
| move 200 (test catalog), this change | PASS | 20.9 | **69.7** | **33.4 / 108.9** | **8.61 / 8 / 27.9** | **0.29** | **0.05** | **0.11 / 0.37 / 0.11 / 0.40** |

\*Run 2 encountered a transient host CPU stall (`FlushProcessing` max 241.9 ms, `per-player` max 120.1 ms) while still holding `76.9%` Server CPU and `16 ms` median visibility; runs 1 and 3 on a quiet host matched at `10.53 / 10.52 ms` visibility mean (`1.76×` faster than `3bdc52c79`), with `contacts` **13.6×** faster (`1.52 -> 0.11 ms`), `subways` **10.4×** faster (`1.02 -> 0.10 ms`), `static-objects` **5.4–8.3×** faster (`1.22 -> 0.15 / 0.23 ms`), `per-player` **4.3–5.4×** faster (`1.19 -> 0.28 / 0.22 ms`), `snapshot` **2.2–2.3×** faster (`1.96 -> 0.90 / 0.85 ms`), `mob-motion` **1.9–2.4×** faster (`3.14 -> 1.65 / 1.33 ms`), and Echo RTT dropping to `35.4 / 34.4 ms`.

## Synchronous UDP send, parallelized `FlushThread`, and compact visibility indexes (follow-up)

After lock-free `FindViewForEntity` and partitioned `ViewList` indexes (`6c4318a62`), `FlushProcessing` accounted for ~83% of remaining server CPU (`38.3–42.2 s` over 60 s on the single `FlushThread` — ~64% saturation of one core):

1. **Single-threaded flush sweep and sleep drift (`FlushThread.cs`).** `FlushThreadProcess` iterated all 200 clients sequentially on one thread (~35–40 ms) and then slept a full `50 ms`, stretching the effective flush cadence to ~85–90 ms and pushing `Echo RTT` P95, `RpcFirstSend` P95, and `ObjectFirstSend` P95 to `128 ms` (`RpcRetainedMax` 51–134).
2. **Per-datagram async UDP allocations and IOCP overhead (`WorldClient.cs`, `WorldServer.cs`).** `WorldClient.sendPacket` called `socket.BeginSendTo(..., new AsyncCallback(FinalSendTo), Remote)` on every outbound datagram (~300k/min `AsyncCallback` + `IAsyncResult`/IOCP completions), and `WorldServer` relied on the OS default 64 KB UDP socket buffers.
3. **Indirect view lookups and Gen2 mob chasing (`ViewManager.cs`, `ViewVisibleThread.cs`).** `ViewList` stored every single-view entity behind a `List<ClientView>`, `ReconcileMobViews` looked up every nearby mob twice, and `CheckPlayerMobViews` chased scattered Gen2 `Mob` objects across ~60,000 neighbor checks per pass.

Changes:

- **Synchronous UDP `SendTo` and 1 MB socket rings (`WorldClient.cs`, `WorldServer.cs`).** Replaced `socket.BeginSendTo` + `new AsyncCallback(FinalSendTo)` with direct synchronous `socket.SendTo` (zero per-packet async allocations, strict in-order UDP dispatch) and raised UDP `ReceiveBufferSize`/`SendBufferSize` to 1 MB with graceful fallback on constrained hosts.
- **Parallel, drift-compensated `FlushThread` (`FlushThread.cs`).** Reuses the client snapshot list, subtracts elapsed sweep time from `FlushIntervalMs` (clamped to `>= 1 ms`), and fans out `TickFlush()` via bounded `Parallel.For` (`MaxDegreeOfParallelism` 2–4) with per-client fault isolation once `clients.Count >= 16`.
- **Single-hop `ViewList` direct entity map & compact `MobEntry` sweeps (`ViewManager.cs`, `ViewVisibleThread.cs`).** Replaced `ViewList.byEntity` with `directByEntity` (`Dictionary<ulong, ClientView>`) plus a lazy `multiByEntity` overflow dictionary only when duplicate entityIds exist; split `nonIntroducibleNearby` from `introducibleMobs` for single-lookup `ReconcileMobViews`; cached compact `MobEntry` structs indexed by `SpatialGrid<int>` in `MobVisibilityCache` and passed a single `List<PlayerSample>` into `ReconcilePlayerSamples`.

Server-side `LoadTest --authenticated --clients 200` (Release build, 30 s warmup + 60 s measure, run serially on 2026-11-27):

| Case | Status | Server CPU % | Echo RTT mean / P95 / max ms | RpcFirstSend mean / P95 / max ms | ObjectFirstSend mean / P95 ms | RpcRetainedMax | mob-views P50 ms |
|---|---|---:|---|---|---|---|---:|
| move 200 + world NPCs, master `6c4318a62` (run 1) | PASS | 75.8 | 35.4 / 128 / 110.1 | 22.1 / 128 / 105.6 | 33.1 / 128 | 51 | 4 |
| move 200 + world NPCs, master `6c4318a62` (run 3) | PASS | 77.0 | 34.4 / 128 / 167.6 | 20.8 / 128 / 104.6 | 34.1 / 128 | 134 | 4 |
| move 200 + world NPCs, this change (run 1) | PASS | 78.4 | **33.9 / 64 / 125.0** | **19.4 / 64 / 111.0** | **29.1 / 64** | **4** | **2** |
| move 200 + world NPCs, this change (run 2) | PASS | **71.1** | 36.8 / **64 / 124.9** | 22.8 / **64 / 79.5** | **27.8** / 128 | **36** | **2** |
| move 200 (test catalog), master `6c4318a62` | PASS | 69.7 | 33.4 / 128 / 108.9 | 19.5 / 64 / 148.8 | 30.9 / 128 | 78 | 0.25 |
| move 200 (test catalog), this change | PASS | 72.1 | **31.9** / 128 / 170.6 | **17.1 / 64 / 124.6** | **24.1 / 64** | **6** | 0.25 |

Under 200 dense moving clients with the full world NPC catalog (~9.2 million object updates/60 s, 0 failures, 0 timeouts), holding a crisp 50 ms parallel flush cadence cuts **Echo RTT P95** (`128 -> 64 ms`), **RpcFirstSend P95** (`128 -> 64 ms`), and **mob-views P50** (`4 -> 2 ms`) in half, drops **ObjectFirstSend** mean by ~15–18% (`33.1–34.1 -> 27.8–29.1 ms`), and reduces peak RPC backlog (`RpcRetainedMax`) from `51–134` down to `4–36` (`6` on the test catalog).

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
