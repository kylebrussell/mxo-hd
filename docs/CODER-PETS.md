# Coder simulacra (pets)

Code: `hds/world/Structures/CoderPets.cs`. Tests: `tests/hds.Tests/CoderPetTests.cs`. Dev command: `?pet` (needs `MXO_DEV_COMMANDS=1`).

## What runs

| Ability | Server behaviour |
|---|---|
| Remote Proxy 1.0-5.0 (27, 28, 29, 586, 594) | 8 s cast, out of combat only. Summons a proxy simulacrum at `sim_clamp` level 1-4 / 4-7 / 8-17 / 18-34 / 35-50. Sheet is picked at random from Aikido, Karate, KungFu, Pistol, Rifle and SMG. |
| Logic Daemon 1.0-4.0 (22, 601, 611, 616) | Summons a Nuker simulacrum at level 4-7 / 8-17 / 18-34 / 35-50. |
| Patch Daemon 1.0-3.0 (602, 612, 617) | Summons a Healer simulacrum at level 12-22 / 23-43 / 44-50. It heals its owner when the owner is hurt and within 20 m. |
| Repair Simulacra 1.0-4.0 (30, 31, 587, 596), Fix Simulacra (615) | Heals the caster's own simulacrum by the stored fixed amount (20, 34, 117, 174, 83) times the caster's healing influence. A player target is still refused. |
| Fortify Simulacra 1.0 (20) | Own simulacrum only. Adds character level × 15 to its MaxHealth for 600 s (native value function 3, scale 15). Adds level ×2 to melee, ballistic, viral and thrown resistance scores. The hostile NPC's melee retaliation consumes the active rating through the shipped armor curve below. Scripted Drain Code and Detonate retain their fixed pet damage. |
| Power Boost (25) | Own pet out of combat. Costs 50 IS with a 4 s cast, 10 m range and 8 s reuse. Snapshots caster-level × 0.5 melee159 and ballistic171 damage influence for 300 s. Proxy melee/firearm hits use those ratings. The same timed buff supplies +40 Force Combat 519 to the owned melee Proxy's entry against the captured NPC shield. |
| Charge Simulacra (603) | Own simulacrum only. For 600 s lowers action IS cost 20% and increases regeneration 50%. Attack and healer actions spend the actual bounded pet IS pool; insufficient IS skips the action. |
| Detonate Proxy (34) | Own simulacrum only. StopProxy1Effect (1109) uses raw `min(health, max/4)`, at least `max/10`, against hostile NPCs within 8 m of the simulacrum. The ordinary viral resistance path uses the pet's level. The owner receives ordinary kill credit. After 0.5 s the same simulacrum takes the existing scripted 13000 damage and dies. |
| Drain Code (19) | Own simulacrum only. The simulacrum takes 8000 (and dies) and the caster gains 50 Inner Strength. |
| Daemon Boost 1.0 (609) | The toggle spends 10 IS to activate and applies 699 immediately to live friendly simulacra within 20 m. Each five-second update costs 5 IS and refreshes the pulse. An upkeep shortfall or unload switches off the parent. The last pulse keeps its own five-second lifetime. It snapshots caster level × 0.5 for Viral Damage 172 and Healing 174. Nukers apply the viral percentage to their hits and Patch Daemons apply the healing percentage to their heals. |
| Daemon Boost 2.0 (619) | The current implementation refreshes 700 on the owner's simulacrum within 10 m. Its five-second snapshot adds caster level × 1.3 accuracy to the attack roll. Upkeep costs 5 IS every five seconds. |

### Lifetime

Detonate Proxy's GOB modifier attaches its callbacks to placeholder 710 with a zero-valued payload. `DetonateProxyProc` reads the pet's HP, subtracts the existing Viral Damage and the child's stored one from the temporary adjustment, then reapplies 1109 on the pet. The stored selector centers the radius on that pet. Stock casts 34, and the server refuses an ordinary request for 1109. The disposable mode `ability-parity-coder-detonate-proxy` checks the ordinary cast and bounded NPC area with neutral, viral-resistant, outside and lethal recipients. The starter pet mode also checks an eligible hostile player in the pet-centered radius and excludes allied and out-of-range players. Player damage uses the pet level for viral resistance, as for NPCs. Exact retail resistance scaling and hit-contest behavior for the reapplication remain open.

- One simulacrum per owner. A new summon replaces the old one. Unloading the ability that summoned the current pet removes that pet and its Simulacra window. Reloading the ability permits a new cast.
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
- **Defensive** (the default) and **Aggressive** simulacra take on their owner's close-combat target. An Aggressive one also engages the nearest hostile NPC within 10 m. A **Passive** one only fights when ordered. Switching to Passive clears an ongoing automatic assist and retains a direct Attack order. An expired Attack target returns the window command to Follow.
- An NPC it fights, if that NPC isn't fighting a player, hits back every 3 s.
- Its kills go through the owner's fight, so death claims, experience, missions and loot follow the normal path.
- Players can't open close combat on a simulacrum.

## Orders: `81 7a` / `81 7b` / `81 7c`

The layouts are client.dll's own.

- **`81 7a`** (ctor 620ef730, 4 bytes): `ID u8, Command u8`.
- **`81 7b`** (620ef832, 8 bytes): `ID u8, Command u8, view u16, spawn u16`. The PR772 stock capture selected `81 51 04 00 02 00`, then sent `81 7b fe 02 04 00 02 00`. The order requires a live, visible hostile NPC within the owner leash and a matching supplied spawn. Older headless calls may use a zero-extended view. The `?pet attack` developer shortcut can still choose an NPC automatically.
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

The stock `/ordersimulacra` handler reads the recipient first and the localized command name second. In English, `/ordersimulacra All Deactivate` uses `81 7a fe 01`. `/ordersimulacra 1 Deactivate` uses `81 7a 01 01` if SimulacraID 1 is registered. The client compares the first argument with the `All` string (ID `01000039`) at 62257bd6, and the second with the command-name table at 62257c00. `Deactivate` is string ID `6a000002`. Use the localized command name as the second argument.

PR440 stock capture `d148595f0aa3` lines 1652 and 1940 records command 1 from the control displaying the Stay tooltip. The DLL dispatches `SDea` through 620f00c1 with command 1 and `SSty` through 620f0204 with command 0. Both paths use 620efa60 to write the command byte. The native range fixture uses `?pet stay` to park the pet before testing from its far stand.

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
- OrganizationID 3 in these MaraC and Syntax samples. Luizoe has 1. See [PvP presentation](CODER-PET-PVP-PRESENTATION.md).
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
- **Remote Proxy 2.0:** [the shipped Subject callback](../research/client-scripts/ability/hacker/simulacra.py#L198) creates a proxy, reports the cast, then destroys the current simulacrum. The server prepares the new proxy off-world, retires the exact previous summon if present, and publishes the replacement. The original engine binding's publication timing is unresolved. The stock replacement fixture checks both an empty slot and an existing proxy, plus the separate Deactivate callback.
- **Drain Code's DecoyDuration 60 s** has no known consumer.
- **Pet resource baseline (OURS):** maximum 100 IS, starting full, regeneration 2 IS/s, attack/heal cost 10. Charge changes cost to 8 and regeneration to 3/s. No retained script/capture establishes pet sheet resource numbers. Elapsed regen is split at Charge expiry; recasting cannot retroactively buff past time.
- **Combat curves:** this server uses a base hit chance of 75%, +2 percentage points per level difference, and any active accuracy score, bounded 5%–95%. The sourced 700 modifier changes the attack roll. The same model handles its three accuracy stats. Pet base damage and healing remain approximate.
- **Fortify retaliation:** the shipped interlock selects the defender's general toughness plus melee or ranged toughness and applies `GetDamageReductionValue`. Pet retaliation currently sends a melee hit. It uses the pet's active Fortify rating against the two actors' levels. The server already weights the NPC damage by level, so it uses `raw * (1 - reductionWithFortify) / (1 - reductionWithoutFortify)`, rounded down with a one-point minimum on landed hits. The ratio preserves the existing unbuffed hit and applies the shipped armor difference once. The retained scripts leave the original pet-specific caller open. [Typed pawn resistance](../research/client-scripts/interlock/combat_resolution.py#L95) [Shipped armor curve](../research/client-scripts/combat_calculations.py#L198)
- **Fortify witness:** `ability-parity-starter-fortify-retaliation` compares ordinary level-4 NPC retaliation against the same level-4 Proxy before, during, and after Fortify. The armor cap leaves 201/300 damage active against the 289/300 unbuffed coefficient. The existing disposable expiry command advances Fortify, and the following ordinary tick removes the resistance and returns the maximum from 494 to 224. The stock-frame replay records raw/final hits 11/11, 8/5 and 9/9, with matching pet-window HP 224→213→208→199. The stock plan compares each new rolled hit to the same rule.
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

The combined [Coder pets and PvP fixture](NATIVEQA-CODER-PETS-PVP.md) uses ordinary skill activation and checks both summon families alongside flagged player targeting.

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
3. `?pet cast 619`: while pet within 10 m, after the pulse `?pet` reports accuracy 64; owner spends 5 IS per 5 s; actual pet attacks use the hit roll (95% cap vs same-level). Cast 619 again: accuracy reports 0 and owner drain stops. 609 instead adds Viral Damage/Healing influence level ×0.5 for 5 s within 20 m; only Nuker hits and healer heals use it, with percentage scaling. Do not infer a statistically exact hit rate from a few hits.
4. `?pet cast 34`: corpse falls using NPC death movement and death FX after 0.5 s; window closes immediately; corpse disappears after 5 s for existing observers. `?pet summon 27`, `?pet cast 19`: death/window removal, owner +50 IS; immediately resummon before old corpse teardown and verify replacement window survives. Deactivate button removes the current pet/window; summon again and zone/reconnect: no stale pet/window.
5. Close native client. In terminal 2: `New-Item -ItemType File -Path "$qa/stop"`; allow fixture normal exit. Inspect stopped.json for CSR_Tester 110 reset to Mara (23828,495,13984), full HP/IS, flag 0 and mission 986 only, and normal release of fixture lock. Record native results against full PR head. Claude owns QA/merge; no native pass is claimed by headless tests.

## Power Boost 25 / typed 699 continuation

The shipped `PowerBoost1Ability` (25) stores own-simulacrum target 11, noncombat-only use, 50 IS, a 4 s cast, 8 s recast, 10 m range, and 300 s of melee 159 / ballistic 171 influence at caster level ×0.5. `AbilityValues.TryValue` applies the native value function and truncation. The level is captured when casting starts. Completion requires the original pet, owner life/world generations, a live connected owner/pet, and 10 m reach. Replacing, killing or zoning the original pet during the cast cannot transfer the buff to a replacement. Explicit foreign-player, foreign-pet and invalid views are refused; no selection/self remains the existing own-pet shorthand. Recasting replaces rather than stacks 25.

159/171 are percentage damage influence, following the existing combat pipeline's `1 + influence/100`. Only reconstructed Proxy melee/firearm attacks consume them. A level 50 caster supplies 25 influence: an 80 baseline hit becomes 100. Nuker viral attacks and healer healing remain 80. Base pet sheets, damage types and damage formulas remain server approximations.

699 now uses its actual catalog targets, Viral Damage 172 and Healing 174, both caster level ×0.5 for 5 s. At level 50, a Nuker 80 baseline viral hit or Patch Daemon 80 baseline heal becomes 100; a Proxy hit stays 80 under 699 alone. This replaces the prototype's flat generic bonus. The existing damage/healing influence convention supplies the percentage conversion. The shared native value evaluator also corrects 700's level-50 accuracy from the prototype's 65 to **64**: its binary32 scale 1.3 multiplied by 50 is 64.999997..., truncated toward zero.

25's 300 s timer is independent of the 699/700 aura timer. Switching or expiring one cannot erase the other. The existing single selected Daemon Boost toggle remains. 25 and 699 share stored stacking type 38; retaining independent typed channels is a server policy, not a proven retail stacking-group interpretation.

Power Boost stores direct Force Combat 519 = 40 for 300 s. An explicit stock Attack order from an owned melee Proxy now reaches the captured Crossbones Tar's shield contest at melee range. The server uses neutral NPC inputs with the existing Force Combat entry comparison. An unboosted score of 100 fails against 110 and consumes the target's shield without damage to either actor. The boosted score of 140 admits a single pet and target pair while preserving the remaining shield. That pair exchanges the existing pet hit and shared NPC retaliation, visible on both actors' ordinary views. Expiry applies to new entry attempts. Stay, Follow, retarget, range loss, death and dismissal release the pair. Player combatants take retaliation priority. If the pair stays in range, the pet resumes with the same NPC action clock after the last player leaves. [Existing entry comparison](../hds/world/Structures/CombatInsightEngagement.cs#L17) · [Captured NPC shield](STEALTH-SHIELDS.md#crossbones-tar-npc-shield)

The shipped NPC templates include a passive Evade Interlock rating (772 → stat 520) and an Evade State Flipper (874). The flipper toggles the native interlock state. Player Evade Combat creates a shield through a separate hook. Which ordinary NPCs use the flipper remains unresolved in the placement data. Pet Attack resolves damage directly against NPCs, and pet-owned interlock remains unimplemented. [NPC state flipper](../research/client-scripts/tempmods.py#L1736) · [Player shield creation](../research/client-scripts/tempmods.py#L552)

The NPC templates separate direct Force Combat (774 → 519), its bonus (1331 → 1048), and the entry permission (891 sets and restores `AllowedToForceCombat`). The shipped NPC console can order an AI to attack a different AI. The stock Simulacra Attack order supplies the target handle but carries no combat tactic. The server keeps its existing ranged, automatic and player-assistance paths. The archive leaves pet Evade activation, AI tactic choice and two-Mob choreography unresolved. [NPC templates](../data/ability_catalog.json) · [Entry permission callback](../research/client-scripts/tempmods.py#L411) · [NPC-to-NPC command](../research/client-scripts/npccommands.py#L16)

### Native follow-up checklist (Claude only)

Use the exact reviewed follow-up head. Commands below are instructions for Claude; no Codex agent runs the fixture. `--build-info` is a read-only exit before lock/database setup.

```powershell
$root='C:/Users/russe/Developer/mxo-wt/codex-package-p6-power-boost'
$head=(git -C $root rev-parse HEAD)
$qa=Join-Path $env:TEMP ('mxo-native-p6-power-'+(Get-Date -Format yyyyMMdd-HHmmss))
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Release
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" --build-info $root
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" $root $qa $head coder-pets
```

The mode exposes copied CSR_Tester 110 only under CSR_AbilityQA. This continuation adds 25 to its loadout and gives the disposable fixture 1000/1000 IS for repeated checks. Keep the fixture terminal running. Use only CSR_Tester 110 in the real client.

1. Before fighting, `?pet summon 594`, then `?pet cast 25`; immediately summon again during the 4 s cast. After completion, `?pet` must report `power melee/ballistic 0/0` on the replacement. Wait the 8 s recast, order Stay and start 25 within 10 m, then walk outside 10 m before it lands: no buff. Starting beyond 10 m refuses before cost. Selecting a helper player or its pet must refuse. The foreign-pet and owner lifecycle gates are also covered headlessly.
2. Return within 10 m and cast 25 on a fresh Proxy. After 4 s, `?pet` reports `power melee/ballistic 25/25`. Cast 609: `aura viral/heal 25/25` may coexist. Spawn a dummy, select it, and click the Simulacra window's Attack button. Server lines `[PET] typed influence Proxy base X final Y` must show `Y=truncate(X*1.25)`; 699 adds no Proxy damage. Toggle 609 off: 25 remains. A pet fighting alone does not put its owner in combat.
3. Turn 609 on, summon a Nuker with `?pet summon 616`, and cast 25 before the owner enters combat. 25 may be present but supplies no viral influence. After the next 609 pulse, a selected dummy takes 25% increased viral damage, visible in `[PET] typed influence Nuker ...`. Summon a Patch Daemon with `?pet summon 617`, use `?health 100`, and observe a 609 pulse followed by `[PET] typed healing base X final Y`, again `Y=truncate(X*1.25)`, plus actual owner HP recovery. 25 adds no healing. For 619, `?pet` reports accuracy 64 at level 50 and its independent resource drain still works.
4. Finally start an ordinary owner fight against a dummy, then attempt 25: noncombat-only refusal. A new pet after dismiss, death or reconnect has no inherited 25. Unit tests cover 300 s expiry and percentage boundaries, so native QA need not wait five minutes. Close the real client. In another terminal: `New-Item -ItemType File -Path "$qa/stop"`. Let the fixture exit normally; inspect stopped.json for CSR_Tester 110 at Mara (23828,495,13984), full HP/IS, flag 0, mission 986 only, and normal lock release. Record only observed native results against the exact follow-up SHA.

## AB9 stock pet-buff follow-up

The Coder sweep at `a4307878` used stock client 7.6005. In `%TEMP%/mxo-headless/b0d0eb7ff285/PacketLog.txt`, the Client-to-Server protocol04 records are 43-byte `80 b9` activations. The first summon is line 2731. Fortify, Power Boost and Charge with zero target are at 3109, 3262 and 3369. Explicit Fortify at 5530 carries view `0e 00` and spawn `0d 00`. The headless replay reuses these bytes and substitutes the current observer view and spawn only for the explicit cast.

- Fortify landed in the original sweep. Window record `81 78 01 00 e0 00 ce 03 03 00 04 00 00` at 3221 gives current HP 224 and MaxHP 974, a level-50 increase of 750. At 5632 the same MaxHP and Fortify recast confirm the explicit recast.
- Power Boost starts at 3274. Charge starts another four-second cast at 3381, and Power Boost never emits its own recast. The server's normal-cast busy deadline could expire before its delayed landing callback was due. A new request accepted in that interval replaced the earlier cast generation. An expired normal cast now settles once before the next normal request takes ownership. The deterministic cast test exercises that boundary.
- Charge started its 30-second recast at 3481. The capture does not expose pet IS or independently measure Charge's effects. Catalog row 603 is named `AcousticDecoy1Ability` and stores target class 11, modifier 117 (cost reduction 20) and 114 (regen increase 50) for 600 seconds. The game displays Charge Simulacra, which the server diagnostic now uses. The pet's IS pool and base cost and regen remain the server approximations listed above.
- The shipped `AcousticDecoy1_GetRange` callback returns 50 m times the selection-range debuff factor. Charge uses that range for admission and again when it lands. The server interprets the positive Selection Range (214) modifier as a percent decrease, following `MessageCloud_DirectObject`. Fortify, Repair and the other pet casts still use their own template ranges.

The selected view must be the current owned pet. Full stock requests also validate its spawn. Zero-target casts use the caster's own pet. The cast stays bound to that original pet through completion. The server logs the applied Fortify, Power Boost and Charge values. Native presentation for the scoped Power Boost entry is pending.
