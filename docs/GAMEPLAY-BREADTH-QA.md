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

### NPC chase around the Mara corner

1. Start `npc-corner-chase`. CSR starts at the far-side Mara marker `20218,95,-9510`.
2. Stay at spawn and watch the Blackwood Goof come around the corner from `18924,95,-8852`.
   Check that ordinary interlock starts without any movement input.

This fixture widens the ordinary aggro radius to 15 m. The bootstrap checks the live Goof,
the blocked shortcut, the grounded route, and CSR's seeded position. Its higher-level
observer leaves the Goof at its post for CSR. The `world-npc-corner-stationary` scenario
checks the automatic approach without a client movement packet. The moving-player scenario
and the railing-gap regression remain separate checks.

The server plans portal waypoints for a blocked pursuit and checks the final approach
before starting melee. The 7.6005 `NPC_BASE` template stores `CharMvt/Walk Speed`
as a raw 1.0 with no known world-unit conversion. It has no stored `CharMvt/Run Speed`.
The shipped `NPCMovementSpeedAbility` 809 targets the movement stat without a stored
magnitude. Pursuit keeps the existing server default of 4 m/s.
