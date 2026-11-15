# Generated escort exit

A generated-floor exit escort could finish beside the interior elevator. `AtExit` accepted proximity to either floor or street door. Before the goal finished, the ordinary floor-door request selected the mission floor and returned “already on that floor.” The next tick advanced the mission while the player and follower stayed upstairs.

The retained 50755f6a failure is evidence, not a successful exit: its only final-session own-view teleport was `54200/2895/-16700`; no street teleport followed. NPC view4 movement was not an own-player teleport. No ride was scheduled or canceled. The authored 101210 transcript explicitly says the waypoint updates once outside.

Generated exit escorts now reuse committed departure evidence (or the existing far-away rule). With the current exit goal open and its actual living followers already following and within the unchanged 1500-unit escort check, use of the current generated floor door may request the ordinary downride before `waitingForExit`. The committed ride carries followers; the next normal tick can finish the goal. No early goal/flag mutation, source changes, new ranges, timer changes or legacy exit changes.

## Native one-pass check (unproved)

Run `NativeQa --fixture-plan mission-generated-escort-exit` first. Start that mode in the normal disposable CSR110 NativeQa fixture. Earlier 29 receipts only, level50 and normal abilities; layout seed101208 is a disclosed development aid. No active mission, chosen source, mission item or selected receipt is seeded.

1. Phone > Tyndall > Critical > Interview With a Bluepill; accept normally.
2. Follow the ordinary waypoint, hover/use the actual street elevator and enter. Walk to Maynard, Talk and confirm he follows.
3. Walk back to the floor door with Maynard nearby. The exit goal and waypoint must remain unchanged while still upstairs.
4. Hover/use that ordinary floor door. Confirm the downride reaches the street with Maynard before the exit goal completes and Lonny’s waypoint appears.
5. Use Lonny’s ordinary door, escort Maynard to him, Talk, then ordinary exit/completion. Verify one receipt/reward; repeat use must not duplicate it.

Stop on a missing door, unusable room, missing follower or premature goal transition. No mission-floor teleport or source reroll establishes native passage. Current native hover/passage and complete mission remain unproved. The separate PR251 V1 restore fix is needed for the retained combined218 carry/relog path; this focused native check starts from a fresh offer and does not claim carry restoration.

## Validation

Pending source qualification. The retained combined218 failure is 159.8s/exit1, schema count0/all31 recorded process identities gone; no retry was used to erase it. The standalone V1 restore PR251 remains separately qualified.
