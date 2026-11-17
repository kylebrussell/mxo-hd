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

### Start at a later area in Native QA

Set a single mission ID and a one-based area before starting `mission-catalog-sample`:

```powershell
$env:MXO_QA_MISSION_ID = '208302'
$env:MXO_QA_MISSION_PHASE = '4'
# Inspect the plan without starting a server or reserving the native client.
dotnet tools/NativeQa/bin/Debug/net8.0/NativeQa.dll --fixture-plan mission-catalog-sample
```

Use the usual NativeQa startup with this environment and a fresh fixture directory. Wait for
`state.json` to say `Ready`. Bootstrap signs into the disposable copy of CSR_Tester, accepts the
real offer, plays the earlier areas with headless assistance, and quits before entering the
selected generated floor. The server keeps the earned goals, inventory and followers. Use the
printed entrance and finish the selected area in the native client. For Of Two Minds area 4,
check Green and Jones are following, use the lobby car, then bring them to Pace and finish with
Morpheus. This starts after area 3, so use an ordinary full run to investigate the earlier areas.

Bootstrap requires the selected checkpoint, its fixed props and actors, and any previously
recruited escorts required by the new area. Scripted late arrivals remain tied to their triggers.
A preparation failure reports `Blocked` with the step that failed. `mission-phase-start.json`
records the staged scope and the number of earlier areas played headlessly. Clear
`MXO_QA_MISSION_PHASE` to restore the ordinary fresh-offer session.
