# Active mission persistence (M1)

Active missions now checkpoint independently of completed missions. Previously disconnect cleanup called End, deleting spawned actors and owned items. Suspend now saves and detaches; End remains permanent cleanup. World entry restores the checkpoint without restarting the phase or selecting another site.

The versioned storage format and offline timer pause are OURS server policy, not a recovered retail database schema. The checkpoint includes acceptance identity/key, phase/goals/triggers, sides/arrivals/waves, elapsed timers, exact site/object placements, following/defeated actors, health/dead state, unlocked objects and item ownership. Entity/view IDs and open loot/Put windows are rebuilt. Definitions, mission text and reward curves are not changed here.

Mission actor identities associate death, kill XP and unclaimed generated corpse loot with one durable effect. Regular mission NPCs already have no random bag-item list; the existing source gives them only pocket cash and an optional mission item. Both are preserved without rerolling. Ordinary world loot keeps its baseline path. A killed actor never becomes a permanent ScriptedCorpse; the XML dead=1 marker is retained separately. The existing MobLifecycle excludes mission NPCs from generic corpse/respawn timers; mission cleanup owns their lifetime.

## Transactions and failure recovery

Startup ensures characters and char_missions use InnoDB and creates an InnoDB active-checkpoint/tombstone table. Inventory already has a transactional migration. Only existing table engines change; columns/indexes are preserved. DDL runs before listeners/business transactions. Validation executes only in harness-owned mxotest databases.

Every checkpoint compares its expected payload under a persistent row lock, rejecting stale sessions. Owned-item grants, moves and removals commit with ownership. A new grant rejects ANY occupied slot, including an unrelated identical GOID; existing ownership and explicit owned moves are distinct. Completed goals do not replay item grants.

Completion atomically commits ticket exchange, owned-item cleanup, XP/info, level progression, completed count and active tombstone/receipt. Signed INT totals use wide arithmetic capped at 2147483647. Feedback reports actual credited deltas at the cap. Retrying the same receipt cannot pay again. M3 owns calculation; M1 supplies the transaction seam.

New output from an owned mission operation waits for its checkpoint. Other queues remain immediate. Failure discards staged output and reconstructs/reannounces the latest committed checkpoint before a retry notice. A nested grant committed before a later failed goal checkpoint remains owned and is reannounced, rather than lost or duplicated. Kill XP/death/generated loot settle as one checkpoint; transient dead/unpaid actors are not saved. Corpse cash claim and its pending-state transition commit together.

Disconnect retains inventory and durable state while removing runtime actors. Abandon/failure/replacement clean exact recorded ownership. World/Loading Area entry rebuilds local views without accepting another mission. Only the current killer's owned pending mission loot permits an ordinary dead actor to be newly introduced, using the existing death presentation; no global world-corpse policy changes.

## Disposable QA and Claude-only checklist

NativeQa mode mission-relog is discovered from its own file. It seeds the preceding Agent Gray mission and owns abilities2/17. The fixed setup command ?missionpersist start accepts known mission2202 normally; ?missionpersist status reports the checkpoint. Both require exact dev1, strict mxotest database and a live in-world character, exclude TheNeo/characters0/2, Loading Area and pending world changes. No combat/health/general RNG override is added.

One-pass native checklist, on the exact PR head/source and snapshot hashes:
1. Start mission-relog. Run ?missionpersist start once, then status; verify Watchful Eyes phase1 and two Get goals in the mission window.
2. Follow its waypoint/elevator to Herschel. Right-click/radial Get once. Capture first goal done and one Tracer in inventory.
3. Log out through the working menu, wait for reservation release, and log back in. Verify same phase, first goal done, second open, same actor placement and exactly one Tracer.
4. Right-click Herschel Get again. Verify one Device and no duplicate Tracer. Leave the mission area normally and verify phase2 continues.
5. Hardline radial to Loading Area, then choose the hardline back. Verify phase/goals/items and actor/object counts remain.
6. Abort through the mission window. Verify mission key/UI and owned items clear; another relog stays abandoned.
7. The separate headless corpse fixture verifies pending loot restoration; Claude may additionally inspect a normal killed mission carrier's dead pose/loot menu after relog. Never label it scripted or reroll its cash.

Headless cases cover ordinary disconnect, process restart, phase/object/unlock state, owned-slot moves, identical foreign-item collision refusal, unpublished failed-goal recovery, signed-INT near-cap completion rollback, ticket receipts and corpse loot/kill XP idempotence. Fixed SQL failure triggers are created/dropped only in the owned disposable schema through the harness's existing root credential; no SUPER grant or binlog/security setting change occurs. Claude alone performs native QA and merges.
