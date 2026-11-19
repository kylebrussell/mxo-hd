# Native ability sweeps

NativeQa discovers six fixed level-50 profiles. Each prints eight numbered actions with the target, HP change or source input, state, duration and IS cost. Use the printed list from the same build as the native session. These profiles cover Awakened, Hacker, Coder, Operative (including Spy), Martial Artist and Gunman. Special weapons, GM actions and shared or unassigned audit entries have their own admission paths and no sweep.

| Mode | Slots 1 through 8 | Native emphasis |
|---|---|---|
| ability-sweep-awakened | 1, 5, 6, 10, 184, 11, 12, 198 | Resistance, toggle transitions, jump clock, Self Defense special |
| ability-sweep-hacker | 38, 57, 77, 97, 314, 333, 383, 72 | Caster- and target-centered areas, viral contest, friendly heal, timed combat buffs |
| ability-sweep-coder | 594, 20, 25, 603, 589, 616, 34, 33 | Proxy and Logic Daemon, player-target refusal, and out-of-combat Detonate and Static Blast |
| ability-sweep-operative | 135, 123, 149, 146, 341, 647, 281, 282 | Damage influence and detection, out-of-combat openers, taunt, interlock throws |
| ability-sweep-martial-artist | 132, 267, 133, 260, 262, 428, 430, 412 | Karate, Kung-Fu and Aikido special choreography and landed effects |
| ability-sweep-gunman | 129, 126, 147, 254, 158, 176, 243, 255 | Equip each matching weapon and verify the style before testing special choreography and controls |

## Native pass

1. Set `MXO_NATIVE_QA_CUI_TEMPLATE` to a separate stock-written `cui.cfg` copy from disposable CSR. Run the [NativeQa launcher](../tools/NativeQa/README.md) with the default loadout. On disposable CSR_Tester (110), open hotbar page 2 and confirm slots 1–8. The fixture loads all transitive discipline, prerequisite and required-style entries after the selected actions.
2. Use the printed targets and check the selected name in the HUD. CSR starts at (24800, 495, -1498), with QAFriend 2 m north. Coder, Martial Artist and Gunman have one isolated 30,000 HP **Melee Dummy** 3.01 m west. Wait for each queued move to resolve before pressing the next special. The other modes have WinterFrost at that location, Trainman 3 m north of WinterFrost and Blackwoods 6 m south of WinterFrost. Flagged QAEnemy starts 2 m east in Hacker and 8 m east in Coder and Operative. Cast Poison Knife and Sever Artery out of combat before Taunt and before the interlock throws. Check shared reuse and state immunity before repeating.
3. Save one result per slot with PASS, FAIL or BLOCKED, target and active style, HP/IS before and after, state, measured duration and real outgoing activation bytes. Retain the full framed client world RPC, including the live target/spawn and any opaque bytes, plus matching replies and UI observations. Include clean hit and deflect samples when available. Record randomized control procs separately. Compare only established fields. A stored input or captured base value does not by itself establish final scaled damage, healing or duration for the fixture's actor and recipient.

### Hacker retest

- QAFriend enters at 500/5000 HP. The disposable Hacker fixture holds natural HP regeneration through the existing guarded QA setting so the wound survives hotbar setup. Target QAFriend and cast Repair RSI 1.0 normally. Record HP immediately before and after the heal. Other sweep profiles keep natural regeneration.
- Cast UI Lag from the documented start with QAEnemy 2 m away. Its shipped 3 m radius is centered on the caster, regardless of selection. Capture a hit or deflect on QAEnemy.
- For Stun 2.0 select WinterFrost. The stock tooltip and shipped selector describe a 4 m area around that selected enemy. Check that Trainman is eligible and Blackwoods is outside. Record a deflect separately from an out-of-radius target. See [debuff radius evidence](ABILITY-DEBUFF-RADIUS.md).

### Martial Artist retest

Use the Melee Dummy, activate Kung-Fu and choose a non-Block tactic. With at least 35 IS and no active reuse timer, press Suicidal Butterfly once just after a round and wait through two ordinary round intervals. Retain the outgoing 428 record, queued `47`, resolved `45` and combat result. A missed 428 may play a default blocked animation. A later special can replace a queued action. Check Piston Kicks and Tomo Nage one at a time after the result and applicable reuse window. Piston Kicks requires a Staggered target in Kung-Fu. Land Power rounds until the selected dummy is Staggered, then activate 262. Tomo Nage requires an Off Balance target in Aikido. Land Grab rounds until the selected dummy is Off Balance, then activate 412. For controlled retries in an active interlock, select the dummy and use `?combatdepth stagger 30` or `?combatdepth offbalance 30`. Observe the slot before the state, during it, and after it expires with reuse clear. See [martial hotbar conditions](ABILITY-MARTIAL-HOTBAR-CONDITIONS.md).

### Gunman retest

The revolver starts equipped. Use `?combatdepth fixture-weapon rifle`, `smg`, or `handgun` to change the gun and select the corresponding style. In a fresh interlock, test Disarming Shot and Pistol Aerial with the handgun and Covering Fire with the single SMG. The fixture sends the ordinary inventory and equipped-item updates for stock hand slot 105. Slot 107 with Free Fire kind 0 is the empty selection.

With a rifle equipped, cast Immobilizing Shot (254) and Disabling Shot (255) before combat. Both use a two-second cast with their own casting scripts (89 and 95). Immobilizing Shot applies Rooted and Force Combat −15 for eight seconds, subject to its damage-break chance. Disabling Shot has a stored 50% chance to apply Powerless for four seconds. Check that both slots grey out in an active interlock. Their shipped `CastWhenUsed=1` and absent `HasSuccessInterlockMove` match the stock result. [Native PR326 result](https://github.com/kylebrussell/mxo-hd/pull/326#issuecomment-7002053269)

### Coder retest

1. Out of combat, summon a pet and set Passive and Follow. Select flagged same-organization QAFriend and click Attack in the Simulacra window. Record either stock suppression or the received player-target request and server refusal. Confirm Melee Dummy receives no redirected pet attack.
2. With the pet near Melee Dummy, cast Detonate Proxy out of combat and wait for the blast. Use `?combatdepth fixture-disengage` and wait for the HUD to clear. Cast Static Blast on Melee Dummy while out of combat.
3. If repeating the already exercised orders, resummon the pet and run those after the out-of-combat checks. In Defensive verify assist. Use Passive to stop assist, then explicitly order Attack on Melee Dummy. Use the fixture disengage command between summons if interlock is still active. See the [original Coder pet fixture](NATIVEQA-CODER-PETS-PVP.md).

The native Actions names and internal records can differ. Logic Barrage 1.0 (57) is `LogicBlast1`, Charge Simulacra (603) uses `AcousticDecoy1Ability`, Detonate Proxy (34) uses `StopProxy1Ability`, and Martial Arts Prowess (135) uses `MartialArtsAbility`.

## Source and limits

The seed replaces only page 2 slots 1–8 from the supplied template. It preserves all other shortcut records, selection data and the remaining UI sections. The stock layout comes from 7.6005 `client.dll` shortcut reader `0x62261b20` and writer `0x62260d50`. The stock launcher dispatch at `0x43ffc6` copies Margin LoadCharacterReply type `0c` to the character UI buffer in 1,000-byte chunks. The chunk ordinal occupies reply offset `0x0b`, so the UI chunks precede the ordinary replies. Stock gives the received profile priority and may save it locally. The server only supplies it to the copied NativeQa account and CSR in the owned test database.

The source for each action ID, stored cost, base, target and modifier input is the shipped [ability catalog](../data/ability_catalog.json) indexed by [the parity audit](ABILITY-PARITY.csv). Captured costs, durations and values are overridden by the existing [cost table](../hds/world/Structures/AbilityCosts.cs), [buff table](../hds/world/Structures/AbilityBuffs.cs) and [target effect table](../hds/world/Structures/AbilityTargets.cs). For native comparison see [combat styles](COMBAT-STYLES.md), [Area Disruption](ABILITY-AREA-DISRUPTION.md), [passive consumers and Sixth Sense](ABILITY-PASSIVE-CONSUMERS.md), [Coder pets](CODER-PETS.md), [Proxy Technician](ABILITY-PROXY-TECHNICIAN.md), [conditional throws](ABILITY-CONDITIONAL-THROW.md) and [Sever Artery](ABILITY-SEVER-ARTERY-STATES.md). The Gunman bag weapons come from the shipped [weapon table](../data/weapons.csv).

Bootstrap uses the existing headless authentication, the full stock 190-byte Margin `0f` load request from the PR266 capture and the retail-captured world-entry records `05` and `0c 01` described in [Client actions](CLIENT-ACTIONS.md#login-and-world-entry). It checks the ordinary loaded-abilities Margin block of the copied helper loadout, corresponding disposable CSR rows and live target presentation. Its readiness report `ability-sweep.json` preserves those observations. The isolated `--bootstrap-checks` run also verifies that CSR receives the requested abilities and their loaded ancestors in Margin, checks the UI response byte-for-byte when the template is configured, and checks weapon swaps and the headless Coder actions. The Coder replay uses the captured 43-byte stock activation records, with live target handles substituted where needed. See the [ability-use wire decode](ABILITY-USE-RECORD.md).

Readiness carries no ability-family pass and changes no audit status. See the [first native session](https://github.com/kylebrussell/mxo-hd/pull/312#issuecomment-6995363576) and [second native session](https://github.com/kylebrussell/mxo-hd/pull/312#issuecomment-6995670186) for the original tested head. Those observations apply to their individual sampled behaviors.
