# Coder ability parity, batch 3

## Stock checklist

Use `ability-parity-coder-utility` on the PR head. The disposable fixture loads Utility Master (620) as the sole authority for its child action 1108.

1. Enter the world and capture Code Hardening (608) in Status or PacketLog. It contributes +5 Melee Damage Bonus and +5 Ballistic Damage Bonus. Capture Utility Master (620) contributing +5 Melee Accuracy Bonus, and check that its granted action (1108) appears.
2. Cast Utility Technician (613), then the Utility Master action (1108). Expect Technician's +100 Max Health, +3 Melee Defense and +20 to each physical resistance. Master should replace all four records with +150 Max Health, +6 Melee Defense and +20 to each physical resistance. Capture self selection, the replacement order and separate 30 s reuse timers. Record any admission refusal and its visible UI state.
3. Summon Logic Daemon 3.0 (611) and Patch Daemon 2.0 (612) in turn at level 50. Expect level 34 Nuker and level 43 Healer, eight-second casts, ten-second reuse, and the stock registration and Deactivate flow.
4. With the Nuker out, toggle Daemon Boost 1.0 (609). Record the 10 IS activation debit and the immediate pet FX. The first 5 IS upkeep follows about five seconds after the pulse. Toggle off and capture the absence of a further upkeep debit across the next interval.

The first two samples could close four rows (608, 613, 620 and 1108) if the stock results match, including visibility and execution for the granted child. The pet and aura samples add coverage to rows with other open checks.

## Reviewed row scope

| Rows | Verified behavior and open evidence |
|---|---|
| 517 Weapon Maker | +50 loaded record, actual IW weapon specialization and strict compile contest, with an IT tool as a control. End-to-end weapon crafting remains open. |
| 608 Code Hardening | The +5 bonuses feed typed melee and ballistic damage and unload cleanly. Stock display remains open. |
| 609 Daemon Boost 1.0 | The shipped initial callback projects the aura immediately. The first upkeep is delayed until the update interval. The replay observes the pet FX, exact 10 and 5 IS debits, and toggle-off. Cross-owner recipients and the complete callback lifecycle remain open. |
| 699, 700 Daemon Boost children | Existing typed gameplay tests verify +25 viral damage/healing and +64 accuracy during the respective five-second effects. The runtime projects to the caster's own pet. Selector 19 also describes nearby friendly simulacra, and admission and modeled pet combat remain open. |
| 610 Engage Foes | The existing detailed replay verifies the six-second Enraged input, selection, exact cost, and NPC taunt ownership. Direct-object registration and stock cast presentation remain open. Reuse `engage-foes-taunt` for a separate native sample when practical. [Engage Foes evidence](ABILITY-ENGAGE-FOES.md) |
| 611, 612 summons | The grouped replay verifies exact levels, roles, ownership, cast and reuse, and the real Simulacra window teardown. The message and full callback lifecycles and the Healer action model remain open. |
| 613, 1108 Utility class buffs | Self routing, the four active stats and their replacement and cleanup have executable tests. Stock cast and presentation remain open. |
| 620 Utility Master | Tested +5 melee-only accuracy, loaded grant access and cleanup. Stock action visibility requires a check. |
| 607 Absorb Simulacra | The shipped callback checks owner identity. Damage type 5 and type 6 values and recipients still require executable support and stock evidence. |

## Utility Master Actions retest

In the first stock run, Utility Technician (613) applied its four caster bonuses, but Actions showed no Utility Master action under the 620 discipline. The native session did not send a 1108 cast. The preceding headless session cast 1108 and saw the four Technician records removed and four Master records added, which only verified server admission and application. [Stock report](https://github.com/kylebrussell/mxo-hd/pull/352#issuecomment-7011137184)

The server now registers the stored zero-valued 620 → 1108 grant after live-avatar `0c 01`. The ability remains absent from the owned archive. The disposable bootstrap verifies hidden-slot `80 b2` load followed by the source-qualified `80 bc 45` grant add, exactly once with repeated readiness. It then verifies Technician removal before Master applies, plus both 30-second timers. The unit regression also covers parent unload and reload.

In the stock client, confirm the Utility Master action appears in Actions, add it to the hotbar, and activate it immediately after Technician. Record Actions visibility, the outgoing `80 b9 54 04` request, +100 → +150 Max Health, +3 → +6 Melee Defense, both +20 resistances, and removal of all Technician records before the Master records. If absent, keep the tree screenshot and the two wire records, and leave the row open.

The stock retest found the Utility Master action under Actions. Activating it from the stock hotbar applied the four expected caster values and replaced Technician in order. This verifies 1108, while the individual loaded bonus comparisons for 608 and 620 remain open. [Stock Utility Master retest](https://github.com/kylebrussell/mxo-hd/pull/360#issuecomment-7011602121)
