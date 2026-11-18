Use mode `gameplay-breadth`, CSR_Tester110 only; keep the same fixture throughout.
Setup starts at level2/XP4900 with Awakened, seeded wander, and a 25HP mission target for the team check.
Watch a nearby Chopper walk without endpoint jumps, then stand still for 8–15 seconds between legs.
Select a walking Chopper and open its radial. Verify it stands still while selected and resumes wandering after you clear selection.
Kill one level1 Chopper normally: expect +335XP, level3/5235XP, MaxHP250 and MaxIS106.
Quit/relog without reseeding; the earned level and XP must persist, with no repeated gain.
Use the printed chase coordinates; watch a hostile approach over the floor before interlock, then withdraw normally.
For the team retest, use `?tp 23828 495 13984 0 0 0`, verify QAEnemy is alive, and invite it to your mission team.
Call Flood and accept Team Progress Fixture; keep your own attacks off Team Security.
Tell QAEnemy `assist`; it accepts your invite and attacks through ordinary combat.
Goal1 must complete on your mission panel; talk to Team Contact for exactly one completion.
Shared helper HUD/item interactions and retail movement-speed tuning remain open.
Claude owns native QA, lock, reset and merge; reset110 to Mara, full health, PvP0 and mission986 only on release.

## Team HUD and shared mission loot

Use `team-breadth` and wait for READY. QAFriend starts near CSR at Mara
Northwest. Submit helper requests as JSON in the fixture's `requests` directory.

1. Invite QAFriend to the mission team and send
   `{"actor":"QAFriend","action":"team-breadth","step":"accept"}`.
   Verify QAFriend's name and health in the team HUD.
2. Call Flood and accept **Team Breadth Fixture**. Send
   `{"actor":"QAFriend","action":"team-breadth","step":"assist"}`.
   Watch the helper's health during ordinary combat. Keep CSR's attacks off
   Team Guard so the helper earns the kill.
3. Check that the kill ticks CSR's goal and opens the Loot window with a Dossier.
   Take All, then Give the Dossier to Team Receiver. Verify the item leaves
   the bag and the mission completes once.
