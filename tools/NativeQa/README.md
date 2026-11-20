# Native ability QA fixture

NativeQa prepares a disposable server and test database. Observe the real client to verify native behavior. Helper-generated requests provide headless coverage.

Build `hds` and this project in Release first. Run from this checkout:

```powershell
dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll <checkout> <new-output-directory> <build-label> [npc|outdoor]
```

It creates `C:/Users/russe/Developer/mxo-client.lock` atomically with CreateNew and exclusively holds it, respecting both other agents' text reservations and open handles. An existing reservation stops startup before database or server work; coordinate through the handoff rather than deleting it. It records acquisition/release in the shared handoff, snapshots the server binaries, and uses native ports 11000/10000. The account `CSR_AbilityQA` accepts a placeholder password and exposes only a disposable copy of CSR_Tester (110), with level-50 abilities. The optional `npc` argument starts beside the test NPC cluster; otherwise it starts at Mara. No other main-database character is read or modified.

Wait for `state.json`, launch the existing client with `-nopatch -noeula`, and enter as CSR_Tester. Create an empty `helpers` file in the output directory to connect QAFriend, QAFar, QAOther and QAEnemy. The enemy is durable enough for control/gunfire checks; it must issue `?pvpflag on` after entering the world.

Place individually named JSON files under `requests/`. Each produces a corresponding `responses/` file. For example:

```json
{"actor":"QAEnemy","action":"cast","ability":315,"target":"CSR_Tester"}
```

Supported helper actions: `chat` (`text`), `cast` (`ability`, `target`), `ranged` or `melee` (`target`), `stop`, `move` (`x`, `y`, `z`), `invite` (`target`), and `accept` (`inviter`). Optional `delayMs` delays that request by up to 30 seconds. Requests act only through the named headless helpers. `events.jsonl` records selected server diagnostics including combat-view rounds; timestamps are batch-read times, not precise effect timestamps. `server.log` retains full output. The `outdoor` start at `(26000,495,-1498)` provides clear space to watch helper-versus-helper combat as a native bystander.

Close the real client and create an empty `stop` file for orderly shutdown. The finally block resets main CSR_Tester to Mara `(23828,495,13984)`, full health/IS, PvP off and mission 986 only, verifies the result in `stopped.json`, appends the handoff and releases the lock. Verify `stopped.json` and the absent lock before another native session. An unhandled process termination requires manually running the same reset through the test harness; do not assume cleanup ran.

## Parallel Windows fixture

Run the second fixture with a fresh output directory and a separate IPv4 loopback address. The stock client keeps auth TCP 11000, margin TCP 10000 and world UDP 10000. A fixture on `127.0.0.2` can use those same ports while the primary fixture serves on `127.0.0.1`.

```powershell
$repo = (Get-Location).Path
$run = Join-Path $env:TEMP "mxo-native-secondary-$((Get-Date).ToString('yyyyMMdd-HHmmss'))"
dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll "$repo" "$run" <build-label> <mode> --bind-address 127.0.0.2
```

After `state.json` reports Ready, run the client launcher in another shell. It verifies the fixture PID's listeners at the published address and ports. It reuses the dedicated client at `$env:USERPROFILE/Developer/mxo-client2/MxO` and updates that copy's `autoexec.cfg` for the run. Set `-SecondaryClientDir` to use another existing plain copy. The launcher reserves the copy while its client is running. `-PrepareOnly` updates the configuration without starting a client. The launcher requires both `127-0-0-2.nip.io` and `Recursion.127-0-0-2.nip.io` to resolve solely to the fixture's loopback address.

```powershell
./tools/RealClient/Start-NativeQaClient.ps1 -FixtureDirectory "$run"
# Log in using the account from state.json and a placeholder password.
./tools/RealClient/Start-NativeQaClient.ps1 -FixtureDirectory "$run" -Stop
New-Item -ItemType File -Path (Join-Path $run 'stop')
```

The secondary fixture uses the `Codex_SecondQA` account and its own CSR_Tester110 row, database, snapshot, logs and lock. Its shutdown drops only its disposable schema. The client launcher uses a windowed 800×600 config and records the owned matrix PID. Send posted input only to that PID's window. The existing `Invoke-RealClient.ps1` game click, double-click, right-click and drag switches move the real cursor. The secondary launcher records Defender's baseline and checks it after stopping the owned client.

## Read-only fixture environment check

`dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll --fixture-plan ignored-actions` prints the actual environment used by the server launch and exits before paths, locks or database setup. The ignored-actions mode must report `MXO_DEV_COMMANDS=1` and `MXO_GM_CHARACTER_IDS=110`. Explicit QA modes `remaining`, `ignored-actions`, `coder-pets`, `styles`, `data-mining`, `death-presentation`, `actions` and `health-callbacks` enable dev commands; default and unknown modes disable them. Only ignored-actions grants GM to copied110. This repairs a fixture configuration regression and does not change any reported native observations. Configuration comes from one shared plan; CLI regression tests inspect that plan without running a fixture.

## EMCP Blast partial fixture

Claude-only mode `emp-blast` owns2,17,1025 and starts at the NPC fixture with exact dev commands enabled. Read-only preflight: `dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll --fixture-plan emp-blast`. `?combatdepth empweakness on 6000` sets only the selected NPC marker and explicit fixture HP in a disposable `mxotest_` database; `off` removes it. This does not establish production marker owners. See [EMP evidence](../../docs/ABILITY-EMP-BLAST.md) and the pinned PR checklist.

Mode configuration and safe pending-branch ports are documented in [Modes/README.md](Modes/README.md). Read-only `--fixture-plan` now also reports the exact immutable loadout, spawn and ordered SQL stages without executing them.

For a pre-placed hotbar, supply the active ability and style IDs in order via `NativeQaModePlan(hotbarAbilities: ...)`. `--fixture-plan` and the READY output print the slot map on stock page 2. The launcher uses the retained disposable `cui.cfg` copy in Temp's `mxo-qa` folder or the file named by `MXO_NATIVE_QA_CUI_TEMPLATE`. It sends the patched copy to the disposable CSR account in the login response.
