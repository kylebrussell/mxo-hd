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
