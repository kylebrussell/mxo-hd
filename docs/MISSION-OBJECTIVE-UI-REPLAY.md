## Current master refresh and known native failure

Refreshed onto cc8f5555 as requested for another native check. Current grant registration and this branch's objective LoadingComplete callbacks are both retained. The original replay implementation, 500ms compatibility delay and gameplay progression/rewards remain unchanged; no new presentation fix is claimed.

IMPORTANT: the previously reported native unticked-row failure is still unresolved by source. Refreshing the open-floor dependencies does not itself prove this widget is fixed. Claude's retest should identify the named view (floating tracker0x43 versus Mission Status Objectives0x16), preserve screenshots and loading/row/tick logs, and stop/retain the failure if it persists. Earlier HOLD/native-failure evidence below is intentionally retained; this new candidate is READY only for the specifically requested native retest after CI, not for an assumed successful merge.

# Restored objectives after client world entry

**Native display FAIL at a313036 (Claude, 2026-11-12 20:17 ET). This branch is not ready for merge or another native retry.** The client received exactly the intended post-LoadingComplete rows and later tick, but still showed one unticked row. Server progress and remaining gameplay goals are correct. The current timing change has not fixed the native presentation.

PR163 preserves correct goal state but failed native display acceptance: its two open rows and completed tick arrived in one UDP packet before the new Objectives panel showed them. Claude observed only the unfinished row after relog. An initial follow-up retained that first replay and added a later copy; Claude's native test still failed. The revised implementation omits ALL pre-loading restored80a0 records and creates the rows only after the exact client `0c 01` LoadingComplete marker.

The pinned client.dll SHA256 is `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Its80a0 manager62210ed0 stores separate goals by ID; flag1 is completion. Widget lookup620cfe00 searches by goal ID, but620cfe64..73 returns NULL when a completed goal has no widget. Panel refresh620d04f0 also exits while initialization byte+0x5c is zero;620d08d5 enables it. Completed goals can update existing widgets but cannot create them on first panel construction. The read-only probe `research/missions/inspect_objective_widget_gate.py` reproduces these instructions. The world-entry marker is documented with capture evidence in CLIENT-ACTIONS.md and research/missions/tutorial-wire-spec.md.

After LoadingComplete, the server introduces current-phase open and completed goals as open rows and flushes them. Completed ticks follow after0.5seconds through the existing shared scheduler. **This delay is an explicit local compatibility policy**, not a decoded retail timing, frame acknowledgment or proof the panel initialized. Each restore permits one refresh; invalid/truncated/extended markers and duplicate markers cannot trigger it. A changed mission, phase, district, world generation, life generation, death, world transition or later restore cancels stale completion. No checkpoint, item, reward, trigger or gameplay progress changes. Ordinary mission acceptance without restoration does not arm it.

## Automated checks

Units inspect the actual queued80a0 goal IDs/states and exercise duplicate/malformed requests, stale Abort/phase/world/death and superseding restore. The `mission-objective-ui-relog` headless scenario uses normal acceptance, first Get, logout,33-second cleanup and same-server login. It sends the real LoadingComplete marker and verifies the additional completion arrives in a different actual encrypted UDP datagram from both row creations. Datagram sequence is recorded at the receive boundary, not inferred from RPC list counters. The original team/order/item ownership, second Get and Abort checks still apply. Fresh wire success cannot prove native widget rendering.

## Claude-only one pass

Use the exact green head and discovered `mission-objective-ui-replay` mode, CSR_Tester110 only. It has prerequisite2201, abilities2/17 and existing mission dev preparation; no GM or random overrides.

1. `?missionpersist start`, then Herschel right-click > Get once. Open Objectives and record the two identical tracking-device rows, first checked and second unchecked. Do not Get the second device.
2. Quit, allow normal session cleanup, relog to the same fixture. Wait one second after the world appears, then open Objectives. Compare both rows/checkmarks. Retain raw0c01 and80a0 packets: the post-loading tick should be later than its row-creation datagram. If rows are still wrong, report display FAIL even if the wire order passes.
3. Repeat the relog once with Objectives visible before quitting, and once with it closed, to check initialization rather than assuming a visible panel. Confirm the first device remains single and goal2 remains unfinished.
4. Herschel > Get once more must grant only the second device and complete goal2. Mission Status > Abort > Yes must send80a6 and clean both devices/checkpoint; another relog must stay clear.
5. Reset main CSR_Tester110 to Mara(23828,495,13984), full health, flag0 and mission986 only before releasing the lock.

Native display acceptance is PENDING Claude. No native client, lock or main DB operation was performed by this implementation.

## Recorded validation

Full4,274 Release units,24 final focused mission/NativeQa checks and12 Python policy/link tests PASS. Both configurations and actual read-only mode/provenance checks PASS. Fresh mission-objective-ui-relog PASS65.2s/exit0/parallel1/disposed at source7a3233278bb281bdf4b0081c696fd7b95968c919; actual received datagram-sequence inequality is asserted, along with malformed/duplicate LoadingComplete, same-ID row order, saved ownership, second Get and Abort. Own success raw saved before teardown: mxo-mission-objectives-success-771358d4a9c841a681d2b992a285eaaf.log, SHA256e87fbfd61e1c76d944429f730819af355bac849ff7d0834efe6af3dfd815d53d. Later commit only records validation. Native display still PENDING.

## Native follow-up after f38f3c0

Claude observed DISPLAY FAIL: one unfinished row despite the later packet. The first HUD replay still contained the original open/completed records, then a second copy arrived after0c01. The revised flow removes that early80a0 copy entirely. The headless client now sends the actual0c01 lifecycle marker after self-spawn by default; the dedicated regression withholds it to assert zero objective records until a valid marker. Earlier65.2s proof applies only to the failed duplicate-copy implementation; revised validation/native acceptance remain pending below.

## Distinguish the two native views

The widget-existence gate above belongs to the floating Objectives tracker(window0x43). Mission Status(window0x16) separately rebuilds its Objectives tab at620d6080; it sorts completed records into a completed group and creates rows through620d5bc0, copying each goal ID and completion checkbox. The same-label manager records remain separate IDs. Check BOTH views and name which one differs; tracker behavior does not establish Mission Status behavior. The probe now includes both paths.

Revised validation: full4,278 Release units and20 focused checks PASS. Both NativeQa configurations and actual readonly plans/provenance PASS. Revised source0af16547a2a4ceb818c52f16164214b8920dedf4 passed BOTH ordinary-auto-marker mission-objectives-relog(54.0s) and withheld-marker mission-objective-ui-relog(54.5s), total108.5s/exit0/parallel1/peak1/disposed. The withheld case proves ZEROearly80a0 before0c01, one row-creation batch, one later-datagram tick and no duplicate replay. Both check ownership/Get2/Abort; successful raw logs23589f1bda4c47389f7878acedca670d and62c25e852cd944f0bbe4f8948ddecc42 were saved before disposal. Native display recheck remains pending.

## Latest failed native sequence comparison

The failed session PacketLog has SHA256 `be815b39bd396a3a140e62af510cdc15b7399b00b627f2ac355f1e926e7e4a61`. A bounded independent parser decoded312 RPC records and75 raw03 packets without parse failures. The acceptance packet at line617 and restore packet at line2067 carry byte-identical809c mission name (instance47/template2202),80a3 briefing/phase header and809e waypoint records. Both paths send the same own-view MissionKey47 update before809c. No omitted phase/count field was found in those messages.

One observed difference is80a1 operator dialogue after the initial objective rows and before the first Get completion; restoration does not replay that conversation. This is not an established cause, and no dialogue or timing workaround is added. The remaining question is the native widget lifecycle and which named view fails: floating Objectives tracker0x43 or Mission Status Objectives0x16. The two presenters have separate initialization and completion behavior. Further implementation waits for a source-backed explanation; prior automated wire proofs do not establish native UI acceptance.


## Master refresh after loaded-grant registration

Merged master 0d333c5c and resolved the shared LoadingComplete dispatcher by retaining both avatar-ready grant registration and the existing objective replay call. The objective helper, headless case and mode are unchanged. 48 focused objective replay, persistence and grant checks passed; both actual NativeQa builds and read-only mode/provenance checks passed. No fresh server was repeated for this maintenance merge. The previous native display failure remains unresolved; this is not READY FOR NATIVE or merge.
