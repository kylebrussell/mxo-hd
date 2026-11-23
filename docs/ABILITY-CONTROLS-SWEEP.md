# Control and immunity sweep

A current control-immunity timer is the active `734 → 681` modifier tuple. An unrelated modifier on attribute 681 does not grant immunity. At the timer's expiry, an eligible new control may land and replace the expired timer even if the expiration sweep has not run. Replacement sends the old timer's removal before the new timer is presented.

Confuse blocks tactical changes, withdrawal and combat special moves. The same special-move gate is checked when already queued moves resolve. Ordinary abilities, weapons and movement remain available while confused. See [ConfuseInit and ConfuseTerm](../research/client-scripts/tempmods.py#L873) and the [immunity callbacks](../research/client-scripts/tempmods.py#L1631).

## Freeze Area 2.0 (479)

The archived 10.02-second Pacify and -100 Viral Resistance came from Radueriel attacking RajkoHaxor. The ability's hit and state notices name both actors, and Radueriel's creation identifies the NPC_BASE template 94030008. The same records give Viral Resistance and control immunity 12 seconds each. [Named hit](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt#L60961-L60962) · [Modifier adds](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt#L60996-L60998) · [NPC creation](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsMaraCThenTimeDOut.log.txt#L61459-L61463)

The server keeps those values for NPC casts. A player casting 479 on either target type uses the authored six-second Pacify and -80 Viral Resistance for twelve seconds. The resistance modifier carries `AddControlImmunityTimer`, which installs the separate twelve-second timer using the modifier's remaining time. The normal Pacify rule removes the state and its parent resistance on damage. The separate immunity and other recipients keep their own timers. [Ability catalog](../data/ability_catalog.json) · [Immunity callback](../research/client-scripts/tempmods.py#L1631)

Use the `ability-parity-hacker-high-rank-pacify` fixture for the player retest. The fixture's Effective Code raises the six-second base to 6.90 seconds. Compare the first natural expiration with a second cast broken by Logic Blast. PR931 had verified the visible 6 m area, 14 m selection limit, 2 s cast, 37 IS and 15 s reuse with the older NPC-derived values. [PR931 native result](https://github.com/kylebrussell/mxo-hd/pull/931#issuecomment-7093037499)

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

The listed durations include the fixture loadout's Resist Contagion and Effective Code bonuses. The bootstrap runs the six retained control samples with a disposable helper, validates NPC states and the caster's four localized immunity records, and writes `ability-controls-bootstrap.json`. Ravager Downgrade remains in the stock checklist through its ordinary Ravager grant. Its complete own use request has yet to be retained. It fixes the viral hit contest draws and lets its status effects expire before CSR enters. The common ordinary entity-target request is 43 bytes. The bootstrap retains the opaque bytes of the outgoing Stun 2.0 sample from `proxy_020` lines 140965–140967 and rewrites the ability id and live target handle, as the stock sender does at `621e5125` and `621e521e`. See the [stock layout and pinned DLL](ABILITY-USE-RECORD.md). The template preserves one actual selector and ground payload. Per-ability native captures, native FX and player-side status presentation remain pending.

## Remaining work

ConfuseUpdate's `randomizeQueue()` and exact Determination Bonus weighting remain open. The NPC fixture observes the local combat notices and server duration, while existing player regressions cover modifier records and health-triggered breaks. The stock retest verified casts, state and duration on all seven assigned targets. The earlier run verified the four immunity announcements and refused controls through active immunity. Exact FX, native player-side presentation and other per-ability gates still need coverage. [Assigned-target retest](https://github.com/kylebrussell/mxo-hd/pull/321#issuecomment-7001527488) and [immunity retest](https://github.com/kylebrussell/mxo-hd/pull/313#issuecomment-6998807737).
