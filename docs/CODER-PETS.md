# Coder simulacra (pets)

Code: `hds/world/Structures/CoderPets.cs`. Tests: `tests/hds.Tests/CoderPetTests.cs`. Dev command: `?pet` (needs `MXO_DEV_COMMANDS=1`).

## What runs

| Ability | Server behaviour |
|---|---|
| Remote Proxy 1.0-5.0 (27, 28, 29, 586, 594) | 8 s cast, out of combat only. Summons a proxy simulacrum at `sim_clamp` level 1-4 / 4-7 / 8-17 / 18-34 / 35-50. Sheet is picked at random from Aikido, Karate, KungFu, Pistol, Rifle and SMG. |
| Logic Daemon 1.0-4.0 (22, 601, 611, 616) | Summons a Nuker simulacrum at level 4-7 / 8-17 / 18-34 / 35-50. |
| Patch Daemon 1.0-3.0 (602, 612, 617) | Summons a Healer simulacrum at level 12-22 / 23-43 / 44-50. It heals its owner when the owner is hurt and within 20 m. |
| Repair Simulacra 1.0-4.0 (30, 31, 587, 596), Fix Simulacra (615) | Heals the caster's own simulacrum by the stored fixed amount (20, 34, 117, 174, 83) times the caster's healing influence. A player target is still refused. |
| Fortify Simulacra 1.0 (20) | Own simulacrum only. Adds character level × 15 to its MaxHealth for 600 s (native value function 3, scale 15). The four resistances (scale 2) are not applied, because simulacra have no resistance model yet. |
| Charge Simulacra (603) | Own simulacrum only. Its 600 s Inner Strength cost and regen buff is recorded, but nothing reads it: simulacra spend no Inner Strength here. |
| Detonate Proxy (34) | Own simulacrum only. Deals `min(health, max/4)`, at least `max/10`, to every hostile NPC within 8 m of the simulacrum (StopProxy1Effect 1109 RadiusOfEffect 8). After 0.5 s the simulacrum takes 13000 and dies. |
| Drain Code (19) | Own simulacrum only. The simulacrum takes 8000 (and dies) and the caster gains 50 Inner Strength. |
| Daemon Boost 1.0 / 2.0 (609 / 619) | A toggle. While on it drains 5 Inner Strength every 5 s, and switches off when there isn't enough. Every 5 s it pulses 699 or 700 at the simulacrum, if the simulacrum is within 20 m (699) or 10 m (700). 699 adds character level × 0.5 to the simulacrum's damage or healing for 5 s. 700's accuracy buffs have no consumer. Using the ability again switches it off. |

### Lifetime

- One simulacrum per owner. A new summon replaces the old one.
- The simulacrum leaves on:
  - a Deactivate order
  - logout (`CombatManager.RemoveClient`)
  - a world reload or zoning (`CombatHandler.AbandonCombat`)
  - the owner leaving the district, or getting more than 240 m away
- At 0 health it dies. It gets retail's NPC death update and `FX_HACKER_SIMULACRA_DEATH_BASE`, and its view is removed 5 s later.

### AI

The simulacrum is ticked every second.

- **Follow:** it walks to within 2.5 m of its owner, moving at most 5 m a tick with the wander animation (`02 0e`).
- **Attack:** it moves within 2.5 m (melee proxies) or 15 m (Nuker and gun proxies) and attacks every 3 s.
- **Defensive** (the default) and **Aggressive** simulacra take on their owner's close-combat target. An Aggressive one also engages the nearest hostile NPC within 10 m. A **Passive** one only fights when ordered.
- An NPC it fights, if that NPC isn't fighting a player, hits back every 3 s.
- Its kills go through the owner's fight, so death claims, experience, missions and loot follow the normal path.
- Players can't open close combat on a simulacrum.

## Orders: `81 7a` / `81 7b` / `81 7c`

The layouts are client.dll's own.

- **`81 7a`** (ctor 620ef730, 4 bytes): `ID u8, Command u8`.
- **`81 7b`** (620ef832, 8 bytes): `ID u8, Command u8, TargetObject u32`. The server reads the low u16 as the view.
- **`81 7c`** (620ef952, 5 bytes): `ID u8, Command u8, Value u8`.

The Simulacra window (`viewsimulacra.cpp`) maps its buttons to FourCC messages (dispatcher 620f0070). It sends ID `0xfe`, meaning the current simulacrum:

| Button | FourCC | RPC |
|---|---|---|
| Stay | `SSty` | `81 7a fe 00` |
| Deactivate | `SDea` | `81 7a fe 01` |
| Attack | `SAtk` | `81 7b fe 02 <target>` |
| Follow | `SFlw` | `81 7a fe 03` |
| Defensive | `SDef` | `81 7c fe 05 00` |
| Aggressive | `SAgr` | `81 7c fe 05 01` |
| Passive | `SPas` | `81 7c fe 05 02` |

The `/ordersimulacra` chat handler (62257c40) uses the same command numbers 0-5, with a SimulacraID instead of `0xfe`. Command 4 is never sent.

## Spawn (`NPC_BASE_SIMULACRA`, GO 40376)

Four retail spawns decode cleanly with `AttributeClass3828`:

- PacketsMaraCThenTimeDOut, lines 418 and 78483 (Nuker L7)
- `second_pc_others_Syntax`, lines 515 and 4213 (Nuker L50, Aikido L50)
- luizoe old sniffer, line 107027 (SMG L50)

### A fresh summon

A freshly summoned simulacrum, as its owner saw it, carries exactly these 18 attributes:

- CharacterName ("Nuker Simulacrum", "Aikido Simulacrum")
- TitleAbility `00 10 00 00`
- CombatantMode `22`
- EffectCounter 1
- MasterCharacterID = owner
- Description
- EffectID `0c 06 00 28` (`FX_HACKER_SIMULACRA_SUMMON_BASE`)
- SimulacraID: a per-owner counter, 1 then 2 for the same owner
- Position
- OrganizationID 3
- NPCRank 2
- Level
- Health, MaxHealth
- CurrentState `0d 08 00 00`
- EquippedItemID
- OwnerCharacterID = owner
- StealthAwareness

There is no IsEnemy, IsFriendly or RSIDescription.

`CoderPetTests.CreationMatchesRetailFreshSummon` checks that our creation reproduces line 78483 byte for byte.

Description, like every id here, goes out little-endian: `0x58000513` is the bytes `13 05 00 58`. The first native test sent `58 00 05 13`, which names no template, and the client rendered no simulacrum. `DescriptionGoesOutInRetailByteOrder` and the headless scenario now check the byte order.

### Values that come from retail

| What | Value |
|---|---|
| MaxHealth | 337 at level 7; 1950 at level 50. One SMG proxy had 2744, probably Fortified. |
| Description | Nuker L7: `0x580004d2` or `0x580004d4`. Nuker L50: `0x58000513`. Aikido and SMG L50: `0xb40001b9`. |
| EquippedItemID | Nuker `0x0a06`. Aikido `0xb917`. SMG `0xba5b`. |

## Evidence and choices

From evidence:

- Level clamps and sheets: `research/client-scripts/ability/hacker/simulacra.py`.
- The Detonate formula: `tempmods.DetonateProxyProc` / `DetonateProxyEnd`.
- The own-simulacrum test: `Decoy1_Test`, `StopProxy1_Test`, `AcousticDecoy2_Test`.
- Costs, cast times, recasts, radii, stored heals and value functions: `data/ability_catalog.json`.
- The Daemon Boost drain and pulse: `ISDrainWithAbilityDeactivation`, `ApplyAuraEffect`.
- The spawn layout and its values: retail dumps.
- Order layouts and command numbers: client.dll.

Chosen, not retail:

- **Health between known levels:** a straight line through 337 at level 7 and 1950 at level 50.
- **Descriptions and weapons with no capture:**
  - Healer and Nuker above level 17 use the L50 Nuker Description.
  - Karate and KungFu use the Aikido weapon.
  - Pistol and Rifle use the SMG weapon.
  - Healer and Proxy names follow the "`<Sheet>` Simulacrum" pattern.
- **Damage:** a simulacrum hits with the server's NPC round model (`CalculateMobDamageWithTactics`) at its level. Patch Daemon heals by the same amount. No capture shows a simulacrum's damage or heal.
- **Timing and movement:**
  - Attack period: 3 s. The client has `Simulacra_AttackActionPeriod`, but its value is unknown.
  - Movement: 5 m steps. Follow distance: 2.5 m. Leash: 60 m.
- **Remote Proxy 2.0:** its script calls `destroyCurrentSimulacrum()` right after creating the simulacrum. We keep the simulacrum, because reproducing that would make the ability useless.
- **Drain Code's DecoyDuration 60 s** has no known consumer.
- **The 81 76 record with SimulacraID** (client print 6214d7b0, a submessage of `81 76`) isn't sent, so the client's Simulacra window may not open by itself. Verify this natively.

## Native QA (one client, `MXO_DEV_COMMANDS=1`)

1. Log in any character and stand in open street. `?pet abilities` lists the ids.
2. Summon:
   - `?pet cast 616` (Logic Daemon 4.0) runs the real cast path: cast bar, 150 IS, then the 8 s cast.
   - `?pet summon 594` skips the cast.
   - Expect a "Nuker Simulacrum" (or "<Sheet> Simulacrum") about 1.5 m to the side, with its name and health bar. Check the summon FX, and whether the Simulacra window appears.
3. Walk about 20 m away. The simulacrum follows (in 5 m steps).
4. `?pet dummy` spawns "Simulacra Dummy" 8 m away, at your level, passive.
5. `?pet attack`, or the window's Attack button with the dummy selected. The simulacrum walks to it and hits every 3 s; the dummy's health drops and it hits back. Let the dummy die: you get the kill's experience.
6. `?pet stay`, then walk away: it stays. `?pet follow`: it comes back. `?pet passive` / `?pet aggressive`, or the window buttons; `/ordersimulacra` should behave the same way.
7. Simulacrum abilities (they need your simulacrum within 16 m):
   - `?pet cast 31` (Repair Simulacra 2.0): its health rises.
   - `?pet cast 20` (Fortify): MaxHealth rises by level × 15.
   - `?pet cast 609` (Daemon Boost): IS drops 5 every 5 s. Run `?pet cast 609` again to turn it off.
8. `?pet dummy`, then `?pet cast 34` (Detonate Proxy) next to it: the dummy takes the blast, and the simulacrum dies 0.5 s later with the death FX. Check that the corpse disappears after 5 s.
9. Summon again, then `?pet cast 19` (Drain Code): the simulacrum dies and IS rises by 50.
10. Summon again:
    - `?pet dismiss` (or the window's Deactivate): it vanishes with the dismiss FX.
    - Summon again and hardline-teleport, or log out: the simulacrum is gone afterwards.
