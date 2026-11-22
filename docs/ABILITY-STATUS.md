# Ability Status, Combat and Bonuses

The server now reads the five primary attributes from the saved character snapshot it sends on world entry. A committed allocation replaces the whole snapshot under the existing allocation lock. Older characters use the same per-attribute display fallback as before. The base is added after the modifier cap, as traced in [Client linked stat math](CLIENT-LINKED-STAT-MATH.md). Max Health and Max Inner Strength now apply the same existing server cap as other direct stat consumers. The timed effect record retains the original caster-derived value. The existing percent bonuses continue to use each character's unbuffed maximum.

A level-50 Upgrade Health supplies a +500 direct modifier. For a level-10 recipient with a 500 health maximum, the receiver's stored Max Health cap is 10 × 25. The published maximum rises to 750 and returns to 500 on removal. This tests the shared cap and the authoritative maximum separately from the raw modifier record.

The shipped data has a template for NPC Determination 769. Its value function reads the learned rank. No supplied NPC ownership/rank assignment was established. The player base for stat 575 and the retail server's live linked coefficients remain unresolved. The direct timed Determination value from 1102 continues to enter the existing mitigation formula. [Determination mitigation](ABILITY-DEFLECT-CODE.md)

The cosmetic review retains the source gates. Coder 18, Game Master 397, Data Miner 537, Data Node Goggles 538, Data Tap 539 and Organization 1199 have metadata roles in the shipped data. Code Shaper 35 and 697 still need an incoming viral contest on simulacra. Hacker 52 and Uncanny Senses 161 have unreachable numeric contests in the available scripts. Resist Infection 75 needs evidence for the authoritative 1055-to-177 link. Tap Data Node 165 and Siphon Data Node 622 need the native tapping and yield executor. Cryptography 297 needs a separate consumer for Decryption Skill. Repair Items 512 needs a sourced repair executor. [Cosmetic review](ABILITY-PASSIVE-CONSUMERS.md)

## Stock-client pass

Start NativeQa in `ability-status` mode. Enter as CSR110. Stay at the seeded Mara spawn. Open Status and record the Combat and Bonuses tabs before each activation, after each activation and after each timed removal. Compare displayed deltas to the character's own baseline. Record actual tab values, screenshots, expiry and any omissions.

| Action | Expected status change |
| --- | --- |
| Hyper-Block 1099 | Melee Defense +52 |
| Determination 1102 | Determination +150 |
| Upgrade Attacks 314 | Viral Accuracy +108 for 20 seconds |
| QAEnemy casts Destroy Resolve 354 | IS Regen Rate 30 → 15 → 30 (−50% for 30 seconds) |
| QAEnemy casts Destroy Health 476 | Health Regen Rate 25 → 20 → 25 (−20% for 40 seconds) |

Leave the two indefinite buffs active. Use the normal Actions window for the three player casts. During each debuff case place a fresh request JSON file in the fixture's `requests` directory. Use the corresponding response file to confirm the helper request was sent, then check Status after the cast completes. Wait for the displayed regeneration rate to return to baseline. The stock HUD test covers the regen rows. The headless own-view check covers Max IS 2000 → 1942 → 2000 and Max HP 5000 → 4480 → 5000.

```json
{"actor":"QAEnemy","action":"status-debuff","ability":354}
```

Repeat with 476. The disposable mode uses the existing scoped viral contest draws to isolate the numerical deltas. The self-checking `--bootstrap-checks` variant verifies the actual add and removal messages and the own-view vital updates in an isolated process before native QA is requested.

The shipped debuffs target IS Regen Rate (114, −50) and Health Regen Rate (112, −20), both with `Ability/ValueType = 1`. The server publishes those signed values on the add and remove records. PR315's stock UI showed `30 × (1 − 50/100) = 15` and `25 × (1 − 20/100) = 20`. [PR315 native QA](https://github.com/kylebrussell/mxo-hd/pull/315#issuecomment-6999207958)

The server's natural regeneration uses the same percentage multipliers. In a two-second out-of-combat tick at a deficit, Destroy Resolve halves IS recovery from 8 to 4. Destroy Health reduces max HP from 5000 to 4480 and multiplies the resulting HP recovery by 0.8, so the tick goes from 100 to `round(4480 × 0.01 × 2 × 0.8) = 72`. Both return to their original tick values on expiry. The stock Status row values and server tick amounts use different baselines.

## Stock request evidence

World entry uses the stock `05` request and `0c 01` readiness record. [Loading-complete evidence](STAT-PRODUCERS.md)

The fixture sends full 43-byte `80 b9` requests. Hyper-Block and Determination replay captured no-target RPCs. The targeted template comes from Upgrade Attacks and binds the ID and current view/spawn pair while preserving the captured inventory, tool and ground bytes. The historical mode also uses that template for Hacker Style and the helper debuffs. [Native ability producer and field layout](ABILITY-USE-RECORD.md)

- Hyper-Block vector capture, 5088–5089. [Vectornew](../research/packet-dumps/community-intake/20260607T205703Z-hdneo-public-vector-supplemental-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_Vectornew.log.txt#L5088)
- Determination Hel capture, 13896–13897. [Got Into Hel Again](../research/packet-dumps/community-intake/20260607T015831Z-hdneo-public-inventory-lastday-rajko-nearend-selected/logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt#L13896)
- Upgrade Attacks PvP capture, 9080–9081. [Vector PvP and Park East](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt#L9080)

## Upgrade Attacks level snapshot

GOB 314 sets Viral Accuracy 342, duration 20 seconds and value function 3, which selects character level. The July 29 capture shows AfterWhoruNeo's own-view level 50 and the self-to-self source-314 bonus +108. The July 31 capture shows the same handle's own-view level 69 during the final event, followed by the self-to-self source-314 bonus +146. Both `80 bc` records use type `15 00`, stat `56 01`, and duration `00 00 a0 41`. The July 29 `80 b9 3a 01` at 9080–9081 is the captured Upgrade Attacks cast. [July 29 own view](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt#L254) · [July 29 bonus](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt#L9370) · [July 31 own view](../research/packet-dumps/community-intake/20260607T022135Z-hdneo-public-two-person-actions-sanitized-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt#L3518) · [July 31 bonus](../research/packet-dumps/community-intake/20260607T022135Z-hdneo-public-two-person-actions-sanitized-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_020_afterwhoruneo_some_2_person_actions_with_mxoemu_and_mission_and_crew_things.log.txt#L142919)

The server takes `2 × character level + 8` at cast completion. This is a server policy calibrated to those two observations. The stock GOB's function-3 coefficient of 1.3 alone predicts about +65 at level 50. The active snapshot feeds the normal Viral Accuracy consumer until the original record expires. The existing level-50 Status fixture still expects +108.

Use `ability-parity-hacker-upgrade-attacks` for the starter-level check. At level 22, select Hacker Style and QAFriend, then press Upgrade Attacks. Confirm the caster's +52 Viral Accuracy in Status and the return to baseline after 20 seconds. Confirm the selected player receives no source-314 record, with a 35 IS debit, a two-second cast and a 90-second own reuse.
