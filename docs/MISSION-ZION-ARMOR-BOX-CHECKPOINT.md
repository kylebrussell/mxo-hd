# Earned Armor/Box checkpoint and measured-room introduction

This package separates the exact two-file measured-room introduction repair from the unmerged five-mission PR241 QA branch. The legacy two-argument room check is unchanged. Both current proxy-introduction checks receive the same captured current persisted elevator witness and recheck the site/witness reference before publication. No source, chooser, range, budget, timer, NPC placement or static changes.

Mode `zion-armor-box-checkpoint` uses one verbatim earned101216 phase3 checkpoint from the405.1-second95ad proof, raw SHA256 `6C1674B6AEA3F699A13D4958DDF93D315B4F696D771DF022F815C1540777CF1F`. Embedded payload SHA256 `349F3FA5F90F4323829F279271958A51CDBB1F3BEAB20A6B22AD5944C457031D`. Identity/start/previous outcomes, actors, elapsed timers, sources, measured witness and fixed548 placement remain untouched. Items are empty; only earlier prerequisite/selected-mission receipts are seeded. This is a disclosed earned-state fixture, not a new chain proof. Bornmann spread/bluevoid and native PR241 HOLD are separate.

The custom hook requires actual owned disposable database/server references and exact `MXO_ZION_ARMOR_SOURCE_SHA256`. It reads persisted HEX payload and receipt/account/inventory boundaries, authenticates realCSR110, restores the exact identity/DTO, uses the ordinary lobby door, and checks stationary arrival transport ACK before first548 creation. It does not Open, modify progress, heal, kill, choose a source, or teleport to the missionfloor. Authored phase3 goals do not yet allow a downward ride, so it logs out normally FROM the reached floor and waits33seconds for normal reservation release. Native therefore starts on that captured floor. Done/EverDone must remain unchanged; natural entered/timer updates are retained.

The failed5b hook reached and verified the floor/Box but incorrectly expected a downward ride before goals allowed departure. That46.34s failure and cleanup remain retained. The corrected lifecycle still needs a granted live qualification. Receipt is not native rendering, room-readiness or native door-entry proof.

## Native one pass — Claude only

1. Use the reviewed frozen mode and exact source pin, disposableCSR110 only. Preserve raw packet/debug/state files. The preset jacks into the captured floor after normal headless entry and floor logout, NOT the seed lobby. Headless entry is not native door-entry proof. No source-forcing workaround.
2. Walk normally and inspect the Box **before any Open**, with avatar/camera clear. Record visible body, actual hover/radial and Armor/NPC positions. If wrong floor, invisible/unreachable body, stale view or different source occurs, STOP/HOLD with evidence. Do not relax range or edit the capture.
3. Only after the pre-Open observation, use ordinary authored mission interactions if requested. This profile alone does not claim new earned rewards, unaided combat/escort survival, whole-chain completion, or a fix for separate NPC placement.

Before normal NativeQa startup, Claude supplies the reviewed source pin for the chosen configuration. For example, using the reviewed Debug producer (replace placeholders with the READY pins):

```powershell
$repo = 'C:/Users/russe/Developer/mxo-wt/codex-mission-armor-checkpoint-lifecycle'
$expected = '<reviewed Debug hds SHA256>'
if ((Get-FileHash "$repo/hds/bin/Debug/net8.0/hds.dll").Hash -ne $expected) { throw 'Source differs from READY' }
$env:MXO_ZION_ARMOR_SOURCE_SHA256 = $expected
dotnet "$repo/tools/NativeQa/bin/Debug/net8.0/NativeQa.dll" $repo '<new native proof directory>' '<reviewed head>' zion-armor-box-checkpoint
```

The hook refuses an absent/wrong pin. The normal command is Claude-only; Codex has not run a native client or taken its lock. Release requires its separately reviewed Release hds hash; do not reuse the Debug pin for a different producer.

No native PASS or complete live hook PASS is claimed at this source checkpoint. Three other earned Zion profiles remain separate future QA work after this core fix merges.
