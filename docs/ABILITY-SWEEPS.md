# Native ability sweeps

NativeQa discovers six fixed level-50 profiles. Each prints eight numbered actions with the target, HP change or source input, state, duration and IS cost. Use the printed list from the same build as the native session. These profiles cover Awakened, Hacker, Coder, Operative (including Spy), Martial Artist and Gunman. Special weapons, GM actions and shared or unassigned audit entries have their own admission paths and no sweep.

| Mode | Slots 1 through 8 | Native emphasis |
|---|---|---|
| ability-sweep-awakened | 1, 5, 6, 10, 184, 11, 12, 198 | Resistance, toggle transitions, jump clock, Self Defense special |
| ability-sweep-hacker | 38, 57, 77, 97, 314, 333, 383, 72 | Area and single control, viral contest, friendly heal, timed combat buffs |
| ability-sweep-coder | 27, 20, 25, 603, 589, 22, 33, 34 | Proxy and Logic Daemon window/follow/assist, pet stats, aura, replacement and detonation |
| ability-sweep-operative | 135, 647, 123, 149, 146, 281, 282, 341 | Damage influence, taunt, map detection, thrown DOT and control |
| ability-sweep-martial-artist | 132, 267, 133, 260, 262, 428, 430, 412 | Karate, Kung-Fu and Aikido special choreography and landed effects |
| ability-sweep-gunman | 129, 126, 147, 254, 158, 176, 243, 255 | Equip each matching weapon and verify the style before testing special choreography and controls |

## Native pass

1. Run the regular [NativeQa launcher](../tools/NativeQa/README.md) for the selected mode and wait for Ready. Use the default mode loadout and only the disposable CSR_Tester (110). Use a fresh hotbar page. Drag the listed visible Actions into slots 1–8 in order. Where placement is unavailable or an action is missing, record the slot and its UI failure. The tutorial's [client hotbar notification](../research/missions/tutorial-wire-spec.md) documents the result of a UI drag. A verified server-to-client hotbar seed route remains open.
2. Use the printed targets. CSR starts at (24800, 495, -1498). WinterFrost is 3.01 m west in the test NPC cluster. QAFriend starts 2 m north and below max HP. Hacker and Operative also stage QAEnemy 8 m east, with stock hostile-server flagging. For interlock moves, confirm the named style after entering Close Combat. If WinterFrost dies, wait for a living respawn or use a fresh fixture. Check shared reuse and state immunity before repeating.
3. Save one result per slot with PASS, FAIL or BLOCKED, target and active style, HP/IS before and after, state, measured duration and real outgoing activation bytes. Retain the full framed client world RPC, including the live target/spawn and any opaque bytes, plus matching replies and UI observations. Include clean hit and deflect samples when available. Record randomized control procs separately. Compare only established fields. A stored input or captured base value does not by itself establish final scaled damage, healing or duration for the fixture's actor and recipient.

## Source and limits

The source for each action ID, stored cost, base, target and modifier input is the shipped [ability catalog](../data/ability_catalog.json) indexed by [the parity audit](ABILITY-PARITY.csv). Captured costs, durations and values are overridden by the existing [cost table](../hds/world/Structures/AbilityCosts.cs), [buff table](../hds/world/Structures/AbilityBuffs.cs) and [target effect table](../hds/world/Structures/AbilityTargets.cs). For native comparison see [combat styles](COMBAT-STYLES.md), [Area Disruption](ABILITY-AREA-DISRUPTION.md), [passive consumers and Sixth Sense](ABILITY-PASSIVE-CONSUMERS.md), [Coder pets](CODER-PETS.md), [Proxy Technician](ABILITY-PROXY-TECHNICIAN.md), [conditional throws](ABILITY-CONDITIONAL-THROW.md) and [Sever Artery](ABILITY-SEVER-ARTERY-STATES.md). The Gunman bag weapons come from the shipped [weapon table](../data/weapons.csv).

Bootstrap uses the existing headless login and sends the retail-captured world-entry records `05` and `0c 01` described in [Client actions](CLIENT-ACTIONS.md#login-and-world-entry). It checks the ordinary loaded-abilities margin block of the copied helper loadout, corresponding disposable CSR rows and live target presentation. Its readiness report `ability-sweep.json` preserves those observations. The standard headless transport supplies authentication, character load and reliable acknowledgements. The sweep adds zero outbound cast, targeting, gear or hotbar actions. The [ability-use wire decode](ABILITY-USE-RECORD.md) establishes the full 43-byte record and pins a device example. Per-slot native outgoing captures are required before using those actions for headless cast replay.

Native outcomes remain pending. The readiness report carries no ability-family PASS and changes no audit status.
