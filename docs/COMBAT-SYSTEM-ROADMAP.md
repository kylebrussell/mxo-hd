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
- **Tactics system** implements rock-paper-scissors combat (Phase 5 partial)
- **Inner Strength** drains during combat and tactic changes

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
- FX system has 100+ combat effects ready (randomly selected)
- Combat ends properly: player death, mob death, or player flees
- **Ranged combat** fully functional with distance-based damage falloff
- **Bullet hit FX** from multiple angles for visual variety
- **Combat Tactics** (Speed/Power/Grab) affect damage with counter system
- **Inner Strength drain** during combat (2 IS/round) and tactic changes (10 IS)

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
  - File: `CombatCalculator.cs` - Added `CombatTactic` enum and modifiers
  - Rock-paper-scissors system: Speed > Power > Grab > Speed
  - Tactics affect damage dealt/received:
    - Speed: 85% damage, 110% damage taken, 10% IS cost reduction
    - Power: 120% damage, 105% damage taken, 10% IS cost increase
    - Grab: 95% damage, 80% damage taken (defensive)
  - Counter bonuses: ±25% damage when tactic beats opponent
  - `ProcessChangeTactic()` now changes tactic with IS cost (10 IS)
  - `CombatSession.SetAttackerTactic()` tracks current tactic

- [ ] **5.2 Combat animations sync**
  - Coordinate attack animations between combatants
  - Use animation IDs from data files
  - Sync with damage timing
  - TODO: Needs original animation timing research

- [x] **5.3 Inner Strength (IS) integration**
  - Combat drains IS per round (2 IS per tick via `DrainCombatIS()`)
  - Tactic changes cost IS (10 IS per change)
  - IS sent to client via `sendISCurrent()`
  - `CombatCalculator.CalculateAbilityISCost()` applies tactic modifiers
  - `CombatCalculator.CalculateISRegen()` for out-of-combat regen (5% max IS)

- [ ] **5.4 Ability integration**
  - Combat abilities (stun, buffs, debuffs)
  - Wire to existing AbilityHandler
  - Use FX effects from FX.cs
  - TODO: Wire abilities to combat tick

### New Methods Added
- `CombatCalculator.GetTacticDamageModifier()` - Counter system bonuses
- `CombatCalculator.GetTacticBaseDamageModifier()` - Base tactic effects
- `CombatCalculator.GetTacticDefenseModifier()` - Defensive modifiers
- `CombatCalculator.CalculateMeleeDamageWithTactics()` - Tactic-aware melee
- `CombatCalculator.CalculateRangedDamageWithTactics()` - Tactic-aware ranged
- `CombatCalculator.CalculateMobDamageWithTactics()` - Mob damage with player defense
- `CombatSession.SetAttackerTactic()` - Change tactic with IS cost
- `CombatHandler.DrainCombatIS()` - Drain IS per combat round
- `Mob.updateCombat(CombatTactic)` - Now accepts player's tactic for defense

---

## Research Needed

These items require investigation or external resources:

- [ ] **Original packet captures** - Do any exist from live servers?
- [ ] **Combat formula documentation** - Any community research on original mechanics?
- [ ] **Animation timing data** - How long do combat animations take?
- [x] **Tactic effects** - Implemented as rock-paper-scissors with Speed/Power/Grab

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
- [ ] IS drains during combat (visual confirmation)
- [ ] Tactic change costs IS

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
