# Mission native follow-ups

This separate low-priority package addresses the three observations from the native-passed open-floor trio. Its base is master38a15939 after PR198. It does not borrow unmerged PR204 production code or change that branch while Claude tests it. Claude owns native QA and merging; the remaining dependent PRs retain priority. Refresh this package when those merges change its base.

| Observation | Change | Native mode and detailed checklist |
|---|---|---|
| Final goal near the elevator immediately advanced the mission | Generated sites with distinct mission/lobby floors require a committed matching departure. Delayed callbacks are scoped to the original mission/phase/site/lifetime; failed checkpoints restore transport state. | `mission-leave-transition`, [departure checklist](MISSION-LEAVE-TRANSITION.md) |
| Safe dossier and loot window remained after Take All | `81 20` acknowledges exact successfully claimed identities; supported `81 1a` closes an emptied mission container. Full bags retain items. SQL rollback grants nothing and sends a lifecycle-fenced empty acknowledgement to clear the pending cursor. NPC claims use the same evidenced removal semantics. | `mission-loot-claim-close`, [loot checklist](MISSION-LOOT-CLAIM-CLOSE.md) |
| Box S3 standing point was exactly at the wall | Prefer fully valid standing routes that avoid cells proven to contain no strictly interior float32 coordinate on an axis. Retain valid original routes as fallback. All three Box bodies and sources remain fixed. | `mission-standing-interior`, [standing checklist](MISSION-STANDING-INTERIOR.md) |

## Scope and limits

The departure rule is authored reconstruction, not a recovered universal retail boundary. Authored exits, escort predicates, explicit leave/upload rules and generated ground-floor behavior keep their prior semantics. Old checkpoints do not gain departure evidence from position alone. Normal mission rides and the conservative matching ordinary panel path establish evidence; unsupported panel bindings do not. Nested transport is rejected before movement rather than being falsely committed inside another checkpoint owner's batch.

Loot item/goal state and success output share the existing mission checkpoint transaction. The empty rollback reply removes zero identities and does not close the window. Queue ordering guarantees claim/close before goal RPCs; object sections still precede RPC sections on the transport, so this is not a close-before-despawn wire guarantee. Tutorial wardrobe granting remains a separate existing inventory-safety gap. Multi-item helper tests do not establish a production multi-item loot window: the current mission Open path lists its first Search item.

Standing selection changes no model centre, source, quaternion, scene/static bound, usable range or search budget. It adds no avatar radius or comfort inset. Known normal three-Box changes are only S2/S3 Standing, Route and RouteSurfaces; S1, all bodies and source fingerprints, order1/2/3 and303body/5graph work remain exact in source-matched offline comparisons. Safe, Computer1407 and Computer1415 complete placement DTOs remain exact. Old admitted Policy3 witnesses still replay without reselection. Point-route validity is not native walking, LOS or picking proof; S2 still has a NAV-boundary standing point.

## Backend validation and native pass

Exact heads, DLL hashes, full/focused test results and frozen fresh-server results are published in the PR and shared handoff before READY FOR NATIVE. Component-only passes retain their original commit attribution. Generated ability coverage is an artifact, not a feature commit.

The standing scenario includes the normal three-Box acceptance, unchanged-source/body checks, same-world abort/reaccept, cold fixed replay, all three Open goals, stationary post-goal wait and ordinary exit. It exercises both standing and departure behavior; the `mission-leave-transition` alias uses the same protocol flow. The loot scenario separately covers a full bag, ordinary deletion/reopen, real late SQL failure in the harness-owned schema, empty retry reply, current-view reannouncement, successful retry, exact claim/close RPCs and no duplicate item.

Claude's one-pass native sequence uses CSR_Tester110 only, with the usual client lock. Run the three discovered modes through their linked checklists, preserving source scenery and using actual checkpoint Root/Route/Standing rather than historical raw anchors. Use ordinary hover/radial/item menus. Record camera, stopping positions, visibility, Open/Take, busy cursor, UI closure, stationary phase behavior, relog and ordinary door/panel departure; disclose any QA aids. Never select TheNeo. On release, reset CSR110 to Mara23828/495/13984, full health, pvpflag0 and mission986 only. No Defender/firewall or virtual-display changes are part of this work.

## Verified backend results

All integration runtime files are identical across producer commits14d104c9,6e11848e and05b17587; only fixture/docs changed. The production hds tree is7f660d3feb035b4e5832f1b13e70ba3abbd7870e and data treee4ebad66d72e374cb94c8247f44b14a056ae418f. Full runtime suite5,214 passed; final50 focused checks, both actual Debug/Release builds, six read-only mode plans and two build-info checks passed. Final CI is linked by exact current head in the PR/handoff. Generated coverage remains an artifact.

| Frozen producer | Normal-wire result |
|---|---|
|6e11848e|mission-loot-claim-close PASS27.8s: full bag, real late SQL rollback with zero ownership/goals and empty retry acknowledgement, fresh Safe reannouncement, exact claim/close/goal ordering and one earned dossier/no duplicate.101RPCs/24view sections, zero parser problems.|
|6e11848e|mission-standing-interior PASS60.1s: same normal three bodies/sources, updated S2/S3 routes, abort/reaccept, cold fixed replay, stationary final-goal waiting and ordinary exit.90/83RPCs and21/22view sections, zero parser problems.|
|05b17587|mission-leave-transition PASS58.4s: real departure SQL failure preserves exact pose25700/2145/13800, phase0, all3 goals, identity/source/site and no departure; trigger removal plus the same normal door request advances to phase1.90/97RPCs,21/28view sections, zero parser problems.|
|05b17587|full-bag-tutorial-loot19.3s, container-and-corpse-loot26.2s, retained-corpse-loot23.3s and mission-relog-corpse-loot32.5s all PASS.|
|05b17587|Qualified mission-computer-1407 PASS94.9s and mission-computer-anchors1415 PASS97.2s, each with five parser-clean sessions, active carry/cold replay, explicit ordinary departure, two earned receipts and no replay. This is new1415 qualification, separate from the historical automatic-exit result.|

Each invocation exited0 with peak one server; the coordinator allowed at most two concurrent slots. All owned processes exited and all nine disposable schemas were removed. The15 Computer/corpse regression process records and six schema checks are in the regression manifest; the primary runs record their own cleanup. No failed run was retried or relabeled in this wave.

Raw evidence is retained under Temp/mxo-mission-native-followups-20261114, including freeze manifests, per-case records and regression manifest SHA256357B289F1A3BFFFE9F6C6822BDB5EBA5BECC56018E1CBBB47F70103372794F1F. Producer6e used Debug DLL11B0AFE021BE8FFFEDC624F3808B456B150A671A7C365F4E914F5A37C7F6454F; producer05b used Debug DLLA990477A71C6B63A23052C550972692B9782E25C387B0A21C2CB6F9E64114D46. These are actual runtime producer hashes, not a claim that a later docs-only build has the same binary hash. Final documentation edits change no hds/data/tools/tests files.