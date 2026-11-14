# Completed mission objectives after relog

Historical PR163 wire fix: native DISPLAY FAILED. Its early combined replay is superseded by [the current lifecycle follow-up](MISSION-OBJECTIVE-UI-REPLAY.md). Use that checklist and distinguish the floating tracker from Mission Status > Objectives.

Claude's native PR158 run at70be317 passed mission restoration and Abort. It also found a smaller presentation regression: Watchful Eyes displayed two identically worded Get objectives before relog, with the first checked, but only one unchecked line afterward. The first device and durable completed goal were preserved.

`MissionRuntime.Reannounce` previously called `SendPhase`, which creates only open objective rows, and then sent completion updates for already completed goals. The missing checked row is consistent with a completion update for a row the fresh HUD never received. This is an inference from the native observation and server packet order; no new native record layout is proposed.

Restoration now creates this phase's completed and open rows using the existing80a0 open state, then replays completed states after all those rows exist. Goal IDs remain distinct even when their text is identical. Goals still locked behind an unfinished dependency and goals from other phases stay hidden. Ordinary phase entry still creates only open objectives. This changes presentation only: checkpoint state, item ownership, rewards, triggers and phase progression are untouched.

## Automated checks

Unit tests inspect actual queued80a0 records for creation-before-completion, identical labels, hidden dependencies, phase boundaries and unchanged ordinary entry. The discovered headless case `mission-objectives-relog` reuses the normal mission acceptance/Get/logout/33-second cleanup/relogin flow. It checks the restored first row is created before its completion tick, the second row stays unfinished, team state still precedes the mission HUD and the saved item is not duplicated. A second ordinary Get must grant only the other device, then Abort removes both owned devices and the checkpoint. The successful raw server log is saved to its own unique Temp file before teardown.

## Claude-only one pass

Use a fresh exact-green-head `mission-objectives-relog` fixture and CSR_Tester110 only. The mode grants only prerequisite2201 and abilities2/17; existing dev commands allow normal `?missionpersist start` acceptance of2202. It has no GM permission, random override or new gameplay command.

1. Type `?missionpersist start`, follow the mission waypoint to Herschel and right-click > Get once. Open Objectives: the two identical tracking-device lines should be separate, first checked and second unchecked. Keep only that first Get.
2. Quit, allow normal session cleanup, then relog to the same fixture. Reopen Objectives. Both rows should return with the same checked/unchecked states. Retain the raw809c/80a0 sequence; completed goal1 must be introduced with state0 before its state1 update. Confirm the first device remains a single item.
3. Right-click Herschel > Get once more. Only the second device should arrive and only the second row newly completes. Use Mission Status > Abort > Yes, verifying the already-fixed80a6 flow and cleanup.
4. Relog again: the aborted mission and both devices must remain gone. Reset main CSR_Tester110 to Mara(23828,495,13984), full health, flag0 and mission986 only before releasing the lock.

Native checkmark acceptance remains pending. Headless packet order does not prove the native Objectives panel displays both rows.

## Recorded validation

Full Release4,231 tests and27 focused mission tests passed. Both NativeQa configurations and actual read-only named/unknown plans plus assembly provenance passed;12 Python policy/link tests passed. The actual-wire regression passed58.2seconds/exit0 with one disposable server, normal cleanup and no source/binary edits during the run. Its raw success log was saved before teardown. Native checkmark presentation still needs Claude's check.
