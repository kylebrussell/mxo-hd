# Gunman parity, second batch

Pistol Evasive applies its three authored caster defenses on a landed hit. Melee, Ballistic and Thrown Defense each gain 20 for 12 seconds. The timed cast binds the original visible hostile and handgun. It lands directly from rest or queues in the original interlock, where the current GOB contributes +10 tactic. The current Ballistic base is 5. The older Pistol Evasive script's base 90 and Block-only modifiers belong to a different definition. [Current GOB](../data/ability_catalog.json#L244) [Older script](../research/client-scripts/ability/soldier/old_sm_pistol.py#L59-L72)

Original CombatView entries show the same 242 ability in 9529 MR and 9530 SR. The sampled entries place the fighters around 259 and 140 units apart, respectively. Pistol Barrage's 9519 MR and 9534 SR also appear at different presentation gaps. A continuing Hel fight uses both variant families in consecutive rounds, so the emitted offsets identify presentation rather than the original input classifier. The normal runtime uses the observed 9529. Selection of 9530 remains open. [MR entry](../research/packet-dumps/community-intake/20260608T130150Z-hdneo-public-warboy-world-game-sanitized-selected/logs/ProxyM2_m2_packets.log.txt#L276143-L276150) [SR entry](../research/packet-dumps/community-intake/20260607T015831Z-hdneo-public-inventory-lastday-rajko-nearend-selected/logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt#L4101-L4108)

The level-22 fixture checks 5 neutral and 4 after the same enemy gains +20 Ballistic Resistance. Both stock cast routes and the owner's 12-second defense records have a native plan. Headless startup uses the retained Handguns and Close Combat controls.

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
| 126 | Stock used 9522, and the player attack now applies its authored -15 Ballistic Accuracy for 10 s. The retained Special Agent and Great Wall Security Socho captures describe NPC origin. The older DirectObject unequip path remains unresolved. |
| 176 | Stock single-SMG interlock used 21083 and the shipped fixed base 17. The timed Rooted state holds movement and releases on expiry. The original localized term presentation remains open. |
| 241 | The native run naturally applied Off Balance with Grab and landed 9527 in Handguns. The same capture proves the move and one Powerless result. The proc distribution, callbacks and damage/targeting remain open. [Native run](https://github.com/kylebrussell/mxo-hd/pull/330#issuecomment-7006864509) |
| 242 | Shipped caster defenses now land and expire. The active cast replay retains enemy damage. Exact damage, Block callback, and queued choreography remain open. |
| 243, 244 | Pistol Aerial has a retained 9518 observation. The exact conditional stun behavior and the Barrage proc, animation and damage still need coverage. |
| 442 | Steady Aim already has a landed +20 and a tested 30 IS / 5 s drain. The recast and remaining script checks stay open. |
| 450, 453, 454 | Existing slowdown/animation or stun paths are partial. The captured Crippling Shot movement values differ from the template, and Rifle Butt Smash/Deadly Shot still need precise native move and damage coverage. |
| 499, 500, 501, 502 | Existing effects cover portions of the dual-pistol attacks. Point Blank's Powerless path, Dash targeting, Whip's duration, and Execution's two conditional debuffs require further confirmation. Old scripts have separate damage constants. |
| 1060, 1070, 1074, 1075, 1076 | Self-only recipient, exact numeric totals and replacement tested. Grouped stock proof pending. |
| 1180 | The GOB has a five-metre radial definition and a five-second stun with immunity callback. Area eligibility, immunity timing and native accessibility remain open. |

## Disarming Shot and Covering Fire cast targets

The GOB gives both shots CastWhenUsed 2, CastWhatHow 2 and a four-second cast. A valid at-rest shot may still land directly. A shot admitted in interlock can queue only against its original live opponent, with the original fight and required single-weapon category through cast completion. If the queued opponent changes, that shot is removed. A midcast weapon swap cannot turn an interlock shot into direct damage.

The rank-19 fixture uses a level-26 character so both prerequisite chains fit simultaneously. Its bootstrap replays the two retained stock interlock requests and checks the weapon-swap cancellation and unchanged-fight controls. The stock plan probes both at-rest requests separately.

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

PR330 confirmed dual-SMG choreography 21082 and PR357 observed a natural eight-second Enraged application. The current mode checks the fixed Ballistic endpoint and matched Enraged removals in the same live player interlock, with ordinary proc rolls and Withdraw before and after release. Shared Enraged target-change and Block IS behavior were verified separately in PR883/919. Only 21082 has an identified ability-rule binding. On a non-landing, the server uses the existing shipped exchange for the active styles and the round outcome. The combat-view test checks both a double miss and an opponent hit, including the literal fallback moves and the absence of a landed special. The original named deflection move ID remains unresolved. [Stock move](https://github.com/kylebrussell/mxo-hd/pull/330#issuecomment-7006864509) [Stock Enraged proc](https://github.com/kylebrussell/mxo-hd/pull/357#issuecomment-7015751646) [Shared Enraged controls](https://github.com/kylebrussell/mxo-hd/pull/919) [ILDB binding](../data/combat_animations.json#L6213)
## Sliding Volley

The current GOB sets a fixed Ballistic base of 77, a Dazed opponent in interlock, a 16 m selection range, dual SMGs, and a +24 tactic input. The archived older SMG direct-object callback used 100. The current record also supplies a 20% landed Enraged modifier lasting 10 seconds and names a success move. The ILDB maps that move to 21081. A miss follows the ordinary style exchange. [Current GOB](../data/ability_catalog.json#L251) [Older callback](../research/client-scripts/ability/soldier/old_sm_smg.py#L52-L57) [ILDB move](../data/combat_animations.json#L6212)

The runtime requires the selected live opponent and the current view at admission. A queued move stays with that opponent. A changed opponent, stale spawn or missing view discards it, and a weapon or loadout change can make it ineligible for resolution. The level-22 legal Submachine Guns profile has +11 Ballistic influence. The same-target test lands for 85 neutral and 76 with +20 Ballistic Resistance.

The `ability-parity-sliding-volley` fixture bootstraps from the retained stance and Close Combat requests. It leaves the full 249 request for the native run, alongside 21081, the ordinary miss, and the paired natural Enraged removal. Only the Daze prerequisite uses a disclosed fixture command. Damage, contests and Enraged rolls remain ordinary.

## Three Round Burst

The current GOB defines a four-second Rifle special usable in and out of interlock. The selected hostile needs Staggered, a live view within 20 m and line of sight. It costs 20 IS, applies its authored +21 to the queued tactic contest, and uses fixed Ballistic base 80. The old Rifle script used 100. A valid interlock cast must finish against its original opponent and fight. A changed view, fight, weapon or loaded move cannot redirect the damage. [Current GOB](../data/ability_catalog.json#L258) [Older Rifle callback](../research/client-scripts/ability/soldier/old_sm_rifle.py#L68-L73)

The exported ILDB rule binds 11367 directly to 256. The separately named 11368 entry has no recovered outcome selector. The ordinary rifle exchange remains the fallback when a special misses. The landed 40% terminal marker runs ApplyStunWithTimer, which applies StunAbility 102. Its authored Combat Tactics -20 is linked to the four-second Stun and removed with it. The four-second immunity remains independent. The new plan checks the matching recipient stat IDs and does not claim an original 256 marker encoding. [Extracted entries](../data/combat_animations.json#L4674-L4675) [Stun GOB](../data/ability_catalog.json#L106) [Shipped callback](../research/client-scripts/tempmods.py#L1659-L1672)

In `ability-parity-three-round-burst` the legal level-27 Rifles profile contributes +13 Ballistic influence. Same-target typed tests produce 90 neutral and 82 with +20 Ballistic Resistance. Headless startup uses the retained Stance and Close Combat only. The stock 256 request, gated target state, resolved melee animation and at-rest route still require native verification.

## Pistol Barrage

The current GOB uses dual handguns with a two-second cast, a fixed Ballistic base of 18, +15 interlock tactic, and a 40% chance to apply eight seconds of Powerless on a landed hit. The old pistol script uses a fixed base of 215. The current level-27 loadout contributes +11 Ballistic influence. The selected opponent takes 19 neutral or 17 through +20 Ballistic Resistance. The cast preserves its original hostile view, weapon and combat route. A queued move also keeps its original opponent through the round. [Current GOB](../data/ability_catalog.json#L246) [Older pistol callback](../research/client-scripts/ability/soldier/old_sm_pistol.py#L29-L34)

Original 244 results contain 9519 MR at a 390-unit presentation gap and 9534 SR at approximately 140 units. The runtime uses observed 9519 on a queued hit and keeps the ordinary style exchange for a non-landing. The original 9534 input selector remains unverified. [MR entry](../research/packet-dumps/community-intake/20260607T015831Z-hdneo-public-inventory-lastday-rajko-nearend-selected/logs/mxopackets2_RajkoPacketsNearTheEnd_PacketsGotIntoHelAgain.log.txt#L4811-L4819) [SR entry](../research/packet-dumps/community-intake/20260608T130150Z-hdneo-public-warboy-world-game-sanitized-selected/logs/ProxyM2_m2_packets.log.txt#L269586-L269596)

The `ability-parity-pistol-barrage` fixture keeps the same level-27 player as the neutral at-rest target and the armored interlock target. The helper uses the retained Soldier Upgrade during an at-rest Powerless and again after recovery. The retained 129 and Close Combat controls bootstrap the legal dual-handgun profile. The first full 244 request and player recipient records remain native checks.

## Disarming Shot accuracy by caster

The authored UDB0 modifier specifies source 126, target 524 and a fixed -15 for ten seconds. Playable Disarming Shot uses that source. The archived NPC-only distribution stays available to NPC casters. The Special Agent applies -24 on NlOBEE in the linked capture.
[Special Agent](../research/packet-dumps/community-intake/20260608T122812Z-hdneo-public-endgame-mission-sanitized-selected/logs/mxopackets1_medanon_niobe3dualduality.log.txt#L14679-L14695)

The existing rank-19 player fixture runs the same pistol and distance before, during and after the effect. Its actual Ballistic Accuracy attack input drops by 15 and recovers on the matched removal. Its Melee Accuracy input stays fixed throughout.
