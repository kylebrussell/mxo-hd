# Flood Prima chain 2101â€“2110

The ten org-3 Flood missions (orders 11â€“20) retain the Prima 2005 story text in `exile_*.xml`: 25 phases and 49 goals. Role names, item IDs, floor placements, wave counts and pacing are authored OURS. These are not the archived Merovingian critical chapters. This slice adds the briefing-supported kill-before-transport gate in 2102 and kill-before-plant gate in 2103. The remaining Merovingian chain is a separate queue.

The first mission represents the guide's dead Exile searches with four searchable furnishings, as already documented in its XML; this is an explicit representation limitation. Hostile proximity Talk does not claim a visible portrait. Mission persistence, rewards and hostile speech fallback use the existing shared runtime.

Computer Put now uses metadata state 0x962 and use 8, with the native rule4006 GenericKeyTest matching an owned mission-key item. Search-with-item uses Take5, other computer goals Hack4; the runner waits for the received current state to offer the action. The old no-item probe's empty 0x962 menu was not proof Put was absent. Native menu confirmation remains pending.

## Claude one-pass native check

Use discovered NativeQa mode `merv-chain` on the exact reviewed head. It grants only common introductory prerequisites 1201/1204/1203/1205â€“1208; none of 2101â€“2110 is precompleted. CSR110 is level50, org3, HP5000/IS2000 at Mara 17000/495/2300 with abilities2/17/1006/77. Flood is the remote mission contact. Dev commands are enabled, no GM, fixed combat rolls or health hold. Layout-only seed210110 is scoped to this fixture. Use existing `?weaken 1` before actual hostile combat if needed; combat still uses normal actions and rolls. Accept and finish each mission through normal UI and elevators/hardlines. Do not mark missions complete through diagnostics.

| Mission | Check each phase in order |
| --- | --- |
|2101 End of the Beginning|P1 open all four search furnishings, optional searches/wave and required data; P2 talk Clotilde, put data in safe, clear Zion wave.|
|2102 Scheduled for Deletion|P1 clear Machines before escorting Ugolino outside; P2 recruit Ginevra and lead both outside; P3 bring both to Mathilde, keep both alive.|
|2103 Fatal Frame-Up|P1 open box for dossier; P2 clear Zion, then put dossier inside desk.|
|2104 Useful Idiot|P1 talk Messier and escort outside; P2 bring him to Clotilde alive.|
|2105 Intercepted Signal|P1 talk Flamsteed and escort outside; P2 deliver him to Armand alive.|
|2106 Assassin Elimination|P1 clear guards; P2 talk Halloran; P3 defeat Alhena.|
|2107 Nothing To Gain|P1 search furnishing and upload; P2 computer Take virus and upload at hardline.|
|2108 Digital Disturbance|P1 Get bomb from Philippa; P2 computer Take virus, then verify actual Put menu and deposit bomb from inventory; P3 Give virus to Cornelius.|
|2109 Bagman|P1 open safe for virus; P2 put virus into next safe.|
|2110 Stolen Artifact|P1 search documents; P2 Give Pemberton documents then Take dossier; P3 approach Arcturus, fight and open his corpse to take statue, clear crew; P4 Give statue to Bertrand.|

Verify each objective updates visibly, all 25 phases stage safely, escort followers persist between buildings, computer menus change at the correct objective, and final mission close occurs once. The ordinary mission Abort UI has an independently reported native team issue; this PR does not claim to fix it.

## Validation

Fresh actual-protocol full chain PASS173.4s, exit0, parallel1. The driver accepts through mission RPCs, uses enabled computer metadata and actual native giver/taker trackers, deposits actual owned bag slots, selects the exact corpse cache, and earns all ten completion rows once. It never calls server Complete helpers. Permanent run evidence: `C:/Users/russe/AppData/Local/Temp/mxo-merv-prima-run1-20261112.txt`. This is server/wire evidence; native UI remains Claude-only pending.

Release unit suite: 4103 passed. Python policy/generator suite: 12 passed. Debug and Release NativeQa builds, read-only mode/default/unknown leak checks and provenance passed. All-module current audit was generated under ignored artifacts with an exact three-artifact CRLF roundtrip; tracked historical snapshots are unchanged.
