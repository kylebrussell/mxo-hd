# Existing neighborhood missions: canonical definitions

The 51 placed neighborhood contacts already offer three generated missions each: Assassination, Retrieval and Rescue. Their stable server IDs are `3000 + contactId * 10 + kind`, with kinds1–3. Their authored text, targets and generation policy are existing OURS content; this change adds no story, template, reward curve or placement rule.

The stock acceptance path kept these definitions in `NeighbourhoodMissions`' private cache and the player's offer list. It never added them to `DataLoader.missions`. Both cold-world restoration and transactional completion searched only that file-backed list. Consequently a stock generated mission could be accepted but its saved definition was unavailable on relog or completion. The previous neighborhood wire case accepted and aborted a mission, so it did not exercise either failure.

`MissionDefinitions.Find` now supplies the same canonical authority to those two lookups. A unique authored file remains valid. A generated identity must decode to kind1–3, exactly one known neighborhood contact and exactly one actual source spot, and re-encode to the same ID. The existing cached/built definition must retain its ID, contact, district, generated flag and non-plot/org0 identity. Unknown, invalid, duplicate and authored/generated collisions refuse; there is no null-spot default, fuzzy name match or checkpoint-provided XML fallback.

The builder has no player-level, random or clock input: only static contact/spot data, fixed arrays and existing XML literals. Tests compare all 153 canonical definitions deeply against independent rebuilds. Actual allocated sites, object state, item slots, NPC identities/health/loot, mission key and acceptance GUID remain in the existing version1 checkpoint. Restoration does not stage or reroll them. M1's checkpoint CAS, transaction, owned-item cleanup, completion receipt and M3's quoted XP/info pair remain unchanged.

## Validation and diagnostic profile

`neighbourhood-retrieval` starts CSR110 two meters in front of The Chessman:76015/485/15845, source sector1147, NAV floor390 +95 and valid yaw byte128. The contact's sourced position/model is unchanged. The profile sets actual level10 and XP1282500 after generic fixture setup, loads2/17, uses no GM or forced combat/callback rolls, and enables only the existing layout seed3072 in the disposable dev process.

The headless case uses the actual contact view and ordinary Use2/list/info/accept, computer's metadata-enabled Take action, goal records and exit completion. Existing diagnostic travel helpers place the test player at actual sites; this is not proof of the client's walking route. It restarts only its own Harness server after saving and disposing the owned session, proves that PID exited before starting its replacement on new ephemeral ports, and restores before any contact list could populate the new process's cache. All replacement processes are disposed in the case's finally block; no native process is involved.

The same mission is completed twice with distinct acceptance identities. A carried item and actual object/NPC state are compared across the cold restart. The existing receipt repository is invoked only against the owned throwaway database to verify duplicate-finish idempotence and refusal of an old receipt during a new run. An unrelated identical item must survive cleanup. At unchanged level10, the existing reference50 policy pays4040 XP and6300 info per run; these are existing OURS tuning, not a recovered retail reward formula. Final relog must leave no active mission and no additional payout. Raw RPC/view lists, each selected DLL/config/hash, server logs and process lifetimes are retained before teardown.

The recorded fresh result is below; native acceptance is pending. Definition resolution for all153 IDs does not claim that every generated layout or contact appearance has received native testing.

## Claude-only one-pass

1. Right-click The Chessman, choose Get Mission, select Retrieval and accept. The three existing non-plot offers remain available.
2. Follow the waypoint/elevator and use the computer's normal Take action. Relog while the mission is active; its site, goal state and held data should persist. No mission-start or completion shortcut is needed.
3. Leave the area normally. Check the completion summary and XP/info. Repeat Retrieval once, then relog again: each run should pay once, with no resumed completed mission.

No native/client/setup/main database action is performed by this change's implementation agent. Native UI and rendering acceptance remain Claude-only.

## Recorded headless result

`neighbourhood-retrieval-persistence` passed56.1s, driver exit0, parallel1 on frozen sourcef75591ca36f34d8200d612f609dbaf3763866f86. Three owned server processes ran sequentially; both replaced PIDs were observed exited before their replacements started, and all processes/sessions disposed normally. The actual selected Debug DLL was35692E4E867C380B6555312A9D4AEE7C070CE6D91904E0B3FCA33AA1A82B5011.

The same existing Retrieval3072 completed twice through ordinary contact/object/exit records, with two distinct receipts and exactly4040 XP/6300 info each. The cold restore retained the acceptance, item slot, actual object state/placement and all NPC identities/health/loot before any contact list populated the new private cache. Duplicate completion returned the existing receipt, an old receipt during the second run was refused, unrelated identical inventory survived cleanup, and final cold relog produced no active mission or extra payout. No protocol/server failure was observed.

Preteardown proof: `Temp/mxo-neighbourhood-persistence-mxotest_run_1_558aa5cabc.json`,112622bytes, SHA256d4457f0582e955492ff04b23fdbace650505a1a824715f5c9e05a9f492c7ffd9. It retains38view lists/allRPCs, all three DLL/config/process logs, two receipts and process lifetime checks. Durable runner stdout/exit are in `Temp/mxo-neighbourhood-fresh`.

Full4438 tests passed before the evidence-finally cleanup hardening;44 scoped tests pass after the pure upstream186 merge. Both actual NativeQa configurations/read-only plans/provenance are checked separately. Nine fresh-tested runtime/mode/driver/helper blobs remain byte-identical after that import, so the fresh case was not redundantly repeated. Native acceptance remains pending.
