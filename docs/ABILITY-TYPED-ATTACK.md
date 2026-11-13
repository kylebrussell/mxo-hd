# Typed queued-special accuracy (partial server policy)

The native client chooses an accuracy stat from the special ability's stored TestFunction. This server now uses that selection for its existing queued NPC/PvP close-combat rounds. Applying the client UI selection to authoritative contests is an explicitly **OURS inference**. The retail tacticalValue producer and complete retail accuracy formula remain unrecovered.

## Evidence and limits

In client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`, `6251fe10` reads TestFunction (string `628c8a5c`); jump table `6251ffb4` selects:

| TestFunction | Native selected stat | Server queued policy |
|---|---|---|
| 1 | 524 ranged | Existing `Attack(true)` including its established 996 convention |
| 2 | 342 viral | General163 + capped direct342 + existing Blind penalty |
| 3 | 522 melee | Existing `Attack(false)` exactly |
| 4 | 1032 thrown | General163 + capped direct1032 + existing Blind penalty |
| 6 | 1005 withdrawal | Existing `Attack(false)` fallback; withdrawal unchanged |
| 0/unknown | native sentinel4096 | Existing `Attack(false)` fallback |

The native API is `getCombatTacticBonusType` (string628c8738/method table629c2878). Selected-stat accumulator625c5df0 calls6251fe10 at625c5e6e and reads the selected stat at625c5e86. All eight recovered direct callers are in client status/display calculation paths, so this is **not proof of the authoritative retail gameplay producer**. Recovered `interlock/combat_resolution.py:getCTandDTotals` consumes precomputed tacticalValue without selecting its stat.

Existing general totals, caps, melee/ranged percentage policy, Blind, roll and damage formulas remain unchanged. No new viral1147 or thrown1145 percentage link math is introduced. Run Program32 stays cosmetic; selecting direct342 does not close its unproven linked-bonus behavior. Captured Upgrade Attacks314 provides direct342+108 for20s; Howitzer1087's captured direct342+13 is unchanged.

The normal handler overwrites cast time from catalog (`AbilityHandler`), then only zero-cast abilities can enter `QueueRoundAbility`. All player viral TestFunction2 abilities currently have positive casts and route to `AbilityTargets` after casting. This PR does not change that admission or make those casts contested. Viral support here is diagnostic/future queued-executor coverage, not proof of an accessible queued viral player move. No internal effect ability is loaded to evade that boundary.

Normal instant Leg Sweep134 is TestFunction4 and already admitted by both queued executors. Previously it used melee522/997; it now uses direct1032. Captured UI Lag97 provides1032−10 for27s and consequently changes these contests. Ordinary melee/ranged attacks and escape/unknown special behavior are preserved.

## Disposable verification

`typed-attack-npc` and `typed-attack-pvp` use normal owned/loaded134 and97, ordinary target/cast requests, actual captured modifier records, queued move records, production totals and HP updates. A read-only paired diagnostic uses seed731 with the existing RollHit implementation to isolate the stat's effect without changing any live fight RNG or health. The NPC case also proves314's exact108 direct viral total through that diagnostic; it does not queue viral33.

`?combatdepth specialattack <105|134|197|33>` requires exact `MXO_DEV_COMMANDS=1`, the actual configured strict `mxotest_` database, current live in-world caller outside LA/pending/death and character other than0/2/TheNeo. It grants nothing and changes no health/resources/live RNG. 33 is explicitly diagnostic-only. There is no production outcome override.

## Claude-only native one-pass checklist (pending)

Use discovered `typed-attack` fixture; no setup from an agent. CSR110 has5000HP/2000IS, normal owned loadout, no GM and no live RNG override.

1. Open Abilities; verify normal134/1112 and105/129 plus314/383 are present. Choose1112, approach WinterFrost, enter close combat and queue134 through its normal ability control. Verify next-move display, round animation, hit/miss and HP behavior; server log must report selected1032. Native choreography/parity is not established by wire assertions.
2. Outside combat use314 normally. Compare `?combatdepth specialattack 33` before/after: direct total increases108 and seeded hit count changes. Treat this solely as a diagnostic, not an accessible queued viral cast.
3. Load/activate129, use captured383 on self, enter close combat and queue105. Verify ordinary controls and selected524 log. Compare the read-only105 diagnostic before/after383 (+46 direct524). Existing ranged percentage convention remains OURS.
4. Repeat a normal queued134 against a legal enemy helper (flagged enemy organization). A second native helper may cast owned97 normally on CSR110; compare134 diagnostic before/after (-10 direct1032) and verify actual modifier/expiry presentation. No fake helper mutation command is required.
5. Switch back to ordinary melee and ranged attacks; verify those paths work as before. Do not infer exact retail hit probabilities or full Run Program parity from this fixture.
