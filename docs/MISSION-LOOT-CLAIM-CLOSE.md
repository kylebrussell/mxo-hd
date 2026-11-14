# Mission loot claim acknowledgement and closure

Status: local implementation under review; fresh/backend integration and Claude-only native acceptance remain pending. This package is based on master38a15939. It changes no mission source/site, placement resolver, leave predicate, reward law or XML.

A successful Safe Take All previously granted dossier6799 and completed Search but replied with an empty `81 20`. Client7.6005 treats its array as claimed identities to remove, so the dossier icon remained. The empty reply also does not close the window.

## Evidence

Read-only client.dll SHA256 `CB5FFCC6BB046FC01E9B10CD8E0C5AA86A82CE8A6A50549C0AF6B5C55F79CF3A`:

- Dispatcher table6217e9c0/internal0x120 ->62179adf -> reader62141580 -> handler621e2bb0. The handler loops ItemArray and calls621cd6d0. That function compares both identity u32s, removes the matching UI row via62084f50 and clears its cache. Zero entries remove nothing; the UI row removal does not close the window.
- Internal0x11a ->62179a05 -> empty reader62141200 ->621ca200. This handler closes container window0x29 and clears cached MissionKey+0x130. Server `81 1a` is an existing supported response, not a guessed opcode.
- Retail `research/packet-dumps/mxopackets1/crashover2k/afterwhoruneo/old_world_packets_second_pc/Neuer Ordner/loot.txt:420` includes server811a after the client close at416.
- Retail `research/packet-dumps/community-intake/20260607T200024Z-hdneo-public-remaining-small-ability-loot-market/logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_second_pc_others_loot_fm11sk_codebyte1_from_corrupted.log.txt:104` lists both identities in8120; line113 grants both via5e. This supports claimed-removal semantics. The former test's “full bag/unclaimed” interpretation was incorrect; its captured bytes remain unchanged.

## Behavior and transaction boundary

Mission Search reserves each successful item against persisted bag occupancy plus pending mission slots. A container operation defers its nested checkpoints until the outer existing mission mutation commits all ownership and goal state together. A late SQL failure discards staged inventory/UI/goal messages and restores the durable checkpoint. Slot0 is never granted. Unrelated mission actions keep their existing checkpoint behavior.

Before goal callbacks, the selected container state is updated,8120 lists byte-exact claims from its original8119 window, and811a is queued only when fully emptied. Claim/close RPCs precede the goal RPC after committed SQL. The existing transport serializes object sections before RPCs: queue insertion does not guarantee close-before-proxy-despawn wire order. No transport priority barrier is added. Sourced811a closes window29 and clears its cached key independently of proxy view lifetime. Full/partial bags list only successfully transferred identities, keep the window selected and retryable, and leave untransferred objectives pending. Repeated Take All cannot replay a completed claim. Item identities retain instance data; they are not replaced by bag slots.

Caller audit: NPC loot previously sent remaining drops, which would remove the wrong UI rows. It now reports only transferred ordinary drops and mission-item claims, while retaining failed drops server-side. NPC window closure and reward/inventory policy are unchanged. `PlayerHandler`'s no-selection empty reply remains valid. Tutorial wardrobe still uses its old empty acknowledgement; its existing unchecked, nontransactional multi-item grant needs a separate inventory-safe review before reporting successful claims. This package does not change that tutorial path. Tutorial corpse loot uses the corrected NPC path.

## Validation and headless filter

Registered normal-wire filter: `mission-loot-claim-close`. It accepts2201 through Agent Gray, uses existing strict disposable site seed220110, ordinary elevator and current saved Root/Route/Standing to Safe552/source0d10003e. Only prior receipts and96 ordinary filler items are seeded. Dossier, selected mission/progress/completion/rewards are never injected. Test movement follows the published route and is not native walking proof.

The case checks full bag/repeated failure, close/reopen exact identity, frees one bag slot through normal delete, deliberately fails the late Done checkpoint in the harness-owned schema, verifies no item/goal/claim/close publication, reopens/retries, verifies exact claimed pair and close-before-goal ordering, and repeats without duplicate dossier. Raw checkpoints/RPC/03/PID/DLL SHA are written before cleanup on pass/failure. No fresh run is claimed until a frozen grant and result are recorded.

Companion regression filters: `full-bag-tutorial-loot`, `container-and-corpse-loot`, `retained-corpse-loot`, `mission-relog-corpse-loot`. NPC full-bag assertions now expect zero claimed pairs and partial claims explicitly identify the transferred revolver; reopened8119 independently verifies retained letter contents.

Unit checks cover exact bytes, full/partial/repeated claims, pending slot reservation, state/callback ordering, mission output commit/discard and NPC partial retry. Full Release suite5172 passed in105seconds before the behavior-identical packet-helper file split. Final43 focused checks pass after the split and final driver assertions. Both actual Debug/Release tool builds, read-only mode plans and build provenance pass. Validation-time and final frozen DLL/configuration hashes are retained with their exact build logs and publication notes; a later prose-only commit does not change the tested production trees. No native UI acceptance is inferred from packet tests.

## Claude-only one-pass checklist

Discoverable mode: `mission-loot-claim-close`. Read-only `--fixture-plan mission-loot-claim-close` verifies abilities2/17/1006/77, original offer origin17000/495/2300, Machines50, prior receipts,96 ordinary filler slots and site seed220110. No GM, fixed callbacks, object creation or forced outcome. CSR_Tester110 only. Never TheNeo.

1. Acquire the usual client lock through Claude's workflow. Agent Gray remote Call > Missions > Critical > The End of the Beginning > accept. Use the normal street elevator; inspect current checkpoint Root/Route/Standing. Walk the route to the Safe, preserving source furniture. Hover and radial Open must work; no forced use or source-centre teleport.
2. Click Take All with the full bag. Dossier icon and window remain, no dossier enters the bag and Search stays pending. Click Take All again, close, then ordinary hover/radial Open again. The same dossier remains.
3. Delete one ordinary filler from the bag UI. Take All again. Exactly one dossier6799 enters the freed slot; the dossier icon disappears and the loot window closes; inspect goal advancement separately. Retain screenshot and raw RPC evidence. Current near-door phase advance is a separate known issue; do not claim it fixed.
4. Repeat/reopen where the current mission allows; no duplicate dossier or replayed goal. Proceed normally if checking later mission goals. Native physical walking, hover/picking and actual UI closure are separate mandatory observations.
5. Standard NativeQa shutdown/reset must verify mainCSR110 at Mara23828/495/13984, full health, pvpflag0 and mission986 only, and release the lock. This implementation agent does not run native QA or touch mainDB.
