# Mach4-1-1: Laying the Groundwork

Source: Machine Criticals/Mach4-1-1, machine41101..machine41109.jpg (9 images, all read)

- Title (Current Mission bar): [Machine Archive 4.1.1] Laying the Groundwork
- Contact: [Archived]Agent Gray (header "Mission Summary. Datalink established.")
- Player: Gray calls the player "Mr. Andrews"; the Operator calls him "Tygrius" (handle). HUD not visible (all images are crops of the Mission window). In-game time span 5:09:43 PM - 5:26:09 PM.
- Mission description: not shown (Summary tab never opened).
- All images show the Log tab, scrolled top to bottom. The "Mission Started!" entry is not visible (the first visible entry is Gray's briefing at the top of image 1; the log scroll bar is not at the very top, so earlier entries may exist above).

## Log in order
1. [Archived]Agent Gray 5:09:43 PM [contact, briefing]: "The pace of events is picking up, Mr. Andrews. We are beginning to make real progress in our efforts to remove the Merovingian's presence from the Matrix.

Your job today is to apply pressure. With enough of his assets under attack, he will lose... *focus*; his pathetic empire will begin to crumble around him.

Proceed to the location indicated, and deal with any Exiles present." ("focus" in italics; images 1-2)
2. Operator 5:10:34 PM: "Expect some resistance in here, Tygrius."
GAP: entries between 5:10:34 PM and 5:17:22 PM not captured (images 2 and 3 do not overlap; the scroll bar moved, so there may be entries in between).
3. Operator 5:17:22 PM: "That's the last hostile on site."
4. Operator 5:17:22 PM: "Goals for this area are complete. Find the nearest exit quickly!"
5. [Archived]Agent Gray 5:17:43 PM: "Well done. I am now sending you to the location of one of the Merovingian's lieutenants, Fanale. Terminate him." (split across images 3-4; a line at the join may be hidden [?], but the text reads continuously)
6. Operator 5:18:43 PM: "My readings show Fanale in here, with some security."
7. Operator 5:24:03 PM: "I'm reading his signal... plus some sort of fluctuation... get through that next door, now!" (split across images 4-5)
8. Operator 5:24:12 PM: "Dammit! He's gone... and I suppose you know what kind of signals I'm getting, too. The Effectuator."
9. Operator 5:24:12 PM: "Goals for this area are complete. Find the nearest exit quickly!"
10. [Archived]Agent Gray 5:24:41 PM: "The escape of Fanale certainly is disappointing. However, our goals proceed as scheduled. Continue your attacks against the Merovingian's holdings.

At the location indicated, you will find a certain Ashbrook. She is one of the Merovingian's security captains. Her deletion would prove very... *detrimental* to the Merovingian's operations." ("detrimental" in italics; images 6-7; the word "deletion" is half hidden at the bottom of image 6 and fully visible at the top of image 7)
11. Operator 5:25:34 PM: "Looks like plenty of bad guys in here."
12. Operator 5:25:59 PM: "Gone! The entire place is empty! This is such... aw, just get out of there."
13. Operator 5:25:59 PM: "Goals for this area are complete. Find the nearest exit quickly!"
14. [Archived]Agent Gray 5:26:09 PM: "It would appear that the Effectuator's "talents" are becoming a bit too... *inconvenient*. Would you not agree, Mr. Andrews?" ("inconvenient" in italics)
15. Operator 5:26:09 PM: "Mission Completed!"

## Objectives shown
not shown (Objectives tab never opened).

## NPCs
- [Archived]Agent Gray: contact (male).
- Operator: female portrait.
- Exiles (phase 1): hostile; "deal with any Exiles present"; count/names not shown. "That's the last hostile on site."
- Fanale: one of the Merovingian's lieutenants; target; male ("Terminate him", "He's gone"); not killed, escapes (with the Effectuator's help, per the operator). Said nothing in the log. "with some security".
- Ashbrook: Merovingian's security captain; female ("She"); target; not found, the site is emptied ("The entire place is empty!"). Said nothing.
- The Effectuator: named only, not seen (blamed for the escapes).
- Hostiles at phase 3: "Looks like plenty of bad guys in here." (then empty 25 s later).

## Items
- none shown (no chat panel, no "You received" lines).

## Rewards
- not shown (no chat panel in any image).

## Locations
- Site 1: "the location indicated" with Exiles (Merovingian asset).
- Site 2: Fanale's location, with security; has a "next door" he is behind.
- Site 3: Ashbrook's location.
- No district / map labels visible.

## Build notes (interpretation)
Phase 1 (Exile site)
- briefing: entry 1 (clean: "The pace of events is picking up, <name>. We are beginning to make real progress in our efforts to remove the Merovingian's presence from the Matrix. Your job today is to apply pressure. With enough of his assets under attack, he will lose... focus; his pathetic empire will begin to crumble around him. Proceed to the location indicated, and deal with any Exiles present.")
- enter: "Expect some resistance in here, <name>."
- goals: DefeatAll ("Kill all Exiles in the area"). Done line: "That's the last hostile on site."
- enemies: Merovingian Exiles; several (about 7 minutes). Site: Warehouse or Normal [?].

Phase 2 (Fanale)
- briefing: entry 5 ("Well done. I am now sending you to the location of one of the Merovingian's lieutenants, Fanale. Terminate him.")
- enter: "My readings show Fanale in here, with some security."
- goals (retail): fight through security, then reach Fanale's room, where he has vanished. Closest playable: DefeatAll ("Kill Fanale's security"), done line "I'm reading his signal... plus some sort of fluctuation... get through that next door, now!" then phase completes with "Dammit! He's gone... and I suppose you know what kind of signals I'm getting, too. The Effectuator." Do NOT spawn a killable Fanale (story: he escapes). Alternative: a Use goal on the "next door" object or a Search of an empty Furniture as "Find Fanale".
- enemies: Merovingian security (Exiles/Merovingian soldiers); count not shown (about 5 minutes). Site: Office or Normal [?].

Phase 3 (Ashbrook)
- briefing: entry 10 (clean: "The escape of Fanale certainly is disappointing. However, our goals proceed as scheduled. Continue your attacks against the Merovingian's holdings. At the location indicated, you will find a certain Ashbrook. She is one of the Merovingian's security captains. Her deletion would prove very... detrimental to the Merovingian's operations.")
- enter: "Looks like plenty of bad guys in here."
- goals (retail): only 25 s between enter and "Gone! The entire place is empty!", so the site was emptied by script (an arrival trigger), not cleared by fighting. Closest playable: a single Search/Use goal ("Find Ashbrook", e.g. a Computer or Furniture in an empty site) whose done line is "Gone! The entire place is empty! This is such... aw, just get out of there." Alternatively a site with no hostiles. Do not spawn Ashbrook.
- site: Office [?] (security captain).
- finish: entry 14 ("It would appear that the Effectuator's "talents" are becoming a bit too... inconvenient. Would you not agree, <name>?")

Mechanics not expressible: the scripted escapes (target vanishes on arrival / site emptied). Use the substitutes above. Player name: Gray uses the surname ("Mr. Andrews"), the operator the handle ("Tygrius").
