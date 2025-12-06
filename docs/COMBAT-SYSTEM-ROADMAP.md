# Combat System Roadmap

This document tracks the work needed to bring the combat system from its current test/stub state to a functional implementation.

## Current State Summary

The combat system exists but is largely non-functional:

- **CombatHandler.cs** sends hardcoded hex test packets instead of structured data
- **Mob.updateCombat()** is completely empty - NPCs never fight back
- **Range combat** is a stub method with no implementation
- **No damage calculations** exist - no formulas, no stat integration
- **No combat session tracking** - nothing tracks who is fighting whom

### What Works

- ILCombatHandler (Object55) game object spawns correctly
- View ID management for combat entities
- `Mob.HitEnemyWithDamage()` can reduce health and broadcast to clients
- FX system has 100+ combat effects ready
- Message queue infrastructure is solid

### Key Files

| File | Purpose | State |
|------|---------|-------|
| `hds/world/Client/RpcHandlers/CombatHandler.cs` | Main combat RPC handler | Hardcoded test packets |
| `hds/world/Structures/Mob.cs` | NPC entity with combat state | `updateCombat()` empty |
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

## Phase 2: Player Attacks Mob

**Goal:** Player can hit mobs, deal damage, and kill them.

### Tasks

- [ ] **2.1 Implement basic damage calculation**
  - Create `CombatCalculator` class or static methods
  - Simple formula: `baseDamage * (attackerLevel / defenderLevel) * random(0.8, 1.2)`
  - Consider weapon equipped (if any)

- [ ] **2.2 Wire combat updates to Mob.HitEnemyWithDamage()**
  - In `CombatHandler.UpdateCloseCombat()`:
    - Get target mob from combat session
    - Calculate damage
    - Call `mob.HitEnemyWithDamage(damage, fxId)`
  - Verify health updates broadcast to all nearby clients

- [ ] **2.3 Implement mob death**
  - When `mob.healthC <= 0`:
    - Set `mob.is_dead = true`
    - Call `SendNpcDies()` (already exists in MobPackets.cs)
    - Set `mob.is_lootable = true`
    - End combat session

- [ ] **2.4 Enable loot system**
  - File: `hds/world/Client/RpcHandlers/PlayerHandler.cs:240`
  - Currently disabled with "send loot disabled" message
  - Wire up `SendLootWindow()` when mob dies
  - Implement `ProcessLootAccepted()` to give items

---

## Phase 3: Mob Fights Back

**Goal:** Mobs attack the player during combat.

### Tasks

- [ ] **3.1 Implement Mob.updateCombat()**
  - File: `hds/world/Structures/Mob.cs:449`
  - Currently empty method
  - On timer tick:
    - Check if still in combat
    - Calculate damage to player
    - Send damage packet to player
    - Update player health

- [ ] **3.2 Player health updates**
  - Reduce player HP when hit
  - Broadcast health change to client
  - Use existing `Health` attribute on player instance

- [ ] **3.3 Implement player death**
  - When player health <= 0:
    - Set death state
    - End combat
    - Trigger respawn flow (hardline?)

- [ ] **3.4 Combat end conditions**
  - Player dies -> combat ends
  - Mob dies -> combat ends
  - Player leaves range -> combat ends (ProcessLeaveCloseCombat)
  - Timeout -> combat ends

---

## Phase 4: Range Combat

**Goal:** Implement ranged/gun combat.

### Tasks

- [ ] **4.1 Implement ProcessRangeCombatRequest()**
  - File: `CombatHandler.cs:105-108`
  - Currently empty stub
  - Similar flow to close combat but:
    - Different animations
    - Range checking
    - Ammo considerations?

- [ ] **4.2 Range-specific damage calculation**
  - Factor in distance
  - Different weapon types (pistol, SMG, rifle)
  - Cover/line of sight (future)

- [ ] **4.3 Range combat animations**
  - Shooting animations for player
  - Hit reactions for mobs
  - Muzzle flash FX

---

## Phase 5: Polish & Systems

**Goal:** Full combat experience with tactics and animations.

### Tasks

- [ ] **5.1 Implement tactics (CT) system**
  - `ProcessChangeTactic()` currently just stops timer
  - Combat Tactics should affect:
    - Damage dealt/received
    - Speed/accuracy
    - Special moves available

- [ ] **5.2 Combat animations sync**
  - Coordinate attack animations between combatants
  - Use animation IDs from data files
  - Sync with damage timing

- [ ] **5.3 Inner Strength (IS) integration**
  - Combat abilities use IS
  - Drain IS on ability use
  - Regenerate IS over time

- [ ] **5.4 Ability integration**
  - Combat abilities (stun, buffs, debuffs)
  - Wire to existing AbilityHandler
  - Use FX effects from FX.cs

---

## Research Needed

These items require investigation or external resources:

- [ ] **Original packet captures** - Do any exist from live servers?
- [ ] **Combat formula documentation** - Any community research on original mechanics?
- [ ] **Animation timing data** - How long do combat animations take?
- [ ] **Tactic effects** - What did each CT actually do?

---

## Testing Checklist

Use these scenarios to verify combat works:

- [ ] Player initiates combat with mob
- [ ] Player deals damage to mob
- [ ] Mob health decreases visibly
- [ ] Mob dies at 0 health
- [ ] Mob corpse is lootable
- [ ] Mob attacks player back
- [ ] Player health decreases
- [ ] Player can die in combat
- [ ] Combat ends when leaving range
- [ ] Multiple players can fight same mob
- [ ] Range combat works
- [ ] Tactic changes affect combat

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
