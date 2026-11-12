# Coder simulacra (pets)

Code: `hds/world/Structures/CoderPets.cs`. Tests: `tests/hds.Tests/CoderPetTests.cs`. Dev command: `?pet` (needs `MXO_DEV_COMMANDS=1`).

## What runs

| Ability | Server behaviour |
|---|---|
| Remote Proxy 1.0-5.0 (27, 28, 29, 586, 594) | 8 s cast, out of combat only. Summons a proxy simulacrum at `sim_clamp` level 1-4 / 4-7 / 8-17 / 18-34 / 35-50. Sheet is picked at random from Aikido, Karate, KungFu, Pistol, Rifle and SMG. |
| Logic Daemon 1.0-4.0 (22, 601, 611, 616) | Summons a Nuker simulacrum at level 4-7 / 8-17 / 18-34 / 35-50. |
| Patch Daemon 1.0-3.0 (602, 612, 617) | Summons a Healer simulacrum at level 12-22 / 23-43 / 44-50. It heals its owner when the owner is hurt and within 20 m. |
| Repair Simulacra 1.0-4.0 (30, 31, 587, 596), Fix Simulacra (615) | Heals the caster's own simulacrum by the stored fixed amount (20, 34, 117, 174, 83) times the caster's healing influence. A player target is still refused. |
| Fortify Simulacra 1.0 (20) | Own simulacrum only. Adds character level × 15 to its MaxHealth for 600 s (native value function 3, scale 15). Adds level ×2 to melee, ballistic, viral and thrown resistance scores. The actual hostile-NPC retaliation consumer uses the resistance curve below. Scripted Drain Code/Detonate self-damage bypasses resistance. |
| Charge Simulacra (603) | Own simulacrum only. For 600 s lowers action IS cost 20% and increases regeneration 50%. Attack and healer actions spend the actual bounded pet IS pool; insufficient IS skips the action. |
| Detonate Proxy (34) | Own simulacrum only. Deals `min(health, max/4)`, at least `max/10`, to every hostile NPC within 8 m of the simulacrum (StopProxy1Effect 1109 RadiusOfEffect 8). After 0.5 s the simulacrum takes 13000 and dies. |
| Drain Code (19) | Own simulacrum only. The simulacrum takes 8000 (and dies) and the caster gains 50 Inner Strength. |
| Daemon Boost 1.0 / 2.0 (609 / 619) | A toggle. While on it drains 5 Inner Strength every 5 s, and switches off when there isn't enough. Every 5 s it pulses 699 or 700 at the simulacrum, if the simulacrum is within 20 m (699) or 10 m (700). 699 adds character level × 0.5 to the simulacrum's damage or healing for 5 s. 700 adds level × 1.3 accuracy for 5 s to the real attack roll. Using the ability again switches it off. |

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
- **Pet resource baseline (OURS):** maximum 100 IS, starting full, regeneration 2 IS/s, attack/heal cost 10. Charge changes cost to 8 and regeneration to 3/s. No retained script/capture establishes pet sheet resource numbers. Elapsed regen is split at Charge expiry; recasting cannot retroactively buff past time.
- **Combat curves (OURS):** base hit chance 75%, +2 percentage points per level difference, +active accuracy score, bounded 5%-95%; resistance damage = ceil(raw×100/(100+score)). Fortify's four same-valued scores cover the current retaliation attack; no new NPC ability damage routing is claimed. The sourced700 modifier changes real hit/miss outcomes; its three accuracy stats share this simplified attack model. Pet base damage/healing remains approximate.
- Resource/HP mutations from AI, repairs and scripted damage serialize on the pet. NPCs already fighting a player still do not retaliate separately against a pet; P5's shared player/NPC attack clock remains separate from this policy.
- Existing observers receive the verified NPC movement death builder (NPC part2, never player Prone/player part1). New observers omit corpses under existing NPC visibility policy. No new late-observer pet corpse creation is claimed.

## Simulacra window wire evidence

Read-only client.dll 7.6005, SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`:

- `62144ed0` allocates 46 bytes and writes `81 76`. `62125940` initializes a 44-byte nested record: reserved 0, SimulacraID u8, name 32 bytes (30 usable + terminators), status 10 bytes.
- `6214d7b0` / `6214d910` print ID and name. Status printer `6214d680` labels offsets 1/3 as Health/MaxHealth u16, 5 CurrentCommand u8, 6 CurrentStance u8, 7 Level u8, 8 Buffer u16; offset 0 reserved.
- Consumer `625de800` deduplicates the ID, stores the name/status, resolves the entity by ID, then emits FourCC SVOp and calls `620f0700` to populate/open the Simulacra window. Entity arrival `625dea34` resolves IDs again, so registration may precede visibility creation.
- `8177 ID` is 3 bytes (62145020); consumer `625de270` erases that ID and sends SVCl when the last entry disappears. Sent once on death/dismiss/logout/zoning/replacement. Delayed corpse teardown cannot erase a replacement's newer ID.
- `8178 ID status 10` is 13 bytes (62145180); updates health/order/mode/level. Buffer remains 0: its gameplay meaning is not established. Window IS display is not claimed; `?pet` exposes server pet IS.

These are DLL-derived layouts, without a retail `81 76` capture. Native acceptance and window presentation remain pending Claude.

## Exact native QA (Claude only, CSR_Tester 110)

Build and run the disposable fixture from this PR's checkout (never invoke it from a Codex agent):

```powershell
$root='C:/Users/russe/Developer/mxo-wt/codex-package-p6-coder-pets'
$head=(git -C $root rev-parse HEAD)
$qa=Join-Path $env:TEMP ('mxo-native-p6-'+(Get-Date -Format yyyyMMdd-HHmmss))
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Debug
dotnet "$root/tools/NativeQa/bin/Debug/net8.0/NativeQa.dll" $root $qa $head coder-pets
```

The fixture exposes only copied CSR_Tester 110 under CSR_AbilityQA, gives the pet loadout and enables MXO_DEV_COMMANDS=1. Use a second terminal for stop; leave the first running. Do not select another character.

1. Log into CSR_AbilityQA / CSR_Tester. `?pet summon 616`: visible Nuker and automatic Simulacra window, correct name, level and HP. `?pet` reports 100/100 IS. Click Stay, Follow and Passive/Defensive/Aggressive: check corresponding server `?pet` state and window state; native orders must send `81 7a/b/c fe`. Run `?pet dummy`, select the spawned dummy, then click Attack; HP changes in window and world.
2. `?pet cast 20`: after 2 s MaxHealth rises 750 and `?pet` reports resistance 100 at level 50; dummy retaliation loses less pet HP (our resistance curve; raw attacks vary). `?pet exhaust` then `?pet`: IS begins at 0, action waits for budget and resumes after regeneration. `?pet cast 603`: after 4 s `?pet` reports cost 8; regeneration rises from 2/s to 3/s. These baseline resource amounts are OURS, not retail parity claims.
3. `?pet cast 619`: while pet within 10 m, after the pulse `?pet` reports accuracy 65; owner spends 5 IS per 5 s; actual pet attacks use the hit roll (95% cap vs same-level). Cast 619 again: accuracy reports 0 and owner drain stops. 609 instead adds damage/heal level × 0.5, with its 20 m radius. Do not infer a statistically exact hit rate from a few hits.
4. `?pet cast 34`: corpse falls using NPC death movement and death FX after 0.5 s; window closes immediately; corpse disappears after 5 s for existing observers. `?pet summon 27`, `?pet cast 19`: death/window removal, owner +50 IS; immediately resummon before old corpse teardown and verify replacement window survives. Deactivate button removes the current pet/window; summon again and zone/reconnect: no stale pet/window.
5. Close native client. In terminal 2: `New-Item -ItemType File -Path "$qa/stop"`; allow fixture normal exit. Inspect stopped.json for CSR_Tester 110 reset to Mara (23828,495,13984), full HP/IS, flag 0 and mission 986 only, and normal release of fixture lock. Record native results against full PR head. Claude owns QA/merge; no native pass is claimed by headless tests.
