# Missions

How missions run, the file format in `data/missions/`, and what was recovered from where.

## How retail worked

- **Critical (story) missions were hand-written.** A developer describes writing them one at a time. IDs run chapter.sub.number, and the client's own labels count 1.1 = 3, 1.2 = 17, 1.3 = 6, then 5 each through 11.2, for about 171 per organisation.
- **Standard missions look assembled from templates** (Assassination, Courier, Retrieval, Rescue, Escort, Infiltration, Recruitment): the same template shows up with different NPCs and buildings. No developer confirms a generator, so that is inferred. None is built yet.
- **The server held the definitions.** The client has no mission data beyond its two optional scripts (`Mission185`, `Mission986`), so everything here is rebuilt from what was sent to players.

## What a mission does

The client handles most of the display. Our server only has to send the right records:

| Step | Record | Notes |
|---|---|---|
| List | `80 95`, `80 96`, `80 97` | The plot flag in `80 96` files it under "Critical Missions" |
| Details | `80 99` | Text and the distances to the first and last locations |
| Accept | `80 9c`, own-view MissionKey | |
| Each phase | `80 a3` briefing, `80 9e` waypoints, `80 a0` goals | The waypoint names a door's static object |
| Lines | `80 a1` | Operator, contact or NPC |
| Goal done | `80 a0` flag 1 | The client removes it from the Objectives window |
| Phase done | `80 a1` string `0x2600000c` or `0x26000004` | "Goals for this area are complete. Find the nearest exit quickly!", or "Goals for this mission phase are complete!" when the player needn't leave (see [Ending a phase](#ending-a-phase)) |
| End | `80 a2`, then XP and info | The client shows the summary screen and adds "Leave the area now!" itself |

Three things the client depends on, found by trying them:

- **Get** appears in an NPC's menu only with `GiverActiveTracker` set on it. No retail record we could find sets it, so it's our fit.
- **Give** appears only while the player holds an item whose 4-byte descriptor is the NPC's MissionKey with `0x80` on the last byte. Retail's statue for Gagarin (key `01 9a 02 02`) was `5e 07 00 f2 04 00 00 01 9a 02 82 01`. We also set `TakerActiveTracker`.
- **Leaving** isn't defined anywhere. In the captures the next step came when the player was 2 to 5 m past a mission door, so `MissionExit` uses 6 m from a door, or 60 m from all of them for leaving another way.

The two points in `80 a3` are street-level entrances to the mission area; the operator's "you've arrived" line fires near the first.

## Mission files

`data/missions/*.xml`, one mission per file. Positions are world units, as the captures carry them.

```xml
<mission id="1203" title="The Internuncio" description="..." plot="1" order="3" exp="33000" info="42000">
  <npc id="1" handle="Campbell" type="FRIENDLY" x="58379" y="1095" z="12850" rsi="58010058" level="5" maxHP="350" weapon="2568" />
  <npc id="8" handle="Zion Soldier" type="FRIENDLY" ...>
    <say display="5">Have you spoken to Gagarin yet?</say>
    <say display="5" after="0">Maybe we'll get to work together in the future.</say>
  </npc>
  <phase>
    <briefing header="30000100450504000000000000003c00" a="59700,-55,10700" b="59700,-55,11300">Text, with {player}.</briefing>
    <waypoint x="59700" y="1145" z="11300" door="c9005054" />
    <objective goal="1" text="Get the Statue from Campbell" type="Get" npc="1" item="1266">
      <talk><say npc="1">Said when talked to while this is open.</say></talk>
      <done><say>The operator, when it completes.</say></done>
    </objective>
    <near x="59700" y="-55" z="10700" radius="600"><say>Fires once, on walking here.</say></near>
    <complete><say>When all of this phase's goals are done.</say></complete>
  </phase>
  <finish>The contact's closing line.</finish>
</mission>
```

- `type`: `Talk`, `Defeat`, `Loot`, `Get`, `Give`.
- `goal` is retail's id, which isn't always the order they open in. `after="2"` opens a goal once goal 2 is done; otherwise it's open from the start of the phase, and they can be done in any order.
- Kill goals (`Defeat`, `DefeatAll`) can wait too. A kill made before the goal opens isn't lost: when the goal opens, a `Defeat` whose NPC is already dead, or a `DefeatAll` with nobody left, is done at once. So is a `Talk` that finds a hostile ("Find Burdette") if the player kills it before reaching it. Where the target shouldn't be there before the goal opens, have it arrive then (`spawnAfter`, below).
- `<say>` with no attribute is the operator, `npc="n"` that NPC, `contact="1"` the mission contact.
- An NPC's own `<say after="n">` is said when it is talked to with no goal open for it, once goal n is done in any phase.
  So that an earlier phase's goal can't set it off, a goal it waits on must not share its id with a goal of an earlier
  phase. The story files number their goals through the whole mission wherever an NPC line waits on one
  (`StoryMissionTests` checks).
- On an NPC: `attacksOnSight="0"` waits for the player to start the fight; `dead="1"` is a corpse; `phase` is the first byte of its MissionKey.
- On an NPC, `spawnAfter="3"` makes it arrive when goal 3 completes, instead of standing there from the start: reinforcements, an Agent, someone who "just appears". The goal is one of phase `at`'s for an NPC placed in a generated site, and for an NPC placed by x, y, z the first goal 3 to complete in any phase. It appears where it would have stood (the site spot kept for it, or its x, y, z), 3 m from the player with `spawnAt="player"` (also when it has no place), or 3 m from one of the phase's objects with `spawnAt="object:1"`. Its talk lines, `<near npc>` triggers and goals work from the moment it arrives; until then a `<near npc>` trigger on it doesn't fire, and a `DefeatAll` waits for it unless its goal is optional. An NPC can also arrive on the area, a place or a timer (`spawnAfter="enter"`, `spawnNear`, `spawnDelay`): see [Arrivals](#arrivals).

```xml
<npc id="3" handle="Security" type="HOSTILE" at="3" rsi="35000024" spawnAfter="3" spawnAt="player" />
<phase site="Normal" enemy="Machines" enemies="2" enemiesAfter="3">
  <objective goal="3" text="Talk to Tarka" type="Talk" npc="2" />
  <objective goal="4" text="Kill Security" type="Defeat" npc="3" after="3" />
</phase>
```
- `order` is its place in the contact's story chain: a contact offers the first unfinished one, then all of them to replay. Finished missions are in the `char_missions` table.
- `org` is the organisation whose contact gives it (1 Zion: Tyndall, 2 Machines: Agent Gray, 3 Merovingian: Flood; see Contacts below); 0 or none is Tyndall's introduction. The client's mission list request (`80 94 <txn> <org>`) carries the contact's org, and it only shows a list whose header names that contact. `contact` names who speaks the mission's contact lines.

### Generated phases

Mission NPCs are placed on the shipped navmesh, including player/object arrivals, generated waves, and
side-change respawns. Indoor polygons are transformed through their prefab pivot and quarter turns.
The placer searches up to 8 m on the requested floor, inset 20 units (0.2 m) from polygon edges where
the polygon is wide enough. It uses the player's connected polygon component when that floor and
local route are known. Player-relative arrivals also require a straight route covered by walkable
polygons, so a three-metre offset cannot put a courier in the next room through a wall. Object-relative
arrivals may use a connected route around a corner. These distances and the inset are implementation
choices. The existing 200-unit floor lookup tolerance distinguishes the client's normal 400-unit floors.

NPC height is the polygon's floor plus 95, as the client's position records use. Object origins on
desks do not become NPC heights. If the requested point cannot be projected, the placer tries the
site's NPC spots, also projected onto walkable floor. A known player-floor position is the last local
fallback. Without a safe point the arrival waits and retries. A wave is planned before any of its
members spawn, and kill-all goals wait for failed placements. A side change retains a walkable
position and the NPC's health. Follower catch-up placements use the same floor projection.

Site NPCs stay inside the allocated area's bounds. Before the player arrives, the area's own floor
anchors the connected component. Projected positions are reserved with one metre of separation,
including earlier members of a planned wave; if offsets converge at a wall, a local search tries
nearby walkable positions before another site spot. Player-relative arrivals can follow the player
outside that rectangle. These spacing distances are implementation choices.

[Native QA on 2026-11-11](MISSION-ARRIVAL-QA.md) checked wall-edge player arrivals, both side changes,
hold-out completion and deadline failure. It also exposed a combat animation fault outside the
placement code. Timed goals show objective text but still have no countdown.

A phase with `site` is placed when it starts instead of at fixed positions. The server picks a floor of a building from the world's own mission markup (see below), puts the phase's NPCs on its NPC spawners, spawns its hostiles and objects, and makes the floor's elevator door the waypoint.

```xml
<mission id="2101" title="..." description="..." plot="1" order="11" org="3" contact="Flood" exp="22000" info="42000">
  <npc id="1" handle="Mizar" type="FRIENDLY" at="1" rsi="89090058" weapon="2564" />
  <npc id="2" handle="Checksum" type="FRIENDLY" at="2" rsi="58010058" />
  <phase site="Normal" enemy="Machines" enemies="3">
    <briefing>When the phase starts, as before. No header, a or b: they come from the site.</briefing>
    <enter><say>When the player reaches the site's floor.</say></enter>
    <object id="1" kind="Computer" />
    <objective goal="1" text="Talk to Mizar" type="Talk" npc="1" />
    <objective goal="2" text="Lead Mizar out of the area" type="Escort" npc="1" to="exit" after="1" />
    <objective goal="3" text="Do not let Mizar die" type="Protect" npc="1" />
    <objective goal="4" text="Search the computer" type="Search" object="1" item="6831" />
  </phase>
</mission>
```

- `site`: `Normal` (any ordinary building: SlumApt, GenericApt, Office, Derelict, Warehouse), `Any`, or one style: `SlumApt`, `GenericApt`, `Office`, `RedPillExtraction`, `ZionOutpost`, `Derelict`, `Warehouse`, `Bank`, `Hotel`, `Skyscraper`.
- `enemy` (`Machines`, `Merovingian` or `Zion`) and `enemies`: hostiles spawned at the player's level, which attack on sight. With `enemiesAfter="3"` they arrive on their site spots when goal 3 completes, instead of when the phase starts, or beside the player or an object with `enemiesAt="player"` or `enemiesAt="object:1"`. More groups, each with its own arrival, are `<wave>`s (see [Waves](#waves)).
- On an NPC, `at="n"` places it in phase n's site when that phase starts. Without `at` it needs x, y, z and spawns at accept. Leave `level` out to match the player's level.
- `<object id kind>`: a `Computer`, `Safe` or `Mainframe` already on the floor is used if there is one; otherwise one is spawned at the floor's spawner for that kind, or a `Box` at a box spawner. `Furniture` (a filing cabinet, desk or dresser) and `Phone` are bound to one in place.
  - **Retail did the same.** It bound a spawned object to a static only when the static was the same object (`08 <goid> <static>`: The Internuncio's filing cabinets, dressers). At a spawner it spawned the object free (`0c`, no static): the client's definitions give spawner markers no Info or Usable part. Mission objects carry the key `ff <instance> 02` and use count 1, as retail's wall safes and filing cabinets did.
  - **Clicking them.** The player selects and uses them as in retail: the client sends `80 c7 <view> <spawn> <use>`, use 3 for a container's Open, 4 for Hack, 5 for Take. The client checks the distance itself ("Target is out of range") and its reach is short, about a step. Containers (safes, cabinets, desks, dressers, boxes) can be opened in their default state, 0x835.
  - **Computers need a state.** In its default state (0x960) a computer can't be selected. The client's `gameobjects.gob` gives computers states 0x960 to 0x965; tried in the client, 0x961 offers Take, 0x963 to 0x965 offer Hack, and 0x960 and 0x962 nothing. A computer is spawned in 0x961 for a Search goal that gives an item and 0x963 for other goals, and goes back to 0x960 when it has no open goal (update mask `01 08`). No state offered Put, so a Put goal at a computer is done with Hack.
  - **Locked.** `locked="1"` spawns a safe, cabinet, desk or box in its LockedState (0x836): the player must Pick Lock it with Open Locks and a lockpick first (rule 4000; see [UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)), then it opens as usual.
  - **The phone** isn't known to be usable, so its goals are done by standing beside it (2.5 m).

Goal types beyond the five above:

| Type | Attributes | Done when |
|---|---|---|
| `DefeatAll` | `wave` (optional: `wave="0"`, `wave="1,2"`) | every hostile of the phase is dead, or with `wave`, every hostile of those waves (see [Waves](#waves)) |
| `Escort` | `npc` (one id, or several: `npc="2,3"`), `to` | the NPCs follow the player from when they're reached or talked to; done at the way out (`to="exit"`), beside another NPC (`to="npc:4"`), beside one of the phase's objects (`to="object:1"`: the player and each NPC within 7 m of it, as "Lead Tarka to the Mainframe"), or on using a hardline (`to="hardline"`) |
| `Protect` | `npc` | the mission fails if the NPC dies; done with the phase's other goals |
| `Search` | `object`, `item` | a container opens a loot window holding the item; a computer's Take or Hack gives it straight away |
| `Put` | `object`, `item` | using the object while carrying the item |
| `Use` | `object` | using the object |
| `Upload` | `item` | uploading at a hardline while carrying it |
| `Survive` | `seconds` | that many seconds after it opens: a hold-out. A goal open from the start of a generated phase counts from reaching the area |

`optional="1"` on any goal lets the phase end without it. `limit="120"` on any goal fails the mission if it isn't done 120 seconds after it opens (counted as for `Survive`); the operator says "You ran out of time." No timer is shown: the client's mission timer isn't known, so say the limit in the goal's text or a line.

### Waves

A phase's own `enemy` and `enemies` are its wave 0. `<wave>` adds more groups of generated hostiles, each with its own arrival, place and kill-all scope:

```xml
<phase site="Normal" enemy="Zion" enemies="3">
  <object id="2" kind="Computer" />
  <wave enemy="Machines" count="3" after="2" delay="44" spawnAt="player"><say>More hostiles coming in!</say></wave>
  <wave count="2" after="enter" near="object:2" radius="800" />
  <objective goal="1" text="Kill the guards" type="DefeatAll" wave="0" />
  <objective goal="2" text="Activate the targeting system" type="Use" object="2" />
  <objective goal="3" text="Transmit the signal" type="Survive" seconds="195" after="2" />
</phase>
```

- `id` numbers the wave; without it they count 1, 2, ... in order. `enemy` is the phase's unless it says, and `count` is how many (default 1).
- When it comes, as on an NPC (see [Arrivals](#arrivals)): `after` (a goal, or `enter`), `near` with `radius`, and `delay`. A wave with none of them is there when the phase starts.
- Where: on the site's spots (kept for it when the phase starts), or `spawnAt="player"` or `spawnAt="object:2"`, 3 m from it. A phase without a site has no spots, so its waves need `spawnAt`.
- Its `<say>` lines are said as it arrives.
- **Kill-all scope.** A `DefeatAll` with `wave="1,2"` counts only those waves, and named hostiles whose `wave` attribute names one of them (an NPC's wave is 0 unless it says). Without `wave` it counts every hostile of the phase, as before. Either way it doesn't wait for a wave that comes only once it's done: one that waits on the DefeatAll itself or on a goal that opens after it. It waits for one that is on its way (its delay running) or waits on the area, a place or a time.

### Arrivals

Named NPCs and waves arrive on the same conditions. Every one given has to happen, in this order, and the delay counts from the last of them, or from the phase's start:

| On an NPC | On a `<wave>` | Meaning |
|---|---|---|
| `spawnAfter="3"` | `after="3"` | goal 3 completes |
| `spawnAfter="enter"` | `after="enter"` | the player reaches the phase's area: a generated phase's floor, or the phase's start when it has no site |
| `spawnNear="object:1"` and `spawnRadius` | `near` and `radius` | the player comes within `radius` units (default 1000, 10 m) of a phase object, a living NPC (`npc:4`) or a point (`x,y,z`) |
| `spawnDelay="27"` | `delay` | that many seconds later |
| `spawnAt="player"` or `"object:1"` | `spawnAt` | where it appears, as above |

A delay with nothing before it counts from the phase's start, which in a generated phase is when the player is still on the street: use `after="enter"` with it for "half a minute after you get there".

### NPCs that change side

`hostileAfter="4"` on a friendly NPC turns it hostile when goal 4 completes; `friendlyAfter="4"` on a hostile turns it friendly. As with `spawnAfter`, the goal is one of phase `at`'s, or any phase's for an NPC placed by x, y, z. Both can be given (friendly, then hostile, then friendly again). A goal that kills the NPC (`Defeat`, `Loot`) needs it hostile by then, and `Escort`, `Protect` and `Give` need it friendly.

```xml
<npc id="2" handle="Dora" type="FRIENDLY" at="2" rsi="..." hostileAfter="5" />
```

- **How the client is told.** An NPC's side is in its creation record only. A friendly mission NPC has `DontAttack`, `TalkDefaultable` and `TalkActiveTracker` and the mission's key; a hostile has `IsEnemy` (`Mob.getCreationData`). All five attributes have update indices in NPC_BASE's class, but no capture shows retail changing them on a living NPC, and Bob in the tutorial carried both `IsEnemy` and `DontAttack` from his creation. So a switch takes the NPC off the clients (a view delete) and spawns it again where it stands, with its health, as a new entity on its new side. The client sees it vanish and reappear at once. An NPC that turns before it has arrived comes on its new side.
- A friendly NPC that was following the player stops when it turns hostile; it can then be fought, and a `DefeatAll` that is still open counts it. Its lines' portrait follows its side.

### Ending a phase

Retail's operator ended a phase with one of two client strings, never both:

- `0x2600000c` `ID_MISSION_GOALSCOMPLETELEAVE`, "Goals for this area are complete. Find the nearest exit quickly!", when the player still had to leave. The next briefing came once they had.
- `0x26000004` `ID_MISSION_GOALSCOMPLETE`, "Goals for this mission phase are complete!", when they didn't. The next briefing, or the contact's closing line and "Mission Completed!", came in the same second. The transcripts in `research/missions/transcripts/` log 154 of these lines. Each is followed at once by the next briefing or the closing. Typical cases are an upload at a hardline (Merv1-2-3, Zion3-1-1, Merv8-3-4), NPCs who were talked to and then follow into the next phase (Merv3-1-3, Merv11-1-2, Merv7-2-4), and leading an NPC out (Merv11-1-4's Tarka). In Merv11-1-4 (screenshots 370 and 371) phase 3 ends on it at 12:09:48 PM and Flood's phase 4 briefing follows at 12:09:48 PM. That phase's other two ends, at 12:04:20 and 12:05:48, are area lines.

The runtime sends the area line and waits for the player to leave, unless:

- the phase has `leave="0"`. Then it sends the mission-phase line and the next phase starts at once, wherever the player is. `leave="1"` always asks them to leave.
- the last goal was done on the way out: an `Upload`, or an `Escort` to `exit` or `hardline`;
- the player is already out, 60 m or more from every way out of the area (or it has none).

Then it sends the mission-phase line and moves on at once. Merv11-2-1 and Merv11-2-3 show only area lines where their phase ends are legible. Merv11-2-1 has no end line visible for its phase 2. Screenshots 40 and 41 meet between 12:54:34 and 12:54:51 PM, so a line may be hidden there.

Mission items are game objects: `Access_Code` 6782, `Map_Disk` 6783, `Recording` 6784, `Frags` 6785, `Virus` 6786, `Data` 6787, `Documents` 6794, `Plan` 6798, `Dossier` 6799, `File` 6800, `Antidote` 6803, `Small_Bomb` 6807, `Statue_Wrapped` 6815, `Package` 6817, `Large_Bomb` 6818, `Device` 6820, `Tracer` 6825, `Medical_Supplies` 6829, `Data_CD` 6831, `Recording_Coded_Message` 6836, `Dossier_Zion_Captain` 6850, `Dossier_Machine_Agent` 6853, `Document_Secret_Agreement` 6858, and the rest of 6782 to 6870 in `data/gameobjects.csv`.

### Mission sites

Retail's static world data still carries its mission markup, and `MissionSites` reads it when a district's first generated mission starts (the Slums: 22,001 sites in under 2 s):

- **Style pointers** (`SlumAptStylePointer`, `OfficeStylePointer`, `RedPillExtractionStylePointer`, ...) mark what kind of area a floor is. Building floors are 400 units apart and a floor's pointers sit 300 to 420 above it, so pointers of one style within 9 m of each other and 1.5 m in height make one site.
- **"Non Player Character Spawner"** objects mark where NPCs stand. Retail's NPCs stood 50 units below theirs (Welcome to the Matrix: spawners at 2542 to 2545, NPCs at 2495).
- **Computer, Safe, Mainframe and Box spawners** mark where mission objects go, and the filing cabinets, desks and dressers are there to search.
- **Elevators** have a door on each floor at the same x and z. A site's waypoint is its floor's door; its street entrance is the lowest door of the same shaft. Of a building's cars, the one that also stops at the street is used (some start higher).
- **Reach.** Pointers of neighbouring buildings can join one cluster, and an elevator only reaches its own building, so a site keeps only the NPC spots and objects within 22 m of its elevator door, about as far as retail's were from theirs (Welcome to the Matrix 3 to 15 m, The Internuncio 12 to 20 m). Teleporting further across such a floor put the client outside.

A site is picked as retail picked them:
- **Distance:** all six captured "Normal" phases were 153 to 165 m from where the player stood when the phase was given, though hundreds of fitting buildings were nearer. So a site is picked 145 to 170 m away, or as near that as there is.
- **Buildings:** retail used a different building each run (Welcome to the Matrix: four buildings in four runs).
- **Neighbourhoods:** a mission's or phase's `neighbourhoods="Achan, Mara"` puts it in those neighbourhoods when one is in range. The introduction's come from the guide's "more likely" lists, and 8 of the 10 captured sites were in them.
- **Reuse and preview:** a phase never reuses one of the mission's earlier sites. The first phase's site is picked when the player looks at the mission, so the info window shows its distance.

Each site's neighbourhood comes from `data/slums_neighbourhoods.csv`: every Slums sector's "Neighborhood" from the client's own world file (`resource/worlds/final_world/slums_barrens_full.metr`). Its sector numbers are the static data's and retail's briefing header's. All 36 contact, broker and Exile-lair positions in the guide fall in the neighbourhood it names. Some names in the file are pre-release; the CSV uses the release names (Hammerville is Dannah Heights, Toppan is Southard).

**Getting upstairs.**
- **The floor panel.** Retail's elevators have a floor panel: the server answers its use with `81 3f <sector u16> <region u16>`, and the client sends back `81 40 <button>`, counted from the bottom floor.
- **The region id** is the floor's id in the client's own world file (`slums_barrens_full.cooked_cod` and the other districts' files: one TransportGroup per shaft, one TransportSpace per floor, its TSPI). A sector's k-th ElevatorPanel by static id is its k-th floor record. `data/elevator_panels.csv` has that for all 19,631 panels and matches all 37 of retail's `81 3f` in the captures. With any other id the client says "Unable to properly establish the elevator panel UI!".
- **A button** moves the player to the same spot on that floor of the shaft, with any follower beside them.
- **The floor list shows when the client knows where the player stands.** client.dll compares the panel's sector and region with the player's own nav location (the own-view teleport record's sector, region and polygon; `NavMesh`) and closes the panel with `81 41` when they differ. With retail's record layout (PR #59) the lobby panels open, and a ride ends with retail's ding (`81 42 2a 06 00 22 00000000 <region> <sector>`).
- **The mission building's elevator doors** still take the player straight to the mission floor (or back to the street once it's done). Retail's operator offered that ride (`ID_OPERATOR_MISSION_ELEVATOR`, "I've hacked into the elevator control system. Do you want me to send you to the right floor?"), but the modal showed from a chat command and not after a door's use, so we don't ask.

## What's built

| Mission | Source | Checked in the real client |
|---|---|---|
| Welcome to the Matrix | Capture, complete | Start to finish |
| Exile Extermination | Capture, complete | Start to finish |
| The Internuncio | Capture, complete | Start to finish |
| Paging, Off White, Unhealthy Competition | Capture, first phase only (text and goals) | Not yet |

Text, NPCs, positions, goals and rewards are as retail sent them. **Which event sets off each line is our fit** to the order and the player's position in the captures, since only the lines themselves were sent.

### The Prima guide's missions

The rest of the story missions come from *The Matrix Online: Prima Official Game Guide* (2005), which prints every phase of Tyndall's seven introductory missions and the first ten of each organisation's (Flood for the Merovingian, Agent Gray for the Machines, Tyndall for Zion): the contact's lines when a phase starts and when the player arrives, the goals, the NPCs by role, the area type and the enemy. Where its text and a capture overlap (Welcome to the Matrix, Exile Extermination, The Internuncio) they match, except that the guide prints role names ("Follower", "Contact") where the game said a name, so the files give those NPCs names.

What the guide doesn't give is our fit: where (generated sites), the NPCs' looks (appearances of the right sex from the captures and world data), rewards (22,000 XP for each phase's full XP share, as The Internuncio paid; 31,500 info plus 10,500 a phase after the first), and template ids (1205 to 1208 for the introduction, 2101+, 2201+ and 2301+ for the organisations' arcs). The introduction is offered to every organisation, as the guide has it; each arc only to its own.

### Known gaps

- Only Zion's first three missions were captured: the rest are the guide's text, placed and paced by us.
- Rewards now use an explicit OURS level curve with `rewardLevel` metadata; unknown archive reference levels use the documented default50 tuning baseline. Retail team/difficulty/input formulas remain unproven. Welcome's3,000 and5,500 samples were both level2 at different difficulties; see [source, policy and native checks](MISSION-REWARDS.md).
- `dead="1"` NPCs now introduce sourced dead attributes and an NPC corpse pose, including late views; native rendering remains pending ([presentation evidence](MISSION-PRESENTATION.md)).
- Reputation doesn't change. Retail's captured early missions didn't change it either.
- Hostile portrait visibility remains unresolved. Automatic hostile speech now also uses an explicit OURS system-chat fallback, preserving original80a1 flags; friendly/interactive speech is unchanged ([presentation evidence](MISSION-PRESENTATION.md)).
- A mission doesn't survive a relog: the NPCs go, and its items are taken back.

### Full bags and loot replies

Story mission Get, Search and Loot goals complete after their item fits in the bag. A full bag leaves the
goal open. Items supplied by a briefing are retried when space becomes available. Give, Put and Upload
verify the tracked item is still present before completing the objective.

Retail's Take All reply is `81 20`, with a u16 array offset of 12, u32 status, u32 info amount, then a u16
count and eight bytes per unclaimed item, containing its four-byte descriptor and goid. The
`abilities_loot_but_inventory_full_corrupted` capture in the
`20260607T200024Z-hdneo-public-remaining-small-ability-loot-market` intake shows two unclaimed items at
lines 80-81 and another Take All request at line 100. The client's class at 0x6287fea0 also names these
fields. The server retains unclaimed pending drops for another Take All and credits the money once.
NPC loot is held per killer and corpse until the corpse despawns or the player disconnects. The native
`80 c7 ... 05` interaction reopens its remaining contents after another kill or a partial claim. Money
is credited once. Loot from a previous life expires when the NPC respawns, and ending a mission clears its corpses.
Take All applies to the selected corpse or container when their loot windows are opened in succession.
The tutorial's corpse-loot objective waits until both the revolver and letter fit in the bag.

### Chapter 12 reconstruction

The three Merovingian 12.1 missions use dialogue from the archived screenshot and forum logs, with
generated sites and reconstructed objectives. Hothouse's captured rewards are reused for Deep Cover
and Courtship. Their XML comments identify the placement and encounter choices. The headless scenario
completes all three using normal mission and combat RPCs, with development teleporting and weakened enemies.

The Merovingian 12.1 collector in Ikebukuro, static `ac 05 a0 07`, goid 48174, issues the three
entry tickets. They use native RPC `59 <bag slot u8>` when double-clicked. The client function at
0x621ed550 checks `GoldenTicketRule` and builds this two-byte request. `rshooks.GoldenTicketStartMission`
checks the ticket's level, reputation and construct properties.

| Mission | Entry ticket | Verification reward | Minimum level | Merovingian reputation | Construct |
|---|---|---|---|---|---|
| Hothouse | 48125 | 48134 | 30 | 20 | 1, Richland/Westview |
| Deep Cover | 48128 | 48137 | 40 | 40 | 2, Downtown |
| Courtship | 48131 | 48140 | 50 | 60 | 2, Downtown |

These values come from the tickets' `GoldenTicketConstructID`, `GoldenTicketMinMervRep`,
`ReqCharLevel`, `GoldenTicketMissionStart`, `GoldenTicketMissionDifficulty` and `Singleton`
properties in the shipped gameobjects.gob, attribute table 3807. The retail template IDs 3299,
3300 and 3301 map to this reconstruction's existing mission IDs 312101, 312102 and 312103.
The first ticket's construct gate covers the shared Richland/Westview world. The offer text calls it Westview.

The entry tickets remain in the bag during play and after abort. Success exchanges one ticket for
its verification reward in one inventory transaction, reusing the consumed slot in a full bag. A failed
grant leaves the mission ready to finish after the inventory problem is resolved. Ticket issuance
enforces the shipped singleton property. These missions enter independently through tickets and
are excluded from Flood's ordinary story list.

The collector's first three offers use their shipped per-offer strings `CL_STORY_12_1_MERV_OFFER01`
through `OFFER03`. Issuance is reconstructed as free, consistent with the tickets' zero VendorPrice.
The server-side requirement-table contents are unavailable in the client. Additional cosmetic
exchanges in the collector's remaining item sets still require implementation.

### Merovingian rewards

Most of the Merovingian logs are level-50 archive replays. They paid 525,000 or 1,039,500 information and capped or
no experience, which isn't a normal reward. So each section of the Merovingian story pays one pair of values,
taken from another log, and every file's header says it the same way: "Rewards: the log shows ... These are ...".

| Section | Experience | Information | From |
|---|---|---|---|
| 1.1 | 55,200 | 73,500 | Zion 1.1 (Redpill Rescue's log) |
| 1.2 | 68,750 | 91,000 | Zion 1.2 (Confiscation's log) |
| 1.3 | 308,750 | 94,500 | Zion 1.3 (Miracle Man's log) |
| 2.1, 2.2 | 297,000 | 98,000 | Zion 2.1 and 2.2 (Chemical Fear's and In Exile's logs) |
| 2.3 | 308,000 | 98,000 | Zion 2.3 (Free Your Mind's log) |
| 3.1 | 308,000 | 101,500 | Zion 3.1 (Dire Connection's log) |
| 4.1, 4.2 | 341,000 | 112,000 | Zion 4.1 and 4.2 (Demonstration of Force's and Between Waking and Sleeping's logs) |
| 4.3 | 352,000 | 115,500 | Zion 4.3 (Shakedown's log) |
| 5 | 286,000 | 94,500 | Machines 5 (Deviation's log): no Zion log shows chapter 5's |
| 6 to 9.2 | 363,000 | 63,000 | Zion 9.2 (1313's log), as the Zion files also have them for 7.2 and 8.3 |
| 9.3 | 374,000 | 63,000 | Zion 9.3 (Senseless's log) |
| 10 | 352,000 | 525,000 | Zion 10 (Blip, Zion's only chapter 10 file) |
| 11.1 | 352,000 | 115,500 | Zion 11.1 (The Spider's log) |
| 11.2 | 352,000 | 175,000 | Zion 11.2 (Mara's information, The Spider's experience) |
| 11.3 | 352,000 | 525,000 | Zion 11.3 (Resistance's information, The Spider's experience) |
| 12.1 | 239,105 | 105,000 | Hothouse's own log, a level-30 player's |

### Recurring characters' looks

The screenshot logs don't show RSIs, so the story files give their NPCs stand-in looks. A character who comes back
keeps one look in all three story sets (`zion_*`, `machines_*`, `merovingian_*`). The look is chosen in this order:

1. **A neighbourhood contact** wears the look the server gives them on their street corner
   (`data/mission_contact_npcs.csv`).
2. **The Merovingian, Persephone and Tesarova** wear their own client models, which the 12.1 files already used.
3. **Anyone else** wears the look the Zion and Machine files agree on. If they don't agree, it is the look most
   files used.

Two limits apply to every choice:

- **Sex.** A look must be of the character's sex. Sexes come from the logs' pronouns, and each look's sex comes from
  the client's model list (`client-data/npc_appearances.tsv` in the research folder). Several characters had been
  given female recruit looks (`c6020058`, `58010058`): the Merovingian, the Effectuator, Malphas, Carlyne, Beirn,
  Desayd, Serpentis, Pemberton, Chrysothemis and Brent. Ookami had been given male ones, though she is "she".
- **No twins by accident.** Two named characters in the same area never share a look. For that reason, Veil and
  the General take their second-most-used looks, and Kaplan, Wolf (5.2.4) and Zion's Craven take other looks.

Names that the logs reuse for different people keep separate looks: Serpentis, Alpha, Bohrmann, Wallaby, Calvinia,
Alligator, Cellino, Chimay, Churyumov, Downlink, Eos, Rabbit and Pigeon in the Merovingian files. The Zion and
Machine files' Abanderada, Anza, Brent, Chrysothemis, Dobrovoleva, Fissure, Ophiuchi and Zverev are also
different people from the Merovingian ones.

| Character | RSI | Model | Client's own model, not used yet |
|---|---|---|---|
| The Merovingian | `57050004` | Merovingian/MerovingianBlack | |
| Persephone | `5c050004` | Persephone/PersephoneCreme | |
| Tesarova | `fc0e0004` | oligarchs/og04 | |
| Malphas | `35000024` | NPCMpedCasual06 (M) | |
| The Effectuator | `9b0000b4` | recruit-03b (M), retail's male "Merovingian Soldier" | |
| Twin, Half Twin | `9c0000b4` | recruit-03c (M) | `2c01004e` Twins01 |
| Seraph | `9c0000b4` | recruit-03c (M) | `92010058` Seraph |
| The General | `9b0000b4` | recruit-03b (M) | `9400004e` NPCM_General |
| Holographic Projection (the General's) | `8f080058` | ZeroOne robot-2-1 | `2c030004` General_holo |
| Flood | `9b0000b4` | recruit-03b (M) | `d4020090` Flood |
| The Oracle | `3c000040` | Pedestrians-female2_1 | `3302009c` OracleNPC |
| Danielle Wright, and 11.1.5's "???" | `60010058` | lieutenant-02 (F) | `ec050004` daniellewright |
| Carlyne | `d8040058` | hacker-lieutenant-01b (M) | |
| Halborn | `8f080058` | ZeroOne robot-2-1 | |
| Beirn | `c9020058` | recruit-02b (M) | `30030004` unlimit/beirn |
| Trainman | `ec020058` | captain-01 (M) | `ad040004` trainman |
| Ookami | `a8060090` | succubus2 (F) | |
| Veil | `58010058` | lieutenant-01b (F) | `c0030008` veil |
| Niobe | `60010058` | lieutenant-02 (F) | `b1070040` Naobi |
| Ghost | `050100b4` | goatee-lieutenant-01 (M) | `e1010058` NPCMGhost |
| Morpheus | `050100b4` | goatee-lieutenant-01 (M) | `41020090` NPCMorph |
| Desayd and his clones | `c9020058` | recruit-02b (M) | |
| Kaplan | `d8040058` | hacker-lieutenant-01b (M) | |
| Cheeta | `9c0000b4` | recruit-03c (M) | |
| Apothecary | `5a000040` | Pedestrians-female1_4 | |
| Ebony | `5a000040` | Pedestrians-female1_4 | |
| Strenlo | `d8040058` | hacker-lieutenant-01b (M) | |
| Earnest Hungerford | `35000024` | NPCMpedCasual06 (M) | |
| 88 Eightball | `38000024` | NPCMpedCasual07 (M) | |
| Blackwood Duffy, Elwop, Killer | `ec020058`, `c9020058`, `8f080058` | | |
| Agents Long, Park (Merovingian files) | `9b000090` | Agent03 | |
| Agents Miller, West, Wilson, Wong (Merovingian files) | `3600004e` | Agent01 | |
| Contacts: Hypatia, Silver, Mercury, Thallia, Raini, Cerulean, Nicky G., The Coroner, Dame White, Anti M., The Jeweler, Argon, The Bag Lady, Beryl, Digger, Sister Margaret, Yttri, Lotus, The Sculptress, Madame T., The Chef, The Auditor, Sunshine, The Bartender, The Network, Mr. Black | their `rsi` in `data/mission_contact_npcs.csv` | their street look | |

The client's own models in the last column would be truer to retail, but nobody has seen them on a mission NPC in
the client yet. The 12.1 files' own models for the Merovingian, Persephone and Tesarova are also untried there.

## Contacts

The client ships retail's contact list: `resource/missions/definitions/sponsors.xml`, the "Missile" mission system's 67 contacts. `data/mission_contacts.csv` is that file (`research/missions/gen_mission_contacts.py` writes it), and `MissionContacts` reads it.

| Field | What it is |
|---|---|
| `id` | Retail's contact id. The list header, each list entry, the info and the accept carry it. |
| `org` | 1 Zion, 2 Machines, 3 Merovingian, 4 Niobe, 5 Pluribus Neo, 6 Cypherites, 7 GMs, 8 the operator, 9 to 14 "Archived" copies of the first six. 0 is a neighbourhood contact. |
| `name_id` | The name in the client's string table. |
| `code` | "Procedural text stringtable abbreviation" (`tyn`, `gra`, `flo`, ...). Only org contacts have one. |
| `face` | The portrait's resource id. |
| `min_rep`, `max_rep` | An org contact needs at least `min_rep` with the org and retires above `max_rep` (0: never). |
| `intro_text_id` | The contact's first words (`ID_CONTACT_INTRO_TYNDALL`, ...). |

- **Org contacts.** The list request's org byte picks the org; of its contacts, the one whose reputation range holds the player's reputation answers (the highest `min_rep` first). The client's file gives each org one contact and no limits, so today that is always Tyndall, Agent Gray, Flood, Anome, Shimada or Veil. Reputation is the character's reputation attribute for the org (`?rep zion 500`).
- **Visibility.** In client 7.6005, the Contacts List (`client.dll:0x620d2b20`) checks bit `org - 1` in the intro-state byte. The character background block (`02`) sets it at login, and a successful mission end (`80 a2`, `client.dll:0x6220ff60`) replaces it. The server sends `07` in both places to make Tyndall, Agent Gray and Flood available throughout play.
- **Every id matches the captures:** Tyndall and Agent Gray in their list headers, and The Newsie (47), The Chef (48) and Mr. Black (50) in theirs. Flood's portrait (`0x28000de6`) was missing before and used Tyndall's.
- **The list as retail sent it:** `80 95 <status u32> <txn> <count> <preview> <org> <contact id> <name id> <portrait>`, then one `80 96 <txn> <index> 0f00 <org> <contact id> <chained> <plot> <title>` per mission, then `80 97 <txn>`. `preview` is 0 for an org contact and 1 for a neighbourhood contact. Each entry's org byte used to be 1 for every list; retail's Machines entries carried 2 and its neighbourhood entries 0.
- Orgs 4 to 6 answer with an empty list: they have no missions yet.

### Neighbourhood contacts

The 53 org-0 contacts are informants who stand in the street. The client's world data has an `NPC_MissionContact` static (goid 6520) for 51 of them: 23 in the Slums, 17 Downtown, 11 International, one per neighbourhood except Moriah Projects, which has two. `data/mission_contact_npcs.csv` says who stands at which (`research/missions/gen_contact_npcs.py` writes it):

| Source | Contacts |
|---|---|
| Retail's own creation record in the captures | 14 (The Newsie, The Chef, Mr. Black, Synn, Grace, Mr. Bishop, ...) |
| The sector's "Contact Type" in the client's world file (`data/sector_properties.csv`), which is the sponsors.xml id | 27, 3 of them also captured |
| The Prima guide's map position, which falls on the static to the metre | 13 |

All three agree wherever they overlap, and so do the 13 Slums contacts in `data/NPC_COLLECTION.xml` (an older emulator's list). The Bartender (5) and The Grey Marshmallow (341, a test entry) have no static; the Downtown Bartender is 40.

- **Spawning.** Retail spawned a contact bound to its static: `08 78 19 <static> <spawn> cd ab 05 3d`, then Position (50 units below the static, as NPCs stand below spawners), ContactName (32 bytes), DescriptionID (the RSI), ContactID and Orientation. `MissionContactNpcs` sends the same to any player within 100 m.
- **Looks.** Retail's RSI where captured (14), else the one in `data/NPC_COLLECTION.xml` (10), else the client's model named after the contact (`characters-NPC/Contacts/thechessman.moa`). Grisaille has no model of her own: `Gray.moa` is a guess.
- **The list.** Using the contact (retail's client sent `80 c7 <view> <spawn> 02 00`) gets their list straight back with request number 0, preview 1 and org 0, as retail's three captured neighbourhood lists were. No capture has a list request (`80 94`) for a neighbourhood contact, so `80 94` still only reaches org contacts.
- **Info and accept.** For a neighbourhood contact's mission the info (`80 99`) and accept (`80 9c`) carry org 0, and the info has the title and `01 00 0a 00` before the description, as retail's Unhealthy Competition did. The org contacts' info now carries their org (retail: 01 for Tyndall, 02 for Agent Gray), not always 01.

### What a neighbourhood contact offers

- **Their own story missions** first, one at a time: missions with `org="0"` and `contact="<their name>"`. Retail's captured lists held just that one mission (plot flag set). Three are captured, all cut short: Paging (The Chef), Off White (Mr. Black) and Unhealthy Competition (The Newsie). Their title, description, briefing and goals are retail's; their site, enemies and rewards are ours, and each ends after its first phase.
- **Generic missions** after that, built by `NeighbourhoodMissions` for every contact: Assassination ("Kill <target>"), Retrieval ("Search the computer") and Rescue ("Lead <name> out of the area", "Do not let <name> die"), each one phase on a generated site in the contact's district (in the Slums, preferring the contact's neighbourhood), with three Exiles. The goal lines are retail's generic wording from the captures. The titles, briefings, descriptions and names are ours, and retail's captured neighbourhood lists didn't show generic missions, so offering them is our design. Ids are 3000 + contact id x 10 + 1 to 3.

**Retail's procedural mission text isn't in the client.** sponsors.xml's `code` names a "procedural text stringtable" for each org contact, but no such table ships: every `.sta` the client has is decoded (`stringtable_client`, `stringtable_ccr`, the cinematics', the place names and four server-only event tables), none holds a briefing of a generic mission, and the captured Assassination briefings ("I am never pleased to order an assassination...") aren't in any of them. client.dll reads only `sponsors.xml` and `organizations.xml` (not shipped) from `resource/missions/definitions`. So generic text has to come from the captures or be written.

Dev: with `MXO_DEV_COMMANDS=1`, `?contact` moves the player in front of the nearest neighbourhood contact in the district, and `?contact <id>` in front of that one (or says which district they're in).

## Azuna's collector exchange

Azuna's static in the second basement of the Creston Heights Museum is `3d 03 90 28`, goid 48153, district 2,
with CSV origin (-146900, -55, 50200). Retail-client QA on 2026-11-11 placed a player beside the visible
NPC at (-146195, -54, 50682), about 8.5 m away. Server reach and the native collector window are centered
at (-146195, -55, 50682), calibrated from that observation. Ten Function Data (48150) exchange for
Azuna's Surveillance (48149). The client strings describe the data as acquired while protecting Azuna,
and describe the surveillance as intended for a Merovingian representative in Ikebukuro. The archived
12.1.2 forum account also describes the ten-item exchange.

The implementation uses the retail collector records recovered from the checked-in captures.

| Record | Contents |
|---|---|
| `81 15` | Session GUID, position as three doubles, horizontal interaction distance as a float, five option bytes |
| `81 13` | Session GUID, initial, incomplete, complete and closing string IDs |
| `81 14` | Session GUID, item-set index, required item/data, prize item/data, final flag, four optional string IDs |
| `81 16` | Client exchange request with item-set index and a u16-length array of selected prize indexes |
| `81 17` | String ID and a byte selecting whether to display the closing text |

The Winter Helper exchanges in `Vector26.log.txt` around line 7831 and
`Vector_abilityloadoutchange.log.txt` around line 38175 show the GUID shared across the records,
one required item per `81 14` row, and an empty final row. Their files are in the
`20260607T203107Z-hdneo-public-vector-state-static-selected` and
`20260607T200024Z-hdneo-public-remaining-small-ability-loot-market` intakes.
The request and closing layouts also come from `client.dll` at 0x62072fa0 and 0x62176eb6.
Azuna uses her shipped offer and closing strings 0x040006c3 and 0x040006c0.

Nearby outdoor-world Override Functions drop Function Data. The museum-floor eligibility bounds are
reconstructed from the captured spawns, within 40 m horizontally and 6 m vertically of Azuna's static.
The native dialog uses a 4.5 m interaction range. These distances are implementation choices.

The server verifies the district, proximity, active collector and bag contents for each trade. Items in
worn slots are excluded. Consumption and reward insertion share a database transaction, including reuse
of a consumed slot in a full bag. At startup, before the network listeners accept players, an old MyISAM
inventory table is converted to InnoDB with its zero timestamps preserved. The character/slot index is
added for legacy tables as well. Fresh databases use the same engine and index.

## The tutorial

`TutorialMission.cs` runs retail's tutorial (mission 986) for any character that spawns in district 0. Its logic is retail's own: `TutorialScript.g.cs` is the client's decompiled `Mission986` script, converted to C# by `research/missions/gen_tutorial_script.py`, so its lines, voiced narration, timers and goal order are retail's. `TutorialMission.cs` is the engine that script expects, and it handles the goals the script leaves to server data: talking, the wardrobe, giving the key, fights, the hardline. `research/missions/tutorial-wire-spec.md` has the wire evidence.

Things the client needed, found by trying them:

- **Template 986 in `80 9c`.** Without it the client's tutorial mode is off: clicking the display, dragging Cheap Shot and the level-up panels send nothing.
- **Room objects a second after the teleport,** as retail sent them. In the same packet, the client drops them and the wardrobe can't be used.
- **The wardrobe and door unlock as retail did:** the wardrobe's state goes to 0x835 when Scarlett is talked to, and the door's MissionKey part goes to 0 and its state to 0x835 when goal 20 closes.
- **Bob and Alex carry the mission's key.** Without it, the client won't offer Close Combat.

What differs from retail:

- **No fades.** Retail's fade to black and white transitions (`81 42`) leave this client's interface hidden for good, and a transition's token never comes back as `81 91`. So the effects aren't sent, and the teleport runs a second later. Cause unknown.
- **No XP or info from the tutorial's fights,** and the player can't die there. Retail's only XP was the script's 501, and it gave an immortality ability we don't have.
- **Bob's body is looted by double-clicking it,** as in retail. The corpse window our combat code opens at the kill doesn't always appear.
- **The level-up goals close when the upgrade is made,** not on each panel click: the client sends the upgrade but not the clicks.
- **Leaving** goes through the Loading Area, and the player picks a hardline, as retail did. The blue pill sends the player out of the game, which is what the script asks for; nobody captured it.

`MXO_TUTORIAL_WARP=n` starts the tutorial at goal n, as retail's `TutorialWarpFactor` did. It's for testing.

## Sources

`research/missions/` holds the findings: `README.md` lists each file and what it covers. The 4 GB of screenshot logs (`hdneo/mxo-missions`) aren't in the repo.
