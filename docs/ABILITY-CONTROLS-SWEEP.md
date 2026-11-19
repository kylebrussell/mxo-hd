# Control and immunity sweep

A current control-immunity timer is the active `734 → 681` modifier tuple. An unrelated modifier on attribute 681 does not grant immunity. At the timer's expiry, an eligible new control may land and replace the expired timer even if the expiration sweep has not run. Replacement sends the old timer's removal before the new timer is presented.

Confuse blocks tactical changes, withdrawal and combat special moves. The same special-move gate is checked when already queued moves resolve. Ordinary abilities, weapons and movement remain available while confused. See [ConfuseInit and ConfuseTerm](../research/client-scripts/tempmods.py#L873) and the [immunity callbacks](../research/client-scripts/tempmods.py#L1631).

## Native QA

Use NativeQa mode `ability-controls-sweep` with the stock client. The disposable CSR fixture starts at Richland `24800, 495, -1498` with a level 50 loadout. Four normal-size humanoid targets stand at least 3 m apart. WinterFrost and Trainman are west of the start, and Blackwoods and Smithling are east. Select each target through its visible model and cast through Actions. Check the target FX, reuse feedback and immunity notices in System. Wait for each immunity window before casting the next ability that grants immunity. Some spells can reach more than one target.

| Ability | Select | Expected state | Duration | Immunity |
| --- | --- | --- | ---: | ---: |
| 40 Code Freeze 1.0 | WinterFrost | Root 99 | 25 s | — |
| 1081 Ravager Downgrade | Trainman | Confuse 91 | 8 s | — |
| 43 Disrupt Inputs 1.0 | Smithling | Blind 90 | 16 s | — |
| 333 Stun 2.0 | Trainman | Stun 102 | 5.25 s | 10 s |
| 393 Code Stop 1.0 | Smithling | Mez 85 | 6.9 s | 12 s |
| 48 Freeze System 1.0 | Blackwoods | Pacify, using Mez 85 | 6.9 s | 12 s |
| 355 Vector | WinterFrost | Powerless 96 | 8.94 s | 8 s |

The listed durations include the fixture loadout's Resist Contagion and Effective Code bonuses. The bootstrap runs these ordinary casts with a disposable helper, validates the NPC state and duration and the caster's four localized immunity records, and writes `ability-controls-bootstrap.json`. It fixes the viral hit contest draws and lets its status effects expire before CSR enters. The common ordinary entity-target request is 43 bytes. The bootstrap retains the opaque bytes of the outgoing Stun 2.0 sample from `proxy_020` lines 140965–140967 and rewrites the ability id and live target handle, as the stock sender does at `621e5125` and `621e521e`. See the [stock layout and pinned DLL](ABILITY-USE-RECORD.md). The template preserves one actual selector and ground payload. Per-ability native captures, native FX and player-side status presentation remain pending.

## Remaining work

ConfuseUpdate's `randomizeQueue()` and exact Determination Bonus weighting remain open. The NPC fixture observes the local combat notices and server duration, while existing player regressions cover modifier records and health-triggered breaks. The audit keeps all control rows open pending complete per-ability coverage and family-native results.
