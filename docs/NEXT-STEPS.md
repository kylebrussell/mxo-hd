# Scaling Plan (Status)

See `docs/TODO.md` for the broader modernization checklist and `docs/PACKET-RESEARCH.md` for the packet-format research workflow.

## Completed
- Static object + subway spatial indices with neighbor-only queries.
- Per-request EF contexts for auth/margin (no shared `MatrixDbContext`).
- Outbound flush loop with throttled `FlushQueue()` to batch sends.

## Validation To Run
- Load test with 50/100/200 simulated clients; measure CPU and tick duration.
- Verify visibility: spawn/despawn behavior for players, mobs, static objects, and subways.
- Verify auth/margin flows after DB context changes.
- Capture or acquire vendor buy/sell packet dumps; the imported community dump set does not currently include those headers.

## Risks To Watch
- View removal logic should not delete views for unrelated entity types.
- Context lifetime changes could expose lazy-loading or stale reads.
- Flush batching must not delay critical packets (combat, movement, logout).
- Protocol 04 enum values are local decoded values; packet dumps often contain encoded command bytes such as `80 c8` and `81 8e`.
