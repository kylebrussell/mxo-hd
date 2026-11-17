Run `dotnet tools/HeadlessClient/bin/Release/net8.0/HeadlessClient.dll --mission-sweep --parallel 8 --logs <output>` after building Release.
The sweep reads every XML in `data/missions`, isolates each mission, and continues after failures.
Earlier mission receipts, level 50, loaded combat abilities and entry tickets are fixture setup; the selected receipt is earned.
PASS requires all required objectives, a completion summary, one selected receipt and no active completed payload.
`mission-sweep.csv` is the mission table; `counts.md` groups failures by class. Both update during the run.
Movement and weaken commands accelerate testing; headless passes do not establish native rendering, picking or collision.
Use before/after complete sweeps for shared fixes; Claude checks three to five representative missions per class.
