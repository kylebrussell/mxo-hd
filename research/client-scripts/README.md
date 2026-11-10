# Client rules scripts

The 7.6005 client's `resource/Python/Monolith.ltp` holds 62 Python 2.3 modules: the game's rules, shared between
the retail server and the client. Every one is here as source, in the pack's own folder layout. They are retail's
own code, but not all of it: names such as `discovery`, `consolevar`, `constants`, `abilitylib`, `combatlib`,
`physics` and `mission` are C++ built-ins of the engine and aren't in the pack, so their values (the `consolevar`
tuning knobs, the `constants.combat.*` numbers) are unknown. Ability numbers (values, durations, tick rates) come
from the ability data, not from these scripts.

## How they were made

- **Source:** `C:\Users\russe\Developer\mxo-research\client-data\dumps\monolith_pyc\`, extracted from the pack with
  `tools/client-data/ltp.py` (magic 62011).
- **Decompiler:** uncompyle6 3.9.3 with xdis 6.1.8 on Python 3.12, one module at a time with a 300 s limit
  (`tools/decompile_all.py`). 61 of 62 modules decompiled cleanly: no "This code section failed" or parse-error
  sections anywhere.
- **`ltfxmap`** (2,356 effect-id assignments in one run) timed out. Its values were recovered by running its
  top-level bytecode as data (`tools/ltfxmap_values.py` with `tools/client-data/pymodule_values.py`) and written
  as `NAME = 0x........` lines; the disassembly has the same 2,356 stores.
- **Python 2 artefacts:** every function ends in an extra `return` (uncompyle6 adds it), `print x` is Python 2
  syntax, and `/` on two ints is floor division. The last matters for the formulas: in `experience` the exponent
  `numerator / 7` steps in sevens.
- `tools/decompile-log.tsv` lists each module, how it was decompiled and its line count. `Missions/Mission986.py`
  is the same script as `research/missions/Mission986-decompiled.py`.

## Where they ran

- `serverrules` (built 2008-11) and `rshooks` (server hooks: "Server version of object") ran on the server; they
  call server-only functions (`addExperience`, `discovery.addHardline`, `rs.claimConstruct`).
- `clientrules` and `clhooks` ran in the client ("Client version of object"); `clientrules` decides which actions
  the client offers and says the server rule does more.
- The `*validate` modules are imported by both, so the client ran the same checks before sending a request.
- The `ability/` modules call server-only functions (`dealAbilityDamage`, `AI.setMostHated`, `Inventory.rmvItem`)
  and none of the client hooks import them. The client just carries them.
- `augments` and the `ability/soldier/old_sm_*` files are imported by nothing: dead code.

## The modules

"mxo-hd" says whether this server does what the module does, and where.

### Combat

| Module | What it computes | Runs on | mxo-hd |
|---|---|---|---|
| `combat` | Entry points for a close-combat round: `ResolveCombat` (who attacks, withdrawals, the round), `PerformFreeAttack` (gun or melee free attack), `GetInnerStrengthUsed` (Block regains 10 with `GainISFromBlocks`, nothing else costs any), `GetTacticalPointsGained` (1, 2 for Energized, 0 on Block), prone-state checks. | server | Yes: `CombatHandler.ResolveCloseCombatRound`, `CombatCalculator.RoundInnerStrengthChange`. Prone states and tactical points: no. |
| `combat_calculations` | The numbers: `GetTacticCombatTacticsMod` (Speed 1.15, Power 0.9, Grab 1.05, Block 0), `GetTacticDefenseTacticsMod` (Speed 0.9, Power 1.0, Grab 1.3, Block 1.5), `GetTacticDamageMod` (1.0, 1.2, 0.85, 0), `GetCloseCombatDamage`, `GetFreeAttackDamage` (guns), `GetDamageReductionValue` (0.2 per level plus armor, capped at +-0.9), `GetShieldDamageValue`, `DamageInterruptsCasting` (33-90% chance to cast through damage), `GetAbilityMitigationDuration` (Determination shortens effects, down to 1/3). | server | Close-combat damage and reduction: `CombatCalculator`. Gun damage (`GetFreeAttackDamage`): the free-attacks PR (`FreeAttacks.cs`). Shields, cast interruption, mitigation: no. |
| `combat_defines` | Prone-state bit masks. | both | No (no prone states). |
| `combatvalidate` | `CanFight`: neither side dead, `CharMvt.isEnemy`, distance under 700 units (7 m), `physics.clearLine`. Also used by `ability/utility` to pick area-effect targets. | both | Ranged: dead and gun-range checks (`CombatHandler.ProcessRangeCombatRequest`). Close combat: only that the NPC exists. No line of sight. |
| `generic_free_attacks` | Rolls for abilities that are free attacks (melee, ranged, thrown, viral): attacker's tactics stat x tactic mod, `getTacticRoll(.., 100)`, against the defender's defense stat x mod, `getTacticRoll(.., 69)`; a loss is `DEFLECTED`. `AbilityOutOfCombat` refuses non-combat abilities while in a fight. | server | No: abilities always land (`AbilityTargets`). |
| `interlock/combat_resolution` | The round: `determineAttacker` (withdrawal, prone, opportunity attack, preferred tactic, multi-on-one, ability level, coin), `getCTandDTotals` (hit when the tactic roll beats the other's defense roll), special moves, Block, finishing and killing moves, withdrawal rolls, the unused vulnerability system. | server | Yes for rolls, special moves and Block: `CombatInterlock.RollHit`, `CombatHandler.ResolveCloseCombatRound`. Withdrawal, finishing moves, multi-on-one: no. |
| `interlock/combat_free_attacks` | `PerformWeaponFreeAttack`: a gunshot hits when the attack roll beats the defense roll, damage from `GetFreeAttackDamage`, less `GetDamageReductionValue` with ranged toughness, at least 1. Picks the shot animation by gun class. `PerformMeleeFreeAttack`: the same with fists (switched off by `consolevar.MeleeFreeFire`). | server | The free-attacks PR (`FreeAttacks.cs`). On master, guns use an invented roll and damage. |
| `interlock/combat_modifiers` | Roll modifiers: multi-on-one (-5 per opponent fought), gun against fists (+5 pistols, +10 SMGs, +15 rifles at range, the same as a penalty at short range), firing into a melee (-15). Abilities can switch each off. `getTacticSpecificArmorPiercingPercent` gives Power 0.5. No script calls these functions, so the engine did, if anything. | server | The free-attacks PR has them, but this server's fights are one on one with fists, so they don't apply yet. |
| `interlock/combat_innerstrength` | Inner Strength per round by tactic: Power -20, Speed -10, Grab -10 (-5 with a martial art), Block +10. No script calls it, and retail captures show no IS spent in fights (`docs/COMBAT-SYSTEM-ROADMAP.md`), so it is an older design or unused. | dead | Not ported, on purpose. |
| `interlock/combat_utility` | Helpers: `isPreferredTactical` (Power beats Speed, Grab beats Power, Speed beats Grab, anything beats Block), bullet time (10%, 60% on bosses and mission targets, 75% PvP), finishing and killing moves, short moves. | server | The free-attacks PR (`FreeAttacks.PreferredTactic`). Bullet time and finishing moves: no. |
| `interlock/combat_abil_level` | Which ability backs a weapon (Handguns, Rifles, Submachineguns), initiative = level + 20/10/0 by tactic. | server | No (abilities aren't checked against the gun). |

### Characters, rewards, crafting

| Module | What it computes | Runs on | mxo-hd |
|---|---|---|---|
| `experience` | Kill XP: rank multiplier x 3^((1 + NPC level - player level) / 7, floored) x share of the damage, then x 5 x (level - 1). Team bonus 1.0, 1.2, 1.5, 1.6, 1.75, 2.1 for 1-6 players. The same shape for decompiling and writing code (/10). The rank multipliers are console variables, so unknown. | server | No: a kill gives NPC level x 100 (`CombatHandler.HandleCombatEnd`). The level table is from client.dll (`PlayerProgression.ExperienceForLevel`). |
| `rshooks` | Server hooks: `GoUpALevel` (health and IS grow, modal messages at levels 3, 8, 9, 10, 15), vendor buyback (VendorPrice / 10, ability codes (i+1)^2 x 200 per level), recycle (/20), pocket change (level x 5 x 0-99 x a scale), golden tickets, mission-map download rolls, data-node sequences, `level_exp_reqs`. | server | Buyback, recycle, level-up stats: `VendorPricing.cs`, `PlayerProgression.cs`. `level_exp_reqs` is the XP from one level to the next and matches `ExperienceForLevel` (4,500 = 5,000 - 500). Pocket change, golden tickets, map download, data nodes, level-up modals: no. |
| `crafting_server` | Crafting rolls on a Gaussian d100 (mean 50, sd 16): compile succeeds when 4 x (skill + 10% of the category ability) + roll > 4 x complexity + 50; info costs; decompile with purity and stability; the Coder buff. | server | No: `CraftingHandler` always succeeds (see `docs/CRAFTING.md`: the cost multiplier and complexity aren't in the client). |
| `recipes` | An old 2006 cookbook with two recipes. | dev tool | `Crafting.cs` uses `data/recipes.csv`. |
| `gameFeatures` | Account features: 200 Mobius Code, 201 hyper-jump beta, 202/203 recapture items by gender; free trials can't have 200, 202, 203. | both | No. |
| `emotes` | 188 emotes and 18 two-person ones (beckon, accept animation, refuse with ShakeHead), with chat messages; moods have minimum levels (Drunk 3 ... Sexy 20). | both | Emotes are echoed to the player only (`PlayerPackets.cs`). No two-person emotes, no mood levels, moods aren't shown to others. |
| `movement` | The 26 demeanour constants (names lost in compiling: they show as 1..26). | both | Mood is passed through as a byte. |

### World objects, missions, NPCs

| Module | What it computes | Runs on | mxo-hd |
|---|---|---|---|
| `serverrules` | Using world objects: `evalContest` (attacker + d100 against defender + 50), containers (exclusive by team, person or mission key), doors and keys, lock picking (always succeeds, gives XP), hacking, traps, security boxes (alarm below -15), luggables (singleton, level, exclusivity), ladders, stance sites, follow, hardlines and constructs, org teleporters. | server | Interactions exist per type (`ObjectInteractionHandler.cs`, `DoorLifecycle.cs`, `MissionStaging.cs`). None of these rules: no contests, interaction XP, traps, ladders, luggable or level checks. |
| `clientrules` | What the client offers: mission keys before hacking or using keys, lock picking hidden in the tutorial, ladder ends, `RequestCombat` through `CanFight`, follow. | client | Not applicable. |
| `clhooks` | Client hooks: neighbourhood ambience effects, recycle info, 34 cinematics, 249 random Bluepill lines, `CanUseGameFeature`. | client | Not applicable. |
| `missionvalidate` | Mission keys: base = key >> 8, part = key & 0xff; part 0 matches any part of the same base; 0xffffffff is never assigned; part 255 is reserved; same base but other part is a PvP opponent. | both | Partly: containers get part 0xff (`MissionStaging.cs`). The player's key is never set, so the client's checks pass. Missions are per player, so cross-player checks never come up. |
| `followvalidate` | `CanFollow`: the team's mission key base matches the NPC's. `CanStopFollow`: the player is the NPC's master. | both | Escorts are per player and start by distance or talking (`MissionRuntime.cs`); no follow request. |
| `trgs` | Hack-construct check, reward encoding, loading `Mission<id>`, mission argument parsing, org-event buffs. | server | Missions run on `MissionRuntime.cs`. |
| `sequence` | NPC waves of Pandora boxes, data nodes and the Kuno toy box: when a wave ends, spawn a reward container. | server | No. |
| `npccommands` | About 20 one-line commands run on a spawned object (attack nearest player, follow, mood, health state, loot, run an ability), used as code strings by `sequence`. | server | No. Small, but only useful with `sequence` and retail's reward tables, which aren't in the client. |
| `NPCTypes` | Six NPC type flags (Swat, Guard, Werewolf, Vampire, Groupie). | both | No. |
| `obj` | Name to object-id table. | both | Not applicable. |
| `abilities` | The list of ability modules to load. | server | Not applicable. |
| `errtofile` | Sends errors to `pyerrors.txt`. | both | Not applicable. |
| `ltfxmap` | 2,356 effect ids by name. | both | `FX.cs` has many, mostly byte-reversed (see `docs/COMBAT-SYSTEM-ROADMAP.md`). |
| `Missions/Mission` | Base class: stage and goal hooks. | server | `TutorialMission.cs`. |
| `Missions/Mission986` | The tutorial. | server | Yes: `TutorialMission.cs`, `TutorialScript.g.cs`. |
| `Missions/Mission185` | An earlier tutorial, the "Tutorial Construct": 28 stages (camera, movement, cellphone, Bill, inventory, a guard fight, a virus, hacking a computer, a hardline). See below. | server | No. |

### Abilities

| Module | What it computes | Runs on | mxo-hd |
|---|---|---|---|
| `tempmods` | 327 callbacks for timed effects, called at start, end, each tick or on health change: damage and heal over time (`Value / Duration x UpdateInterval` a tick), stun and mez (seven permissions off; stun time `Value >> 8`, immunity `Value & 255`), confuse, enrage, root, sneak, Evade's IS drain, debuff sweeps, event items. The values come from the ability data. | server | Partly: `AbilityBuffs.cs` sends stat records, `AbilityTargets.cs` has one damage over time and stuns, `Mob.Stun`. |
| `ability/defines` | Durations (30 s to 60 min) and combat-modifier codes (`MOD_AFFECTS_*`, `MOD_*`). | both | No. |
| `ability/effects` | Corrode an item, destroy an item (or an NPC's weapon), disable an ability and those that depend on it. | server | No. |
| `ability/general` | Detection (d100 + Detect against 50 + 8 x Duration + 4 x the ability), event weapons (Wooden Stake, Silver Bullet, Holy Water 10, x10 or x75 against the weak), EMP 5000, Precise Blows and Energized Attacks exclude each other, jack out. | server | No. |
| `ability/utility` | `evalContest`, sneak detection range 300 + 2000 x (1 - level/50) units in a 60-degree view, `(level - 1) x 3 + 100` health, line-of-sight helpers, auras. | server | No (NPCs read `StealthAwareness` in `MobBehaviour.cs`). |
| `ability/hacker/crafting` | Write Code and Compile play the idle animation. | server | `CraftingHandler.cs`. |
| `ability/hacker/health` | Five resurrects: Restore RSI (health 1, IS near 0); Revitalize, Rejuvenate, Revive, Renew (full). In PvP they add ability 1208. No heal formula. | server | No reviving others. |
| `ability/hacker/misc` | Recall team member, Engage Foes, debuff sweep constants. | server | No. |
| `ability/hacker/simulacra` | Pets: Logic Daemon (levels 4-7 ... 35-50), Patch Daemon (healer), Remote Proxy, decoys (60/120 s); Message Cloud (20 s, pulses every 1 s in 12 m, -80% selection range for 4 s). | server | No. |
| `ability/hacker/virii`, `virii_defines` | Virus hit roll: 4 x (Transmit Virus + Execute Program) + d100 against 50 + 4 x (Awakened level + Resist Viruses); needs a program launcher; vaccinations deflect. Levels 1/2/3 last 10/15/25 s. Corruption destroys the weapon or hacker tool on a d100 roll. | server | No. |
| `ability/npc/specialmoves` | NPC moves (Twin Razor 300, Boost Underlings), NPC detection (d100 + 4 x level), disguises, 24 event-costume captures, event viruses. | server | No. |
| `ability/soldier/misc` | Synaptic Sweep Kick: 12-47 damage to each enemy within 4 m. | server | No. |
| `ability/soldier/old_sm_*` | Older special moves with flat damage (Aikido 15-350, Karate 10-325, Pistol 50-350, Rifle 50-325, SMG 75-300). | dead | Not applicable. |
| `ability/spy/misc` | Ghost Strike (80% re-stealth), Sixth Sense (15 m), Tap Info Bank, invisibility (30 s + 2 x level, -300 speed, no enemy within 8 m in view), disguise, traps, Hamstring (30 s, x0.75 speed, -25 attack), root and mez (60 s). | server | No. |
| `ability/spy/sneaks` | Sneak, Concealment, Escape & Evasion upkeep; sneak attacks always succeed; free-attack animations. | server | No. |
| `augments` | Ten empty augment tables. | dead | Nothing to port. |
| `__init__` (5) | Empty package files. | | |

## Ported from here

- **Free attacks (gunfights), in its own PR:** `hds/world/Structures/FreeAttacks.cs`, from
  `interlock/combat_free_attacks`, `combat_calculations.GetFreeAttackDamage`, `interlock/combat_modifiers` and
  `combat_utility.isPreferredTactical`.

## Not ported, and what it would take

- **Validation (`combatvalidate`, `followvalidate`, `missionvalidate`):** the real client runs the same checks
  before it sends a request, so the server only needs them against a tampered client. The useful one is the
  close-combat check (7 m, alive, line of sight), which needs line of sight the server doesn't have.
- **`tempmods`, `effects`, `virii`:** not tables. A port needs a small timed-effect engine with C# versions of the
  callbacks, and the per-ability values from the ability data or captures. `AbilityBuffs.cs` can carry the stat
  records they produce, but not the behaviour.
- **`sneaks`:** needs NPC facing, line of sight and a stealth state shown to others; about 150 lines on top of those.
- **`specialmoves`:** mostly event content (costumes, holiday viruses). Low value now.
- **`npccommands`:** small, but only useful with `sequence` (Pandora boxes, data nodes) and the reward tables.
- **`Mission185`:** its text is in the client, but its 56 voice lines, its rooms and doors, its NPCs (Bill, the
  guard, the "Firearm Construct") and nine of its goal triggers aren't. It uses seven calls the tutorial runtime
  lacks (`addTempAbility`, `sealDoor`, `unlockDoor`, `setExclusiveWaypoint`, `killNPC`, `getDoorZoneIDs`,
  `activateStage`) and the `OnStartOfStage` and `OnPlayerEnterZone` hooks, and its panel ids predate 7.6005. Running
  it would mean building a new mission on `tutorial_v2`'s rooms, not reusing the tutorial.
- **Kill XP (`experience`):** the shape is known but the rank multipliers aren't; they could be fitted to the kill
  XP in the captures.
