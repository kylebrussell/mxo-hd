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
| Phase done | `80 a1` string `0x2600000c` | "Goals for this area are complete. Find the nearest exit quickly!" |
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
- `<say>` with no attribute is the operator, `npc="n"` that NPC, `contact="1"` the mission contact.
- On an NPC: `attacksOnSight="0"` waits for the player to start the fight; `dead="1"` is a corpse; `phase` is the first byte of its MissionKey.
- `order` is its place in the contact's story chain: a contact offers the first unfinished one, then all of them to replay. Finished missions are in the `char_missions` table.
- `org` is the organisation whose contact gives it (1 Zion: Tyndall, 2 Machines: Agent Gray, 3 Merovingian: Flood); 0 or none is Tyndall's introduction. The client's mission list request (`80 94 <txn> <org>`) carries the contact's org, and it only shows a list whose header names that contact. `contact` names who speaks the mission's contact lines.

### Generated phases

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
- `enemy` (`Machines`, `Merovingian` or `Zion`) and `enemies`: hostiles spawned at the player's level, which attack on sight.
- On an NPC, `at="n"` places it in phase n's site when that phase starts. Without `at` it needs x, y, z and spawns at accept. Leave `level` out to match the player's level.
- `<object id kind>`: `Computer`, `Safe`, `Mainframe` or `Box` are spawned at the floor's spawner for that kind; `Furniture` (a filing cabinet, desk or dresser already there) and `Phone` are bound to one in place, as retail bound The Internuncio's filing cabinet.

Goal types beyond the five above:

| Type | Attributes | Done when |
|---|---|---|
| `DefeatAll` | | every hostile of the phase is dead |
| `Escort` | `npc` (one id, or several: `npc="2,3"`), `to` | the NPCs follow the player from when they're reached or talked to; done at the way out (`to="exit"`), beside another NPC (`to="npc:4"`), or on using a hardline (`to="hardline"`) |
| `Protect` | `npc` | the mission fails if the NPC dies; done with the phase's other goals |
| `Search` | `object`, `item` | using the object opens a loot window holding the item |
| `Put` | `object`, `item` | using the object while carrying the item |
| `Use` | `object` | using the object |
| `Upload` | `item` | uploading at a hardline while carrying it |

`optional="1"` on any goal lets the phase end without it.

Mission items are game objects: `Access_Code` 6782, `Map_Disk` 6783, `Recording` 6784, `Frags` 6785, `Virus` 6786, `Data` 6787, `Documents` 6794, `Plan` 6798, `Dossier` 6799, `File` 6800, `Antidote` 6803, `Small_Bomb` 6807, `Statue_Wrapped` 6815, `Package` 6817, `Large_Bomb` 6818, `Device` 6820, `Tracer` 6825, `Medical_Supplies` 6829, `Data_CD` 6831, `Recording_Coded_Message` 6836, `Dossier_Zion_Captain` 6850, `Dossier_Machine_Agent` 6853, `Document_Secret_Agreement` 6858, and the rest of 6782 to 6870 in `data/gameobjects.csv`.

### Mission sites

Retail's static world data still carries its mission markup, and `MissionSites` reads it when a district's first generated mission starts (the Slums: 22,001 sites in under 2 s):

- **Style pointers** (`SlumAptStylePointer`, `OfficeStylePointer`, `RedPillExtractionStylePointer`, ...) mark what kind of area a floor is. Building floors are 400 units apart and a floor's pointers sit 300 to 420 above it, so pointers of one style within 9 m of each other and 1.5 m in height make one site.
- **"Non Player Character Spawner"** objects mark where NPCs stand. Retail's NPCs stood 50 units below theirs (Welcome to the Matrix: spawners at 2542 to 2545, NPCs at 2495).
- **Computer, Safe, Mainframe and Box spawners** mark where mission objects go, and the filing cabinets, desks and dressers are there to search.
- **Elevators** have a door on each floor at the same x and z. A site's waypoint is its floor's door; its street entrance is the lowest door of the same shaft. Of a building's cars, the one that also stops at the street is used (some start higher).
- **Reach.** Pointers of neighbouring buildings can join one cluster, and an elevator only reaches its own building, so a site keeps only the NPC spots and objects within 22 m of its elevator door, about as far as retail's were from theirs (Welcome to the Matrix 3 to 15 m, The Internuncio 12 to 20 m). Teleporting further across such a floor put the client outside.

A site is picked 50 to 600 m from the player when possible, and a phase never reuses one of the mission's earlier sites. The first phase's site is picked when the player looks at the mission, so the info window shows its distance.

**Getting upstairs.** Retail's elevators had a floor panel (`81 3f <sector u16> <region u16>`, then the client's `81 40 <button>`). The client checks the region id against the panel: retail's ids for the two panels in the captures (0x1f for sector 405's shaft at 60300, 33390, 0x19 six floors up) are accepted, others give "Unable to properly establish the elevator panel UI!". Even with retail's id the client then asks `81 41 01 00` and shows nothing, so the floor list isn't working. In a mission's building the operator takes the player instead: using its elevator door or panel takes them to the mission floor, or back to the street once that floor is done, with a follower beside them. Retail's operator asked first (`ID_OPERATOR_MISSION_ELEVATOR`, "I've hacked into the elevator control system. Do you want me to send you to the right floor?"); that modal showed from a chat command, but not after a door's use, and once it failed none showed until relogging, so we don't ask.

## What's built

| Mission | Source | Checked in the real client |
|---|---|---|
| Welcome to the Matrix | Capture, complete | Start to finish |
| Exile Extermination | Capture, complete | Start to finish |
| The Internuncio | Capture, complete | Start to finish |

Text, NPCs, positions, goals and rewards are as retail sent them. **Which event sets off each line is our fit** to the order and the player's position in the captures, since only the lines themselves were sent.

### The Prima guide's missions

The rest of the story missions come from *The Matrix Online: Prima Official Game Guide* (2005), which prints every phase of Tyndall's seven introductory missions and the first ten of each organisation's (Flood for the Merovingian, Agent Gray for the Machines, Tyndall for Zion): the contact's lines when a phase starts and when the player arrives, the goals, the NPCs by role, the area type and the enemy. Where its text and a capture overlap (Welcome to the Matrix, Exile Extermination, The Internuncio) they match, except that the guide prints role names ("Follower", "Contact") where the game said a name, so the files give those NPCs names.

What the guide doesn't give is our fit: where (generated sites), the NPCs' looks (appearances of the right sex from the captures and world data), rewards (22,000 XP for each phase's full XP share, as The Internuncio paid; 31,500 info plus 10,500 a phase after the first), and template ids (1205 to 1208 for the introduction, 2101+, 2201+ and 2301+ for the organisations' arcs). The introduction is offered to every organisation, as the guide has it; each arc only to its own.

### Known gaps

- Only Zion's first three missions were captured: the rest are the guide's text, placed and paced by us.
- Rewards are fixed at one capture's values; retail's rose with level (Welcome paid 3,000 to 5,500 XP).
- `dead="1"` NPCs aren't spawned, so the bodies in The Internuncio don't appear.
- Reputation doesn't change. Retail's captured early missions didn't change it either.
- Hostile NPCs' talk lines reach the client but didn't show on screen when tested.
- A mission doesn't survive a relog: the NPCs go, and its items are taken back.

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
