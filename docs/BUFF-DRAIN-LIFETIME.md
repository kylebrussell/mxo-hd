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

## Qualified producer and proof (2026-11-15)

Producer `db39cef7cae7eec8141e2bcd9bb8628468bfdcc1` is based on master `803ee63ac58bf0a997cf9977ac49c18e8810ff67`, with data unchanged. Focused Release265 and the unfiltered Release6,249 tests passed. Actual Debug/Release builds, both fixture plans/build-info and exact-one case/mode discovery passed. Newest actual Debug HDS SHA256 is `90EBD462DDEFE846863AF8C5F2E9985B8E34BD0F1D78FC480392FA6F12E40827`; Release driver `F8543A5CFFBB193A29A32874C4503244F796346DFD508CEBB537AE8B5CAA46BA`; Release NativeQa `A21B9441B9B6D92EA290DF2CFD40BF6CF5CE30034A158B46EFE4DE69E156AD81`.

The normal case passed in60.5s, held process exit0. Character1001 used normal16 on in session1, normal80fe00 Quit, the existing33-second reservation, same-character relog, normal16 on and one off. Both sessions charged exactly1IS per tick: actual remaining1979 then1986 with ordinary intervening regeneration1987; complete standalone9-byte own IS records corroborated actual totals. After the second-session off, there were0 drain events for3.2s and only ordinary regeneration1994. No drain/regen/timer/cost override was used. Parsers were clean: session1 35RPC/12viewlists, session2 36/13. This relog uses a new UDP endpoint; same-client reset, reused numeric generation, old callback/new activation and rollback are controlled scheduler/unit proofs, not a separate live claim.

Driver51712/server51012, schema`mxotest_run_1_1cab74ce9d`; independent schemaCOUNT0/all6 observed canonical process identities gone at23:20:50Z. Raw `%TEMP%/mxo-buff-drain-mxotest_run_1_1cab74ce9d.json` SHA256 `286B34FE41F1B67CAA8E09EDEF5CA745AC141FCA2E74AF2CD0CB8CA3579704A9`.

One explicit actual-mode bootstrap passed at the same source, held exit0: runner8628/server20268/schema`mxotest_bootstrap_f82454b896`, declared and actually seeded abilities2/17/16, real canary login and state serialization. The inherited AfterStart hook is a no-op (0 custom/1 inherited), not additional gameplay or native proof. Independent schemaCOUNT0/all10 observed identities gone at23:22:22Z. Process observation is sampled ancestry, not exhaustive OS history or a blanket no-PID-reuse claim.

All five currently produced drains13/16/442/941/943 have an explicitly tested pure termination contract.941/943 retain their health-change listeners while active; termination removes listener membership and sends the existing Fx/stat off records without invoking health/term/observer callbacks.442 retains the exact sourced15-second timer/records through the shared captured-owner cooldown publisher. The constructor captures transport before upgrade/buff locks; resource claim and publication use the original transport epoch in separate outer gates, with captured PlayerCharacter resources. Synchronized reset tests cover the boundary before resource mutation and between mutation/publication.

Retained qualification failures include zero-test fixture syntax errors and262pass/1fail contract inventory at66144f8e, which correctly exposed the initial omission of941/943. The corrected265 focus and6,249 full result are separately attributed. Proof root `%TEMP%/mxo-buff-drain-lifetime-20261115`, `final-proof-summary.json` SHA256 `2796BD80FE5DBF902616F0A60F1252CDB87FCE2B15A926C6270B2DF3E3A13949`. No native/client lock/main database action or live retry occurred.
