# Retail Tutorial v2 (mission 986): wire specification

Goal: rebuild the retail tutorial on the C# emulator. Sources are the decrypted retail logs, the decompiled client copy of the retail mission script (`Mission986.py`, 2006-11-15), and the class-dump field names in `docs/generated/client-rpc-classes.txt`. Nothing in the worktree was changed.

## 0. How to read this

**Evidence marks**

- **[O run:line]**: observed on the wire, at that line of that log.
- **[S]**: stated by `Mission986.py` (a line number is given when it matters), not necessarily seen on the wire.
- **[I]**: inferred. The reasoning is given.
- **[?]**: not determined. Section 7 collects these.

**Runs (log short names)**

| Short name | Log | Key | Slot | Ended |
|---|---|---|---|---|
| main | `.../20260608T120313Z-...-selected/logs/mxopackets1_crashover2k_002_create_mxoemu_character2.log.txt` | 00 d7 01 02 | 1 | complete, three Bob fights |
| uriah | `.../20260608T120313Z-...-selected/logs/mxopackets1_crashover2k__create_char_mxoemu_and_make_complete_tutorial_enteringtheworld_uriah.log.txt` | 00 26 03 03 | 0 | complete |
| unknown | `.../20260607T030922Z-...-selected/logs/mxopackets1_luizoe_old_sniffer_Unknown.log.txt` | 00 f1 02 02 | 0 | complete |
| syntax | `.../20260608T065306Z-...-selected/logs/mxopackets1_luizoe_margins_Syntax.log.txt` | 00 da 00 02 | 0 | status 8, goals 29 to 37 never closed |
| rajko, cpy, recursion, devoted, morpheuzz, norajob | see `tl.py` `FILES` | various | 0 | aborted early, status 6 |

**Coordinates.** Every position below is in **slot 0** unless marked. [O] 9 of 10 runs use slot 0. `80 a3` field `b` selects the slot: main has `b=1` and everything there is offset by **dx = -32000, dz = +64000** (y unchanged), and static ids gain **0x00100000** (`0x6a` in uriah:2399, `0x10006a` in main:2979). [I] This is a room copy within one tutorial district. Pick slot 0 unless you want concurrent players; only slots 0 and 1 were seen.

**Packet form** (already handled by the emulator): `flag [simtime] 03 <view updates> 00 00 04 <rpc lists>`. RPC opcodes below omit the size byte. Offsets inside bodies are counted from the first opcode byte, so a body offset of 18 is written as 0x14.

## 1. Script call to wire

The lines below were verified against `main` and at least one other complete run, unless marked.

### 1.1 Layouts

| Script call | Wire | Layout | Hex example |
|---|---|---|---|
| `playNarration(id)` | `81 45` | Id u32 (0x86xxxxxx). Fire and forget, the client plays the wav | `81 45 75 08 00 86` MOVEMENT_DIALOG [O main:823] |
| `broadcastMessage(sid, img)` | `80 a1` | SpeakerImageID u32, SpeakerNameID u32 (0), NameOffset u16 (0x14), StringID u32, TextOffset u16 (0x17), DisplayFlags u8 (1), ClientFlags u8 (0), sized name (empty), sized text (empty) | `80 a1 a1 06 00 90 00 00 00 00 14 00 14 00 00 88 17 00 01 00 01 00 00 01 00 00` image 0x900006a1, string 0x88000014 [O main:845] |
| `toggleView(view, show)` | `81 02` | ViewID u16, Show u8 | `81 02 0e 00 01` TARGET on [O main:823]; `81 02 01 00 00` INVENT off [O main:350] |
| `sendMissionEvent(n)` | `81 91` | EventID u32, server to client | `81 91 01 00 00 00` [O main:456]; `81 91 00 00 00 00` [O main:2230]; `81 91 02 00 00 00` [O main:12773] |
| `sendModalMessage(msg, sid, 0, left, right)` | `81 8f` | SpeakerImageID u32 (0), StringID u32, TextOffset u16 (0x1c), Message u32, LeftChoice u32, RightChoice u32, Caption u32 (0), sized text (empty) | `81 8f 00 00 00 00 15 00 00 88 1c 00 03 00 00 00 05 00 00 c2 01 00 00 c2 00 00 00 00 01 00 00` [O main:1966] |
| `completeGoal` / goal open | `80 a0` | GoalID u8, TextOffset u16 (6), Flags u8 (0 open, 1 done), sized text (u16 length incl. NUL) | `80 a0 01 06 00 00 15 00 "Move around the room\0"` [O main:823] |
| `scheduleEvent` | none | Server-side timer only. Nothing on the wire [S, consistent with every delay checked in section 4.2] | |
| `addTempAbility(a, 460, 1)` | `80 b2` | Id u16, Value u16, CSSlot u16 (0x0208). Followed by `80 bc` buff lines | `80 b2 cc 01 01 00 08 02` NPCImmortalAbility [O main:402]. Also `80 b2 bb 01 01 00 08 02` NPCAbility (443), not in the script, sent one RPC after goal 0 [O main:350] |
| `grantExperience(a, 501)` | `80 e6`, `80 e5` | Value u32, Source u32. `80 e6` = gained, `80 e5` = new total [I from names and values] | `80 e6 f5 01 00 00 03 00 00 00`, `80 e5 f5 01 00 00 03 00 00 00` [O main:28884]. Then the level-up: own view and `2e` with string 0x60000089 |
| `respawnNPC('Bob')` | NPC create | A new `0c 57 02 <new spawn> cd ab ...` at Bob's home, a new view id | [O main:19430], see 4.4 |
| `unkillNPC('Bob')` | `80 a0` | Goal 23 reopened with **empty text** | `80 a0 17 06 00 00 01 00 00` [O main:19420]. [I] that this RPC is unkillNPC's effect: it is the only thing sent besides the narration and the new NPC |
| `playLocalFX(fx)` | `81 42` | FXID u32, TokenID u32, UserData u32 | `81 42 92 07 00 28 00 00 00 00 00 00 00 00` fade to black, no token [O main:2451] |
| `playLocalFX(fx, token)` | `81 42` | Same, with TokenID = script event id. **The client sends it back as `81 91 <token u32>` when the FX ends**, and that drives the teleport | `81 42 1d 00 00 22 8a 13 00 00 00 00 00 00` (token 5002) [O main:2908]; reply `81 91 8a 13 00 00` [O main:2918] |
| `teleportToMissionElement(name)` | `81 99` | x, y, z f64 then quaternion x, y, z, w f32. Not in the class dump | `81 99 00 00 00 00 00 45 da c0 00 00 00 00 00 40 7a 40 00 00 00 00 80 18 e7 40 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f` = main slot 1 (-26900, 420, 47300), quat (0,-0.7071,0,0.7071) [O main:2923] |
| `bootPlayer` | [?] | Blue pill path never captured | |

Note on `81 42`: the FX values in `hds/world/Structures/FX.cs` are byte-swapped relative to the wire. FX.cs `0x1d000022` is wire `1d 00 00 22`, a real id of 0x2200001d, because of how that file prints them.

### 1.2 Mission element coordinates (slot 0)

[O uriah:1914, 2341, 4128, 12649, 14680; identical in unknown and syntax; main is the same plus the slot 1 offset]

| Element | Teleport x, y, z | Quaternion | Annotator's zone | Standing y |
|---|---|---|---|---|
| start (not a teleport) | 9400, 295, -17200 | n/a | | 295 |
| Interrogation | 7600, 3920, -16150 | 0, -0.3987, 0, 0.9171 | 2 [O main:3926] | 3995 |
| Gallery | 5100, 420, -16700 | 0, -0.7071, 0, 0.7071 | 6 [O main:5021] | 495 |
| Dojo Ante | 5800, -1180, -13700 | identity | 4 [O main:12208] | -1105 |
| Firearm Training | 6700, 1520, -12300 | identity | 1 [O main:25704] | 1595 |
| Exit | 6950, 220, -11100 | identity | 3 [O main:28235] | 295 |

The player's own `/loc`-style notes read 75 higher than the teleport y [O main:3926 vs main:2673, main:5690 vs main:2923]. Send the teleport y as listed; the client settles at y + 75. All are sector 1, group 34, area 0 per the annotator.

### 1.3 Panel ids

[O] from the wire. `constants.PANEL` lives in the DLL; the string order at `dll_strings.txt` 39585-39599 agrees with the interlock and level-up runs.

| Name | Id | Name | Id | Name | Id |
|---|---|---|---|---|---|
| INVENT | 0x01 | TARGET | 0x0e | INTERLOCK_BLOCK | 0x1a |
| ACTIONS | 0x02 | TOOLBAR | 0x0f | INTERLOCK_GRAB | 0x1b |
| STATUS | 0x03 | PLAYER_VIEW | 0x15 | INTERLOCK_WITHDRAWL | 0x1c |
| MISSION | 0x05 | MINITEAM | 0x16 | FIRST_ABILITY | 0x1d |
| TEAM | 0x06 | INTERLOCK_POWER | 0x18 | ADVANCE_LEVEL | 0x1e |
| MAP | 0x07 | INTERLOCK_SPEED | 0x19 | APPLY_ADVANCE | 0x1f |
| CREW | 0x08 | BUFFHUD | 0x27 | OPEN_ATTRIBUTE | 0x20 |
| FACTION | 0x09 | LEVEL_UP | 0x0c | UPGRADE_ATTRIBUTE | 0x21 |
| PDA | 0x0b | | | CONFIRM_ATTRIBUTE | 0x22 |

## 2. Client to server triggers

| Trigger | Wire | Layout | Hex | Effect |
|---|---|---|---|---|
| Movement | own-view object update | `03 02 00 01 <mask> [02: state u8] [04: yaw u8] [08: x y z f32] 00 00` | `01 0e 03 f7 7d a8 b0 c6 00 80 93 43 86 06 37 47` [O main:768]; yaw-less `01 02 77` [O main:501] | Goals 0 and 1. Baseline is the first update; a later different yaw closes 0, a different position closes 1 [S lines 748-764]. Note the operator precedence in line 758: `not movementComplete and x or y or z` |
| Toggled view activated | `81 04` | ViewID u16, ResponseToken u32 (0) | `81 04 15 00 00 00 00 00` [O main:2872] | The goals in `OnToggledViewActivated`. TOOLBAR (0x0f), BUFFHUD (0x27) and TARGET (0x0e) are sent automatically within a second of the toggle; the others wait for a click [O main:840, main:12306] |
| Modal answer | `81 90` | Message u32, LeftChosen u8 | `81 90 03 00 00 00 00` = message 3, **right** button = red pill [O main:2220]; `81 90 19 00 00 00 01` = message 25, left = "Again!" [O main:19415]; `81 90 19 00 00 00 00` = "Move on." [O main:24222] | `OnModalDialogueAccepted(.., messageid, leftchoice)`. `0300000000` in all 4 complete runs |
| Inventory swap | `63` | SrcSlot u8, DestSlot u8 | `63 02 6b` [O main:10729] | Reply `65 02 00 68 00 00`: SlotA u16, SlotB u16, flag u8. So client dest 0x6b becomes slot **0x68** (ShoesSlot, [I] from the reply and from the gun going to 0x69). Goal 10 needs src item 1258 or 905 and goal 9 complete [S lines 726-735] |
| Shortcut added | `81 8d` | ShortcutType u32, InvSlot u32, ClassData u32, InstanceData u32 | `81 8d 00 00 00 00 00 00 00 00 c6 00 00 00 00 00 00 00` = type 0, ability 198 (CheapShotAbility) [O main:12388] | Goal 15, once goal 14 is complete [S lines 737-746]. Five `type=7` shortcuts follow event 2 and must be ignored [O main:12788] |
| FX finished | `81 91` | EventID u32 | `81 91 8a 13 00 00` [O main:2918] | Token returned from `81 42`. Runs the teleport |
| Object use | `80 c7` | Handle (view u16, spawn u16), ComponentOffset u16 | `80 c7 03 00 2b 00 09 00` talk [O main:1766] | Component values below |
| Static use | `80 c8` | ObjID u32, ComponentOffset u16 | `80 c8 4d 00 10 00 06 00` [O main:28874] | Same meaning as `80 c7` on the object bound to that static id |
| Take All | `81 1f` | empty | `81 1f` [O main:8795] | Goals 8 and 24 |
| Close loot | `81 1a` | empty; server echoes `81 1a` | [O main:8522, 8527] | Wardrobe state back to 0x835 |
| Start combat | `40` | TargetID (view u16, spawn u16), ForcedInterlock u8, WeaponToUseSlot u8 | `40 08 00 35 00 01 6b` melee [O main:17871]; `40 04 00 3b 00 00 04` ranged [O main:26656] | Goal 22 on Bob. The emulator's combat code already takes these |
| Exit start | `80 fc` | ExitType u8 | `80 fc 01` [O main:29897] | Section 3.3 |

`80 c7` ComponentOffset values seen [O]: `09` talk, `0e` give, `03` open wardrobe or door, `05` loot (NPC) and jack out (booth), `06` upload on the booth. A repeat upload is answered `81 18 00 00 0d 00 00 98` [O unknown:14079, syntax:24315]: Status 0x9800000d, whose string is not in the extracted tables; [I] nothing left to upload.

`81 48` (empty) comes from the client once when PLAYER_VIEW first shows, and no reply was seen [O main:2842, uriah:2277]. `81 54` with position is a debug query answered by an echo with `07 01 00` [O main:2847, 2862] [I from the annotator's "Setup state is 7" at main:26010]. `28` chat in `main` and `uriah` is the capturer annotating; it is useful for labels and otherwise irrelevant.

## 3. Entering and leaving

### 3.1 World load

[O main:194] `06` WorldFile: `06 0e 00 22 03 00 00 e6 94 0d 49 01 49 00 39 00 "resource/worlds/final_world/tutorial_v2/tutorial_v2.metr\0" 2b 00 "Halloween_Event,Winter3HalloweenFlyEyeTSEC\0"`

- Layout: path offset u16 (0x0e), ConstructID u32, SimTime u32, bLoadObjs u8, RezOverrides offset u16, sized path, sized overrides.
- ConstructID 0x322 in 7 runs, 0x321 in syntax, recursion and morpheuzz [O inst.py]. [I] two tutorial districts or servers. The same value is in `80 a3` field `a`.
- Followed by `80 e5 <xp>` and `80 e4 <info>` with Source 0 (`80 e4 e8 03 ...` = 1000 info on a new character) [O main:194].
- Some runs start from the Loading Area first (main:33, `06` la.metr ConstructID 0x385); others go straight to the tutorial (uriah:151). The character-creation path decides that [?]. The emulator already loads district 0 as the tutorial (`PlayerHandler.processSpawn`).
- No flag on the character tells the client it is in the tutorial. [O] The self-create (main:217, `0c 0c 00 29 cd ab`) is a normal PlayerCharacter: Level 1, HP 150, IS 100, Position (slot 0: 9400, 295, -17200), TeleportCounter 1. [I] The client is driven by the world and mission RPCs.

### 3.2 Mission start order

[O main:350-527; order holds in all 10 runs except as noted]

1. Client `80 be`, `0c 01` (LoadingComplete) [main:286].
2. Own-view update `03 02 00 03 ...` setting position and MissionKey `00 <instance u16> <keyByte>`, plus settings `3a` and `8167` [main:298-350]. Raw at main:350: `03 02 00 03 28 00 40 00 00 00 10 00 <x y z f32> <sim u32> 80 f0 ff bf 01 00 02 00 d7 01 02 80 80 80 10 11 00 00 00 00 00`. Attribute indices around the MissionKey are not decoded [?].
3. `80 8d` team: `80 8d bf 01 00 02 0e 00 04 00 00 00 00 00 16 00 "mxoemu's Mission Team\0"` (team id and level vary).
4. `OnStartOfTutorialMission`: `81 02 <v> 00 00` for 01, 02, 03, 05, 06, 07, 08, 09, 0b, 0e, 0f, 27, 16, 15, in that order.
5. `80 9c` mission name. **It can land anywhere in the toggle run**: after 0f in main, after 15 in uriah, after 09 in unknown, after 0b in norajob. [I] Independent, so send it either side.
   `80 9c 00 00 00 00 00 00 2b 00 00 00 d7 01 02 00 00 00 00 00 00 00 00 00 00 00 00 14 00 00 26 70 0a 00 28 00 00 ff 3c 00 39 00 00 0c 00 "Tutorial v2\0" 01 00 00 01 00 da 03 00 00`
   That is: status u32, txn u16, title offset 0x2b, org u16, instance u16, key u8, contact id/name/image 0, operator name 0x26000014, operator image 0x28000a70, flags `00 00 ff`, list offset 0x3c, empty offset 0x39, title, empty, one template id 986.
6. `80 a3 30 00 22 03 01 00 00 00 00 00 00 00 00 00 3c 00` + 30 zero bytes + `01 00 00`. Fields: text offset 0x30, ConstructID, **slot u16**, ..., e=60, empty text. In main slot=1, otherwise 0.
7. `80 a0 00 06 00 00 ...` goal 0 open.
8. `80 b2 bb 01 ...`, then `80 bc` buffs for ability 0x11, then `80 b2 cc 01 ...` and its `80 bc` buffs (`mission.addTempAbility(460)` plus the unscripted 443) [main:350, 402].
9. `81 45 70 08 00 86` CAMERA_DIALOG, `81 91 01 00 00 00` [main:456].
10. Object creates: TeamInfo (goid 2584), Ammit [main:511]. These run on the object stream, so they may come earlier: in the same packet as `80 9c` at uriah:466 and rajko:1066. Channel-1 message for the TeamInfo view: `01 00 02 24 00 05 00 01 cf 07 41 a0 07 00 22 03 00 00 00 64 00 64 00 96 00 96 00 <x y z f32> 00 00` [main:527], carrying char id, ConstructID, IS and HP.

From then on view 5 (TeamInfo) follows the player: `05 00 01 00 04 <x y z f32>` [main:793]. [?] Whether the client needs TeamInfo at all; a solo mission may run without it. Its goid is missing from `GameObjectDefinitions.cs`.

### 3.3 Completed exit

| Step | Dir | Wire | Line |
|---|---|---|---|
| 1 | C>S | `80 c7 <booth view> <booth spawn> 05 00` | main:29816 |
| 2 | S>C | `80 b3 11 00`, buff removals `80 bc`, `80 b3 bb 01`, `80 b3 cc 01`, `81 01` | main:29821 |
| 3 | S>C | `80 a0 25 06 00 01 ...`, `81 45 4b 07 00 86` | main:29873 |
| 4 | C>S | `80 fc 01` (2 s later) | main:29897 |
| 5 | S>C | `80 fd 00 00 00 00 01 00` (Status u32, ExitType u8, PlayerHasDied u8) | main:29902 |
| 6 | C>S | `80 fe 01` | main:29907 |
| 7 | S>C | `80 a2 08 04 00 00 00 4e dd 8e b5 00 ...` status 8, kills u16 (4: three Bobs and Alex; 2 in uriah and unknown), team killed u16 0, then a u32 that looks like seconds but is not, xp 0, info 0, rep 0, empty arrays, intro state 0 | main:29912 |
| 8 | S>C | `80 e5 f5 01 00 00 02 00 00 00`, `80 e4 c8 00 00 00 02 00 00 00`, `8107` | main:29912 |
| 9 | C>S | `8108 00 00 00 00` | main:29933 |
| 10 | S>C | `8109`, `72 03 <char id u32>` (GroupType 3, PlayerID), `06` la.metr ConstructID 0x385 or 0x386, `80 e5`, `80 e4` with Source 0. Repeats about once a second until the client answers | main:29963-30015 |
| 11 | C>S | `05`, `6c 01 00 00 00 00 00 00 00` (ConstructID 1, HardlineID 0 = default) | main:30005 |
| 12 | S>C | settings, `6d 01 00 00 00 08 00 01 01 00 00 99 00 00 00 00` hardline list, [C>S `8101`], `80 fa 01 00 00 00 99 00 00 00 01` (Construct 1, Hardline 153, make active), `8107` | main:30101-30153 |
| 13 | S>C | `06` `slums_barrens_full.metr` ConstructID 1 | main:30221 |
| 14 | S>C | Self-create at **(12300, -705, 59700)**, Level 2, HP 200, IS 103, MissionKey `ff ff ff ff`, MissionTypeFlags 255, EquippedItemID = the Revolver | main:30249; same position uriah:16450, syntax:25177 |

- Information 1000 down to 200 by then: the Awakened upgrade (section 4, goal 33). XP 501.
- Intro state: the `80 a2` trailing byte is 0 in every run and no `80 aa` was sent. So no retail intro-state change is visible on the world wire [O], and "tutorial done" is [I] held server-side (district or a character flag). The emulator chooses.
- syntax exited with goals 29 to 37 open and still got status 8 [O syntax:24903], so the booth works regardless of mission progress once it can be reached.

### 3.4 Abort (status 6)

[O rajko:1431-1518, cpy:3558-3614; also devoted, morpheuzz, recursion, norajob]

C>S `80 a6`, `80 fc 01`. S>C `80 fd 00 00 00 00 01 00`, then one packet with own-view level-up, `80 a1 70 0a 00 28 14 00 00 26 14 00 0a 00 00 26 17 00 01 01 01 00 00 01 00 00` (Operator: ID_MISSION_LEAVE_AREA, "Leave the area now!"), `80 a2 06` + 23 zero bytes, `80 e5 00 ... 02`, `80 e4 e8 03 ... 02`, `80 e6 f5 01 00 00 03 ...`, `80 e5 f5 01 00 00 03 ...` (`OnAbort`: 501 XP at level 1 [S line 53-57]), `2e 17` level-up text. C>S `80 fe 01`. S>C `80 b3 11 00`, `80 b3 bb 01`, `80 b3 cc 01`, `80 a2 08` + zeros and the odd u32, `80 e5`, `80 e4`, `72`, `8107`. Then the same Loading Area flow as step 10 onward.

## 4. The whole tutorial

### 4.1 Engine rules

These are what the script assumes the mission engine does. All [O].

1. **Packet order on completion**: `80 a0 <g> flags=1`, then everything `OnGoalComplete(g)` emits, then `80 a0 <g+1> flags=0`. [O main:823, 2833, 8825]. The exceptions in the goal table are RPCs that come before `flags=1`.
2. **Early completions are cached silently and replayed.** `completeGoal` on a future goal sends nothing. When the previous goal closes, the next goal goes straight to `flags=1` with no `flags=0`, and its `OnGoalComplete` runs right then. So the whole chain can arrive in one packet.
   - uriah: talk to Ammit at line 743 sends the NPC reply and nothing else; at line 1084, `80 a0 00 01`, narr, `80 a0 01 00`, `80 a0 01 01`, toggle, narr, **`80 a0 02 01` with no open**, narr, `80 a0 03 00`.
   - unknown:14136-14321: panel clicks 0x0c, 0x1d, 0x1e, 0x1f, 0x20, 0x21, 0x22 close nothing; the click on 0x03 then closes 29 to 36 in order with all their narrations and opens 37.
   - Timers scheduled inside that chain still fire, and the `isGoalComplete` guard in `OnMissionEvent` drops them [S lines 313-319].
3. **Late events are dropped** by that same guard. A delayed event for goal `g` (`g+1000`) or a reminder (`g`) does nothing once `g` is complete. Events at 2000 and up skip the check.
4. **Reminder loop**: the broadcast event schedules the reminder event; the reminder sends narration + broadcast and reschedules itself every 60 s. Confirmed: TALK2 main:3024, 4554 (+59), 6439 (+60), 7726 (+61). Log timestamps are whole seconds. Reminders for the interlock goals use OPERATOR_IMG rather than the tactic's own first-broadcast image [S lines 533-556]. Goals 5 and 15 remind with a broadcast and no narration [S].
5. **Mission-data goals**, marked "Mission data" in the table, are completed by the engine from data this script does not contain: goals 2, 6, 7, 8, 11, 21, 22, 23, 24, 27, 28, 37. `Mission986.py` runs only `OnGoalComplete` for them.

### 4.2 Timers checked on the wire

All from `main`; uriah, unknown and syntax agree where the step was not skipped.

| Step | Script | Observed |
|---|---|---|
| Goal 2 close to modal | 28 s | 06:34:33 to 06:35:01, 28 s; uriah 13:23:48 to 13:24:16 |
| Red pill to fade | 7 s | 06:35:11 to 06:35:18 |
| Fade to teleport | 10 s | to 06:35:28 |
| Teleport to CRAPPYPANTS | 24 s | to 06:35:52 |
| CRAPPYPANTS to goal 4 close | 25 s | to 06:36:17 |
| Goal 5 close to white in | 5 s | 06:36:23 to 06:36:28 |
| White out to TALK2 | 5 s | 06:36:29 to 06:36:34 |
| TALK2 to its broadcast | 5 s | to 06:36:39 |
| DAMAGE broadcast to goal 12 close | 40 s | 06:43:53 to 06:44:33 |
| Goal 21 close to CLOSECOMBAT broadcast | 39 s | 06:46:47 to 06:47:27 (+1) |
| CONNING broadcast to goal 26 close | 12 s | 06:53:47 to 06:53:59 |
| UPLOAD broadcast to first reminder | 71 s | 06:55:49 to 06:57:01 (+1) |

The fade uses a plain timer (no token). The four white transitions use tokens, so **the teleport waits for the client's `81 91`**, 1 to 2 s after the FX starts [O main:2908-2923, 11946-11971, 24287-24328, 27750-27765].

### 4.3 Goal table

Machine-readable with full hex: `goals.json`. Text is verbatim from captured `80 a0`.

Evidence for this table: goal open and close wire, order, the 'Completes when' triggers and the delays checked in 4.2 are [O] at the lines shown. The rest of each row is from the script [S]. Specifically, these were **never captured** and rest on `Mission986.py` alone:

- Broadcasts: 0x88000013 (MOVEMENT), 0x8800001b (LOOT conditional), 0x88000024 (LOOT2 conditional).
- Reminder narrations: CAMERA, MOVEMENT, TALKING, CONTAINER, LOOT, INVENTORY, ACTIONS, SPEED, POWER, GRAB, BLOCK, WITHDRAWL, COMBAT_LOOT, LEVELUP, LEVELUP2, LEVELUP_ABILITY, LEVELUP_ABILITY3, LEVELUP_ATTRIBUTE family (0x86000c2c), EXIT.

Everything else in the table was seen at least once across the 10 runs. Every broadcast image that was captured matches the script value.


| # | Constant | Goal text | Server sends on open (delays from the previous goal's close) | Completes when | Extra wire on completion | Evidence (open>close lines; seconds) |
|---|---|---|---|---|---|---|
| 0 | CAMERA | Move the camera around by holding down the right mouse button | narr CAMERA_DIALOG 0x86000870<br>event 81 91 01 00 00 00<br>+26s broadcast 0x88000012 img 0x28000ba4 | OnMovementUpdated: yaw differs from the previous update (needs one earlier update; closes within about 1 s of the change, so [I] polled) `C>S own-view update 03 02 00 01 <mask with 04> <yaw u8>...` | - | main 350>823 (16s); uriah 10; unknown 15; syntax 10 |
| 1 | MOVEMENT | Move around the room | narr MOVEMENT_DIALOG 0x86000875<br>+1s broadcast 0x88000013 img 0x28000ba5 | OnMovementUpdated: position differs. In all captures it closes in the same packet as goal 0 `C>S own-view update with mask 08 (x y z f32)` | - | main 823>823 (0s); uriah 0; unknown 0; syntax 0 |
| 2 | TALKING | Talk to Ammit with double-left click | show TARGET(0xe)<br>narr TALKING_DIALOG 0x86000879<br>+0s broadcast 0x88000014 img 0x900006a1 | Talk to Ammit. Mission data, not in the script. Cached if done early (uriah 743) `80 c7 <Ammit view u16> <Ammit spawn u16> 09 00` | NPC update on Ammit's view (not decoded, main 1843)<br>2e NPC chat line + 80 a1 img 0x2800121f text 'Listen to what the Captain has to say. And be honest.' | main 823>1843 (19s); uriah closed without 80 a0 flags=0 (completion was cached); unknown 0; syntax closed without 80 a0 flags=0 (completion was cached) |
| 3 | CHOICE | Choose wisely | narr CHOICE_DIALOG 0x8600087B<br>+28 s: 81 8f 00 00 00 00 15 00 00 88 1c 00 03 00 00 00 05 00 00 c2 01 00 00 c2 00 00 00 00 01 00 00 (modal msg=3)<br>+28+71 s then every 60 s, if unanswered: narration CHOICE_REMINDER + broadcast ID_TUTORIAL_NEW_PILL image 0x900006a1 [script] | Red pill: Message=3, LeftChosen=0. Left (blue) was never captured: script sends BLUEPILL_CONFIRM + modal msg=7000 `81 90 03 00 00 00 00` | 81 91 00 00 00 00 (event 0)<br>81 45 narration REDPILL_DIALOG, sent BEFORE 80 a0 03 flags=1 | main 1843>2230 (38s); uriah 38; unknown 30; syntax 29 |
| 4 | REDPILL | New Recruit Orientation | +7 s: 81 42 92 07 00 28 00 00 00 00 00 00 00 00 (fade to black, token 0)<br>+17 s: 81 99 teleport 'Interrogation' (7600, 3920, -16150) quat (0,-0.3987,0,0.9171)<br>+22 s: Ammit view deleted (main 2721, about 5 s after teleport; interest, not script)<br>+41 s: 81 45 CRAPPYPANTS_DIALOG 0x860007d0 | Script timer: 66 s after goal 3 (7+10+24+25); observed 66-67 s | - | main 2230>2833 (66s); uriah 67; unknown 67; syntax 66 |
| 5 | CALIBRATE | Click on your display to calibrate | show PLAYER_VIEW(0x15)<br>narr CALIBRATE_DIALOG 0x86000965<br>+5s broadcast 0x88000036 img 0x280011a0 | OnToggledViewActivated `81 04 15 00 00 00 00 00 (PLAYER_VIEW)` | - | main 2833>2884 (6s); uriah 3; unknown 2; syntax 2 |
| 6 | TALK2 | Talk to Scarlett | narr CALIBRATE_SUCCESS 0x86000714<br>+10s broadcast 0x88000016 img 0x280011a0<br>+5 s: 81 42 1d 00 00 22 8a 13 00 00 00 00 00 00 (white in, token 5002)<br>On C>S 81 91 8a 13 00 00: 81 99 'Gallery' (5100, 420, -16700) quat (0,-0.7071,0,0.7071), then 81 42 1e 00 00 22 + 8 zero bytes (white out)<br>Then Scarlett and the Wardrobe spawn (interest, about 1 s later)<br>+5 s after teleport: 81 45 TALK2_DIALOG 0x86000715 | Talk to Scarlett. Mission data `80 c7 <Scarlett view> <Scarlett spawn> 09 00` | Wardrobe view: 01 20 35 08 00 00 (state 0x835, usable)<br>Scarlett NPC update<br>2e + 80 a1 img 0x2800121f: "Hello there. I've hidden a key, but apparently too well. Can you help me?" | main 2884>7920 (200s); uriah 13; unknown 12; syntax 11 |
| 7 | CONTAINER | Search the wardrobes for a key. Optional, press V to highlight interactive objects. | narr CONTAINER_DIALOG 0x8600071A<br>+5s broadcast 0x88000019 img 0x280011a0 | Open the wardrobe. Mission data `80 c7 <Wardrobe view> <Wardrobe spawn> 03 00` | Wardrobe view: 01 20 34 08 00 00 (state 0x834, open), sent before 80 a0 07 flags=1 | main 7920>8468 (18s); uriah 8; unknown 7; syntax 5 |
| 8 | LOOT | Take all of the loot | narr LOOT_DIALOG 0x8600071C<br>+0s broadcast 0x8800001a img 0x280011a0<br>81 19 87 02 00 76 10 00 00 00 00 00 05 <instance u16> <keyByte> 03 00 01 <instance u16> <0x80/keyByte> 38 02 00 00 00 00 00 14 89 03 00 00 00 00 00 14 ea 04 00 00 (loot list: Door_Key, F shoes, M shoes), after 80 a0 08 flags=0 | Closes in the Take All reply packet. Closing the window (81 1a) does not complete it (main 8522) `81 1f (Take All, empty body)` | 5e 02 00 ea 04 00 00 00 00 00 14 01 + 81 89 <charid u32> ea 04 00 00<br>5e 03 00 89 03 00 00 00 00 00 14 01 + 81 89 <charid> 89 03 00 00<br>5e 04 00 38 02 00 00 01 <instance> <0x80/key> 01 + 81 89 <charid> 38 02 00 00<br>81 20 0c 00 00 00 00 00 00 00 00 00 03 00 <3 items in list order, reversed><br>after 81 1a from client: 81 1a + wardrobe 01 20 35 08 00 00 | main 8468>8825 (21s); uriah 2; unknown 2; syntax 2 |
| 9 | INVENTORY | Use the Character menu to check out your Inventory | show INVENT(0x1)<br>narr INVENTORY_DIALOG 0x8600071F<br>+5s broadcast 0x8800001c img 0x280011a0 | OnToggledViewActivated `81 04 01 00 00 00 00 00 (INVENT)` | - | main 8825>9118 (15s); uriah 3; unknown 1; syntax 59 |
| 10 | EQUIP | Drag on your new shoes | narr EQUIP_DIALOG 0x86000722<br>+5s broadcast 0x8800001d img 0x280011a0 | OnInventoryItemsSwapped: src item is 1258 or 905 and dest is ShoesSlot, goal 9 already complete. Client sends dest 0x6b, server answers slot 0x68 `63 02 6b` | 65 02 00 68 00 00 (swap ack: slot 2 <-> 0x68), BEFORE the goal<br>own-view/RSI updates (not decoded) | main 9118>10744 (78s); uriah 8; unknown 1; syntax 4 |
| 11 | GIVE | Right-click Scarlett and Give her the key | narr GIVE_DIALOG 0x86000724<br>+5s broadcast 0x8800001e img 0x280011a0<br>0-1 s after: 2e + 80 a1 img 0x2800121f, Scarlett: 'Where {i}is{/i} that key?' | Give key. Mission data `80 c7 <Scarlett view> <Scarlett spawn> 0e 00` | 5f 04 00 01 (remove key slot)<br>81 22 38 02 00 00 01 <instance> <0x80/key> 00 00 00 00<br>after goal 12 opens: 2e + 80 a1 Scarlett 'Thanks! You may pass.' | main 10744>11853 (100s); uriah 13; unknown 9; syntax 3 |
| 12 | DAMAGE | Learn about the combat UI | narr GIVE_SUCCESS 0x86000727<br>+10s broadcast 0x88000038 img 0x280011a0<br>+7 s (6-7 observed): 81 42 1d 00 00 22 8b 13 00 00 00 00 00 00 (token 5003)<br>On C>S 81 91 8b 13 00 00: 81 99 'Dojo Ante' (5800, -1180, -13700) identity quat + white out<br>Door (goid 6994) spawns about 1 s after<br>+5 s after teleport: 81 45 DAMAGE_DIALOG 0x86000966 | 40 s after the DAMAGE broadcast (script); observed 58-59 s after goal 11 | - | main 11853>12168 (58s); uriah 58; unknown 59; syntax 58 |
| 13 | ACTIONS | Click on the Character menu and select Actions to open your Action panel | show ACTIONS(0x2)<br>narr ACTIONS_DIALOG 0x86000729<br>+5s broadcast 0x8800001f img 0x280011a0 | OnToggledViewActivated `81 04 02 00 00 00 00 00 (ACTIONS)` | - | main 12168>12293 (36s); uriah 5; unknown 5; syntax 3 |
| 14 | HOTBAR | Learn about the Hotbar | show TOOLBAR(0xf),BUFFHUD(0x27)<br>narr HOTBAR_DIALOG 0x8600072B | OnToggledViewActivated. Client answers at once (0-1 s), with 3f, 3e and 81 04 27 00 `81 04 0f 00 00 00 00 00 (TOOLBAR)` | - | main 12293>12313 (0s); uriah 1; unknown 0; syntax 0 |
| 15 | CHEAPSHOT | Drag your Cheap Shot onto your hotbar | +5s broadcast 0x88000035 img 0x280011a0 | OnShortcutAdded: ShortcutType=0, ClassData=198 (CheapShotAbility), goal 14 already complete `81 8d 00 00 00 00 00 00 00 00 c6 00 00 00 00 00 00 00` | - | main 12313>12403 (18s); uriah 6; unknown 8; syntax 13 |
| 16 | SPEED | Calibrate your blue Speed tactic | show INTERLOCK_SPEED(0x19)<br>+5s broadcast 0x88000039 img 0x280012f7 | OnToggledViewActivated. Client also sends 42 00 ... `81 04 19 00 00 00 00 00 (INTERLOCK_SPEED)` | 47 tactic update | main 12403>12470 (11s); uriah 5; unknown 6; syntax closed without 80 a0 flags=0 (completion was cached) |
| 17 | POWER | Calibrate your red Power tactic | show INTERLOCK_POWER(0x18)<br>narr SPEED_SUCCESS 0x86000969<br>+5s broadcast 0x8800003a img 0x28001305 | OnToggledViewActivated `81 04 18 00 00 00 00 00 (INTERLOCK_POWER)` | 47 tactic update | main 12470>12535 (8s); uriah 1; unknown 0; syntax 4 |
| 18 | GRAB | Calibrate your green Grab tactic | show INTERLOCK_GRAB(0x1b)<br>narr POWER_SUCCESS 0x8600096A<br>+5s broadcast 0x8800003b img 0x280012ff | OnToggledViewActivated `81 04 1b 00 00 00 00 00 (INTERLOCK_GRAB)` | 47 tactic update | main 12535>12610 (6s); uriah 1; unknown 1; syntax 8 |
| 19 | BLOCK | Calibrate your yellow Block tactic | show INTERLOCK_BLOCK(0x1a)<br>narr GRAB_SUCCESS 0x86000967<br>+5s broadcast 0x88000037 img 0x280012fe | OnToggledViewActivated `81 04 1a 00 00 00 00 00 (INTERLOCK_BLOCK)` | 47 tactic update | main 12610>12695 (7s); uriah 1; unknown 1; syntax 1 |
| 20 | WITHDRAWL | Select Withdraw with mouse click | show INTERLOCK_WITHDRAWL(0x1c)<br>narr BLOCK_SUCCESS 0x8600096B<br>+5s broadcast 0x8800003c img 0x280012fd | OnToggledViewActivated `81 04 1c 00 00 00 00 00 (INTERLOCK_WITHDRAWL)` | 47 tactic update | main 12695>12773 (7s); uriah 4; unknown 0; syntax 12 |
| 21 | ENTERDOJO | Open the doors and enter the dojo. | narr ENTERDOJO_DIALOG 0x8600072F<br>event 81 91 02 00 00 00<br>Client answers with five 81 8d type=7 tactic shortcuts (all 4 complete runs)<br>No broadcast or reminder: the script never schedules ENTERDOJO+1000, and none was captured | INFERRED: player enters the dojo zone. Closed 2-5 s after the door use, with player z (slot 0) -12573, -12450, -12428, -12383. Door plane z=-12600; annotator (main 17416) says the zone changed to 5 `80 c7 <Door view> <Door spawn> 03 00, then walking through` | Door view on use: 01 80 02 34 08 00 00 (0x834 open); 20 s later 0x838; 1 s later 0x835 | main 12773>13476 (41s); uriah 6; unknown 9; syntax 8 |
| 22 | COMBAT | Use your new combat skills | narr COMBAT_DIALOG 0x860007FD<br>+39s broadcast 0x88000021 img 0x280011a0 | Start close combat with Bob (Target, ForcedInterlock=1, weapon slot 0x6b). Mission data. Closes while the combat setup packets go out `40 <Bob view u16> <Bob spawn u16> 01 6b` | No narration on close (main 17912) | main 13476>17912 (192s); uriah 41; unknown 13; syntax 30 |
| 23 | ABILITIES_USE | Defeat the Guard. Notice: your camera will pivot around your fight. | - | Bob dies: 81 9d death notice, 46 end of combat, then 80 a0 17 flags=1. The script's ABILITIES_USE+1000 path is never scheduled, so this is mission data | - | main 17912>24010 (206s); uriah 54; unknown 36; syntax 23 |
| 24 | COMBAT_LOOT | Loot the corpse | narr COMBAT_LOOT_DIALOG 0x86000737<br>+2s broadcast 0x88000023 img 0x280011a0 | Loot and Take All on the corpse `80 c7 <Bob view> <Bob spawn> 05 00, then 81 1f` | Corpse view on open: 05 00 80 80 80 80 80 40 34 08 00 00 (CurrentState 0x834)<br>81 19 00 00 00 00 10 00 00 00 00 00 02 <instance> <key> 02 00 cb <instance> <0x80/key> 89 1a 00 00 00 00 00 14 05 0a 00 00 (Letter, Revolver0; main has 00 00 00 10 for the gun)<br>5e 04 00 05 0a 00 00 <data> 01 + 81 89; 5e 05 00 89 1a 00 00 cb <instance> <0x80/key> 01 + 81 89; 81 20 0c 00 ... 02 00 <items><br>3f <new slot> 00 10 00 00 00 from the client (auto-arms the gun) | main 18859>19379 (34s); uriah 12; unknown 6; syntax 9 |
| 25 | RESPAWN | Practice combat. | narr RESPAWN_DIALOG 0x8600096D<br>81 8f 00 00 00 00 34 00 00 88 1c 00 19 00 00 00 46 00 00 88 43 00 00 88 00 00 00 00 01 00 00 (modal msg=25, left 'Again!', right 'Move on.'), between the narration and 80 a0 19 flags=0 | Right choice ('Move on.'). Left: 81 90 19 00 00 00 01 respawns Bob, see 'respawn loop' `81 90 19 00 00 00 00` | - | main 19379>24227 (132s); uriah 3; unknown 3; syntax 3 |
| 26 | CONNING | Learn about conning and sizing up your enemies | narr COMBAT_LOOT_SUCCESS 0x8600073B<br>+10s broadcast 0x8800003d img 0x280011a0<br>+7 s: 81 42 1d 00 00 22 8c 13 00 00 00 00 00 00 (token 5004)<br>On C>S 81 91 8c 13 00 00: 81 99 'Firearm Training' (6700, 1520, -12300) identity quat + white out<br>Alex spawns about 1 s after<br>+5 s after teleport: 81 45 CONNING_DIALOG 0x8600096e | 12 s after the CONNING broadcast (script); observed 29-30 s after goal 25 | - | main 24227>24852 (30s); uriah 29; unknown 30; syntax 30 |
| 27 | FIREARM_COMBAT | Click on a target, and click on the gun icon in the Free Fire dropdown on the hotbar to fire continuously. | narr CONNING_SUCCESS 0x8600073F<br>+5s broadcast 0x88000026 img 0x280011a0 | Alex dies: 81 9d then 80 a0 1b flags=1. Mission data | - | main 24852>27635 (93s); uriah 22; unknown 24; syntax 35 |
| 28 | UPLOAD | Double click on the Hardline to Upload your codes and mission items. All other items must stay in the Matrix. | narr FIREARM_COMBAT_SUCCESS 0x86000740<br>+10s broadcast 0x88000029 img 0x280011a0<br>+7 s (6 observed): 81 42 1d 00 00 22 8d 13 00 00 00 00 00 00 (token 5005)<br>On C>S 81 91 8d 13 00 00: 81 99 'Exit' (6950, 220, -11100) identity quat + white out<br>+5 s after teleport: 81 45 UPLOAD_DIALOG 0x86000964 | Hardline upload. Slot-0 runs used the spawned booth, main used the static id `80 c7 <booth view> <booth spawn> 06 00, or 80 c8 4d 00 <slot<<4> 00 06 00` | own view: level 2 (HP 200, IS 103), see ProgressionPackets<br>2e string 0x2e000064 'Character data for %s has been written'<br>5f for each Letter slot<br>81 18 01 00 00 00 00 00 | main 27635>28884 (103s); uriah 26; unknown 19; syntax 16 |
| 29 | LEVELUP | Review your status panel. Leveling up gives you more Memory Capacity for Abilities and allows you to improve your Attributes. | show STATUS(0x3)<br>narr LEVELUP_DIALOG 0x86000746<br>+5s broadcast 0x8800002a img 0x280011a0<br>80 e6 f5 01 00 00 03 00 00 00<br>80 e5 f5 01 00 00 03 00 00 00 (grantExperience 501), between the toggle and the narration<br>after 80 a0 1d flags=0: 2e 17 ... 89 00 00 60 ... (ID_LEVEL_UP_FEEDBACK) | OnToggledViewActivated `81 04 03 00 00 00 00 00 (STATUS)` | - | main 28884>29112 (53s); uriah 12; unknown 18; syntax never closed |
| 30 | OPENLEVEL | Open up the level up panel. Raise your upgradeable Abilities to maximum level. | narr LEVELUP2_DIALOG 0x86000748<br>+5s broadcast 0x8800003e img 0x280011a0 | OnToggledViewActivated (value 0x0c observed) `81 04 0c 00 00 00 00 00 (LEVEL_UP)` | - | main 29112>29180 (7s); uriah 8; unknown closed without 80 a0 flags=0 (completion was cached); syntax - |
| 31 | SELECTAWAKENED | Select Awakened | narr LEVELUP_ABILITY_DIALOG 0x86000C25<br>+5s broadcast 0x8800003f img 0x280011a0 | OnToggledViewActivated `81 04 1d 00 00 00 00 00 (FIRST_ABILITY)` | - | main 29180>29272 (11s); uriah 1; unknown closed without 80 a0 flags=0 (completion was cached); syntax - |
| 32 | ADVANCEAWAKENED | Increase Awakened to Level Two. Spend Information to improving your upgradeable Abilities. | narr LEVELUP_ABILITY2_DIALOG 0x86000C26<br>+5s broadcast 0x88000040 img 0x280011a0 | OnToggledViewActivated `81 04 1e 00 00 00 00 00 (ADVANCE_LEVEL)` | - | main 29272>29349 (13s); uriah 3; unknown closed without 80 a0 flags=0 (completion was cached); syntax - |
| 33 | APPLY_LEVELUP | Select the Confirm button to confirm your investment in Awakened. | narr LEVELUP_ABILITY3_DIALOG 0x86000C29<br>+5s broadcast 0x88000041 img 0x280011a0 | OnToggledViewActivated; 80 b7 is the real upgrade (ability 2 Awakened, one level) `80 b7 02 00 00 00 01 + 81 04 1f 00 00 00 00 00 (APPLY_ADVANCE)` | 2e 0x2e000064<br>80 e7 e0 fc ff ff ff ff ff ff 11 00 00 00 (info -800)<br>80 e4 c8 00 00 00 11 00 00 00 (info 200)<br>80 b3 11 00; 80 b2 11 00 02 00 08 02; 80 b8 02 00 02 00 00 00 00 00 | main 29349>29389 (4s); uriah 9; unknown closed without 80 a0 flags=0 (completion was cached); syntax - |
| 34 | OPEN_ATTRIBUTE | Open the attribute level up panel. This is where you improve your character's Attributes. | narr LEVELUP_ATTRIBUTE_DIALOG 0x86000C2B<br>+5s broadcast 0x8800004c img 0x280011a0 | OnToggledViewActivated `81 04 20 00 00 00 00 00 (OPEN_ATTRIBUTE)` | - | main 29389>29540 (10s); uriah 2; unknown closed without 80 a0 flags=0 (completion was cached); syntax - |
| 35 | UPGRADE_ATTRIBUTE | Increase an attribute to level two | narr LEVELUP_ATTRIBUTE2_DIALOG 0x86000C2D<br>+5s broadcast 0x8800004d img 0x280011a0 | OnToggledViewActivated `81 04 21 00 00 00 00 00 (UPGRADE_ATTRIBUTE)` | - | main 29540>29634 (18s); uriah 4; unknown closed without 80 a0 flags=0 (completion was cached); syntax - |
| 36 | CONFIRM_ATTRIBUTE | Confirm your attribute investment | narr LEVELUP_ATTRIBUTE3_DIALOG 0x86000C2E<br>+5s broadcast 0x88000053 img 0x280011a0 | OnToggledViewActivated; 80 b5 is the real attribute raise (0x52 in main, 0x4e in syntax) `80 b5 04 00 01 00 00 52 00 00 00 01 + 81 04 22 00 00 00 00 00 (CONFIRM_ATTRIBUTE)` | 2e 0x2e000064<br>80 b6 08 00 00 00 00 00 01 00 00 52 00 00 00 09 (attribute 0x52 now 9) | main 29634>29660 (1s); uriah 10; unknown closed without 80 a0 flags=0 (completion was cached); syntax - |
| 37 | EXIT | Exit the tutorial | narr EXIT_DIALOG 0x86000825<br>+5s broadcast 0x8800002b img 0x280011a0 | Jack out at TutorialExitPoint_PhoneBooth. Mission data `80 c7 <booth view> <booth spawn> 05 00` | 80 b3 11 00; 80 b3 bb 01; 80 b3 cc 01 + 80 bc buff removals; 81 01 (empty), BEFORE 80 a0 25 flags=1<br>81 45 EXIT_SUCCESS 0x8600074b<br>Then the exit handshake, section 3.3 | main 29660>29873 (25s); uriah 12; unknown 13; syntax - |

### 4.4 The respawn loop (goals 23 to 25)

[O main:19415-24227]. Goal 25 stays open throughout.

| Step | Wire | Line |
|---|---|---|
| Client picks "Again!" | `81 90 19 00 00 00 01` | 19415 |
| Goal 23 reopened, narration | `80 a0 17 06 00 00 01 00 00`, `81 45 33 07 00 86` | 19420 |
| New Bob, new spawn id and view, at home | `0c 57 02 37 cd ab ...` | 19430 |
| Kill: goal 23 closes (full text), modal again, COMBAT_RESPAWN | `81 9d`, `80 a0 17 06 00 01 ...`, `81 8f ... 19 00 00 00 ...`, `81 45 2f 0c 00 86` | 21760 |
| Corpse can be looted again for the same items | `81 19`, `81 1f`, `5e`, `81 89`, `81 20` | 22442-22784 |
| "Move on." | `81 90 19 00 00 00 00` | 24222 |
| Goal 25 closes | `80 a0 19 06 00 01 ...`, `81 45 3b 07 00 86`, `80 a0 1a 06 00 00 ...` | 24227 |

Script order for "Move on." is at lines 295-301. No respawn in uriah, unknown or syntax: they answered 0 first.

The first time through, goal 24 closes with the Take All. If goal 24 is already complete when Bob dies, `OnGoalComplete(23)` skips the loot prompt and goes straight to the modal [S lines 165-169, O main:21760].

## 5. Objects

### 5.1 Static versus spawned

The `.metr` could not be used to settle this. `tutorial_v2.metr` is only the sector table (`METR`, `SCIN`, `DSHD`, `DSST`), and none of the object coordinates occur in it, in the two `.cooked_*` files, or in the `worlds_*`, `missions*` and `gameobjects` packs in either float width or plain form [O `metr.py`, `packfind.py`]. The sector files were not found. So the evidence is the wire:

- **Server spawned, no static id** (`0c <goid> <spawn> cd ab`): Ammit, Scarlett, Bob, Alex, TeamInfo, ILCombatHandler.
- **Server spawned, bound to a static id** (`08 <goid u16> <static u32> <spawn> cd ab`): Wardrobe 0x6a, Door 0x51, Booth 0x4d, chair 0x49 and 0x4a. [I] these exist in the world file too: the client addressed 0x4d, 0x4a and 0x49 by static id with `80 c8` before any spawn for them [O main:28874, unknown:3888, syntax:7138].

Only one wardrobe is ever spawned although the goal says "wardrobes" [O, all 4 runs]. Nobody tried another; the client's reaction to using an unspawned one is unknown [?].

### 5.2 NPCs

All goid 599 NPC_BASE; rsi = the `Description` attribute, the project's rsi= form. All have CharacterName, TitleAbility 0x00001000, CombatantMode 0x22, StopFollowActiveTracker 1, DontAttack 1, TalkDefaultable 1, CurrentState 0x7fe. Positions wander; only Bob's respawn gives a clean home point.

| NPC | Level | HP | rsi LE bytes | Rank | Other attributes | Key byte 0 | Spawned | Wander box seen (x; z; y) |
|---|---|---|---|---|---|---|---|---|
| Ammit | 50 | 2600/2600 | 86 01 00 58 | 1 | TalkActiveTracker 1, DebuffState 4 | 00 | world entry [main:511] | 9011..9540; -17184..-16370; 295 |
| Scarlett | 1 | 75/75 | 89 09 00 58 | 3 | TalkActiveTracker 1, DebuffState 4 | 01 | Gallery arrival [main:2979] | 3750..4481; -17104..-16555; 495 |
| Bob | 2 | none sent. 79 after a 21 hit, so [I] 100 | 37 05 00 58 | 3 | IsEnemy 1, UseCount 2, DebuffState 9, Action and NoiseLevel vary | 02 | interest ring, about 2450 away in xz, player still in the ante [main:13168] | home **5800, -1105, -10300** [main:19430 minus offset]; 5094..6799; -11211..-9467 |
| Alex | 1 | 75/75 | ec 02 00 58 | 3 | DebuffState 4, no IsEnemy, Action 4 or 5 | 00 | Firearm arrival [main:24384] | 7350..7614; -12218..-11550; 1595 |

Notes:

- The MissionKey on each = `<key byte 0> <instance u16> <keyByte>`. [I] byte 0 indexes a mission element; Scarlett 01, Bob 02, Wardrobe 05, Door 06 hold across runs.
- Bob's combat AI, Alex shooting back at the player, tactics and damage already exist in `CombatHandler`, `MobPackets` and `CombatPackets`, built from these same logs. HP 79 appears first in main:18022, uriah:10768, unknown:9103. Alex drops 19 a hit [O main:26937, main:27278].
- Bob's loot in all runs: Revolver0 (2565) and Letter (6793).
- Talk reply text [O]: Ammit "Listen to what the Captain has to say. And be honest."; Scarlett "Hello there. I've hidden a key, but apparently too well. Can you help me?"; Bob and Alex none.
- Talk reply wire: `2e 05 00 00 00 00 00 24 00 00 00 <text offset u16> <zeros> <sized name> <sized text>` then `80 a1 1f 12 00 28 00 00 00 00 14 00 00 00 00 00 <text offset u16> 01 00 <sized name> <sized text>` [O main:1843]. The chat line text offset (0x2c Ammit, 0x2f Scarlett) tracks the name length.
- On talk the NPC's view also gets a long update starting `06 2e 01 40 00 25 ...` [O main:1843, 7920]. Not decoded; whether the client needs it is [?].

**Raw creation, Ammit**, main:511 (slot 1, so position is offset; spawn 0x2b, view 3):

```
0c 57 02 2b cd ab 10 8e 41 6d 6d 69 74 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00
00 00 00 00 00 00 00 10 00 00 22 c0 01 80 c9 86 01 00 58 01 00 00 00 00 00 ef d5 c0 00 00 00 00 00
70 72 40 00 00 00 d8 15 e8 e6 40 88 00 d7 01 02 82 01 a8 32 28 0a b2 04 28 0a fe 07 00 00 88 01 01
01 03 00 00
```

Decode (`npc.py` table; mask byte, then that group's attributes, top bit = another group follows):

| Bytes | Meaning |
|---|---|
| `0c 57 02 2b cd ab` | create goid 0x0257, spawn 0x2b |
| `10` | 16 attributes |
| `8e` + `"Ammit"` padded to 32, `00 10 00 00`, `22` | Group 0 mask, CharacterName, TitleAbility, CombatantMode |
| `c0 01` | StopFollowActiveTracker 1 |
| `80` | no group 2 attributes |
| `c9 86 01 00 58 01 <pos f64 x3>` | Description, DontAttack, Position |
| `88 00 d7 01 02` | MissionKey |
| `82 01` | NPCRank 1 |
| `a8 32 28 0a` | Level 50, Health 2600 |
| `b2 04 28 0a fe 07 00 00` | DebuffState 4, MaxHealth 2600, CurrentState 0x7fe |
| `88 01 01 01` | TalkDefaultable 1, TalkActiveTracker 1 |
| `03 00 00` | view id 3, end of create |

Bob's differs (main:13168): no Level-50 fields, `IsEnemy`, `UseCount 2`, no Health or MaxHealth. Raw is in section 8.

### 5.3 Visibility

[O `dist.py`] Dynamic objects follow an interest ring of roughly 2500 in xz. Bob appears at 2412 to 2518 and goes at 2518 to 3210 from where he was created; the Door goes at about 2300 to 2800 and returns at 2300 to 2570 (uriah:5038-7583, 11086-11158). A reappearance can take a different view id. The spawn id stays for the same life (Bob 240 across four uriah re-creates, Door 52 in main) and changes for a new life (main's Bobs: 53, 55, 57). Delete wire: `01 00 01 01 00 <view u16> 00 00` [O main:2721]. The Bob corpse was not seen deleted in main, and goes when the player teleports out [I].

Ammit is deleted about 5 s after the Interrogation teleport, once she is out of range [O main:2721].

### 5.4 Interactive objects

| Object | Goid | Static id | Position; quaternion | Create attributes | Use (C>S) | Reply |
|---|---|---|---|---|---|---|
| Wardrobe | 7538 Wardrobe | 0x6a | 5450, 522.5, -16500; 0,-0.7071,0,0.7071 | UseCount 3, MissionKey 05.., CurrentState 0x836 | `80 c7 v s 03 00` | State updates and `81 19`. See below |
| Door | 6994 Door DT 2Push Knob darkwood01 | 0x51 | 5800, -1055, -12600 | MissionKey 06.., CurrentState 0x836 | `80 c7 v s 03 00` | `01 80 02 34 08 00 00`; +20 s 0x838; +1 s 0x835 [O main:13371, 14309, 14319; uriah:7568, 9203, 9313; unknown:8112, 9066, 9083; syntax:14023, 14685, 14757]. Re-create after that has MissionKey 00.. and no CurrentState [O main:23254] |
| Hardline booth | 2412 TutorialExitPoint_PhoneBooth | 0x4d | 6950, 331, -8550; 0,1,0,-4.37e-8 | Position, Orientation only | `80 c7 v s 06 00` upload, `80 c7 v s 05 00` exit, or `80 c8 4d 00 ss 00 06 00` | Goal 28 wire; section 3.3 |
| Chair | 38338 Stance Site Invis Casual Chair | 0x4a, 0x49 | 7295, 3945, -15710; 0,-0.9848,0,0.1736 (0x4a). 7105, 3945, -15710 (0x49) | Position, Orientation, SiteInUse 1 | `80 c8 4a 00 00 00 00 00` | Create with SiteInUse 1 about 1 s later [O unknown:3888-3992, syntax:7138-7248]. Optional: sitting in the red room |

**Wardrobe state machine** [O main; same in uriah, unknown, syntax]:

| Trigger | State update on its view | Other | Line |
|---|---|---|---|
| Gallery arrival | create with 0x836 | | 2979 |
| Goal 7 opens (talk to Scarlett) | `01 20 35 08 00 00` | | 7920 |
| Use | `01 20 34 08 00 00` | `81 19` list | 8458, 8468 |
| Close `81 1a` | `01 20 35 08 00 00` | `81 1a` echo | 8527 |
| Use again | `01 20 34 08 00 00` | `81 19` | 8564 |
| After looted, use | `01 20 34 08 00 00` | `81 19 ... 05 <instance> <key> 00 00`, empty list | uriah:3406 |

Update byte `20` is update index 5 = CurrentState per `AttributeClass369`. The Door's `80 02` is index 8 per `AttributeClass363`. [?] Whether the door is unlocked by event 2: its state stays 0x836 until used, and no pre-use state change exists. So the door accepted use from state 0x836 in all 4 runs.

**Loot `81 19`**: StringID u32 (0x76000287 ID_WARDROBE, 0 on a corpse), array offset u16 (0x10), InfoAmount u32 (0), MissionKey `05 <instance u16> <key>`, count u16, then (InstanceData u32, TypeID u32). **Items**: Door_Key instance data = `01 <instance u16> <0x80|keyByte>`; Letter = `cb <instance u16> <0x80|keyByte>`; shoes and gun `00 00 00 14`.

## 6. What differs across the 10 runs

Constant [O]:

- Goal texts, narration ids, broadcast ids and images, FX and tokens, all timers.
- Teleport points, Wardrobe, Door, Booth positions and states, NPC stats, rsi, loot contents and order.
- The modal hex, the panel toggle set.
- Item slots: 2, 3, 4 for the shoes and key; 4 and 5 for the gun and letter once the key has gone.
- `63 02 6b` for the equip, every time.

Different [O]:

| Thing | Values |
|---|---|
| Instance and key byte | Per run. `00 d7 01 02`, `00 26 03 03`, ... |
| ConstructID | 0x322 (7), 0x321 (3) |
| Slot | 1 in main, 0 elsewhere |
| 80 9c position in the toggle run | Varies (section 3.2) |
| View and spawn ids | Assigned per session. TeamInfo and Ammit take views 3 and 5, in either order |
| NPC first position, yaw, Action | Wander state at spawn time |
| Revolver instance data | `00 00 00 10` in main, `00 00 00 14` elsewhere |
| Kills in 80 a2 | 4 in main (Bob 3x, Alex), 2 elsewhere |
| Attribute raised | 0x52 main, 0x4e syntax. Player's choice |
| Order and caching | uriah and syntax talk to Ammit before goal 2 opens; unknown clicks level-up panels before goal 29 closes |

**Male versus female shoes do not vary.** [O uriah:3227, main:8468, unknown:4945, syntax:8851] The wardrobe always holds Mclothing 1258, Fclothing 905 and the key, whatever the character. The four complete characters all dragged slot 2 (1258). `norajob` is the female run but aborted before the wardrobe, so **a female character equipping 905 was never captured**; slot 3 and script line 733 imply that it completes the same way [I].

No blue pill in any run. All 10 mission template lists are just [986]. `80 a3` has no text in any run.

## 7. Unknowns

1. **Goal 21's exact trigger** [I]. Fits entering zone 5 past z = -12600, within a second. The zone test itself could not be read from the world files. A position check is a safe stand-in. `OnPlayerEnterZone` in the script is empty, so this is engine data.
2. **What `81 91` events 0, 1, 2 do in the client** [?]. Sent exactly as listed. Event 2 is followed by the five tactic shortcuts. Possibly UI unlocks.
3. **Whether TeamInfo (2584), `80 8d`, the talk NPC update and the unscripted `80 b2 bb 01` are required** [?]. Send them; drop them only if testing shows no effect. Goids 2412 and 2584 are missing from `GameObjectDefinitions.cs`, so they need adding (booth creation mask `02 05`: Position, Orientation).
4. **Blue pill** [S only]: BLUEPILL_CONFIRM, modal 7000 with `0x8800002c`, then a boot 5 s later. `bootPlayer`'s wire is unknown.
5. **Consolevar warp** (`TutorialWarpFactor`): script only.
6. **Self-view attribute indices** around the MissionKey at mission start (bytes `ff bf 01 00 02` before it) [?]. `BuildMissionKeyUpdate` already sets the key correctly; the rest was not decoded.
7. **The `80 a2` u32 after team kills** (`4e dd 8e b5`, varying around 0xb58xxxxx) [?]. Not seconds. The emulator already sends a fixed value.
8. **Chair** in uriah:1968 appears without a captured `80 c8`, right after the teleport, where main has no chair at all.
9. **Booth timing**: main spawned it only after the upload (main:28920); slot-0 runs had it from arrival, at about 2450 (uriah:14804). Handle both `80 c7` and `80 c8`.
10. **Static world contents** beyond the objects named: the sector data was not readable.
11. **Goal 8's reminder** references an undefined `sender` (script line 500), which would raise in retail. No capture stayed on goal 8 long enough to show what happened: main's lasted 21 s against a 62 s first reminder.
12. **Time resolution**: logs have whole seconds, so any delay here is plus or minus 1 s.

## 8. Reference hex

Bob, main:13168 (slot 1):

```
0c 57 02 35 cd ab 12 8e 42 6f 62 00 ...(name to 32)... 00 10 00 00 22 c4 99 01 80 d9 37 05 00 58 01 0a
00 00 00 40 e1 be d9 c0 00 00 00 00 00 44 91 c0 00 00 00 20 3b 3a ea 40 cc 02 02 d7 01 02 01 83 01 03
88 02 a2 09 fe 07 00 00 08 01 08 00 00
```

Scarlett and Wardrobe, main:2979 (slot 1):

```
0c 57 02 2e cd ab 11 8e 53 63 61 72 6c 65 74 74 00 ...(name to 32)... 00 10 00 00 22 c4 09 01 80 c9 89 09
00 58 01 00 00 00 00 80 96 db c0 00 00 00 00 00 f0 7e 40 00 00 00 00 60 f0 e6 40 88 01 d7 01 02 82 03 a8
01 4b 00 b2 04 4b 00 fe 07 00 00 88 01 01 01 04 00 00
01 00 08 72 1d 6a 00 10 00 2f cd ab 05 c6 03 00 00 00 00 f2 04 35 bf 00 00 00 00 f3 04 35 3f 00 00 00 00
80 ed d9 c0 00 00 00 00 00 54 80 40 00 00 00 00 80 31 e7 40 24 05 d7 01 02 36 08 00 00 06 00 00
```

Door, main:12017: `08 52 1b 51 00 10 00 34 cd ab 03 80 a2 00 00 00 00 00 96 d9 c0 00 00 00 00 00 7c 90 c0 00 00 00 00 00 19 e9 40 06 d7 01 02 01 36 08 00 00 07 00 00`

Booth, main:28920: `08 6c 09 4d 00 10 00 3c cd ab 02 05 00 00 00 00 80 76 d8 c0 00 00 00 00 00 b0 74 40 00 00 00 00 40 13 eb 40 00 00 00 00 00 00 80 3f 00 00 00 00 2e bd 3b b3 0c 00 00`

Alex, main:24384: `0c 57 02 3b cd ab 11 8e 41 6c 65 78 00 ...(name to 32)... 00 10 00 00 22 c4 35 01 80 c9 ec 02 00 58 01 00 00 00 00 80 12 d8 c0 00 00 00 00 00 ec 98 40 00 00 00 00 40 9c e9 40 88 00 d7 01 02 83 04 03 a8 01 4b 00 b2 04 4b 00 fe 07 00 00 08 01 04 00 00`

TeamInfo, main:511: `0c 18 0a 2a cd ab 01 01 01 00 00 00 05 00 00`

Tools, all in this folder (run with `python -I`): `tl.py` (log names), `pretty.py <run> <from> <to> [x]` (decoded timeline), `dump.py`, `dump_rpc.py`, `allobjs.py`, `objs.py`, `viewtrace.py`, `trig.py`, `goals_cmp.py`, `cmp2.py`, `inst.py`, `misc.py`, `pos.py`, `dist.py`, `metr.py`, `packfind.py`, `gen.py`. Decoded timelines: `main_p.txt`, `uriah_p.txt`, `unknown_p.txt`, `syntax_p.txt`.
