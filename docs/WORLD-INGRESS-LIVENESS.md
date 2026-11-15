# Endpoint ingress and inactive cleanup

An active connection can wait behind a long serialized packet handler. Previously only entering `processPacket` refreshed `lastUsedTime`. A visibility pass could mark that owner inactive, block in mission suspension, then remove it after fresh packets had resumed. The next datagram created another owner with counters starting at zero; the client correctly discarded those old-numbered reliable replies.

Retained Machines producer e277185b failed128.8s before its third-floor Ride. The saved lobby54800/145/30700 and door54800/3345/30700 were valid; the street teleport executed. `Teleported to0/0` is the helper's fixed rotation/animation text, not a zero position. Logs show a32s processing gap, stale inactive removal, SSEQ674→2 and RPC counter0 without reset handshake. The raw processing log does not itself measure socket arrival time. Proof/cleanup remain in `Temp/mxo-machines-next-five-refresh-20261114/fresh-e277185b-diagnosis.json` (schema77ff139581 COUNT0; driver32864/server55228/conhost48024 gone).

`QueuePacket` now stamps actual raw endpoint ingress before the serialized worker, preserving the existing pre-decrypt activity policy and30s inactivity limit. Old queued processing cannot extend silence. Public direct `processPacket` preserves its one-arrival stamp; production calls it only through the queued worker, now routed to an unstamped core. Constructor and test-fixture `lastUsedTime`/`alive` fields remain canonical. Production sets alive true only in the two constructors; an explicitly stopped or claimed endpoint cannot be revived by late work or setting Alive true. One existing unit fixture that reused a stopped actor now creates a new owner for subsequent cases.

Inactive cleanup atomically validates the current dictionary owner and latest activity, claiming terminality before Trade/Combat/MissionSuspend/offline effects. It retains that owner in the dictionary through cleanup, drops late/queued work, and removes only the same reference. Dictionary/liveness gates never cover mission locks or SQL. A failed cleanup releases only the cleanup-in-progress lease for the existing retry behavior; the endpoint remains terminal and mapped. Bots retain their no-idle-expiry policy.

## Explicit diagnostic

Mode/scenario `world-ingress-liveness` uses a normal Mara CSR110 profile. `?ingresspause` is default off, requires actual dev1 + an owned `mxotest_` database + `MXO_INGRESS_LIVENESS_QA=1`, authenticated current on-world CSR110 and exact current endpoint ownership. It is one use per endpoint. After flushing `INGRESS PAUSE START35`, it deliberately blocks only that endpoint's real packet worker35s. It changes no mission, source, inventory, reward, RNG or runtime deadline. The completion proof records actual ingress generation, queued packets, owner/world/life/transport, elapsed time and endpoint; changed owners/lifecycles receive no success reply.

The headless case sends normal keepalives during that delay, checks online/mission/wallet/XP at32s and afterward, requires actual ingress while the worker is blocked, continued SSEQ/reliable RPCs, no reset/removal, and ordinary8148/8149 after resume. A second use must refuse. Full raw packets, Margin data, account snapshots, diagnostics and source/schema/PIDs are retained on failure as well as success. Unit coverage uses a fake clock and the actual blocked serialized queue, stale dead snapshots, replaced owners, terminal claims, explicit stop and bot policy. No fresh server starts without a coordinator grant.

## Native one-pass checklist

1. Claude alone starts the frozen mode using the established disposable NativeQa procedure; no Codex client or lock access. Confirm CSR110 at Mara and a normal UI action works. Record mission, wallet and connection identity.
2. Enter `?ingresspause` once. After START35, make ordinary small movements during the explicit35s delay so the native client sends real activity. Do not relog, teleport, retry or assume UI responsiveness while its worker is intentionally paused.
3. Require DONE35, the same connection/world, no reset/disconnect and responsive ordinary UI afterward. Record actual ingress delta/current-owner evidence and unchanged mission/wallet/online state. A second command must refuse promptly.
4. Preserve failures and raw/source/process/schema proof, then use normal shutdown/reset. This diagnostic does not fix or certify normal cold-admission latency, rendering, pathfinding or native mission completion.

Current source/build/focused/full/live proof is pending validation. No native PASS is claimed.
