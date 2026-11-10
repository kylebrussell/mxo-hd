# Mach3-1-4: Defuse the Situation

Source: Machine Criticals/Mach3-1-4, screenshot_48..screenshot_56.jpg (9 images, all read)

- Title (Current Mission bar): [Machine Archive 3.1.4] Defuse the Situation
- Contact: [Archived]Agent Gray (header "Mission Summary. Datalink established."). Only Gray and the Operator speak.
- Player: name redacted (white boxes) in the lines; HUD not visible (all images are crops of the Mission window). In-game time span 2:55:05 AM - 3:03:49 AM.
- Mission description: not shown (Summary tab never opened).
- All images are the Log tab scrolled top to bottom, taken at the end. At most joins one text line is hidden or only partly visible; marked [?] where it may hold words.

## Log in order
1. Operator 2:55:05 AM: "Mission Started!"
2. [Archived]Agent Gray 2:55:05 AM [contact, briefing]: "The threat posed by the Colonel's explosive device is completely unacceptable. Since your last mission, we have obtained data that may allow us to disable the device. [?]

Proceed to the indicated location and locate an encrypted access code." (images 48-49; a line at the join may be hidden [?])
3. Operator 2:56:00 AM: "That code should be on site here somewhere."
4. Operator 2:56:18 AM: "Bingo! Head out of there and we'll get our next waypoint."
5. Operator 2:56:18 AM: "Goals for this area are complete. Find the nearest exit quickly!"
6. [Archived]Agent Gray 2:56:29 AM: "Excellent work, <player name redacted>. The data on that CD must be decrypted before we can use it.

Proceed to our data lab and submit the CD for decryption." (images 50-51)
7. Operator 2:57:13 AM: "Okay, there should be a decryption terminal in here. Just put the CD in and the Machine programs will do the rest for us."
8. Operator 2:58:04 AM: "All set! Now off to use the disk."
GAP: possible entry between 2:58:04 AM and 2:58:26 AM (join between images 51 and 52; e.g. a "Goals for this ... are complete" line [?]).
9. [Archived]Agent Gray 2:58:26 AM: "Now comes the most... *delicate* part of this mission, <player name redacted>. You must take the decrypted CD to a nearby transmitting station. Place the CD in the transmitting computer there, and activate a nearby targeting system.

The system must transmit the signal [?] for a certain period of time. Needless to say, the Colonel's minions will surely do what they can to... interfere with our plans. Be prepared." ("delicate" in italics; images 52-53; a line at the join may be hidden [?])
10. Operator 2:59:21 AM: "There should be a room in here with two computers. Put the CD into one of the computers, and then activate the second one, and then... get ready."
11. Operator 3:00:01 AM: "I'm reading inbound signals! Heads up!"
12. Operator 3:00:45 AM: "More hostiles coming in!"
13. [Archived]Agent Gray 3:03:17 AM: "Very good, <player name redacted>. The explosive device has been disabled. [?]

Your cooperation has proved invaluable." (images 54-55; a line at the join may be hidden [?])
14. Operator 3:03:17 AM: "Goals for this area are complete. Find the nearest exit quickly!"
15. Operator 3:03:48 AM: "There's one thing Gray didn't explain... who in the world would have the kind of access and resources to pose this sort of threat?

I don't have a good feeling about this, <player name redacted>."
16. Operator 3:03:49 AM: "Mission Completed!"

## Objectives shown
not shown (Objectives tab never opened).

## NPCs
- [Archived]Agent Gray: contact (male).
- Operator: female portrait.
- Hostiles: "the Colonel's minions" (commandos) arriving in waves during the transmission at site 3 ("I'm reading inbound signals! Heads up!", "More hostiles coming in!"); count/names not shown.
- No named field NPCs.

## Items
- encrypted access code on a CD (site 1): found ("That code should be on site here somewhere." / "Bingo!"; Gray: "The data on that CD..."). Container not shown.
- the CD is put into a decryption terminal at the data lab (site 2), then carried on as "the decrypted CD" / "the disk".
- the decrypted CD is placed in the transmitting computer at site 3.
- No "You received" lines, no Details popups.

## Rewards
- not shown (no chat panel in any image).

## Locations
- Site 1: "the indicated location" (the access code site).
- Site 2: "our data lab" (Machine facility with a "decryption terminal"; "the Machine programs will do the rest").
- Site 3: "a nearby transmitting station", "a room in here with two computers" (transmitting computer + targeting system).
- No district / map labels visible.

## Build notes (interpretation)
Phase 1 (find the access code)
- briefing: entry 2 (clean: "The threat posed by the Colonel's explosive device is completely unacceptable. Since your last mission, we have obtained data that may allow us to disable the device. Proceed to the indicated location and locate an encrypted access code.")
- enter: "That code should be on site here somewhere."
- goals: Search for "Encrypted Access Code" CD (container unknown; suggest Computer or Safe), "Find the encrypted access code"; done "Bingo! Head out of there and we'll get our next waypoint." Found 18 s after entering, so it is near the entrance and there is no fight logged.
- enemies: none logged. Site: Office or Normal [?].

Phase 2 (decrypt at the data lab)
- briefing: entry 6.
- enter: "Okay, there should be a decryption terminal in here. Just put the CD in and the Machine programs will do the rest for us."
- goals: Put the CD at the decryption terminal (Computer), "Put the CD in the decryption terminal"; done "All set! Now off to use the disk." (If the item should change into a "Decrypted CD", closest: Put "Encrypted CD" then Search the same terminal for "Decrypted CD".)
- enemies: none. Site: Office [?].

Phase 3 (transmitting station)
- briefing: entry 9 (clean: "...The system must transmit the signal for a certain period of time. ...")
- enter: "There should be a room in here with two computers. Put the CD into one of the computers, and then activate the second one, and then... get ready."
- goals: Put the decrypted CD at the transmitting computer ("Place the CD in the transmitting computer"), then Use the targeting system computer ("Activate the targeting system"), waits on the Put. Then survive waves until the transmission is done; done line Gray "Very good, <name>. The explosive device has been disabled. Your cooperation has proved invaluable."
- wave lines: "I'm reading inbound signals! Heads up!" (about 40 s after the briefing, i.e. at activation), "More hostiles coming in!" (44 s later).
- enemies: Colonel's minions (commandos), two or more waves; total time from activation to success about 3-4 minutes.
- site: Office or Warehouse [?] (a "transmitting station").
- finish: entry 15 ("There's one thing Gray didn't explain... who in the world would have the kind of access and resources to pose this sort of threat? I don't have a good feeling about this, <name>.") - spoken by the Operator, not Gray; Gray's last line is the success line.

Mechanics not expressible: a timed transmission with hostile waves (hold-out). Closest playable: after the Use goal, spawn commandos and a DefeatAll goal ("Defend the transmitting station"); optionally Protect a terminal-operator NPC if objects cannot be protected.
