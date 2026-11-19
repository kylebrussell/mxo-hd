# Awakened combat actives

## Energized Attacks and Precise Blow

GOB 9 and 15 have a while-enabled modifier with a one-second UpdateFuncInterval. The shipped callbacks select Energized or Precise tactics at initialization and reset the mode on termination. Their update scripts call PlayFX only in combat, with 0x2800070c for Energized Attacks and 0x280006ff for Precise Blow. The existing combat consumers apply complementary damage and attack-roll changes.

The toggles reject concurrent use before charging the cast. The shipped STA IDs are 0xc6001a9f for attempting Energized Attacks with Precise Blow active and 0xc6001a9b for the inverse. The server retains its normal action refusal acknowledgement and sends the localized ability message. Recurring effects use the active toggle's exact identity and world, life and transport generations. They stop on removal or lifecycle changes.

Run NativeQa mode `ability-parity-awakened-tactical-toggles`. Enter Self Defense interlock against Melee Dummy, enable one tactic from Actions, and retain the on record and recurring effect. Try the other tactic, capture client refusal or outgoing request and reply, then disable the first and verify the pulse stops. Repeat in reverse order. The self and observer effect records are verified in the disposable replay. Stock confirmed both on/off sequences and that the active tactic continues after a refusal. The single-message retest and the stock button gate at rest remain open.

## Power Shot

GOB 14 has two instant ValueFunction 4 modifiers, each with a range of 10–100 and scale 1. The caster's Current Inner Strength modifier has WhenEnabled 1 and STA 0xc40001af says it boosts the caster's Inner Strength. The target health modifier has WhenEnabled 2 and DamageType 0. The catalog also supplies a 16 m selection range, a 60-second recast and a success interlock move flag.

In retail client 7.6005, modifier evaluator 62517ff0 dispatches function 4 to 625180c5. The branch calculates a level-dependent center and calls the Gaussian sampler 6279f470 with bounds derived from the stored range. The constructor at 62818ff0 registers ability_max_avg_value_skew at binary32 0.8, and 62819050 registers ability_stddevs_per_range at 6. The modifier path at 62521419 uses the same globals. Exact server input levels and distinct landed damage, capped IS restoration and admission still need executable proof.

## Stock retest of the tactical conflict

Stock capture 58353c2f063b delivered both cross-toggle refusals to CSR110. At 15:43:25, packet lines 3490–3492 contain the reply for 15 and the shipped localized record c6001a9b. At 15:43:48, lines 4599–4601 show the reply for 9 and c6001a9f. The first stock pass reported neither line. [The stock retest on 22efd304](https://github.com/kylebrussell/mxo-hd/pull/383#issuecomment-7022547427) showed the shipped localized line first in each direction, followed by the supplemental plain chat. The shipped conflict callbacks each call the localized output function once. The server now sends the usual action refusal acknowledgement and that localized output only. The next stock pass should confirm one visible localized line in each direction.

| Toggle | On | First FX | Last FX | Off | Unique own-view pulses |
|---|---|---|---|---|---|
| Energized Attacks | 15:43:15 | 15:43:16 | 15:43:28 | 15:43:29 | 13 |
| Precise Blow | 15:43:38 | 15:43:39 | 15:43:52 | 15:43:52 | 13 |

These are packet-header times with one-second precision. Energized gaps are all one logged second. Precise has one two-second logged gap from 15:43:49 to 15:43:51 and all other gaps are one. The last Precise pulse precedes the off packet in file order. The repeated effect bytes also appear on observer views and do not represent extra pulses.
