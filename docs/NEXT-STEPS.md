# Scaling Plan (Status)

See `docs/TODO.md` for the broader modernization checklist and `docs/PACKET-RESEARCH.md` for the packet-format research workflow.

## Completed
- Static object + subway spatial indices with neighbor-only queries.
- Per-request EF contexts for auth/margin (no shared `MatrixDbContext`).
- Outbound flush loop with throttled `FlushQueue()` to batch sends.

## Validation To Run
- Done: 50/100/200 authenticated clients, idle ([baseline](SCALE-BASELINE.md)) and moving, with world NPCs and per-stage visibility-pass durations ([movement round](SCALE-MOVEMENT.md)). Broadcasts no longer hold the `Clients` lock, so 200 dense moving clients now pass (echo about 113 ms); the next target is the player-views stage.
- Done (headless): view spawn/despawn for players, NPCs and hardline booths (`view-range-lifecycle`); subway cars (`subway-*`).
- Verify auth/margin flows after DB context changes.
- Capture or acquire vendor buy/sell packet dumps; the imported community dump set does not currently include those headers.

## Risks To Watch
- View removal logic should not delete views for unrelated entity types.
- Context lifetime changes could expose lazy-loading or stale reads.
- Flush batching must not delay critical packets (combat, movement, logout).
- Protocol 04 enum values are local decoded values; packet dumps often contain encoded command bytes such as `80 c8` and `81 8e`.
