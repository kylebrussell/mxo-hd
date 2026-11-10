# Mach3-1-2: Operation Myrmidon

Source: Machine Criticals/Mach3-1-2, screenshot_24..screenshot_33.jpg (10 images, all read)

- Title (Current Mission bar): [Machine Archive 3.1.2] Operation Myrmidon
- Contact: [Archived]Agent Gray (header "Mission Summary. Datalink established."). Only Gray and the Operator speak.
- Player: name redacted (white boxes) in the lines; HUD not visible (all images are crops of the Mission window). In-game time span 2:17:46 AM - 2:41:51 AM.
- Mission description: not shown (Summary tab never opened).
- All images are the Log tab scrolled top to bottom, taken at the end. At most joins one text line is hidden or only partly visible; marked [?] where it may hold words.

## Log in order
1. Operator 2:17:46 AM: "Mission Started!"
2. [Archived]Agent Gray 2:17:46 AM [contact, briefing]: "Because of the commandos' brazen interference in our operations, we are launching a strike against them. I have selected you to lead this strike, <player name redacted>. [?]

Proceed to the indicated location. Eliminate all commandos. Obtain any information possible." (images 24-25; a line at the join may be hidden [?])
3. Operator 2:18:18 AM: "Looks like Gray found you a real hornets' nest, <player name redacted>. I'm reading a lot of hostile signs in there."
4. Operator 2:21:10 AM: "That dossier looks interesting... I'll start scanning the contents."
5. Operator 2:26:13 AM: "All enemies have been accounted for!"
6. Operator 2:26:13 AM: "Goals for this area are complete. Find the nearest exit quickly!"
7. [Archived]Agent Gray 2:26:29 AM: "Your operator has completed the scan of that dossier. Very efficient... for a human.

The dossier reveals details about an individual known only as the Colonel. This person is now the *focus* of your investigation. [?]

Proceed to the next location. Eliminate all opposition and search for information on this Colonel." ("focus" in italics; images 27-28; a line at the join may be hidden [?])
8. Operator 2:26:50 AM: "I'll let you know if you find anything of interest, <player name redacted>."
9. Operator 2:28:11 AM: "Those notes look promising! Better [?] upload them to me when you get a chance." (images 28-29; a line at the join may be hidden [?])
10. Operator 2:32:53 AM: "All enemy commandos are down."
11. Operator 2:33:35 AM: "Goals for this mission phase are complete!"
12. [Archived]Agent Gray 2:33:35 AM: "Our search continues, <player name redacted>. The Colonel's exact location is carefully guarded, in a most... *methodical* manner. Multiple levels of security, and layers of encryption... very impressive. Very precise.

Regardless, we shall break it. The notes you obtained have been [?] decoded. Proceed to the next location." ("methodical" in italics; images 30-31; a line at the join may be hidden [?])
13. Operator 2:34:22 AM: "I don't really know what Gray wants you to find here. Maybe he doesn't know. I'll let you know if anything stands out, though."
GAP: no entries between 2:34:22 AM and 2:41:12 AM (log continuous; about 7 minutes of play not logged).
14. Operator 2:41:12 AM: "Bingo! Get that CD to a hardline, <player name redacted>."
15. Operator 2:41:51 AM: "Goals for this mission phase are complete!"
16. [Archived]Agent Gray 2:41:51 AM: "Your operator has forwarded some data to us, <player name redacted>. We are [?] currently...

Very interesting. The nature of this data was most... unexpected. Most...

This data must be processed further. Contact us in the near future." (images 32-33; the line at the join is only partly visible [?])
17. Operator 2:41:51 AM: "Mission Completed!"

## Objectives shown
not shown (Objectives tab never opened).

## NPCs
- [Archived]Agent Gray: contact (male).
- Operator: female portrait.
- Commandos: hostile, at sites 1 and 2 ("Eliminate all commandos", "All enemy commandos are down"); count/names/levels not shown. Site 3: whether there were enemies is not shown.
- No named field NPCs.

## Items
- dossier (site 1): found by the player ("That dossier looks interesting..."); the Operator scans it remotely, no upload logged. Holder/container not shown.
- notes (site 2): found ("Those notes look promising! Better upload them to me..."); the phase-complete line at 2:33:35 (42 s after the last commando died) suggests an upload at a hardline [?].
- CD (site 3): found ("Bingo! Get that CD to a hardline"); uploaded 39 s later (phase complete at 2:41:51).
- No "You received" lines, no Details popups.

## Rewards
- not shown (no chat panel in any image).

## Locations
- Site 1: "the indicated location", "a real hornets' nest" of commandos.
- Site 2: "the next location".
- Site 3: "the next location"; nothing else said.
- No district / map labels visible.

## Build notes (interpretation)
Phase 1 (strike the commandos, get the dossier)
- briefing: entry 2 (clean: "Because of the commandos' brazen interference in our operations, we are launching a strike against them. I have selected you to lead this strike, <name>. Proceed to the indicated location. Eliminate all commandos. Obtain any information possible.")
- enter: "Looks like Gray found you a real hornets' nest, <name>. I'm reading a lot of hostile signs in there."
- goals: Search for "Dossier" (container unknown; suggest Box or Furniture), objective "Find the dossier"; done line "That dossier looks interesting... I'll start scanning the contents." DefeatAll, objective "Eliminate all commandos"; done line "All enemies have been accounted for!" Both parallel (dossier was found before all enemies died).
- enemies: commandos (Zion-aligned humans); many ("hornets' nest"). Site: Warehouse or Derelict [?].

Phase 2 (find information on the Colonel)
- briefing: entry 7.
- enter: "I'll let you know if you find anything of interest, <name>."
- goals: Search for "Notes" (container unknown; suggest Safe or Furniture), "Find the notes"; done "Those notes look promising! Better upload them to me when you get a chance." Upload "Notes" at a hardline ("Upload the notes"), waits on the Search; DefeatAll "Eliminate all opposition"; done "All enemy commandos are down." Phase ended with "Goals for this mission phase are complete!" (not "...area..."), consistent with the last goal being the hardline upload.
- enemies: commandos; several. Site: Office or Normal [?].

Phase 3 (the CD)
- briefing: entry 12 (clean: "...The notes you obtained have been decoded. Proceed to the next location.")
- enter: "I don't really know what Gray wants you to find here. Maybe he doesn't know. I'll let you know if anything stands out, though."
- goals: Search for "CD" (container unknown; suggest Computer or Safe), "Find the CD"; done "Bingo! Get that CD to a hardline, <name>." Upload "CD" at a hardline ("Upload the CD"), waits on the Search.
- enemies: not mentioned; probably commandos again [?]. Site: Office [?].
- finish: entry 16 (clean: "Your operator has forwarded some data to us, <name>. We are currently... Very interesting. The nature of this data was most... unexpected. Most... This data must be processed further. Contact us in the near future.")

Mechanics: the dossier is scanned by the Operator without an upload; closest playable is a plain Search goal (or Search + Upload for consistency with phases 2 and 3).
