# Hostile Hacker effects: viruses, fields, auras and effect removal

This follow-up gives the hostile Hacker templates an executor on the existing `TimedEffects` tempmod holder (PR 102/103). It does not add a second effect system. Code: `AbilityHostile.cs` (families, catalog debuffs, removal scripts, area targets) and `AbilityAuras.cs` (toggled auras).

## Evidence

| Item | Source |
|---|---|
| Debuff family | `abilitylib.isADebuffAbility` is client.dll `62515f30` (Python binding `625167b0`, format string at `628c8be0`): StatusCategory `UDB0` (0x30424455). `isABuffAbility` is `62515ef0`: `UBF0`. All 22 hostile templates in this group store UDB0. Miasma 1.0, Static Field, the sweeps and Gaussian Blur store UBF0. |
| Values, durations, radius, interval | The shipped template (`data/ability_catalog.json`): MinValue/MaxValue, Duration, DurationType, RadiusOfEffect, CastDirectObjects, UpdateFuncInterval, StackingType. |
| DOT tick | `tempmods.ViralDOT`: `int(Value / Duration * UpdateInterval + 0.5)`, interval per template. Processor Lag 1.0 stores 5 s. The others store 2.5 s. |
| Virus lines | A template whose ActivationStringTarget is 0xc600001a ("%2s's %1z infects you!") is a virus. The captured UI Lag 1.0, Vector and Code Infection 4 all store this string. Other templates send their own stored Subject/Target strings (Miasma 2.0: 0xc4000036 "%3s is caught in your Miasma field!"). |
| Auras | `ApplyAuraEffect` (init and update) re-applies Value (Miasma 701, Static Field 694) while the caster has health. `ISDrainWithAbilityDeactivation` takes Value IS per interval, or deactivates the aura with 0xc60020b4 and the shortfall. |
| Sweeps | `NegativeConditionSweepPulse`: each debuff parent is removed with Value% chance per pulse, except `dispelExemptAbilities` (tempmods.py line 8, which includes ControlImmunityTimer 734). Line 0xc60022fe "%1z removes %4o from you!", FX_CHARACTER_ANTIVIRAL_SWEEP 0x28000630. |
| Delete/Cancel Upgrade | `RemoveRandomBuff`: candidates are buff parents (UBF0), not CanNOTRemoveBuff and not exempt. The pick is `random.randrange(0, n - 1)`, which never chooses the last of several candidates (kept as scripted). Lines 0xc4000026/0xc4000027, fail lines 0xc6000197/0xc6000198. `deactivateAbility` also switches off toggled auras. `UpgradeSweepPulse` (Delete Upgrade 2.0) gives each buff its Value% chance. |
| String ids | `stringtable_client.tsv` rows whose text matches the script's arguments. Several of these rows are unnamed in the table, so the match is by text. |

## Behaviour

- **Numeric debuffs.** Each fixed, timed (DurationType 0) modifier of a UDB0 template becomes a stat tempmod (kind 0x15). Each `*DOT` modifier becomes damage over time on DOTPlaceHolder 1157 (kind 0x1d). Both refresh, stack across abilities, expire and are cleared at death through the existing holder. Captured `TempMods.Retail` entries still take precedence. This applies to every numeric-only UDB0 template, so about 47 abilities moved from missing to partial, including some outside the Hacker trees.
- **Consumers.** Accuracy, defence and resistance already feed NPC and PvP combat (`AbilityStats`). IS regen (114) and health regen (112) feed regeneration. Max Health (106) and Max IS (109) tempmods now compose in `RefreshVitals`, clamp the current value and restore on expiry. Inner Strength Cost Reduction (117) now changes ability costs (`AbilityCosts.Effective`). Destroy Resolve's instant Current IS (111) is applied when it lands. Movement, evade, force-combat, viral accuracy/defence/resistance records reach the client but have no server consumer.
- **Areas.** CastDirectObjects 3 abilities (the fields, Plague Zone, Insidious Code, UI Lag 2.0) reach hostile NPCs and PvP-legal players within RadiusOfEffect of the primary target, whether that target is an NPC or a player.
- **Fields you can leave.** Miasma 1.0 and Static Field are toggles. Every 5 s they drain IS, then apply their 5 s effect to hostile NPCs and PvP-legal players in radius 6 of the caster. A target that walks out keeps the effect for at most one interval. Using the aura again, an IS shortfall, death, disconnect or a Delete Upgrade switches it off.
- **Cleansing.** Negative Condition Sweep 1.0/2.0 (friendly target) and Sweep Party 1.0/2.0 (team within 9 m) put a pulse tempmod (kind 0x09) on each recipient. It removes debuff parents only: never buffs, never ControlImmunityTimer. Sweeps that share a StackingType replace each other.

## Choices (not retail evidence)

- The `80 bd 05` on/off switch for auras copies the retail switch for other "while enabled" toggles. No aura capture exists.
- The pulse record uses kind 0x09 with the sweep as both parent and target, copying the layout of DeflectVirus1's proc record.
- The 117 cost formula is `cost - trunc(cost × value / 100)`, clamped at 0. The native cost path is not reconstructed.
- Destroy Resolve applies its components in stored order: Max IS first, then the instant Current IS.
- Delete Upgrade 2.0 runs RemoveRandomBuff, then UpgradeSweepPulse.
- PvP casters also receive the virus or Subject line when their effect lands on a player.

## Gaps

- **Buffer capacity.** Buffer Overflow 1.0/2.0 and Cancel Upgrade 2.0 keep a removable timed record with the stored amount. The buff buffer itself (`getBuffBufferMax`, MaxBuffBuffer 162, "increases as you gain levels") is native and unreconstructed, so buffs are never refused.
- **Crash's Force Combat** stores no DurationType and is not guessed.
- **Gaussian Blur 49** is a self buff with an `ImpartBlurInit` dissembling callback. The kBlur value is unknown, and its numeric evade part belongs to the beneficial-buff work, so it remains missing.
- **Static Field's 694** applies its stored raw viral 1 × the caster's influence. The native value function is not applied.
- **NPC-side consumers.** NPCs have no IS, no max-health debuff and no movement debuff, and debuff-only aura pulses do not aggro an NPC.
- **Mitigation.** Eligible UDB0 on-use debuff durations now consume known target Determination575 with the script formula, excluding self effects and immunity timers; complete target bases remain unknown. Hit/deflection rolls and resistance-derived DOT reduction remain gaps. See [combat depth](COMBAT-DEPTH.md).

## Tests

- Unit: `AbilityHostileTests` (35 cases).
- Headless: `hostile-fields-pvp` (Fumble Field on two flagged players, Code Infection ticks, Destroy Health, Delete Upgrade removing only a buff, a sweep record) and `hostile-fields-npc` (Miasma 1.0 aura pulses and toggle-off, Plague Zone 1.0 ticks of 10).

## Native QA (CSR_Tester110 only; pending)

1. Near WinterFrost, cast Miasma 1.0 (61). Expect the on-self line, the IS bar dropping 3 every 5 s and WinterFrost debuffed in the server log. Cast it again: the switch goes off.
2. Cast Plague Zone 1.0 (400) on WinterFrost. Expect ticks of 10 every 2.5 s, five ticks in total.
3. `?duelbot pvp`. Cast Fumble Field (361), Destroy Health (476) and Code Infection 1.0 (350) on the bot, then check the infects and disarm lines, the bot's health ticks and its lowered maximum.
4. Put Overclock (327) on yourself and have the hostile helper cast Delete Upgrade 1.0 (324): the Overclock icon must disappear and your debuffs stay. Cast Negative Condition Sweep 2.0 (424) on yourself while debuffed: debuff icons drop over 25 s and the immunity timer stays.
