# Hacker hostile upgrade and debuff sweep

Run `ability-parity-hacker-hostile-upgrades` with level-50 CSR110. The two owned Archive profiles use 86 and 100 memory. This disposable fixture fixes the ordinary viral contest draws so the landed effects can be inspected. Dispel callback rolls keep their ordinary behavior.

After CSR has fully entered and can select targets, use the helper chat action to move the staged players. For example, submit `{"actor":"QAEnemy","action":"chat","text":"?tp 25200 495 -1498 0 0 0"}` through the fixture requests. The other stands are QAFriend at **24850, 495, -1498** and QAFar at **27000, 495, -1498**. QAFriend is allied. QAEnemy and QAFar are enemies. QAFar is outside all five selection ranges on the CSR stand.

Select Hacker Style and keep QAEnemy selected for the following hotbar casts.

| Order | Ability | Shipped selection range | Stock observation |
| --- | --- | --- | --- |
| 1 | Overheat 1.0 (65) | 20 m | -24 melee and -24 ballistic resistance on QAEnemy for 33 seconds, then remove both. |
| 2 | Cancel Upgrade 1.0 (377) | 16 m | Remove one active eligible upgrade on QAEnemy and emit the corresponding caster and target removal lines. |
| 3 | Delete Upgrade 1.0 (324) | 16 m | Remove one active eligible upgrade on QAEnemy and emit the corresponding removal lines. |
| 4 | Delete Upgrade 2.0 (420) | 16 m | Remove an eligible upgrade on QAEnemy. Its additional sweep independently rolls 20% per eligible remaining parent. |
| 5 | Combat Shutdown (480) | 14 m | -61 melee and ballistic accuracy and -31 melee and ballistic defense on QAEnemy for 25 seconds, then remove all four. |

First use helper cast 217 on QAEnemy and wait for the Evading state. Leave it on. Before each of the three strip casts, arm QAEnemy with a real Gaussian Blur (49) using `{"actor":"QAEnemy","action":"cast","ability":49,"target":"QAEnemy"}`. Check its add record and allow its eight-second reuse before arming it again. Save each normal enemy-selection request, the exact removal line that attributes it to the cast, and the 49 off record. A landed 420 with one eligible parent proves its guaranteed removal. For the extra sweep sample, also arm Combat Evasion (110) on QAEnemy with the same helper action. A single landed 420 removing both fresh parents corroborates the extra pulse. If it only removes one, check the survivor, wait for reuse and rearm the removed parent before retrying. Overheat is a hostile debuff and is outside the eligible upgrade set.

Before step 5, unload Upgrade Artist (318), Delete Upgrade 2.0 (420) and Enhanced Potency (422). Load the Combat Shutdown chain, keeping the other active Archive entries. Load Code Freeze 2.0 (41), Network Hacker (64), Skript Kiddie (390), Effective Code (391), Code Rot 1.0 (392), Overheat 2.0 (394), Efficient Code (477) and Combat Shutdown (480). Keep the resulting memory at 100. Select QAEnemy again and capture the four timed adds and removals. The ordinary PvP admission tests also distinguish an allied selection and an out-of-range hostile selection. Preserve those refusals if you repeat them after the relevant reuse.

Stock evidence is scoped per recorded ability. Full native contest probability, obstructed geometry and the additional 420 random sweep each require their own observation.
