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

## What's built

| Mission | Source | Checked in the real client |
|---|---|---|
| Welcome to the Matrix | Capture, complete | Start to finish |
| Exile Extermination | Capture, complete | Start to finish |
| The Internuncio | Capture, complete | Start to finish |

Text, NPCs, positions, goals and rewards are as retail sent them. **Which event sets off each line is our fit** to the order and the player's position in the captures, since only the lines themselves were sent.

### Known gaps

- Every organisation is offered Zion's missions: no Machine or Merovingian mission has been captured in full.
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
