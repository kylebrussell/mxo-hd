# Buff drain lifetime

This fixes activation ownership, not the sourced drain amounts or intervals. Scheduled callbacks retain the exact Active reference, PlayerData/player identity, character, world/life, district, transport epoch and actual endpoint owner. A stale callback cannot look up a newer activation by a reused numeric generation, charge it or remove it. Removing an activation permanently cancels its drain capability, including a rolled-back/reinserted old snapshot.

Normal `80fe00` quit previously only released the database online flag and answered with the exit RPC: its still-mapped endpoint could remain alive during the ordinary reservation. The confirmed-quit hook cancels existing drain capabilities. Loading Area/world reset already drops OnWorld and advances world/transport scope. No new global registry, changes to drain values/costs/timers, or changes to unrelated RPC resend, weapon equipment or hotbar persistence are included.

The regression is `buff-drain-lifetime`, with normal16 on, actual1IS ticks, normal Quit, the existing33s reservation wait, same-identity login, normal16 on and one off. It retains raw sessions and source/process/schema on failures. Standalone exact9-byte IS updates corroborate recorded charged totals; the reader does not claim to parse every coalesced attribute shape. Unit tests exercise same-client numeric-generation reuse, old callback/new activation, cancellation rollback, reset/world/life/owner/endpoint/character changes and scheduler callbacks. No forced hit/RNG, cooldown reset or injected buff.

## One-pass native checklist

1. Prepare actual `buff-drain-lifetime` mode and enter as CSR110. Use Actions to toggle Punishing Blows16 on. Observe the normal1IS/s drain; ordinary regeneration may also occur.
2. Use normal Quit. Reconnect the same character to the same running fixture without preparing/reseeding it again.
3. Use Actions to turn16 on, confirm its ordinary modifier/presentation, then turn it off once after the normal duplicate-use window. Confirm continuing1IS/s loss stops; ordinary regeneration is allowed.
4. Toggle on/off once more and repeat the check. Do not use broker termination, forced reset, altered timers or combat-depth commands as proof.

Native observation and the normal headless case are separate from the controlled scheduler tests. Runtime source and final qualification pins are recorded in the PR; no new native pass is claimed here.
