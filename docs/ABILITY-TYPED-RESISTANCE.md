# Typed ability resistance: explicit OURS application policy

Calm Mind, Calm Body5 (Thrown Resistance1033) and Empty Mind8 (Viral Resistance157) now affect supported hostile health-damage abilities and DOT ticks. The existing timed function3 buffs resolve75 at character50 for20seconds. Personal Firewall68/319 and other existing stat producers feed the same target totals. This is a **partial** shared consumer, not recovered full retail damage parity.

## Primary evidence and limits

Reviewed client.dll SHA256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. GOB source SHA256 remains catalog3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103. Original Monolith.ltp combat_calculations.pyc SHA256 `ce5f422592eb8efe4747b2e3ceca2dd15be76106273ad47b6b32e750a01afa88` was read with the existing Python2.3 marshal reader; GetDamageReductionValue and the four typed DOT functions were checked against their original code/constant/name objects. Owned evidence: Temp/mxo-a1-original-pyc-20261112.json.

- combat_calculations.GetDamageReductionValue198–229 supplies the level/armor curve. Original interlock/combat_resolution.calcDamageForLoser selects toughness plus melee/ranged toughness; combat_free_attacks does the same. These prove the ordinary callers, not a typed health-modifier caller.
- Native6258d830 narrows attacker/defender levels to unsigned8 and calls the original Python function with four integer arguments; wrapper6258db50 calls it at6258dba6. A bounded shipped-image direct-call search found only that Python bridge caller. The complete server-side resistance caller and character-attribute input construction remain unrecovered.
- Health damage is **target108**, not107. Direct selection requires explicit on-use WhenEnabled2, target108 and explicit DamageType0/1/2/7. These types are melee/ballistic/viral/thrown and consume180/181/157/1033 respectively. Heal3, IS5, other/omitted types, target107 and self health writers are excluded. Multiple conflicting direct health component types are refused rather than choosing a first component.
- DOT metadata targets1157 (DOTPlaceHolder). It must store explicit matching DamageType and the exact known updater PhysicalDOT/RangedDOT/ViralDOT/ThrownDOT. Original tempmods896–972 calls dealAbilityDamage with the corresponding type. The native dealAbilityDamage Python member is absent from the shipped client image, so its full server math is not claimed recovered. Unknown updater/type stays unchanged.
- The existing caster influence path625ec512–625ec58a remains applied once. Each DOT carries its resolved component type through Mod→Active; captured overrides inherit a type only when the catalog target/proc component is unambiguous. Replacement keeps the new instance's type. Resistance is read again when each tick lands.

## Exact OURS formula

Let `raw` be the existing positive damage **after** the caster's existing influence, and `t` be current capped target144 plus the selected typed stat. Player values use existing AbilityStats.Player totals/caps; NPCs use the existing timed totals. No new attribute base, linked percent1140/1144/1146/1148 interpretation or stat baseline is added.

```
R(t) = clamp((defenderLevel-attackerLevel)*0.2
       + min(0.33, 0.66/(defenderLevel*4+20)*(t+20)-0.33), -0.9, 0.9)
final = clamp(floor(raw * (1-R(t))/(1-R(0))), 0, 65535)
```

`t=0` returns the exact existing raw damage (ushort domain). Nonpositive raw returns0. The script clamps keep the denominator at least0.1. Negative resistance can increase damage; the final ushort clamp bounds it. There is no invented minimum1. At equal level50, raw100 becomes82 for t75,117 for t-75,52 for t240, and a sufficiently strong reduction can round raw1 to0.

**Applying and normalizing this curve here is OURS.** It preserves the server's existing approximate zero-modifier damage instead of silently installing a new character baseline. The native complete input/caller, final retail magnitude and linked percent stat families remain unresolved. Ordinary interlock/free attack/gunfire already have their own resistance and never call this helper; no double resistance, heal, IS drain, self cost or health cap/regen path is added. Direct NPC/PvP damage reads target totals before applying that cast's new debuffs; DOT reads each landing. Existing ability message fields report final damage and raw-minus-final where that message already existed; no new opcode is invented.

## Actual inventory scope

The initial stored scan found72 resistance modifiers across42 current player rows; this is a potential contributor inventory, not42 fully proven closures. Viral157 contributors:8,20,47,48,68,319,352,360,473,479,1077,1088. Thrown1033:5,20,68,319,1088. FortifySimulacra20 is pet-only, and an unsupported producer/executor is not made executable merely by this shared helper. Linked percent contributors remain separate gaps. The current ignored all-module bundle classifies5/8 PARTIAL, never DONE; historical tracked CSV/JSON/MD snapshots remain exact master by policy.

## Deterministic diagnostic fixture

Discovered `typed-resistance` starts CSR110 at Mara23828/13984, level50, org1, HP5000/IS2000; QAEnemy is org2/flagged, same level/build. Abilities2,17,1006,1112,5,8,330,1135,474 are learned50/loaded. No GM or generic callback/combat/hit RNG override. The mode enables only `MXO_TYPED_RESISTANCE_QA=1` and `MXO_TYPED_RESISTANCE_QA_DAMAGE_DRAW=65`.

That control replaces only1135's existing fallback integer draw in50..80 inclusive, only when dev is exactly1 and **actual Store.config database** is a valid nonempty `mxotest_` name. Environment DB spoof cannot enable it. Explicit supplied RNG in the pure helper wins; ordinary AbilityDamage callers default to no QA opt-in. Other abilities/domains stay untouched. The stored1135 base30 versus existing generic50..80 fallback is an independent unresolved magnitude; this PR does not claim to fix it.

Spy Style1112 learned50 contributes29% thrown influence. Thus raw draw65 becomes83 before target resistance, then68 under5. The native record is kind15/parent1112/target1034/value29, not passive kind45. Viral330 uses the existing fixed captured424 and becomes348 under8. The paired measurements use the same actual caster stance/modifiers.474 needs no forced roll: a single infection keeps its source total while8 naturally expires, allowing early mitigated and later zero-modifier ticks to be compared.

## Claude-only one-pass native check

1. Verify exact head/build-info/fixture-plan and start mode `typed-resistance` through the normal exclusive workflow. In CSR110 Chat use `?pvpflag on`. QAEnemy is idle and already prepared org2/flagged. Keep Chat's damage lines visible. No global health hold is used; compare damage message values rather than HP deltas across regeneration.
2. Have QAEnemy use normal330 on CSR110 via existing helper request: `{"actor":"QAEnemy","action":"cast","ability":330,"target":"CSR_Tester110"}`. Observe ordinary2s casting,424 baseline damage. After its4s reuse, activate Empty Mind8 on self from Actions; verify+75 viral buff20s, request330 again before expiry, expect348 damage and76 resisted. Do not add helper attacks/interlock rounds between paired measurements.
3. After8 expires, request normal1135 on CSR110. Its ordinary required Spy Style auto-switch must show the actual kind15 +29 record; rawdraw65 therefore gives83 baseline. Wait out any real control/reuse, activate Calm Mind, Calm Body5 from Actions and request1135 again within its20s, expect68 damage and15 resisted. Same helper stance and modifiers must remain in effect; no workaround removes legitimate influence. The fresh normal request succeeded without a fabricated daze or interlock gate. If native Actions imposes another actual prerequisite, record it rather than bypassing it.
4. When8's real60s reuse is ready, activate8 and request474 once. Observe its typed damage ticks under the buff, the natural buff-off record at20s, and later ticks from the **same** infection after resistance disappears. No second random infection is used for that comparison. Confirm expiry/death/control UI stays healthy.

Requests are placed only by Claude in the fixture's existing requests directory with unique filenames; response/sent files and packet logs are evidence. No worker launches the client or writes broker/helper requests.

## Validation status

Fresh run3 PASS72.5s/exit0/parallel1: actual330424→348,113583→68, actual kind15/1034/29 record and unchanged caster stance, then one474 DOT before/after natural8 expiry. Permanent stdout/exit: Temp/mxo-a1-typed-fresh-run3-20261112.txt. Full successful runtime log was not retained before harness disposal; no missing-log inference is used. The fixture now copies its own runtime log on future successful runs. Run1(38.3s) incorrectly expected thrown65 after influence; run2(37.4s) incorrectly asserted passive45/value28. Both fixture failures are retained; runtime was unchanged for run3. Unit tests cover mathematical zero/negative/cap/rounding bounds, real5/8, self/heal/107 exclusions, live target cap/clock expiry, per-component DOT/replacement, ordinary Toughness unchanged, strict QA domain/DB/explicit RNG behavior. Native QA remains pending Claude; no retail or native PASS is claimed here.

Reproduce primary inventory/slices/original pyc objects read-only: `python research/abilities/inspect_typed_resistance.py <client.dll> <Monolith.ltp> <mxo-research/tools/client-data>`. Generated JSON belongs in ignored artifacts or owned Temp. Full premerge Release suite:4185 passed; subsequent mode/NPC regression additions are covered by focused final validation.
