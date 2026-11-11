# Native ability QA fixture

This prepares a disposable server and test database. It does not control the real client. Use Computer Use for native input and observe the resulting UI; helper-generated requests are headless coverage.

Build `hds` and this project in Debug first. Run from this checkout:

```powershell
dotnet tools/NativeQa/bin/Debug/net8.0/NativeQa.dll <checkout> <new-output-directory> <build-label> [npc]
```

It creates `C:/Users/russe/Developer/mxo-client.lock` atomically with CreateNew and exclusively holds it, respecting both other agents' text reservations and open handles. An existing reservation stops startup before database or server work; coordinate through the handoff rather than deleting it. It records acquisition/release in the shared handoff, snapshots the server binaries, and uses native ports 11000/10000. The account `CSR_AbilityQA` accepts a placeholder password and exposes only a disposable copy of CSR_Tester (110), with level-50 abilities. The optional `npc` argument starts beside the test NPC cluster; otherwise it starts at Mara. No other main-database character is read or modified.

Wait for `state.json`, launch the existing client with `-nopatch -noeula`, and enter as CSR_Tester. Create an empty `helpers` file in the output directory to connect QAFriend, QAFar, QAOther and QAEnemy. The enemy is durable enough for control/gunfire checks; it must issue `?pvpflag on` after entering the world.

Place individually named JSON files under `requests/`. Each produces a corresponding `responses/` file. For example:

```json
{"actor":"QAEnemy","action":"cast","ability":315,"target":"CSR_Tester"}
```

Supported helper actions: `chat` (`text`), `cast` (`ability`, `target`), `ranged` (`target`), `stop`, `move` (`x`, `y`, `z`), `invite` (`target`), and `accept` (`inviter`). Optional `delayMs` delays that request by up to 30 seconds. Requests act only through the named headless helpers. `events.jsonl` records selected server diagnostics; timestamps are batch-read times, not precise effect timestamps. `server.log` retains full output.

Close the real client and create an empty `stop` file for orderly shutdown. The finally block resets main CSR_Tester to Mara `(23828,495,13984)`, full health/IS, PvP off and mission 986 only, verifies the result in `stopped.json`, appends the handoff and releases the lock. Verify `stopped.json` and the absent lock before another native session. An unhandled process termination requires manually running the same reset through the test harness; do not assume cleanup ran.
