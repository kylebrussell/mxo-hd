# Native mission encounter QA — 2026-11-11

Native checks used build `6a335d1`, an isolated database, and a QA account containing only
CSR_Tester (`charId 110`). The main character was verified/reset to mission 986 only, Mara
`(23828,495,13984)`, before each client-lock release. TheNeo was not selected or modified.
The client lock was released at 09:38 ET, before Claude claimed it for PR 94.

## Observed behavior

| Check | Native result |
| --- | --- |
| Player-relative hostile and friendly arrivals | Security, Courier and the generated Machine Program appeared on the room's floor at the wall-edge test point. Security was reachable by close combat. |
| Hostile to friendly | Defector despawned/respawned with a Talk action; the native conversation completed the next goal. |
| Friendly to hostile | Turncoat despawned/respawned with Close Combat instead of Talk. |
| Hold-out | The twenty-second goal completed after 20.17 seconds and advanced the phase. Objective text appeared; no countdown appeared. |
| Deadline | After entry into the allocated area, the twenty-second deadline failed with server reason `You ran out of time.` The native client displayed its Mission Failed summary. No countdown appeared. |

The arrival test point was `(41010.332,1695,12280.438)`, nav record `045e/0024/0102`.
The former raw three-metre offset has no polygon at this floor. The new Security placement was
`(41139,1695,12414)`; Courier was `(40913.345,1695,12564.329)`.
Aggro was disabled and `?weaken 1` reduced nearby NPC health for this placement test.

Object-relative arrivals and the two-soldier wave are covered headlessly. Their native combat
completion was not checked: after the first melee kill, the server logged `DefenderDied`, death,
XP and loot, while the client retained the interlock pose and left Security standing. This prevented
further native attacks. The fault was handed to the combat/PvP owner; no combat files were changed.

The conversation-only native fixture is
`tools/HeadlessClient/Fixtures/encounter-presentation.xml`. It is test data, loaded only by an
explicit fixture server, and permits both side changes and timers to be inspected without combat.
The initial arrival fixture is `arrival-navmesh.xml` in the same directory.

## Placement follow-up

Native QA found two additional placement cases:

- Two invalid site spots could converge on the same projected fallback, overlapping the actors.
- A walkable NPC spot could lie outside the allocated area's rectangle. Teleporting directly to
  that NPC did not start an area-entry timer. The navmesh connects that room to the rest of the
  floor; it is not evidence of a disconnected room.

The follow-up reserves distinct projected positions, including a wave's planned members, and keeps
site placements inside the area. The allocated floor supplies the component reference before the
player arrives. Unit regressions use the native coordinates, and the headless wall-edge scenario
also verifies separation. These follow-up changes were verified in unit/headless tests; native
evidence above is from `6a335d1`.

Local evidence and full server logs are retained under:

- `C:/Users/russe/AppData/Local/Temp/mxo-native-arrivals-20261111/`
- `C:/Users/russe/AppData/Local/Temp/mxo-native-sides-timers-20261111/`

The shared handoff file records lock ownership, resets, results and commit hashes.
