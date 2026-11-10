# Zion1-1-2: Recruit with Machines

Source: Zion Criticals/Zion1-1-2, Screenshots_211..Screenshots_215.jpg (5 images, all read)

- Title (Current Mission bar): [Zion Archive 1.1.2] Recruit with Machines
- Contact: [Archived]Tyndall (Operator lines also present)
- Player: TiAmo, level 21 on HUD. In-game time 11:22:38 AM - 11:27:34 AM (about 5 minutes).
- Image order: all 5 images taken at the END of the mission at the same spot (indoors, red patterned carpet), Details popup open on the item, Log scrolled top to bottom. Image order = log order.
- Mission description: not shown (Summary tab never opened).

## Log in order
1. Operator 11:22:38 AM: "Mission Started!"
2. [Archived]Tyndall 11:22:38 AM (contact, briefing): "The Machines claim they are innocent of breaking the Truce. They say they can prove it by helping us stop these so called imposters. We need you to hook up with one of their teams and protect a potential redpill contact. I don't know about you, but I think this is strange. Machines actually helping us recruit... A few Machine Operatives are guarding our man, Dallas Vandenberg, at this location. Just meet up with the Programs and Dallas Vandenberg, and then lead Dallas Vandenberg to the REP and extract him from the Matrix." (one entry, split across Screenshots_211-212)
   - GAP?: Screenshots_212 ends with a separator after this entry and Screenshots_213 starts at 11:23:24; probably contiguous, not certain.
3. Operator 11:23:24 AM: "The code that I'm seeing pass by isn't what I was expecting... The Machine Operatives may have upgraded or changed somehow. I don't know for sure if these are Machine Operatives or not, as the signatures that I'm seeing are those of programs."
4. Operator 11:24:12 AM: "What the...?! Have the Machines betrayed us?! Show them that Zion will not go quietly!"
5. Operator 11:25:13 AM: "Oh no... That must be Dallas Vandenberg! Those bastards killed him! Get out of there before more show up and they kill you as well!"
   - GAP: one entry between 11:25:13 and 11:25:38 not captured; only its last line tail is visible at the top of Screenshots_215: "...the nearest exit quickly." (almost certainly Operator "Goals for this area are complete. Find the nearest exit quickly!", but header/time/rest not read [?]).
6. Operator 11:25:38 AM: "Something is terribly wrong..."
7. [Archived]Tyndall 11:27:34 AM (contact): "Maybe we'll have better luck next time, TiAmo."
8. Operator 11:27:34 AM: "Mission Completed!"

## Chat lines
- Team (purple, all images): "[Team] Duboshin: We'll follow you out of here." (partial, top), "[Team] Altaj: I want them to pay for this!", "[Team] Tolosa: I can heal you if things get bad.", "[Team] Agent: Strike hard. Strike fast." (x2), "[Team] Agent: Destroy the humans.", "[Team] Agent: You will not leave, human." (x2). The Duboshin/Altaj/Tolosa/first two Agent lines are left over from Zion1-1-1; "Destroy the humans." and "You will not leave, human." are new and belong to this mission's hostile "Agent" NPCs.
- System (Screenshots_211): "You gained 30,000 experience from completing a mission.", "You gained 73,500 information from completing a mission." (Zion1-1-1 gave 55,200 XP, so these lines are this mission's.)

## Objectives shown
Not shown.

## NPCs
- Dallas Vandenberg: potential redpill contact, guarded by "Machine Operatives"; found dead ("That must be Dallas Vandenberg! Those bastards killed him!"). Sex: "our man", "him" = male. No lines.
- "Agent" / "Programs" / "Machine Operatives": appear as escorts/guards first, then turn hostile ("Have the Machines betrayed us?!"). Barks: "Destroy the humans.", "You will not leave, human." Count unknown.

## Items
- Machine Communique re: The Plant (title shows an accented "Communique" with acute e). How it was obtained is not shown (no "You received" line visible); probably looted from a hostile Agent. Details popup text, verbatim:
  "//SIGINT/////////////////////////
  //INTERCEPT-AAA-EYESONLY/////////
  ////////////////////////////////
  AGENT MORRISON 38459372930294000 SIGNAL TO SOURCE. REPORTING SUCCESSFUL INSERTION OF THE PLANT AT SPECIFIED COORDINATES. CONDITIONS FAVORABLE FOR RAPID GROWTH AND DEPLOYMENT. WILL MONITOR AND REPORT ON PLANT'S CONDITION. SIGNAL CLOSED.
  //SIGINT/////////////////////////
  //INTERCEPT-AAA-EYESONLY/////////
  ////////////////////////////////"
  (count of slashes approximate)

## Rewards
- "You gained 30,000 experience from completing a mission." / "You gained 73,500 information from completing a mission."

## Locations
- Interior with red patterned carpet and wood panelling (Screenshots_211-215). No district name. Lines mention "this location" and "the REP".

## Build notes (interpretation)
Retail story: a scripted betrayal. The friendly "Machine Operatives" turn out to be imposter programs; Dallas Vandenberg is found dead; the player fights and leaves. The REP escort never happens.
- Phase 1 (one area)
  - briefing: Tyndall 11:22:38 text above.
  - enter: Operator "The code that I'm seeing pass by isn't what I was expecting... The Machine Operatives may have upgraded or changed somehow. I don't know for sure if these are Machine Operatives or not, as the signatures that I'm seeing are those of programs." (this arrives 46 s after start, probably on entering the building)
  - goals:
    1. DefeatAll (or Defeat named "Agent"s): "Defeat the imposter Programs". Start line when they turn hostile: "What the...?! Have the Machines betrayed us?! Show them that Zion will not go quietly!" Enemies: Machines (imposter Agents / Machine programs), barks "Destroy the humans.", "You will not leave, human.", "Strike hard. Strike fast." [?the last may be from 1.1.1 only].
    2. Talk (or Use/inspect) Dallas Vandenberg's body: "Find Dallas Vandenberg". Done line: "Oh no... That must be Dallas Vandenberg! Those bastards killed him! Get out of there before more show up and they kill you as well!" A dead NPC cannot be talked to with our goal types; closest: Talk to a non-hostile Dallas Vandenberg NPC placed lying down, with that line as the operator response, or skip and fire the line when the area is cleared.
    3. Loot: "Machine Communique re: The Plant" from one of the Agents (not confirmed by the log, but the item was in hand at the end).
  - after: "Goals for this area are complete. Find the nearest exit quickly!" then Operator "Something is terribly wrong..."
  - site type guess: Hotel or Normal (red carpet, wood panelling).
  - finish: "Maybe we'll have better luck next time, TiAmo." (player name)
- Mechanics not expressible: NPCs switching from friendly to hostile mid-mission. Closest: spawn the "Programs" hostile from the start (the operator's first line already warns), or spawn them only after a Talk goal with a friendly "Machine Operative" if phased spawns exist.
