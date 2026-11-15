# Mission team state after relog

A saved mission could restore its HUD, actors and item ownership without restoring the native client's mission-team manager. Claude observed the resulting Mission window Abort confirmation say "Are you certain you wish to leave your team?" and send no request after Yes. Source restoration now creates a missing team through the existing normal helper, then sends full808e team information and the own MissionTeamID before replaying the saved mission HUD. Existing team leadership and membership are retained. A disconnected solo player gets the same ordinary solo-team policy used by mission acceptance.

## Primary client evidence

Pinned7.6005 client SHA256: cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a. `python research/abilities/inspect_mission_team_replay.py <client.dll>` checks the native bytes without loading or modifying the client.

- Mission window620d6770 calls621bfc40 at620d67b9. It compares the cached team leader(+0x2c) to the own character ID. True selects stringaa0000bd, "Are you certain you wish to abort your current mission?", and MIAc. False selects aa0000c0, the Leave Team prompt, and MILc.
- MILc confirmation620d6949 calls621c0540. That function returns without sending anything unless its native group state(+0x30) is3. When joined it sends71 with the actual group type.
-808e's native dispatcher62179393 calls622143a0. Its consumer populates the real members and passes the packet's leader field to the native setter at622145c4. Existing SocialPackets.TeamInfo has the captured full team layout; no new packet shape is invented.
- Server disconnect removes live team membership. MissionPersistence.WorldEntry previously replayed the saved mission without rebuilding this social state. The baseline wire regression reproduces the missing808e after an ordinary disconnect/relog.

The exact native Abort click still needs Claude's check. The server regression supplies the proven missing prerequisite, then sends80a6 directly to test cleanup; it cannot establish that the native UI emits the request. Shared team-mission progression and persistent multiplayer team membership are outside this fix.

## Regression and native fixture

Unit tests cover no mission, missing solo team and an existing team with a different leader; replay must not promote that member or duplicate its team. The fresh mission-team-relog scenario starts2202 normally, performs the first actual Get, logs out, waits for normal33-second session cleanup and reconnects to the same server. It checks full team info before the restored mission HUD, own leader/member/TeamID, unchanged checkpoint/grant ownership and actual server Abort cleanup. Baseline without replay must fail for missing team info. No active mission state is granted in SQL and no RNG is overridden.

Discovered mode `mission-team-relog` uses only the existing prerequisite2201 and fixed `?missionpersist start` acceptance, dev commands and ordinary abilities2/17. It has no GM, forced rolls or new gameplay authority. Codex only executes --fixture-plan/--build-info; Claude alone starts native fixtures.

## Claude one-pass checklist

1. Start a fresh fixture at this PR's exact green head using mode mission-team-relog and CSR_Tester110 only. Type `?missionpersist start`, find Herschel and right-click > Get once. Confirm only the first goal completes.
2. Quit normally, wait for normal reservation cleanup, then log in again to the same fixture. Confirm the first goal stays complete and the bag item is not duplicated. Check that808e/full team state precedes the restored809c HUD.
3. Open HUD > Mission > Mission Status > Abort. A solo leader should now see the actual Abort-current-mission confirmation, rather than the Leave Team fallback. Click Yes. Record whether the client sends80a6; verify the abort HUD/end response, empty active checkpoint and owned mission-item cleanup. Do not count a forced server request as native success.
4. Relog once more. The aborted mission must not return and its owned item must remain gone. Record any remaining client gate if Yes still sends no request.
5. Stop normally, reset/verify main CSR_Tester at Mara(23828,495,13984), full health, flag0 and mission986 only, then release the lock as usual. Never select TheNeo.

## Recorded validation

The baseline ordinary-disconnect run failed61.5s/exit1 for missing808e after the restored mission HUD (Temp/mxo-abort-team-baseline-run-20261112.txt). A first attempted fixed run still used the baseline binary because Copy-Item preserved an older source timestamp; it failed68.1s and is retained separately (Temp/mxo-abort-team-fixed-run-20261112.txt). Both configurations were then forcibly rebuilt. The corrected fixed run passed55.6s/exit0, with full team state before809c, preserved first Get and item ownership, and successful actual80a6 cleanup. Proof: Temp/mxo-abort-team-fixed2-run-20261112.txt and the unique server-folder MissionTeamRelog-Proof.txt path printed there. This is server/wire evidence; native Abort remains pending.
