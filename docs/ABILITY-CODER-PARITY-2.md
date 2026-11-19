# Coder ability parity, batch 2

Run `ability-parity-coder-advanced` using the stock client and the disposable level-50 fixture. Capture the Status or PacketLog modifier records immediately after entering the world. The rank-50 passive contributions below each have typed gameplay and unload tests. Exact stock evidence could close three rows.

| Ability | Expected modifier records |
|---|---|
| Run Advanced Program (585) | Viral Damage Bonus 1031 +10 |
| Close Combat Training (600) | Ballistic Accuracy Bonus 996 +5 and Melee Accuracy Bonus 997 +5 |
| Enhance Daemon (614) | Ballistic Resistance Bonus 1140 +5 and Melee Defense Bonus 1143 +5 |

Additional passive samples are Tool Maker 518 and Upgrade Maker 521 (+50 each), Transmit Code 593 (+10 to each typed resistance bonus), and Proxy Master 599 (+5 Inner Strength Cost Modifier Bonus). Their other checks remain open.

In Actions, cast the advanced summons one at a time outside combat. Use the Simulacra window to Deactivate each before the next. Capture the cast/reuse, actual pet sheet and level, registration and removal. Expect eight-second casts.

| Summon | Pet at caster level 50 | Reuse |
|---|---|---:|
| Remote Proxy 4.0 (586) | Level 34 proxy | 8 s |
| Remote Proxy 5.0 (594) | Level 50 proxy | 8 s |
| Logic Daemon 2.0 (601) | Level 17 Nuker | 10 s |
| Patch Daemon 1.0 (602) | Level 22 Healer | 10 s |
| Logic Daemon 4.0 (616) | Level 50 Nuker | 10 s |
| Patch Daemon 3.0 (617) | Level 50 Healer | 10 s |

## Reviewed row scope

| Rows | Verified behavior and open checks |
|---|---|
| 585, 600, 614 passives | Verify the exact loaded and removed effects in the viral damage, accuracy, defense and ballistic resistance consumers at learned ranks 25 and 50. |
| 518, 521 crafting passives | Verify the +50 totals and actual category-specific contest inputs with the shipped Code Compiler (IT) and Health Pill (IC) prototypes. Materialization outcomes across supported tool and upgrade recipes remain open. |
| 593 Transmit Code | The linked throw and viral consumers are exercised on load and unload. Exact retail linked coefficients remain open. |
| 586, 594, 601, 602, 616, 617 summons | The bootstrap verifies owned entity, sheet, level, cast and reuse, registration and Deactivate. The creation message, full callback lifecycle, and Healer action values remain open. [Earlier stock Proxy and Nuker run](https://github.com/kylebrussell/mxo-hd/pull/312#issuecomment-7000140962) |
| 589, 698 Proxy Technician parent and child | Exact child values, cost cadence, source and recipient lifecycle, range exclusion and matching attack channels have executable tests. Pet hit baseline, child buffer semantics and full native presentation remain open. [Earlier stock aura run](https://github.com/kylebrussell/mxo-hd/pull/312#issuecomment-7000140962) |
| 595, 597, 588, 618 controls | The executable state templates expire at 6/10/12 s and 4/8 s respectively. Stun Pulse stores a separate 12 s immunity callback. Exact hit, geometry, controls and full combat playback remain open. |
| 590, 591, 598 viral attacks and 605, 618 specials | The runtime exposes generic damage and queued Low Blow/Bash animations. Exact damage, rules and native special choreography still need proof. |
| 596, 587, 615 pet repairs | The stock applied heal and invalid-player selection are tracked in the PR333 pet-repair follow-up. |
| 599, 1107 Proxy Master | The grouped run verifies the +5 cost modifier bonus record. The linked cost consumer and the grant's selected-own-pet routing, modifiers and cleanse remain open. |
| 603 Charge Simulacra, 619 Daemon Boost 2.0 | Existing real resources and typed attack tests exercise the pet effects. The Charge range script and exact base resource rates remain open. Daemon Boost's own-pet targeting differs from selector 19 friendly-area behavior. |
| 604 Utility Coder | The live ability executor chooses self and applies +50 Max Health and +20 to both physical resistances, with numeric cleanup. Stock admission, class replacement and active presentation still need a run. |

## Stock passive records

Stock CSR110 showed the matching bonus rows as aggregate totals. The native login's PacketLog entry at 08:13:21 (CSEQ 2, SSEQ 10) independently identifies each contributing ability. These are individual `80 bc 45` add records with locator low byte 0 and duration 0.

| Source | Target and signed value | Record locators |
|---|---|---|
| 585 | 1031 +10 | `00011300` |
| 600 | 997 +5 and 996 +5 | `00011700`, `00011800` |
| 614 | 1140 +5 and 1143 +5 | `00011b00`, `00011c00` |

The two 600 records are Melee and Ballistic Accuracy Bonus. The aggregate Viral Accuracy Bonus row does not attribute a viral contribution to 600. The source-specific wire proof complements the existing headless typed consumer and unload checks. The stock session did not retest the summons. [Stock report](https://github.com/kylebrussell/mxo-hd/pull/349#issuecomment-7010210704)
