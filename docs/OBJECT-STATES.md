# Object states

What the client's own data says about objects that change state (computers, containers, doors, switches) and what each state lets the player do. The data is `data/object_states.csv` and `data/object_usables.csv`, read by `ObjectStates` (`hds/world/Structures/ObjectStates.cs`). Both were made from the 7.6005 client's `gameobjects.gob` by `build_object_states.py` in `mxo-research/object-states/`, which also writes the full decode (every state's script, every rule's script) to `object_states_raw.json` there.

Everything below is read from the client's data unless it says "guess" or "tried in the client".

## How the client does it

- **A state is an event id.** An object's `CurrentState` attribute holds one, e.g. `0x961`. Each state has a short script. The client runs it when it sees the state, at spawn or in a view update. Tried in the client: a computer spawned in `0x961` offers Take, one spawned in `0x963` offers Hack.
- **Scripts turn usables on and off.** `0x961` (ST_Giver) turns Take on and Hack off. Scripts only touch what they name, so a usable stays as it was otherwise.
- **The use number is the usable's place in the object's component list.** The client sends `80 c7 <view> <spawn> <use> 00`. A computer's components are Container, MissionElement, Model, Info, HackUsable, GetUsable, TextUsable, StateMachine, PutUsable, so Hack is 4, Take 5, Search 6, Put 8. This fits every use number we know from retail and the client: booths 3/4/5/8, NPC Loot 5, Talk 9, Get 13, Give 14, container Open 3.
- **Each usable has an on/off flag, its ActiveTracker.** The object stores whether it starts on. Some classes also send it as an attribute (`TextActiveTracker`, `TalkActiveTracker`, the phone's `ActiveTracker`). That's how retail turned on an NPC's Talk, and how a bomb turns on each defuse step in turn. The `tracker` and `starts_on` columns have it.
- **The client checks a rule before it offers a usable** (`ClientRuleID`, a function in the client's `clientrules.py`). Then the server runs its own rule (`ExecuteRuleID`, `serverrules.py`) and the result picks a script: 1 is success, other numbers are failures such as a trap going off. That script usually moves the object to another state. The server sends that state; the client doesn't move the object itself (guess, but the tutorial door only changed when retail sent the update).
- **A state can move on by itself.** Some states have a time and a next state: a door is open for 20 s, then closing for 1 s (5 s for AttributeClass365 doors), then closed. That's the timing of the retail door capture `DoorLifecycle` copies. A computer's states have a next state and no time: that's where a Take or Hack goes (the script says "go to next").

## Client rules that decide what the player sees

| Rule | Used by | Offers the usable when |
|---|---|---|
| 1002 ComputerHack | computer Hack | the player's MissionKey and the object's match, ignoring the first byte |
| 4006 GenericKeyTest | computer Put, NPC Give and Put, container Unlock | as above, and the player carries a mission item whose key matches the object's: the whole key when the item's first byte isn't 0, otherwise all but the first byte |
| 1010 ContainerOpen | container Open, NPC Search | the object has uses left and no AI, or the key matches (a first byte of `ff` doesn't count as a match) |
| 4000 DoorPickLock | Pick Lock | always, except during the tutorial |
| 4004 DoorOpenGeneric | door Open | the door has no key, or the player's key isn't for another mission |
| 4003 DoorLockTest | door Unlock | same as GenericKeyTest |
| none | Take, Search, the phone's Use | always |

A MissionKey is a u32: the first byte is the part, the other three the base (`ff 98 02 02` is part `ff`, base `0x2 0298`).

## Computers

Computer 3, MainFrame_Computer 357, Laptop 1265 and the Active_Laptops are AttributeClass20; Active_ComputerBlack/White 1417/1418 and Active_ComputerAlienware 8734 are AttributeClass19, which has an FX component first, so its use numbers are one higher (Hack 5, Take 6, Search 7, Put 9).

| State | Name | Offers | Next | Tried in the client |
|---|---|---|---|---|
| `0x960` | ST_Normal (default) | nothing (turns Take, Hack and Put off) | | can't be selected |
| `0x961` | ST_Giver | Take | `0x960` | Take |
| `0x962` | ST_Taker | Put | `0x960` | nothing (no item carried) |
| `0x963` | ST_Unhacked | Hack | `0x960` | Hack |
| `0x964` | ST_UnhackedGiver | Hack, then Take | `0x961` | Hack |
| `0x965` | ST_UnhackedTaker | Hack, then Put | `0x962` | Hack |

- **Take** (use 5) hands over what the computer holds (guess: its Container's method 2) and goes to the next state. **Hack** (use 4) sends the hack text and sound, then goes to the next state: an UnhackedGiver becomes a Giver, so the player hacks it and then takes the file. **Put** (use 8) takes the item and goes to the next state.
- **Search** (use 6, TextUsable) sends the mission text (`MissionElement/TextID`). No state turns it on: it starts off, and the server turns it on with `TextActiveTracker` 1.
- **Put needs a key item.** Put is on in `0x962` and `0x965`, but the client shows it only to a player carrying a mission item whose key matches the computer's (GenericKeyTest). That's why `0x962` offered nothing when tried.
- Going straight from `0x961` to `0x963` would leave Take on, since `0x963` only turns Hack on. Going through `0x960` first turns everything off (guess from the scripts; `ObjectStates.Enter` models it).

## Containers

Filing cabinets, safes, boxes, desks and dressers (AttributeClass46, 155), the wardrobe (369) and similar classes share one machine:

| State | Name | Offers |
|---|---|---|
| `0x835` | ClosedState (default) | Open (3) |
| `0x836` | LockedState | Pick Lock (8), Unlock (9) |
| `0x841` | UnlockedState | Open |
| `0x834` | OpenedState | nothing (turns Open off) |
| `0x839` | TrappedState | arms the trap (turns the Trap and damage volume on) |
| `0x83a` | DisarmedState | disarms it |
| `0x83b` | TriggeredState | the trap went off |

- Open succeeds to `0x834`, whose script plays the model's opening (`OpeningFX`) and turns Open off; if the trap goes off (result 2) it goes to `0x83b`. Pick Lock and Unlock go to `0x841`.
- Reach (`MaxDistance`): 100 (1 m) for most, 350 for the Wall Safe 1416. Some AttributeClass155 objects label their Open "Search".
- The tutorial's constants call `0x836` "closed" and `0x835` "ready": in the data they are LockedState and ClosedState.

## Doors

AttributeClass363, 364 and 365 doors: Open is use 3, Pick Lock 4, Unlock 8.

| State | Name | Offers | Next |
|---|---|---|---|
| `0x835` | ClosedState (default) | Open | |
| `0x836` | LockedState | Pick Lock, Unlock | |
| `0x834` | OpenedState | nothing | `0x838` after 20 s |
| `0x842` | ReopenedState (opened while closing) | nothing | `0x838` after 20 s |
| `0x838` | ClosingState | Open | `0x835` after 1 s (5 s for 365) |

Pick Lock goes to `0x835`, which is why the tutorial's retail update to `0x835` turned Pick Lock into Open. Unlock goes to `0x834` (open).

## Other objects

- **Hardline booths** (592 and the other AttributeClass3632 objects) have no state machine: Exit 3, Upload 4, Loading Area 5, Enter Construct 6, Hack Construct 7, Synchronize 8. Upload is the double-click action.
- **The desk phone** (Blinking_Desk_Phone 1401, AttributeClass128) has one usable, "Use" (use 2), whose server rule sends the mission text and stops the blinking. It has no state machine and its usable starts off; AttributeClass128's `ActiveTracker` attribute should turn it on (guess, untried). The cell phone 1399 is the same. `gob_components.tsv` lists the phone as Info, Usable, FX, Model, Interlock: that file's lists are shifted by one, and the phone's real components are MissionElement, Info, TextUsable, FX, Model.
- **Put** is offered only by computers (PutUsable) and NPCs (UsePut, use 20 on NPC_BASE, next to Give at 14), both under GenericKeyTest.
- **Data nodes**: the DataNode object 606 has no usable or state machine. DataNodeContainer 47080 is a container with timed states: BoxSetupState for 20 s, BoxInitialState for 90 minutes, then BoxCleanupState.
- **Others in the data**: room light switches (On `0x834` / Off `0x835`, Off turns back On after 30 minutes), security panels, cameras and alarms, bombs and searchlights (each defuse step turns the next one's ActiveTracker on).

## What the server does with it

- `DoorLifecycle` takes each door's open and closing times from the data (20 s and 1 s, or 5 s for AttributeClass365).
- When a mission container's loot is taken, `MissionRuntime.OnTakeAll` sends it its OpenedState (`0x834`). Retail's script went there as soon as Open succeeded; here it waits until the loot is taken, so a player who closes the loot window can open it again.
- `ObjectStates.Track` makes the server follow an object; then each use (`80 c7`) moves it to the state the data gives and sends it (`ObjectStates.OnUse`). A followed object that isn't a mission object stops there, so its uses don't reach the old generic handlers (which answered use 5 with a test loot window). Only `?objstate` objects are followed for now.
- For `MissionStaging` (not changed here): `ObjectStates.StateOffering(goid, "Take")` gives `0x961`, `"Hack"` `0x963`, `"Put"` `0x962`, `("Hack", "Take")` `0x964`, and `DefaultState(goid)` `0x960`, for class 19 computers too. `UseNumber(goid, "Hack")` gives the use number, `StateAfterUse(goid, state, use)` where a use leads, and `BuildStateUpdate(goid, state)` the view update with the class's own mask (computer `01 08`, wardrobe `01 20`, door `01 80 02`).

## The files

`data/object_states.csv`: one row per state, for 436 objects that have a usable and aren't items, characters or NPCs.

| Column | |
|---|---|
| `goid`, `object`, `class` | `class` is the attribute class (the repo's AttributeClassN) |
| `state`, `state_name` | the event id and the attribute it's stored in (ST_Giver, ClosedState, ...) |
| `default` | 1 for the object's default CurrentState |
| `next`, `seconds` | where the state goes after `seconds`, or on "go to next" when there's no time |
| `enables`, `disables` | use numbers the state's script turns on and off |

`data/object_usables.csv`: one row per usable.

| Column | |
|---|---|
| `use` | the number the client sends |
| `usable`, `label` | component name and the menu text from the client's string table |
| `cursor`, `max_distance` | blank when the object doesn't store one |
| `defaultable` | 1 if it can be the double-click action |
| `client_rule`, `execute_rule` | rule id and name (`clientrules.py`, `serverrules.py`) |
| `results` | rule result to state: `1>0x834`, or `1>next` for the current state's next |
| `tracker`, `starts_on` | the attribute that turns it on, and whether it starts on (blank: not stored) |

## How the gob was read

`gob.py` (`mxo-research/tools/client-data`) reads the file's index and record heads. What follows a record's head:

- u32 storage size, u16 layout (section 3), u16 attribute table (section 1, the repo's AttributeClassN), u16 component name list (section 2), u16 unknown, u32 n, then n bytes of the object's attribute storage. An attribute's id in section 1 is its offset in the storage; bytes past n aren't stored.
- Section 3 lists, per layout, each component's attributes. Section 2 names each component (HackUsable, GetUsable, ...): the use number is the index in that list.
- A state attribute (type 28) is 24 bytes: id, a value that is `0xffffffff` on roots and `0x13` or `0x22` elsewhere (unknown), seconds, unknown, next state, unknown.
- Then a u32 count of statements, each a u32 op count and ops: u8 code, u32 argument count, arguments of u64 value, u32 type (section 4: 5 uint32, 13 bool, 15 object, 21 EventID, 32 effect) and u32 flags (2: the value is an attribute's offset; on an object, 1 marks a role: 0 the object itself, 2 the user).
- Op 7 turns component `arg0` on or off (`arg1`). Op 0 calls a method, `arg0` counting the methods of the object's components in order (Model 11, Container 7, MissionElement 2, StateMachine 2, FX 3, NavMeshController 4, Usable 1, Info 0); the op-1 arguments after it are its arguments. A lone op 1 with two arguments sets an attribute. StateMachine's methods are 0 "set state" and 1 "go to next". The counts were solved from 1,380 set-state calls; the other methods' meanings (Container 2 hands over the contents, Model 8 plays an effect) are guesses.
- Last, a u32 count of handlers: scope, key, first op, op count. Scope 0 is an event (key = the event id; a state's id is entering it). Otherwise scope is a rule id and key the rule's result.
- Rule ids and names come from the `ID` each function gets in the client's `clientrules.py` and `serverrules.py`.

## Not known yet

To try in the client with `MXO_DEV_COMMANDS=1`. `?objstate <goid> [state hex|-] [Attribute=hex ...]` spawns the object at the player's feet with the mission's key and says what the data expects; `?objstate 0x<state>` sends the last one a new state; `?objstate item <goid> [part hex]` adds a mission item to the inventory (client only, not saved).

- Whether the phone's Use appears with `ActiveTracker=01`, and not without.
- Whether a computer's Search appears with `TextActiveTracker=01`.
- Whether Put appears in `0x962` once the player carries a key item, and with which item key part (`00` or `ff`).
- Whether `0x964` offers Take as well as Hack (if usables a state doesn't name start on, it would).
- Whether a container's `0x834` plays its opening (safe door, drawer) and takes Open off.
- Whether going from `0x961` to `0x963` leaves Take on.
