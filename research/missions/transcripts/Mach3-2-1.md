# Mach3-2-1: Always Watching

Source: Machine Criticals/Mach3-2-1, screenshot_64..screenshot_77.jpg (14 images, all read)

- Title (Current Mission bar): [Machine Archive 3.2.1] Always Watching
- Contact: [Archived]Agent Gray (header "Mission Summary. Datalink established."). Field NPCs Agent Stewart and Gerald Gajan also speak.
- Player: name redacted (white boxes) in the lines; HUD not visible (all images are crops of the Mission window or of a Computer popup). In-game time span 3:21:28 AM - 3:26:46 AM.
- Mission description: not shown (Summary tab never opened).
- Images 64-66 are crops of three "Computer" popups (no timestamps); images 67-77 are the Log tab scrolled top to bottom, taken at the end.

## Log in order
1. Operator 3:21:28 AM: "Mission Started!"
2. [Archived]Agent Gray 3:21:28 AM [contact, briefing]: "Greetings, <player name redacted>. Something has recently come to our attention; communications chatter related to the masked men has suddenly increased.
[?] (join between images 67 and 68; part of the entry may be hidden)
We must establish deeper surveillance of this group. Proceed to the location indicated on your HUD and obtain a listening device from Agent Stewart."
3. Operator 3:22:32 AM: "Agent Stewart should be on site here with the listening device."
4. Agent Stewart 3:22:39 AM [NPC, green silhouette icon]: "Greetings <player name redacted>. Please take the device from me."
5. Agent Stewart 3:22:43 AM: "The device is ready for use, human."
6. Operator 3:22:45 AM: "Okay, on to the next spot..."
7. Operator [?]3:22:45 AM: (only the header is visible at the bottom of image 69; probably "Goals for this area are complete. Find the nearest exit quickly!" [?])
8. Agent Stewart 3:22:47 AM: "You may continue on your way, <player name redacted>."
9. [Archived]Agent Gray 3:22:55 AM: "The next location is actually a... suspected base of the masked humans. You must locate a main communications computer inside, plant the listening device, and exit the area. Avoid... *unnecessary* hostilities if possible." ("unnecessary" in italics; images 70-71, join looks continuous)
10. Operator 3:23:40 AM: "All right, see if you can sneak in and out of there. Search all of the computers until you find their comms unit, and plant the device there."
11. Operator 3:24:00 AM: "Bingo! Plant the device in there and we can get moving."
12. Operator 3:24:07 AM: "Sweet! Device in place... now you can get out of there."
13. Operator 3:24:07 AM: "Goals for this area are complete. Find [the nearest exit quickly!]" (cut off at the bottom of image 72; standard phrase [?])
GAP: possible entries between 3:24:07 AM and 3:24:35 AM (join between images 72 and 73).
14. Operator 3:24:35 AM: "Damn, just looks like generic spam." (logged after the area goals were complete; probably the player opened another computer, see image 65)
15. [Archived]Agent Gray 3:25:08 AM: "Excellent work, <player name redacted>. Proceed to one of our laboratories for calibration of the listening signal."
16. Operator 3:26:04 AM: "There should be a Dr. Gajan in here.
[?] (join between images 73 and 74; part of the entry may be hidden)
He needs the exact coordinates of the mission location to calibrate the device's signal."
17. Gerald Gajan 3:26:29 AM [NPC, green silhouette icon]: "Ah, <player name redacted>. I presume you have the coordinates for me? Please direct your operator to download them to me now."
18. Operator 3:26:29 AM: "Okay, I'm downloading the coords... all set. You can get out of there now!"
19. Operator 3:26:29 AM: "Goals for this area are complete. Find the nearest exit quickly!"
20. Gerald Gajan 3:26:31 AM: "Excellent. The transmissions should begin coming through to this terminal now." (images 75-76)
21. [Archived]Agent Gray 3:26:46 AM: "Excellent work, as always, <player name redacted>. We will begin analyzing the intercepted transmissions from that device, and you will be contacted when your... service is required. That is all."
22. Operator 3:26:46 AM: "Mission Completed!"

## Computer popups (no timestamps; order and moment uncertain)
- Image 64, "Computer": "> New hardware detected... install in progress.
> Installation complete!
> New protocol detected... installation logs deleted.
> Communications system online
> _"
- Image 65, "Computer": "> Logged in as Fujian. Email headers:
- Promote your online business
- BUCKLED URGE
- Meet single women in Richland
- INCHES INWEEKS11" (may continue below the crop [?])
- Image 66, "Computer": "> System engaged! Intercepted transmissions:
- security system message
- email: Re: BUCKLED URGE
- security system message
- security system message
- email deletion
- security system message"

## Objectives shown
not shown (Objectives tab never opened).

## NPCs
- [Archived]Agent Gray: contact (male).
- Operator: female portrait.
- Agent Stewart: Machine NPC (green icon); hands the listening device (phase 1). Lines: entries 4, 5, 8.
- Gerald Gajan ("Dr. Gajan", "He"): male, Machine-side NPC at a Machine laboratory (green icon); receives the coordinates (phase 3). Lines: entries 17, 20.
- Phase 2: "masked humans" at their suspected base; no names/counts shown ("Avoid... unnecessary hostilities").

## Items
- listening device ("the device"): received from Agent Stewart; planted in the masked men's comms computer.
- coordinates of the mission location: downloaded by the operator to Gajan (not an inventory item as far as shown).
- No Details popups shown.

## Rewards
- not shown (no chat panel in any image).

## Locations
- Phase 1: location on the HUD where Agent Stewart waits (not described).
- Phase 2: "a... suspected base of the masked humans" with several computers, one of them the "main communications computer" / "comms unit".
- Phase 3: "one of our laboratories" (Machine lab) with Dr. Gajan and a terminal.
- No district / map labels visible.

## Build notes (interpretation)
Phase 1 (get the listening device)
- briefing: entry 2 (clean: "Greetings, <name>. Something has recently come to our attention; communications chatter related to the masked men has suddenly increased. We must establish deeper surveillance of this group. Proceed to the location indicated on your HUD and obtain a listening device from Agent Stewart.")
- enter: "Agent Stewart should be on site here with the listening device."
- goals: Get "Listening Device" from Agent Stewart ("Get the listening device from Agent Stewart"). On talk: "Greetings <name>. Please take the device from me." Then "The device is ready for use, human." Done: Operator "Okay, on to the next spot..." After: Stewart "You may continue on your way, <name>."
- enemies: none. Site: Normal.

Phase 2 (plant the device)
- briefing: entry 9.
- enter: "All right, see if you can sneak in and out of there. Search all of the computers until you find their comms unit, and plant the device there."
- goals: Search Computer (the comms unit, among several computers) -> found: "Bingo! Plant the device in there and we can get moving." Then Put "Listening Device" at that computer, waits for the search -> done: "Sweet! Device in place... now you can get out of there." Computer text on install: image 64 text. Suggested objective texts: "Find the communications computer", "Plant the listening device".
- decoy computers: image 65 text, operator line "Damn, just looks like generic spam." (closest: Use on a decoy computer with that line, optional).
- enemies: masked humans (rogue redpills / "masked men" group; human faction, closest Zion-like humans); count unknown; stealth suggested. Site: Warehouse or Office [?].

Phase 3 (calibrate at the lab)
- briefing: entry 15.
- enter: entry 16 (clean: "There should be a Dr. Gajan in here. He needs the exact coordinates of the mission location to calibrate the device's signal.")
- goals: Talk to Gerald Gajan ("Talk to Dr. Gajan"). On talk: entry 17. Done: Operator "Okay, I'm downloading the coords... all set. You can get out of there now!" After: Gajan "Excellent. The transmissions should begin coming through to this terminal now." (image 66 may be Gajan's terminal showing the intercepted transmissions; optional Use on a terminal.)
- enemies: none. Site: Office (laboratory).
- finish: "Excellent work, as always, <name>. We will begin analyzing the intercepted transmissions from that device, and you will be contacted when your... service is required. That is all."

Mechanics not expressible: "Avoid unnecessary hostilities" / "sneak in and out" (stealth suggestion only, no fail condition shown); "direct your operator to download" is just dialog (Talk goal). Closest playable as above.
