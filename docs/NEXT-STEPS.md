# Scaling Plan (Next Steps)

## Goals
- Keep the client protocol unchanged while increasing concurrent players and reducing per-tick CPU cost.
- Remove remaining global/shared bottlenecks and O(n^2) work in the world loop.

## 1) Static Object + Subway Spatial Index
- Build a per-district spatial grid for static objects and subways.
- Update view logic to query only nearby cells (no full scans).
- Keep the existing 5,000 range behavior and view creation/deletion semantics.

Notes:
- Build the static grid once after `DataLoader.getInstance()` and rebuild if data reloads.
- Subways can be indexed per district; they are low-count but currently scanned against all clients.

## 2) Thread-Safe DB Context Usage
- Remove cross-thread usage of `Store.matrixDbContext` and create per-request contexts instead.
- Audit any shared EF contexts in auth/margin/world handlers to avoid contention or data races.
- Prefer short-lived contexts for read-heavy operations (world list, character list, etc).

Notes:
- Keep connection pooling enabled and avoid long-lived transactions.
- Consider a small factory helper for `MatrixDbContext` creation.

## 3) Outbound Flush Loop per Client
- Decouple `FlushQueue()` from RPC handlers and batch sends on a fixed tick (e.g. 50-100ms).
- Add a lightweight per-client send loop that coalesces queued messages and handles resend logic.
- Ensure ACK-only messages still flow on time-sensitive paths.

Notes:
- Guard the send loop with backpressure (queue size limit) and metrics.
- Preserve the current packet format and encryption timing.

## Validation
- Load test with 50/100/200 simulated clients; measure CPU and tick duration.
- Verify visibility: spawn/despawn behavior for players, mobs, and static objects.
- Verify auth/margin flows after DB context changes.

## Risks to Watch
- View removal logic should not delete views for unrelated entity types.
- Context lifetime changes could expose lazy-loading or stale reads.
- Send loop batching must not delay critical packets (combat, movement, logout).
