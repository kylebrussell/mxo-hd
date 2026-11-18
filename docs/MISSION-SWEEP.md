Run `dotnet tools/HeadlessClient/bin/Release/net8.0/HeadlessClient.dll --mission-sweep --parallel 8 --logs <output>` after building Release.
The sweep reads every XML in `data/missions`, isolates each mission, and continues after failures.
Earlier mission receipts, level 50, loaded combat abilities and entry tickets are fixture setup; the selected receipt is earned.
PASS requires all required objectives, a completion summary, one selected receipt and no active completed payload.
`mission-sweep.csv` is the mission table; `counts.md` groups failures by class. Both update during the run.
Movement and weaken commands accelerate testing; headless passes do not establish native rendering, picking or collision.
Use before/after complete sweeps for shared fixes; Claude checks three to five representative missions per class.
Set `MXO_QA_MISSION_ID` and use NativeQa mode `mission-catalog-sample` to check any catalog mission through the ordinary UI.
Native samples: 1205 (escort elevator), 104101 (three followers), 102301 (lobby cars), 2201 (props); complete normally once.
Claude owns the native client lock, character reset and merge.
The baseline uses assisted street travel; native samples establish visible doors, walking and picking.

### Direct mission samples in one Native QA login

Set `MXO_QA_MISSION_SAMPLES` to comma-separated `mission:area` pairs and start
`mission-catalog-sample` with a fresh fixture directory. Areas are one-based. Clear
`MXO_QA_MISSION_PHASE` for a direct-sample session.

```powershell
$env:MXO_QA_MISSION_SAMPLES = '205204:2,205301:3,203305:4,308203:4,303105:1'
```

Wait for `state.json` to say `Ready`. Bootstrap starts each listed sample on the
disposable CSR_Tester, checks its staged checkpoint, required actors and props,
and aborts it normally before logging out. `mission-samples-bootstrap.json`
records each staged area, available entrance and mission-object approaches. A failed sample blocks readiness.
The catalog fixture disables ambient aggro. The aggro-enabled team fixture checks
its required helper at startup and refuses readiness if an ambient NPC attacks it.

In the native client, type `?missionphase 205204 2`. It accepts the exact mission ID
and starts the requested area without a contact. The chat confirmation discloses
skipped areas and staged carry-over. The command is enabled only for the live
disposable CSR_Tester in the catalog profile. Finish or abort the active mission
and leave combat before entering the next command. Use the same district and
start from the fixture's hardline if you want the same site selection as the
bootstrap probe.

Direct starts stage carry-over from authored goals. They leave the requested
area's goals open and do not award earlier receipts or rewards. A direct start
can pick a different site than a continuous run. Watch late arrivals after their
normal triggers. When no measured public entrance is recorded in Slums,
the console can print an assisted lobby approach for the selected car. That
approach skips the public door and must be recorded as assisted. Use the ordinary
elevator from that lobby.

After the actual phase starts, the console prints `READY Mission object` lines for its selected site.
Use that output when the bootstrap picked a different site. Each verified point gives world coordinates,
the yaw in hex and a copyable `?tp` command with the NAV sector, region and polygon. Board the ordinary
elevator before using an object approach. Record the teleport as assisted and still use the native radial
to search, loot or use the prop. For 205301 area 3, search and loot Furniture first, then use Computer.

### Earned checkpoint from earlier areas

Use `MXO_QA_MISSION_ID` and `MXO_QA_MISSION_PHASE` for one mission when the
actual earlier history matters. Clear `MXO_QA_MISSION_SAMPLES` first. Bootstrap
accepts the selected ID, plays earlier areas headlessly, and quits before the
selected floor. It uses the ordinary ticket path for ticket missions. Follow
the printed entrance after native login. `mission-phase-start.json` records the
earned scope. Clear both phase and sample options for a fresh contact-offer fixture.
