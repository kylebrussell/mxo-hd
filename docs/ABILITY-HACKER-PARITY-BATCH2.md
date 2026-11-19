# Hacker ability parity, second batch

This batch reviews 30 rows in the Corpsman, Destroyer, Doctor and Force Multiplier trees. The [reviewed audit evidence](../research/abilities/evidence/hacker-a2-runtime.json) records the gameplay checks and the source used for each.

Devastation Field (468) has a stored 50% proc on a 0.1-second terminal modifier that calls `ApplyStunWithTimer`. An admitted proc installs a six-second stun and matching immunity when the terminal callback runs, provided the target lacks active control immunity. Unit tests set the proc draw immediately below and at 50% and check delayed application, existing-immunity refusal, and expiry. The exact stock refusal line, damage magnitude, area geometry and interlock presentation remain open. The PvP area path now also refuses a retiring primary view before it can supply the area center.

## Ready for the combined Hacker sweep

Five rows have executable evidence for their remaining gameplay checks. Group Heal fits the existing Corpsman sweep. The other samples need the indicated live conditions and legal loadouts. Count a row only after its stock result has been retained.

| Row | Required stock observation |
| --- | --- |
| 237 Fast Healing 2.0 | Select a wounded living teammate within 10 m and observe +60% Health Regen Rate and the associated tick change until removal. Try a farther player and a hostile selection. Use a profile with ordinary natural regeneration enabled; the Corpsman heal fixture holds it during setup. |
| 381 Group Heal 1.0 | Hold the shared +10% healing and +24% radius loadout. Recruit the near and far helpers, keep the nearby third helper outside the team and select that nonmember for the cast. Expect +28 HP on caster and the teammate within 7.44 m. The farther teammate and nonmember receive nothing. In the combined Corpsman profile, QAFriend at 4 m is inside and QAFar at 9.25 m is outside. |
| 398 Repel Combat | Compare melee and ballistic defense and the sourced Powerless duration before loading, while loaded and after unloading at rank 50. |
| 461 Infection Efficiency | Compare melee accuracy, ballistic damage and a timed stun while toggling normal load membership. |
| 469 Destroyer | Record the 1088 grant becoming available and disappearing on load/unload. Measure the +5% stun duration while loaded. The A1 Corpsman profile has a different ancestry, so stage the Destroyer samples within a legal loadout for this tree. |

To integrate 381 after the shared mode lands, add 381 to its Corpsman ability list and extend the heal loop with cost 20, amount 28 and no far recipient. Wait for the own four-second timer and omit the family wait, since 381 stores no family timer. This uses 59 learned-memory points including the shared ancestors. A separate 461/469 Destroyer setup uses 60 points.

The disposable `hacker-a2-group-heal` scenario sends an ordinary full-length activation and verifies cast, debit, reuse, team radius and fresh HP updates for Group Heal. The prior native evidence for Repair RSI 3.0 (235) is tracked separately. This batch adds no duplicate 235 closure.

## Additional samples and remaining gaps

| Rows | Evidence and next gap |
| --- | --- |
| 375, 326 | Existing resurrection approval covers normal offers, refusal and full recipient vitals for these IDs. Stock consent/motion and the flagged 1208 callback remain open for the individual rows. |
| 1092 | Current capture uses +7 Concentration, +10 radius and +6 Healing. The raw GOB radius and healing values are 7 and 5. Preserve the capture and recheck ordinary class-upgrade admission and printed values. |
| 399, 462 | Powerless and Enraged reach different real action gates. 399 still uses the retained 10.14-second capture against the raw six-second field. 462 applies six seconds and expires. Stock presentation and full area/admission checks remain. |
| 400, 466 | Actual health takes five viral ticks. The clean 400 fixture sees 10 per tick. 466's lower captured total applies 71 per tick before source influence; the raw total is 97. Detailed final damage math and wider admission remain unresolved. |
| 401, 403, 404, 465, 467, 1088 | The scoped consumers execute and restore baseline stats on expiry. A grouped hostile replay should still record target admission, range/deflect and exact area recipients. |
| 402 | The actual Evade Combat entry check consumes the −25 Force Combat modifier. All three movement penalties are present and expire. Client movement behavior and complete target admission remain open. |
| 233, 236, 238, 1097, 376 | Doctor grant membership is verified on the producer. Its linked Contested Withdraw contribution, the class upgrade's admission and evasion, Clear Head's linked Determination, Harmful Code damage math and Advanced Damage Resistance's unresolved stat links still need evidence. |
| 377, 379 | The scoped dispel and sweep callbacks execute. Cast admission, area team rules for the sweep and complete native presentation remain open. |
| 378, 380 | Startup dispatches the correct child and an actual paid pulse costs seven. A shortfall stops the toggle and leaves the remaining IS. Recipient expiry, simultaneous same-child overlap, full area rules and native FX or cleanse presentation remain open. [Prior stock pulse evidence](https://github.com/kylebrussell/mxo-hd/pull/320#issuecomment-6999754005) |
