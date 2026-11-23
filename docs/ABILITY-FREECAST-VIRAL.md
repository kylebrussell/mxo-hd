# Free-cast viral contest — PARTIAL server policy

Normal positive-cast TF2 attacks previously went directly to damage and effects. They now test each existing legal single-target or target-centered-area recipient once, before damage rolls, resistance, tempmods and hostile procs. An explicit cast origin keeps already resolved queued exchanges, passive/child/aura/DOT callbacks and other test functions outside this contest. Selector4 and pet35 remain gated.

Primary script: `research/client-scripts/generic_free_attacks.py`. Its attack342, defense183, consistency637 and actual player tactic factors feed a strict attack>defense test. Block reduces attack input to0 but still rolls. NPCs lack a modeled current tactic; multiplier1 is an explicit server policy. Only current modeled buff/passive deltas are available. Native base/inherent totals and percentage-link coefficients remain unresolved; this implementation supplies none.

The chosen modern policy uses native integer mean/sigma construction, signed16 narrowing, range100/69 and the inverse table from client6258da00/6279f2f0. `research/protocol/freecast-viral-roll.json` records table bits, thresholds, SHA256 and independent literal/exhaustive oracle outputs. Tests cover all32768 rand15 values for seven input vectors. The unsigned left-tail clamp and32767 return-mean edge are preserved. Selecting Gaussian modern flags, max consistency100, independent server rand15 draws and nearest-even64-significand arithmetic is **OURS**. The live x87 control word, server settings and random sequence are unproven; this is no claim of exact retail probability or sequence.

Captured `81 67` hit/deflect events in `research/protocol/freecast-viral-events.json` establish stringIDs240000f1/f2, flag1, ability-ID type08 and four signed fields attack/0/defense/0. They are byte-tested against source capture records. Deflection uses sourced FX2800059c with unchanged health. Recipients are caster, victim and created-view observers in the same world; hidden/foreign-world observers receive nothing. Existing cost and cooldown policy is retained, including cost paid at cast start.

## Archived level-50 damage for 336 and 337

Both logs capture normal `80 b9` requests from AfterWhoruNeo on July 9, 2009. The Object12 name/level record reads `32` hex (level 50) before the combat sequence. The selected Zombie Lurch views 10/161 and 16/162, plus the later Zombie Rot/Lurch/Curse cohort, all decode at level 52. The captured hit and deflect records witness normal contest outcomes in live PvE. They establish successful target admission for the landed records. Independent line-of-sight geometry and the caster's complete active loadout are still unknown. [Caster view][bomb-caster] · [Lurch creations][bomb-lurch] · [Rot and Lurch creations][bomb-rot] · [Curse creation][bomb-curse] · [Hit and deflect][bomb-contest]

Each row below groups identical `81 67` damage-caster records (string `0x7c0000fa`). Packet counts include area records with the same printed target name. The two reported values are landed and resisted damage. Their sum is inferred pre-mitigation damage, and all pairs fit four-percent mitigation with nearest-integer rounding.

| Ability | Landed | Resisted | Sum | Records | Packet-header lines |
| --- | ---: | ---: | ---: | ---: | --- |
| 336 | 250 | 10 | 260 | 1 | [Marketplace 8726][bomb-250] |
| 336 | 269 | 11 | 280 | 5 | [Combat 13519][bomb-269]; combat 16096, 18332; marketplace 4365, 12747 |
| 337 | 288 | 12 | 300 | 12 | [Combat 13664][bomb-288]; combat 16853 (2), 17618, 19120 (2); marketplace 1472, 3493 (2), 18048, 22761 (2) |
| 337 | 346 | 14 | 360 | 4 | [Marketplace 11916][bomb-346] (2); marketplace 27773, 40998 |

The shipped GOB stores fixed viral damage modifiers of 14 and 17. The current server retains the capture-calibrated pre-influence roll bands 266–323 and 283–346 and applies the target's typed resistance after the offensive stages. The relation of the fixed modifiers to those historical damage totals, level and PvP scaling, and the reason for the two Speed-band 336 totals remain open.

The captures expose an intermediate stage for 337. All twelve caster records totaling 300 follow a visible Speed request. All four totaling 360 follow Power, with the same latest tactic at request and landing for each paired sample. The marketplace Power requests at lines 11160, 27207 and 40679 precede the corresponding 360 groups. This supports a single 300 pre-tactic subtotal in those sessions because the shipped `GetFullAbilityDamage` multiplies by 1.2 under Power. The chosen server policy applies that Power factor for Code Nuke 2.0 at landing, after existing viral influence and before typed mitigation. [Power before the first 360 group][nuke-power] · [Shipped damage calculation](../research/client-scripts/combat_calculations.py#L301)

The marketplace 336 request at 12203 precedes a switch from Power to Speed at 12675, and lands for a pre-resistance sum of 280 at 12747, matching other Speed 280 samples. That ordering is consistent with sampling the landing tactic. The other Speed total of 260 prevents a unique 336 subtotal. The implementation retains the existing 336 calibration. [336 request][bomb-switch-cast] · [Speed before its landing][bomb-switch-speed]

[bomb-caster]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat.log.txt#L10978
[bomb-lurch]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat.log.txt#L11960
[bomb-rot]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat_marketplace_abilitychangecoder.log.txt#L2067
[bomb-curse]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat_marketplace_abilitychangecoder.log.txt#L21204
[bomb-contest]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat.log.txt#L16836
[bomb-250]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat_marketplace_abilitychangecoder.log.txt#L8726
[bomb-269]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat.log.txt#L13519
[bomb-288]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat.log.txt#L13664
[bomb-346]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat_marketplace_abilitychangecoder.log.txt#L11916
[nuke-power]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat_marketplace_abilitychangecoder.log.txt#L11160
[bomb-switch-cast]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat_marketplace_abilitychangecoder.log.txt#L12203
[bomb-switch-speed]: ../research/packet-dumps/community-intake/20260607T003822Z-hdneo-mxopackets1-combat-ability-selected/logs/actions_combat_marketplace_abilitychangecoder.log.txt#L12675

## Diagnostic profiles and one-pass native checklist

`freecast-viral-hit` and `freecast-viral-deflect` load2/17/1006/33/57/314/272 at the existing WinterFrost fixture. Neither grants GM status nor changes combat, damage, callback or health RNG. Only this new contest consumes `MXO_FREECAST_VIRAL_QA_ATTACK15`/`DEFENSE15`, gated by exact dev1, marker1, actual validated mxotest_ database, live authenticated non-LA actor, owned loaded ability and protected-character exclusions. Generic callback draw variables remain blank. Default profiles clear all new keys.

1. In the hit profile select WinterFrost and cast normal Static Blast33 outside interlock. Expect the viral hit log, normal animation and damage.
2. In the deflect profile repeat without activating314. Expect the deflect log/effect, unchanged target HP and paid IS/cooldown. This profile forces the contest only; ordinary damage rolls and combat remain normal.
3. With a legal enemy player, normal Logic Blast57 should follow the same hit/deflect profile; a nearby observer should see the result. Headless tests cover this route; real client appearance remains Claude-only and pending.
4. 314 and272 remain real existing buffs: activation changes modeled attack or defense inputs. They can change the diagnostic outcome, so the baseline checklist deliberately leaves them inactive.

No new generic buffs, viral immunity/resistance contest, pet authority or caster-centered area consumer is claimed. Headless framing and authoritative state are separate from native rendering acceptance.

## Existing fixture compatibility

Twenty existing headless cases asserted guaranteed positive-cast TF2 landing. They now explicitly opt into contest-only draws32766/15636 and establish owned active abilities through the existing strict `?qaload` bridge when needed. This does not change their other RNG controls, base stats or damage policy. The two new hit/deflect cases use their own separate draw pairs. Other scenarios do not opt in. Merged PR178 adds two radius fixtures with positive-cast333; both now explicitly choose the same scoped landing control, while retaining its caster-aware TargetMetres calls. The discovered native radius profile enables dev only for that strict contest guard; generic callback draws stay blank.

Affected existing cases: `consumable-sweep-control`, `ability-health-presentation-damage`, `ability-concentration-armed-fixture`, `ability-concentration-control`, `ability-concentration-loaded`, `ability-health-deflect-lineup`, `combat-depth-mitigation`, `ability-buff-buffer-death`, `ability-control-duration-bonuses`, `ability-control-duration-npc`, `buff-dispel-fixture-hit`, `buff-dispel-fixture-miss`, `buff-dispel-proc-pvp`, `hostile-fields-pvp`, `hostile-fields-npc`, `ability-passive-damage`, `typed-ability-resistance`, `combat-rounds-and-observers`, `ability-parity-controls`, `ability-parity-area`.

## Validation record

All22 selected fresh cases passed on source401995f2799c6b24cb587c97177e2440ab592285: the new hit/deflect cases27.4/31.7s (driver exit0,59.1s,parallel1), plus both serial10-case regression batches (277.8s and400.3s, each exit0). Both slots were released after normal disposal. Initial new-case failure67.8s was a fixture error:57 was sent during33's actual shared682 cooldown. The corrected case waits for its real timer-off packet; production cooldown policy is unchanged. Failed logs remain in Temp/mxo-freecast-fresh-new.

New-case DB-named JSON/runtime evidence is saved before teardown even on failure. It includes actual selected DLL path/configuration/SHA256, every actor's RPCs and all received03 lists. Captured fields are NPC hit100/0/34/0, PvP hit100/0/55/0, NPC deflect50/0/69/0 and PvP deflect50/0/111/0. Tested Debug DLL SHA256A5FDDBB1A139E39A65CF747F8FD941C817401C3611ABC0DA173B6776BEEB75E4. Headless proof demonstrates ordinary admission, result/effect framing, authoritative health and existing-resource/cooldown behavior; native acceptance remains pending.

Full4434 unit suite passed before the narrow created-view follow-up; current45 focused tests additionally cover missing/uncreated/deleting otherwise-legal player views and actual314/272 buffs flipping the common contest and restoring it on removal. Pure upstream master180 world15 import changes none of the six tested contest/origin/runtime source blobs, so fresh tests were not repeated for that import. Both build configurations/read-only plans/provenance, actual all-module ability audit, Python12 policy tests and deterministic CRLF three-artifact roundtrip were checked. Historical tracked audit snapshots remain unchanged.

Merged178 follow-up: both caster-aware TargetMetres reads are preserved. Its two existing333 radius headless fixtures now explicitly select guarded contest-only landing draws; the native radius mode truthfully enables dev only for that guard, with GM/generic callback forcing off. Already owned333 is refreshed through the normal QA loadout/80ae bridge before the headless casts. Current69 scoped tests pass. Exact debuff-radius-native-fixture fresh PASS56.4s/exit0/parallel1 on source6e3b28fad48d7da6c36f36c4c23ce52e6e7902c5, Debug762D24A9C584A166CEE0912F92E03B52FF6C58AC6C9483284EB5CC9F19AF3B5F. Five actual hit=True qa=True contests prove primary+near baseline and primary+near+edge expanded; outside and baseline-edge have no contest. Preteardown135397byte Temp/mxo-debuff-radius-native-mxotest_run_1_09e94baee7.json SHA256cb2b74b78c61475549483bf0c559e4f03db4fa66511cb5cf9bf9d477669b8cd5 preserves44received03 lists/allRPC/startupflags/traces. This is23 selected fresh passes overall; direct102s prior178 proof/nativePASS retained without another redundant repeat. Source/native rendering remains a separate Claude check.
