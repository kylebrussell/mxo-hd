# Starter interlock damage

Disarming Shot (126), Guard Breaker (138), and Iron Guard (139) have single fixed instant health effects in the shipped catalog. Their respective bases are ballistic 11, physical 36, and physical 12. The three legacy CSV ranges are 0/0 and previously fell back to a generic damage roll. The sourced bases still pass through the ordinary landed interlock dispatch, combat influence, and typed resistance.

## Stock capture

Use `ability-parity-starter-interlock-bases` on level-19 CSR110. The live prerequisite profile uses 44 of 50 memory with rank-15 abilities and a pistol equipped.

1. Use Close Combat on **QAEnemy**, who is flagged on the isolated PvP fixture. Once the interlock is active, reselect Handguns and use Disarming Shot from the hotbar. Capture the actual selected-target request, the landed round, the damage presentation, and the targetâ€™s ten-second Ballistic Accuracy penalty and its matching removal. The live path retains the captured range âˆ’34 to âˆ’18 documented at `hds/world/Structures/TempMods.cs:298`. The GOB lists a raw âˆ’15, which is outside this base-damage change. The older Python pistol callback names a weapon-unequip effect. Canonical recipient and invocation still need primary stock evidence.
2. Leave interlock, unequip the pistol and enter Close Combat with **Melee Dummy**. Reselect Aikido and use Iron Guard. Capture the landed move, the physical result, and the caster's +10 melee and +10 ballistic defense adds and removals after twelve seconds.
3. Reselect Kung-Fu, use Grab and wait for the target's actual Off Balance state (mask 04). Use Guard Breaker while the action enables. Capture the landed move and its target-only âˆ’10 melee defense for fifteen seconds.

The disposable probe opens interlock with the byte-backed Close Combat sequence used by the stock choreography fixture, patching its live target handle. It uses the established full inline 43-byte ability record layout for the three combat activations. These specific stock hotbar requests still need capture. If an action is greyed out or fails to send, preserve the current style, weapon, target state, and active interlock evidence. The probe uses normal round contests and retries within the same fight after a missed round.
