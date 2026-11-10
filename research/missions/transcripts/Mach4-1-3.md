# Mach4-1-3: Tightening the Circle

Source: Machine Criticals/Mach4-1-3, machine41301..machine41308.jpg (8 images, all read)

- Title (Current Mission bar): [Machine Archive 4.1.3] Tightening the Circle
- Contact: [Archived]Agent Gray (header "Mission Summary. Datalink established."). Miguel Zappa (friendly, green silhouette icon) also speaks.
- Player: "Mr. Andrews" (Zappa, Gray). HUD not visible (all images are crops of the Mission window). In-game time span 1:52:00 AM - 2:08:49 AM.
- Mission description: not shown (Summary tab never opened).
- All images show the Log tab, scrolled top to bottom.

## Log in order
1. Operator 1:52:00 AM: "Mission Started!"
2. [Archived]Agent Gray 1:52:00 AM [contact, briefing]: "Our scientists have been working on a little project. We now have a bug capable of locking down the Effectuator's RSI, and triangulating the location of what you might term his physical body. In fact, he possesses no physical body, but the explanation would most likely be beyond your comprehension.

Proceed to Dr. Zappa and obtain the bug from him." (images 1-2; reads continuously across the join, a hidden line is possible but unlikely [?])
3. Operator 1:52:53 AM: "Dr. Zappa is somewhere in this building."
4. Miguel Zappa 1:53:37 AM [friendly NPC, green]: "Your reputation for punctuality precedes you, Mr. Andrews. I have the bug for you here."
5. Operator 1:53:41 AM: "Bug in hand; you can head on over to good old Effy now."
6. Operator 1:53:41 AM: "Goals for this area are complete. Find the nearest exit quickly!"
7. [Archived]Agent Gray 1:54:08 AM: "Proceed to the location indicated. Place the bug on the Effectuator's person."
8. Operator 1:54:42 AM: "This place looks kind of empty."
9. Operator 1:55:11 AM: "Nothing. You can head out of there."
10. Operator 1:55:11 AM: "Goals for this area are complete. Find the nearest exit quickly!"
11. [Archived]Agent Gray 1:55:36 AM: "Do not be discouraged. We will track down the Effectuator, and apprehend him. There is a high probability that he will be at the next location."
12. Operator 1:56:27 AM: "Okay, I'm reading some signals this time. Looks like a party going on in there! Time to go to work."
GAP: images 6 and 7 do not overlap; entries between 1:56:27 AM and 2:06:19 AM (about 10 minutes, probably fighting) may be missing.
13. Operator 2:06:19 AM: "His signal's just through there. Aw, crap... those fluctuations are starting to power up. Get through the far door!"
14. Operator 2:08:19 AM: "This is insane! How can we plant a bug on someone who vanishes before we get there?"
15. Operator 2:08:19 AM: "Goals for this area are complete. Find the nearest exit quickly!"
16. [Archived]Agent Gray 2:08:49 AM: "New strategies seem to be required. We will be in contact, Mr. Andrews."
17. Operator 2:08:49 AM: "Mission Completed!"

## Objectives shown
not shown (Objectives tab never opened).

## NPCs
- [Archived]Agent Gray: contact (male).
- Operator: female portrait.
- Miguel Zappa ("Dr. Zappa"): friendly (green silhouette icon); male ("obtain the bug from him"); Machine-side scientist; hands the bug. Line: entry 4.
- The Effectuator ("good old Effy"): target; male; never reached, vanishes. Said nothing.
- Hostiles at site 3: "Looks like a party going on in there!"; names/count not shown (about 10 minutes of play).

## Items
- "the bug" (given by Miguel Zappa; to be placed "on the Effectuator's person"; never used). Exact item name not shown, no "You received" line visible.

## Rewards
- not shown (no chat panel in any image).

## Locations
- Site 1: Dr. Zappa's building ("Dr. Zappa is somewhere in this building").
- Site 2: "the location indicated" (empty).
- Site 3: "the next location" (many hostiles, a "far door").
- No district / map labels visible.

## Build notes (interpretation)
Phase 1 (Dr. Zappa)
- briefing: entry 2 (clean, as transcribed).
- enter: "Dr. Zappa is somewhere in this building."
- goals: Get from Miguel Zappa ("Get the bug from Dr. Zappa"); item e.g. "RSI Bug" [name guessed]. Talk line: "Your reputation for punctuality precedes you, <name>. I have the bug for you here." Done line: "Bug in hand; you can head on over to good old Effy now."
- enemies: none. Site: Office [?] (scientist).

Phase 2 (empty location)
- briefing: entry 7 ("Proceed to the location indicated. Place the bug on the Effectuator's person.")
- enter: "This place looks kind of empty."
- goals (retail): proximity trigger, nobody there (29 s). Closest playable: Search ("Search for the Effectuator", Furniture/Computer) with done line "Nothing. You can head out of there." No hostiles.
- site: Normal or SlumApt [?].

Phase 3 (the party)
- briefing: entry 11 ("Do not be discouraged. We will track down the Effectuator, and apprehend him. There is a high probability that he will be at the next location.")
- enter: "Okay, I'm reading some signals this time. Looks like a party going on in there! Time to go to work."
- goals: DefeatAll ("Kill all Exiles in the area"; done/progress line "His signal's just through there. Aw, crap... those fluctuations are starting to power up. Get through the far door!"), then a Put/Use goal at the far room ("Plant the bug", e.g. Put the bug at an object, or Use a Computer) whose done line is "This is insane! How can we plant a bug on someone who vanishes before we get there?" (Story: the bug is never planted; if Put is used, the item is consumed, which is fine since the mission ends.)
- enemies: Merovingian Exiles [?] (Effectuator's people); several.
- finish: entry 16 ("New strategies seem to be required. We will be in contact, <name>.")

Mechanics not expressible: target vanishing on approach, timed "fluctuations". Same pattern as Mach4-1-1 and Mach4-1-2.
