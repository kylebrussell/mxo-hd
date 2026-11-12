# Mission presentation: bodies and dialogue consumers

M2 is limited to presentation. Mission ownership/persistence and reward scaling belong to separate changes. No main database or native client is used by Codex.

## Scripted bodies

The Internuncio XML1203 explicitly marks Boyce and four Merovingian Soldiers `dead="1"`. SpawnNpc already assigned0HP/IsDead, but visibility skipped all dead NPCs before allocating a view. NPC creation also hardcoded IsDead=false and living CurrentState0x806. A marker sourced only from that XML flag now permits those mission bodies to be introduced. Their creation carries0HP/IsDead/deadstate0x7fd, followed by the existing sourced NPC death-pose packet. DeathPresentation keeps the NPC stance/animation layout separate from player prone stance5. Ordinary killed NPCs retain their existing-view-only corpse behavior; no respawn/loot policy changes.

Late viewers receive the same dead attributes and pose. Merely visiting a mission corpse does not kill it again or complete a defeat objective. The phase/mission still removes its own actors.

## Native dialog evidence

Read-only client SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. `research/abilities/inspect_mission_dialog_flags.py` asserts exact bytes. RPC80a1 constructor62139d20 is used at62179762; dispatcher calls622111f0. DisplayFlags is byte18: bit1 emits the portrait-dialog event6220d370; bit2 calls6220f220 and component22's text list620d5720, belonging to viewmissionstatus.cpp; absentbit4 writes timestamped mission-dialog history through6220f830->6220f260; this path is not established as general chat. ClientFlags byte19 separately controls mission state. The dispatcher has no hostility test.

A bounded query of280 retail corpus files found114 structurally valid80a1 records. Friendly portrait0x2800121f appeared22 times, allDisplay1/client0. Hostile0x2800121e appeared twice in duplicate copies of the same captured Watchman line: `Command accepted! Simulacrum is ready for combat.`, at06:08:52 on2009-07-31, Display5/client0. Sources: community-intake/20260607T212233Z-hdneo-public-session-spawn-movement-sanitized-selected/logs/mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:51284 and community-intake/20260608T122812Z-hdneo-public-endgame-mission-sanitized-selected/logs/mxopackets1_crashover2k__create_char_devoted_make_first_mission.log.txt:51266. Those are one event, not two independent observations.

Flags5 selects portrait output and suppresses that history; it does not select the mission-status text list. The absence of that extra channel is not proof of why the previous native hostile portrait was invisible. No floating speech or center-screen interpretation is claimed for bit2. Any extra visible fallback would be an explicit OURS presentation choice, not a recovered retail flag.

## Explicit OURS visible fallback

For automatic, noninteractive hostile NPC lines only, the same original recipient also receives one existing SYSTEM chat RPC2e containing `NPC name: resolved story text`. Original80a1 image, text and flags are preserved byte-for-byte, including5. No flag2/status-panel route is added. Friendly speech, actual interactive OnTalk choices and global NPC dialogue stay unchanged. This is a visible fallback policy, not a claim to have recovered retail hostile portrait visibility or reinterpreted history as chat.

A strict disposable diagnostic `?missionpresentation speech` accepts only the live in-world CSR110, MXO_DEV_COMMANDS exactly1, an mxotest_ database, active Internuncio1203 and a currently visible hostile scripted body owned by that mission instance. It passes fixed `[QA] Hostile mission presentation diagnostic.` through the same Say path, with original hostile portrait flags5. It accepts no caller-supplied entity or text and changes no mission goals, story text or actor state. It exists because The Internuncio contains bodies but no live-hostile speech line; a corpse's diagnostic line is clearly QA, not added story dialogue.

## One-pass Claude-only native checklist

Codex has not run native setup. After green CI on the pinned PR head, build Debug and use the normal NativeQa CLI with this checkout, a fresh QA directory, that full SHA and discovered mode `mission-presentation`. Mode seeds completed1201/1204 so Tyndall offers1203, places CSR_Tester at59000,1095,12200 near Campbell/Boyce, with5000HP. It enables dev commands for the strict fixed speech diagnostic, with no GM, RNG overrides or health hold.

1. Enter world normally. Open the native Missions window, choose Tyndall, request her next story mission, open The Internuncio details and Accept through the UI. Confirm the objective Get the Statue from Campbell and mission waypoint.
2. Walk around Campbell's room and adjacent bodies. Inspect Boyce and the four Merovingian Soldiers: they should be corpses with0HP, a prone/dead NPC pose and no combat interaction. Observe them once; do not attack, loot or advance the Get objective just to test presentation.
3. Keep the native chat window visible and type `?missionpresentation speech` once. Verify one `Boyce: [QA] Hostile mission presentation diagnostic.` SYSTEM chat line (or the actual visible owned hostile body's name), plus the unchanged hostile80a1 packet with flags5 in events. Record whether the original portrait window itself appears separately; that cause remains unresolved and must not be reported as repaired solely because chat works.
4. Close/return from the mission dialog normally. If checking Campbell's ordinary talk, approach and use the actual right-click/radial Talk interaction; friendly dialogue should retain its existing behavior with no extra system-chat echo. Do not call this a retail-complete mission presentation PASS unless the exact observed subset supports it.

Fresh headless covers ordinary mission list/details/accept, Boyce's dead creation/pose, a second viewer's dead creation/pose, and exactly one original hostile dialog plus one explicit system-chat fallback through the guarded diagnostic. Unit tests cover ordinary dead-NPC exclusion, scripted-body introduction, live restoration, captured flag1/5 preservation and negative QA privilege gates. Native acceptance/rendering remains Claude-only and pending.
