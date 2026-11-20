# Starter Hacker viral damage

The Logic-series legacy rows have damage endpoints zero/zero. Harmful Code has 12/0. Each missing range reached the generic 50–80 draw. The shipped GOB assigns one instant viral Current Health modifier to each, with equal endpoints and ValueFunction 0.

| Ability | Template | Fixed base | Hacker Style fixture |
|---|---|---:|---:|
| 53 Harmful Code 1.0 | HarmfulCodeAbility | 12 | 14 |
| 57 Logic Barrage 1.0 | LogicBlast1Ability | 8 | 9 |
| 58 Logic Blast 1.0 | LogicBlast2Ability | 11 | 12 |
| 59 Logic Barrage 2.0 | LogicBlast3Ability | 17 | 19 |
| 60 Code Nuke 1.0 | LogicBomb1Ability | 7 | 8 |

The baseline test at master 4cf9225e drew 57 for all four with RNG seed 1 from the generic 50–80 fallback. The corrected test obtains the shipped bases with three different seeds and checks that a valid legacy range takes precedence. The high-rank capture tables still pin 330 to 424 and 359 to 645–1183.

Run `ability-parity-starter-hacker-viral-damage` for the stock check. At level 18, select Hacker Style and use all four from Actions against QAEnemy, waiting for shared reuse between casts. The fixture has +12 viral damage from Hacker Style and +5 linked viral damage from the required Transmit Virus passive. QAFriend is 2 m past QAEnemy and QAFar is 5 m past QAEnemy. The first three casts have a single target, and Code Nuke has a 4 m target-centered radius. Only Code Nuke should damage QAFriend.

For Harmful Code, use `ability-parity-harmful-code`, which loads its early prerequisites in 49 memory at level 19. The exact scoped regression failed on the old server. The new ability should deal 14 to the selected target with the existing bonuses, a 4-second cast, an 8-second own timer and a 4-second shared timer.

The disposable replay uses the complete Logic Barrage request from the PR396 stock run, with the ability ID and live handle substituted. PR396 observed the four Logic-series hits and the Code Nuke secondary target in the stock client. The stock Harmful Code request, retail contest and resistance formulas remain open.
