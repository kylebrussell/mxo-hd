# Mach1-3-6: Bugged Line

Source: Machine Criticals/Mach1-3-6, Screenshots_196..Screenshots_201.jpg (6 images, all read)

- Title (Current Mission bar): [Machine Archive 1.3.6] Bugged Line
- Contact: [Archived]Agent Gray
- Player: TiAmo (nameplate "TiAmo - Zion", tag "<Daniel Institute of Dream Interpretation>"), HUD level 21. Only in-game times visible: 9:40:50 AM and 9:40:58 AM. Mission still running in image 201 (the button reads "Abort", not "Continue").
- Mission description: not shown.
- Image notes: images 196-200 are scene shots without UI (same room, camera fixed); image 201 has the Objectives window, Mission log and chat.

## Log in order
GAP: everything before 9:40:50 AM not captured; only the tail of one earlier entry is visible.
1. [?speaker] [time not visible]: "[?...] the Assassin." (end of an entry above Park's line)
2. Park The Exterminator 9:40:50 AM [green NPC icon]: "Morpheus was the human terrorist responsible for subverting Thomas Anderson. Why do you ask?"
3. Miller The Exterminator 9:40:58 AM [green NPC icon]: "Morpheus? A former leader of the Zion group of rebel humans. He was [?...]" (cut off at the bottom of the log; the chat shows what is most likely the rest: "...former leader of the Zion group of rebel humans. He was recently terminated by an unknown program known by the alias "The Assassin."")
GAP: everything after 9:40:58 AM not captured.

### Chat lines (image 201)
- System: "Vector-Hostile.Privaront is offline." / "You target [Park The Exterminator]" / "[Park The Exterminator] is hostile."
- Team panel: "...former leader of the Zion group of rebel humans. He was recently terminated by an unknown program known by the alias "The Assassin."" (speaker cut off; matches Miller's log entry)
- "[Team] Wong The Exterminator: The human hacker known as Morpheus was eliminated recently. While alive, he led many assaults on our mainframes."

## Objectives shown
Image 201 (one phase, partly scrolled):
- "Delete the traitor program" (white text, unchecked box)
- "Do not delete the valuable program 1" (red text = must-not-happen condition)
- "Do not delete the valuable program 2" (red)
- "Do not delete the valuable [?program 3]" (red, cut off at the window bottom)

## NPCs
- [Archived]Agent Gray: contact.
- Miller The Exterminator: Machine program (exterminator in tan/khaki overalls with a round back emblem); orange nameplate; male model. Line: entry 3.
- Wong The Exterminator: same type; male model; line in Team chat (above).
- Park The Exterminator: same type; level 22 (target frame "Park The Exterminator", chevron rank icon, "22"); "[Park The Exterminator] is hostile." when targeted (attackable). Line: entry 2. Target frame shows a red health bar (full).
- Marks The Exterminator: same type (nameplate "Marks The Exterminator" in image 201); no line captured.
- 4 exterminators in total are visible in images 199-200 (two at left, two at right).
- "Agent" x several (purple/magenta nameplates "Agent"): in image 196 about 5-6 Agents in black suits around the player, one with a red health bar under the name; in 197 one Agent walks with the player and one more at bottom-left; 198-200 several Agents walk around among the exterminators. Whether they are friendly escorts, hostile imposters or spectators is not determinable.
- Image 201: three black-suited bodies lie on the floor (look like defeated Agents/men in suits) [?], and Marks The Exterminator stands behind them.

## Items
- none shown

## Rewards
- not shown

## Locations
- One interior room: dark wooden floor, beamed ceiling, shoji-style paper screens/doors, potted plants in vases, a reed rocking chair, a small rug, a table with papers in a back room (image 196). Japanese-style house or office interior. No map/district label visible.

## Build notes (interpretation, not transcript)
Known structure: one phase (at least) with 4 exterminator programs (Miller, Wong, Park, Marks), one of whom is a "traitor"; the player asks them about Morpheus and must delete (kill) only the traitor; killing any of the 3 "valuable" programs fails the mission.

- briefing: not captured. Builder must write one (invented): Gray sends the player to a Machine site where one of the exterminator programs has been leaking information ("bugged line") and must be identified by questioning and deleted.
- enter: not captured.
- goals:
  1. Talk to each exterminator (optional; their answers are the clues). Captured answers:
     - Park The Exterminator: "Morpheus was the human terrorist responsible for subverting Thomas Anderson. Why do you ask?"
     - Miller The Exterminator: "Morpheus? A former leader of the Zion group of rebel humans. He was recently terminated by an unknown program known by the alias "The Assassin.""
     - Wong The Exterminator: "The human hacker known as Morpheus was eliminated recently. While alive, he led many assaults on our mainframes."
     - Marks The Exterminator: not captured. One more line ending "...the Assassin." was said just before Park's (speaker unknown; perhaps Marks, or Miller's own line said earlier).
  2. "Delete the traitor program" = Defeat <traitor>. Which exterminator is the traitor is NOT shown. In image 201 the player has targeted Park (hostile, level 22) - possibly about to attack Park, but this is not proof.
  3. Fail conditions "Do not delete the valuable program 1/2/3" = Protect the other three exterminators (Protect goals; they must not die).
- enemies: Machine programs (the exterminators are attackable; possibly the traitor fights back). The "Agent" NPCs' role is unknown; possibly imposter Agents that attack (three suited bodies on the floor in image 201) - unproven.
- site type guess: Normal or Office (Japanese-style interior).
- finish: not captured.
- Mechanics our goal types cannot express: choosing the right target among identical-looking NPCs (deduction/choice) with fail-on-wrong-kill. Closest playable version: Defeat <traitor> + Protect the three others (mission fails if a protected NPC dies). Make all four attackable, but only the traitor's death completes the goal.
- clean game text: the exterminator lines above are complete except Marks (missing). Use "The Assassin" in quotes as in the chat.
