# Utility abilities

Data-node search, light luggable pickup/drop and visible Download Mission Map pings now execute; see [DATA-MINING.md](DATA-MINING.md) for decoded records, evidence, deliberate choices, remaining gaps and the current native checklist. Its P3 implementation supersedes the historical locator/reveal gaps below.

Open Locks, Disarm Traps, Disable Security Devices, Recall Team Member, Recall Mission Team, Download Mission Map, Hack Construct Key and Find Data Node, and what they do to world and mission objects. The code is `hds/world/Structures/UtilityAbilities.cs`.

No capture shows any of these, so every rule comes from the client's own server-side Python in `research/client-scripts`. Item and object numbers come from the 7.6005 `gameobjects.gob`, and lines from `stringtable_client`. Anything marked **ours** is a choice we made.

## Where it hooks in

- **Object uses** (`80 c7 <view> <spawn> <use>`): `ObjectInteractionHandler` calls `UtilityAbilities.OnObjectUse` before the object state machine (`ObjectStates.OnUse`) and the mission runtime see the use. It handles a usable whose `execute_rule` (in `data/object_usables.csv`) is 4000 DoorPickLock or 1012 SecurityPBXDisable, on objects the server follows. It also handles mission objects spawned locked. The object then moves to the state its script gives that rule result (`ObjectStates.StateAfterUse`).
- **Abilities**: `AbilityHandler` calls `UtilityAbilities.Land` when the cast ends, for 119, 382, 486 and 621.
- **Mission accept**: `MissionHandler.Accept` runs Download Mission Map's roll.
- **Hardline booth use 7** (Hack Construct): `TeleportHandler.ProcessBoothAction`.
- **Loaded abilities** come from `AbilityPassives.Level`. **Tools** must be in the bag: an item whose `AbilityPrerequisite/ReqAbilityID` names the ability (the script's `Inventory.findToolForAbility`).

## Open Locks (142): Pick Lock

`serverrules.py DoorPickLock` (rule 4000) as it shipped:

1. Open Locks isn't loaded: `0x7a000141` "Lock picking failed! Make sure you have %1z loaded."
2. No lockpick tool in the bag (Lock_Pick 4, or Digital_Camera 379, the only items whose ReqAbilityID is Open Locks): `0x7a000142` "...Make sure you have a lockpick tool in your inventory."
3. Otherwise it succeeds: `0x7a000145` "Lock Picked Successfully!". "Lock picks always succeed after the progress bar has finished."

There is **no level check**. The rule's contest code (LockPicking skill against `Difficulty x 4`, with failure line `0x7a000144`) sits after an early `return` and never ran, so any level of Open Locks opens any lock. The lines go out as `81 67` ability lines naming ability 142.

On success the object takes its script's result-1 state:
- a door (AttributeClass363/364/365, or TestLockedDoor 638) goes from LockedState `0x836` to ClosedState `0x835`, which offers Open;
- a container (safe, cabinet, desk, dresser, box) goes to UnlockedState `0x841`, which offers Open.

The progress bar is the usable's own data (`PickLockUsage/ActivationTime_BaseDuration` 5 s for doors and 15 s for the Wall Safe, shortened by LockPicking × `ModMult`). We take it that the client runs the bar before it sends the use, as for Hack, so the server answers at once (**ours**, untried). Retail's XP for picking a lock (`RST.LOCKPICK`) uses console variables we don't have, so none is given.

**Mission objects.** `<object id="1" kind="Safe" locked="1" />` spawns a lockable object (one whose LockedState has a rule-4000 Pick Lock) in `0x836`. Until its lock is picked, every other use of it is ignored. Unlock wants a mission key item, which isn't built. Once picked it is an ordinary mission container: Open, then the Search goal. `?missionobject` respawns it unlocked. No shipped mission uses `locked` yet; `tools/HeadlessClient/Fixtures/locked-safe.xml` does.

## Disable Security Devices (218)

`serverrules.py SecurityPBXDisable` (rule 1012) is the "Disable Security Devices" usable of Security_Panel 466 (use 1) and the "Use" of Security_Desk 377:

- Disable Security Devices isn't loaded: chat "Operation failed! Make sure you have the Disable Security Devices ability loaded." Nothing changes.
- No DisableDeviceTool (616): "Operation failed! Make sure you have a disable device tool in your inventory."
- Otherwise the contest is `evalContest(SecuritySystems level, 0, Difficulty, 0)`, that is (A + aMod + d100) − (D + dMod + 50). Below −15 the result is 2: "ALARM TRIGGERED", and the device goes to Alarming `0x83b`. Otherwise the result is 1: "ACCESS GRANTED", and the device goes to Disabled `0x83a`.
- Difficulty is `DisableUsage/Difficulty`: 10 on the panel, 0 on the desk. Security Systems (640) is a skill nobody here has loaded, so it is 0.

These chat lines are the script's own literal strings (`AbilityInv.sendChat`). They go out as system chat.

Not done: the device's effect on cameras and alarms nearby, and the XP.

## Disarm Traps (119)

The ability is cast on an object. `ability/spy/misc.py DisarmTraps_Test` and `_DirectObject`:

- The object must be in its TrappedState `0x839` (`TrapArmed`). Otherwise nothing happens and no line is sent, as in retail.
- A trap nobody has detected (`DetectTrapFX` unset) **goes off**: `0x76000250` "Trap went off while being disarmed!".
- Otherwise the contest is `evalContest(level, TrapDisarmBonus, 0, DisarmDifficulty)`:
  - above 0: `0x7600024e`, and the object goes to DisarmedState `0x83a`;
  - above −25: `0x7600024f`, and nothing changes;
  - lower: `0x76000250`, and the object goes to TriggeredState `0x83b`.
- Every object's `Trap/DisarmDifficulty` in the gob is 0. The only Disarm Traps tool, TrapDetectToolBasic 2500, stores no TrapDisarmBonus, so the bonus is 0.

Not done:
- Detect Traps isn't in the 7.6005 ability table, so only `?trapobj` makes a trap visible.
- The trap's damage (`DmgVol.applyDamage`) has no amount in the data, so a trap that goes off does no damage. It is not turned into generic damage.
- The scripts don't check for a tool, so neither do we.

## Recall Team Member (382), Recall Mission Team (486)

`ability/hacker/misc.py`:

- **Recall Team Member** works on a selected player in the caster's team, other than the caster. The caster gets `0x7a00015b` "You begin to teleport a team member to your location."
- **Recall Mission Team** recalls every online team member except the caster. The caster gets `0x7a000164` "You begin to teleport your team to your location." The script has no `_Subject` for it, so this caster line is **ours**, from the string table.
- Each target (`RecallTeamMember_DirectObject`):
  - **in another district**: the caster gets `0x7a0002a1` "You cannot recall that player. He is in a different construct."
  - **fighting or dead**: skipped. Retail's `ID_CLIENT_TELEPORTING_INCOMBAT` / `_DEAD` lines aren't in the 7.6005 client's table, so no line is sent.
  - **otherwise**: the target gets `0x7a00015a` "Your team member %2s is attempting to teleport you to their location." It also gets the phase-out FX (`FX_CHARACTER_RECALL_TEAM_MEMBER_PHASE_OUT` 0x280006d1, or `_MISSION_TEAM_` 0x280006d2) and is teleported to the caster's position (`MissionStaging.Teleport`).
- Costs and cooldowns are the template's: 30 IS and 30 s for 382, 75 IS and 180 s for 486.

## Download Mission Map (44)

`rshooks.py ComputeDownloadMissionMapOutcome`, run when a mission is accepted:

- The ability value is Download Mission Map's level × 4, plus Map Downloading's (641) level.
- The download fails if (ability + d100) − (difficulty + 50) is below 0.
- Otherwise the access flags are 1, plus 2, 4, 8, 16 and 32 for each further 10 points. They're kept for the mission (`UtilityAbilities.MissionMapAccess`).
- Success plays `FX_CHARACTER_GENERIC_ABILITY_UPGRADE` on the player, as retail did.
- **Ours:** our missions are built at the player's level, so the player's level is the difficulty.

Not done:
- **The reveal.** The flags went back to the server's native mission code, which showed "objectives, enemies, and locked doors" on the team's map. The record that carries them isn't identified. `80 c5` (Positions: DeltaX i8, DeltaZ i8, Relationship u8; DisplaySeconds) looks like a map ping, but its use isn't confirmed, so nothing is sent.
- Retail's `ID_ABILITY_DOWNLOAD_MISSION_MAP_SUCCESS` / `_FAILED` lines aren't in the client's string table.

## Hack Construct Key (166)

`serverrules.py HardlineHackConstruct` (rule 10024) is the hardline's "Hack Construct" (booth use 7). The client itself offers it only to a faction at war.

- Without the ability loaded: `0x60000025` "HackConstruct ability is required."
- Otherwise: `0x60000027` "You must be in a faction to hack constructs." Factions and construct wars don't exist here.

## Find Data Node (621) and the data-mining tiers

**Find Data Node** is the mastery of Locate Data Node (215). It is a toggle that needs a tool:
- DataNodeLocator_HandHeld 422: 30 m, handheld;
- DataNodeLocatorGoggles 423 and BasicDataNodeLocatorGoggles 38209: 5 m, goggles;
- AdvancedDataNodeLocatorGoggles 38208: 15 m.

Without a tool the player gets system string `0x6000002e` "A tool is required to activate this ability." With one it starts with `0x7c000011` "You begin searching for Data Nodes." and stops on the next use with `0x7c000012`.

What the search then does (`LocateDataNode_DirectObject`):
- **Handheld:** a 10 s proximity search. Detect Luggables (541) also asks for the nearest luggable (`requestLuggableLocation`, "this will place a waypoint at the location of the nearest luggable").
- **Goggles:** a visual search every second. See Luggables (540) makes characters within 5 m glow white.

These are the client's records `80 ca` RezID, `80 cb` AngleToDataNode / PercentOfRangeToDataNode, `80 cc`, `80 cd` ID / RezID / DataNodePos (fixed 20 bytes), `80 ce` ID and `80 cf`. Their layouts aren't fully read, and data nodes aren't spawned in the world (only `?spawndatanode`), so none of them is sent.

**Data Node Goggles (538) and Data Tap (539)** are prerequisite tiers of the Data Miner tree. No script reads them.

**Improved Hacking (489)** isn't a lockpick or hack-skill modifier: it is a timed Viral Damage and Healing buff (two StatModifiers, 300 s). It belongs with the numeric buffs and is not changed here.

## Testing

- Unit: `tests/hds.Tests/UtilityAbilityTests.cs`.
- Headless:
  - `utility-mission-locked-safe` (fixture `locked-safe.xml`): a locked mission safe refuses Pick Lock without the ability, then without the tool, then opens and its Search goal completes.
  - `utility-recall-team`: Recall Team Member, then Recall Mission Team, bring a team member to the caster. A player outside the team stays put.
  - `utility-security-and-traps`: a Security_Panel's Disable Security Devices and Disarm Traps on a `?trapobj` safe.

## Native QA (MXO_DEV_COMMANDS=1)

Give the character the abilities first. Load them in the client, or set `char_abilities` rows with `is_loaded=1` on a throwaway character, never TheNeo. Put the tools in the bag (`inventory` rows: Lock_Pick 4, DisableDeviceTool 616, TrapDetectToolBasic 2500, DataNodeLocator_HandHeld 422).

1. **Locked door.** `?objstate 638` spawns TestLockedDoor locked (`0x836`). Right-click it. Expect Pick Lock and Unlock, and no Open.
   - Pick Lock without Open Locks: "Lock picking failed! Make sure you have Open Locks loaded."
   - Load Open Locks, no lockpick: "...a lockpick tool in your inventory."
   - With Lock_Pick in the bag: "Lock Picked Successfully!". The door now offers Open, and Open opens it.
   - Note whether the client showed a progress bar before the line.
2. **Locked safe.** `?objstate 1416 0x836` spawns a locked Wall Safe. Pick Lock (use 8) should turn its menu into Open.
3. **Security panel.** `?objstate 466`. Use "Disable Security Devices" without the tool, then with it. Expect "ACCESS GRANTED" or "ALARM TRIGGERED", and the panel's look changing for Disabled or Alarming.
4. **Trap.** `?trapobj 1416` spawns a trapped safe with its trap detected. `?trapobj 1416 hidden` spawns one undetected. Select it and cast Disarm Traps. Expect "Trap was successfully disarmed!", "...not disarmed successfully!" or "Trap went off...". A hidden trap always goes off. Note any trap FX.
5. **Recall** needs two clients. On one machine, one real client and a headless player in the same team. Cast Recall Team Member on the team member: the member gets "Your team member ... is attempting to teleport you", plays the phase-out effect and appears beside the caster.
6. **Find Data Node** without a locator: "A tool is required to activate this ability." With the handheld: "You begin searching for Data Nodes.", and on the next use "You stop searching...".
7. **Download Mission Map** loaded, then accept a mission. The debug log shows `[UTILITY] Download Mission Map: access flags N`, and on success the player plays the ability-upgrade effect.
