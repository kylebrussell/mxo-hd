# Starter interlock damage

Disarming Shot (126), Guard Breaker (138), and Iron Guard (139) have single fixed instant health effects in the shipped catalog. Their respective bases are ballistic 11, physical 36, and physical 12. The three legacy CSV ranges are 0/0 and previously fell back to a generic damage roll. The sourced bases still pass through the ordinary landed interlock dispatch, combat influence, and typed resistance.

## Stock capture

Use `ability-parity-starter-interlock-bases` on level-19 CSR110. The live prerequisite profile uses 44 of 50 memory with rank-15 abilities and a pistol equipped.

1. Use Close Combat on **QAEnemy**, who is flagged on the isolated PvP fixture. Once the interlock is active, reselect Handguns and use Disarming Shot from the hotbar. Capture the actual selected-target request, the landed round, the damage presentation, and the targetâ€™s ten-second Ballistic Accuracy penalty and its matching removal. The live path retains the captured range âˆ’34 to âˆ’18 documented at `hds/world/Structures/TempMods.cs:298`. The GOB lists a raw âˆ’15, which is outside this base-damage change. The older Python pistol callback names a weapon-unequip effect. Canonical recipient and invocation still need primary stock evidence.
2. Leave interlock, unequip the pistol and enter Close Combat with **Melee Dummy**. Reselect Aikido and use Iron Guard. Capture the landed move, the physical result, and the caster's +10 melee and +10 ballistic defense adds and removals after twelve seconds.

## Isolated Guard Breaker retest

Use `ability-parity-starter-guard-breaker` for row 138. This starts unarmed beside Melee Dummy with no PvP opponent. The prior stock run (packet log `fb16102b1c0c`) landed Disarming Shot for 11 with a ten-second Ballistic Accuracy reduction of -21 and Iron Guard for 13 with both +10 caster defense records for twelve seconds. Iron Guard was against QAEnemy while the pistol remained equipped.

1. Select Melee Dummy and enter ordinary Close Combat. Reselect Kung Fu during the interlock and keep the dummy selected.
2. Enter `?combatdepth offbalance 60` in chat. The fixture enables the existing disposable-only command. Wait for the response confirming 885/200 on Melee Dummy. The disposable replay also checks the target view for simultaneous interlock and Off Balance (04).
3. Click Guard Breaker while it is enabled. Capture the actual outgoing request, landed choreography, physical damage (40 in this profile), and -10 target melee defense for fifteen seconds. If the round misses, retry after reuse while Off Balance is active. Refresh the sixty-second state if it has expired.

The QA command prepares the prerequisite alone. The move still uses the ordinary hit roll and damage path. Natural Grab remains covered in the original grouped disposable replay.

The disposable probe opens interlock with the byte-backed Close Combat sequence used by the stock choreography fixture, patching its live target handle. It uses the established full inline 43-byte ability record layout for the three combat activations. These specific stock hotbar requests still need capture. If an action is greyed out or fails to send, preserve the current style, weapon, target state, and active interlock evidence. The probe uses normal round contests and retries within the same fight after a missed round.
