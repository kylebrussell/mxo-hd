# Gunman parity, second batch

Pistol Evasive now applies its three shipped caster modifiers after the hit. Melee, ballistic, and thrown defense each gain 20 for 12 seconds. The same landed executor preserves the existing attack, and the ordinary expiry path removes all three records. The current modeled cast lands directly, so the interlock animation and queued admission remain open.

## Stock retest

Use disposable `gunman-upgrades-and-styles`. The mode seeds the four test guns, the three styles, and the five upgrades. Select Melee Dummy and activate each upgrade from Actions. Compare the exact self status deltas and verify the previous upgrade clears when the new one activates. Each row below can close with a passing native observation of its own values and replacement.

| Upgrade | Ballistic accuracy | Max HP | Ballistic damage | Ballistic defense |
| --- | ---: | ---: | ---: | ---: |
| Gunman 1070 | +4 | +70 | +2 | +4 |
| Expert Rifleman 1074 | +9 | +120 | +4 | +4 |
| Gunslinger 1075 | +4 | +100 | +2 | +6 |
| Duelist 1076 | +3 | +110 | +4 | +10 |
| Operative 1060 | +4 | 0 | 0 | 0 |

Operative also gives +4 melee accuracy. Review each gun style with a matching equipped weapon and capture the stand record and actual rendered exchange. The stock Close Combat action can switch to Self Defense first, so reselect the desired style once interlocked. On Handguns, use Pistol Evasive and retain the ability RPC, any queue record, the hit, the three caster modifiers, and their removal after 12 seconds. Note separately whether Block changes.

**Potential full rows from this grouped run** are 1070, 1074, 1075, 1076 and 1060 (5). The weapon styles and Pistol Evasive can contribute individual checks. A stand not visible in the trace keeps its verification open.

## Thirty-row review

| IDs | Verified scope and remaining work |
| --- | --- |
| 125, 239, 440, 441 | Exact loaded projectile accuracy/damage, melee or ballistic resistance, Root/Stun durations, and the granted ability where applicable. Unload restores baseline. Native samples remain pending. |
| 128, 240 | Exact loaded Initiative Bonus records and upgrade grants, removed on unload. The ordinary attack reads Initiative 1044, and the shipped passive supplies linked bonus 1046. The retail coefficient and consumption remain open. |
| 498 | Known consumers exist for the defense, resistance and Powerless duration bonuses. The modeled Determination duration policy still needs retail comparison, and the row remains open. |
| 129, 147, 158 | All seven active-style stats feed attack, damage, typed defenses and direct withdrawal. Unit tests cover weapon-class/ILDB selection, and native runs showed style-specific rounds. The retained stock trace did not identify the exact opening stand records. [Native run](https://github.com/kylebrussell/mxo-hd/pull/330#issuecomment-7006864509) |
| 126 | The retained native round queued Disarming Shot and landed 9522. The live accuracy range differs from GOB -15. The DirectObject script's unequip path and exact damage remain unresolved. |
| 176 | A single-SMG native run landed 21083. Root probability, state breaks and exact damage remain open. |
| 241 | The native run naturally applied Off Balance with Grab and landed 9527 in Handguns. The same capture proves the move and one Powerless result. The proc distribution, callbacks and damage/targeting remain open. [Native run](https://github.com/kylebrussell/mxo-hd/pull/330#issuecomment-7006864509) |
| 242 | Shipped caster defenses now land and expire. The active cast replay retains enemy damage. Exact damage, Block callback, and queued choreography remain open. |
| 243, 244 | Pistol Aerial has a retained 9518 observation. The exact conditional stun behavior and the Barrage proc, animation and damage still need coverage. |
| 442 | Steady Aim already has a landed +20 and a tested 30 IS / 5 s drain. The recast and remaining script checks stay open. |
| 450, 453, 454 | Existing slowdown/animation or stun paths are partial. The captured Crippling Shot movement values differ from the template, and Rifle Butt Smash/Deadly Shot still need precise native move and damage coverage. |
| 499, 500, 501, 502 | Existing effects cover portions of the dual-pistol attacks. Point Blank's Powerless path, Dash targeting, Whip's duration, and Execution's two conditional debuffs require further confirmation. Old scripts have separate damage constants. |
| 1060, 1070, 1074, 1075, 1076 | Self-only recipient, exact numeric totals and replacement tested. Grouped stock proof pending. |
| 1180 | The GOB has a five-metre radial definition and a five-second stun with immunity callback. Area eligibility, immunity timing and native accessibility remain open. |

## Gunman Upgrade activation and reuse

Stock capture `8d95b840f9f5` shows the 30-second `80 bd 01` timer in a separate packet before `80 bd 05` and the modifiers. Pressing the active hotbar slot again still sent an ordinary `80 b9`. The server treated that request as a new activation and denied it.

The installed 7.6005 client (client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`) explains the second request. Dispatcher `625e9ae0` sends subtype 01 to the timer map at `625e9970`. Subtype 05 reads the final on/off byte and updates the separate active map at `625e94a0`. It leaves the float unread. The eligibility code at `625c6c78` reads the authored `IsAToggleAbility` field and checks that active map. At `625c6c95` an active toggle bypasses the later cooldown branch at `625c6cf8`, allowing the client to request switch-off.

Ability 1070 is a shipped toggle. The server now removes its active modifiers on the second use and keeps the timer that started on activation. A new activation waits for the remainder of that timer. In `ability-parity-starter-gunman-grant`, activate and click at +4 seconds to turn the buff off. Click again while inactive and once more after the original 30 seconds. Capture the off records, the inactive hotbar gate and reactivation.

## Immobilizing Shot release

Vector26 records an eight-second Rooted interval from 07:05:56 to 07:06:04. The original recipient records retire, followed by 81 67 state-end 0x7a00003c naming Rooted 0x7a000155. An independent proxy_003 recipient capture records the same eight-second template adding and ending in the same second, alongside 254 damage and another state ending. Rooted receives the same state-end line after its paired removals. This second trace establishes early release presentation. The exact roll and cause of the archive's early release are unavailable. An earlier Vector26 root also clears during defeat and is excluded from the ordinary release comparison. [Natural release](../research/packet-dumps/community-intake/20260607T024558Z-hdneo-public-mission-code-rajko-worldstate-sanitized-selected/logs/mxopackets1_crashover2k_afterwhoruneo_old_world_packets_Vector26_manythings_4bill_upgradeabilitie_buyzionkey_start_keymission_gotowhite.log.txt#L25463-L25478) [Early release](../research/packet-dumps/community-intake/20260607T212233Z-hdneo-public-session-spawn-movement-sanitized-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_login_session.log.txt#L20681-L20704)

The shipped EnableRootWithChance registers SendGenericTermString on the chance record. That helper addresses the rooted recipient through both locators. The observed releases omit the separate 0x7c000236 generic term frame. The existing server presentation follows the captured paired removals and the ordinary Rooted state-end. The generic script dispatch remains unverified. Force Combat 519 -15 is consumed in the server's modeled Evade entry contest, whose original retail formula is separately unresolved. [Shipped callback](../research/client-scripts/tempmods.py#L1226-L1237)

The victim-side fixture uses the retained stock 254 for the natural phase, then the same 254 followed by a retained 255 shot for the damage phase. It uses ordinary break rolls. It checks the matching record IDs, single Rooted state-end, and owner and observer movement recovery on each observed release. The unit test independently exercises the protected first hit and rolls 49 and 50.

## Pistol Aerial Stun

The shipped Pistol Aerial (243) uses Handguns in interlock, a fixed Ballistic base of 16 and a 40% landed proc. Proxy_003 records a 0.01-second marker before the four-second Stun, its linked record, Combat Tactics −20 and independent immunity 734/681. The Stun and linked records retire with their original IDs. The existing combat consumer reads the penalty for the affected fighter. [Archived application](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt#L8690-L8702) [Archived removals](../research/packet-dumps/community-intake/20260607T191353Z-hdneo-public-combat-pvp-rajko-state-selected/logs/mxopackets1_crashover2k_afterwhoruneo_proxy_003_pvp_and_tele_to_park_east_north_died_in_pvp.log.txt#L9052-L9065)

`ApplyStunWithTimer` suppresses the Stun when control immunity is active and names a caster-only lower-buff stacking line. The server keeps the original immunity and sends one private visible system notice. The numeric retail string ID for that refusal remains unverified. [Shipped callback](../research/client-scripts/tempmods.py#L1659-L1672)

The level-25 `ability-parity-pistol-aerial` fixture tests ordinary live-opponent admission, damage against the same target with and without the stock +20 Soldier Upgrade, and the successful and immune Stun branches. Only the disposable QA pair forces the modifier roll. A separate fixture command holds an ordinary 734/681 record long enough to fit the immune test between Pistol Aerial reuses. The headless bootstrap checks the paired stands, Handguns and Close Combat with the retained 241 control. It leaves the first complete stock 243 request to the native run. [Previous stock choreography](https://github.com/kylebrussell/mxo-hd/pull/326#issuecomment-7002053269)

## Controlled Burst

The shipped level-27 Controlled Burst (250) stores Ballistic 26, 40% Enraged for eight seconds on landing, dual-submachine-gun category IWDS, a live enemy selector at 14 m, 30 IS, four-second casting script 92, ten-second own reuse and the five-second 689 family. The loaded test profile combines Submachine Guns +8 with Firearms Expert +2 and Dual Submachine Guns +3. Against the same level-27 target, this produces 29 before armor and 26 against +20 Ballistic Resistance from Soldier Upgrade. [Shipped ability](../data/ability_catalog.json#L252)

The timed move stays on its original selected spawn. A request admitted in an interlock must queue against its original opponent in the same fight after four seconds. A request admitted without an interlock retains the existing direct selected-hit route. A replaced fight, stale target, unloaded ability or wrong weapon cannot redirect the shot. The GOB stores CastWhatHow 2 and CastWhenUsed 2 with no explicit CastRequiredStates. The recovered CastWhatHow 2 flag check belongs to a tactic-total consumer and does not establish an extra target-state requirement for this move. The native mode records the interlock route and the stock client's separate at-rest decision. [Recovered tactic-total consumer](DESPERATION-CONSUMER-RESEARCH.md)

PR330 confirmed dual-SMG choreography 21082 and PR357 observed a natural eight-second Enraged application. The current mode checks the fixed Ballistic endpoint and matched Enraged removals in the same live player interlock, with ordinary proc rolls and Withdraw before and after release. Shared Enraged target-change and Block IS behavior were verified separately in PR883/919. Only 21082 has an identified ability-rule binding. The authored deflection flag leaves a separate choreography variant unresolved. [Stock move](https://github.com/kylebrussell/mxo-hd/pull/330#issuecomment-7006864509) [Stock Enraged proc](https://github.com/kylebrussell/mxo-hd/pull/357#issuecomment-7015751646) [Shared Enraged controls](https://github.com/kylebrussell/mxo-hd/pull/919) [ILDB binding](../data/combat_animations.json#L6213)
