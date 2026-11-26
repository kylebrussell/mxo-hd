Use mode `corpse-loot-ui`, CSR_Tester110 only, one fixture.
Call Flood and accept Into the Hive (301210); earlier receipts are fixture setup.
Follow the measured street/lobby/floor positions printed by the server and use ordinary doors/cars.
Complete phase0, then kill Agent Park in ordinary close combat; weakening is optional assistance.
Require the corpse-loot window to appear automatically with the hit list.
Take All; require exactly one hit list and the Loot goal completed once.
Close a loot window locally, kill another hostile, and require the next loot window to appear again.
Close/reopen a corpse through the normal menu if offered; report a missing Search/Loot action separately.
The fix explicitly closes the old client window before publishing its next corpse selection; retained loot is unchanged.
Client6202088b/62021a25 returns a cached window;811a handler621ca200 clears it before8119 creates the replacement.
SendNpcDies and SendCombatEnd force-flush in separate packets, and automatic post-kill 81 1a + 81 19 is scheduled 0.25 s after combat teardown (gated by transport fence, world/combat generation, and corpse selection); manual double-click 80 c7 ... 05 00 opens immediately and suppresses duplicate auto-open.
For a template-free multi-kill check, mode `level-four-breadth` prints four nearby Chopper Runners; kill all four in a row (closing each Corpse window via Take All or X between kills) and require 4/4 automatic Corpse windows.
Claude owns native QA, lock, reset and merge; reset110 to Mara/full health/PvP0/mission986 on release.
