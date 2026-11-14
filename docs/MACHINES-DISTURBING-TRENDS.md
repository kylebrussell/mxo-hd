# Disturbing Trends: current admission repair

PR204 builds on master cc8f5555 (merged open-floor trio196/197/203). It now includes a shared search-policy repair for the native failure reported at6fa29181. Mission XML, source/site selection, dialogue, Map Disk6783, prerequisite receipts and reward law remain unchanged. PR202's rewardLevel metadata is separate.

## Reproduced failure and repair

A clean detached master build reproduces the native report headlessly: first Ophiuchi Talk succeeds, then ordinary exit refuses the next area with SearchExhausted:RouteGraphs. Exact master cc8f5555121f73357b58bef05c49061b1c3de6e0, Debug DLL SHA256 B14651EE444CCC42910FA22200BC0E7C6921A888EC1BE6A76F7A0544C70EAF16,32.5 seconds. Owned PID36636 exited; its disposable schema was disposed. The negative proof is retained under Temp/mxo-pr204-route-repro-20261114/master-before-*.

The normal existing seed201101 selects source1407/key0d1001ed, sector209/static493, raw29000/1026/13460, on the Office site whose door is28800/945/14700 and bounds28900..31100/12350..15100. An exact-source offline test reproduces33 body-node visits,32 complete graph attempts and one static-root graph before refusal. No easier seed or source was selected.

Reflection of the same request finds386 connected NAV surfaces,489 static-subtracted standing cells and only nine cells reachable from the actual Ride root. Conservative selected-elevator419 bounds fully cover NAV209/42/0, sealing the lobby's sole exit in the static graph. Both duplicate bounds independently cover it; deduplication is insufficient. Reports: Temp/mxo-pr204-route-repro-20261114/static-graph-diagnostic.{json,txt} and office-before.json. The first160 nearest-source candidate bodies all lack a reachable static use point.

SearchPolicyVersion3 retains the original direct probe and root-centre wave, then prioritizes candidates with a reachable use point on the already-built static graph, including mixed multi-object sets. The original all-candidate family remains the final fallback. Repeated identical complete body sets are not charged twice across waves/orders. Every accepted set still rebuilds its complete-body graph and passes the existing geometry, static bounds, NAV, exact/native-cast route and native usable-range checks. Work budgets remain4096/32. Failed-search memo keys change with the search policy; existing admitted placement fingerprints and fixed replay do not.

The exact-source regression now uses two body nodes, two complete graphs (one direct probe, one complete set) and one static-root graph. Centre29101.4375/825.003776550293/14592.57421875; Standing28740/895/14592.57421875. Read the actual checkpoint for Root/Route/Standing. These coordinates are a deterministic current candidate, not retail placement evidence.

**Native limitation:** the conservative closed-door graph remains sealed. This repair finds an in-range standing point inside the lobby for a body outside that standing component; it does not claim an open office route or native line of sight. No statics are hidden, moved, deleted or excluded. Claude must verify visible open-floor placement, physical access and native hover/radial Take. Stop and retain a native failure if the wall/door prevents picking.

## Protocol coverage and one-pass native checklist

Mode/scenario: machines-disturbing-trends. CSR_Tester110 only for native QA. The fixture grants exactly20 prior receipts, level50/appropriate XP, Machines organisation and ordinary support abilities; no selected mission, item, progress, completion or reward is injected. Only the existing strict dev/disposable layout seed201101 controls site RNG. No callback/combat-roll override or GM character is used.

1. With the client lock, Agent Gray > Call > Critical > Disturbing Trends. Accept, use the ordinary street-lobby elevator, Talk to the first Ophiuchi and leave at the elevator. The next waypoint must arrive with no safe-placement refusal.
2. Enter the operations post with its normal elevator. Compare the actual checkpoint source1407/0d1001ed and published Root/Route/Standing. Walk to the standing point and verify the free Computer renders on floor, source scenery remains unchanged, hover offers Computer/Take and radial Take works. Do not use the historical anchor28740/881/13200 as the free proxy position or force a Take request.
3. Take one Map Disk6783 and leave the operations post normally. Logout/relog while carrying before Give; verify the same active mission, pending delivery phase, original sites, earned history and exact disk slot. Enter phase3 normally and Give to the current Ophiuchi definition6. Do not reuse the historical first Ophiuchi view.
4. Verify exactly one completion receipt and default level50 gains54,871 XP/45,339 information. The archived HUD17 and pending PR202 are separate reward caveats. Existing level scaling is authored reconstruction, not a proven retail curve.
5. Release/reset CSR110 to Mara23828/495/13984, full health, pvpflag0 and mission986 only. Never TheNeo. Claude owns native QA and merge.

The current headless driver follows saved route points with test-only movement assistance after the ordinary elevator ride and checks actual creation centre/quaternion against the persisted placement. It stabilizes at phase3 through the normal exit before carry logout: the known near-door predicate can advance phase2 after Take. This case does not claim active phase2 proxy replay or preservation of its completed Take in phase3's current Give-goal set. The retained c07a2ec4 failure demonstrates normal phase advance with the disk preserved, not lost progress. It retains raw checkpoints on failure, exact source/item identity, carry/relog, current NPC6 Give, one payout, RPC/view lists and actual DLL/PID provenance. Test movement is not native walking proof.

## Current validation

The first candidate passed15 focused checks; the integrated search/mixed-set changes passed158 open-floor and Disturbing Trends checks. Full-suite, frozen current fresh-run, Debug/Release plan/provenance and CI results are published by exact head and DLL hash in PR204 and the shared handoff before a new READY. Earlier protocol successes below predate the shared resolver and do not validate this repair.

## Historical evidence (superseded heads only)

Everything below retains its original source attribution. Old DRAFT/HOLD text and coordinates describe those historical heads, not the current checklist or readiness.
# Disturbing Trends — dependent draft

**HOLD: depends on PR 196 and its unresolved native Computer picking. Not ready for native completion or merge.** This package makes no placement or gameplay-policy fork and does not relax the existing native blocker.

Scope is only the existing stored Machines mission **201201, Disturbing Trends**: three phases and three goals. XML/story/rewards are unchanged. It does not change Poster Hunt or any Box mapping. The branch imports exact reviewed parent `981dda270a6201ac8e9ee41f635f7c81fa244757` plus actual master; parent hds/source-anchor/generator blobs are unchanged by this package.

## Source and authored boundaries

`research/missions/transcripts/Mach1-2-1.md` is an archived replay: Ophiuchi sends the player to retrieve a Map Disk, which is then handed to Ophiuchi in another building. Title, dialogue, names and recorded completion amounts come from that source. Normal ordered Agent Gray offering, generated sites, appearances, goal wording and item stand-in Map Disk 6783 remain authored reconstruction policies.

The archived post-completion HUD shows level 17 and recorded gains of 54,871 XP / 45,339 information. This branch deliberately does **not** import pending PR 202's reference metadata: current `rewardLevel` remains absent/default 50. At the fixture's level 50, current policy pays 54,871 / 45,339; at level 17 it quotes 17,917 / 15,415. The existing curve/default baseline is explicitly OURS, not a retail level formula. If PR 202 later lands, the expected payout/profile documentation must be updated explicitly; do not silently retain these old expectations.

## Preserved placement gate

The initial gate on master `064b0460` failed honestly: fixed existing seed 201101 selected source **1407 / 0x0d1001ed** (sector 209/static 493), concrete GO3, but that master did not resolve its legacy `(29000,1026,13460)` pose. Negative proof remains `Temp/mxo-machines-disturbing-trends-selection-negative.json` (SHA256 `91a5ce0225d514f268f850ac88e2ba62bcb0ee87c80b0e8a9011fc05e2ab8242`), with separate failed log and original test copy. No seed search or avoidance occurred.

The same normal offline Choose/PlaceObjects path now passes with reviewed PR 196: effective pose **(28740,881,13200)**, quaternion **00000000f204353f00000000f40435bf**. The raw source remains unchanged. No source/prototype is forced, and no unreviewed anchor is substituted. This proves the imported source consumer, not native model selection, action availability, walking or LOS. PR 196's real native Take failure remains an explicit blocker.

## Standalone normal protocol case

Mode/case: `machines-disturbing-trends`. Exactly **20 earlier receipts** are seeded: seven intro missions, Prima 2201–2210 and 201101–201103. Selected 201201 and later 201202 are not granted. No item, progress, completion or reward is injected.

The standalone case reuses the merged contact selector and existing normal elevator/object/latest-placement NPC helpers, with no shared callbacks or runner changes:

1. Normal Agent Gray Call/list/info/accept, then Talk to phase-1 Ophiuchi (definition 1) and exit normally.
2. Enter the operations post, assert the naturally chosen source key and exact parent pose/quaternion, then send the actual source-backed enabled Computer Take action for Map Disk 6783.
3. Verify one tracked disk and its owned slot. Use ordinary logout, wait the existing 33-second reservation cleanup, relog, and verify the same mission identity, disk slot, completed Take and saved source/prototype/state. No duplicate disk is allowed.
4. Exit normally and enter phase 3. Resolve **definition 6**, the new friendly Ophiuchi, from its latest actual placement/side and active Give tracker. Do not reuse a historical same-name view. Give consumes the tracked disk, then ordinary exit completes once.
5. Verify the current 80a2 payout, exact persistent deltas, one receipt and no parser problems in either session. Unique raw RPC/03, per-phase checkpoints, both NPC instances, actual source and DLL/configuration/hash are mandatory evidence before teardown, including failure.

StillNpcs disables unsolicited aggro and test movement can cross walls. No forced combat outcome is used. Headless protocol completion cannot clear the native picking hold.

## Claude-only one-pass — held

Do not attempt a force-complete workaround. Once root confirms PR 196's native blocker is resolved, use the discovered mode: CSR 110 at Mara, actual level/XP 50, Machines, ordinary support abilities, no GM or forced outcome. The only layout control is the existing strict dev/disposable seed 201101.

Call Agent Gray > Critical > Disturbing Trends. Talk to the first Ophiuchi, leave normally, enter the operations post, and inspect the actual Computer at `(28740,881,13200)`. If hover/radial Take is missing, stop and retain the blocker. If available, Take the disk, relog while carrying, return through ordinary exits/elevators and Give to the new phase-3 Ophiuchi. Verify one normal completion and the current policy reward pair. Native walking, model/picking and archived-versus-normal parity require separate observation; this draft claims none.

## Local validation before fresh review

Release suite: **4,605 passed** (71 seconds); focused mission and inherited Computer tests: **30 passed**. Both actual NativeQa configurations built and emitted the discovered mode plan and build provenance. Python artifact policy: **9 passed**. No fresh driver has run for this package.

Actual Release hds SHA256: `0efb0056c562055bf53ec30beea38a2d51dbeccdaecaeea71b93b1ca4f874942e`; Debug: `d066271101dd02aac5a80ba5097cfe97ea5a57c982ebe6a4e62ff4c2fe8fc73c`. Logs/plans are in `Temp/mxo-disturbing-trends-*`. Production hds, Computer CSV/generator and mission XML remain identical to the imported reviewed parent/current master. This is a dependent test/mode/documentation package, not a native picking repair.

## Fresh proof — dependent protocol result

On frozen `c56781fd36e20656b0bafa4232b19bb36da28a3f`, `machines-disturbing-trends --parallel 1` passed in **61.8 seconds**, explicit exit 0. The owned server and disposable database were disposed; slot 1 was released. Normal Take, carry logout/relog, exact saved item/source identity, current phase-3 NPC6 Give, ordinary exit and one 54,871 XP / 45,339 information payout all passed. Both sessions were parser-clean (86/85 RPCs and 21/13 actual 03 view lists). This does **not** establish native Computer picking.

Required unique evidence: `Temp/mxo-disturbing-trends-mxotest_run_1_3377288f57.json`, SHA256 `42dd9e1f3489acf85e44fdd47a04d19a7b5568b15a12520dc41382ac3a084e0e`; raw server log with the same stem, SHA256 `48c47f90cd2964f33b7277d4d1e4d0f228f70fb67aa1be9fee2b27da26eb35ac`. The proof records the actual Debug DLL path/hash above, both sessions, phase checkpoints and NPC instances, `Step=complete`, `Failure=null`, and `NativePickingProven=false`. Permanent driver stdout is `Temp/mxo-disturbing-trends-fresh-stdout.log`.

## Reviewed-parent refresh

Imported exact reviewed196 head4dbb2067672e806aa31a04d9e3aba496385342b5 and actual master0d333c5c70e12a2c840bee9bc7847f851940ed99. Own DisturbingTrends fixture, driver, mode, tests and selected mission XML/dialogue are byte-identical to0fb638da49ef3876a6619d6f77767bcc89491994. The mode's historical RequiredComputerParent metadata remains unchanged; the actual imported dependency is4dbb as recorded here. No213 carried-NPC helper,202 reward metadata or new placement policy is imported. Current default expectation remains54,871 XP/45,339 information.

Forty-seven focused mission/source/creation-receipt/reset checks passed. Both actual Debug/Release builds, machines-disturbing-trends plans and build provenance passed; retained at Temp/mxo-disturbing-refresh-plans-provenance.json. Debug hds SHA2567F0812157B09AACEEF5F9BE51FF7864154AE58B340B7263984BA82500EAF1B75; Release54A05734E753EF42C2059EAF6FF96C33A150B4537D2EE4452FD046C9DDEFB09C. The earlier61.8-second result belongs only to its recorded source; no new fresh result is claimed before root review/grant.

PR204 remains dependent DRAFT/NOT READY until196 native PASS and actual merge. Source1407 physical picking, ordinary native route and all archived-versus-current reward qualifications remain explicit. Layout seed201101 fixes a per-mission/phase PRNG stream only, not player-position/preview/history-dependent candidate pools; no identical source-placement claim is made.

Approved metadata correction: RequiredComputerParent now records exact4dbb2067672e806aa31a04d9e3aba496385342b5 instead of the stale historical parent. This is the sole exception to the mode byte-identity comparison above; all behavioral fields, driver/fixture/XML/dialogue and54,871/45,339 reward expectations remain unchanged. Both affected tool configurations and actual read-only plans/provenance pass again; current proof is Temp/mxo-disturbing-metadata-plans-provenance.json. Registered fresh filter is machines-disturbing-trends. No fresh launch before root final-pin review and explicit grant.

## Captured reviewed-parent result

Frozen06fefac8c82b442c9b0ed9f939999187d56c4f2d passed machines-disturbing-trends in75.3 seconds, explicit exit0, peak one server. Owned harness/server/database disposal completed and slot A was released. Actual Debug hds.dll SHA256E726A7A97F531A6FFEB8EC980C746661ED434B642C74695D7DD95FCF3680869F matches the approved build. Required proof `Temp/mxo-disturbing-trends-mxotest_run_1_3c898919f9.json` SHA2565346D98C39522E32A02B01E7B5F8715088DDD49B5F7FD717B2776A70638660BC records Step=complete/Failure=null, five checkpoints and two concrete Ophiuchi identities. Both sessions are parser-clean:86/81RPC records and22/16 actual view lists. Paired raw server log and permanent stdout/exit `mxo-disturbing-06fefac8-fresh.out/.exit` are retained.

Actual owned phase2 source is sector209/static493/native key0d1001ed, GO3. Its saved Spot29000/1026/13460 is distinct from the exact typed-parent wire pose28740/881/13200 and literal quaternion00000000F204353F00000000F40435BF, both asserted from actual creation. Normal Take produces exactly one owned Map Disk6783 in slot1 and changes the saved object state2401 to2400. Normal logout waits33 seconds before relog; mission identity, item slot, earned goal and serialized source/object state remain identical, with no duplicate disk. Ordinary phase3 NPC6 Give consumes the item. One completion receipt and actual80a2/DB checks verify54,871 XP and45,339 information exactly once (final cash46,339 includes initial1,000).

The raw driver's unchanged Dependency string still names historical981dda27; it is not rewritten after capture. Actual frozen ancestry and updated mode metadata positively identify reviewed4dbb2067672e806aa31a04d9e3aba496385342b5. That display-string limitation does not alter the source hash or earned assertions. No native picking, physical route or exact retail reward claim is made. DRAFT/NOT READY remains explicit pending196 native PASS and actual merge; this headless result does not clear native HOLD.
