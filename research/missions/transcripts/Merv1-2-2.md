# Merv1-2-2: Propaganda

Source: Merovingian Criticals/Merv1-2-2, screenshot_54..screenshot_59.jpg (6 images, all read)

- Title (Current Mission bar): [Merovingian Archive 1.2.2] Propaganda (matches folder)
- Contact: [Archived]Flood (Operator lines also present)
- Player: Vesuveus, level 50 on HUD. In-game time 10:45:29 PM - 10:56:25 PM (about 11 minutes).
- Image order: all 6 images taken at the END of the mission at the same spot (indoor, green tiled floor, wood-panelled walls), Details popup open on "Captain's Log Entry from Morpheus", Inventory window title visible behind the Mission window, Log scrolled top to bottom between shots. Image order = log order. (Tabs here read Summary / Objectives / Log.)
- Mission description: not shown (Summary area only shows "Mission Summary. Datalink established.").

## Log in order
1. Operator 10:45:29 PM: "Mission Started!"
2. [Archived]Flood 10:45:29 PM (contact, briefing): "Morpheus has been observed putting up propaganda posters in the Matrix. Much to the disdain of the Merovingian these messages are bringing Machine programs to our doorstep. We need you to locate these posters and tear them down. I will send you the address of one of these two locations now, but be on your guard; Zion Operatives could still be there."
   (screenshot_54 ends "...We need you to locate"; screenshot_55 starts "these posters and tear them down."; appears contiguous)
3. Operator 10:45:56 PM: "There are at least three posters. Don't leave a single piece of propaganda hanging!"
4. Operator 10:46:19 PM: "You've been spotted by the camera! Be on your guard for Zion Operatives!"
5. Operator 10:50:00 PM: "I hope removing these posters helps keep the Agents at bay."
6. [Archived]Flood 10:50:00 PM (contact): "Excellent work. We have one more location for you to hit."
7. Operator 10:50:00 PM: "Goals for this area are complete. Find the nearest exit quickly!"
8. [Archived]Flood 10:50:18 PM (contact): "I am sending you the address of final location. You will run into more dreadful Zion Operatives."
9. Operator 10:50:53 PM: "Inside this building you will find five more posters. We need the entire place wiped of Morpheus' propaganda."
10. Operator 10:56:13 PM: "All posters have been torn down!"
11. Operator 10:56:13 PM: "Goals for this area are complete. Find the nearest exit quickly!"
12. [Archived]Flood 10:56:25 PM (contact, closing): "That is all for now, Vesuveus."
13. Operator 10:56:25 PM: "Mission Completed!"
(Entries 3-4, 4-5, 8-9 sit at image boundaries; the joins look contiguous. No gap is evident.)

## Chat lines
- Team (purple, all images; hostile Zion NPC barks): "[Team] Zion Expert Rifleman: W[e have] strict orders to deal with intrud[ers with] violence!" (cut), "[Team] Zion Expert Rifleman: A[nd now, you] die!" (cut), "[Team] Zion Ravager: And now, [you die!]" (cut), "[Team] Zion Kung Fu Grandmaster: And now, you die!", "[Team] Zion Network Attacker: We've got a break in!"
- System (screenshot_54): "You gained 15,555 information from searching a body.", "You received VariableFrag_C2[?]..." (cut off), "You recover from being off ba[lance]...", "You leave combat.", "Character data for Vesuveus ha[s been] written to the database.", "You gained 525,000 information from completing a mission."

## Objectives shown
Not shown.

## NPCs
- Zion Operatives (hostile), generic names seen in Team chat: Zion Expert Rifleman, Zion Ravager, Zion Kung Fu Grandmaster, Zion Network Attacker. Lines: "We have strict orders to deal with intruders with violence!" [partly cut], "And now, you die!", "We've got a break in!". Which area each was in is unknown; count unknown.
- No named friendly NPCs.

## Items
- None mission-specific. Loot: "VariableFrag_C2..." (cut) from a searched body.
- Captain's Log Entry from Morpheus (Details popup open in all images; how obtained not shown, probably not this mission). Verbatim:
  "CAPTAIN'S LOG, MORPHEUS. ENTRY #1170: I've just returned from a scouting mission in the Matrix. It's hard to keep going on my own, but I know I'm close to something. Once upon a time I had the support of a good crew - men and women who would do the right thing in spite of Lock's orders - but those days are past. Someone has been taking down the hardlines. They want us to think it's the Machines, but I've been up close with Agents enough times to know the difference. Those are not Agents. The Merovingian is behind this, perhaps trying to maintain his stranglehold on illegal access to the Matrix, perhaps some other motive. I'm not sure. But I intend to find out."

## Rewards
- "You gained 525,000 information from completing a mission." (System at screenshot_54, right after completion; this mission). No XP line (level 50).

## Locations
- Area 1: first poster location (has a security camera: "You've been spotted by the camera!").
- Area 2: "this building", final location with five posters.
- Screenshot spot: indoor, green tiled floor, wood-panelled walls; no district name visible.

## Build notes (interpretation)
Story: Morpheus' propaganda posters attract Machine attention to the Merovingian. Player tears down posters at two locations guarded by Zion operatives.

- Phase 1 (first location)
  - briefing: "Morpheus has been observed putting up propaganda posters in the Matrix. Much to the disdain of the Merovingian these messages are bringing Machine programs to our doorstep. We need you to locate these posters and tear them down. I will send you the address of one of these two locations now, but be on your guard; Zion Operatives could still be there."
  - enter: "There are at least three posters. Don't leave a single piece of propaganda hanging!" (Operator)
  - goals:
    1. Use x3: "Tear down the propaganda posters" (3 poster objects; one Use goal per poster, or a count). Done line: "I hope removing these posters helps keep the Agents at bay."
  - ambient: Operator "You've been spotted by the camera! Be on your guard for Zion Operatives!" (a security camera alerts the guards; no goal type for cameras: play this line on entering a trigger zone, or drop it).
  - after: Flood "Excellent work. We have one more location for you to hit." then "Goals for this area are complete. Find the nearest exit quickly!"
  - enemies: Zion (Expert Rifleman, Ravager, Kung Fu Grandmaster, Network Attacker types), count unknown.
  - site type guess: Normal or Office.
- Phase 2 (final location)
  - briefing: "I am sending you the address of final location. You will run into more dreadful Zion Operatives." (clean: "the address of the final location")
  - enter: "Inside this building you will find five more posters. We need the entire place wiped of Morpheus' propaganda." (Operator)
  - goals:
    1. Use x5: "Tear down the propaganda posters" (5 poster objects). Done line: "All posters have been torn down!"
  - after: "Goals for this area are complete. Find the nearest exit quickly!"
  - enemies: Zion, count unknown (took about 5 minutes).
  - site type guess: Normal / Warehouse.
- finish: "That is all for now, Vesuveus."
- Mechanics: the camera spotting is an alarm mechanic; closest playable: hostiles already present, keep the line as an enter/trigger line or omit. Posters need a usable world object (Use goal on a wall object).
