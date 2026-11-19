# Starter Hacker viral damage

The four legacy rows have damage endpoints zero/zero, and none has a captured override in the landing handler. The shipped GOB assigns one target Current Health modifier to each. The modifier is instant, viral, and has equal endpoints with ValueFunction 0.

| Ability | Template | Fixed base | Level-18 Hacker Style fixture |
|---|---|---:|---:|
| 57 Logic Barrage 1.0 | LogicBlast1Ability | 8 | 9 |
| 58 Logic Blast 1.0 | LogicBlast2Ability | 11 | 12 |
| 59 Logic Barrage 2.0 | LogicBlast3Ability | 17 | 19 |
| 60 Code Nuke 1.0 | LogicBomb1Ability | 7 | 8 |

The baseline test at master 4cf9225e drew 57 for all four with RNG seed 1 from the generic 50–80 fallback. The corrected test obtains the shipped bases with three different seeds and checks that a valid legacy range takes precedence. The high-rank capture tables still pin 330 to 424 and 359 to 645–1183.

Run `ability-parity-starter-hacker-viral-damage` for the stock check. At level 18, select Hacker Style and use all four from Actions against QAEnemy, waiting for shared reuse between casts. The fixture has +12 viral damage from Hacker Style and +5 linked viral damage from the required Transmit Virus passive. QAFriend is 2 m past QAEnemy and QAFar is 5 m past QAEnemy. The first three casts have a single target, and Code Nuke has a 4 m target-centered radius. Only Code Nuke should damage QAFriend.

The disposable replay uses the normal 43-byte captured Logic Barrage form with the ability ID and live handle substituted. The replay checks the existing contest, cast, damage, reuse and recipient paths with the fixed bases. The retail contest and resistance formulas and ability-specific stock input and rendering remain open. The four audit rows remain partial.
