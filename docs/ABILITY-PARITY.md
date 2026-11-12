# Player ability parity audit

This is a conservative audit of the shipped 7.6005 templates against the executable server on this branch. **No ability is declared retail-complete merely because it sends an animation or a modifier record.** Counts: 561 player-loadable candidates and their granted abilities (536 memory/discipline candidates, 25 additional grants); 36 cosmetic, 22 missing, 503 partial. No full-parity `done` claims yet.

## Inventory and evidence

`data/abilityIDs.csv` contains 9,972 data rows, including unnamed engine IDs and repeated aliases at +4096/+8192. Its named rows resolve to **1,404 canonical shipped templates**. `data/ability_catalog.json` retains those templates, their component names, stored attributes and original stored AbilityID (171 differ from the template index). It omits absent engine defaults rather than inventing zeroes. GOB SHA-256: `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

The main table includes every non-NPC class-0 template with a positive inventory memory cost or a discipline flag, plus the transitive closure of its grants. This intentionally retains legacy, GM and ambiguous discipline candidates; it does not claim the retail UI exposed all of them. Access is tagged below. The canonical appendix accounts for every remaining template, including NPC, item, state and stat records. CSV aliases normalize to the canonical ID for execution. Friendly names come from the community name table; GOIDs, not suggestive prerequisite component names, determine dependency links.

Tree is the stored discipline parent, or the nearest prerequisite discipline where none is stored. Archetype follows prerequisite GOIDs, with category fallback. A shared ability can be usable by several builds. Each modifier's raw stored numbers are **inputs to native value/duration functions**, not asserted final retail damage or healing. Actual captured values take precedence in the server.

Read alongside [`abilities.py`](../research/client-scripts/abilities.py), [`tempmods.py`](../research/client-scripts/tempmods.py), [`combat.py`](../research/client-scripts/combat.py), [`combat_calculations.py`](../research/client-scripts/combat_calculations.py) and the linked `ability/` callbacks. `augments.py` and `ability/soldier/old_sm_*` are legacy code not imported by `abilities.GetImportObjects`; they are not silently treated as the active rules. `discovery`, `abilitylib`, `combatlib`, constants and console-variable defaults remain native dependencies. Callback links identify the decompiled definitions, not proof that the server implements them.

## Status meanings and shared gaps

- **done**: all identified behavior and relevant gameplay paths validated. None assigned yet.
- **partial**: some real effects execute; a listed behavior, scaling, target rule, condition or native presentation remains incomplete.
- **missing**: intended effect lacks an executor; an animation, cost or generic damage fallback does not count.
- **cosmetic**: discipline/passive loads or displays, but its native passive/stat behavior is not fully applied.

The new catalog fallback supplies explicit costs, cast times, recast/family timers and timed controls, preserving retail overrides. Insufficient IS and incapacitated/powerless casts are rejected. Root blocks movement and jumps; stun/mez blocks actions; powerless blocks abilities; disarm blocks weapon fire; enrage blocks withdrawal. Pacify and scripted roots break as their health callbacks specify. A negative chance-root proc protects its first health change (including healing), then flips positive for later damage checks, as in the script. A control state cannot stack through its immunity timer.

Hacker area damage and controls reach visible NPCs on the same floor. DOTs on secondary targets keep ticking without replacing the current fight; kills share death claiming, XP, mission and loot handling. The merged PvP paths also use action gates, combat-stat bonuses and health-triggered control breaks. LOS, full hit/deflection tests, cone shapes, multi-NPC combat AI, resistance-derived ability damage and some PvP target rules remain incomplete.

Loaded passives now evaluate the deterministic native value functions: learned ability level (2), character level (3), and fixed ranges. Scale uses the stored binary32 value, x87 multiplication/truncation, min offset and positive max cap. Owned loadouts, upgrades, new-world entry and Death Effect updates rebuild totals and retail-style `80 bc 45 00` records; obsolete unconditional Hyper-Jump/Write Code records are removed. Shared grants contribute once. Vitals, regeneration, combat accuracy/defence/resistance bonuses, damage influence, control durations and crafting use these totals. Control duration bonuses follow the stored class-4 state links (stun, root, pacify, blind, powerless, confuse) and keep fractional seconds. Immunity timers and break rules retain their separate definitions. Percent bonuses apply to the server's existing base formulas; those bases, character-attribute links, several stat families, complete stealth/detection attributes, Determination mitigation and special callbacks are still incomplete. Stealth state/visibility/detection now has an executor; see [STEALTH.md](STEALTH.md) for sourced behavior and choices. A passive record is not proof that every consuming system is implemented. See [native modifier evidence and remaining limits](ABILITY-MODIFIERS.md).

Friendly direct/group heals and HOTs use each shipped template's deterministic base and the caster's healing influence, replacing the target-quarter-max-health placeholder. HOT uses its stored total and the script tick formula. Simulacra repairs heal only the caster's own simulacrum; player targets are refused. Mobius Code retains its explicit 10% target-max heal; its 1% immunity proc is still missing. These are sourced base values, with native influence multiplication/truncation; exact final retail amounts still require native comparison with a known caster build and complete attribute contributions. Fixed stored self/friendly buffs gain records/family expiry; captured values win. Deterministic numeric timed buffs using character-level function3 resolve from the caster at application, with active snapshots shared by stat/vital consumers and exact removal records. Script/aura/state/random effects and indefinite style scaling stay excluded; [scaled-buff evidence](ABILITY-SCALED-BUFFS.md) lists native comparison points. Restore RSI325, Revitalize326, Revive375 and Rejuvenate416 now use dead-friendly targets, the native81 88 consent exchange and their script-defined resource restoration. Refusal, timeout, wrong-ability replies, replies without a live offer and reconstruction are rejected; the native short reply carries no per-offer nonce. Exact script-specific resurrection motion and native dialog acceptance remain unverified; see [resurrection evidence](ABILITY-RESURRECTION.md). Coder simulacra (Remote Proxy, Logic/Patch Daemon) spawn as owned NPC_BASE_SIMULACRA views with follow/assist AI, the client's orders, death and lifetime, plus the own-simulacrum abilities and Daemon Boost; see [CODER-PETS.md](CODER-PETS.md) for what is retail evidence and what is chosen.

Combat styles (Self Defense, Aikido, Karate, Kung-Fu, Hacker/Coder/Spy Style) become active through `3e`. The active style's while-enabled modifiers join the passive totals, and its ILDB exchange set drives the interlock moves all viewers see. `CombatSpecialMove` abilities need their stored stance: a loaded style is switched to, an unloaded one refuses the move. Thrown DOTs, Berserker/Side Roll caster buffs, Taunt aggro and the Exile weapons have executors. Their inferences and gaps (RandomCombatStateChance, weakness by NPC name, special-move choreography) are in [COMBAT-STYLES.md](COMBAT-STYLES.md). `43` MartialArt is unreachable in 7.6005.

`AbilityEffects`, `Virii`, `TempMods` and other modules contain script-derived pure helpers for corrosion, disables and immunity, but helpers without live handler wiring remain missing/partial in this audit.

## Per-ability table

Access `memory` means the stored memory cost is positive; `discipline` means its discipline flag is set; `grant` means reachable through a player candidate. A grant can also carry memory metadata. Values are raw unless explicitly described as captured.

| ID / ability | Access | Archetype / tree | Shipped behavior / callbacks | Server behavior | Status |
|---|---|---|---|---|---|
| 1 Adrenaline Booster | memory | Awakened / Awakened | Ballistic Damage Resistance raw ? × native function 3 / scale 1.5 (20.0s) | IS 20 (stored); reuse 60s; buff records + caster-level stat snapshot; combat resistance | partial |
| 2 Awakened | discipline | Awakened / Awakened | grant Self Defense [17]; Stealth Detection raw ? × native function 2 / scale 0.25 (while enabled); [UpdateCombatStyle](../research/client-scripts/tempmods.py#L65) | loaded passive totals + records (@50: Stealth Detection +12 [stealth detection]); load/unload/startup grants 17 | partial |
| 5 Calm Mind, Calm Body | memory | Awakened / Awakened | Thrown Resistance raw ? × native function 3 / scale 1.5 (20.0s) | IS 20 (stored); reuse 60s; buff records + caster-level stat snapshot; numeric totals only; gameplay consumer missing | cosmetic |
| 6 Consistent Technique | memory | Awakened / Awakened | Melee Damage Resistance raw ? × native function 3 / scale 1.5 (20.0s) | IS 20 (capture); reuse 60s; buff records + selected combat/vital stats (capture) | partial |
| 7 Detect Vulnerability | memory | Awakened / Awakened | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor | missing |
| 8 Empty Mind | memory | Awakened / Awakened | Viral Resistance raw ? × native function 3 / scale 1.5 (20.0s) | IS 20 (stored); reuse 60s; buff records + caster-level stat snapshot; numeric totals only; gameplay consumer missing | cosmetic |
| 9 Energized Attacks | memory | Awakened / Awakened | [EnergizedAttacksInit](../research/client-scripts/tempmods.py#L1202), [EnergizedAttacksTerm](../research/client-scripts/tempmods.py#L1208), [EnergizedAttacks_Test](../research/client-scripts/ability/general.py#L180), [PlayEnergizedFX](../research/client-scripts/tempmods.py#L760) | script tactical toggle; precise/energized combat attack and damage multipliers; mutual refusal | partial |
| 10 Hyper-Dodge | memory | Awakened / Awakened | Ballistic Defense raw ? × native function 2 / scale 0.96 (while enabled) | IS 20 (capture); buff records + selected combat/vital stats (capture) | partial |
| 11 Hyper-Jump | memory | Awakened / Awakened | Super Jump raw ? × native function 2 / scale 3.0 (while enabled); hyperjump movement / rank-dependent range | IS 20 (stored); loaded passive totals + records (@50: Super Jump +150 [total only]); jump trajectory + control gate; rank/limits incomplete | partial |
| 12 Hyper-Speed | memory | Awakened / Awakened | Movement Speed raw ? × native function 2 / scale 0.6 (600.0s); Sneak Movement raw ? × native function 2 / scale 0.2 (600.0s); Invisibility Movement raw ? × native function 2 / scale 0.2 (600.0s); [HyperSpeedInit](../research/client-scripts/tempmods.py#L1700), [HyperSpeedTerm](../research/client-scripts/tempmods.py#L1706) | IS 40 (capture); buff records + selected combat/vital stats (capture) | partial |
| 13 Hyper-Strength | memory | Awakened / Awakened | Melee Damage raw ? × native function 2 / scale 2.0 (while enabled); [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995), [PlayHyperStrengthFX](../research/client-scripts/tempmods.py#L1353) | IS 25 (stored); reuse 10s; buff records + learned-level stat snapshot; damage; IS drain 25/5s; shortfall deactivates | partial |
| 14 Power Shot | memory | Awakened / Awakened | Current Inner Strength raw 10..100 × native function 4 / scale 1.0 (instant); physical raw 10..100 (instant) | reuse 60s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 15 Precise Blow | memory | Awakened / Awakened | [PerciseBlowInit](../research/client-scripts/tempmods.py#L1214), [PerciseBlowTerm](../research/client-scripts/tempmods.py#L1220), [PlayPreciseFX](../research/client-scripts/tempmods.py#L767), [PreciseBlow_Test](../research/client-scripts/ability/general.py#L167) | script tactical toggle; precise/energized combat attack and damage multipliers; mutual refusal | partial |
| 16 Punishing Blows | memory | Awakened / Awakened | Damage Against Vulnerability raw 15 (while enabled); [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | buff records + learned-level stat snapshot; numeric totals only; gameplay consumer missing; IS drain 1/1s; shortfall deactivates | cosmetic |
| 17 Self Defense | grant, grant | Generic combat / Awakened | Melee Accuracy raw ? × native function 2 / scale 1.0 (while enabled); Melee Damage raw 4..? × native function 2 / scale 0.42 (while enabled); Melee Defense raw ? × native function 2 / scale 0.96 (while enabled); Ballistic Accuracy raw ? × native function 2 / scale 1.0 (while enabled); Ballistic Damage raw 2..? × native function 2 / scale 0.2 (while enabled); Ballistic Defense raw ? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw ? × native function 2 / scale 0.96 (while enabled); Viral Defense raw ? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 1..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Melee Damage +24, Melee Defense +47, Melee Accuracy +50, Ballistic Damage +12, Ballistic Defense +47, Ballistic Accuracy +50, Thrown Defense +47, Viral Defense +47, Contested Withdraw +65); style interlock exchanges | partial |
| 18 Coder | memory | Coder / Coder | Native action / passive metadata; no decoded stored modifier | IS 0 (stored); reuse 30s | cosmetic |
| 19 Drain Code | memory | Coder / Code Shaper | health effect raw 8000 (instant); Current Inner Strength raw 50 (instant); [Decoy1_Test](../research/client-scripts/ability/hacker/simulacra.py#L41) | IS 0 (stored); reuse 58s; generic hit 50..80 / CSV value; scaling unresolved; own simulacrum only; 8000 to the simulacrum, +50 caster IS; DecoyDuration 60s unexplained | partial |
| 20 Fortify Simulacra 1.0 | memory | Coder / Code Shaper | Melee Damage Resistance raw ? × native function 3 / scale 2.0 (600.0s); Ballistic Damage Resistance raw ? × native function 3 / scale 2.0 (600.0s); Viral Resistance raw ? × native function 3 / scale 2.0 (600.0s); Thrown Resistance raw ? × native function 3 / scale 2.0 (600.0s); Max Health raw ? × native function 3 / scale 15.0 (600.0s) | IS 30 (stored); reuse 8s; own simulacrum only; Max Health +level x15 for 600s; resistances recorded only | partial |
| 21 Lockdown 1.0 | memory | Coder / Proxy Coder | Rooted (12.0s) | IS 10 (stored); reuse 13.5s; Rooted 12s | partial |
| 22 Logic Daemon 1.0 | memory | Coder / Code Shaper | [LogicDaemon1_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L82), [LogicDaemon1_Subject](../research/client-scripts/ability/hacker/simulacra.py#L72) | IS 50 (stored); reuse 10s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 4-7; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); Nuker sheet | partial |
| 23 Deflect Code | memory | Coder / Code Shaper | Determination Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Determination Bonus +10 [total only]) | cosmetic |
| 24 Minor Repairs | memory | Coder / Coder | Consumable Compile Time Reduction raw 33 (while enabled) | loaded passive totals + records (@50: Consumable Compile Time Reduction +33 [total only]) | cosmetic |
| 25 Power Boost 1.0 | memory | Coder / Proxy Coder | Ballistic Damage raw ? × native function 3 / scale 0.5 (300.0s); Melee Damage raw ? × native function 3 / scale 0.5 (300.0s); Force Combat raw 40 (300.0s) | IS 50 (stored); reuse 8s | missing |
| 26 Proxy Coder | memory | Coder / Proxy Coder | health effect raw 13000 (instant) | IS 40 (stored); reuse 13.5s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 27 Remote Proxy 1.0 | memory | Coder / Coder | [RemoteProxy1_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L193), [RemoteProxy1_Subject](../research/client-scripts/ability/hacker/simulacra.py#L180) | IS 50 (stored); reuse 8s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 1-4; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); random proxy sheet | partial |
| 28 Remote Proxy 2.0 | memory | Coder / Code Shaper | [RemoteProxy2_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L212), [RemoteProxy2_Subject](../research/client-scripts/ability/hacker/simulacra.py#L198) | IS 50 (stored); reuse 8s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 4-7; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); random proxy sheet | partial |
| 29 Remote Proxy 3.0 | memory | Coder / Proxy Coder | [RemoteProxy3_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L230), [RemoteProxy3_Subject](../research/client-scripts/ability/hacker/simulacra.py#L217) | IS 50 (stored); reuse 8s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 8-17; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); random proxy sheet | partial |
| 30 Repair Simulacra 1.0 | memory | Coder / Coder | heal raw 20 (instant) | IS 20 (stored); reuse 13s; heals own simulacrum: stored base; caster healing influence; player cast refused | partial |
| 31 Repair Simulacra 2.0 | memory | Coder / Code Shaper | heal raw 34 (instant) | IS 13 (stored); reuse 28s; heals own simulacrum: stored base; caster healing influence; player cast refused | partial |
| 32 Run Program | memory | Coder / Proxy Coder | Viral Accuracy Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Viral Accuracy Bonus +10 [total only]) | cosmetic |
| 33 Static Blast 1.0 | memory | Coder / Proxy Coder | viral raw 22 (instant) | IS 15 (stored); reuse 6s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 34 Detonate Proxy | memory | Coder / Code Shaper | ISDrain Target - this string should never be seen. raw 0 (0.5s); [DetonateProxyEnd](../research/client-scripts/tempmods.py#L2357), [DetonateProxyProc](../research/client-scripts/tempmods.py#L2338), [StopProxy1_Test](../research/client-scripts/ability/hacker/simulacra.py#L52) | IS 30 (stored); reuse 60s; own simulacrum only; DetonateProxyProc blast clamp(health, max/10, max/4) to NPCs within 8m, then 13000 to the simulacrum | partial |
| 35 Code Shaper | memory | Coder / Code Shaper | [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308) | IS 0 (stored); reuse 5s | cosmetic |
| 36 Tinkering | memory | Coder / Coder | viral raw 4 (instant) | IS 20 (stored); reuse 30s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 37 Write Code | memory | Coder / Coder | Code Writing raw ?..15 × native function 2 (while enabled); grant Write Code [970]; [TurnOffStealthAbils](../research/client-scripts/tempmods.py#L2528), [WriteCode_IsFragmentKnowable](../research/client-scripts/clhooks.py#L24) | IS 0 (stored); loaded passive totals + records (@50: Code Writing +15 [total only]); load/unload/startup grants 970 | partial |
| 38 Area Disruption | memory | Hacker / Pathogenist | Stun (3.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 14 (stored); reuse 43s; Stun 3s | partial |
| 39 Bolster Health 1.0 | memory | Hacker / Patcher | Max Health raw 50 (600.0s) | IS 25 (stored); reuse 8s; buff records + selected combat/vital stats (stored constants) | partial |
| 40 Code Freeze 1.0 | memory | Hacker / Virologist | Rooted (25.0s); Force Combat raw -20 (native duration); [OnHealthReduceDeactivateAbility](../research/client-scripts/tempmods.py#L1045) | IS 18 (stored); reuse 58s / group 713 20s; Rooted 25s / break Damage | partial |
| 41 Code Freeze 2.0 | memory | Hacker / Network Hacker | Rooted (19.0s); Force Combat raw -20 (19.0s); [OnHealthReduceDeactivateChance](../research/client-scripts/tempmods.py#L1055) | IS 20 (stored); reuse 43s / group 713 20s; Rooted 19s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 42 Combat Aura 1.0 | memory | Hacker / Team Patcher | ISDrain Target - this string should never be seen. raw 12 (while enabled); ISDrain Target - this string should never be seen. raw 695 (while enabled); [AddRecastTimer](../research/client-scripts/tempmods.py#L1247), [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [DoNothing](../research/client-scripts/tempmods.py#L1299), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 8 (stored); reuse 15s; toggled aura: IS drain 12/5s, pulses 695 on friendly team players in its effect radius | partial |
| 43 Disrupt Inputs 1.0 | memory | Hacker / Selective Phage | blinded (16.0s) | IS 10 (stored); reuse 43s; blinded 16s | partial |
| 44 Download Mission Map | memory | Hacker / Hacker | [DownloadMissionMap_Deactivate](../research/client-scripts/ability/hacker/misc.py#L77) | rshooks.py roll on mission accept, access flags stored, success FX; the client record that shows the map is not identified; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md) | partial |
| 45 Execute Program | memory | Hacker / Hacker | Concentration Bonus raw ? × native function 2 / scale 0.1 (while enabled); Inner Strength Regen Rate Bonus raw ? × native function 2 / scale 0.2 (while enabled); Max Inner Strength Bonus raw ? × native function 2 / scale 0.2 (while enabled); Evade Interlock Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Inner Strength Regen Rate Bonus +10 [regeneration], Max Inner Strength Bonus +10 [maximum vital], Evade Interlock Bonus +5 [total only], Concentration Bonus +5 [total only]) | partial |
| 46 Fast Healing 1.0 | memory | Hacker / Guardian Patcher | Health Regen Rate raw 40 (600.0s) | IS 35 (stored); reuse 13s; buff records + selected combat/vital stats (stored constants) | partial |
| 47 Freeze Area 1.0 | memory | Hacker / Network Hacker | Pacify (6.0s); Viral Resistance raw -30 (12.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 32 (stored); reuse 58s; Pacify 6s / break Damage | partial |
| 48 Freeze System 1.0 | memory | Hacker / Network Hacker | Pacify (6.0s); Viral Resistance raw -20 (12.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 26 (stored); reuse 43s; Pacify 6s / break Damage | partial |
| 49 Gaussian Blur | memory | Hacker / Hacker | Combat Evasion raw 25 (600.0s); [ImpartBlurInit](../research/client-scripts/tempmods.py#L1190), [ImpartBlurTerm](../research/client-scripts/tempmods.py#L1196) | IS 20 (stored); reuse 8s; Gaussian Blur kBlur0x40 self/watchers/late views; stored Combat Evasion+25 has no engagement consumer | cosmetic |
| 50 Group Repairs 1.0 | memory | Hacker / Team Patcher | heal raw 11 (instant) | IS 14 (stored); reuse 4s / group 686 4s; target/team heal; stored base 11; caster healing influence | partial |
| 51 Guardian Patcher | memory | Hacker / Guardian Patcher | Max Health Bonus raw 1 (while enabled); grant Guardian Patcher Upgrade [1094] | IS 0 (stored); loaded passive totals + records (@50: Max Health Bonus +1 [maximum vital]); load/unload/startup grants 1094 | partial |
| 52 Hacker | memory | Hacker / Hacker | Computer Hacking raw ? × native function 2 / scale 4.0 (while enabled) | loaded passive totals + records (@50: Computer Hacking +200 [total only]) | cosmetic |
| 53 Harmful Code 1.0 | memory | Hacker / Patcher | viral raw 12 (instant) | IS 18 (stored); reuse 8s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 54 Restore Health 2.0 | memory | Hacker / Doctor | heal raw 37 (instant) | IS 18 (stored); reuse 4s / group 685 4s; target/team heal; stored base 37; caster healing influence | partial |
| 55 Patcher | memory | Hacker / Patcher | heal raw 21 (instant); Melee Accuracy raw -17 (10.0s); Ballistic Accuracy raw -17 (10.0s); Thrown Accuracy raw -17 (10.0s); Viral Accuracy raw -17 (10.0s) | IS 22 (stored); reuse 43s; target/team heal; stored base 21; caster healing influence; buff records + selected combat/vital stats (capture) | partial |
| 56 Restore Group 1.0 | memory | Hacker / Patcher | heal raw 8 (instant) | IS 12 (stored); reuse 4s / group 685 4s; target/team heal; stored base 8; caster healing influence | partial |
| 57 Logic Barrage 1.0 | memory | Hacker / Hacker | viral raw 8 (instant) | IS 5 (stored); reuse 4s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 58 Logic Blast 1.0 | memory | Hacker / Virologist | viral raw 11 (instant) | IS 6 (stored); reuse 4s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 59 Logic Barrage 2.0 | memory | Hacker / Selective Phage | viral raw 17 (instant) | IS 7 (stored); reuse 4s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 60 Code Nuke 1.0 | memory | Hacker / Pathogenist | viral raw 7 (instant) | IS 11 (stored); reuse 2s / group 682 2s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 61 Miasma 1.0 | memory | Hacker / Pathogenist | ISDrain Target - this string should never be seen. raw 3 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 5 (stored); reuse 28s; toggled aura: IS drain 3/5s, pulses 701 on hostile NPC/PvP targets in its effect radius | partial |
| 62 Movement Accelerator 1.0 | memory | Hacker / Patcher | Movement Speed raw 10 (600.0s) | IS 20 (stored); reuse 8s; buff records + selected combat/vital stats (stored constants) | partial |
| 63 Network Firewall | memory | Hacker / Team Patcher | Viral Defense raw ?..35 × native function 3 / scale 0.96 (300.0s); Thrown Defense raw 5 (300.0s); Ballistic Defense raw 5 (300.0s); Melee Defense raw 5 (300.0s); ISDrain Target - this string should never be seen. raw 10 (300.0s); [RefundIS](../research/client-scripts/tempmods.py#L3085) | IS 50 (stored); buff records + selected combat/vital stats (stored scripted); term refunds 10 IS to recipient | partial |
| 64 Network Hacker | memory | Hacker / Network Hacker | Viral Accuracy Bonus raw 1 (while enabled); grant Network Hacker Upgrade [1078] | IS 0 (stored); loaded passive totals + records (@50: Viral Accuracy Bonus +1 [total only]); load/unload/startup grants 1078 | partial |
| 65 Overheat 1.0 | memory | Hacker / Network Hacker | Ballistic Damage Resistance raw -24 (33.0s); Melee Damage Resistance raw -24 (33.0s) | IS 15 (stored); reuse 58s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 66 Overload 1.0 | memory | Hacker / Selective Phage | Melee Accuracy raw -10 (33.0s); Ballistic Accuracy raw -10 (33.0s); Melee Defense raw -10 (33.0s); Ballistic Defense raw -10 (33.0s) | IS 14 (stored); reuse 43s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 67 Pathogenist | memory | Hacker / Pathogenist | grant Pathogenist Upgrade [1085] | IS 0 (stored); load/unload/startup grants 1085 | partial |
| 68 Personal Firewall 1.0 | memory | Hacker / Guardian Patcher | Viral Resistance raw 40 (300.0s); Melee Damage Resistance raw 10 (300.0s); Thrown Resistance raw 10 (300.0s); Ballistic Damage Resistance raw 10 (300.0s); ISDrain Target - this string should never be seen. raw 75 (300.0s); [GenericConstHealthRegenEvent](../research/client-scripts/tempmods.py#L88), [PersonalFirewallInit](../research/client-scripts/tempmods.py#L1122), [PersonalFirewallTerm](../research/client-scripts/tempmods.py#L1128) | IS 40 (stored); reuse 13s; buff records + selected combat/vital stats (stored scripted); armor bit0x100; term heals 75 (death cleanup guarded) | partial |
| 69 Processor Lag 1.0 | memory | Hacker / Selective Phage | ViralDOT raw total 30 (30.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 10 (stored); reuse 43s; stored total/interval DOT executor | partial |
| 70 Quick Recovery | memory | Hacker / Guardian Patcher | Max Health Bonus raw ? × native function 2 / scale 0.48 (while enabled); Sweep Bonus raw ? × native function 2 / scale 0.2 (while enabled) | IS 0 (stored); loaded passive totals + records (@50: Sweep Bonus +10 [total only], Max Health Bonus +23 [maximum vital]) | partial |
| 71 Repair and Upgrade | memory | Hacker / Patcher | Healing Bonus raw ? × native function 2 / scale 0.1 (while enabled); Sweep Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Sweep Bonus +5 [total only], Healing Bonus +5 [healing]) | partial |
| 72 Resist Combat | memory | Hacker / Team Patcher | Combat Evasion raw 20 (600.0s) | IS 40 (capture); reuse 13s; buff records + selected combat/vital stats (capture) | partial |
| 73 Resist Contagion | memory | Hacker / Network Hacker | Viral Accuracy Bonus raw ? × native function 2 / scale 0.28 (while enabled); Stun Duration Bonus raw ? × native function 2 / scale 0.1 (while enabled) | IS 0 (stored); loaded passive totals + records (@50: Stun Duration Bonus +5 [control duration], Viral Accuracy Bonus +14 [total only]) | partial |
| 74 Resist Damage | memory | Hacker / Team Patcher | Heal and Upgrade Area Radius raw ? × native function 2 / scale 0.481 (while enabled); Sweep Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Sweep Bonus +5 [total only], Heal and Upgrade Area Radius +24 [total only]) | cosmetic |
| 75 Resist Infection | memory | Hacker / Pathogenist | Debuff Area Radius Bonus raw ? × native function 2 / scale 0.5 (while enabled) | loaded passive totals + records (@50: Debuff Area Radius Bonus +25 [total only]) | cosmetic |
| 76 Resist Viruses | memory | Hacker / Selective Phage | Evade Interlock Bonus raw ? × native function 2 / scale 0.14 (while enabled); Contested Withdraw Bonus raw ? × native function 2 / scale 0.24 (while enabled); Viral Defense Bonus raw ? × native function 2 / scale 0.24 (while enabled) | loaded passive totals + records (@50: Contested Withdraw Bonus +11 [total only], Viral Defense Bonus +11 [total only], Evade Interlock Bonus +7 [total only]) | cosmetic |
| 77 Repair RSI 1.0 | memory | Hacker / Hacker | heal raw 9 (instant) | IS 10 (stored); reuse 4s / group 686 4s; target/team heal; stored base 9; caster healing influence | partial |
| 80 Restore Health 1.0 | memory | Hacker / Patcher | heal raw 13 (instant) | IS 11 (stored); reuse 4s / group 685 4s; target/team heal; stored base 13; caster healing influence | partial |
| 83 Selective Phage | memory | Hacker / Selective Phage | Contested Withdraw Bonus raw 1 (while enabled); Evade Interlock Bonus raw 1 (while enabled); Viral Defense Bonus raw 1 (while enabled); grant Selective Phage Upgrade [1082] | IS 0 (stored); loaded passive totals + records (@50: Evade Interlock Bonus +1 [total only], Viral Defense Bonus +1 [total only], Contested Withdraw Bonus +1 [total only]); load/unload/startup grants 1082 | partial |
| 86 Slow | memory | Hacker / Virologist | Movement Speed raw -50 (28.0s); Ballistic Accuracy raw -19 (28.0s); Sneak Movement raw -50 (28.0s); Invisibility Movement raw -50 (28.0s); Force Combat raw -15 (28.0s) | IS 12 (stored); reuse 8s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 89 Static Field | memory | Hacker / Pathogenist | ISDrain Target - this string should never be seen. raw 12 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 11 (stored); reuse 5s; toggled aura: IS drain 12/5s, pulses 694 on hostile NPC/PvP targets in its effect radius | partial |
| 92 Team Patcher | memory | Hacker / Team Patcher | grant Team Patcher Upgrade [1089] | load/unload/startup grants 1089 | partial |
| 95 Transmit Virus | memory | Hacker / Virologist | Viral Damage Bonus raw ? × native function 2 / scale 0.3 (while enabled); Viral Resistance Bonus raw ? × native function 2 / scale 0.3 (while enabled) | loaded passive totals + records (@50: Viral Resistance Bonus +15 [total only], Viral Damage Bonus +15 [damage]) | partial |
| 97 UI Lag 1.0 | memory | Hacker / Virologist | Viral Accuracy raw -10 (27.0s); Thrown Accuracy raw -10 (27.0s); Viral Defense raw -10 (27.0s); Thrown Defense raw -10 (27.0s) | IS 12 (capture); reuse 58s; captured hit 0..0; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 98 Virologist | memory | Hacker / Virologist | Contested Withdraw Bonus raw 1 (while enabled); Viral Defense Bonus raw 1 (while enabled); grant Virologist Upgrade [1077] | loaded passive totals + records (@50: Viral Defense Bonus +1 [total only], Contested Withdraw Bonus +1 [total only]); load/unload/startup grants 1077 | partial |
| 101 Aikido | memory | Martial Artist / Martial Arts Initiate | Melee Accuracy raw ? × native function 2 / scale 1.3 (while enabled); Melee Damage raw 4..? × native function 2 / scale 0.459 (while enabled); Melee Defense raw 10..? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw 10..? × native function 2 / scale 0.96 (while enabled); Viral Defense raw 10..? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw 10..? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 10..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Melee Accuracy +64, Melee Damage +26, Melee Defense +57, Ballistic Defense +57, Thrown Defense +57, Viral Defense +57, Contested Withdraw +74); style interlock exchanges | partial |
| 105 Body Shot | memory | Operative / Soldier | ballistic raw 8 (instant); Melee Damage Resistance raw -5 (10.0s); Ballistic Damage Resistance raw -5 (10.0s) | IS 20 (stored); reuse 8s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Rifles (auto-switch when loaded) | partial |
| 107 Flash Bomb | memory | Operative / Spy / Cutthroat | blinded (12.0s); Dazed (12.0s), 15% proc; Staggered (12.0s), 15% proc; Off-Balance (12.0s), 15% proc | IS 25 (stored); reuse 58s / group 1216 3s; blinded 12s, Dazed 12s, Off-Balance 12s, Staggered 12s | partial |
| 110 Combat Evasion | memory | Operative / Spy / Spy | Combat Evasion raw 20 (300.0s) | IS 30 (stored); reuse 15s; buff records + selected combat/vital stats (stored constants) | partial |
| 113 Combat Insight | memory | Operative / Operative | Force Combat Bonus raw ? × native function 3 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Force Combat Bonus +10 [total only]) | cosmetic |
| 116 Combat Toughness | memory | Operative / Soldier | Melee Resistance Bonus raw ? × native function 2 / scale 0.15 (while enabled); Ballistic Resistance Bonus raw ? × native function 2 / scale 0.15 (while enabled) | loaded passive totals + records (@50: Ballistic Resistance Bonus +7 [combat resistance], Melee Resistance Bonus +7 [combat resistance]) | partial |
| 118 Cutthroat | memory | Operative / Spy / Cutthroat | grant Cutthroat Upgrade [1119] | load/unload/startup grants 1119 | partial |
| 119 Disarm Traps | memory | Operative / Spy / Infiltrator | [DisarmTraps_DirectObject](../research/client-scripts/ability/spy/misc.py#L436), [DisarmTraps_Test](../research/client-scripts/ability/spy/misc.py#L446) | Disarm Traps on a followed TrappedState object (DisarmTraps_Test contest, lines 0x7600024e/f/0x76000250, Disarmed/Triggered state); trap damage and Detect Traps absent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md); IS 10 (stored) | partial |
| 120 Disguise | memory | Operative / Spy / Infiltrator | grant Disguise [1114]; Contested Withdraw Bonus raw ? × native function 2 / scale 0.5 (while enabled); Sneak Movement Speed Bonus raw ? × native function 2 / scale 0.5 (while enabled) | loaded passive totals + records (@50: Contested Withdraw Bonus +25 [total only], Sneak Movement Speed Bonus +25 [total only]); load/unload/startup grants 1114 | partial |
| 121 Side Roll Escape | memory | Operative / Spy / Spy | Combat Evasion raw 20 (30.0s) | IS 15 (stored); buff records + selected combat/vital stats (stored constants); on landing: caster Combat Evasion buff, then leaves the fight (TestFunction 6 inferred) | partial |
| 122 Back Roll Escape | memory | Operative / Spy / Assassin | Enraged (10.0s) | IS 25 (stored); Enraged 10s | partial |
| 123 Find Weakness | memory | Operative / Spy / Spy | Thrown Damage raw ? × native function 3 / scale 0.5 (20.0s) | IS 20 (stored); reuse 90s; buff records + caster-level stat snapshot; damage | partial |
| 124 Firearms Skill | memory | Operative / Soldier | Ballistic Damage raw ? × native function 3 / scale 0.5 (20.0s) | IS 20 (stored); reuse 90s; buff records + caster-level stat snapshot; damage | partial |
| 125 Firearms Expert | memory | Gunman / Gunman | Ballistic Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Ballistic Accuracy Bonus +5 [combat accuracy], Ballistic Damage Bonus +5 [damage]) | partial |
| 126 Disarming Shot | memory | Gunman / Gunman | ballistic raw 11 (instant); Ballistic Accuracy raw -15 (10.0s); [PistolDisarmingShot_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L51) | IS 20 (stored); reuse 8s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Handguns (auto-switch when loaded) | partial |
| 128 Gunman | memory | Gunman / Gunman | Initiative Bonus raw 1 (while enabled); grant Gunman Upgrade [1070] | loaded passive totals + records (@50: Initiative Bonus +1 [total only]); load/unload/startup grants 1070 | partial |
| 129 Handguns | memory | Gunman / Gunman | Ballistic Accuracy raw ? × native function 2 / scale 1.3 (while enabled); Ballistic Damage raw 2..? × native function 2 / scale 0.235 (while enabled); Melee Defense raw 8..? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw 8..? × native function 2 / scale 0.96 (while enabled); Viral Defense raw 8..? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw 8..? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 10..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Ballistic Accuracy +64, Ballistic Damage +13, Melee Defense +55, Ballistic Defense +55, Thrown Defense +55, Viral Defense +55, Contested Withdraw +74); weapon-specific interlock mapping unverified | partial |
| 130 Hindering Shot | memory | Operative / Operative | Movement Speed raw -50 (8.0s); ballistic raw 9 (instant); Sneak Movement raw -50 (8.0s); Invisibility Movement raw -50 (8.0s); Force Combat raw -10 (8.0s) | IS 20 (stored); reuse 8s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Handguns (auto-switch when loaded) | partial |
| 131 Infiltrator | memory | Operative / Spy / Infiltrator | grant Infiltrator Upgrade [1115] | load/unload/startup grants 1115 | partial |
| 132 Karate | memory | Martial Artist / Martial Arts Initiate | Melee Accuracy raw ? × native function 2 / scale 1.3 (while enabled); Melee Damage raw 4..? × native function 2 / scale 0.5 (while enabled); Melee Defense raw -2..? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw -2..? × native function 2 / scale 0.96 (while enabled); Viral Defense raw -2..? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw -2..? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 8..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Melee Accuracy +64, Melee Damage +29, Melee Defense +45, Ballistic Defense +45, Thrown Defense +45, Viral Defense +45, Contested Withdraw +72); style interlock exchanges | partial |
| 133 Kung-Fu | memory | Martial Artist / Martial Arts Initiate | Melee Accuracy raw 10..? × native function 2 / scale 1.3 (while enabled); Melee Damage raw 4..? × native function 2 / scale 0.47 (while enabled); Melee Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Viral Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 6..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Melee Accuracy +74, Melee Damage +27, Melee Defense +49, Ballistic Defense +49, Thrown Defense +49, Viral Defense +49, Contested Withdraw +70); style interlock exchanges | partial |
| 134 Leg Sweep | memory | Operative / Spy / Infiltrator | thrown raw 13 (instant); Melee Defense raw -15 (10.0s) | IS 12 (stored); reuse 12s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Spy Style (auto-switch when loaded) | partial |
| 135 Martial Arts Prowess | memory | Operative / Soldier | Melee Damage raw ? × native function 3 / scale 0.5 (20.0s) | IS 20 (capture); reuse 90s / group 687 5s; buff records + selected combat/vital stats (capture) | partial |
| 136 Martial Arts Expertise | memory | Martial Artist / Martial Arts Initiate | Melee Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Melee Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled) | IS 0 (stored); loaded passive totals + records (@50: Melee Accuracy Bonus +5 [combat accuracy], Melee Damage Bonus +5 [damage]) | partial |
| 137 Martial Arts Initiate | memory | Martial Artist / Martial Arts Initiate | Initiative Bonus raw 2 (while enabled); grant Martial Arts Initiate Upgrade [1062] | loaded passive totals + records (@50: Initiative Bonus +2 [total only]); load/unload/startup grants 1062 | partial |
| 138 Guard Breaker | memory | Martial Artist / Martial Arts Initiate | physical raw 36 (instant); Melee Defense raw -10 (15.0s) | IS 20 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 139 Iron Guard | memory | Martial Artist / Martial Arts Initiate | physical raw 12 (instant); Ballistic Defense raw 10 (12.0s); Melee Defense raw 10 (12.0s) | IS 25 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Aikido (auto-switch when loaded) | partial |
| 140 Multi-Fighting | memory | Operative / Soldier | [disableMultiOnOnePenaltyInit](../research/client-scripts/tempmods.py#L70), [disableMultiOnOnePenaltyTerm](../research/client-scripts/tempmods.py#L76) | presentation only; no effect executor | cosmetic |
| 141 Neuro-Dart | memory | Operative / Spy / Cutthroat | Movement Speed raw -50 (15.0s); Melee Accuracy raw -15 (15.0s); ThrownDOT raw total 9 (15.0s); Ballistic Accuracy raw -15 (15.0s); Melee Defense raw -13 (15.0s); Ballistic Defense raw -13 (15.0s); Sneak Movement raw -50 (15.0s); Invisibility Movement raw -50 (15.0s); Force Combat raw -20 (15.0s); [ThrownDOT](../research/client-scripts/tempmods.py#L935) | IS 20 (stored); reuse 18s / group 1215 3s; stored total/interval DOT executor; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 142 Open Locks | memory | Operative / Spy / Infiltrator | Native action / passive metadata; no decoded stored modifier | Pick Lock (rule 4000 DoorPickLock): Open Locks loaded + Lock_Pick/Digital_Camera tool, retail lines 0x7a000141/142/145; door or container moves to the script state; mission `locked="1"` objects; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md) | partial |
| 143 Operative | memory | Operative / Operative | Sitting Health Regen Rate raw 50 (while enabled); grant Operative Upgrade [1060] | loaded passive totals + records (@50: Sitting Health Regen Rate +50 [total only]); load/unload/startup grants 1060 | partial |
| 145 Overhand Smash | memory | Operative / Soldier | physical raw 7 (instant); Melee Accuracy raw -10 (10.0s); Enraged (10.0s), 10% proc; Melee Defense raw -10 (10.0s) | IS 20 (stored); reuse 12s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 10s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Karate (auto-switch when loaded) | partial |
| 146 Poison Knife | memory | Operative / Spy / Spy | ThrownDOT raw total 17 (25.0s); [ThrownDOT](../research/client-scripts/tempmods.py#L935) | IS 15 (stored); reuse 20s / group 1216 3s; stored total/interval DOT executor; stored ThrownDOT on the target (script tick) | partial |
| 147 Rifles | memory | Gunman / Gunman | Ballistic Accuracy raw ? × native function 2 / scale 1.3 (while enabled); Ballistic Damage raw 2..? × native function 2 / scale 0.235 (while enabled); Melee Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Viral Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw 2..? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 6..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Ballistic Accuracy +64, Ballistic Damage +13, Melee Defense +49, Ballistic Defense +49, Thrown Defense +49, Viral Defense +49, Contested Withdraw +70); weapon-specific interlock mapping unverified | partial |
| 148 Sharpshooter | memory | Operative / Soldier | [disableFiringInMeleePenInit](../research/client-scripts/tempmods.py#L41), [disableFiringInMeleePenTerm](../research/client-scripts/tempmods.py#L47), [disableFiringMeleeRangePenInit](../research/client-scripts/tempmods.py#L53), [disableFiringMeleeRangePenTerm](../research/client-scripts/tempmods.py#L59) | presentation only; no effect executor | cosmetic |
| 149 Sixth Sense | memory | Operative / Spy / Infiltrator | Sixth Sense Detection raw ? × native function 2 / scale 4.0 (while enabled); [SixthSense_Subject](../research/client-scripts/ability/spy/misc.py#L114), [SixthSense_Test](../research/client-scripts/ability/spy/misc.py#L142) | IS 10 (stored); loaded passive totals + records (@50: Sixth Sense Detection +200 [total only]) | cosmetic |
| 150 Compel Close Combat | memory | Operative / Operative | Force Combat raw 20 (300.0s) | IS 30 (stored); buff records + selected combat/vital stats (stored constants) | partial |
| 151 Sneak | memory | Operative / Spy / Spy | grant Sneak [1111]; Ballistic Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Melee Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Movement Speed Bonus raw ? × native function 2 / scale 0.2 (while enabled); Determination Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Ballistic Defense Bonus +5 [combat defence], Melee Defense Bonus +5 [combat defence], Movement Speed Bonus +10 [total only], Determination Bonus +5 [total only]); load/unload/startup grants 1111; stealth detection/AI incomplete | partial |
| 152 Spinning Backsweep | memory | Operative / Spy / Spy | thrown raw 28 (instant); [SneakAttackSpinBack_Test](../research/client-scripts/ability/spy/sneaks.py#L92) | IS 20 (stored); reuse 30s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 153 Drop Kick | memory | Operative / Spy / Cutthroat | thrown raw 52 (instant); [SneakAttackDropKick_Test](../research/client-scripts/ability/spy/sneaks.py#L100) | IS 20 (stored); reuse 30s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 154 Soldier | memory | Operative / Soldier | grant Soldier Upgrade [1061]; Initiative Bonus raw 2 (while enabled) | loaded passive totals + records (@50: Initiative Bonus +2 [total only]); load/unload/startup grants 1061 | partial |
| 155 Spy | memory | Operative / Spy / Spy | grant Spy Upgrade [1113] | load/unload/startup grants 1113 | partial |
| 156 Stealth Awareness | memory | Operative / Operative | Stealth Detection raw ? × native function 3 / scale 0.3 (300.0s) | reuse 15s; buff records + caster-level stat snapshot; stealth detection | partial |
| 158 Submachine Guns | memory | Gunman / Gunman | Ballistic Accuracy raw ? × native function 2 / scale 1.3 (while enabled); Ballistic Damage raw 2..? × native function 2 / scale 0.235 (while enabled); Melee Defense raw 4..? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw 4..? × native function 2 / scale 0.96 (while enabled); Viral Defense raw 4..? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw 4..? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 8..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Ballistic Accuracy +64, Ballistic Damage +13, Melee Defense +51, Ballistic Defense +51, Thrown Defense +51, Viral Defense +51, Contested Withdraw +72); weapon-specific interlock mapping unverified | partial |
| 161 Uncanny Senses | memory | Operative / Spy / Spy | Sneak Attack Detection raw ? × native function 2 / scale 4.0 (while enabled) | loaded passive totals + records (@50: Sneak Attack Detection +40 [total only]) | cosmetic |
| 164 Weak Points | memory | Operative / Spy / Cutthroat | Thrown Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled); Thrown Accuracy Bonus raw ? × native function 2 / scale 0.2 (while enabled); Ballistic Defense Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Thrown Accuracy Bonus +10 [total only], Ballistic Defense Bonus +10 [combat defence], Thrown Damage Bonus +10 [damage]) | partial |
| 165 Tap Data Node | memory | Awakened / Awakened | Tap Data Node Skill raw ? × native function 2 (while enabled); [TapDataNode_Subject](../research/client-scripts/ability/spy/misc.py#L193) | IS 0 (stored); loaded passive totals + records (@50: Tap Data Node Skill +5 [total only]) | cosmetic |
| 166 Hack Construct Key | memory | Coder / Awakened | Native action / passive metadata; no decoded stored modifier | Hardline Hack Construct (rule 10024) ability/faction gate lines only; construct hacking itself absent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md); IS 75 (stored); reuse 10s | partial |
| 169 Emergency Repairs 1.0 | memory | Hacker / Virologist | HOT raw total 14 (15.0s); [HOT](../research/client-scripts/tempmods.py#L975) | IS 10 (stored); reuse 13s / group 685 4s; target/team heal over time; stored base 14; caster healing influence | partial |
| 170 Passive Code | memory | Hacker / Patcher | Healing raw ? × native function 3 / scale 0.5 (20.0s) | IS 15 (stored); reuse 90s; buff records + caster-level stat snapshot; healing | partial |
| 173 Combat Enhancement 1.0 | memory | Hacker / Guardian Patcher | Melee Accuracy raw ?..26 × native function 3 / scale 1.3 (300.0s); Ballistic Accuracy raw ?..26 × native function 3 / scale 1.3 (300.0s); Thrown Accuracy raw ?..26 × native function 3 / scale 1.3 (300.0s) | IS 40 (stored); reuse 13s; buff records + caster-level stat snapshot; combat accuracy | partial |
| 175 Invisibility | memory | Operative / Spy / Shadow | grant Invisibility [1116]; Invis Movement Speed Bonus raw ? × native function 2 / scale 0.5 (while enabled) | loaded passive totals + records (@50: Invis Movement Speed Bonus +25 [total only]); load/unload/startup grants 1116 | partial |
| 176 Covering Fire | memory | Gunman / Gunman | ballistic raw 17 (instant); [EnableRootWithChance](../research/client-scripts/tempmods.py#L1226) | IS 25 (stored); reuse 8s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; Rooted 8s / break Chance; special move: needs Submachine Guns (auto-switch when loaded) | partial |
| 184 Hyper-Sprint | memory | Awakened / Awakened | Movement Speed raw 15 (while enabled); Sneak Movement raw 15 (while enabled); Invisibility Movement raw 15 (while enabled); [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995), [SpeedingInit](../research/client-scripts/tempmods.py#L1082), [SpeedingTerm](../research/client-scripts/tempmods.py#L1088) | IS 25 (capture); reuse 5s; buff records + selected combat/vital stats (capture) | partial |
| 185 Intensify Damage | memory | Operative / Operative | Melee Damage raw 1 (15.0s); Ballistic Damage raw 1 (15.0s) | IS 15 (stored); reuse 20s; buff records + selected combat/vital stats (stored constants) | partial |
| 186 Improved Sneak | memory | Operative / Spy / Operative | Native action / passive metadata; no decoded stored modifier | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md) | partial |
| 196 Ignore Pain | memory | Awakened / Awakened | Melee Damage Resistance raw 5 (10.0s); Ballistic Damage Resistance raw 5 (10.0s); [OnHealthReduceHealProc](../research/client-scripts/tempmods.py#L1068) | IS 25 (stored); reuse 30s | missing |
| 197 Head Butt | memory | Awakened / Awakened | Melee Accuracy raw -10 (20.0s); physical raw 16 (instant) | IS 12 (capture); reuse 30s; captured hit 21..45; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Self Defense (auto-switch when loaded) | partial |
| 198 Cheap Shot | memory | Awakened / Awakened | physical raw 7 (instant) | IS 20 (capture); reuse 20s; captured hit 21..67; special move: needs Self Defense (auto-switch when loaded) | partial |
| 207 Wooden Stake Attack | memory | Legacy / special weapon / Shared / no stored parent | [WoodenStakeAttack_DirectObject](../research/client-scripts/ability/general.py#L63) | general.py damage 10, x10 vs NPCKilledOnlyByWood (NPC weakness by name: server policy) | partial |
| 208 Silver Bullet Attack | memory | Legacy / special weapon / Shared / no stored parent | [SilverBulletAttack_DirectObject](../research/client-scripts/ability/general.py#L81) | general.py damage 10, x10 vs NPCKilledOnlyBySilver (NPC weakness by name: server policy) | partial |
| 215 Locate Data Node | memory | Awakened / Awakened | Locate Data Node Skill raw ? × native function 2 (while enabled); [LocateDataNode_DirectObject](../research/client-scripts/ability/spy/misc.py#L208) | IS 5 (stored); loaded passive totals + records (@50: Locate Data Node Skill +5 [total only]) | cosmetic |
| 217 Evade Combat | memory | Awakened / Awakened | Evading (while enabled); [SetEvadeCombatTimer](../research/client-scripts/tempmods.py#L1275) | IS 40 (capture); reuse 30s; buff records + selected combat/vital stats (capture) | partial |
| 218 Disable Security Devices | memory | Operative / Spy / Shadow | Native action / passive metadata; no decoded stored modifier | Disable Security Devices use (rule 1012 SecurityPBXDisable): ability + DisableDeviceTool, Security Systems vs Difficulty contest, ACCESS GRANTED/ALARM chat, Disabled/Alarming state; no XP, alarm has no effect; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md); IS 0 (stored) | partial |
| 233 Clear Head | memory | Hacker / Doctor | Healing Bonus raw ? × native function 2 / scale 0.1 (while enabled); Determination Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Determination Bonus +10 [total only], Healing Bonus +5 [healing]) | partial |
| 234 Repair RSI 2.0 | memory | Hacker / Guardian Patcher | heal raw 20 (instant) | IS 15 (stored); reuse 4s / group 686 4s; target/team heal; stored base 20; caster healing influence | partial |
| 235 Repair RSI 3.0 | memory | Hacker / Doctor | heal raw 48 (instant) | IS 22 (stored); reuse 4s / group 686 4s; target/team heal; stored base 48; caster healing influence | partial |
| 236 Doctor | memory | Hacker / Doctor | Contested Withdraw Bonus raw 2 (while enabled); grant Doctor Upgrade [1097] | loaded passive totals + records (@50: Contested Withdraw Bonus +2 [total only]); load/unload/startup grants 1097 | partial |
| 237 Fast Healing 2.0 | memory | Hacker / Doctor | Health Regen Rate raw 60 (600.0s) | IS 45 (stored); reuse 13s; buff records + selected combat/vital stats (stored constants) | partial |
| 238 Harmful Code 2.0 | memory | Hacker / Doctor | viral raw 33 (instant) | IS 30 (stored); reuse 8s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 239 Dual Handguns | memory | Gunman / Gunslinger | Melee Resistance Bonus raw ? × native function 2 / scale 0.125 (while enabled); Ballistic Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.05 (while enabled) | loaded passive totals + records (@50: Ballistic Accuracy Bonus +5 [combat accuracy], Ballistic Damage Bonus +2 [damage], Melee Resistance Bonus +6 [combat resistance]) | partial |
| 240 Gunslinger | memory | Gunman / Gunslinger | Initiative Bonus raw 2 (while enabled); Ballistic Damage Bonus raw 1 (while enabled); grant Gunslinger Upgrade [1075] | IS 20 (stored); loaded passive totals + records (@50: Ballistic Damage Bonus +1 [damage], Initiative Bonus +2 [total only]); load/unload/startup grants 1075 | partial |
| 241 Pistol Slide | memory | Gunman / Gunslinger | ballistic raw 67 (instant); Powerless (8.0s), 30% proc; [PistolSlide_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L82) | IS 20 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 8s; special move: needs Handguns (auto-switch when loaded) | partial |
| 242 Pistol Evasive | memory | Gunman / Gunslinger | Thrown Defense raw 20 (12.0s); ballistic raw 5 (instant); Ballistic Defense raw 20 (12.0s); Melee Defense raw 20 (12.0s); [PistolEvasive_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L59), [PistolEvasive_Subject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L66) | IS 30 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Handguns (auto-switch when loaded) | partial |
| 243 Pistol Aerial | memory | Gunman / Gunslinger | ballistic raw 16 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659), [PistolAerial_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L22) | IS 20 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Stun 4s; special move: needs Handguns (auto-switch when loaded) | partial |
| 244 Pistol Barrage | memory | Gunman / Gunslinger | ballistic raw 18 (instant); Powerless (8.0s), 40% proc; [PistolBarrage_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L29) | IS 30 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 8s; special move: needs Handguns (auto-switch when loaded) | partial |
| 245 Scattershot | memory | Gunman / Scattershot | Initiative Bonus raw 1 (while enabled); grant Scattershot Upgrade [1071] | loaded passive totals + records (@50: Initiative Bonus +1 [total only]); load/unload/startup grants 1071 | partial |
| 246 Dual Submachine Guns | memory | Gunman / Scattershot | Melee Resistance Bonus raw ? × native function 2 / scale 0.125 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.125 (while enabled) | loaded passive totals + records (@50: Ballistic Damage Bonus +6 [damage], Melee Resistance Bonus +6 [combat resistance]) | partial |
| 247 Bullet Spray | memory | Gunman / Scattershot | ballistic raw 25 (instant); Enraged (8.0s), 15% proc | IS 30 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 8s; special move: needs Submachine Guns (auto-switch when loaded) | partial |
| 248 Suppression Fire | memory | Gunman / Scattershot | Rooted (12.0s); ballistic raw 15 (instant) | IS 35 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Rooted 21.12s; special move: needs Submachine Guns (auto-switch when loaded) | partial |
| 249 Sliding Volley | memory | Gunman / Scattershot | ballistic raw 77 (instant); Enraged (10.0s), 20% proc | IS 15 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 10s; special move: needs Submachine Guns (auto-switch when loaded) | partial |
| 250 Controlled Burst | memory | Gunman / Scattershot | ballistic raw 26 (instant); Enraged (8.0s), 40% proc | IS 30 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 8s; special move: needs Submachine Guns (auto-switch when loaded) | partial |
| 251 Rifleman | memory | Gunman / Rifleman | Ballistic Resistance Bonus raw 1 (while enabled); grant Rifleman Upgrade [1073] | loaded passive totals + records (@50: Ballistic Resistance Bonus +1 [combat resistance]); load/unload/startup grants 1073 | partial |
| 252 Advanced Riflery | memory | Gunman / Rifleman | Ballistic Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.125 (while enabled) | loaded passive totals + records (@50: Ballistic Damage Bonus +6 [damage], Ballistic Accuracy Bonus +5 [combat accuracy]) | partial |
| 253 Wounding Shot | memory | Gunman / Rifleman | ballistic raw 6 (instant); NeutralDOT raw total 7 (10.0s); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659), [NeutralDOT](../research/client-scripts/tempmods.py#L961) | IS 20 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; stored total/interval DOT executor; special move: needs Rifles (auto-switch when loaded) | partial |
| 254 Immobilizing Shot | memory | Gunman / Rifleman | ballistic raw 18 (instant); Force Combat raw -15 (8.0s); [EnableRootWithChance](../research/client-scripts/tempmods.py#L1226) | IS 30 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Rooted 8s / break Chance; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Rifles (auto-switch when loaded) | partial |
| 255 Disabling Shot | memory | Gunman / Rifleman | Powerless (4.0s), 50% proc; ballistic raw 16 (instant) | IS 20 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 4s; special move: needs Rifles (auto-switch when loaded) | partial |
| 256 Three Round Burst | memory | Gunman / Rifleman | ballistic raw 80 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 20 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Rifles (auto-switch when loaded) | partial |
| 257 Kung Fu Master | memory | Martial Artist / Kung Fu Master | grant Kung Fu Master Upgrade [1063]; Initiative Bonus raw 1 (while enabled); Melee Damage Bonus raw 1 (while enabled) | loaded passive totals + records (@50: Initiative Bonus +1 [total only], Melee Damage Bonus +1 [damage]); load/unload/startup grants 1063 | partial |
| 258 Kung Fu Proficiency | memory | Martial Artist / Kung Fu Master | Melee Damage Bonus raw ? × native function 2 / scale 0.075 (while enabled); Melee Accuracy Bonus raw ? × native function 2 / scale 0.15 (while enabled); Max Health Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Max Health Bonus +5 [maximum vital], Melee Accuracy Bonus +7 [combat accuracy], Melee Damage Bonus +3 [damage]) | partial |
| 259 Dim Mak Strike | memory | Martial Artist / Kung Fu Master | physical raw 71 (instant); Powerless (6.0s) | IS 20 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 6s; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 260 Misdirect Punch | memory | Martial Artist / Kung Fu Master | physical raw 24 (instant); blinded (4.0s) | IS 30 (capture); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; blinded 4s; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 261 Machinegun Fist Combo | memory | Martial Artist / Kung Fu Master | physical raw 23 (instant); [RemoveRandomBuff](../research/client-scripts/tempmods.py#L342) | IS 30 (capture); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; instant script: removable buffs (UBF0) / current IS on landing; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 262 Piston Kicks | memory | Martial Artist / Kung Fu Master | physical raw 108 (instant); Stun (4.0s), 40% proc | IS 25 (capture); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Stun 4s; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 263 Karate Master | memory | Martial Artist / Karate Master | Initiative Bonus raw 1 (while enabled); grant Karate Master Upgrade [1065]; Melee Accuracy Bonus raw 1 (while enabled) | loaded passive totals + records (@50: Melee Accuracy Bonus +1 [combat accuracy], Initiative Bonus +1 [total only]); load/unload/startup grants 1065 | partial |
| 264 Karate Expertise | memory | Martial Artist / Karate Master | Melee Damage Bonus raw ? × native function 2 / scale 0.075 (while enabled); Melee Accuracy Bonus raw ? × native function 2 / scale 0.15 (while enabled); Max Health Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Max Health Bonus +5 [maximum vital], Melee Accuracy Bonus +7 [combat accuracy], Melee Damage Bonus +3 [damage]) | partial |
| 265 Ki-Charged Punch | memory | Martial Artist / Karate Master | physical raw 68 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 30 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Karate (auto-switch when loaded) | partial |
| 266 Sidekick Combo | memory | Martial Artist / Karate Master | physical raw 28 (instant); Enraged (8.0s), 20% proc | IS 25 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 8s; special move: needs Karate (auto-switch when loaded) | partial |
| 267 Sky-High Sidekick | memory | Martial Artist / Karate Master | physical raw 21 (instant); Ballistic Damage Resistance raw -40 (12.0s); Melee Damage Resistance raw -40 (12.0s) | IS 30 (capture); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Karate (auto-switch when loaded) | partial |
| 268 Swirling Ki Summon | memory | Martial Artist / Karate Master | physical raw 2 (instant); Melee Accuracy raw -60 (12.0s); Ballistic Accuracy raw -60 (12.0s) | IS 60 (stored); reuse 12s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Karate (auto-switch when loaded) | partial |
| 269 Aikido Prowess | memory | Martial Artist / Aikido Master | Melee Damage Bonus raw ? × native function 2 / scale 0.075 (while enabled); Melee Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Max Health Bonus raw ? × native function 3 / scale 0.3 (while enabled) | loaded passive totals + records (@50: Melee Accuracy Bonus +5 [combat accuracy], Max Health Bonus +15 [maximum vital], Melee Damage Bonus +3 [damage]) | partial |
| 270 Aikido Master | memory | Martial Artist / Aikido Master | Initiative Bonus raw 2 (while enabled); grant Aikido Master Upgrade [1067]; Viral Defense Bonus raw 1 (while enabled) | loaded passive totals + records (@50: Initiative Bonus +2 [total only], Viral Defense Bonus +1 [total only]); load/unload/startup grants 1067 | partial |
| 271 Iron Body | memory | Martial Artist / Aikido Master | Ballistic Damage Resistance raw 40 (20.0s); Melee Damage Resistance raw 40 (20.0s) | IS 50 (stored); reuse 28s; buff records + selected combat/vital stats (capture) | partial |
| 272 Serene Calm | memory | Martial Artist / Aikido Master | Melee Defense raw ? × native function 3 / scale 0.5 (40.0s); Ballistic Defense raw ? × native function 3 / scale 0.5 (40.0s); Viral Defense raw ? × native function 3 / scale 0.5 (40.0s); Thrown Defense raw ? × native function 3 / scale 0.5 (40.0s) | IS 50 (stored); reuse 180s; buff records + selected combat/vital stats (capture) | partial |
| 273 Counter Throw | memory | Martial Artist / Aikido Master | physical raw 56 (instant); Confuse (4.0s) | IS 15 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Confuse 4s; special move: needs Aikido (auto-switch when loaded) | partial |
| 274 Maki-Otoshi | memory | Martial Artist / Aikido Master | physical raw 30 (instant); Melee Accuracy raw -60 (16.0s) | IS 30 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Aikido (auto-switch when loaded) | partial |
| 275 Programmer | memory | Coder / Programmer | Crafting Cost Discount raw 10 (while enabled) | loaded passive totals + records (@50: Crafting Cost Discount +10 [crafting]) | partial |
| 276 Decompile Items | memory | Coder / Programmer | Decompile Skill raw ? × native function 2 (while enabled) | reuse 10s; loaded passive totals + records (@50: Decompile Skill +50 [crafting]) | partial |
| 277 Compile Items | memory | Coder / Code Crafter | Compile Skill raw ? × native function 2 (while enabled); grant Compile Items [969] | loaded passive totals + records (@50: Compile Skill +50 [crafting]); load/unload/startup grants 969 | partial |
| 278 Wicked Aim | memory | Operative / Spy / Knife Thrower | Thrown Damage Bonus raw ? × native function 2 / scale 0.3 (while enabled); Thrown Accuracy Bonus raw ? × native function 2 / scale 0.3 (while enabled); Root Duration Bonus raw ? × native function 2 / scale 0.2 (while enabled); Pacify Duration Bonus raw ? × native function 2 / scale 0.24 (while enabled) | loaded passive totals + records (@50: Thrown Damage Bonus +15 [damage], Pacify Duration Bonus +11 [control duration], Root Duration Bonus +10 [control duration], Thrown Accuracy Bonus +15 [total only]) | partial |
| 279 Deadly Throw | memory | Operative / Spy / Knife Thrower | Movement Speed raw -50 (15.0s); thrown raw 21..? (instant); Sneak Movement raw -50 (15.0s); Invisibility Movement raw -50 (15.0s); Dazed (8.0s), 20% proc; Staggered (8.0s), 20% proc; Force Combat raw -15 (15.0s); [DealDamageIfOffBalance](../research/client-scripts/tempmods.py#L2374) | IS 18 (stored); reuse 10s / group 1216 3s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Spy Style (auto-switch when loaded) | partial |
| 280 Crippling Throw | memory | Operative / Spy / Knife Thrower | Rooted (15.0s); thrown raw 1 (instant); ISDrain Target - this string should never be seen. raw -100 (15.0s); Force Combat raw -40 (15.0s); [OnHealthReduceDeactivateChance](../research/client-scripts/tempmods.py#L1055) | IS 20 (stored); reuse 10s / group 1215 3s; generic hit 50..80 / CSV value; scaling unresolved; Rooted 15s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Spy Style (auto-switch when loaded) | partial |
| 281 Wounding Throw | memory | Operative / Spy / Shadow | ThrownDOT raw total 61 (50.0s); [ThrownDOT](../research/client-scripts/tempmods.py#L935) | IS 20 (stored); reuse 10s / group 1215 3s; stored total/interval DOT executor; special move: needs Spy Style (auto-switch when loaded); stored ThrownDOT on the target (script tick) | partial |
| 282 Blinding Throw | memory | Operative / Spy / Assassin | blinded (10.0s); ThrownDOT raw total 24 (15.0s); [DealDamageIfOffBalance](../research/client-scripts/tempmods.py#L2374), [ThrownDOT](../research/client-scripts/tempmods.py#L935) | IS 25 (stored); reuse 8s / group 1215 3s; stored total/interval DOT executor; blinded 10s; special move: needs Spy Style (auto-switch when loaded) | partial |
| 283 Knife Thrower | memory | Operative / Spy / Knife Thrower | grant Knife Thrower Upgrade [1120] | load/unload/startup grants 1120 | partial |
| 284 Improved Sneak Attack | memory | Operative / Spy / Assassin | Powerless Duration Bonus raw ? × native function 2 / scale 0.5 (while enabled); Confuse Duration Bonus raw ? × native function 2 / scale 0.5 (while enabled); Pacify Duration Bonus raw ? × native function 2 / scale 0.26 (while enabled) | loaded passive totals + records (@50: Confuse Duration Bonus +25 [control duration], Pacify Duration Bonus +12 [control duration], Powerless Duration Bonus +25 [control duration]); stealth detection/AI incomplete | partial |
| 285 Assassin | memory | Operative / Spy / Assassin | grant Assassin Upgrade [1122] | load/unload/startup grants 1122 | partial |
| 286 Spin Throw | memory | Operative / Spy / Assassin | thrown raw 80 (instant); [SneakAttackSpinThrow_Test](../research/client-scripts/ability/spy/sneaks.py#L116) | IS 50 (stored); reuse 38s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 287 Suplex | memory | Operative / Spy / Knife Thrower | Dazed (15.0s); Staggered (15.0s); Off-Balance (15.0s); thrown raw 17..120 (instant); [SneakAttackSuplex_Test](../research/client-scripts/ability/spy/sneaks.py#L124) | IS 50 (stored); reuse 38s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Off-Balance 15s, Staggered 15s, Dazed 15s; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 288 Topsy Turvy | memory | Operative / Spy / Assassin | thrown raw 37 (instant); Confuse (10.0s); [SneakAttackTopsyTurvy_Test](../research/client-scripts/ability/spy/sneaks.py#L132) | IS 60 (stored); reuse 38s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Confuse 10s; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 289 Punt | memory | Operative / Spy / Master Assassin | thrown raw 20..? (instant); [SneakAttackPunt_Test](../research/client-scripts/ability/spy/sneaks.py#L108) | IS 40 (stored); reuse 38s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 290 Shadow | memory | Operative / Spy / Shadow | grant Shadow Upgrade [1117] | load/unload/startup grants 1117 | partial |
| 291 Desperate Speed | memory | Operative / Spy / Shadow | Movement Speed raw 40 (15.0s); Sneak Movement raw 20 (15.0s); Invisibility Movement raw 20 (15.0s) | IS 50 (stored); reuse 298s; buff records + selected combat/vital stats (stored constants) | partial |
| 292 Cartwheel Escape | memory | Operative / Spy / Infiltrator | Confuse (4.0s); Combat Evasion raw 25 (30.0s) | IS 15 (stored); reuse 30s / group 1200 12s; Confuse 4s | partial |
| 293 Stealth Countermeasures | memory | Operative / Spy / Cutthroat | Stealth Detection raw 80 (30.0s) | IS 140 (stored); reuse 15s; buff records + selected combat/vital stats (stored constants) | partial |
| 294 Shifty Technique | memory | Operative / Spy / Master Assassin | Combat Evasion raw 80 (30.0s) | IS 40 (stored); reuse 118s; buff records + selected combat/vital stats (stored constants) | partial |
| 295 Canny Strike | memory | Operative / Spy / Knife Thrower | thrown raw 8..10 (instant) | IS 70 (stored); reuse 30s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 296 Aikido Spinning Clay Pigeon | memory | Martial Artist / Aikido Master | physical raw 375 (instant); Confuse (8.0s) | IS 40 (stored); reuse 40s; generic hit 50..80 / CSV value; scaling unresolved; Confuse 8s; special move: needs Aikido (auto-switch when loaded) | partial |
| 297 Cryptography | memory | Coder / Programmer | Decryption Skill raw ? × native function 2 (while enabled) | reuse 15s; loaded passive totals + records (@50: Decryption Skill +50 [total only]) | cosmetic |
| 298 Detection | memory | Awakened / Awakened | Concealment Detection raw ? × native function 2 / scale 4.0 (while enabled); Stealth Detection raw ? × native function 2 / scale 0.3 (while enabled); [Detection_DirectObject](../research/client-scripts/ability/general.py#L21), [Detection_Test](../research/client-scripts/ability/general.py#L55) | IS 5 (stored); reuse 10s; loaded passive totals + records (@50: Stealth Detection +15 [stealth detection], Concealment Detection +200 [total only]) | partial |
| 309 Stand Off | memory | Hacker / Arbalest | Concentration Bonus raw ? × native function 2 / scale 0.2 (while enabled); Viral Damage Bonus raw ? × native function 2 / scale 0.08 (while enabled); Inner Strength Cost Modifier Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Viral Damage Bonus +3 [damage], Inner Strength Cost Modifier Bonus +5 [total only], Concentration Bonus +10 [total only]) | partial |
| 310 Arbalest | memory | Hacker / Arbalest | grant Arbalest Upgrade [1083]; Viral Damage Bonus raw 1 (while enabled) | loaded passive totals + records (@50: Viral Damage Bonus +1 [damage]); load/unload/startup grants 1083 | partial |
| 311 Logic Blast 2.0 | memory | Hacker / Arbalest | viral raw 31 (instant) | IS 8 (stored); reuse 4s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 312 Logic Barrage 3.0 | memory | Hacker / Arbalest | viral raw 40 (instant) | IS 10 (stored); reuse 4s / group 683 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 313 Logic Cannon 1.0 | memory | Hacker / Arbalest | viral raw 18 (instant) | IS 18 (stored); reuse 15s / group 684 15s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 314 Upgrade Attacks | memory | Hacker / Arbalest | Viral Accuracy raw ? × native function 3 / scale 1.3 (20.0s) | IS 35 (capture); reuse 90s; buff records + selected combat/vital stats (capture) | partial |
| 315 Stun 1.0 | memory | Hacker / Arbalest | Stun (7.0s); [AddControlImmunityTimerVal](../research/client-scripts/tempmods.py#L1639) | IS 20 (stored); reuse 8s; captured hit 0..0; Stun 7s | partial |
| 317 Potency | memory | Hacker / Upgrade Artist | Melee Resistance Bonus raw ? × native function 2 / scale 0.24 (while enabled); Ballistic Resistance Bonus raw ? × native function 2 / scale 0.24 (while enabled); Concentration Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Ballistic Resistance Bonus +11 [combat resistance], Concentration Bonus +10 [total only], Melee Resistance Bonus +11 [combat resistance]) | partial |
| 318 Upgrade Artist | memory | Hacker / Upgrade Artist | grant Upgrade Artist Upgrade [1095]; Melee Resistance Bonus raw 1 (while enabled); Ballistic Resistance Bonus raw 1 (while enabled) | loaded passive totals + records (@50: Melee Resistance Bonus +1 [combat resistance], Ballistic Resistance Bonus +1 [combat resistance]); load/unload/startup grants 1095 | partial |
| 319 Personal Firewall 2.0 | memory | Hacker / Upgrade Artist | Viral Resistance raw 80 (300.0s); Ballistic Damage Resistance raw 20 (300.0s); Thrown Resistance raw 20 (300.0s); Melee Damage Resistance raw 20 (300.0s); ISDrain Target - this string should never be seen. raw 250 (300.0s); [GenericConstHealthRegenEvent](../research/client-scripts/tempmods.py#L88), [PersonalFirewallInit](../research/client-scripts/tempmods.py#L1122), [PersonalFirewallTerm](../research/client-scripts/tempmods.py#L1128) | IS 45 (stored); reuse 13s; buff records + selected combat/vital stats (stored scripted); armor bit0x100; term heals 250 (death cleanup guarded) | partial |
| 320 Negative Condition Sweep 1.0 | memory | Hacker / Upgrade Artist | [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 15 (stored); reuse 5s; pulse sweep removes cleansable debuff parents (UDB0, not exempt) | partial |
| 321 Efficiency | memory | Hacker / Upgrade Artist | Inner Strength Cost Reduction raw 15 (300.0s) | IS 40 (stored); reuse 8s; buff records + selected combat/vital stats (stored constants) | partial |
| 322 Bolster Health 2.0 | memory | Hacker / Upgrade Artist | Max Health raw 200 (600.0s) | IS 30 (stored); reuse 8s; buff records + selected combat/vital stats (stored constants) | partial |
| 323 Movement Accelerator 2.0 | memory | Hacker / Upgrade Artist | Movement Speed raw 25 (720.0s); Invisibility Movement raw 5 (720.0s); Sneak Movement raw 5 (720.0s) | IS 40 (stored); reuse 8s; buff records + selected combat/vital stats (stored constants) | partial |
| 324 Delete Upgrade 1.0 | memory | Hacker / Upgrade Artist | [RemoveRandomBuff](../research/client-scripts/tempmods.py#L342) | IS 16 (stored); reuse 8s; instant script: removable buffs (UBF0) / current IS on landing | partial |
| 325 Restore RSI | memory | Hacker / Guardian Patcher | [RestoreRSI_DirectObject](../research/client-scripts/ability/hacker/health.py#L11), [RestoreRSI_Test](../research/client-scripts/ability/hacker/health.py#L25) | IS 75 (stored); reuse 120s; dead friendly player + native approval; 1 HP / 1 IS; exact resurrect motion unverified | partial |
| 326 Revitalize RSI | memory | Hacker / Doctor | [RevitalizeRSI_DirectObject](../research/client-scripts/ability/hacker/health.py#L32) | IS 100 (stored); reuse 45s; dead friendly player + native approval; full HP / IS; exact resurrect motion unverified | partial |
| 327 Overclock | memory | Hacker / Virologist | Viral Damage raw ? × native function 3 / scale 0.5 (20.0s) | IS 20 (capture); reuse 90s; buff records + selected combat/vital stats (capture) | partial |
| 328 Code Branching | memory | Hacker / Ballista | Melee Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Melee Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled); Ballistic Accuracy Bonus raw ? × native function 2 / scale 0.2 (while enabled); Viral Resistance Bonus raw 0..50 × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Melee Defense Bonus +5 [combat defence], Ballistic Defense Bonus +5 [combat defence], Melee Damage Bonus +10 [damage], Ballistic Accuracy Bonus +10 [combat accuracy], Viral Resistance Bonus +10 [total only]) | partial |
| 329 Logic Blast 3.0 | memory | Hacker / Ballista | viral raw 51 (instant) | IS 11 (capture); reuse 4s / group 682 4s; captured hit 367..619 | partial |
| 330 Logic Barrage 4.0 | memory | Hacker / Ballista | viral raw 63 (instant) | IS 13 (capture); reuse 4s / group 683 4s; captured hit 424..424 | partial |
| 331 Logic Cannon 2.0 | memory | Hacker / Ballista | viral raw 29 (instant) | IS 27 (stored); reuse 15s / group 684 15s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 332 Emergency Repairs 2.0 | memory | Hacker / Ballista | HOT raw total 76 (50.0s); [HOT](../research/client-scripts/tempmods.py#L975) | IS 25 (stored); reuse 300s / group 685 4s; target/team heal over time; stored base 76; caster healing influence | partial |
| 333 Stun 2.0 | memory | Hacker / Ballista | Stun (5.0s); [AddControlImmunityTimerVal](../research/client-scripts/tempmods.py#L1639) | IS 24 (capture); reuse 30s; captured hit 0..0; Stun 5s | partial |
| 334 Ballista | memory | Hacker / Ballista | grant Ballista Upgrade [1084]; Viral Damage Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Viral Damage Bonus +5 [damage]); load/unload/startup grants 1084 | partial |
| 335 Artillerist | memory | Hacker / Artillerist | grant Artillerist Upgrade [1086] | load/unload/startup grants 1086 | partial |
| 336 Logic Bomb 1.0 | memory | Hacker / Artillerist | viral raw 14 (instant) | IS 14 (capture); reuse 2s / group 682 2s; captured hit 266..323 | partial |
| 337 Code Nuke 2.0 | memory | Hacker / Artillerist | viral raw 17 (instant) | IS 18 (capture); reuse 2s / group 683 2s; captured hit 283..346 | partial |
| 338 Master Knife Thrower | memory | Operative / Spy / Master Knife Thrower | Thrown Accuracy Bonus raw 5 (while enabled); grant Master Knife Thrower Upgrade [1121] | loaded passive totals + records (@50: Thrown Accuracy Bonus +5 [total only]); load/unload/startup grants 1121 | partial |
| 339 Deadly Aim | memory | Operative / Spy / Master Knife Thrower | Blind Duration Bonus raw ? × native function 2 / scale 0.5 (while enabled); Enrage Duration Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Enrage Duration Bonus +5 [total only], Blind Duration Bonus +25 [control duration]) | partial |
| 340 Throat Cutting Throw | memory | Operative / Spy / Master Knife Thrower | thrown raw 80 (instant); Dazed (8.0s), 20% proc; Off-Balance (8.0s), 20% proc; [ApplyPowerlessIfStaggered](../research/client-scripts/tempmods.py#L2362) | IS 15 (stored); reuse 8s / group 1215 3s; generic hit 50..80 / CSV value; scaling unresolved; Off-Balance 8s | partial |
| 341 Sever Artery | memory | Operative / Spy / Master Knife Thrower | ThrownDOT raw total 68 (30.0s); Movement Speed raw -50 (30.0s); Sneak Movement raw -50 (30.0s); Invisibility Movement raw -50 (30.0s); ISDrain Target - this string should never be seen. raw 10 (30.0s); Force Combat raw -10 (30.0s); [RandomCombatStateChance](../research/client-scripts/tempmods.py#L2702), [ThrownDOT](../research/client-scripts/tempmods.py#L935) | IS 35 (stored); reuse 15s / group 1216 3s; stored total/interval DOT executor; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; stored ThrownDOT + 30 s slow/ForceCombat debuffs on the target; RandomCombatStateChance missing | partial |
| 343 Miasma 2.0 | memory | Hacker / Artillerist | Melee Defense raw -21 (45.0s); Ballistic Defense raw -21 (45.0s) | IS 14 (stored); reuse 28s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 344 Combat Hacking | memory | Hacker / Artillerist | Melee Defense raw 20 (32.0s); Ballistic Defense raw 20 (32.0s); Inner Strength Cost Reduction raw 15 (32.0s) | IS 20 (capture); reuse 298s; buff records + selected combat/vital stats (capture) | partial |
| 345 Range Booster | memory | Hacker / Artillerist | Virus Area Radius raw 12 (30.0s) | IS 40 (capture); reuse 180s; buff records + selected combat/vital stats (capture) | partial |
| 346 Dedicated Code | memory | Hacker / Artillerist | Inner Strength Cost Modifier Bonus raw ? × native function 2 / scale 0.3 (while enabled); Viral Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Viral Damage Bonus +5 [damage], Inner Strength Cost Modifier Bonus +15 [total only]) | partial |
| 347 Subduing Throw | memory | Operative / Spy / Master Knife Thrower | thrown raw 57 (instant); Off-Balance (8.0s), 20% proc; Staggered (8.0s), 20% proc; [ApplyStunEffectIfDazed](../research/client-scripts/tempmods.py#L2368) | IS 15 (stored); reuse 8s / group 1216 3s; generic hit 50..80 / CSV value; scaling unresolved; Stun 4s | partial |
| 348 Staggering Throw | memory | Operative / Spy / Master Knife Thrower | Staggered (12.0s), 50% proc; thrown raw 35..191 (instant); [StaggeringThrow_Subject](../research/client-scripts/ability/spy/sneaks.py#L202) | IS 45 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Staggered 12s | partial |
| 349 Hostile Programming | memory | Hacker / Vector | Viral Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Max Inner Strength Bonus raw 0..30 × native function 2 / scale 0.3 (while enabled) | loaded passive totals + records (@50: Viral Accuracy Bonus +5 [total only], Max Inner Strength Bonus +15 [maximum vital]) | partial |
| 350 Code Infection 1.0 | memory | Hacker / Vector | ViralDOT raw total 100 (30.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 8 (stored); reuse 12s / group 702 4s; stored total/interval DOT executor | partial |
| 351 Code Infection 2.0 | memory | Hacker / Vector | ViralDOT raw total 129 (30.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 10 (stored); reuse 12s / group 702 4s; stored total/interval DOT executor | partial |
| 352 Freeze System 2.0 | memory | Hacker / Vector | Pacify (6.0s); Viral Resistance raw -48 (12.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 34 (stored); reuse 15s; Pacify 6s / break Damage | partial |
| 353 Infect Area 1.0 | memory | Hacker / Vector | ViralDOT raw total 94 (30.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 15 (stored); reuse 12s; stored total/interval DOT executor | partial |
| 354 Destroy Resolve 1.0 | memory | Hacker / Vector | Inner Strength Regen Rate raw -50 (30.0s); Max Inner Strength raw -58 (30.0s); Current Inner Strength raw -58 (instant) | IS 28 (stored); reuse 120s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; instant script: removable buffs (UBF0) / current IS on landing | partial |
| 355 Vector | memory | Hacker / Vector | Viral Defense raw -35 (30.0s); Powerless (6.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 35 (stored); reuse 60s; Powerless 8.94s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 356 Howitzer | memory | Hacker / Howitzer | grant Howitzer Upgrade [1087]; Debuff Area Radius Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Debuff Area Radius Bonus +5 [total only]); load/unload/startup grants 1087 | partial |
| 357 Logic Bomb 2.0 | memory | Hacker / Howitzer | viral raw 23 (instant) | IS 20 (capture); reuse 2s / group 682 2s; captured hit 305..392 | partial |
| 358 Code Shield | memory | Hacker / Howitzer | Viral Defense Bonus raw ? × native function 2 / scale 0.24 (while enabled); Viral Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Powerless Duration Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Viral Accuracy Bonus +5 [total only], Powerless Duration Bonus +10 [control duration], Viral Defense Bonus +11 [total only]) | partial |
| 359 Code Nuke 3.0 | memory | Hacker / Howitzer | viral raw 25 (instant) | IS 25 (capture); reuse 4s / group 683 4s; captured hit 645..1183 | partial |
| 360 Insidious Code | memory | Hacker / Howitzer | Melee Damage Resistance raw -100 (24.0s); Melee Damage raw -14 (24.0s); Viral Resistance raw -100 (24.0s) | IS 28 (stored); reuse 58s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 361 Fumble Field | memory | Hacker / Howitzer | Ballistic Accuracy raw -27 (35.0s); Thrown Accuracy raw -27 (35.0s) | IS 35 (stored); reuse 58s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 362 White Noise | memory | Hacker / Howitzer | blinded (12.0s) | IS 29 (stored); reuse 58s; blinded 12s | partial |
| 363 Improved Invisibility | memory | Operative / Spy / Master Shadow | Ballistic Defense Bonus raw ? × native function 2 / scale 0.2 (while enabled); Melee Defense Bonus raw ? × native function 2 / scale 0.2 (while enabled); Determination raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Melee Defense Bonus +10 [combat defence], Determination +10 [total only], Ballistic Defense Bonus +10 [combat defence]) | partial |
| 364 Smoke Bomb | memory | Operative / Spy / Master Shadow | blinded (20.0s); [SmokeBombInit](../research/client-scripts/tempmods.py#L1147) | IS 120 (stored); reuse 60s; blinded 20s | partial |
| 365 Ghost Strike | memory | Operative / Spy / Master Shadow | thrown raw 29 (instant); [GhostStrike_Subject](../research/client-scripts/ability/spy/misc.py#L20), [GhostStrike_Test](../research/client-scripts/ability/spy/misc.py#L31) | IS 60 (stored); reuse 10s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 366 Impart Invisibility | memory | Operative / Spy / Master Shadow | [ImpartInvisibilityInit](../research/client-scripts/tempmods.py#L1169), [ImpartInvisibility_Test](../research/client-scripts/ability/spy/misc.py#L365) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md); IS 100 (stored); reuse 10s | partial |
| 367 Master Shadow | memory | Operative / Spy / Master Shadow | Contested Withdraw Bonus raw 5 (while enabled); grant Master Shadow [1118] | loaded passive totals + records (@50: Contested Withdraw Bonus +5 [total only]); load/unload/startup grants 1118 | partial |
| 368 Vanish | memory | Operative / Spy / Master Shadow | [VanishInit](../research/client-scripts/tempmods.py#L1159) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md); IS 30 (stored); reuse 1800s | partial |
| 369 Corpsman | memory | Hacker / Corpsman | grant Corpsman Upgrade [1092]; Buff Area Radius Bonus raw 1 (while enabled) | loaded passive totals + records (@50: Buff Area Radius Bonus +1 [total only]); load/unload/startup grants 1092 | partial |
| 370 Dedication | memory | Hacker / Corpsman | Healing Bonus raw ? × native function 2 / scale 0.1 (while enabled); Sweep Bonus raw ? × native function 2 / scale 0.1 (while enabled); Melee Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Melee Defense Bonus +5 [combat defence], Ballistic Defense Bonus +5 [combat defence], Healing Bonus +5 [healing], Sweep Bonus +5 [total only]) | partial |
| 371 Restore Group 2.0 | memory | Hacker / Corpsman | heal raw 20 (instant) | IS 18 (stored); reuse 4s / group 685 4s; target/team heal; stored base 20; caster healing influence | partial |
| 372 Group Repairs 2.0 | memory | Hacker / Corpsman | heal raw 18 (instant) | IS 21 (stored); reuse 4s / group 686 4s; target/team heal; stored base 18; caster healing influence | partial |
| 373 Combat Training | memory | Hacker / Corpsman | Melee Accuracy raw ? × native function 3 / scale 1.3 (300.0s) | IS 45 (stored); buff records + caster-level stat snapshot; combat accuracy | partial |
| 374 Weapons Boost | memory | Hacker / Corpsman | Ballistic Accuracy raw ? × native function 3 / scale 1.3 (300.0s) | IS 75 (stored); reuse 20s; buff records + caster-level stat snapshot; combat accuracy | partial |
| 375 Revive RSI | memory | Hacker / Corpsman | [ReviveRSI_DirectObject](../research/client-scripts/ability/hacker/health.py#L58) | IS 100 (stored); reuse 30s; dead friendly player + native approval; full HP / IS; exact resurrect motion unverified | partial |
| 376 Advanced Damage Resistance | memory | Hacker / Force Enhancer | Viral Resistance Bonus raw ? × native function 2 / scale 0.24 (while enabled); Melee Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled); Thrown Resistance Bonus raw 0..40 × native function 2 / scale 0.24 (while enabled) | loaded passive totals + records (@50: Viral Resistance Bonus +11 [total only], Melee Damage Bonus +5 [damage], Ballistic Damage Bonus +5 [damage], Thrown Resistance Bonus +11 [total only]) | partial |
| 377 Cancel Upgrade 1.0 | memory | Hacker / Force Enhancer | [RemoveRandomBuff](../research/client-scripts/tempmods.py#L342) | IS 25 (stored); reuse 4s; instant script: removable buffs (UBF0) / current IS on landing | partial |
| 378 Deflect Virus 1.0 | memory | Hacker / Force Enhancer | ISDrain Target - this string should never be seen. raw 7 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 10 (stored); reuse 10s | missing |
| 379 Sweep Party 1.0 | memory | Hacker / Force Enhancer | [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 50 (stored); reuse 30s; pulse sweep removes cleansable debuff parents (UDB0, not exempt) | partial |
| 380 Deflect Bullets 1.0 | memory | Hacker / Force Enhancer | ISDrain Target - this string should never be seen. raw 7 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 10 (stored); reuse 10s | missing |
| 381 Group Heal 1.0 | memory | Hacker / Force Enhancer | heal raw 26 (instant) | IS 20 (stored); reuse 4s; target/team heal; stored base 26; caster healing influence | partial |
| 382 Recall Team Member | memory | Hacker / Force Enhancer | [RecallTeamMember_DirectObject](../research/client-scripts/ability/hacker/misc.py#L17), [RecallTeamMember_Subject](../research/client-scripts/ability/hacker/misc.py#L34), [RecallTeamMember_Test](../research/client-scripts/ability/hacker/misc.py#L41) | Recall Team Member: team member, same district, not fighting/dead; caster/target lines 0x7a00015b/15a, phase-out FX, teleport to caster; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md); IS 30 (stored); reuse 30s | partial |
| 383 Force Enhancer | memory | Hacker / Force Enhancer | Ballistic Damage raw 6 (30.0s); Max Inner Strength raw -187 (30.0s); Ballistic Defense raw 19 (30.0s); Ballistic Accuracy raw 42 (30.0s) | IS 0 (stored); reuse 300s; buff records + selected combat/vital stats (capture) | partial |
| 384 Disrupt Connection | memory | Operative / Spy / Master Assassin | Stun Duration Bonus raw ? × native function 2 / scale 0.3 (while enabled); Contested Withdraw Bonus raw ? × native function 2 / scale 0.2 (while enabled); Enrage Duration Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Contested Withdraw Bonus +10 [total only], Enrage Duration Bonus +5 [total only], Stun Duration Bonus +15 [control duration]) | partial |
| 385 Diving Frankensteiner | memory | Operative / Spy / Master Shadow | thrown raw 164 (instant); [SneakAttackDivingFrank_Test](../research/client-scripts/ability/spy/sneaks.py#L140) | IS 80 (stored); reuse 60s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 386 Freedom to Joints | memory | Operative / Spy / Master Knife Thrower | thrown raw 92 (instant); Powerless (8.0s); [SneakAttackFreeJoints_Test](../research/client-scripts/ability/spy/sneaks.py#L164) | IS 60 (stored); reuse 60s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 8s; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 387 Bull Dog | memory | Operative / Spy / Shadow | thrown raw 75 (instant); [SneakAttackBullDog_Test](../research/client-scripts/ability/spy/sneaks.py#L148) | IS 37 (stored); reuse 60s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 388 Reverse Bull Dog | memory | Operative / Spy / Master Assassin | thrown raw 56 (instant); ISDrain Target - this string should never be seen. raw 16 (0.1s); [AddControlImmunityTimerVal](../research/client-scripts/tempmods.py#L1639), [ApplyStunEffect](../research/client-scripts/tempmods.py#L1647), [SneakAttackRevBullDog_Test](../research/client-scripts/ability/spy/sneaks.py#L156) | IS 60 (stored); reuse 60s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Spy Style (auto-switch when loaded); stealth detection/AI incomplete | partial |
| 389 Master Assassin | memory | Operative / Spy / Master Assassin | grant Master Assassin Upgrade [1123]; Thrown Damage Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Thrown Damage Bonus +5 [damage]); load/unload/startup grants 1123 | partial |
| 390 Skript Kiddie | memory | Hacker / Skript Kiddie | Ballistic Defense Bonus raw 2 (while enabled); grant Skript Kiddie Upgrade [1079]; Melee Defense Bonus raw 2 (while enabled) | loaded passive totals + records (@50: Ballistic Defense Bonus +2 [combat defence], Melee Defense Bonus +2 [combat defence]); load/unload/startup grants 1079 | partial |
| 391 Effective Code | memory | Hacker / Skript Kiddie | Ballistic Defense Bonus raw ? × native function 2 / scale 0.26 (while enabled); Pacify Duration Bonus raw ? × native function 2 / scale 0.3 (while enabled); Melee Defense Bonus raw 0..100 × native function 2 / scale 0.26 (while enabled) | loaded passive totals + records (@50: Ballistic Defense Bonus +12 [combat defence], Pacify Duration Bonus +15 [control duration], Melee Defense Bonus +12 [combat defence]) | partial |
| 392 Code Rot 1.0 | memory | Hacker / Skript Kiddie | Viral Accuracy raw -39 (18.0s) | IS 20 (stored); reuse 30s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 393 Code Stop 1.0 | memory | Hacker / Skript Kiddie | Pacify (6.0s); ISDrain Target - this string should never be seen. raw 0 (12.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 20 (stored); reuse 15s; Pacify 6s / break Damage | partial |
| 394 Overheat 2.0 | memory | Hacker / Skript Kiddie | Ballistic Damage Resistance raw -52 (28.0s); Melee Damage Resistance raw -52 (28.0s) | IS 20 (stored); reuse 60s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 395 Buffer Overflow 1.0 | memory | Hacker / Skript Kiddie | [BufferOverflowInit](../research/client-scripts/tempmods.py#L532), [BufferOverflowTerm](../research/client-scripts/tempmods.py#L538), [BufferOverflowUpdate](../research/client-scripts/tempmods.py#L520) | IS 38 (stored); reuse 30s; Buffer Overflow 1.0 60s; native script buffer reservation min(Value,free); baseline100+4level is OURS; refuses new upgrades until removal/expiry | partial |
| 396 Crash | memory | Hacker / Skript Kiddie | Ballistic Accuracy raw -27 (42.0s); Viral Accuracy raw -27 (42.0s); Movement Speed raw -50 (28.0s); Combat Evasion raw -50 (28.0s); Sneak Movement raw -50 (28.0s); Invisibility Movement raw -50 (28.0s); Force Combat raw -15 (native duration) | IS 25 (capture); reuse 15s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 397 Game Master | memory | Game Master / Game Master | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor | cosmetic |
| 398 Repel Combat | memory | Hacker / Despoiler | Ballistic Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Powerless Duration Bonus raw ? × native function 2 / scale 0.3 (while enabled); Melee Defense Bonus raw 0..50 × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Ballistic Defense Bonus +5 [combat defence], Powerless Duration Bonus +15 [control duration], Melee Defense Bonus +5 [combat defence]) | partial |
| 399 Zone of Powerlessness | memory | Hacker / Despoiler | Powerless (6.0s) | IS 30 (stored); reuse 30s; Powerless 10.14s | partial |
| 400 Plague Zone 1.0 | memory | Hacker / Despoiler | ViralDOT raw total 59 (15.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 15 (stored); reuse 12s; stored total/interval DOT executor | partial |
| 401 Inefficiency Field 1.0 | memory | Hacker / Despoiler | Inner Strength Cost Reduction raw -20 (30.0s); Inner Strength Regen Rate raw -35 (30.0s) | IS 20 (stored); reuse 60s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 402 Bottleneck Field | memory | Hacker / Despoiler | Movement Speed raw -125 (18.0s); Sneak Movement raw -125 (18.0s); Invisibility Movement raw -125 (18.0s); Force Combat raw -25 (18.0s) | IS 23 (stored); reuse 30s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 403 Disruption Field | memory | Hacker / Despoiler | Viral Defense raw -50 (23.0s) | IS 15 (stored); reuse 30s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 404 Despoiler | memory | Hacker / Despoiler | Melee Damage raw -14 (30.0s); Ballistic Damage raw -14 (30.0s); Melee Accuracy raw -39 (30.0s); Ballistic Accuracy raw -39 (30.0s) | IS 40 (stored); reuse 30s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 405 Aikido Redirection | memory | Martial Artist / Aikido Grandmaster | Viral Resistance Bonus raw ? × native function 2 / scale 0.25 (while enabled); Viral Defense Bonus raw ? × native function 2 / scale 0.125 (while enabled); Powerless Duration Bonus raw ? × native function 2 / scale 0.2 (while enabled); Melee Damage Bonus raw ? × native function 2 / scale 0.025 (while enabled); Melee Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Enrage Duration Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Enrage Duration Bonus +5 [total only], Melee Damage Bonus +1 [damage], Melee Defense Bonus +5 [combat defence], Powerless Duration Bonus +10 [control duration], Viral Defense Bonus +6 [total only], Viral Resistance Bonus +12 [total only]) | partial |
| 406 Ki Burst | memory | Martial Artist / Aikido Grandmaster | physical raw 23 (instant); Enraged (15.0s), 20% proc | IS 70 (stored); reuse 15s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 15s | partial |
| 407 Punch Reversal Catch Slam | memory | Martial Artist / Aikido Grandmaster | Powerless (4.0s); physical raw 21 (instant) | IS 40 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 4s; special move: needs Aikido (auto-switch when loaded) | partial |
| 408 Awakened | memory | Legacy / special weapon / Shared / no stored parent | Combat Tactics raw ? × native function 2 / scale 4.0 (while enabled); Viral Defense raw ? × native function 2 / scale 4.0 (while enabled) | loaded passive totals + records (@50: Combat Tactics +200 [combat accuracy], Viral Defense +200 [total only]) | partial |
| 409 Clear Mind | memory | Hacker / Physician | Melee Defense Bonus raw ? × native function 2 / scale 0.2 (while enabled); Healing Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Defense Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Melee Defense Bonus +10 [combat defence], Ballistic Defense Bonus +10 [combat defence], Healing Bonus +5 [healing]) | partial |
| 410 Aikido Grandmaster | memory | Martial Artist / Aikido Grandmaster | Melee Defense Bonus raw 5 (while enabled); Enrage Duration Bonus raw 2 (while enabled); Initiative Bonus raw 1 (while enabled); Melee Resistance Bonus raw 5 (while enabled); grant Aikido Grandmaster Upgrade [1069] | loaded passive totals + records (@50: Enrage Duration Bonus +2 [total only], Initiative Bonus +1 [total only], Melee Resistance Bonus +5 [combat resistance], Melee Defense Bonus +5 [combat defence]); load/unload/startup grants 1069 | partial |
| 411 Aerial Takedown | memory | Martial Artist / Aikido Grandmaster | Melee Damage Resistance raw 40 (15.0s); physical raw 96 (instant); Enraged (15.0s), 40% proc | IS 25 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 15s; special move: needs Aikido (auto-switch when loaded) | partial |
| 412 Tomo Nage | memory | Martial Artist / Aikido Grandmaster | Enraged (6.0s); physical raw 113 (instant); Ballistic Damage Resistance raw -100 (12.0s); Melee Damage Resistance raw -100 (12.0s) | IS 45 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 6s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Aikido (auto-switch when loaded) | partial |
| 413 Restore Health 3.0 | memory | Hacker / Physician | heal raw 70 (instant) | IS 25 (stored); reuse 4s / group 685 4s; target/team heal; stored base 70; caster healing influence | partial |
| 414 Repair RSI 4.0 | memory | Hacker / Physician | heal raw 76 (instant) | IS 35 (stored); reuse 4s / group 686 4s; target/team heal; stored base 76; caster healing influence | partial |
| 415 Harmful Code 3.0 | memory | Hacker / Physician | viral raw 52 (instant) | IS 23 (stored); reuse 8s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 416 Rejuvenate RSI | memory | Hacker / Physician | [RejuvenateRSI_DirectObject](../research/client-scripts/ability/hacker/health.py#L45) | IS 150 (stored); reuse 15s; dead friendly player + native approval; full HP / IS; exact resurrect motion unverified | partial |
| 417 Upgrade Health | memory | Hacker / Physician | Max Health raw ? × native function 3 / scale 10.0 (300.0s) | IS 80 (stored); reuse 15s; buff records + caster-level stat snapshot; maximum vital | partial |
| 418 Physician | memory | Hacker / Physician | Healing Bonus raw 5 (while enabled); grant Physician Heal [1098] | loaded passive totals + records (@50: Healing Bonus +5 [healing]); load/unload/startup grants 1098 | partial |
| 419 Bolster Health 3.0 | memory | Hacker / Upgrade Master | Max Health raw ? × native function 3 / scale 10.0 (300.0s) | IS 45 (stored); reuse 10s; buff records + caster-level stat snapshot; maximum vital | partial |
| 420 Delete Upgrade 2.0 | memory | Hacker / Upgrade Master | ISDrain Target - this string should never be seen. raw 20 (instant); [RemoveRandomBuff](../research/client-scripts/tempmods.py#L342), [UpgradeSweepPulse](../research/client-scripts/tempmods.py#L314) | IS 25 (stored); reuse 10s; instant script: removable buffs (UBF0) / current IS on landing | partial |
| 421 Enhanced Dodge | memory | Hacker / Upgrade Master | Melee Defense raw ? × native function 3 / scale 0.96 (300.0s); Ballistic Defense raw ? × native function 3 / scale 0.96 (300.0s) | IS 40 (stored); reuse 10s; buff records + caster-level stat snapshot; combat defence | partial |
| 422 Enhanced Potency | memory | Hacker / Upgrade Master | Melee Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled); Thrown Resistance Bonus raw ? × native function 2 / scale 0.1 (while enabled); Viral Resistance Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Ballistic Damage Bonus +10 [damage], Thrown Resistance Bonus +5 [total only], Viral Resistance Bonus +5 [total only], Melee Damage Bonus +10 [damage]) | partial |
| 423 Movement Accelerator 3.0 | memory | Hacker / Upgrade Master | Invisibility Movement raw 10 (960.0s); Sneak Movement raw 10 (960.0s); Movement Speed raw 45 (960.0s) | IS 50 (stored); reuse 10s; buff records + selected combat/vital stats (stored constants) | partial |
| 424 Negative Condition Sweep 2.0 | memory | Hacker / Upgrade Master | [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 40 (stored); reuse 10s; pulse sweep removes cleansable debuff parents (UDB0, not exempt) | partial |
| 425 Upgrade Master | memory | Hacker / Upgrade Master | grant Upgrade Master Upgrade [1096]; Concentration Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Concentration Bonus +5 [total only]); load/unload/startup grants 1096 | partial |
| 426 Kung Fu Grandmaster | memory | Martial Artist / Kung Fu Grandmaster | Initiative Bonus raw 1 (while enabled); Powerless Duration Bonus raw 2 (while enabled); Melee Accuracy Bonus raw 5 (while enabled); grant Kung Fu Grandmaster Upgrade [1064]; Melee Damage Bonus raw 1 (while enabled) | loaded passive totals + records (@50: Powerless Duration Bonus +2 [control duration], Melee Accuracy Bonus +5 [combat accuracy], Melee Damage Bonus +1 [damage], Initiative Bonus +1 [total only]); load/unload/startup grants 1064 | partial |
| 427 Triple Front Kick | memory | Martial Artist / Kung Fu Grandmaster | physical raw 91 (instant); Powerless (6.0s) | IS 25 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 6s; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 428 Suicidal Butterfly | memory | Martial Artist / Kung Fu Grandmaster | Confuse (4.0s), 30% proc; physical raw 25 (instant) | IS 35 (capture); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; Confuse 4s; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 429 Extreme Falling Kick | memory | Martial Artist / Kung Fu Grandmaster | ISDrain Target - this string should never be seen. raw 4 (0.1s); physical raw 46 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 40 (capture); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 430 Wooden Dummy Drill | memory | Martial Artist / Kung Fu Grandmaster | physical raw 107 (instant); Powerless (6.0s) | IS 30 (capture); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 6s; special move: needs Kung-Fu (auto-switch when loaded) | partial |
| 431 Kung Fu Perfection | memory | Martial Artist / Kung Fu Grandmaster | Melee Defense Bonus raw ? × native function 2 / scale 0.2 (while enabled); Viral Defense Bonus raw ? × native function 2 / scale 0.125 (while enabled); Stun Duration Bonus raw ? × native function 2 / scale 0.2 (while enabled); Melee Damage Bonus raw ? × native function 2 / scale 0.025 (while enabled) | loaded passive totals + records (@50: Melee Damage Bonus +1 [damage], Melee Defense Bonus +10 [combat defence], Stun Duration Bonus +10 [control duration], Viral Defense Bonus +6 [total only]) | partial |
| 433 Total Dedication | memory | Hacker / Trauma Surgeon | Inner Strength Regen Rate Bonus raw ? × native function 2 / scale 0.2 (while enabled); Healing Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Inner Strength Regen Rate Bonus +10 [regeneration], Healing Bonus +5 [healing]) | partial |
| 434 Restore Group 3.0 | memory | Hacker / Trauma Surgeon | heal raw 32 (instant) | IS 24 (stored); reuse 4s / group 685 4s; target/team heal; stored base 32; caster healing influence | partial |
| 435 Group Repairs 3.0 | memory | Hacker / Trauma Surgeon | heal raw 30 (instant) | IS 30 (stored); reuse 4s / group 686 4s; target/team heal; stored base 30; caster healing influence | partial |
| 436 Viral Shielding | memory | Hacker / Trauma Surgeon | Melee Defense raw ? × native function 3 / scale 0.24 (300.0s); Thrown Defense raw ? × native function 3 / scale 0.24 (300.0s); Ballistic Defense raw ? × native function 3 / scale 0.24 (300.0s); ISDrain Target - this string should never be seen. raw 25 (300.0s); Viral Defense raw ? × native function 3 / scale 0.96 (300.0s); [RefundIS](../research/client-scripts/tempmods.py#L3085) | IS 100 (stored); reuse 15s; buff records + selected combat/vital stats (stored scripted); term refunds 25 IS to recipient | partial |
| 437 Panicked Heal | memory | Hacker / Trauma Surgeon | heal raw 123 (instant); [DoNothing](../research/client-scripts/tempmods.py#L1299) | IS 100 (stored); reuse 300s; target/team heal; stored base 123; caster healing influence | partial |
| 438 Renew RSI | memory | Hacker / Trauma Surgeon | [RenewRSI_DirectObject](../research/client-scripts/ability/hacker/health.py#L71) | IS 150 (stored); reuse 15s; dead friendly player + native approval; full HP / IS; exact resurrect motion unverified | partial |
| 439 Trauma Surgeon | memory | Hacker / Trauma Surgeon | grant Trauma Surgeon [1093]; Buff Area Radius Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Buff Area Radius Bonus +5 [total only]); load/unload/startup grants 1093 | partial |
| 440 Expert Rifleman | memory | Gunman / Expert Rifleman | Ballistic Accuracy Bonus raw 5 (while enabled); Ballistic Damage Bonus raw 1 (while enabled); Root Duration Bonus raw 2 (while enabled); grant Expert Rifleman Upgrade [1074] | loaded passive totals + records (@50: Ballistic Damage Bonus +1 [damage], Root Duration Bonus +2 [control duration], Ballistic Accuracy Bonus +5 [combat accuracy]); load/unload/startup grants 1074 | partial |
| 441 Rifle Mastery | memory | Gunman / Expert Rifleman | Ballistic Damage Resistance raw ? × native function 2 / scale 0.4 (while enabled); Stun Duration Bonus raw ? × native function 2 / scale 0.2 (while enabled); Root Duration Bonus raw ? × native function 2 / scale 0.3 (while enabled) | loaded passive totals + records (@50: Stun Duration Bonus +10 [control duration], Root Duration Bonus +15 [control duration], Ballistic Damage Resistance +20 [combat resistance]) | partial |
| 442 Steady Aim | memory | Gunman / Expert Rifleman | Ballistic Accuracy raw 20 (while enabled); ISDrain Target - this string should never be seen. raw 30 (while enabled); [AddRecastTimer](../research/client-scripts/tempmods.py#L1247), [DoNothing](../research/client-scripts/tempmods.py#L1299), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 30 (stored); reuse 15s; buff records + learned-level stat snapshot; combat accuracy; IS drain 30/5s; shortfall deactivates | partial |
| 450 Crippling Shot | memory | Gunman / Expert Rifleman | Movement Speed raw -75 (8.0s); ballistic raw 49 (instant); Sneak Movement raw -75 (8.0s); Invisibility Movement raw -75 (8.0s) | IS 45 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 453 Rifle Butt Smash | memory | Gunman / Expert Rifleman | ballistic raw 6 (instant) | IS 40 (stored); reuse 60s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Rifles (auto-switch when loaded) | partial |
| 454 Deadly Shot | memory | Gunman / Expert Rifleman | ballistic raw 61 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 45 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; Stun 4s; special move: needs Rifles (auto-switch when loaded) | partial |
| 461 Infection Efficiency | memory | Hacker / Destroyer | Melee Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled); Stun Duration Bonus raw 0..50 × native function 2 / scale 0.3 (while enabled) | loaded passive totals + records (@50: Melee Accuracy Bonus +5 [combat accuracy], Ballistic Damage Bonus +5 [damage], Stun Duration Bonus +15 [control duration]) | partial |
| 462 Enrage Foes | memory | Hacker / Destroyer | Enraged (6.0s) | IS 26 (stored); reuse 30s; Enraged 6s | partial |
| 465 UI Lag 2.0 | memory | Hacker / Destroyer | Ballistic Accuracy raw -20 (17.0s); Thrown Accuracy raw -20 (17.0s); Ballistic Defense raw -18 (17.0s); Thrown Defense raw -18 (17.0s) | IS 20 (stored); reuse 30s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 466 Plague Zone 2.0 | memory | Hacker / Destroyer | ViralDOT raw total 97 (15.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 20 (capture); reuse 12s; captured hit 0..0; stored total/interval DOT executor | partial |
| 467 Inefficiency Field 2.0 | memory | Hacker / Destroyer | Inner Strength Cost Reduction raw -30 (30.0s); Inner Strength Regen Rate raw -80 (30.0s) | IS 25 (stored); reuse 60s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 468 Devastation Field | memory | Hacker / Destroyer | viral raw 1 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 56 (stored); reuse 30s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 469 Destroyer | memory | Hacker / Destroyer | Stun Duration Bonus raw 5 (while enabled); grant Destroyer [1088] | loaded passive totals + records (@50: Stun Duration Bonus +5 [control duration]); load/unload/startup grants 1088 | partial |
| 470 Ravager | memory | Hacker / Ravager | Inner Strength Regen Rate Bonus raw 5 (while enabled); grant Ravager Downgrade [1081]; Max Inner Strength Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Max Inner Strength Bonus +5 [maximum vital], Inner Strength Regen Rate Bonus +5 [regeneration]); load/unload/startup grants 1081 | partial |
| 471 Attack Programming | memory | Hacker / Ravager | Inner Strength Cost Modifier Bonus raw ? × native function 2 / scale 0.1 (while enabled); Inner Strength Regen Rate Bonus raw ? × native function 2 / scale 0.3 (while enabled) | loaded passive totals + records (@50: Inner Strength Regen Rate Bonus +15 [regeneration], Inner Strength Cost Modifier Bonus +5 [total only]) | partial |
| 472 Code Infection 3.0 | memory | Hacker / Ravager | ViralDOT raw total 171 (30.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 11 (stored); reuse 12s / group 702 4s; stored total/interval DOT executor | partial |
| 473 Freeze System 3.0 | memory | Hacker / Ravager | Pacify (4.0s); Viral Resistance raw -80 (8.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 42 (stored); reuse 15s; Pacify 4s / break Damage | partial |
| 474 Code Infection 4.0 | memory | Hacker / Ravager | ViralDOT raw total 214 (30.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 13 (stored); reuse 12s / group 702 4s; stored total/interval DOT executor | partial |
| 475 Infect Area 2.0 | memory | Hacker / Ravager | ViralDOT raw total 144 (30.0s); [ViralDOT](../research/client-scripts/tempmods.py#L896) | IS 25 (stored); reuse 12s; stored total/interval DOT executor | partial |
| 476 Destroy Health | memory | Hacker / Ravager | Health Regen Rate raw -20 (40.0s); Max Health raw -520 (40.0s) | IS 40 (stored); reuse 120s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 477 Efficient Code | memory | Hacker / Network Attacker | Melee Accuracy Bonus raw ? × native function 2 / scale 0.2 (while enabled); Stun Duration Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Accuracy Bonus raw ? × native function 2 / scale 0.2 (while enabled); Melee Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Stun Duration Bonus +5 [control duration], Ballistic Accuracy Bonus +10 [combat accuracy], Melee Damage Bonus +10 [damage], Ballistic Damage Bonus +10 [damage], Melee Accuracy Bonus +10 [combat accuracy]) | partial |
| 478 Code Flux | memory | Hacker / Network Attacker | Stealth Detection raw -100 (60.0s); Combat Evasion raw -100 (60.0s); blinded (8.0s), 10% proc | IS 42 (stored); reuse 30s; blinded 8s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 479 Freeze Area 2.0 | memory | Hacker / Network Attacker | Pacify (6.0s); Viral Resistance raw -80 (12.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 38 (stored); reuse 15s; Pacify 10.02s / break Damage; captured debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 480 Combat Shutdown | memory | Hacker / Network Attacker | Ballistic Accuracy raw -61 (25.0s); Ballistic Defense raw -31 (25.0s); Melee Accuracy raw -61 (25.0s); Melee Defense raw -31 (25.0s) | IS 23 (stored); reuse 60s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 481 Buffer Overflow 2.0 | memory | Hacker / Network Attacker | [BufferOverflowInit](../research/client-scripts/tempmods.py#L532), [BufferOverflowTerm](../research/client-scripts/tempmods.py#L538), [BufferOverflowUpdate](../research/client-scripts/tempmods.py#L520) | IS 47 (stored); reuse 30s; Buffer Overflow 2.0 60s; native script buffer reservation min(Value,free); baseline100+4level is OURS; refuses new upgrades until removal/expiry | partial |
| 482 Code Shock | memory | Hacker / Network Attacker | Stun (6.0s); blinded (6.0s); Dazed (5.0s); Off-Balance (5.0s); Staggered (5.0s); [AddControlImmunityTimerVal](../research/client-scripts/tempmods.py#L1639) | IS 56 (stored); reuse 60s; blinded 6s, Dazed 5s, Off-Balance 5s, Staggered 5s, Stun 6s | partial |
| 483 Network Attacker | memory | Hacker / Network Attacker | grant Network Attacker Upgrade [1080]; Viral Accuracy Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Viral Accuracy Bonus +5 [total only]); load/unload/startup grants 1080 | partial |
| 484 Supreme Damage Resistance | memory | Hacker / Force Multiplier | Viral Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled); Melee Resistance Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Resistance Bonus raw ? × native function 2 / scale 0.1 (while enabled); Max Inner Strength Bonus raw ? × native function 2 / scale 0.2 (while enabled); Thrown Damage Bonus raw 0..50 × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Viral Damage Bonus +10 [damage], Melee Resistance Bonus +5 [combat resistance], Ballistic Resistance Bonus +5 [combat resistance], Max Inner Strength Bonus +10 [maximum vital], Thrown Damage Bonus +10 [damage]) | partial |
| 485 Combat Aura 2.0 | memory | Hacker / Force Multiplier | ISDrain Target - this string should never be seen. raw 15 (while enabled); ISDrain Target - this string should never be seen. raw 696 (while enabled); [AddRecastTimer](../research/client-scripts/tempmods.py#L1247), [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [DoNothing](../research/client-scripts/tempmods.py#L1299), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 15 (stored); reuse 15s; toggled aura: IS drain 15/5s, pulses 696 on friendly team players in its effect radius | partial |
| 486 Recall Mission Team | memory | Hacker / Force Multiplier | [RecallMissionTeam_DirectObject](../research/client-scripts/ability/hacker/misc.py#L53), [RecallMissionTeam_Test](../research/client-scripts/ability/hacker/misc.py#L61) | Recall Mission Team: every online team member as Recall Team Member; caster line 0x7a000164; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md); IS 75 (stored); reuse 180s | partial |
| 487 Sweep Party 2.0 | memory | Hacker / Force Multiplier | [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 80 (stored); reuse 30s; pulse sweep removes cleansable debuff parents (UDB0, not exempt) | partial |
| 488 Cancel Upgrade 2.0 | memory | Hacker / Force Multiplier | ISDrain Target - this string should never be seen. raw 100 (instant); [BufferOverflowInit](../research/client-scripts/tempmods.py#L532), [BufferOverflowTerm](../research/client-scripts/tempmods.py#L538), [BufferOverflowUpdate](../research/client-scripts/tempmods.py#L520), [RemoveRandomBuff](../research/client-scripts/tempmods.py#L342) | IS 50 (stored); reuse 10s; Cancel Upgrade 2.0 50s; native script buffer reservation min(Value,free); baseline100+4level is OURS; refuses new upgrades until removal/expiry; instant script: removable buffs (UBF0) / current IS on landing | partial |
| 489 Improved Hacking | memory | Hacker / Force Multiplier | Viral Damage raw ? × native function 3 / scale 0.5 (300.0s); Healing raw ? × native function 3 / scale 0.5 (300.0s) | IS 120 (stored); reuse 10s; buff records + caster-level stat snapshot; damage, healing | partial |
| 490 Force Multiplier | memory | Hacker / Force Multiplier | grant Force Multiplier Upgrade [1091]; Sweep Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Sweep Bonus +5 [total only]); load/unload/startup grants 1091 | partial |
| 491 Submachine Gun Specialization | memory | Gunman / SMG Specialist | Determination Bonus raw ? × native function 2 / scale 0.2 (while enabled); Melee Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled); Enrage Duration Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Melee Defense Bonus +5 [combat defence], Ballistic Damage Bonus +5 [damage], Enrage Duration Bonus +5 [total only], Determination Bonus +10 [total only]) | partial |
| 492 Pinning Fire | memory | Gunman / SMG Specialist | Rooted (8.0s); ballistic raw 21 (instant) | IS 45 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Rooted 14.08s | partial |
| 493 Direct Fire | memory | Gunman / SMG Specialist | ballistic raw 35 (instant); Enraged (10.0s), 30% proc | IS 50 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 10s | partial |
| 494 Full Auto | memory | Gunman / SMG Specialist | ballistic raw 123 (instant); Enraged (8.0s), 40% proc | IS 38 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 8s; special move: needs Submachine Guns (auto-switch when loaded) | partial |
| 495 SMG Specialist | memory | Gunman / SMG Specialist | Initiative Bonus raw 1 (while enabled); Stun Duration Bonus raw 2 (while enabled); Ballistic Damage Bonus raw 4 (while enabled); grant SMG Specialist Upgrade [1072] | loaded passive totals + records (@50: Stun Duration Bonus +2 [control duration], Ballistic Damage Bonus +4 [damage], Initiative Bonus +1 [total only]); load/unload/startup grants 1072 | partial |
| 496 Full Auto Redux | memory | Gunman / SMG Specialist | ballistic raw 150 (instant); Melee Accuracy raw -15 (8.0s); Ballistic Accuracy raw -15 (8.0s) | IS 45 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Submachine Guns (auto-switch when loaded) | partial |
| 497 Duelist | memory | Gunman / Duelist | Initiative Bonus raw 2 (while enabled); Ballistic Defense Bonus raw 5 (while enabled); Powerless Duration Bonus raw 2 (while enabled); grant Duelist Upgrade [1076] | loaded passive totals + records (@50: Ballistic Defense Bonus +5 [combat defence], Powerless Duration Bonus +2 [control duration], Initiative Bonus +2 [total only]); load/unload/startup grants 1076 | partial |
| 498 Handgun Artistry | memory | Gunman / Duelist | Powerless Duration Bonus raw ? × native function 2 / scale 0.2 (while enabled); Melee Defense Bonus raw ? × native function 2 / scale 0.2 (while enabled); Determination Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Resistance Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Melee Defense Bonus +10 [combat defence], Determination Bonus +5 [total only], Ballistic Resistance Bonus +5 [combat resistance], Powerless Duration Bonus +10 [control duration]) | partial |
| 499 Dual Pistol Point Blank | memory | Gunman / Duelist | ballistic raw 98 (instant); Powerless (6.0s); [PistolPointBlank_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L75) | IS 30 (stored); reuse 10s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 6s | partial |
| 500 Dual Pistol Dash | memory | Gunman / Duelist | Enraged (8.0s); ballistic raw 13 (instant); [PistolDash_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L44) | IS 35 (stored); reuse 20s / group 690 5s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 8s | partial |
| 501 Pistol Whip | memory | Gunman / Duelist | ballistic raw 109 (instant); blinded (8.0s) | IS 30 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; blinded 10.08s; special move: needs Handguns (auto-switch when loaded) | partial |
| 502 Dual Pistol Execution | memory | Gunman / Duelist | ballistic raw 45 (instant); Ballistic Defense raw -31 (8.0s); Melee Accuracy raw -27 (8.0s); [PistolExecution_DirectObject](../research/client-scripts/ability/soldier/old_sm_pistol.py#L89) | IS 35 (stored); reuse 10s / group 689 5s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Handguns (auto-switch when loaded) | partial |
| 503 Concealment | memory | Gunman / Sniper | Determination raw ? × native function 2 / scale 0.2 (while enabled); Inner Strength Regen Rate Bonus raw ? × native function 2 / scale 0.2 (while enabled); Contested Withdraw Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled); grant Concealment Grant [1137]; [Concealment_RemoveConcealmentStealth](../research/client-scripts/tempmods.py#L82) | loaded passive totals + records (@50: Contested Withdraw Bonus +5 [total only], Inner Strength Regen Rate Bonus +10 [regeneration], Ballistic Damage Bonus +5 [damage], Determination +10 [total only]); load/unload/startup grants 1137; stealth detection/AI incomplete | partial |
| 504 Concealed Shot | memory | Gunman / Sniper | ballistic raw 22 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659), [DoNothing](../research/client-scripts/tempmods.py#L1299) | IS 45 (stored); reuse 45s; generic hit 50..80 / CSV value; scaling unresolved; stealth detection/AI incomplete | partial |
| 505 Sniper Shot | memory | Gunman / Sniper | ballistic raw 47 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 60 (stored); reuse 45s; generic hit 50..80 / CSV value; scaling unresolved; Stun 4s; special move: needs Rifles (auto-switch when loaded) | partial |
| 506 Line Up The Shot | memory | Gunman / Sniper | Ballistic Accuracy raw ? × native function 3 / scale 1.3 (20.0s); Ballistic Damage raw ? × native function 3 / scale 0.5 (12.0s); ISDrain Target - this string should never be seen. raw 1137 (instant); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [LineUpTheShot_Subject](../research/client-scripts/ability/spy/misc.py#L63), [LineUpTheShot_Test](../research/client-scripts/ability/spy/misc.py#L70), [OnHealthReduceDeactivateAbility](../research/client-scripts/tempmods.py#L1045) | IS 30 (stored); reuse 15s | missing |
| 507 Escape And Evasion | memory | Gunman / Sniper | Sneak Movement raw 25 (while enabled); Stealth raw ? × native function 3 / scale 1.0 (while enabled); Stealthed (while enabled); [EscapeandEvasion_Deactivate](../research/client-scripts/ability/spy/sneaks.py#L68), [EscapeandEvasion_DirectObject](../research/client-scripts/ability/spy/sneaks.py#L58), [EscapeandEvasion_Test](../research/client-scripts/ability/spy/sneaks.py#L74), [OnHealthReduceDeactivateAbility](../research/client-scripts/tempmods.py#L1045) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md) | partial |
| 508 Sniper | memory | Gunman / Sniper | Ballistic Accuracy Bonus raw 2 (while enabled); Ballistic Damage Bonus raw 2 (while enabled); grant Sniper Upgrade [1136] | loaded passive totals + records (@50: Ballistic Damage Bonus +2 [damage], Ballistic Accuracy Bonus +2 [combat accuracy]); load/unload/startup grants 1136 | partial |
| 512 Repair Items | memory | Coder / Programmer | Repair Skill raw ? × native function 2 (while enabled) | reuse 10s; loaded passive totals + records (@50: Repair Skill +50 [total only]) | cosmetic |
| 513 Quick Coding | memory | Coder / Programmer | heal raw 13 (instant) | IS 20 (stored); reuse 6s; target/team heal; stored base 13; caster healing influence | partial |
| 514 Advanced Tinkering | memory | Coder / Programmer | viral raw 6 (instant) | IS 40 (stored); reuse 30s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 515 Code Crafter | memory | Coder / Code Crafter | Compile Time Reduction raw 25 (while enabled); Code Writing Discount raw 25 (while enabled) | IS 0 (stored); loaded passive totals + records (@50: Code Writing Discount +25 [total only], Compile Time Reduction +25 [total only]) | cosmetic |
| 516 Ability Maker | memory | Coder / Ability Maker | Ability Crafting raw ? × native function 2 (while enabled) | loaded passive totals + records (@50: Ability Crafting +50 [crafting]) | partial |
| 517 Weapon Maker | memory | Coder / Weapon Maker | Weapon Crafting raw ? × native function 2 (while enabled) | loaded passive totals + records (@50: Weapon Crafting +50 [crafting]) | partial |
| 518 Tool Maker | memory | Coder / Tool Maker | Tool Crafting raw ? × native function 2 (while enabled) | loaded passive totals + records (@50: Tool Crafting +50 [crafting]) | partial |
| 521 Upgrade Maker | memory | Coder / Upgrade Maker | Upgrade Crafting raw ? × native function 2 (while enabled) | loaded passive totals + records (@50: Upgrade Crafting +50 [crafting]) | partial |
| 528 Apparel Maker | memory | Coder / Apparel Maker | Apparel Crafting raw ? × native function 2 (while enabled) | loaded passive totals + records (@50: Apparel Crafting +50 [crafting]) | partial |
| 531 Karate Focus | memory | Martial Artist / Karate Grandmaster | Stun Duration Bonus raw ? × native function 2 / scale 0.2 (while enabled); Viral Defense Bonus raw ? × native function 2 / scale 0.25 (while enabled); Melee Damage Bonus raw ? × native function 2 / scale 0.125 (while enabled); Melee Resistance Bonus raw ? × native function 2 / scale 0.1 (while enabled); Enrage Duration Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Viral Defense Bonus +12 [total only], Melee Damage Bonus +6 [damage], Melee Resistance Bonus +5 [combat resistance], Enrage Duration Bonus +5 [total only], Stun Duration Bonus +10 [control duration]) | partial |
| 532 Machinegun Kick | memory | Martial Artist / Karate Grandmaster | physical raw 73 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 32 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Karate (auto-switch when loaded) | partial |
| 533 Karate Grandmaster | memory | Martial Artist / Karate Grandmaster | Initiative Bonus raw 1 (while enabled); Stun Duration Bonus raw 2 (while enabled); Melee Damage Bonus raw 4 (while enabled); grant Karate Grandmaster Upgrade [1066] | loaded passive totals + records (@50: Stun Duration Bonus +2 [control duration], Melee Damage Bonus +4 [damage], Initiative Bonus +1 [total only]); load/unload/startup grants 1066 | partial |
| 534 Wrist Throw | memory | Martial Artist / Karate Grandmaster | physical raw 31 (instant); [ApplyStunWithTimer](../research/client-scripts/tempmods.py#L1659) | IS 65 (stored); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Karate (auto-switch when loaded) | partial |
| 535 Berserker Attack | memory | Martial Artist / Karate Grandmaster | Melee Damage Resistance raw 50 (21.0s); Melee Damage raw 11 (21.0s); Ballistic Damage Resistance raw 50 (21.0s) | IS 40 (stored); reuse 60s / group 687 5s; buff records + selected combat/vital stats (stored constants); special move: needs Karate (auto-switch when loaded); on landing: caster buff from stored WhenEnabled 1 modifiers | partial |
| 536 Ki-Charged Foot Sweep | memory | Martial Artist / Karate Grandmaster | physical raw 134 (instant); Powerless (6.0s), 50% proc | IS 45 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 6s; special move: needs Karate (auto-switch when loaded) | partial |
| 537 Data Miner | memory | Awakened / Data Miner | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor | cosmetic |
| 538 Data Node Goggles | memory | Awakened / Data Miner | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor; prerequisite tier only (no script references it); see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md) | missing |
| 539 Data Tap | memory | Awakened / Data Miner | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor; prerequisite tier only (no script references it); see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md) | missing |
| 540 See Luggables | memory | Awakened / Data Miner | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor; flag read by Find Data Node goggles (luggable glow); needs the visual locate record; see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md) | missing |
| 541 Detect Luggables | memory | Awakened / Data Miner | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor; flag read by Find Data Node handheld (nearest luggable); needs requestLuggableLocation; see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md) | missing |
| 547 Holy Water Attack | memory | Legacy / special weapon / Shared / no stored parent | [HolyWaterAttack_DirectObject](../research/client-scripts/ability/general.py#L99) | general.py damage 10, x10 vs NPCKilledOnlyByHolyWater (NPC weakness by name: server policy) | partial |
| 585 Run Advanced Program | memory | Coder / Proxy Technician | Viral Damage Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Viral Damage Bonus +10 [damage]) | partial |
| 586 Remote Proxy 4.0 | memory | Coder / Proxy Technician | [RemoteProxy4_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L248), [RemoteProxy4_Subject](../research/client-scripts/ability/hacker/simulacra.py#L235) | IS 75 (stored); reuse 8s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 18-34; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); random proxy sheet | partial |
| 587 Repair Simulacra 3.0 | memory | Coder / Proxy Technician | heal raw 117 (instant) | IS 16 (stored); reuse 28s; heals own simulacrum: stored base; caster healing influence; player cast refused | partial |
| 588 Enrage 1.0 | memory | Coder / Proxy Technician | Enraged (12.0s) | IS 27 (stored); reuse 28s; Enraged 12s | partial |
| 589 Proxy Technician | memory | Coder / Proxy Technician | ISDrain Target - this string should never be seen. raw 5 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 15 (stored); reuse 15s | cosmetic |
| 590 Static Barrage 1.0 | memory | Coder / Proxy Technician | viral raw 32 (instant) | IS 18 (stored); reuse 6s / group 683 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 591 Static Blast 2.0 | memory | Coder / Proxy Technician | viral raw 42 (instant) | IS 23 (stored); reuse 6s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 593 Transmit Code | memory | Coder / Proxy Master | Viral Resistance Bonus raw ? × native function 2 / scale 0.2 (while enabled); Thrown Resistance Bonus raw ? × native function 2 / scale 0.2 (while enabled) | loaded passive totals + records (@50: Thrown Resistance Bonus +10 [total only], Viral Resistance Bonus +10 [total only]) | cosmetic |
| 594 Remote Proxy 5.0 | memory | Coder / Proxy Master | [RemoteProxy5_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L266), [RemoteProxy5_Subject](../research/client-scripts/ability/hacker/simulacra.py#L253) | IS 120 (stored); reuse 8s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 35-50; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); random proxy sheet | partial |
| 595 Stun Pulse | memory | Coder / Proxy Master | Stun (6.0s); [AddControlImmunityTimer](../research/client-scripts/tempmods.py#L1631) | IS 30 (stored); reuse 30s; Stun 6s | partial |
| 596 Repair Simulacra 4.0 | memory | Coder / Proxy Master | heal raw 174 (instant) | IS 22 (stored); reuse 28s; heals own simulacrum: stored base; caster healing influence; player cast refused | partial |
| 597 Enrage 2.0 | memory | Coder / Proxy Master | Enraged (10.0s) | IS 45 (stored); reuse 30s; Enraged 10s | partial |
| 598 Static Barrage 2.0 | memory | Coder / Proxy Master | viral raw 64 (instant) | IS 27 (stored); reuse 6s / group 683 4s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 599 Proxy Master | memory | Coder / Proxy Master | Inner Strength Cost Modifier Bonus raw 5 (while enabled); grant Proxy Master [1107] | loaded passive totals + records (@50: Inner Strength Cost Modifier Bonus +5 [total only]); load/unload/startup grants 1107 | partial |
| 600 Close Combat Training | memory | Coder / Utility Coder | Ballistic Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled); Melee Accuracy Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Melee Accuracy Bonus +5 [combat accuracy], Ballistic Accuracy Bonus +5 [combat accuracy]) | partial |
| 601 Logic Daemon 2.0 | memory | Coder / Utility Coder | [LogicDaemon2_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L97), [LogicDaemon2_Subject](../research/client-scripts/ability/hacker/simulacra.py#L87) | IS 90 (stored); reuse 10s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 8-17; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); Nuker sheet | partial |
| 602 Patch Daemon 1.0 | memory | Coder / Utility Coder | [PatchDaemon1_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L142), [PatchDaemon1_Subject](../research/client-scripts/ability/hacker/simulacra.py#L132) | IS 100 (stored); reuse 10s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 12-22; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); Healer sheet heals owner (amount a choice) | partial |
| 603 Charge Simulacra | memory | Coder / Utility Coder | Inner Strength Cost Reduction raw 20 (600.0s); Inner Strength Regen Rate raw 50 (600.0s); [AcousticDecoy1_GetRange](../research/client-scripts/ability/hacker/simulacra.py#L18) | IS 35 (stored); reuse 30s; own simulacrum only; IS cost/regen buff has no consumer (simulacra spend no IS) | partial |
| 604 Utility Coder | memory | Coder / Utility Coder | Max Health raw 50 (while enabled); Melee Damage Resistance raw 20 (while enabled); Ballistic Damage Resistance raw 20 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 605 Low Blow | memory | Coder / Utility Coder | physical raw 17 (instant) | IS 30 (stored); reuse 6s; generic hit 50..80 / CSV value; scaling unresolved; special move: needs Coder Style (auto-switch when loaded) | partial |
| 607 Absorb Simulacra | memory | Coder / Utility Technician | health effect raw 50..? (instant); health effect raw 18..? (instant); [AcousticDecoy2_Test](../research/client-scripts/ability/hacker/simulacra.py#L30) | IS 25 (stored); reuse 30s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 608 Code Hardening | memory | Coder / Utility Technician | Melee Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Damage Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Ballistic Damage Bonus +5 [damage], Melee Damage Bonus +5 [damage]) | partial |
| 609 Daemon Boost 1.0 | memory | Coder / Utility Technician | ISDrain Target - this string should never be seen. raw 5 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 10 (stored); reuse 15s; toggle; 5 IS/5s drain with deactivation; 699 Viral Damage/Healing +level x0.5 to simulacrum within 20m | partial |
| 610 Engage Foes | memory | Coder / Utility Technician | Enraged (6.0s); [EngageFoes_DirectObject](../research/client-scripts/ability/hacker/misc.py#L69), [TauntAggro](../research/client-scripts/tempmods.py#L1281) | IS 50 (stored); reuse 30s; Enraged 6s | partial |
| 611 Logic Daemon 3.0 | memory | Coder / Utility Technician | [LogicDaemon3_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L112), [LogicDaemon3_Subject](../research/client-scripts/ability/hacker/simulacra.py#L102) | IS 110 (stored); reuse 10s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 18-34; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); Nuker sheet | partial |
| 612 Patch Daemon 2.0 | memory | Coder / Utility Technician | [PatchDaemon2_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L157), [PatchDaemon2_Subject](../research/client-scripts/ability/hacker/simulacra.py#L147) | IS 120 (stored); reuse 10s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 23-43; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); Healer sheet heals owner (amount a choice) | partial |
| 613 Utility Technician | memory | Coder / Utility Technician | Melee Damage Resistance raw 20 (while enabled); Ballistic Damage Resistance raw 20 (while enabled); Max Health raw 100 (while enabled); Melee Defense raw 3 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 614 Enhance Daemon | memory | Coder / Utility Master | Melee Defense Bonus raw ? × native function 2 / scale 0.1 (while enabled); Ballistic Resistance Bonus raw ? × native function 2 / scale 0.1 (while enabled) | loaded passive totals + records (@50: Ballistic Resistance Bonus +5 [combat resistance], Melee Defense Bonus +5 [combat defence]) | partial |
| 615 Fix Simulacra | memory | Coder / Utility Master | heal raw 83 (instant) | IS 45 (stored); reuse 30s; heals own simulacrum: stored base; caster healing influence; player cast refused | partial |
| 616 Logic Daemon 4.0 | memory | Coder / Utility Master | [LogicDaemon4_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L127), [LogicDaemon4_Subject](../research/client-scripts/ability/hacker/simulacra.py#L117) | IS 150 (stored); reuse 10s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 35-50; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); Nuker sheet | partial |
| 617 Patch Daemon 3.0 | memory | Coder / Utility Master | [PatchDaemon3_Deactivate](../research/client-scripts/ability/hacker/simulacra.py#L172), [PatchDaemon3_Subject](../research/client-scripts/ability/hacker/simulacra.py#L162) | IS 170 (stored); reuse 10s; NPC_BASE_SIMULACRA spawn (retail creation layout), owned; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp 44-50; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice); Healer sheet heals owner (amount a choice) | partial |
| 618 Bash | memory | Coder / Utility Master | physical raw 66 (instant); Stun (4.0s); Enraged (8.0s) | IS 40 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Stun 4s, Enraged 8s; special move: needs Coder Style (auto-switch when loaded) | partial |
| 619 Daemon Boost 2.0 | memory | Coder / Utility Master | ISDrain Target - this string should never be seen. raw 5 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 20 (stored); reuse 15s; toggle; 5 IS/5s drain with deactivation; 700 accuracy has no simulacrum consumer | partial |
| 620 Utility Master | memory | Coder / Utility Master | grant Utility Master [1108]; Melee Accuracy Bonus raw 5 (while enabled) | loaded passive totals + records (@50: Melee Accuracy Bonus +5 [combat accuracy]); load/unload/startup grants 1108 | partial |
| 621 Find Data Node | memory | Awakened / Data Miner | Native action / passive metadata; no decoded stored modifier | Find Data Node toggle: locator tool gate (ID_ABILITY_TOOL_FAIL), start/stop lines; 80 ca-80 cf locate records not sent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md); IS 0 (stored) | partial |
| 622 Siphon Data Node | memory | Awakened / Data Miner | Tap Data Node Skill raw ? × native function 2 (while enabled) | IS 0 (stored); loaded passive totals + records (@50: Tap Data Node Skill +50 [total only]) | cosmetic |
| 628 Concentration | memory | Awakened / Awakened | Concentration raw 5..? × native function 2 / scale 0.5 (while enabled) | loaded passive totals + records (@50: Concentration +30 [total only]) | cosmetic |
| 629 Impart Invisibility | memory | Operative / Spy / Shadow | [InvisibilityImparted_Deactivate](../research/client-scripts/ability/spy/misc.py#L390), [InvisibilityImparted_DirectObject](../research/client-scripts/ability/spy/misc.py#L378), [InvisibilityImparted_Test](../research/client-scripts/ability/spy/misc.py#L397) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md); IS 0 (stored); reuse 30s | partial |
| 647 Taunt | memory | Operative / Soldier | [TauntAggro](../research/client-scripts/tempmods.py#L1281) | IS 10 (stored); reuse 10s; TauntAggro: NPCs in radius hate caster most and turn on them; humans unaffected | partial |
| 692 Deflect Bullets 1.0 | memory | Hacker / Force Enhancer | Ballistic Defense raw ? × native function 3 / scale 0.96 (5.0s); Thrown Defense raw ? × native function 3 / scale 0.48 (5.0s); Concentration raw 10 (5.0s); [DeflectBullets1Effect_DirectObject](../research/client-scripts/ability/hacker/virii.py#L21), [DoNothing](../research/client-scripts/tempmods.py#L1299) | IS 0 (stored); reuse 10s; buff records + caster-level stat snapshot; combat defence | partial |
| 693 Deflect Virus 1.0 | memory | Hacker / Force Enhancer | Viral Defense raw ? × native function 3 / scale 0.96 (5.0s); Melee Defense raw ? × native function 3 / scale 0.48 (5.0s); ISDrain Target - this string should never be seen. raw 4 (5.0s); [DeflectVirus1Effect_DirectObject](../research/client-scripts/ability/hacker/virii.py#L29), [DoNothing](../research/client-scripts/tempmods.py#L1299), [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 0 (stored); reuse 10s | missing |
| 694 Static Field | memory | Hacker / Pathogenist | viral raw 1 (instant) | IS 0 (stored); reuse 5s; generic hit 50..80 / CSV value; scaling unresolved | partial |
| 695 Combat Aura 1.0 | memory | Hacker / Team Patcher | Melee Accuracy raw ? × native function 3 / scale 1.3 (5.0s); Ballistic Accuracy raw ? × native function 3 / scale 1.3 (5.0s) | IS 0 (stored); buff records + caster-level stat snapshot; combat accuracy | partial |
| 696 Combat Aura 2.0 | memory | Hacker / Force Multiplier | Ballistic Damage raw ? × native function 3 / scale 0.5 (5.0s); Melee Damage raw ? × native function 3 / scale 0.5 (5.0s) | IS 0 (stored); reuse 10s; buff records + caster-level stat snapshot; damage | partial |
| 697 Code Shaper | memory | Coder / Code Shaper | Viral Defense raw ? × native function 3 / scale 0.96 (5.0s) | IS 0 (stored) | cosmetic |
| 698 Proxy Technician | memory | Coder / Proxy Technician | Melee Accuracy raw ? × native function 3 / scale 1.3 (5.0s); Ballistic Accuracy raw ? × native function 3 / scale 1.3 (5.0s) | IS 0 (stored); reuse 15s | cosmetic |
| 699 Daemon Boost 1.0 | memory | Coder / Utility Technician | Viral Damage raw ? × native function 3 / scale 0.5 (5.0s); Healing raw ? × native function 3 / scale 0.5 (5.0s) | IS 0 (stored); reuse 15s; Daemon Boost 1.0 pulse: simulacrum damage/heal +level x0.5 for 5s | partial |
| 700 Daemon Boost 2.0 | memory | Coder / Utility Master | Melee Accuracy raw ? × native function 3 / scale 1.3 (5.0s); Ballistic Accuracy raw ? × native function 3 / scale 1.3 (5.0s); Viral Accuracy raw ? × native function 3 / scale 1.3 (5.0s) | IS 0 (stored); reuse 15s; Daemon Boost 2.0 pulse: recorded; simulacra have no accuracy roll | partial |
| 701 Miasma 1.0 | memory | Hacker / Pathogenist | Melee Defense raw -14 (5.0s); Ballistic Defense raw -14 (5.0s) | IS 0 (stored); reuse 28s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 860 Technique | memory | Martial Artist / Zen Master | Aikido Damage raw ? × native function 3 (while enabled); Viral Defense raw ? × native function 3 / scale 2.0 (while enabled) | loaded passive totals + records (@50: Aikido Damage +50 [total only], Viral Defense +100 [total only]) | cosmetic |
| 861 Balance | memory | Martial Artist / Zen Master | [ApplyAuraEffectOnHealthChange](../research/client-scripts/tempmods.py#L1327) | IS 50 (capture); reuse 60s | missing |
| 862 Balance | memory | Martial Artist / Zen Master | health effect raw 50..100 (instant) | IS 0 (stored); generic hit 50..80 / CSV value; scaling unresolved | partial |
| 868 Purity | memory | Martial Artist / Zen Master | [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 20 (capture); reuse 15s; instant25% cleanse of debuff parents (UDB0, not exempt) | partial |
| 869 Sweep The Floor | memory | Martial Artist / Zen Master | Disarmed (20.0s), 25% proc; physical raw 312 (instant) | IS 40 (stored); reuse 10s / group 687 5s; generic hit 50..80 / CSV value; scaling unresolved; Disarmed 20s | partial |
| 870 Spinning Flower | memory | Martial Artist / Zen Master | Powerless (15.0s); physical raw 325 (instant); Combat Tactics raw -10 (15.0s) | IS 40 (capture); reuse 10s / group 688 5s; generic hit 50..80 / CSV value; scaling unresolved; Powerless 24.6s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 871 Zen Master | memory | Martial Artist / Zen Master | [ApplyAuraEffectOnHealthChange](../research/client-scripts/tempmods.py#L1327) | presentation only; no effect executor | cosmetic |
| 872 Zen Master | memory | Martial Artist / Zen Master | Current Inner Strength raw 5 (instant) | presentation only; no effect executor | missing |
| 940 Elusive Code | memory | Hacker / Trojan Horse | Combat Evasion raw ? × native function 3 / scale 1.0 (while enabled); Ballistic Damage Resistance raw ? × native function 3 / scale 0.5 (while enabled) | loaded passive totals + records (@50: Combat Evasion +50 [total only], Ballistic Damage Resistance +25 [combat resistance]) | partial |
| 941 Reactive Code | memory | Hacker / Trojan Horse | ISDrain Target - this string should never be seen. raw 7 (while enabled); [ApplyAuraEffectOnHealthChange](../research/client-scripts/tempmods.py#L1327), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 7 (stored); reuse 15s | missing |
| 942 Reactive Code | memory | Hacker / Trojan Horse | health effect raw 50..150 (instant) | IS 0 (stored); generic hit 50..80 / CSV value; scaling unresolved | partial |
| 943 Code Conversion | memory | Hacker / Trojan Horse | ISDrain Target - this string should never be seen. raw 6 (while enabled); [ApplyNextAuraEffectHealthChance](../research/client-scripts/tempmods.py#L1332), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 6 (stored); reuse 15s | missing |
| 945 Trojan Horse | memory | Hacker / Trojan Horse | ISDrain Target - this string should never be seen. raw 20 (while enabled); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995) | IS 20 (stored); reuse 15s; toggled aura: IS drain 20/4s, pulses 946 on hostile NPC/PvP targets in its effect radius | partial |
| 946 Trojan Horse | memory | Hacker / Trojan Horse | Current Inner Strength raw -20 (instant) | IS 0 (stored); instant script: removable buffs (UBF0) / current IS on landing | partial |
| 947 Packet Loss 1.0 | memory | Hacker / Trojan Horse | Rooted (10.0s), 10% proc; viral raw 62..300 (instant) | IS 20 (stored); reuse 4s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved; Rooted 10s | partial |
| 948 Packet Loss 2.0 | memory | Hacker / Trojan Horse | Stun (10.0s), 15% proc; viral raw 100..420 (instant) | IS 24 (stored); reuse 4s / group 682 4s; generic hit 50..80 / CSV value; scaling unresolved; Stun 10s | partial |
| 969 Compile Items | grant, grant | Coder / Code Crafter | Veteran raw ? (while enabled); [CompileActivate_Deactivate](../research/client-scripts/ability/hacker/crafting.py#L27), [CompileActivate_Subject](../research/client-scripts/ability/hacker/crafting.py#L21) | presentation only; no effect executor | missing |
| 970 Write Code | grant, grant | Coder / Coder | Veteran raw ? (while enabled); [WriteCodeActivateTerm](../research/client-scripts/tempmods.py#L3079), [WriteCodeActivate_Deactivate](../research/client-scripts/ability/hacker/crafting.py#L14), [WriteCodeActivate_Subject](../research/client-scripts/ability/hacker/crafting.py#L8) | IS 0 (stored) | missing |
| 1006 Hacker Style | memory | Hacker / Hacker | Viral Accuracy raw 6..? × native function 2 / scale 1.3 (while enabled); Viral Damage raw 4..? × native function 2 / scale 0.47 (while enabled); Melee Defense raw ? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw ? × native function 2 / scale 0.96 (while enabled); Viral Defense raw ? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw ? × native function 2 / scale 0.96 (while enabled); Healing raw 4..? × native function 2 / scale 0.47 (while enabled); Contested Withdraw raw ? × native function 2 / scale 1.3 (while enabled) | IS 0 (stored); active-style modifiers via 3e/80 b9 (@50: Healing +27, Melee Defense +47, Ballistic Defense +47, Thrown Defense +47, Viral Accuracy +70, Viral Damage +27, Viral Defense +47, Contested Withdraw +64); style interlock exchanges | partial |
| 1008 Coder Style | memory | Coder / Coder | Melee Defense raw ? × native function 2 / scale 0.96 (while enabled); Viral Defense raw ? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw ? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw ? × native function 2 / scale 0.96 (while enabled); Melee Accuracy raw 0..? × native function 2 / scale 1.3 (while enabled); Viral Accuracy raw 0..? × native function 2 / scale 1.3 (while enabled); Contested Withdraw raw 2..? × native function 2 / scale 1.3 (while enabled); Melee Damage raw 4..? × native function 2 / scale 0.45 (while enabled); Viral Damage raw 4..? × native function 2 / scale 0.45 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Melee Accuracy +64, Melee Damage +26, Melee Defense +47, Ballistic Defense +47, Thrown Defense +47, Viral Accuracy +64, Viral Damage +26, Viral Defense +47, Contested Withdraw +66); style interlock exchanges | partial |
| 1022 Enhanced Wooden Stake Attack | memory | Legacy / special weapon / Shared / no stored parent | [WoodenStakePlusAttack_DirectObject](../research/client-scripts/ability/general.py#L72) | general.py damage 10, x75 vs NPCKilledOnlyByWood (NPC weakness by name: server policy) | partial |
| 1023 Enhanced Holy Water Attack | memory | Legacy / special weapon / Shared / no stored parent | [HolyWaterPlusAttack_DirectObject](../research/client-scripts/ability/general.py#L140) | general.py damage 10, x75 vs NPCKilledOnlyByHolyWater (NPC weakness by name: server policy) | partial |
| 1024 Enhanced Silver Bullet Attack | memory | Legacy / special weapon / Shared / no stored parent | [SilverBulletPlusAttack_DirectObject](../research/client-scripts/ability/general.py#L90) | general.py damage 10, x75 vs NPCKilledOnlyBySilver (NPC weakness by name: server policy) | partial |
| 1025 EMCP Blast | memory | Legacy / special weapon / Shared / no stored parent | [EMPBlast_DirectObject](../research/client-scripts/ability/general.py#L157) | reuse 30s | missing |
| 1060 Operative Upgrade | memory, grant | Shared / unassigned / Operative | Melee Accuracy raw 4 (while enabled); Ballistic Accuracy raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1061 Soldier Upgrade | memory, grant | Shared / unassigned / Soldier | Melee Damage Resistance raw 20 (while enabled); Ballistic Damage Resistance raw 20 (while enabled); Max Health raw 50 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1062 Martial Arts Initiate Upgrade | memory, grant | Martial Artist / Martial Arts Initiate | Melee Accuracy raw 4 (while enabled); Max Health raw 70 (while enabled); Melee Damage raw 2 (while enabled); Melee Defense raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1063 Kung Fu Master Upgrade | memory, grant | Martial Artist / Kung Fu Master | Melee Accuracy raw 6 (while enabled); Max Health raw 90 (while enabled); Melee Damage raw 2 (while enabled); Melee Defense raw 4 (while enabled) | IS 20 (capture); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1064 Kung Fu Grandmaster Upgrade | memory, grant | Martial Artist / Kung Fu Grandmaster | Melee Accuracy raw 8 (while enabled); Max Health raw 120 (while enabled); Melee Damage raw 4 (while enabled); Melee Defense raw 5 (while enabled) | IS 20 (capture); reuse 30s; buff records + selected combat/vital stats (capture) | partial |
| 1065 Karate Master Upgrade | memory, grant | Martial Artist / Karate Master | Melee Accuracy raw 5 (while enabled); Max Health raw 90 (while enabled); Melee Damage raw 4 (while enabled); Melee Defense raw 5 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1066 Karate Grandmaster Upgrade | memory, grant | Martial Artist / Karate Grandmaster | Melee Accuracy raw 7 (while enabled); Max Health raw 110 (while enabled); Melee Damage raw 6 (while enabled); Melee Defense raw 6 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1067 Aikido Master Upgrade | memory, grant | Martial Artist / Aikido Master | Melee Accuracy raw 4 (while enabled); Max Health raw 110 (while enabled); Melee Damage raw 2 (while enabled); Melee Defense raw 6 (while enabled) | IS 20 (capture); reuse 30s; buff records + selected combat/vital stats (capture) | partial |
| 1069 Aikido Grandmaster Upgrade | memory, grant | Martial Artist / Aikido Grandmaster | Melee Accuracy raw 5 (while enabled); Max Health raw 150 (while enabled); Melee Damage raw 3 (while enabled); Melee Defense raw 8 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1070 Gunman Upgrade | memory, grant | Gunman / Gunman | Ballistic Accuracy raw 4 (while enabled); Max Health raw 70 (while enabled); Ballistic Damage raw 2 (while enabled); Ballistic Defense raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1071 Scattershot Upgrade | memory, grant | Gunman / Scattershot | Ballistic Accuracy raw 6 (while enabled); Max Health raw 90 (while enabled); Ballistic Damage raw 4 (while enabled); Ballistic Defense raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1072 SMG Specialist Upgrade | memory, grant | Gunman / SMG Specialist | Ballistic Accuracy raw 6 (while enabled); Max Health raw 110 (while enabled); Ballistic Damage raw 6 (while enabled); Ballistic Defense raw 7 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1073 Rifleman Upgrade | memory, grant | Gunman / Rifleman | Ballistic Accuracy raw 7 (while enabled); Max Health raw 90 (while enabled); Ballistic Damage raw 2 (while enabled); Ballistic Defense raw 3 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1074 Expert Rifleman Upgrade | memory, grant | Gunman / Expert Rifleman | Ballistic Accuracy raw 9 (while enabled); Max Health raw 120 (while enabled); Ballistic Damage raw 4 (while enabled); Ballistic Defense raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1075 Gunslinger Upgrade | memory, grant | Gunman / Gunslinger | Ballistic Accuracy raw 4 (while enabled); Max Health raw 100 (while enabled); Ballistic Damage raw 2 (while enabled); Ballistic Defense raw 6 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1076 Duelist Upgrade | memory, grant | Gunman / Duelist | Ballistic Accuracy raw 3 (while enabled); Max Health raw 110 (while enabled); Ballistic Damage raw 4 (while enabled); Ballistic Defense raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1077 Virologist Upgrade | grant, grant | Hacker / Virologist | Viral Resistance raw 20 (while enabled); Concentration raw 5 (while enabled); Viral Accuracy raw 3 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1078 Network Hacker Upgrade | grant, grant | Hacker / Network Hacker | Inner Strength Regen Rate raw 10 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1079 Skript Kiddie Upgrade | grant, grant | Hacker / Skript Kiddie | Inner Strength Regen Rate raw 10 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 6 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1080 Network Attacker Upgrade | grant, grant | Hacker / Network Attacker | Inner Strength Regen Rate raw 12 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 9 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1081 Ravager Downgrade | memory, grant | Hacker / Ravager | Viral Defense raw -12 (30.0s); Confuse (8.0s); [RemoveRandomBuff](../research/client-scripts/tempmods.py#L342) | IS 35 (stored); reuse 120s; Confuse 8s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; instant script: removable buffs (UBF0) / current IS on landing | partial |
| 1082 Selective Phage Upgrade | grant, grant | Hacker / Selective Phage | Viral Defense raw 2 (while enabled); Viral Damage raw 2 (while enabled); Viral Accuracy raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1083 Arbalest Upgrade | grant, grant | Hacker / Arbalest | Viral Damage raw 4 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 6 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1084 Ballista Upgrade | grant, grant | Hacker / Ballista | Viral Damage raw 10 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 9 (while enabled) | IS 20 (capture); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1085 Pathogenist Upgrade | grant, grant | Hacker / Pathogenist | Virus Area Radius raw 10 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 4 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1086 Artillerist Upgrade | grant, grant | Hacker / Artillerist | Viral Damage raw 2 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 6 (while enabled); Virus Area Radius raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1087 Howitzer Upgrade | grant, grant | Hacker / Howitzer | Viral Damage raw 5 (while enabled); Concentration raw 7 (while enabled); Viral Accuracy raw 9 (while enabled); Virus Area Radius raw 10 (while enabled) | IS 20 (capture); reuse 30s; buff records + selected combat/vital stats (capture) | partial |
| 1088 Destroyer | memory, grant | Hacker / Destroyer | Melee Damage Resistance raw -120 (30.0s); Viral Resistance raw -120 (30.0s); Ballistic Damage Resistance raw -120 (30.0s); Thrown Resistance raw -120 (30.0s) | IS 40 (capture); reuse 90s; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only | partial |
| 1089 Team Patcher Upgrade | grant, grant | Hacker / Team Patcher | Healing raw 3 (while enabled); Concentration raw 7 (while enabled); Combat Evasion raw 10 (while enabled); Heal and Upgrade Area Radius raw 5 (while enabled) | IS 20 (capture); reuse 30s; buff records + selected combat/vital stats (capture) | partial |
| 1091 Force Multiplier Upgrade | grant, grant | Hacker / Force Multiplier | Inner Strength Cost Reduction raw 10 (while enabled); Concentration raw 7 (while enabled); ISDrain Target - this string should never be seen. raw 5 (while enabled); [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 20 (stored); reuse 30s; pulse sweep removes cleansable debuff parents (UDB0, not exempt) | partial |
| 1092 Corpsman Upgrade | grant, grant | Hacker / Corpsman | Heal and Upgrade Area Radius raw 7 (while enabled); Concentration raw 7 (while enabled); Healing raw 5 (while enabled) | IS 20 (capture); reuse 30s; buff records + selected combat/vital stats (capture) | partial |
| 1093 Trauma Surgeon | grant, grant | Hacker / Trauma Surgeon | Heal and Upgrade Area Radius raw 10 (while enabled); Concentration raw 7 (while enabled); Healing raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1094 Guardian Patcher Upgrade | grant, grant | Hacker / Guardian Patcher | Healing raw 3 (while enabled); Concentration raw 7 (while enabled); Combat Evasion raw 20 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1095 Upgrade Artist Upgrade | grant, grant | Hacker / Upgrade Artist | Inner Strength Cost Reduction raw 5 (while enabled); Concentration raw 7 (while enabled); Combat Evasion raw 20 (while enabled); [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 20 (stored); reuse 30s; pulse sweep removes cleansable debuff parents (UDB0, not exempt) | partial |
| 1096 Upgrade Master Upgrade | memory, grant | Hacker / Upgrade Master | Ballistic Damage raw 25 (300.0s); Viral Damage raw 25 (300.0s); Thrown Damage raw 25 (300.0s); Max Buffer raw 200 (300.0s); Melee Damage raw 25 (300.0s) | IS 60 (stored); reuse 10s; buff records + selected combat/vital stats (stored constants) | partial |
| 1097 Doctor Upgrade | grant, grant | Hacker / Doctor | Healing raw 5 (while enabled); Concentration raw 7 (while enabled); Combat Evasion raw 20 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1098 Physician Heal | memory, grant | Hacker / Physician | heal raw 180 (instant) | IS 70 (stored); reuse 120s; target/team heal; stored base 180; caster healing influence | partial |
| 1099 Hyper-Block | memory | Awakened / Awakened | Melee Defense raw ? × native function 2 / scale 0.96 (while enabled) | IS 20 (capture); buff records + selected combat/vital stats (capture) | partial |
| 1100 Hyper-Deflect | memory | Awakened / Awakened | Viral Defense raw ? × native function 2 / scale 0.96 (while enabled) | IS 20 (capture); buff records + selected combat/vital stats (capture) | partial |
| 1101 Hyper-Sense | memory | Awakened / Awakened | Thrown Defense raw ? × native function 2 / scale 0.96 (while enabled) | IS 20 (capture); buff records + learned-level stat snapshot; numeric totals only; gameplay consumer missing | cosmetic |
| 1102 Determination | memory | Awakened / Awakened | Determination raw ? × native function 2 / scale 3.0 (while enabled) | IS 50 (capture); reuse 30s; buff records + learned-level stat snapshot; numeric totals only; gameplay consumer missing | cosmetic |
| 1107 Proxy Master | memory, grant | Coder / Proxy Master | Inner Strength Cost Reduction raw 25 (600.0s); ISDrain Target - this string should never be seen. raw 10 (600.0s); Inner Strength Regen Rate raw 50 (600.0s); Health Regen Rate raw 50 (600.0s); [NegativeConditionSweepPulse](../research/client-scripts/tempmods.py#L182) | IS 200 (stored); reuse 15s; pulse sweep removes cleansable debuff parents (UDB0, not exempt) | partial |
| 1108 Utility Master | grant, grant | Coder / Utility Master | Melee Damage Resistance raw 20 (while enabled); Ballistic Damage Resistance raw 20 (while enabled); Max Health raw 150 (while enabled); Melee Defense raw 6 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1110 Bull Dog | memory | Operative / Spy / Spy | ISDrain Target - this string should never be seen. raw 10 (0.5s); [NeutralDOT](../research/client-scripts/tempmods.py#L961), [SneakFreePrototype_Subject](../research/client-scripts/ability/spy/sneaks.py#L172) | IS 10 (stored); reuse 10s / group 691 15s; stealth detection/AI incomplete | missing |
| 1111 Sneak | grant, grant | Operative / Spy / Spy | Stealth raw ? × native function 2 (while enabled); Noise Level Modifier raw 10 (while enabled); Stealthed (while enabled); Movement Speed raw -200 (while enabled); [OnHealthReduceDeactivateAbility](../research/client-scripts/tempmods.py#L1045), [SneakGrant_Test](../research/client-scripts/ability/spy/sneaks.py#L20), [SneakInit](../research/client-scripts/tempmods.py#L459), [SneakTerm](../research/client-scripts/tempmods.py#L467), [SneakUpdate](../research/client-scripts/tempmods.py#L472) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md) | partial |
| 1112 Spy Style | memory | Operative / Spy / Spy | Thrown Accuracy raw 2..? × native function 2 / scale 1.3 (while enabled); Thrown Damage raw 4..? × native function 2 / scale 0.5 (while enabled); Melee Accuracy raw 4..? × native function 2 / scale 1.1 (while enabled); Melee Damage raw 3..? × native function 2 / scale 0.34 (while enabled); Melee Defense raw 0..? × native function 2 / scale 0.96 (while enabled); Ballistic Defense raw 0..? × native function 2 / scale 0.96 (while enabled); Viral Defense raw 0..? × native function 2 / scale 0.96 (while enabled); Thrown Defense raw 0..? × native function 2 / scale 0.96 (while enabled); Contested Withdraw raw 8..? × native function 2 / scale 1.3 (while enabled) | active-style modifiers via 3e/80 b9 (@50: Melee Accuracy +59, Melee Damage +20, Melee Defense +47, Ballistic Defense +47, Thrown Accuracy +66, Thrown Damage +29, Thrown Defense +47, Viral Defense +47, Contested Withdraw +72); style interlock exchanges | partial |
| 1113 Spy Upgrade | memory, grant | Operative / Spy / Spy | Melee Defense raw 3 (while enabled); Thrown Accuracy raw 3 (while enabled); Thrown Damage raw 2 (while enabled); Stealth raw 8 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1114 Disguise | memory, grant | Operative / Spy / Infiltrator | [DisguiseGrant_DirectObject](../research/client-scripts/ability/spy/misc.py#L238), [DisguiseGrant_Test](../research/client-scripts/ability/spy/misc.py#L278) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md) | partial |
| 1115 Infiltrator Upgrade | memory, grant | Operative / Spy / Infiltrator | Melee Defense raw 3 (while enabled); Thrown Accuracy raw 3 (while enabled); Disguise Duration raw 3 (while enabled); Sneak Movement raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1116 Invisibility | grant, grant | Operative / Spy / Shadow | [InvisibilityGrant_Deactivate](../research/client-scripts/ability/spy/misc.py#L324), [InvisibilityGrant_DirectObject](../research/client-scripts/ability/spy/misc.py#L303), [InvisibilityGrant_Test](../research/client-scripts/ability/spy/misc.py#L331) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md); IS 50 (stored); reuse 30s | partial |
| 1117 Shadow Upgrade | memory, grant | Operative / Spy / Shadow | Contested Withdraw raw 6 (while enabled); Thrown Accuracy raw 5 (while enabled); Invisibility Duration raw 3 (while enabled); Invisibility Movement raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1118 Master Shadow | grant, grant | Operative / Spy / Master Shadow | ISDrain Target - this string should never be seen. raw 30 (180.0s); [AddRecastTimer](../research/client-scripts/tempmods.py#L1247), [ISDrainWithAbilityDeactivation](../research/client-scripts/tempmods.py#L995), [ShadowObjectInit](../research/client-scripts/tempmods.py#L589), [ShadowObjectTerm](../research/client-scripts/tempmods.py#L579) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md); IS 12 (stored) | partial |
| 1119 Cutthroat Upgrade | memory, grant | Operative / Spy / Cutthroat | Thrown Damage raw 3 (while enabled); Thrown Accuracy raw 5 (while enabled); Combat Evasion raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1120 Knife Thrower Upgrade | memory, grant | Operative / Spy / Knife Thrower | Thrown Damage raw 4 (while enabled); Thrown Accuracy raw 15 (while enabled); Combat Evasion raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1121 Master Knife Thrower Upgrade | memory, grant | Operative / Spy / Master Knife Thrower | Thrown Damage raw 6 (while enabled); Thrown Accuracy raw 18 (while enabled); Combat Evasion raw 10 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1122 Assassin Upgrade | memory, grant | Operative / Spy / Assassin | Thrown Damage raw 6 (while enabled); Thrown Accuracy raw 5 (while enabled); Stealth raw 5 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1123 Master Assassin Upgrade | memory, grant | Operative / Spy / Master Assassin | Thrown Damage raw 10 (while enabled); Thrown Accuracy raw 7 (while enabled); Stealth raw 8 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1124 Disrupt Connection | memory | Operative / Spy / Master Assassin | Native action / passive metadata; no decoded stored modifier | IS 25 (stored) | missing |
| 1125 Disable Evasion | memory | Operative / Spy / Spy | ISDrain Target - this string should never be seen. raw ? (8.0s); [DisableEvasion](../research/client-scripts/tempmods.py#L2380), [EnableEvasion](../research/client-scripts/tempmods.py#L2385), [SneakFreeDisableShield_Test](../research/client-scripts/ability/spy/misc.py#L719) | IS 25 (stored); reuse 45s; stealth detection/AI incomplete | missing |
| 1127 Harrying Attack | memory | Operative / Spy / Infiltrator | Dazed (12.0s), 30% proc; Staggered (12.0s), 30% proc; Off-Balance (12.0s), 30% proc; thrown raw 16..76 (instant); [SneakFreeHarryingAttack_Subject](../research/client-scripts/ability/spy/sneaks.py#L177) | IS 25 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Staggered 12s, Off-Balance 12s, Dazed 12s; stealth detection/AI incomplete | partial |
| 1128 Distracting Blow | memory | Operative / Spy / Shadow | thrown raw 17..114 (instant); [ApplyAuraEffect](../research/client-scripts/tempmods.py#L1308), [SneakFreeDistractingBlow_Subject](../research/client-scripts/ability/spy/sneaks.py#L182) | IS 35 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; stealth detection/AI incomplete | partial |
| 1130 Infuriating Assault | memory | Operative / Spy / Master Shadow | thrown raw 17..132 (instant); Enraged (4.0s); [SneakFreeInfuriatingAssault_Subject](../research/client-scripts/ability/spy/sneaks.py#L187) | IS 40 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Enraged 4s; stealth detection/AI incomplete | partial |
| 1131 Leg Throw | memory | Operative / Spy / Cutthroat | thrown raw 24..154 (instant); Off-Balance (12.0s), 50% proc; [SneakFreeLegThrow_Subject](../research/client-scripts/ability/spy/sneaks.py#L192) | IS 25 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Off-Balance 12s; stealth detection/AI incomplete | partial |
| 1132 Dazzling Throw | memory | Operative / Spy / Knife Thrower | thrown raw 43 (instant); Dazed (12.0s), 50% proc; [SneakFreeDazzlingThrow_Subject](../research/client-scripts/ability/spy/sneaks.py#L197) | IS 35 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; Dazed 12s; stealth detection/AI incomplete | partial |
| 1133 Weakening Blow | memory | Operative / Spy / Assassin | thrown raw 21..113 (instant); Ballistic Damage Resistance raw -45 (8.0s); Melee Damage Resistance raw -45 (8.0s); [SneakFreeWeakeningThrow_Subject](../research/client-scripts/ability/spy/sneaks.py#L207) | IS 35 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; stealth detection/AI incomplete | partial |
| 1134 Warding Blow | memory | Operative / Spy / Master Assassin | Melee Accuracy raw -30 (8.0s); thrown raw 25..142 (instant); Ballistic Accuracy raw -30 (8.0s); [SneakFreeWardingBlow_Subject](../research/client-scripts/ability/spy/sneaks.py#L212) | IS 45 (stored); reuse 45s / group 691 15s; generic hit 50..80 / CSV value; scaling unresolved; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; stealth detection/AI incomplete | partial |
| 1135 Paralyzing Throw | memory | Operative / Spy / Master Assassin | Rooted (8.0s); thrown raw 30 (instant); Force Combat raw -20 (8.0s); ISDrain Target - this string should never be seen. raw -100 (8.0s); [ApplyStunEffectIfDazed](../research/client-scripts/tempmods.py#L2368), [OnHealthReduceDeactivateChance](../research/client-scripts/tempmods.py#L1055) | IS 30 (stored); reuse 6s / group 1216 3s; generic hit 50..80 / CSV value; scaling unresolved; Rooted 8s / break Chance; stored debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only; special move: needs Spy Style (auto-switch when loaded) | partial |
| 1136 Sniper Upgrade | memory, grant | Gunman / Sniper | Ballistic Accuracy raw 10 (while enabled); Max Health raw 60 (while enabled); Ballistic Damage raw 4 (while enabled); Ballistic Defense raw 1 (while enabled) | IS 20 (stored); reuse 30s; buff records + selected combat/vital stats (stored constants) | partial |
| 1137 Concealment Grant | grant, grant | Gunman / Sniper | Stealth raw ? × native function 2 / scale 1.0 (while enabled); Concealed (while enabled); Stealthed (while enabled); [ConcealInit](../research/client-scripts/tempmods.py#L2155), [ConcealTerm](../research/client-scripts/tempmods.py#L2172), [ConcealmentGrant_Deactivate](../research/client-scripts/ability/spy/sneaks.py#L40), [ConcealmentGrant_DirectObject](../research/client-scripts/ability/spy/sneaks.py#L30), [ConcealmentGrant_Test](../research/client-scripts/ability/spy/sneaks.py#L48), [OnHealthReduceDeactivateAbility](../research/client-scripts/tempmods.py#L1045) | stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md); reuse 15s | partial |
| 1149 Desperation | memory | Awakened / Awakened | Initiative raw ? (20.0s); [DesperationFunction](../research/client-scripts/tempmods.py#L2402) | IS 10 (stored); reuse 90s | missing |
| 1180 System Control | memory | Awakened / Organization | Stun (5.0s); [AddControlImmunityTimerVal](../research/client-scripts/tempmods.py#L1639) | Stun 5s | partial |
| 1181 Champion of Zion | memory | Awakened / Organization | health effect raw 250 (instant); [ZionRestoreIS](../research/client-scripts/tempmods.py#L2654) | generic hit 50..80 / CSV value; scaling unresolved | partial |
| 1182 Retribution | memory | Awakened / Organization | health effect raw 150 (instant) | generic hit 50..80 / CSV value; scaling unresolved | partial |
| 1199 Organization | discipline | Awakened / Organization | Native action / passive metadata; no decoded stored modifier | presentation only; no effect executor | cosmetic |

## Canonical inventory outside the player table

These records are not silently dropped. Scope is the stored class or an explicit NPC name; other class-0 records require evidence that they are loadable rather than item/event/internal actions. Their full decoded fields remain in the JSON catalog.

| ID | Template | Scope / exclusion |
|---:|---|---|
| 78 | FocusAttributeAbility | core attribute |
| 79 | PerceptionAttributeAbility | core attribute |
| 81 | ReasonAttributeAbility | core attribute |
| 82 | BeliefAttributeAbility | core attribute |
| 84 | VitalityAttributeAbility | core attribute |
| 85 | MezAbility | engine state |
| 87 | HasteAbility | engine state |
| 88 | InnerstrengthSitRegenRateAbility | engine stat |
| 90 | BlindAbility | engine state |
| 91 | ConfuseAbility | engine state |
| 93 | EnrageAbility | engine state |
| 94 | ImpairConnectionAbility | engine state |
| 96 | PowerlessAbility | engine state |
| 99 | RootedAbility | engine state |
| 100 | SlowAbility | engine state |
| 102 | StunAbility | engine state |
| 103 | WoundedAbility | engine state |
| 106 | MaxHealthAbility | engine stat |
| 108 | CurrentHealthAbility | engine stat |
| 109 | MaxInnerstrengthAbility | engine stat |
| 111 | CurrentInnerstrengthAbility | engine stat |
| 112 | HealthRegenRateAbility | engine stat |
| 114 | InnerstrengthRegenRateAbility | engine stat |
| 115 | MovementSpeedAbility | engine stat |
| 117 | InnerstrengthCostModifierAbility | engine stat |
| 127 | BuffAreaRadiusAbility | engine stat |
| 144 | DamageResistanceAbility | engine stat |
| 157 | ViralResistanceAbility | engine stat |
| 159 | MeleePhysicalDamageAbility | engine stat |
| 160 | AllAbilityLevelsAbility | engine stat |
| 162 | MaxBuffBufferAbility | engine stat |
| 163 | CombatTacticsAbility | engine stat |
| 168 | WithdrawCombatAbility | engine state |
| 171 | RangePhysicalDamageAbility | engine stat |
| 172 | ViralDamageAbility | engine stat |
| 174 | HealDamageAbility | engine stat |
| 177 | DebuffAreaRadiusAbility | engine stat |
| 178 | InvokedDebuffTimesAbility | engine stat |
| 179 | InvokedBuffTimesAbility | engine stat |
| 180 | MeleeDamageResistanceAbility | engine stat |
| 181 | RangedDamageResistanceAbility | engine stat |
| 182 | StealthDamageResistanceAbility | engine stat |
| 183 | ViralDeflectionAbility | engine stat |
| 187 | PillBolsterHealthAbility | item / event / internal / unproven loadability |
| 188 | PillBolsterHealth2Ability | item / event / internal / unproven loadability |
| 189 | PillBolsterHealth3Ability | item / event / internal / unproven loadability |
| 190 | PillHealthReplenish1Ability | item / event / internal / unproven loadability |
| 191 | PillHealthReplenish2Ability | item / event / internal / unproven loadability |
| 192 | PillHealthReplenish3Ability | item / event / internal / unproven loadability |
| 193 | PillHealthReplenish4Ability | item / event / internal / unproven loadability |
| 194 | ConditionStatesAbility | engine stat |
| 195 | ForceCloseCombatAbility | item / event / internal / unproven loadability |
| 199 | DazeAbility | engine state |
| 200 | OffBalanceAbility | engine state |
| 201 | StaggeredAbility | engine state |
| 202 | DeathEffectAbility | engine stat |
| 203 | SpeedTacticAbility | item / event / internal / unproven loadability |
| 204 | StealthDetectionAbility | engine stat |
| 205 | GrabTacticAbility | item / event / internal / unproven loadability |
| 206 | PowerTacticAbility | item / event / internal / unproven loadability |
| 209 | StealthAbility | engine stat |
| 210 | TacticISCostModifierAbility | engine stat |
| 211 | VulnerabilityDurationModifierAbility | engine stat |
| 212 | DamageAgainstVulnerabilityAbility | engine stat |
| 213 | VulnerabilityGenerationPercentAbility | engine stat |
| 214 | SelectionRangeAbility | engine stat |
| 216 | JackoutAbility | engine state |
| 219 | SpeedTacticsAbility | engine stat |
| 220 | PowerTacticsAbility | engine stat |
| 221 | GrabTacticsAbility | engine stat |
| 222 | GrabDamageAbility | engine stat |
| 223 | SpeedDamageAbility | engine stat |
| 224 | PowerDamageAbility | engine stat |
| 225 | GrabGunDamageAbility | engine stat |
| 226 | PowerGunDamageAbility | engine stat |
| 227 | SpeedGunDamageAbility | engine stat |
| 228 | GrabGunTacticsAbility | engine stat |
| 229 | PowerGunTacticsAbility | engine stat |
| 230 | SpeedGunTacticsAbility | engine stat |
| 231 | DisarmedAbility | engine state |
| 232 | CombatDisarmedAbility | item / event / internal / unproven loadability |
| 299 | SuspiciousCynic | personality archetype |
| 300 | TrueBeliever | personality archetype |
| 301 | TroubledIntellectual | personality archetype |
| 302 | NewAgeHippy | personality archetype |
| 303 | FanaticSelfImprover | personality archetype |
| 304 | DevotedAscetic | personality archetype |
| 305 | LunaticFringe | personality archetype |
| 306 | InquisitiveGenius | personality archetype |
| 307 | SecludedIntrovert | personality archetype |
| 308 | DetachedSpectator | personality archetype |
| 316 | DodgeAbility | engine stat |
| 342 | ViralTransmissionAbility | engine stat |
| 432 | NPCKilledOnlyByHolyWaterAbility | NPC |
| 443 | NPCAbility | NPC |
| 444 | NPCBoostUnderlingsAbility | NPC |
| 446 | NPCBloodDrinkAbility | NPC |
| 447 | NPCHealthRegenLowAbility | NPC |
| 448 | NPCTwinRazor2Ability | NPC |
| 449 | NPCPounceAbility | NPC |
| 451 | NPCTwinRazorAbility | NPC |
| 456 | NPCFeralScentAbility | NPC |
| 457 | NPCHealthRegenMediumAbility | NPC |
| 458 | NPCHealthRegenHighAbility | NPC |
| 460 | NPCImmortalAbility | NPC |
| 463 | NPCKilledOnlyBySilverAbility | NPC |
| 464 | NPCKilledOnlyByWoodAbility | NPC |
| 509 | NoiseLevelBuffTargetAbility | engine stat |
| 510 | RippleMagBuffTargetAbility | engine stat |
| 511 | PillHealthReplenish5Ability | item / event / internal / unproven loadability |
| 519 | ForceCombatAbility | engine stat |
| 520 | EvadeInterlockAbility | engine stat |
| 522 | MeleeCombatTacticsAbility | engine stat |
| 523 | StealthDamageAbility | engine stat |
| 524 | RangeCombatTacticsAbility | engine stat |
| 525 | InnerstrengthStandRegenRateAbility | engine stat |
| 526 | HealthSitRegenRateAbility | engine stat |
| 527 | HealthStandRegenRateAbility | engine stat |
| 529 | SignalProtectionAbility | engine stat |
| 530 | CraftingCostDiscountAbility | engine stat |
| 545 | CrashStateAbility | engine state |
| 548 | TacticBoost1Ability | item / event / internal / unproven loadability |
| 549 | TacticBoost2Ability | item / event / internal / unproven loadability |
| 550 | TacticBoost3Ability | item / event / internal / unproven loadability |
| 551 | TacticBoost4Ability | item / event / internal / unproven loadability |
| 552 | RifleDamageAbility | engine stat |
| 553 | HandGunDamageAbility | engine stat |
| 554 | SubmachineGunDamageAbility | engine stat |
| 555 | HandGunCombatTacticsAbility | engine stat |
| 556 | RifleCombatTacticsAbility | engine stat |
| 557 | SubmachineGunCombatTacticsAbility | engine stat |
| 558 | AimmedShotTacticsAbility | engine stat |
| 559 | BurstTacticsAbility | engine stat |
| 560 | RetaliateTacticsAbility | engine stat |
| 561 | AimmedShotDamageAbility | engine stat |
| 562 | BurstDamageAbility | engine stat |
| 563 | RetaliateDamageAbility | engine stat |
| 564 | ConcentrationAbility | engine stat |
| 565 | NPCKissAbility | NPC |
| 566 | PreOrderHyperLeapAbility | item / event / internal / unproven loadability |
| 567 | SuperJumpAbility | engine stat |
| 568 | JumpAbility | engine stat |
| 569 | KarateCombatTacticsAbility | engine stat |
| 570 | KungFuCombatTacticsAbility | engine stat |
| 571 | AikidoCombatTacticsAbility | engine stat |
| 572 | AikidoDamageAbility | engine stat |
| 573 | KarateDamageAbility | engine stat |
| 574 | KungFuDamageAbility | engine stat |
| 575 | DeterminationAbility | engine stat |
| 576 | NPCCounterSneakAbility | NPC |
| 577 | SneakAttackDetectionAbility | engine stat |
| 592 | WeaponReqsNotMetEffectAbility | item / event / internal / unproven loadability |
| 606 | BlockTacticsAbility | engine stat |
| 623 | ConcealedAbility | engine state |
| 624 | EvasionStateAbility | engine state |
| 625 | NPCWeaklingPenaltyAbility | NPC |
| 626 | NPCToughPenaltyAbility | NPC |
| 627 | NPCDetectionAbility | NPC |
| 630 | NPCVirusBlindDoNotUseAbility | NPC |
| 633 | NPCWolfAttack4Ability | NPC |
| 634 | NPCWolfAttack1Ability | NPC |
| 635 | NPCWolfAttack2Ability | NPC |
| 636 | NPCWolfAttack3Ability | NPC |
| 637 | ConsistencyAbility | engine stat |
| 638 | DetectAbility | engine stat |
| 639 | DisguiseDurationAbility | engine stat |
| 640 | SecuritySystemsAbility | engine stat |
| 641 | MapDownloadingAbility | engine stat |
| 642 | AnalyzeWeaknessAbility | engine stat |
| 643 | ComputerHackingAbility | engine stat |
| 644 | InvisDurationAbility | engine stat |
| 645 | LockPickingAbility | engine stat |
| 646 | SixthSensingAbility | engine stat |
| 648 | StabilityLossReductionAbility | engine stat |
| 649 | CodeWritingAbility | engine stat |
| 650 | ItemDecompilingAbility | engine stat |
| 651 | ItemCompilingAbility | engine stat |
| 652 | ApparelCraftingAbility | engine stat |
| 653 | AbilityCraftingAbility | engine stat |
| 654 | ToolCraftingAbility | engine stat |
| 655 | WeaponCraftingAbility | engine stat |
| 656 | UpgradeCraftingAbility | engine stat |
| 657 | NPCMinorTacticBoostAbility | NPC |
| 658 | NPCMediumTacticBoostAbility | NPC |
| 659 | PillConditionSweep1Ability | item / event / internal / unproven loadability |
| 660 | StableMezAbility | engine state |
| 661 | NPCPenaltyBaseAbility | NPC |
| 662 | NPCDisguiseAbility | NPC |
| 663 | DazeEffectAbility | item / event / internal / unproven loadability |
| 664 | StaggeredEffectAbility | item / event / internal / unproven loadability |
| 665 | OffBalanceEffectAbility | item / event / internal / unproven loadability |
| 670 | CombatEffectAbility | engine state |
| 671 | SittingEffectAbility | engine state |
| 672 | PillBolsterHealth1Ability | item / event / internal / unproven loadability |
| 673 | PillBolsterHealth4Ability | item / event / internal / unproven loadability |
| 674 | Antibiotic1Ability | item / event / internal / unproven loadability |
| 675 | Antibiotic2Ability | item / event / internal / unproven loadability |
| 676 | Antibiotic3Ability | item / event / internal / unproven loadability |
| 677 | Antibiotic4Ability | item / event / internal / unproven loadability |
| 678 | Antibiotic5Ability | item / event / internal / unproven loadability |
| 681 | ISDrainTargetAbility | engine stat |
| 682 | aaVirusDamage1Ability | item / event / internal / unproven loadability |
| 683 | aaVirusEnhacedDamageAbility | item / event / internal / unproven loadability |
| 684 | aaLogicCannonAbility | item / event / internal / unproven loadability |
| 685 | aaHeal1Ability | item / event / internal / unproven loadability |
| 686 | aaHeal2Ability | item / event / internal / unproven loadability |
| 687 | aaMeleeMove1 | item / event / internal / unproven loadability |
| 688 | aaMeleeMove2 | item / event / internal / unproven loadability |
| 689 | aaRangeMove1 | item / event / internal / unproven loadability |
| 690 | aaRangeMove2 | item / event / internal / unproven loadability |
| 691 | aaSneakAttack | item / event / internal / unproven loadability |
| 702 | aaDOT1 | item / event / internal / unproven loadability |
| 703 | LocateDataNodeSkillAbility | engine stat |
| 704 | TapDataNodeSkillAbility | engine stat |
| 705 | RepairBonusAbility | engine stat |
| 706 | CompileSkillAbility | engine stat |
| 707 | DecompileSkillAbility | engine stat |
| 708 | RepairSkillAbility | engine stat |
| 709 | CryptographySkillAbility | engine stat |
| 710 | NullTargetAbility | engine stat |
| 711 | aaHealPillAbility | item / event / internal / unproven loadability |
| 712 | NPCHealthBoostAbility | NPC |
| 713 | aaRoot1Ability | item / event / internal / unproven loadability |
| 714 | NPCAssassinAbility | NPC |
| 715 | PVPEffectAbility | engine state |
| 716 | HealInhaler1Ability | item / event / internal / unproven loadability |
| 717 | HealInhaler2Ability | item / event / internal / unproven loadability |
| 718 | HealInhaler3Ability | item / event / internal / unproven loadability |
| 719 | HealInhaler4Ability | item / event / internal / unproven loadability |
| 720 | HealInhaler5Ability | item / event / internal / unproven loadability |
| 721 | PVPFlipperAbility | item / event / internal / unproven loadability |
| 722 | NPCChainFlameAbility | NPC |
| 723 | NPCChainFlameTriggerAbility | NPC |
| 724 | NPCChainFlameEffectAbility | NPC |
| 725 | NPCMorpheusSpeechAbility | NPC |
| 734 | ControlImmunityTimerAbility | item / event / internal / unproven loadability |
| 735 | PVPFlipper2Ability | item / event / internal / unproven loadability |
| 736 | PVPFlipper3Ability | item / event / internal / unproven loadability |
| 740 | aaAntibioticPillAbility | item / event / internal / unproven loadability |
| 741 | NPCAbilityCraftingAbility | NPC |
| 742 | NPCAikidoCombatTacticsAbility | NPC |
| 743 | NPCAikidoDamageAbility | NPC |
| 744 | NPCAimmedShotDamageAbility | NPC |
| 745 | NPCAimmedShotTacticsAbility | NPC |
| 746 | NPCAllAbilityLevelsAbility | NPC |
| 747 | NPCAnalyzeWeaknessAbility | NPC |
| 748 | NPCApparelCraftingAbility | NPC |
| 749 | NPCBeliefAttributeAbility | NPC |
| 750 | NPCBlockTacticsAbility | NPC |
| 751 | NPCBuffAreaRadiusAbility | NPC |
| 752 | NPCBurstDamageAbility | NPC |
| 753 | NPCBurstTacticsAbility | NPC |
| 754 | NPCCodeWritingAbility | NPC |
| 755 | NPCCombatTacticsAbility | NPC |
| 756 | NPCCompileSkillAbility | NPC |
| 757 | NPCComputerHackingAbility | NPC |
| 758 | NPCConcentrationAbility | NPC |
| 759 | NPCConsistencyAbility | NPC |
| 760 | NPCCraftingCostDiscountAbility | NPC |
| 761 | NPCCryptographySkillAbility | NPC |
| 762 | NPCCurrentHealthAbility | NPC |
| 763 | NPCCurrentInnerstrengthAbility | NPC |
| 764 | NPCDamageAgainstVulnerabilityAbility | NPC |
| 765 | NPCDamageResistanceAbility | NPC |
| 766 | NPCDebuffAreaRadiusAbility | NPC |
| 767 | NPCDecompileSkillAbility | NPC |
| 768 | NPCDetectAbility | NPC |
| 769 | NPCDeterminationAbility | NPC |
| 770 | NPCDisguiseDurationAbility | NPC |
| 771 | NPCDodgeAbility | NPC |
| 772 | NPCEvadeInterlockAbility | NPC |
| 773 | NPCFocusAttributeAbility | NPC |
| 774 | NPCForceCombatAbility | NPC |
| 775 | NPCGrabDamageAbility | NPC |
| 776 | NPCGrabGunDamageAbility | NPC |
| 777 | NPCGrabGunTacticsAbility | NPC |
| 778 | NPCGrabTacticsAbility | NPC |
| 779 | NPCHandGunCombatTacticsAbility | NPC |
| 780 | NPCHandGunDamageAbility | NPC |
| 781 | NPCHealDamageAbility | NPC |
| 782 | NPCHealthRegenRateAbility | NPC |
| 783 | NPCHealthSitRegenRateAbility | NPC |
| 784 | NPCHealthStandRegenRateAbility | NPC |
| 785 | NPCInnerstrengthCostModifierAbility | NPC |
| 786 | NPCInnerstrengthRegenRateAbility | NPC |
| 787 | NPCInnerstrengthSitRegenRateAbility | NPC |
| 788 | NPCInnerstrengthStandRegenRateAbility | NPC |
| 789 | NPCInvisDurationAbility | NPC |
| 790 | NPCInvokedBuffTimesAbility | NPC |
| 791 | NPCInvokedDebuffTimesAbility | NPC |
| 792 | NPCISDrainTargetAbility | NPC |
| 793 | NPCItemCompilingAbility | NPC |
| 794 | NPCItemDecompilingAbility | NPC |
| 795 | NPCJumpAbility | NPC |
| 796 | NPCKarateCombatTacticsAbility | NPC |
| 797 | NPCKarateDamageAbility | NPC |
| 798 | NPCKungFuCombatTacticsAbility | NPC |
| 799 | NPCKungFuDamageAbility | NPC |
| 800 | NPCLocateDataNodeSkillAbility | NPC |
| 801 | NPCLockPickingAbility | NPC |
| 802 | NPCMapDownloadingAbility | NPC |
| 803 | NPCMaxBuffBufferAbility | NPC |
| 804 | NPCMaxHealthAbility | NPC |
| 805 | NPCMaxInnerstrengthAbility | NPC |
| 806 | NPCMeleeCombatTacticsAbility | NPC |
| 807 | NPCMeleeDamageResistanceAbility | NPC |
| 808 | NPCMeleePhysicalDamageAbility | NPC |
| 809 | NPCMovementSpeedAbility | NPC |
| 810 | NPCNoiseLevelBuffTargetAbility | NPC |
| 811 | NPCNullTargetAbility | NPC |
| 812 | NPCPerceptionAbility | NPC |
| 813 | NPCPowerDamageAbility | NPC |
| 814 | NPCPowerGunDamageAbility | NPC |
| 815 | NPCPowerGunTacticsAbility | NPC |
| 816 | NPCPowerTacticsAbility | NPC |
| 817 | NPCRangeCombatTacticsAbility | NPC |
| 818 | NPCRangedDamageResistanceAbility | NPC |
| 819 | NPCRangePhysicalDamageAbility | NPC |
| 820 | NPCReasonAbility | NPC |
| 821 | NPCRepairBonusAbility | NPC |
| 822 | NPCRepairSkillAbility | NPC |
| 823 | NPCRetaliateDamageAbility | NPC |
| 824 | NPCRetaliateTacticsAbility | NPC |
| 825 | NPCRifleCombatTacticsAbility | NPC |
| 826 | NPCRifleDamageAbility | NPC |
| 827 | NPCRippleMagBuffTargetAbility | NPC |
| 828 | NPCSecuritySystemsAbility | NPC |
| 829 | NPCSignalProtectionAbility | NPC |
| 830 | NPCSixthSensingAbility | NPC |
| 831 | NPCSneakAttackDetectionAbility | NPC |
| 832 | NPCSpeedDamageAbility | NPC |
| 833 | NPCSpeedGunDamageAbility | NPC |
| 834 | NPCSpeedGunTacticsAbility | NPC |
| 835 | NPCSpeedTacticsAbility | NPC |
| 836 | NPCStabilityLossReductionAbility | NPC |
| 837 | NPCStealthAbility | NPC |
| 838 | NPCStealthDamageAbility | NPC |
| 839 | NPCStealthDamageResistanceAbility | NPC |
| 840 | NPCStealthDetectionAbility | NPC |
| 841 | NPCSubmachineGunCombatTacticsAbility | NPC |
| 842 | NPCSubmachineGunDamageAbility | NPC |
| 843 | NPCSuperJumpAbility | NPC |
| 844 | NPCTacticISCostModifierAbility | NPC |
| 845 | NPCTapDataNodeSkillAbility | NPC |
| 846 | NPCToolCraftingAbility | NPC |
| 847 | NPCUpgradeCraftingAbility | NPC |
| 848 | NPCViralDamageAbility | NPC |
| 849 | NPCViralDeflectionAbility | NPC |
| 850 | NPCViralResistanceAbility | NPC |
| 851 | NPCViralTransmissionAbility | NPC |
| 852 | NPCVitalityAttributeAbility | NPC |
| 853 | NPCVulnerabilityDurationModifierAbility | NPC |
| 854 | NPCVulnerabilityGenerationPercentAbility | NPC |
| 855 | NPCWeaponCraftingAbility | NPC |
| 863 | Event1_1Neo1Ability | item / event / internal / unproven loadability |
| 864 | Event1_1Neo2Ability | item / event / internal / unproven loadability |
| 865 | Event1_1Neo3Ability | item / event / internal / unproven loadability |
| 866 | Event1_1Neo4Ability | item / event / internal / unproven loadability |
| 867 | Event1_1Neo5Ability | item / event / internal / unproven loadability |
| 873 | NPCNeoRSIAbility | NPC |
| 874 | NPCEvadeStateFlipperAbility | NPC |
| 875 | Event1_1DeathAbility | item / event / internal / unproven loadability |
| 876 | Event1_1AOEAbility | item / event / internal / unproven loadability |
| 877 | SneakMovementSpeedAbility | engine stat |
| 878 | InvisMovementSpeedAbility | engine stat |
| 879 | SneakAttackCombatTacticsAbility | engine stat |
| 880 | NPCSneakAttackCombatTacticsAbility | NPC |
| 881 | StealthStateAbility | engine state |
| 883 | NPCStaggerAbility | NPC |
| 884 | NPCDazeAbility | NPC |
| 885 | NPCOffBalanceAbility | NPC |
| 886 | Event2_0FlyVirusAbility | item / event / internal / unproven loadability |
| 887 | Event2_0FlyVirusEffect1Ability | item / event / internal / unproven loadability |
| 888 | Event2_0FlyVirusEffect2Ability | item / event / internal / unproven loadability |
| 889 | Event2_0GMFlyVirusAbility | item / event / internal / unproven loadability |
| 890 | Event2_0GMCureAllAbility | item / event / internal / unproven loadability |
| 891 | NPCCantForceCombatAbility | NPC |
| 892 | NPCCantFreeAttackAbility | NPC |
| 893 | NPCCantBeFreeAttackedAbility | NPC |
| 894 | NPCCantBeForcedIntoCombatAbility | NPC |
| 895 | SusceptibleToLogicBomb1Ability | item / event / internal / unproven loadability |
| 896 | ImmunityToMyselfAbility | item / event / internal / unproven loadability |
| 897 | Event2_0CorruptingAuraAbility | item / event / internal / unproven loadability |
| 898 | Event2_0CorruptingAuraEffectAbility | item / event / internal / unproven loadability |
| 899 | Event2_0WaveOfCorruptionAbility | item / event / internal / unproven loadability |
| 900 | Event2_0WaveOfCorruptionEffectAbility | item / event / internal / unproven loadability |
| 901 | Event2_0CorruptingBileAbility | item / event / internal / unproven loadability |
| 902 | NPCUnDisguiseAbility | NPC |
| 903 | EventSlot1Ability | engine state |
| 904 | EventSlot2Ability | engine state |
| 905 | EventSlot3Ability | engine state |
| 908 | NPCShowInterlockImmuneAbility | NPC |
| 909 | NPCEventSlot1Ability | NPC |
| 910 | NPCEventSlot2Ability | NPC |
| 911 | NPCEventSlot3Ability | NPC |
| 912 | Event2_0FlyVirusImmunityAbility | item / event / internal / unproven loadability |
| 913 | Event2_0FlymanSusceptibilityAbility | item / event / internal / unproven loadability |
| 914 | Event2_0FlymanKillItemAbility | item / event / internal / unproven loadability |
| 915 | NPCImmobilizeAbility | NPC |
| 916 | Event2_0DeathAbility | item / event / internal / unproven loadability |
| 917 | NPCEventSlot1ToggleAbility | NPC |
| 918 | NPCEventSlot2ToggleAbility | NPC |
| 919 | NPCEventSlot3ToggleAbility | NPC |
| 920 | Event2_0DeathSelfAbility | item / event / internal / unproven loadability |
| 921 | NPCBufferOverflowSelfAbility | NPC |
| 922 | Event2_0Machine1Ability | item / event / internal / unproven loadability |
| 923 | Event2_0Merv1Ability | item / event / internal / unproven loadability |
| 924 | Event2_0Zion1Ability | item / event / internal / unproven loadability |
| 925 | Event2_0Machine2Ability | item / event / internal / unproven loadability |
| 926 | Event2_0Merv2Ability | item / event / internal / unproven loadability |
| 927 | Event2_0Zion2Ability | item / event / internal / unproven loadability |
| 928 | Event2_0Machine3Ability | item / event / internal / unproven loadability |
| 929 | Event2_0Merv3Ability | item / event / internal / unproven loadability |
| 930 | Event2_0Zion3Ability | item / event / internal / unproven loadability |
| 931 | Event2_0Machine4Ability | item / event / internal / unproven loadability |
| 932 | Event2_0Merv4Ability | item / event / internal / unproven loadability |
| 933 | Event2_0Zion4Ability | item / event / internal / unproven loadability |
| 934 | Event2_0BombEffectAbility | item / event / internal / unproven loadability |
| 935 | Event2_0BombExplodeAbility | item / event / internal / unproven loadability |
| 936 | Event2_0FlymanTransformAbility | item / event / internal / unproven loadability |
| 937 | Event2_0FlymanTransformPulseAbility | item / event / internal / unproven loadability |
| 938 | Event2_0FlymanTransformEffectAbility | item / event / internal / unproven loadability |
| 939 | Event2_0FlymanKillItem2Ability | item / event / internal / unproven loadability |
| 944 | CodeConversionEffectAbility | item / event / internal / unproven loadability |
| 949 | PrisonMentalityAbility | item / event / internal / unproven loadability |
| 950 | Event2_5KillCodeAbility | item / event / internal / unproven loadability |
| 952 | Event2_5KillCodeEffectAbility | item / event / internal / unproven loadability |
| 953 | Event2_5KillCodeTriggerAbility | item / event / internal / unproven loadability |
| 954 | CompileTimeReductionAbility | engine stat |
| 955 | ConsumableCompileTimeReductionAbility | engine stat |
| 956 | CodeWritingDiscountAbility | engine stat |
| 957 | PrisonMasteryAbility | item / event / internal / unproven loadability |
| 958 | aaPrisonMentalityFamilyAbility | item / event / internal / unproven loadability |
| 959 | DefenseTacticsAbility | engine stat |
| 960 | SignalBoosterAuraAbility | item / event / internal / unproven loadability |
| 961 | SignalBoosterEffectAbility | item / event / internal / unproven loadability |
| 962 | TransitionCoatAbility | item / event / internal / unproven loadability |
| 963 | TransitionGlassesAbility | item / event / internal / unproven loadability |
| 964 | DazeChanceAbility | engine stat |
| 965 | StaggeredChanceAbility | engine stat |
| 966 | OffBalanceChanceAbility | engine stat |
| 967 | MobiusCodeAbility | item / event / internal / unproven loadability |
| 968 | CraftingPlaceHolder | engine stat |
| 971 | AntiStunPill1Ability | item / event / internal / unproven loadability |
| 972 | AntiStunPill2Ability | item / event / internal / unproven loadability |
| 973 | aaAntiStunPillAbility | item / event / internal / unproven loadability |
| 974 | AssassinMaskAbility | item / event / internal / unproven loadability |
| 975 | AssassinTransformAbility | item / event / internal / unproven loadability |
| 976 | AssassinDeathTouchAbility | item / event / internal / unproven loadability |
| 977 | AssassinDeathPulseAbility | item / event / internal / unproven loadability |
| 978 | AssassinCorruptionWaveAbility | item / event / internal / unproven loadability |
| 979 | AssassinDisableAbility | item / event / internal / unproven loadability |
| 980 | FlitGunAbility | item / event / internal / unproven loadability |
| 981 | CorruptingCalmAbility | item / event / internal / unproven loadability |
| 982 | CorruptingFeastAbility | item / event / internal / unproven loadability |
| 983 | CorruptingFuryAbility | item / event / internal / unproven loadability |
| 984 | CorruptingShroudAbility | item / event / internal / unproven loadability |
| 985 | CorruptionPulseAbility | item / event / internal / unproven loadability |
| 986 | CorruptionWaveAbility | item / event / internal / unproven loadability |
| 987 | SignalCorruptionAbility | item / event / internal / unproven loadability |
| 988 | AssassinCrushAbility | item / event / internal / unproven loadability |
| 989 | AssassinRingAbility | item / event / internal / unproven loadability |
| 990 | AssassinImmunityAbility | item / event / internal / unproven loadability |
| 991 | CorruptCode1Ability | item / event / internal / unproven loadability |
| 992 | CorruptCode2Ability | item / event / internal / unproven loadability |
| 993 | AssassinMask2Ability | item / event / internal / unproven loadability |
| 994 | AssassinMask2EffectAbility | item / event / internal / unproven loadability |
| 995 | StunDurationBonusAbility | engine stat |
| 996 | BallisticAccuracyBonusAbility | engine stat |
| 997 | MeleeAccuracyBonusAbility | engine stat |
| 998 | MovementSpeedBonusAbility | engine stat |
| 1003 | HalloweenMaskAbility | item / event / internal / unproven loadability |
| 1004 | HalloweenMask2Ability | item / event / internal / unproven loadability |
| 1005 | WithdrawTacticsAbility | engine stat |
| 1007 | CryptosBoxAbility | item / event / internal / unproven loadability |
| 1009 | MeleeDefenseTacticsAbility | engine stat |
| 1010 | RangedDefenseTacticsAbility | engine stat |
| 1011 | ThrowDefenseTacticsAbility | engine stat |
| 1012 | CurrentTacticAbility | engine stat |
| 1013 | BlockTacticUsedAbility | item / event / internal / unproven loadability |
| 1014 | SnowballAbility | item / event / internal / unproven loadability |
| 1015 | SpeedTacticUsedAbility | item / event / internal / unproven loadability |
| 1016 | GrabTacticUsedAbility | item / event / internal / unproven loadability |
| 1017 | PowerTacticUsedAbility | item / event / internal / unproven loadability |
| 1018 | aaPartyHatAbility | item / event / internal / unproven loadability |
| 1019 | WinterPartyHatAbility1 | item / event / internal / unproven loadability |
| 1020 | WinterPartyHatAbility2 | item / event / internal / unproven loadability |
| 1021 | WinterPartyHatAbility3 | item / event / internal / unproven loadability |
| 1026 | NPCEMPWeaknessAbility | NPC |
| 1027 | BallisticDefenseBonusAbility | engine stat |
| 1028 | ThrownDamageBonusAbility | engine stat |
| 1029 | ThrownDefenseBonusAbility | engine stat |
| 1030 | ViralDefenseBonusAbility | engine stat |
| 1031 | ViralDamageBonusAbility | engine stat |
| 1032 | ThrowAccuracyAbility | engine stat |
| 1033 | ThrowResistanceAbility | engine stat |
| 1034 | ThrowDamageAbility | engine stat |
| 1035 | StealthedCodeDamageAbility | engine stat |
| 1036 | CodeCorruptionDamageAbility | engine stat |
| 1037 | CodeExecutionDamageAbility | engine stat |
| 1038 | CodeReplicationDamageAbility | engine stat |
| 1039 | PacifyDurationBonusAbility | engine stat |
| 1040 | RootDurationBonusAbility | engine stat |
| 1041 | BlindDurationBonusAbility | engine stat |
| 1042 | PowerlessDurationBonusAbility | engine stat |
| 1043 | ConfuseDurationBonusAbility | engine stat |
| 1044 | InitiativeAbility | engine stat |
| 1045 | WithdrawBonusAbility | engine stat |
| 1046 | InitiativeBonusAbility | engine stat |
| 1047 | DeterminationBonusAbility | engine stat |
| 1048 | ForceCombatBonusAbility | engine stat |
| 1049 | EvadeInterlockBonusAbility | engine stat |
| 1050 | SneakMovementSpeedBonusAbility | engine stat |
| 1051 | InvisMovementSpeedBonusAbility | engine stat |
| 1052 | SweepBonusAbility | engine stat |
| 1053 | InnerstrengthCostModifierBonusAbility | engine stat |
| 1054 | BuffAreaRadiusBonusAbility | engine stat |
| 1055 | DebuffAreaRadiusBonusAbility | engine stat |
| 1056 | MaxInnerstrengthBonusAbility | engine stat |
| 1057 | MaxHealthBonusAbility | engine stat |
| 1058 | HealthRegenBonusAbility | engine stat |
| 1059 | InnerstrengthRegenBonusAbility | engine stat |
| 1068 | EnrageDurationBonusAbility | engine stat |
| 1090 | ConcentrationBonusAbility | engine stat |
| 1103 | NPCThrowResistanceAbility | NPC |
| 1104 | DamagePercentAbility | engine stat |
| 1105 | DefensePercentAbility | engine stat |
| 1106 | CombatTacticPercentAbility | engine stat |
| 1109 | StopProxy1EffectAbility | item / event / internal / unproven loadability |
| 1126 | HackerBuffAbility | item / event / internal / unproven loadability |
| 1129 | DistractingBlowEffectAbility | item / event / internal / unproven loadability |
| 1138 | NPCThrowDamageAbility | NPC |
| 1139 | BallisticDamageBonusAbility | engine stat |
| 1140 | BallisticResistanceBonusAbility | engine stat |
| 1141 | HealDamageBonusAbility | engine stat |
| 1142 | MeleeDamageBonusAbility | engine stat |
| 1143 | MeleeDefenseBonusAbility | engine stat |
| 1144 | MeleeResistanceBonusAbility | engine stat |
| 1145 | ThrownAccuracyBonusAbility | engine stat |
| 1146 | ThrownResistanceBonusAbility | engine stat |
| 1147 | ViralAccuracyBonusAbility | engine stat |
| 1148 | ViralResistanceBonusAbility | engine stat |
| 1152 | SpotlightAuraAbility | item / event / internal / unproven loadability |
| 1153 | SpotlightEffectAbility | item / event / internal / unproven loadability |
| 1154 | SpotlightImmunity | item / event / internal / unproven loadability |
| 1155 | SpotlightEffect2Ability | item / event / internal / unproven loadability |
| 1156 | SpotlightEffect3Ability | item / event / internal / unproven loadability |
| 1157 | DOTPlaceHolderAbility | engine stat |
| 1158 | SpotlightTimerAbility | item / event / internal / unproven loadability |
| 1159 | SpotlightDmgAuraAbility | item / event / internal / unproven loadability |
| 1160 | SpotlightDmgEffectAbility | item / event / internal / unproven loadability |
| 1161 | SpotlightUndoAbility | item / event / internal / unproven loadability |
| 1162 | NPCStunAbility | NPC |
| 1163 | MachineAAbility | item / event / internal / unproven loadability |
| 1164 | ZionAAbility | item / event / internal / unproven loadability |
| 1165 | MervAAbility | item / event / internal / unproven loadability |
| 1166 | MachineBAbility | item / event / internal / unproven loadability |
| 1167 | MachineCAbility | item / event / internal / unproven loadability |
| 1168 | MachineDAbility | item / event / internal / unproven loadability |
| 1169 | MachineEAbility | item / event / internal / unproven loadability |
| 1170 | MervBAbility | item / event / internal / unproven loadability |
| 1171 | MervCAbility | item / event / internal / unproven loadability |
| 1172 | MervDAbility | item / event / internal / unproven loadability |
| 1173 | MervEAbility | item / event / internal / unproven loadability |
| 1174 | ZionBAbility | item / event / internal / unproven loadability |
| 1175 | ZionCAbility | item / event / internal / unproven loadability |
| 1176 | ZionDAbility | item / event / internal / unproven loadability |
| 1177 | ZionEAbility | item / event / internal / unproven loadability |
| 1185 | FlameVirusSelfAbility | item / event / internal / unproven loadability |
| 1187 | FlyVirusSelfAbility | item / event / internal / unproven loadability |
| 1188 | FlyStormAbility | item / event / internal / unproven loadability |
| 1189 | BlueHeadlampAbility | item / event / internal / unproven loadability |
| 1190 | ZionCGranterAbility | item / event / internal / unproven loadability |
| 1191 | ZionDGranterAbility | item / event / internal / unproven loadability |
| 1192 | ZionEGranterAbility | item / event / internal / unproven loadability |
| 1193 | MachineCGranterAbility | item / event / internal / unproven loadability |
| 1194 | MachineDGranterAbility | item / event / internal / unproven loadability |
| 1195 | MachineEGranterAbility | item / event / internal / unproven loadability |
| 1196 | MervCGranterAbility | item / event / internal / unproven loadability |
| 1197 | MervDGranterAbility | item / event / internal / unproven loadability |
| 1198 | MervEGranterAbility | item / event / internal / unproven loadability |
| 1200 | aaEscapeMoveAbility | item / event / internal / unproven loadability |
| 1201 | UnlimitCrushAbility | item / event / internal / unproven loadability |
| 1202 | UnlimitDeathPulseAbility | item / event / internal / unproven loadability |
| 1203 | UnlimitDeathTouchAbility | item / event / internal / unproven loadability |
| 1204 | UnlimitImmunityAbility | item / event / internal / unproven loadability |
| 1205 | UnlimitTransformAbility | item / event / internal / unproven loadability |
| 1206 | CodePulseExplodeAbility | item / event / internal / unproven loadability |
| 1207 | CodePulseAuraAbility | item / event / internal / unproven loadability |
| 1208 | CodePulseFlipperAbility | item / event / internal / unproven loadability |
| 1209 | PVPPulseAura20Ability | item / event / internal / unproven loadability |
| 1210 | PVPPulseFlipper20Ability | item / event / internal / unproven loadability |
| 1211 | PVPPulseAura10Ability | item / event / internal / unproven loadability |
| 1212 | PVPPulseFlipper10Ability | item / event / internal / unproven loadability |
| 1213 | PVPPulseAura40Ability | item / event / internal / unproven loadability |
| 1214 | PVPPulseFlipper40Ability | item / event / internal / unproven loadability |
| 1215 | aaKnifeMove1 | item / event / internal / unproven loadability |
| 1216 | aaKnifeMove2 | item / event / internal / unproven loadability |
| 1218 | CodeEyesAbility | item / event / internal / unproven loadability |
| 1219 | TwinGhostAbility | item / event / internal / unproven loadability |
| 1220 | ZombieVirusBaseAbility | item / event / internal / unproven loadability |
| 1221 | ZombieVirusAoEAbility | item / event / internal / unproven loadability |
| 1222 | ZombieVirusGrantAbility | item / event / internal / unproven loadability |
| 1223 | ZombieVirusGranterAbility | item / event / internal / unproven loadability |
| 1224 | PBlueHeadlampAbility | item / event / internal / unproven loadability |
| 1225 | PCodeEyesAbility | item / event / internal / unproven loadability |
| 1226 | PFlameVirusSelfAbility | item / event / internal / unproven loadability |
| 1227 | PFlyStormAbility | item / event / internal / unproven loadability |
| 1228 | PFlyVirusSelfAbility | item / event / internal / unproven loadability |
| 1229 | PacZoneAura10Ability | item / event / internal / unproven loadability |
| 1230 | PacZoneAura20Ability | item / event / internal / unproven loadability |
| 1231 | PacZoneAura40Ability | item / event / internal / unproven loadability |
| 1232 | PacZoneFlipper10Ability | item / event / internal / unproven loadability |
| 1233 | PacZoneFlipper20Ability | item / event / internal / unproven loadability |
| 1234 | PacZoneFlipper40Ability | item / event / internal / unproven loadability |
| 1235 | ZombieVirus2BaseAbility | item / event / internal / unproven loadability |
| 1236 | ZombieVirus2BiteAbility | item / event / internal / unproven loadability |
| 1237 | ZombieVirus3BiteAbility | item / event / internal / unproven loadability |
| 1238 | ZombieVirus3BaseAbility | item / event / internal / unproven loadability |
| 1239 | RemoteProxyLevelAbility | engine stat |
| 1240 | HolidayMaskAbility | item / event / internal / unproven loadability |
| 1241 | SnowballFrostAbility | item / event / internal / unproven loadability |
| 1242 | NPCThrowAccuracyAbility | NPC |
| 1243 | RedEyeAbility | item / event / internal / unproven loadability |
| 1244 | PRedEyeAbility | item / event / internal / unproven loadability |
| 1245 | RSIConsumableAbility | item / event / internal / unproven loadability |
| 1246 | NPCConfuseAbility | NPC |
| 1247 | NPCPacifyAbility | NPC |
| 1248 | NPCBlindAbility | NPC |
| 1249 | NPCPowerlessAbility | NPC |
| 1250 | NPCRoot1Ability | NPC |
| 1251 | NPCRoot2Ability | NPC |
| 1252 | SmithVirus1AttackAbility | item / event / internal / unproven loadability |
| 1253 | SmithVirus1BaseAbility | item / event / internal / unproven loadability |
| 1254 | SmithVirus2BaseAbility | item / event / internal / unproven loadability |
| 1255 | SmithVirus2AttackAbility | item / event / internal / unproven loadability |
| 1256 | SmithVirusImmunityAbility | item / event / internal / unproven loadability |
| 1257 | SmithRagePillAbility | item / event / internal / unproven loadability |
| 1258 | OracleAnniversaryCookieAbility | item / event / internal / unproven loadability |
| 1259 | SmithEndBrownieAbility | item / event / internal / unproven loadability |
| 1260 | CorruptorSpawnAura | item / event / internal / unproven loadability |
| 1261 | CorruptorSpawnAuraEffect | item / event / internal / unproven loadability |
| 1262 | Event2_0CorruptingBile2Ability | item / event / internal / unproven loadability |
| 1263 | CorruptPatchersPillAbility | item / event / internal / unproven loadability |
| 1264 | NPCShadowOff | NPC |
| 1265 | NPCShadowAOEFriendly | NPC |
| 1266 | NPCShadowAura | NPC |
| 1267 | NPCShadowAOEHostile | NPC |
| 1268 | NPCInitiativeAbility | NPC |
| 1269 | SeraphicVisionAbility | item / event / internal / unproven loadability |
| 1270 | PSeraphicVisionAbility | item / event / internal / unproven loadability |
| 1271 | NPCMeleeDefenseAbility | NPC |
| 1272 | NPCRangeDefenseAbility | NPC |
| 1273 | NPCThrowDefenseAbility | NPC |
| 1274 | QuicksilverPillAbility | item / event / internal / unproven loadability |
| 1275 | OptimizerInhalerAbility | item / event / internal / unproven loadability |
| 1276 | OptimizerHypoAbility | item / event / internal / unproven loadability |
| 1277 | OverdriveHypoAbility | item / event / internal / unproven loadability |
| 1278 | DecompileTimerReductionAbility | engine stat |
| 1279 | HeadlessSpawnAura | item / event / internal / unproven loadability |
| 1280 | HeadlessSpawnAuraEffect | item / event / internal / unproven loadability |
| 1281 | CSRDollMoonliteAbility | item / event / internal / unproven loadability |
| 1282 | CSRDollBrewkoAbility | item / event / internal / unproven loadability |
| 1283 | CSRDollChrolAbility | item / event / internal / unproven loadability |
| 1284 | CSRTissueAbility | item / event / internal / unproven loadability |
| 1285 | ShadowWallKickAbility | item / event / internal / unproven loadability |
| 1286 | ShadowPhoenixAbility | item / event / internal / unproven loadability |
| 1287 | ShadowRazorAbility | item / event / internal / unproven loadability |
| 1288 | ZombieBiteImmunityAbility | item / event / internal / unproven loadability |
| 1289 | WarSwordAbility | item / event / internal / unproven loadability |
| 1290 | WarSwordLargeAbility | item / event / internal / unproven loadability |
| 1291 | PWarSwordAbility | item / event / internal / unproven loadability |
| 1292 | PWarSwordLargeAbility | item / event / internal / unproven loadability |
| 1293 | OGAccelerateEffectAbility | item / event / internal / unproven loadability |
| 1294 | OGDecelerateEffectAbility | item / event / internal / unproven loadability |
| 1296 | OGRunEffectAbility | item / event / internal / unproven loadability |
| 1297 | OGIndefAccelAbility | item / event / internal / unproven loadability |
| 1298 | OGDecelImmunityAbility | item / event / internal / unproven loadability |
| 1299 | OGTerminateEnemiesAbility | item / event / internal / unproven loadability |
| 1300 | OGRunImmunityAbility | item / event / internal / unproven loadability |
| 1301 | OGTerminateFriendliesAbility | item / event / internal / unproven loadability |
| 1302 | OGTerminateAbility | item / event / internal / unproven loadability |
| 1303 | OGAccelerateAbility | item / event / internal / unproven loadability |
| 1304 | OGDecelerateAbility | item / event / internal / unproven loadability |
| 1305 | OGRunAbility | item / event / internal / unproven loadability |
| 1306 | OGAccelerateBitAbility | item / event / internal / unproven loadability |
| 1307 | OGDecelerateBitAbility | item / event / internal / unproven loadability |
| 1308 | OGRunBitAbility | item / event / internal / unproven loadability |
| 1309 | OGTerminateBitAbility | item / event / internal / unproven loadability |
| 1312 | OGLOSDecelerateAbility | item / event / internal / unproven loadability |
| 1313 | OGLOSDecelerateEffectAbility | item / event / internal / unproven loadability |
| 1314 | OGLOSAccelerateAbility | item / event / internal / unproven loadability |
| 1315 | OGLOSAccelerateEffectAbility | item / event / internal / unproven loadability |
| 1316 | OGLOSTerminateAbility | item / event / internal / unproven loadability |
| 1317 | OGLOSTerminateEnemiesAbility | item / event / internal / unproven loadability |
| 1318 | OGLOSTerminateFriendliesAbility | item / event / internal / unproven loadability |
| 1319 | OGLOSTerminateFriendliesImmunityAbility | item / event / internal / unproven loadability |
| 1320 | OGTerminateFriendliesImmunityAbility | item / event / internal / unproven loadability |
| 1321 | SmithFirewallCandyAbility | item / event / internal / unproven loadability |
| 1323 | OverrideOrbAbility | item / event / internal / unproven loadability |
| 1324 | POverrideOrbAbility | item / event / internal / unproven loadability |
| 1325 | SSRGumAbility | item / event / internal / unproven loadability |
| 1327 | NPCBallisticAccuracyBonusAbility | NPC |
| 1328 | NPCMeleeAccuracyBonusAbility | NPC |
| 1329 | NPCThrownAccuracyBonusAbility | NPC |
| 1330 | NPCViralAccuracyBonusAbility | NPC |
| 1331 | NPCForceCombatBonusAbility | NPC |
| 1332 | NPCInitiativeBonusAbility | NPC |
| 1333 | ISBoosterAbility | item / event / internal / unproven loadability |
| 1334 | LESIGSpawnCyphAbility | item / event / internal / unproven loadability |
| 1335 | LESIGSpawnCyphEffectAbility | item / event / internal / unproven loadability |
| 1336 | LESIGSpawnEPNAbility | item / event / internal / unproven loadability |
| 1337 | LESIGSpawnEPNEffectAbility | item / event / internal / unproven loadability |
| 1338 | LESIGSpawnLupinesAbility | item / event / internal / unproven loadability |
| 1339 | LESIGSpawnLupinesEffectAbility | item / event / internal / unproven loadability |
| 1340 | LESIGSpawnSWATAbility | item / event / internal / unproven loadability |
| 1341 | LESIGSpawnSWATEffectAbility | item / event / internal / unproven loadability |
| 1342 | LESIGSpawnZionAbility | item / event / internal / unproven loadability |
| 1343 | LESIGSpawnZionEffectAbility | item / event / internal / unproven loadability |
| 1344 | ShurikenAbility | item / event / internal / unproven loadability |
| 1345 | KunoShurikenInitiateAbility | item / event / internal / unproven loadability |
| 1346 | KunoShurikenMasterAbility | item / event / internal / unproven loadability |
| 1347 | KunoShurikenGrandmasterAbility | item / event / internal / unproven loadability |
| 1348 | KunoShuriken83Ability | item / event / internal / unproven loadability |
| 1349 | KunoShuriken84Ability | item / event / internal / unproven loadability |
| 1350 | KunoShuriken85Ability | item / event / internal / unproven loadability |
| 1351 | KunoShuriken86Ability | item / event / internal / unproven loadability |
| 1352 | KunoShuriken87Ability | item / event / internal / unproven loadability |
| 1353 | KunoShuriken88Ability | item / event / internal / unproven loadability |
| 1354 | KunoShuriken89Ability | item / event / internal / unproven loadability |
| 1355 | KunoShuriken90Ability | item / event / internal / unproven loadability |
| 1356 | KunoShuriken91Ability | item / event / internal / unproven loadability |
| 1357 | KunoShuriken92Ability | item / event / internal / unproven loadability |
| 1358 | KunoShuriken93Ability | item / event / internal / unproven loadability |
| 1359 | KunoShuriken94Ability | item / event / internal / unproven loadability |
| 1360 | KunoShuriken95Ability | item / event / internal / unproven loadability |
| 1361 | KunoShuriken96Ability | item / event / internal / unproven loadability |
| 1362 | KunoShuriken97Ability | item / event / internal / unproven loadability |
| 1363 | KunoShuriken98Ability | item / event / internal / unproven loadability |
| 1364 | KunoShuriken99Ability | item / event / internal / unproven loadability |
| 1365 | KunoShuriken100Ability | item / event / internal / unproven loadability |
| 1366 | NPCKunoichiBuffs | NPC |
| 1367 | NPCShadowAOEFriendlyShort | NPC |
| 1368 | NPCShadowAOEHostileKuno | NPC |
| 1369 | NinjaScarfAbility | item / event / internal / unproven loadability |
| 1370 | PNinjaScarfAbility | item / event / internal / unproven loadability |
| 1371 | BlueShiftAbility | item / event / internal / unproven loadability |
| 1372 | NPCSleepwalkerBuffs | NPC |
| 1373 | NPCSleepSecurityBuffs | NPC |
| 1374 | SleepBlueShift1Ability | item / event / internal / unproven loadability |
| 1375 | SleepBlueShift2Ability | item / event / internal / unproven loadability |
| 1376 | SleepBlueShift3Ability | item / event / internal / unproven loadability |
| 1377 | NPCValkAndvaranautAbility | NPC |
| 1378 | NPCValkyrjaBuffs | NPC |
| 1379 | ValkAndvaranautAbility | item / event / internal / unproven loadability |
| 1380 | NPCValkValfreyjaBuffs | NPC |
| 1381 | OGCompressAbility | item / event / internal / unproven loadability |
| 1382 | OGSignalJammingAbility | item / event / internal / unproven loadability |
| 1383 | OGSignalJammingEffectAbility | item / event / internal / unproven loadability |
| 1384 | AntioverrideProto01AccAbility | item / event / internal / unproven loadability |
| 1385 | AntioverrideProto01DecAbility | item / event / internal / unproven loadability |
| 1386 | AntioverrideProto01RunAbility | item / event / internal / unproven loadability |
| 1387 | NPCUnsusceptibilityAbility | NPC |
| 1388 | RSICaptureAbility | item / event / internal / unproven loadability |
| 1389 | NPCWAPBuffs | NPC |
| 1390 | NPCWrightAwakeningAbility | NPC |
| 1391 | NPCWrightSignalModulationAbility | NPC |
| 1392 | AntibioticToggleAbility | item / event / internal / unproven loadability |
| 1393 | AntibioticInstantAbility | item / event / internal / unproven loadability |
| 1394 | AntioverrideDecAbility | item / event / internal / unproven loadability |
| 1395 | AntioverrideRunAbility | item / event / internal / unproven loadability |
| 1396 | AntioverrideSigAbility | item / event / internal / unproven loadability |
| 1397 | NPCAntibioticAbility | NPC |
| 1398 | NPCWrightDecelerateAbility | NPC |
| 1399 | NPCWrightDecelerateEffectAbility | NPC |
| 1400 | NPCWrightSignalJammingAbility | NPC |
| 1401 | NPCWrightSignalJammingEffectAbility | NPC |
| 1402 | OverrideImageAbility | item / event / internal / unproven loadability |
| 1403 | POverrideImageAbility | item / event / internal / unproven loadability |
| 1404 | NPCExecute30Ability | NPC |
| 1405 | NPCExecBuffs | NPC |
| 1406 | NPCFuncBuffs | NPC |
| 1407 | NPCExecute31Ability | NPC |
| 1408 | NPCExecute32Ability | NPC |
| 1409 | NPCExecute33Ability | NPC |
| 1410 | NPCExecute34Ability | NPC |
| 1411 | NPCExecute35Ability | NPC |
| 1412 | NPCExecute36Ability | NPC |
| 1413 | NPCExecute37Ability | NPC |
| 1414 | NPCExecute38Ability | NPC |
| 1415 | NPCExecute39Ability | NPC |
| 1416 | NPCExecute40Ability | NPC |
| 1417 | NPCExecute41Ability | NPC |
| 1418 | NPCExecute42Ability | NPC |
| 1419 | NPCExecute43Ability | NPC |
| 1420 | NPCExecute44Ability | NPC |
| 1421 | NPCExecute45Ability | NPC |
| 1422 | NPCExecute46Ability | NPC |
| 1423 | NPCExecute47Ability | NPC |
| 1424 | NPCExecute48Ability | NPC |
| 1425 | NPCExecute49Ability | NPC |
| 1426 | NPCExecute50Ability | NPC |
| 1427 | NPCExecute51Ability | NPC |
| 1428 | NPCExecute52Ability | NPC |
| 1429 | NPCExecute53Ability | NPC |
| 1430 | NPCExecute54Ability | NPC |
| 1431 | NPCExecute55Ability | NPC |
| 1432 | NPCExecute56Ability | NPC |
| 1433 | NPCExecute57Ability | NPC |
| 1434 | NPCExecute58Ability | NPC |
| 1435 | NPCExecute59Ability | NPC |
| 1436 | NPCExecute60Ability | NPC |
| 1437 | NPCExecute61Ability | NPC |
| 1438 | NPCExecute62Ability | NPC |
| 1439 | NPCExecute63Ability | NPC |
| 1440 | NPCExecute64Ability | NPC |
| 1441 | NPCExecute65Ability | NPC |
| 1442 | NPCExecute66Ability | NPC |
| 1443 | NPCExecute67Ability | NPC |
| 1444 | NPCExecute68Ability | NPC |
| 1445 | RSICapKuno83Ability | item / event / internal / unproven loadability |
| 1446 | RSICapKuno85Ability | item / event / internal / unproven loadability |
| 1447 | RSICapKuno84Ability | item / event / internal / unproven loadability |
| 1448 | RSICapKuno86Ability | item / event / internal / unproven loadability |
| 1449 | RSICapKuno87Ability | item / event / internal / unproven loadability |
| 1450 | RSICapKuno88Ability | item / event / internal / unproven loadability |
| 1451 | RSICapKuno89Ability | item / event / internal / unproven loadability |
| 1452 | RSICapKuno90Ability | item / event / internal / unproven loadability |
| 1453 | RSICapKuno91Ability | item / event / internal / unproven loadability |
| 1454 | RSICapKuno92Ability | item / event / internal / unproven loadability |
| 1455 | RSICapKuno93Ability | item / event / internal / unproven loadability |
| 1456 | RSICapKuno94Ability | item / event / internal / unproven loadability |
| 1457 | RSICapKuno95Ability | item / event / internal / unproven loadability |
| 1458 | RSICapKuno96Ability | item / event / internal / unproven loadability |
| 1459 | RSICapKuno97Ability | item / event / internal / unproven loadability |
| 1460 | RSICapKuno98Ability | item / event / internal / unproven loadability |
| 1461 | RSICapKuno99Ability | item / event / internal / unproven loadability |
| 1462 | RSICapKuno100Ability | item / event / internal / unproven loadability |
| 1463 | RSICapSSRAbility | item / event / internal / unproven loadability |
| 1464 | RSICapValfAbility | item / event / internal / unproven loadability |
| 1465 | RSICapEliteAbility | item / event / internal / unproven loadability |
| 1466 | RSICapWrightAbility | item / event / internal / unproven loadability |
| 1467 | RSICapExecAbility | item / event / internal / unproven loadability |

## Reproduction and checks

1. `python research/abilities/extract_catalog.py --client <read-only MxO folder>`
2. `dotnet run --project tools/AbilityAudit`
3. `python research/abilities/build_audit.py`

The runtime inventory calls the real `AbilityCosts`, `AbilityCooldowns`, `AbilityBuffs`, `TempMods`, `AbilityTargets` and grant lookup, rather than searching comments. The generated [CSV](ABILITY-PARITY.csv) is filterable by archetype, tree and status. Ability changes have unit coverage in `AbilityParityTests.cs` and four fresh-server `ability-parity-*` scenarios for support, controls, area effects and IS denial. Native ability checks are in [ABILITY-PARITY-QA.md](ABILITY-PARITY-QA.md) and the handoff file; this ability task does not acquire the client lock. The earlier PR94 native validation is separately recorded in `PVP.md`.
