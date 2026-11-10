# Mach3-2-2: A Veiled Communique

Source: Machine Criticals/Mach3-2-2, screenshot_78..screenshot_89.jpg (10 images, all read; numbers 79 and 80 are not in the folder)

- Title (Current Mission bar): [Machine Archive 3.2.2] A Veiled Communique
- Contact: [Archived]Agent Gray (header "Mission Summary. Datalink established."). No field NPC speaks.
- Player: name redacted (white boxes) in the lines; HUD not visible (all images are crops of the Mission window or of a Computer popup). In-game time span 10:51:17 AM - 11:01:51 AM.
- Mission description: not shown (Summary tab never opened).
- Image 78 is a crop of a "Computer" popup (no timestamp); images 81-89 are the Log tab scrolled top to bottom, taken at the end.

## Log in order
1. Operator 10:51:17 AM: "Mission Started!"
2. [Archived]Agent Gray 10:51:17 AM [contact, briefing]: "Good day, <player name redacted>. There are... *developments* concerning the listening device you planted for us. Your attention is again required.

We seem to be picking up a good deal of information regarding a certain awakened human. The name given is Veil. Our records show a reference to Veil: a Zion operative who betrayed the crew of the SS Novalis and was sentenced to imprisonment by the Zion High Council.

Proceed to the location indicated. We must know why her name is surfacing." ("developments" in italics; images 81-83, joins look continuous)
3. Operator 10:52:00 AM: "Apparently some of those communications went through this location. Search all the computers for a start -- we need something more to go on."
GAP: possible entries between 10:52:00 AM and 10:56:36 AM (join between images 83 and 84 looks continuous, but 4.5 minutes passed; probably computer searches that logged nothing).
4. Operator 10:56:36 AM: "Bingo! Snag that file..."
5. Operator 10:56:52 AM: "Okay... I think that's all you're going to turn up there. Get outside and upload that file at a hardline, and we'll go from there."
6. Operator 10:58:09 AM: (only the header is visible at the bottom of image 84; probably "Goals for this area are complete..." or a hardline upload line [?])
7. [Archived]Agent Gray 10:58:09 AM: "The file you obtained has been analyzed, along with the data from the computers you searched. There is a 98.6% probability that the next location is of interest to us.

Proceed there and obtain any information possible." ("98.6%" printed with a small degree-like sign "98.6*" [?])
GAP: possible entries between 10:58:09 AM and 10:58:59 AM (join between images 85 and 86).
8. Operator 10:58:59 AM: "Looks like the same deal here -- search everything and find out anything you can."
9. Operator 10:59:40 AM: "Wait... I'm getting some interesting stuff up on my screens. Head through that door as far as you can, and you should find a computer. It looks like it [?] might be a central communications terminal. Take a look at it and see what you find." (images 86-87; one line at the join is hidden [?])
10. Operator 10:59:52 AM: "Whoa! Sounds like a jailbreak being planned, or maybe it already happened? Well, that's enough for now. Better get out of there."
11. Operator 10:59:5[?] AM: (only the header is visible at the bottom of image 87; probably "Goals for this area are complete. Find the nearest exit quickly!" [?])
GAP: possible entries between 10:59:52 AM and 11:00:24 AM (join between images 87 and 88).
12. Operator 11:00:24 AM: "Uh oh, looks like a lockdown! You'd better figure a way past that locked door."
GAP: possible entries between 11:00:24 AM and 11:01:51 AM.
13. [Archived]Agent Gray 11:01:51 AM: "So. It would appear that the masked humans will be... taking Veil into their own custody. An intriguing development indeed.

Remain in contact, <player name redacted>. We must maintain control of all possible ramifications." (images 88-89)
14. Operator 11:01:51 AM: "Mission Completed!"

## Computer popup (no timestamp)
- Image 78, "Computer": "> Transmissions received from offset
+1 +20 +2 +10
> _" (probably the central communications terminal of phase 2 [?])

## Objectives shown
not shown (Objectives tab never opened).

## NPCs
- [Archived]Agent Gray: contact (male).
- Operator: female portrait.
- Veil: mentioned only (female: "her name"; Zion operative who betrayed the SS Novalis crew, imprisoned by the Zion High Council). Not met.
- No field NPCs or hostiles named. "the masked humans" are mentioned.

## Items
- "that file" (phase 1): taken from a computer ("Snag that file"), uploaded at a hardline. Name not shown.
- No Details popups shown.

## Rewards
- not shown (no chat panel in any image).

## Locations
- Phase 1: "this location" through which "some of those communications went"; several computers.
- Hardline outside (upload).
- Phase 2: "the next location"; several computers, a door leading to "a central communications terminal"; a lockdown with "that locked door" on the way out.
- No district / map labels visible.

## Build notes (interpretation)
Phase 1 (search the relay site)
- briefing: entry 2 (clean, "98.6%").
- enter: "Apparently some of those communications went through this location. Search all the computers for a start -- we need something more to go on."
- goals: Search Computer for "Encrypted File" [name unknown; suggest "Intercepted File"] ("Search the computers"). Found: "Bingo! Snag that file..." Then: "Okay... I think that's all you're going to turn up there. Get outside and upload that file at a hardline, and we'll go from there." Then Upload the file at a hardline (waits for the search). Suggested objective text: "Upload the file at a hardline".
- enemies: not shown (none mentioned). Site: Office or Normal [?].

Phase 2 (central comms terminal)
- briefing: entry 7.
- enter: "Looks like the same deal here -- search everything and find out anything you can."
- goals: (optional decoy computers) then Use Computer "central communications terminal" ("Examine the communications terminal"). Trigger line before it: entry 9 (clean: "...Head through that door as far as you can, and you should find a computer. It looks like it might be a central communications terminal. Take a look at it and see what you find."). Computer text: image 78. Done: "Whoa! Sounds like a jailbreak being planned, or maybe it already happened? Well, that's enough for now. Better get out of there."
- enemies: not mentioned; probably masked humans guarding [?]. Site: Office or Warehouse [?].
- finish: entry 13 (clean: "So. It would appear that the masked humans will be... taking Veil into their own custody. An intriguing development indeed. Remain in contact, <name>. We must maintain control of all possible ramifications.")

Mechanics not expressible: the post-completion "lockdown" with a locked door ("figure a way past that locked door") - closest playable: ignore, or add a Use goal on a door panel/computer before exit. The hint "Wait... I'm getting some interesting stuff up on my screens" seems triggered by progress (perhaps after searching some computers); closest: say it as the enter line of a second goal.
