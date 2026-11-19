# Native ability sweeps

NativeQa discovers six fixed level-50 profiles. Each prints eight numbered actions with the target, HP change or source input, state, duration and IS cost. Use the printed list from the same build as the native session. These profiles cover Awakened, Hacker, Coder, Operative (including Spy), Martial Artist and Gunman. Special weapons, GM actions and shared or unassigned audit entries have their own admission paths and no sweep.

| Mode | Slots 1 through 8 | Native emphasis |
|---|---|---|
| ability-sweep-awakened | 1, 5, 6, 10, 184, 11, 12, 198 | Resistance, toggle transitions, jump clock, Self Defense special |
| ability-sweep-hacker | 38, 57, 77, 97, 314, 333, 383, 72 | Caster- and target-centered areas, viral contest, friendly heal, timed combat buffs |
| ability-sweep-coder | 27, 20, 25, 603, 589, 22, 33, 34 | Proxy and Logic Daemon window/follow/assist, pet stats, aura, replacement and detonation |
| ability-sweep-operative | 135, 123, 149, 146, 341, 647, 281, 282 | Damage influence and detection, out-of-combat openers, taunt, interlock throws |
| ability-sweep-martial-artist | 132, 267, 133, 260, 262, 428, 430, 412 | Karate, Kung-Fu and Aikido special choreography and landed effects |
| ability-sweep-gunman | 129, 126, 147, 254, 158, 176, 243, 255 | Equip each matching weapon and verify the style before testing special choreography and controls |

## Native pass

1. Run the regular [NativeQa launcher](../tools/NativeQa/README.md) for the selected mode and wait for Ready. Use the default mode loadout and only the disposable CSR_Tester (110). Use a fresh hotbar page. Drag the listed visible Actions into slots 1–8 in order. Where placement is unavailable or an action is missing, record the slot and its UI failure. The tutorial's [client hotbar notification](../research/missions/tutorial-wire-spec.md) documents the result of a UI drag. A verified server-to-client hotbar seed route remains open.
2. Use the printed targets and check the selected name in the HUD. CSR starts at (24800, 495, -1498), with QAFriend 2 m north. Martial Artist and Gunman have one isolated 30,000 HP **Melee Dummy** 3.01 m west. Wait for each queued move to resolve before pressing the next special. The other modes have WinterFrost at that location, Trainman 3 m north of WinterFrost and Blackwoods 6 m south of WinterFrost. Flagged QAEnemy starts 2 m east in Hacker and 8 m east in Operative. Cast Poison Knife and Sever Artery out of combat before Taunt and before the interlock throws. Check shared reuse and state immunity before repeating.
3. Save one result per slot with PASS, FAIL or BLOCKED, target and active style, HP/IS before and after, state, measured duration and real outgoing activation bytes. Retain the full framed client world RPC, including the live target/spawn and any opaque bytes, plus matching replies and UI observations. Include clean hit and deflect samples when available. Record randomized control procs separately. Compare only established fields. A stored input or captured base value does not by itself establish final scaled damage, healing or duration for the fixture's actor and recipient.

### Hacker retest

- QAFriend enters at 500/5000 HP. The disposable Hacker fixture holds natural HP regeneration through the existing guarded QA setting so the wound survives hotbar setup. Target QAFriend and cast Repair RSI 1.0 normally. Record HP immediately before and after the heal. Other sweep profiles keep natural regeneration.
- Cast UI Lag from the documented start with QAEnemy 2 m away. Its shipped 3 m radius is centered on the caster, regardless of selection. Capture a hit or deflect on QAEnemy.
- For Stun 2.0 select WinterFrost. The stock tooltip and shipped selector describe a 4 m area around that selected enemy. Check that Trainman is eligible and Blackwoods is outside. Record a deflect separately from an out-of-radius target. See [debuff radius evidence](ABILITY-DEBUFF-RADIUS.md).

The native Actions names and internal records can differ. Logic Barrage 1.0 (57) is `LogicBlast1`, Charge Simulacra (603) uses `AcousticDecoy1Ability`, Detonate Proxy (34) uses `StopProxy1Ability`, and Martial Arts Prowess (135) uses `MartialArtsAbility`.

## Source and limits

The source for each action ID, stored cost, base, target and modifier input is the shipped [ability catalog](../data/ability_catalog.json) indexed by [the parity audit](ABILITY-PARITY.csv). Captured costs, durations and values are overridden by the existing [cost table](../hds/world/Structures/AbilityCosts.cs), [buff table](../hds/world/Structures/AbilityBuffs.cs) and [target effect table](../hds/world/Structures/AbilityTargets.cs). For native comparison see [combat styles](COMBAT-STYLES.md), [Area Disruption](ABILITY-AREA-DISRUPTION.md), [passive consumers and Sixth Sense](ABILITY-PASSIVE-CONSUMERS.md), [Coder pets](CODER-PETS.md), [Proxy Technician](ABILITY-PROXY-TECHNICIAN.md), [conditional throws](ABILITY-CONDITIONAL-THROW.md) and [Sever Artery](ABILITY-SEVER-ARTERY-STATES.md). The Gunman bag weapons come from the shipped [weapon table](../data/weapons.csv).

Bootstrap uses the existing headless login and sends the retail-captured world-entry records `05` and `0c 01` described in [Client actions](CLIENT-ACTIONS.md#login-and-world-entry). It checks the ordinary loaded-abilities margin block of the copied helper loadout, corresponding disposable CSR rows and live target presentation. Its readiness report `ability-sweep.json` preserves those observations. The standard headless transport supplies authentication, character load and reliable acknowledgements. The sweep adds zero outbound cast, targeting, gear or hotbar actions. The [ability-use wire decode](ABILITY-USE-RECORD.md) establishes the full 43-byte record and pins a device example. Per-slot native outgoing captures are required before using those actions for headless cast replay.

Readiness carries no ability-family pass and changes no audit status. See the [first native session](https://github.com/kylebrussell/mxo-hd/pull/312#issuecomment-6995363576) and [second native session](https://github.com/kylebrussell/mxo-hd/pull/312#issuecomment-6995670186) for the original tested head. Those observations apply to their individual sampled behaviors.
