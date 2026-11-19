# Awakened combat actives

## Energized Attacks and Precise Blow

GOB 9 and 15 have a while-enabled modifier with a one-second UpdateFuncInterval. The shipped callbacks select Energized or Precise tactics at initialization and reset the mode on termination. Their update scripts call PlayFX only in combat, with 0x2800070c for Energized Attacks and 0x280006ff for Precise Blow. The existing combat consumers apply complementary damage and attack-roll changes.

The toggles reject concurrent use before charging the cast. The shipped STA IDs are 0xc6001a9f for attempting Energized Attacks with Precise Blow active and 0xc6001a9b for the inverse. The server retains its normal action refusal acknowledgement and sends the localized ability message. Recurring effects use the active toggle's exact identity and world, life and transport generations. They stop on removal or lifecycle changes.

Run NativeQa mode `ability-parity-awakened-tactical-toggles`. Enter Self Defense interlock against Melee Dummy, enable one tactic from Actions, and retain the on record and recurring effect. Try the other tactic, capture client refusal or outgoing request and reply, then disable the first and verify the pulse stops. Repeat in reverse order. The self and observer effect records are verified in the disposable replay. Stock rendering and the stock button gate at rest are pending.

## Power Shot

GOB 14 has two instant ValueFunction 4 modifiers, each with a range of 10–100 and scale 1. The caster's Current Inner Strength modifier has WhenEnabled 1 and STA 0xc40001af says it boosts the caster's Inner Strength. The target health modifier has WhenEnabled 2 and DamageType 0. The catalog also supplies a 16 m selection range, a 60-second recast and a success interlock move flag.

In retail client 7.6005, modifier evaluator 62517ff0 dispatches function 4 to 625180c5. The branch calculates a level-dependent center and calls the Gaussian sampler 6279f470 with bounds derived from the stored range. The constructor at 62818ff0 registers ability_max_avg_value_skew at binary32 0.8, and 62819050 registers ability_stddevs_per_range at 6. The modifier path at 62521419 uses the same globals. Exact server input levels and distinct landed damage, capped IS restoration and admission still need executable proof.
