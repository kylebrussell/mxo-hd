# Character-level timed buffs

The prior catalog fallback accepted fixed min/max constants only. It rejected deterministic level-scaled beneficial buffs even after the native value evaluator had been decoded. This change adds thirteen timed numeric templates, resolving character-level function3 from the **caster** when the buff lands. Existing captured overrides remain authoritative.

New templates: Adrenaline Booster1; Calm Mind, Calm Body5; Empty Mind8; Find Weakness123; Firearms Skill124; Stealth Awareness156; Passive Code170; Combat Enhancement173; Combat Training373; Weapons Boost374; Upgrade Health417; Bolster Health419; Enhanced Dodge421.

Nine have existing gameplay consumers: melee/ballistic accuracy, ballistic damage/resistance, healing, defence and maximum health. Thrown resistance, viral resistance, thrown damage and stealth detection still lack consumers. Those four templates gain numeric records/totals but remain **cosmetic** in the audit. Neither storing a value nor showing a countdown establishes gameplay parity.

## Evidence and implementation

The shipped GOB's `ValueFunction=3`, `ValueScale`, optional min/cap, `TargetAttribute`, duration and target fields provide each numeric template. The evaluator is the same native `62517ff0` / truncation helper`626a92d8` established in [modifier evidence](ABILITY-MODIFIERS.md). Stored binary32 scales multiply with x87 precision before truncation: character20 with scale1.3 produces25; Enhanced Dodge at50 with scale0.96 produces47. Positive template caps still apply.

Only self/friendly numeric modifiers with positive, matching timed durations are eligible. Each must use deterministic fixed/function3 values and have no init/update/term/health callback or target state. Function2 learned-level styles, indefinite combat styles, auras, procs, random values and script callbacks are excluded. This does not guess missing stealth, viral-resistance or thrown-combat systems.

An active buff now retains its resolved definition. The stat and vital consumers read that snapshot, and expiry/dismissal/replacement use its original record values. Later caster/recipient level changes cannot silently change an already applied buff or cause an off record with different values. Friendly buffs use the source player's level; a level20 caster's Upgrade Health adds200 to a level50 recipient, not500. Replacing it with a level50 caster's Bolster Health adds500 over the recipient's original baseline, avoiding compounding. Removing it also clamps current health when necessary.

The existing nonzero StackingType-to-family replacement policy is reused; full native priority/stacking semantics remain unverified. Existing captured buffs retain their captured values even where the raw template differs. This deliberately leaves full scaling of those overrides and Death Effect's impact on them open. Generic support still supplies existing range, cast, cooldown, IS and live-friendly gates.

## Validation and pending native checks

On integrated376ba87, all3600 Debug and Release unit tests pass (91s/90s), and CI43147022196 passes. Nine new tests cover source level, native precision/caps, active snapshots, captured overrides and health replacement. NativeQa `scaled` mode builds in Release; QAFriend is20 and CSR_Tester/QAOther50 for the unequal-source checks.

The full92-case baseline passes1179.4s with six servers at peak, including every new scaled-buff case. That baseline predates the native resurrection reply correction. After the parser-only correction was integrated, all13 ability scenarios pass151.7s with four servers at peak, including captured short replies, all four resurrection callbacks and their refusal/reconstruction guards, scaled self/friendly buffs, passive lifecycle, healing, damage, controls, area and costs. The exact initial15% Death Effect reduction remains a unit assertion; live/reconnect tests allow valid recovery during slow database/delivery work instead of assuming the first168 modifier remains current.

No native check is claimed. With the normal CSR_Tester110 disposable harness and client lock, verify Firearms Skill124's20-second countdown and expiry, Passive Code170's healing change, Upgrade Health417 and Bolster Health419 cast by unequal-level sources, family replacement, buff dismissal, max/current-health updates and reconnect/death cleanup. Compare against a known retail caster build before asserting exact final magnitudes. Reset110 at Mara `(23828,495,13984)`, full health, flag0 and mission986 only before releasing the lock. TheNeo is never a native test character.

