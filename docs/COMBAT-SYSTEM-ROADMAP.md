# Combat System Roadmap

This document tracks the work needed to bring the combat system from its current test/stub state to a functional implementation.

## Current State Summary

The combat system is functional with bidirectional combat and tactical options:

- **CombatHandler.cs** uses event-driven CombatSession architecture
- **CombatCalculator.cs** provides damage formulas with level scaling and tactic modifiers
- **Mob.updateCombat()** attacks player back during combat (Phase 3 complete)
- **Player death** triggers death state, respawn via hardline
- **Range combat** is fully implemented (Phase 4 complete)
- **Distance-based damage** for ranged combat with falloff
- **Loot system** works - shows money reward on mob kill
- **Tactics and rounds** follow retail's rules, taken from the combat scripts in the client (see "Close combat by retail's own rules" below)
- **Inner Strength** costs nothing in a fight, as in retail; Block regains it and regen is slower

### Real-client check (2026-11-07, 7.6005 client)

Before this check the server-side combat ran (damage, death, loot), but the client showed none of it. Melee against a Blackwoods now plays out on screen:
- **Engage:** double-click → "You enter combat".
- **Damage:** the mob's health bar drains and damage numbers float over both fighters, and the player's health bar drops.
- **Death:** at 0 HP the NPC stops offering "Close Combat" and the Corpse window opens.
- **Loot:** Take All pays out (`[LOOT] Player received N info`) and the corpse dissolves into code.

What was wrong:
- **Per-tick update to the combat handler.** Every tick sent a hard-coded blob to the combat handler (ILCombatHandler) view. It didn't fit the object's layout, so the client dropped the rest of the packet, the health updates included. Retail sends the handler's state once, at combat start, and nothing per round. The per-tick update is removed.
- **Player's health.** It was sent in the NPC hit format to the own view (view 2), which has its own attribute layout. It is now `02 00 02 80 80 80 40 <hp>`: own-view attribute 27, as retail sent it.
- **Hit effects.** NPC hits carried effect ids from `FX.cs` that aren't valid ids. Retail ids are `0x28xxxxxx`; most `FX.cs` entries are stored byte-reversed, and the hand-combat ones aren't in that family at all. Retail sent most hits as health alone (6,531 against 448 with an effect), so melee and ranged hits now do the same: `04 80 80 80 40 <hp>`.
- **Delete-view.** `sendDeleteViewPacket` wrote an extra `00` after the view id. The client then read the next view id one byte off and dropped everything after it. That is where the NPC death went.
- **NPC death.** It is now the retail NPC death: `05 01 c2 02 00 80 80 e0 01 00 00 80 80 03 fd 07 00 00 00 00 00 00`. That sets IsDead=1, Health=0 and CurrentState=0x7fd, and the death goes out before the combat teardown.
  - The retail death I first copied (`04 c2 …`) came from a player in PvP, which uses another layout.
  - Adding a guessed movement part (`02 01 0d`) made the client reject the update.

Later the same day:
- **Death reaches everyone.** The NPC death now goes to every player who has the NPC spawned, like the health updates already did.
- **Corpse window is not reliable.** It opened in 3 of 4 kills. The one miss had a byte-identical death and loot sequence, so it varies on the client side.

Ranged attacks, tried 2026-11-07 with a revolver and a rifle equipped:
- **The client asks to draw first.** The tooltip over a mob changes to "Fire!". A double-click then sends RPC `0x3d 01` (draw / combat stance), and `0x3d 00` a few seconds later.
- **The server now answers.** Retail answered with the own-view CombatantMode `02 00 02 80 80 80 80 80 80 02 21` (`22` for at rest), and the server now does too.
- **The client still never fires.** It never sends the ranged attack (`0x41 <target>`). Retail clients sent `3d 01`, `41` and `40` together in one packet, without waiting for an answer, so the client itself decides not to fire. Loading FirearmsAbility, HandgunsAbility, RiflesAbility and GunmanAbility for the character didn't change that. Unknown so far: what the client checks before firing.

Ranged attacks again, 2026-11-07, after the post-combat lock fix (still not firing):
- **The attack slot is the lead.** The last byte of the close-combat request names the slot the attack comes from: `40 <target> 01 6b` for fists (0x6b), and retail's gunfire sent `40 <target> 00 69`, the weapon slot (0x69). Client RPCs `3e <slot> 00 10 00 00 00 <flag>` and `3f <slot> 00 10 00 00 00` set that slot. After the tutorial equipped the Redpill Special, the retail client sent `3e 69 … 01` and `3f 69 …` by itself. Ours only ever sends `3e 6b`/`3f 6b`, before and after an equip, so its Free Fire button keeps showing a fist and a double-click only draws (`3d 01`, then `3d 00`).
- **What retail sent with the equip** (luizoe Unknown log, line 11216): `65 04 00 69 00 00` (item move), own-view EquippedItemID, then `80 bc 56 00 <id u24> <weapon goid u32> 0c 02 …` (a bonus record: the Redpill Special adds RangeCombatTactics, ability index 0x20c), two `80 bd 05 11 00 00 00 00 00 00 00/01`, and `81 67` (the player's name record). At login, retail sends nine `80 bc 15 00 <id> 00 00 11 00 00 00 <ability index u16> <level u16> 00 00 01 00 00 00 00` records: RangePhysicalDamage, MeleeCombatTactics, RangedDefenseTactics, Withdraw, MeleePhysicalDamage, MeleeDefenseTactics, RangeCombatTactics, ViralDeflection, ThrowDefenseTactics.
- **Tried without success:** the starter Redpill Special instead of other guns; HandGunCombatTactics, HandGunDamage, RangeCombatTactics and CombatTactics abilities on top of the four above; retail's login `80 bc 15` set plus the weapon's `80 bc 56` record; the equip-time `80 bc 56` and `80 bd` pair; firing from range and from point blank. None made the client select slot 0x69 or send `41`. Untested: `81 67` at equip, and the inventory record's flag bytes (we send `00 00 00 0c` for every item; retail's vary).

Why the client never fired, found 2026-11-07 (late):
- **The client fires the hotbar's Free Fire weapon, and ours had none.** Client RPC `3f <inventory slot u16> <kind u32>` reports the Free Fire choice: `3f 6b 00 00 00 00 00` is none (fists), `3f 04 00 10 00 00 00` the item in bag slot 4. The last byte of the `40` sent with a shot is that slot.
- **Retail's client picked a gun by itself when one arrived through the item-add RPC `5e`.** In the tutorial capture (crashover2k `..._uriah.log.txt`, line ~12247) looting put the Redpill Special into bag slot 4 (`5e 04 00 05 0a 00 00 00 00 00 14 01`) and the client at once sent `3f 04 00 10 00 00 00`. It then fired with the gun still in the bag: `3d 01`, `41 06 00 f2 00`, `40 06 00 f2 00 00 04` (line 13648). The `3f 69` after an equip is the client following the picked item to the weapon slot, not the equip itself.
- **The choice lasts for one client process.** At world entry the client sends `80 be`, `3f <choice>`, `3e 6b ...`; a freshly started client sends `3f 6b 00 00 00 00 00`. TheNeo's guns are in the inventory from login, so nothing ever picked one: "Fire!" over a mob, but only `3d 01`.
- **Checked in the 7.6005 client:** the dev command `?additem 2565` (adds the Redpill Special with `5e`) made our client send `3f 17 00 10 00 00 00` by itself, and the Free Fire button changed from a fist to a gun. Firing itself is still unchecked: a Windows Firewall prompt on screen kept every click from reaching the game as a click on the NPC.
- **Retail ranged fight (tutorial capture 13:30:12-13:30:32, luizoe Unknown log line 11774):** no combat handler object, no `40` echo, no RPC 46. After one `41` the server fired by itself about every 5 s until the NPC died. Each shot: own view `02 00 03 28 <action counter> 01 0c 38 81 00 <target> <x y z f32> <sim ms>` plus attributes (interleaved: mask byte, then that group's values) NoiseLevel, and on the first shot ConditionStateFlags 0x00100000 and CombatantMode 0x21; then RPC `4e 00`. Some shots carry `00 10` instead of `81 00` (maybe misses). The NPC's health goes out as for melee. RPC `81 68` is a combat-log line with a small string table (`<offset of 2nd name u16> <offset of 1st name u16> <type u32: 3 = player hits, 4 = is hit> <four u16 numbers> ... <weapon goid> <name> <name>`); the numbers aren't decoded. The NPC shot back with an action on its own view, `2a <counter> 01 0c 38 81 00 <player view 2 + spawn id> <yaw> <x y z f32>`. The death was the usual NPC death; the client then sent `3d 00` itself.
- **The server now does the player's side of that:** shots every 5 s from the first `41`, the companion `40` ignored, misses (`00 10`, no damage) and the `81 68` combat-log line after every shot. Hit/miss decoded from the tutorial fight: `81 00` = hit, `00 10` = miss, and the log's result field (3 = hit, 4 = miss) matched all ten shots; hits had attack roll > defense roll. The roll ranges and the 72% hit rate are a guess shaped like retail's numbers. The Free Fire pick (`3f`) is stored so the log names the gun. Not done: the NPC's return-fire animation (our NPCs carry no weapon) and log lines for the NPC's hits.

Guns from the NPC side and weapon stats (2026-11-08, code only, not checked in the client):
- **NPC guns come from retail spawns.** `data/mob_parsed.csv` column 21 is the NPC's EquippedItemID (7,707 of 13,141 NPCs carry a gun). The loader keeps it and the NPC spawn sends it (NPC_BASE attribute 33, as retail: `80 80 80 80 a0 b4 02 00 00` = AutomaticRifle1 on "Chopper Jumper", luizoe Unknown log line 32719).
- **NPCs shoot back.** In a gunfight an NPC with a gun fires on its own view, `2a <counter> 01 0c <class> <81 00 hit | 00 10 miss> <player: view 2 + self-spawn id> 00 <x y z f32>` (tutorial capture line 14258, luizoe line 32745), then the player's health and an `81 68` line with the NPC as attacker. NPCs without a gun keep the old counterattack.
- **Weapon stats from the client's data.** `data/weapons.csv` (632 guns) was decoded from `resource/GameObjects/gameobjects.gob` inside `packmaps/gameobjects.pkb`: a 40-byte header, an index of `{goid u32, offset u32, size u32}`, per-record schema and component-list indices, and a fixed "hot" struct per record whose field offsets come from the schema section (`Component/Prop`, type, offset). Fields used: InvItem/GOCategoryID (IWRf rifles and shotguns, IWHg handguns, IWSm SMGs, IWDH dual handguns, IWDS dual SMGs), Tool/Range (m), Tool/ReuseTimer (s), Tool/BaseDamage, Tool/NoiseGenerated. Typical values: handgun 16 m / 4.5 s, rifle 22 m / 3.5 s, SMG 20 m / 3.0 s, dual handgun 16 m / 4.0 s, dual SMG 20 m / 2.5 s, a 26 m / 5.5 s group (sniper rifles, guessed). No clip, ammo, reload or accuracy fields exist for guns.
- **Attack class byte:** client.dll switches on the five categories in that order, and retail captures show 0x37 (rifles), 0x38 (handguns), 0x39 (SMGs), so dual handguns are 0x3a and dual SMGs 0x3b.
- **NoiseLevel:** a shot sets the gun's NoiseGenerated (Redpill Special: 50 = retail's 0x32), and each further shot closes half the gap to 100; retail sent exactly 50, 75, 87, 93, 96.
- **Now used by the server:** fire interval = ReuseTimer; no shot while the target is beyond Tool/Range (the fight ends after three shots' time out of range, our choice); NPCs only shoot within their gun's range. Damage gets +15% per BaseDamage tier above 1, which is a guess: BaseDamage is a small tier (1 to 8 and more) that rises with the level requirement. The ranged damage falloff now gets metres (it used to get world units, so a 6 m shot counted as beyond 50 m).
- **Not done:** other players seeing NPC and player shots, line of sight, and any ammo or reload (none in the data). NPCs starting a gunfight on their own came later (below).

Close combat by retail's own rules (2026-11-08):
- **The client carries the server's combat scripts.** `resource/Python/Monolith.ltp` (zip entries with an `LTPb` signature, Python 2.3 bytecode) holds `combat_calculations`, `combat`, `interlock/combat_resolution`, `interlock/combat_free_attacks` and more. `interlock/combat_resolution.determineResultForStandardCombat` is the round logic the server now follows. `client.dll` has the RPC field names (0x42: TacticalValue, TacticalTotalValue, TacticType; 0x40: TargetID, ForcedInterlock, WeaponToUseSlot; 0x4e: ForcedCombat) and `combatlib.getTacticRoll` at 0x6258da00.
- **Tactics** are `42 <value u16> <total i16> <type u16>`: 0 Speed, 1 Power, 2 Grab, 3 Block. The old enum (None/Speed/Power/Grab) and its rock-paper-scissors damage were invented. Retail answers, in and out of fights, with `80 bc` records (kind 0x15): the old tactic's six off, the new tactic's six on with new record ids. In a fight RPC 47 kind 6 follows. Pressing the current tactic gets no answer. `CombatTactics` has the table.

| Tactic | Ability | Tactic% (attack roll) | Defense% (defense roll) | Damage% | EvadeInterlock | ForceCombat |
|---|---|---|---|---|---|---|
| Speed | 1015 | +15 | -10 | 0 | 0 | -20 |
| Power | 1017 | -10 | 0 | +20 | -20 | 0 |
| Grab | 1016 | +5 | +30 | -15 | -65 | +100 |
| Block | 1013 | -100 | +50 | -100 | +60 | -400 |

- **Rounds** resolve about every 4.5 s (median of 1346 retail RPC 45s; the server used 3 s). `CombatInterlock` builds RPC 47 (a fighter's move decided and the next one; kind 3 new round, 6 next move changed, 4 opening) and RPC 45 (the outcome). Layouts are in its comments and unit-tested byte for byte against the tutorial fight. Fights with these records run without a client error and show a green ring under the player; this wasn't compared with the old build, so the records may not be what causes the ring.
- **Rolls:** each fighter rolls attack against the other's defense (`combat_resolution.getCTandDTotals`). A roll is normal around (base + range) / 2 with deviation (base + range) / 6, clamped to 1..base + range; the range is 100 for attack, 69 for defense. **Guess:** base = level x the tactic's percent, because this server doesn't model the Combat Tactics and Defense Tactics stats or Consistency. A fighter on Block doesn't hit.
- **Damage** is `combat_calculations.GetCloseCombatDamage` and `combat_resolution.calcDamageForLoser`: bonus, Gaussian (1/3 to 4/3), tactic Damage%, x 4, less the absorbed share from `GetDamageReductionValue` (0.2 per level above the attacker plus armor, capped at +-0.9), at least 1. RPC 45 and 81 68 carry the absorbed amount, negative when the defender is lower level (retail: 21 done, -5 absorbed). **Guess:** bonus = 4 + level / 10, because the DamageBonus stat isn't modelled; it matches retail's level 1 value of 16 before absorption.
- **Special moves:** an instant ability used on the close-combat target becomes the next move (RPC 47 kind 6 at once) and lands with the next round if the player's roll wins; the NPC doesn't hit that round and RPC 45 says 0x100 (`handleSpecialMove`). If the roll loses, the round is a plain exchange and the ability is lost. Abilities with a cast time still land when the cast ends. Cheap Shot and Head Butt have damage ranges in `AbilityTargets` inferred from a few retail health drops (a guess), and Cheap Shot has retail's effect id.
- **Energized and Precise:** RPC 42's TacticType 1 and 2 (client.dll's IExx and IPxx button events). Energized gives +15% damage and -15% attack roll, Precise the reverse (`combat_calculations`).
- **Combat log:** the u16 after the weapon in `81 68` is the attacker's tactic. That's where "Speed Redpill Special" comes from. 81 68 is gunfire only; melee uses RPC 45.
- **Conditions:** Speed causes Daze (0x02), Grab Off Balance (0x04), Power Staggered (0x08) on the NPC's ConditionStateFlags for 10 s. Staggered adds 5% damage taken. **Guess:** the 25% chance per landed hit; captures show the conditions but not their odds. Retail's vulnerability scripts exist, but RPC 4f was all zeros in all 109 captures, so they appear to have been switched off. **Not done:** the conditions on the player's own view.
- **Inner Strength follows retail:** tactic changes and rounds cost nothing, and a round on Block regains 10 (`combat.GetInnerStrengthUsed`; captures show IS regenerating through tactic changes). The first version's 10 per change and 2 per round were invented and are now 0. Regen goes on in a fight at a fifth of the normal rate, for Inner Strength and health. The CombatEffectAbility records, sent at the start and end of a fight, put -80 on both regen rate stats. **Inferred:** that -80 means 80% slower; the scripts don't state it. In 20 captures in-fight IS regen ran at a median 0.42 of the out-of-fight rate, and at 0.25-0.33 in the logs with the lowest ratios.
- **NPCs with guns open fire:** when an aggroing NPC has a gun and the player is within its range, it stays where it stands, faces the player and fires (`CombatHandler.StartNpcGunfight`, retail: entilsar lastday2 21:48:40-41: own-view in-combat flags, CombatEffect records and 81 68, no combat handler object, no 40/46). The player's own 41 replaces the session with a two-way gunfight; the fight ends after three shots' time with the player out of the NPC's reach. A stunned NPC holds its fire and doesn't close in.
- **Where the tactic controls are:** Speed, Power, Grab, Block and Withdraw are entries under "Awakened" in the client's Actions window (O). A double-click sends RPC 42. The client then shows the tactic's icon at the top of the screen. `?tactic speed|power|grab|block` (with `MXO_DEV_COMMANDS=1`) runs the same handler from chat. `client.dll` also has an interlock window with its own tactic buttons (id 0xe, `viewinterlock.cpp`), which never appeared in our fights; what opens it is unknown (the code path is 0x6219f1b0, called from 0x625c049b). Not done: more than one NPC attacking at once, withdraw rolls (`determineResultOfWithdrawalRequest`, `CombatWithdrawIS`), the fight animation entries of the combat view, finishing moves, and retail's gun damage and roll formula (`GetFreeAttackDamage`; guns keep the earlier estimate, with only the tactic's Damage% added).
- **Checked in the 7.6005 client (one client, 2026-11-08):**
  - **Tactics:** double-clicking them in the Actions window sends RPC 42, in and out of fights. The records go out as retail's and the on-screen icon follows (fist for Power, open hand for Block). Rolls and damage in the log change with the tactic.
  - **Conditions:** the client shows "STAGGERED" on the NPC, and Daze or Off Balance flags go out without a client error.
  - **Inner Strength:** no drop on a tactic change or a round. It regenerated about 1.6 per 2 s tick in a fight, and rose from 191 to the 200 cap on Block rounds.
  - **Abilities:** Cheap Shot and Head Butt become the next move and land with the round. An ability on a target other than the fight's switches the fight to that target.
  - **Stun:** Stun 1.0 landed and the next round logged "NPC stunned". Stun 2.0 was used, but the NPC died before it landed. The stun's on-screen effect wasn't checked.
  - **Gun NPCs:** Crow Bars Braggart and A.S.P. Major opened fire with no client input. The client's log said "A.S.P. Major successfully hit TheNeo with a Speed Dual Stitchers for 8 damage". The fight ended three shots after the player left its range. Firing back with the Free Fire button made it two-way ("TheNeo failed to hit A.S.P. Major with a Power Redpill Special").
  - **Not checked:** Energized and Precise (no control for them was found), Grab and Block in the log's weapon line, RPC 45's effect on screen beyond the absence of errors, and whether the green ring and camera come from these records.
- **Testing notes:** `MXO_MOBS=test` loads `data/mob_just_some_and_winter.csv` instead of the whole world (`mob_parsed.csv`). Only its rows with a weapon in column 21 have guns: Crow Bars Braggart at -35071,95,-18733 and A.S.P. Major at -67797,-505,-56877 work, with `MXO_AGGRO_LEVEL_GAP=100` for level 50 TheNeo. The large white figure and snow at the Blackwoods spot are its `WinterFrost` NPC. `?gotopos` during a fight moves the server's position but not the client's view; relog with `-SetPosition` instead. Double-clicking an NPC requests close combat even with a gun picked; select it with one click and press the Free Fire button.

NPC respawn (2026-11-07): a killed NPC's corpse stays for 30 s, long enough to loot it. Then the NPC is unspawned, and 90 s after the death it comes back at its spawn point with full health, as a new view. `MXO_MOB_CORPSE_SECONDS` and `MXO_MOB_RESPAWN_SECONDS` change both times. Mission NPCs don't respawn; their views are deleted when the mission ends. Players who arrive after a death don't get the corpse, which used to spawn standing with 0 health.

To repeat the check, start the server with `MXO_MOB_WANDER=0` so the test mobs stay on their spawn point, then:
```
tools/RealClient/Invoke-RealClient.ps1 -Relaunch -Handle TheNeo -SetPosition 24500,500,-1900 -SetRotation 126 -Login -WaitWorld -DoubleClick 500,640
```
After about 60 s, `-Click 348,358` presses Take All.

### What Works

- ILCombatHandler (Object55) game object spawns correctly
- View ID management for combat entities
- `Mob.HitEnemyWithDamage()` reduces mob health and broadcasts to clients
- `Mob.updateCombat()` reduces player health and sends damage packet
- `FindMobByViewSpawnId()` looks up target mob from client's view
- `CombatCalculator` applies damage with level-based scaling for both directions
- Mobs die when health reaches 0, marked as lootable
- Players die when health reaches 0, can respawn via hardline
- Loot window shows on mob death, awards money based on level
- Kill XP follows retail's `experience.ComputeExperience` (`KillExperience.cs`): 67 x 3^((1 + NPC level - level) / 7) x 5 x (level - 1), so 391 at level 1 or 2 against the same level, 3,527 at level 10, 19,204 at level 50. The 67 (weakling) and 135 (tough) multipliers are inferred from all 15 kill rewards in the captures, which the formula reproduces exactly. Every NPC counts as a weakling (our NPC data has no rank), and the killer gets the whole reward (no damage shares or team bonus yet). It was NPC level x 100.
- FX system has 100+ combat effects ready (randomly selected)
- Combat ends properly: player death, mob death, or player flees
- **Ranged combat** fully functional with distance-based damage falloff
- **Bullet hit FX** from multiple angles for visual variety
- **Combat Tactics** (Speed/Power/Grab/Block) change rolls and damage by retail's numbers
- **Inner Strength** regains 10 a round on Block and regenerates at a fifth of the rate in a fight

### Key Files

| File | Purpose | State |
|------|---------|-------|
| `hds/world/Client/RpcHandlers/CombatHandler.cs` | Main combat RPC handler | **Functional** - event-driven with CombatSession |
| `hds/world/Structures/CombatSession.cs` | Combat state tracking | **NEW** - tracks combatants, timer, events |
| `hds/world/Structures/CombatCalculator.cs` | Damage formulas | **NEW** - level-based damage with variance |
| `hds/world/ServerPackets/CombatPackets.cs` | Combat packet builders | **NEW** - structured packet methods |
| `hds/world/Structures/Mob.cs` | NPC entity with combat state | **Functional** - `updateCombat()` attacks player |
| `hds/world/Structures/FX.cs` | Visual effect IDs | Ready to use |
| `hds/world/ServerPackets/MobPackets.cs` | Mob packet builders | Has `SendNpcDies()` |
| `hds/resources/gameobjects/definitions/AttributeClasses/AttributeClass3664.cs` | ILCombatHandler object | Working |

---

## Phase 1: Foundation ✅ COMPLETE

**Goal:** Understand existing code and create proper structure without changing behavior.

**Completed:** 2024-12-06

### Tasks

- [x] **1.1 Decode hardcoded combat packets**
  - File: `CombatHandler.cs`
  - Added detailed inline comments explaining packet structure
  - Documented byte-by-byte breakdown of combat data blobs
  - Identified key fields: positions, health, animation IDs, FX IDs

- [x] **1.2 Create CombatPackets.cs**
  - Location: `hds/world/ServerPackets/CombatPackets.cs`
  - Created structured packet builders:
    - `SendEnterCombatMode()` - Sets player combat stance
    - `SendLeaveCombatMode()` - Removes combat stance
    - `SendCombatInitialize()` - Links combatants
    - `SendCombatUpdate()` - Timer tick updates
    - `SendCombatHit()` - Damage/FX notifications
    - `SendCombatEnd()` - Cleanup

- [x] **1.3 Create CombatSession class**
  - Location: `hds/world/Structures/CombatSession.cs`
  - Tracks combat state:
    - Attacker (WorldClient)
    - Defender (Mob or WorldClient for PvP)
    - Combat start time, round counter
    - Combat type (melee/ranged)
    - Hit counter for animations
    - Event callbacks for ticks and end
  - Built-in timer management

- [x] **1.4 Refactor CombatHandler to use new structures**
  - Now creates CombatSession on combat start
  - Uses CombatPackets for structured packet sending
  - Event-driven tick handling
  - Proper cleanup on combat end
  - Legacy fallback maintained for compatibility

---

## Phase 2: Player Attacks Mob ✅ COMPLETE

**Goal:** Player can hit mobs, deal damage, and kill them.

**Completed:** 2024-12-06

### Tasks

- [x] **2.1 Implement basic damage calculation**
  - Created `CombatCalculator.cs` with static methods
  - Formula: `baseDamage * (attackerLevel / defenderLevel) * random(0.8, 1.2)`
  - Separate formulas for melee, ranged, and mob damage
  - Random hit FX selection for visual variety

- [x] **2.2 Wire combat updates to Mob.HitEnemyWithDamage()**
  - `HandleCombatTick()` now:
    - Gets target mob from combat session (via `FindMobByViewSpawnId`)
    - Calculates damage using `CombatCalculator`
    - Calls `session.ApplyDamageToDefender(damage, fxId)`
  - Mob health updates broadcast to all nearby clients via existing `HitEnemyWithDamage`

- [x] **2.3 Implement mob death**
  - When `mob.healthC <= 0`:
    - `CombatSession.MarkDefenderDead()` sets `is_dead = true`, `is_lootable = true`
    - Stops mob AI updates (`isUpdateable = false`)
    - `SendNpcDies()` sends death animation using correct mob view ID
    - Combat session ends automatically

- [x] **2.4 Enable loot system**
  - Added `pendingLootMoney` and `hasLootPending` to `ClientData.cs`
  - `HandleCombatEnd()` calculates loot money (50-150 per mob level)
  - `SendLootWindow()` called automatically when mob dies
  - `ProcessLootAccepted()` uses pending loot amount instead of hardcoded 5000
  - Money saved to DB and UI updated

### New Files Created
- `hds/world/Structures/CombatCalculator.cs` - Damage formulas and FX selection

### Modified Files
- `hds/world/Client/ClientData.cs` - Added pending loot tracking fields

---

## Phase 3: Mob Fights Back ✅ COMPLETE

**Goal:** Mobs attack the player during combat.

**Completed:** 2024-12-06

### Tasks

- [x] **3.1 Implement Mob.updateCombat()**
  - File: `hds/world/Structures/Mob.cs:485`
  - Mob tracks its combat target (`WorldClient combatTarget`)
  - On combat tick:
    - Gets player health from `playerInstance.Health`
    - Calculates damage via `CombatCalculator.CalculateMobDamage()`
    - Updates player health attribute
    - Sends damage packet to player self-view (view ID 2)
    - Returns true if player died

- [x] **3.2 Player health updates**
  - Player HP reduced on each combat tick
  - Health packet sent to player's self-view
  - Same packet format as mob damage: `04 80 80 80 c0 <health> c0 <fxId> 01 <hitCounter>`
  - `playerInstance.Health.setValue()` keeps server state in sync

- [x] **3.3 Implement player death**
  - When player health <= 0:
    - `HandlePlayerDeath()` called from `HandleCombatTick()`
    - Sets `playerInstance.IsDead` to true
    - Sends self-view update with death state
    - Player can respawn via hardline
    - TODO: Proper death animation/effects, respawn timer

- [x] **3.4 Combat end conditions**
  - Player dies -> `CombatEndReason.AttackerDied`
  - Mob dies -> `CombatEndReason.DefenderDied`
  - Player leaves range -> `CombatEndReason.AttackerFled`
  - `ProcessLeaveCloseCombat()` stops session
  - Mob's combat target cleared on end (`clearCombatTarget()`)

### New Methods Added
- `Mob.setCombatTarget(WorldClient)` - Sets mob's attack target
- `Mob.getCombatTarget()` - Gets current target
- `Mob.clearCombatTarget()` - Clears target on combat end
- `Mob.updateCombat()` - Calculates and applies damage to player
- `Mob.SendPlayerDamagePacket()` - Sends health update to player
- `CombatHandler.HandlePlayerDeath()` - Handles player death state

### Modified Files
- `hds/world/Structures/Mob.cs` - Added combat target tracking and updateCombat()
- `hds/world/Client/RpcHandlers/CombatHandler.cs` - Wired mob attacks, player death handling

---

## Phase 4: Range Combat ✅ COMPLETE

**Goal:** Implement ranged/gun combat.

**Completed:** 2024-12-06

### Tasks

- [x] **4.1 Implement ProcessRangeCombatRequest()**
  - File: `CombatHandler.cs:333-399`
  - Full implementation matching close combat flow
  - Creates CombatSession with `CombatType.Ranged`
  - Faster tick rate (2 seconds vs 3 for melee)

- [x] **4.2 Range-specific damage calculation**
  - `CombatCalculator.CalculateRangedDamage()` with distance parameter
  - `CombatCalculator.CalculateDistance()` computes player-mob distance
  - Distance-based damage falloff:
    - Close (< 5m): 90% damage (too close)
    - Optimal (5-30m): 100% damage
    - Medium (30-50m): 80% damage
    - Long (50m+): 60% minimum damage

- [x] **4.3 Range combat FX**
  - 11 bullet hit FX variations from multiple angles (HF, HB, MF, MB, etc.)
  - Muzzle flash FX defined (ready for future attacker FX)
  - All angles: front, back, left, right, high, mid, low

### New Methods Added
- `CombatCalculator.CalculateRangedDamage(level, level, distance)` - Distance-aware damage
- `CombatCalculator.CalculateDistance(playerPos, mobX, mobY, mobZ)` - 3D distance calculation
- `CombatCalculator.CalculateDistanceModifier(distance)` - Damage falloff curve
- `CombatCalculator.GetRandomMuzzleFlashFx()` - For future attacker FX

---

## Phase 5: Polish & Systems ✅ PARTIAL

**Goal:** Full combat experience with tactics and animations.

**Completed:** 2024-12-06

### Tasks

- [x] **5.1 Implement tactics (CT) system**
  - File: `CombatTactics.cs`, `CombatInterlock.cs`
  - The first version's rock-paper-scissors damage was invented and is gone; the numbers are now retail's (table under "Close combat by retail's own rules")
  - `ProcessChangeTactic()` answers with retail's `80 bc` records and RPC 47

- [ ] **5.2 Combat animations sync**
  - Coordinate attack animations between combatants
  - Use animation IDs from data files
  - Sync with damage timing
  - TODO: Needs original animation timing research

- [x] **5.3 Inner Strength (IS) integration**
  - A round on Block regains 10 (`ApplyRoundInnerStrength()`, retail's `GainISFromBlocks`); other rounds and tactic changes cost nothing
  - IS sent to client via `sendISCurrent()`; regen in a fight runs at a fifth of the rate
  - See "Inner Strength follows retail" under "Close combat by retail's own rules"

- [x] **5.4 Ability integration**
  - An instant ability on the close-combat target is the next move and lands with the round if the roll wins (`CombatHandler.QueueRoundAbility`, `ResolveCloseCombatRound`)
  - Abilities with a cast time land when the cast ends (`AbilityTargets.Land`), and a stunned NPC neither attacks nor closes in

### New Methods Added
- `CombatCalculator.CloseCombatDamage()`, `DamageReduction()` - Retail's close-combat damage
- `CombatInterlock.RollHit()`, `TacticRoll()` - Retail's rolls
- `CombatInterlock.BuildRoundState()`, `BuildRoundResult()` - RPCs 47 and 45
- `CombatTactics.Select()` - Tactic records
- `CombatHandler.ResolveCloseCombatRound()`, `QueueRoundAbility()`, `StartNpcGunfight()`
- `CombatHandler.ApplyRoundInnerStrength()` - IS per combat round

---

## Research Needed

These items require investigation or external resources:

- [ ] **Original packet captures** - Do any exist from live servers?
- [ ] **Animation timing data** - How long do combat animations take?
- [x] **Tactic effects** - Retail's numbers, from the client's combat scripts and `80 bc` records
- [x] **Combat formula documentation** - The client's `resource/Python/Monolith.ltp` has the server's combat scripts

---

## Testing Checklist

Use these scenarios to verify combat works:

- [x] Player initiates combat with mob
- [x] Player deals damage to mob
- [x] Mob health decreases visibly
- [x] Mob dies at 0 health
- [x] Mob corpse is lootable
- [x] Mob attacks player back
- [x] Player health decreases
- [x] Player can die in combat
- [x] Combat ends when leaving range
- [ ] Multiple players can fight same mob
- [x] Range combat works
- [x] Tactic changes affect combat
- [x] Block regains IS each round (server log and own-view packets)

---

## Notes

### Packet Structure Hints

From `MobPackets.cs`, the pattern for view updates is:
```
[ViewID 2 bytes] [Update type] [Data...]
```

From `Mob.HitEnemyWithDamage()`:
```
04 80 80 80 c0 [health 2 bytes] c0 [fxId 4 bytes] 01 [hitCounter 2 bytes]
```

### CombatantMode Values

Found in code with TODO comments:
- `0x22` - Default combat mode (used for mobs)
- Other values unknown - needs research

### Relevant RPC Headers

From `NetworkProtocolHeaders.cs`:
- `CLIENT_CLOSE_COMBAT = 0x40` (CR2)
- `CLIENT_RANGE_COMBAT = 0x41` (CR2)
- `CLIENT_LEAVE_COMBAT = 0x44` (CR2)
- `CLIENT_CHANGE_CT = 0x42` (CR2)
