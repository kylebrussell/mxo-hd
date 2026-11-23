# Hacker ability parity, second batch

This batch reviews 30 rows in the Corpsman, Destroyer, Doctor and Force Multiplier trees. The [reviewed audit evidence](../research/abilities/evidence/hacker-a2-runtime.json) records the gameplay checks and the source used for each.

Devastation Field (468) has a stored 50% proc on a 0.1-second terminal modifier that calls `ApplyStunWithTimer`. An admitted proc installs a six-second stun and matching immunity when the terminal callback runs, provided the target lacks active control immunity. Unit tests set the proc draw immediately below and at 50% and check delayed application, existing-immunity refusal, and expiry. The exact stock refusal line, damage magnitude, area geometry and interlock presentation remain open. The PvP area path now also refuses a retiring primary view before it can supply the area center.

## Ready for the combined Hacker sweep

Five rows have executable evidence for their remaining gameplay checks. Group Heal passed its stock replay. The other four need the indicated live conditions and legal loadouts.

| Row | Required stock observation |
| --- | --- |
| 237 Fast Healing 2.0 | Select a wounded living teammate within 10 m and observe +60% Health Regen Rate and the associated tick change until removal. Try a farther player and a hostile selection. Use a profile with ordinary natural regeneration enabled; the Corpsman heal fixture holds it during setup. |
| 381 Group Heal 1.0 | Stock cast on the selected nonmember healed CSR and the near teammate by 28 after four seconds. The 9.25 m teammate and the nonmember stayed at 100 HP. The retest plan staggers the stands at separate headings while retaining the same distances. [Native result](https://github.com/kylebrussell/mxo-hd/pull/351#issuecomment-7012105784) |
| 398 Repel Combat | Compare melee and ballistic defense and the sourced Powerless duration before loading, while loaded and after unloading at rank 50. |
| 461 Infection Efficiency | Compare melee accuracy, ballistic damage and a timed stun while toggling normal load membership. |
| 469 Destroyer | Record the 1088 grant becoming available and disappearing on load/unload. Measure the +5% stun duration while loaded. The A1 Corpsman profile has a different ancestry, so stage the Destroyer samples within a legal loadout for this tree. |

The NativeQa profile `ability-parity-hacker-a2-group-heal` stages 381 independently. Run `--fixture-plan ability-parity-hacker-a2-group-heal` for the setup and helper acceptance requests. At spawn, invite QAFriend at 7 m and QAFar at 9.25 m, leave QAOther at 3 m out of the team, select QAOther and use Group Heal from Actions. All start at 100 HP. CSR and QAFriend should reach 128 HP. QAFar and QAOther should remain at 100. The profile holds natural regeneration and covers 381. The other rows above need separate status, progression or combat fixtures.

The disposable `hacker-a2-group-heal` scenario sends an ordinary full-length activation and verifies cast, debit, reuse, team radius and fresh HP updates for Group Heal. The prior native evidence for Repair RSI 3.0 (235) is tracked separately. This batch adds no duplicate 235 closure.

## Additional samples and remaining gaps

| Rows | Evidence and next gap |
| --- | --- |
| 375, 326 | Existing resurrection approval covers normal offers, refusal and full recipient vitals for these IDs. Stock consent/motion and the flagged 1208 callback remain open for the individual rows. |
| 1092 | Current capture uses +7 Concentration, +10 radius and +6 Healing. The raw GOB radius and healing values are 7 and 5. Preserve the capture and recheck ordinary class-upgrade admission and printed values. |
| 399, 462 | Powerless and Enraged reach different action gates. Zone of Powerlessness uses the authored six-second base, with the normal duration influence applied on landing. The archived effective 9.9 s and 10.14 s records retain their unresolved profile inputs. Enrage 462 applies six seconds and expires. |
| 400, 466 | Actual health takes five viral ticks. The clean 400 fixture sees 10 per tick. 466's lower captured total applies 71 per tick before source influence; the raw total is 97. Detailed final damage math and wider admission remain unresolved. |
| 401, 403, 404, 465, 467, 1088 | The scoped consumers execute and restore baseline stats on expiry. A grouped hostile replay should still record target admission, range/deflect and exact area recipients. |
| 402 | The actual Evade Combat entry check consumes the −25 Force Combat modifier. All three movement penalties are present and expire. Client movement behavior and complete target admission remain open. |
| 233, 236, 238, 1097, 376 | Doctor grant membership is verified on the producer. Its linked Contested Withdraw contribution, the class upgrade's admission and evasion, Clear Head's linked Determination, Harmful Code damage math and Advanced Damage Resistance's unresolved stat links still need evidence. |
| 377, 379 | The scoped dispel and sweep callbacks execute. Cast admission, area team rules for the sweep and complete native presentation remain open. |
| 378, 380 | Startup dispatches the correct child and an actual paid pulse costs seven. A shortfall stops the toggle and leaves the remaining IS. Recipient expiry, simultaneous same-child overlap, full area rules and native FX or cleanse presentation remain open. [Prior stock pulse evidence](https://github.com/kylebrussell/mxo-hd/pull/320#issuecomment-6999754005) |
