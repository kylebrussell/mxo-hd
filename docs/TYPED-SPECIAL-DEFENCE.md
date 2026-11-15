# Typed combat-special defence

Queued interlock specials now select the defender's typed defence in both NPC and PvP rounds. Ordinary melee and gun rounds retain their existing paths. Hyper-Sense1101 therefore affects thrown-special hit contests rather than only sending a numeric record. This change does not alter damage, resistance, healing, Neutral damage, direct casts, field hits, DOTs or projectile formulas.

## Primary evidence

Read-only client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`:

- `getDefenseTacticBonusType` method registration at `629c2888` points to wrapper `625201f0`, which calls `6251ffd0`.
- `6251ffd0..62520032` reads the shipped `TestFunction` property (`628c8a5c`). Its jump table at `62520158` selects these exact engine stats:

| TestFunction | Defence stat |
|---:|---|
|0|none (`0x1000` sentinel)|
|1|1010 RangedDefenseTactics|
|2|183 ViralDeflection|
|3|1009 MeleeDefenseTactics|
|4,6|1011 ThrowDefenseTactics|
|5, unknown|none (native diagnostic/failure path)|

- [`combat_resolution.getCorrectDTTotals`](../research/client-scripts/interlock/combat_resolution.py) uses the **opposing requested special**, then `GetCorrectDefenseBonus` chooses ranged/viral/melee/thrown. Without a special, equipped weapon or fists chooses ordinary defence. Both sides can request their own special; each uses the opponent's selector independently.
- Caller qualification matters: `combat_utility.isSpecialMove` excludes escape abilities. Native `isEscapeAbility` wrapper `62516a00..62516a89` reads TestFunction and compares with6 at `62516a66`. Thus the raw selector maps6 to1011, but the interlock caller retains ordinary defence for an escape rather than treating it as an opposing attack special.
- For reproduction, run `python research/abilities/inspect_special_defense.py <client.dll>`; this standard-library-only reader verifies the full DLL hash, TestFunction string, dispatch opcodes and table entries without opening a client process. The shipped catalog retains each `Ability/TestFunction`.

## Preserved server policy and limits

`AbilityStats.Player` still applies catalog total/level caps before the contest. Existing general defence959, level baseline and percent-delta policy are retained: the latter uses the character level with the selected type's linked bonus (ranged1027, melee1143, thrown1029, viral1030). These baseline/percentage choices are **ours**, already used for melee/ranged; this PR does not reconstruct retail character-attribute base curves or retail hit probabilities. Unknown/uncontested specials provide zero additional defence; RollHit's existing level baseline remains. NPCs use their own timed totals; they do not borrow a player's stats.

`CombatInterlock.RollHit` and live random sources remain unchanged. The dev diagnostic uses paired seed731 over1000 production contests and takes one fixture HP per landed diagnostic contest via `PvpCombat.Damage`. This controlled diagnostic is not a retail RNG claim. It requires exact `MXO_DEV_COMMANDS=1`; `?combatdepth specialdefense` reports selector/total, landed count and authoritative HP before/after. Use throwaway characters with more than1000 HP. It does not queue a real attack. Fresh queued-special scenarios separately exercise real NPC and PvP interlock wiring.

## Resistance research boundary

Native status UI `620f88e0..620f8ad2` displays144 general resistance plus180 melee,181 ballistic,1033 thrown or157 viral. The script `combat_calculations.GetDamageReductionValue` supplies the documented +/-90% level/armor formula, but its recovered callers establish only melee/ballistic free attacks and interlock damage. Native `6258d830` delegates to Python; its only direct call found is wrapper `6258dba6`. No recovered source establishes applying that curve to thrown/viral ability health modifiers, DOTs or fields, or undoing resistance already included in captured damage overrides. Consequently original5/8 and Personal Firewall thrown/viral resistance remain unconsumed. No normalized ratio or missing retail base is invented.

## Claude-only native checklist

Use the PR body's exact full head SHA and build provenance. Only Claude may start NativeQa setup, reserve the native client, run it or reset it. On a disposable `typed-defense` fixture use focused owned loadout `2,17,1112,134,1101` (owned unloaded discipline ancestors are added by NativeQa). Check `--fixture-plan typed-defense` reports dev1 before setup. Confirm Release/Debug `--build-info` source/catalog/snapshot hashes match.

1. Start with more than1000 HP (`?health 5000`), enter `?combatdepth specialdefense`, record total/landed/HP delta. Activate Hyper-Sense1101 from native Actions; its stat1011 record at learned50 is47. Repeat the diagnostic: fewer paired-seed landed contests and actual HP loss equal to the reported landed count. This is diagnostic damage, not1000 live attacks.
2. Start ordinary close combat with the test NPC, then use Leg Sweep134: log must select NPC special134 stat1011; ordinary rounds retain prior behaviour and HP changes normally. Stop the fight.
3. With a PvP-legal helper opponent, run a real interlock and queue Leg Sweep134 toward a Hyper-Sense defender. Log must select PvP special134 stat1011. Observe combat exchanges and HP updates on both views; do not infer exact retail odds from a few samples.
4. Remove/expire Hyper-Sense and confirm the diagnostic returns to its original total/count; basic melee and gun contests remain unchanged. Stop/reset the disposable fixture normally. Resistance5/8 is still a known gap.
