# Midphase elevator return: captured 101208 replay

This fixture replays Claude's native 101208 checkpoint, not a newly earned mission chain. Raw native MySQL batch bytes are embedded once in HeadlessClient; their doubled embedded quote escapes are translated only in this fixture before strict JSON parsing. Raw SHA256 `30363052ED5F7847D7F89CD4DD748420537CA09944F73CCDFF7264DA125D2D12`; normalized payload SHA256 `AAB1935022D4C1030189C8C8C9D0F379831D8A7F2CFCFAE21BECA16DF1B35EB9`. Runtime persistence parsing is unchanged.

Captured state: mission 101208 phase 0, accepted identity d7ae1ed76aee4d4bab40f73d1a2182d0, Started1794793133/Instance47. Done1,3,2; Upload4 open. CD6831 is owned in bag6 with raw count0. Native mode seeds only this active capture plus 27 earlier receipts before login; no101208 receipt. Level50/5000HP, ordinary2/17/1006/77, no GM/dev commands, fixed rolls or healing aids. Still-NPC/aggro-off fixture is disclosed. Headless active capture is installed after normal UDP authentication but BEFORE RPC05/WorldEntry.

## One-pass native checklist

1. Start disposable mode `mission-midphase-return`. Confirm own standing3700/1295/13100, active101208, Done1,3,2, Upload4open and CD6831bag6. Do not take/give/upload during this check.
2. Walk to exact current floor door270532686; ordinary Open must show the down dialog and land3700/95/13100. No `?tp` or `?missionfloor` may replace the ride. Record actual arrival and ACK, unchanged phase/CD/goals, no reward/receipt.
3. Normal Quit. Reconnect SAME character/fixture without re-preparing, retaining CD and lobby position. Walk/open lobby door270532869; ordinary up dialog must land3700/1295/13100. Open current floor again and ride down.
4. Optionally use the ordinary elevator panel's actualFloor2 destination; its495-standing-floor door should open normally and must not redirect to the mission floor through shaft proximity. This negative is unit-covered; the headless case does not invent a panel request.
5. Retain packet/debug logs, all raw current checkpoint/item/account/receipt rows, sessions and process/schema cleanup. Stop on wrong identity/actor/item/phase. Headless receipt proves transport delivery, not native rendering/walking or a new earned chain.

Headless case uses only ordinary80c8 door requests and normal80fe00 Quit with the existing33s reservation. Positive rides have no movement or teleport aids, flag edits, direct CommitGeneratedFloor calls or goal mutations. Each ride requires fresh exact own011c destination plus independent exact12-bit ACK/PSS from retained PacketLog. No arbitrary sleep is used as arrival proof. Account/receipt/item rows and captured site/NPC witness remain unchanged. Native mode uses its normal inherited no-op bootstrap, not an actual trip hook; the separately discovered headless case supplies trip coverage.


## Verified headless checkpoint replay

At source commit `347b2f88`, the fresh `mission-midphase-return` case passed in72.0s. It authenticated and restored the captured checkpoint normally, performed three ordinary door trips (floor down, same-fixture Quit/33s/relog, lobby up, floor down), and required fresh own-life arrival records with exact ACK/PSS receipts. Both sessions had zero parser errors. Movement aids were disabled; phase, goals, captured site/witness, CD6831, account and receipt rows stayed unchanged. This is transport and checkpoint proof, not a newly earned mission or native visibility/walking proof.

The actual NativeQa `mission-midphase-return` bootstrap also passed in24.19s. Its inherited/no-op hook proves seed/auth startup only; the separate gameplay case above proves the trips. Both disposable schemas independently counted zero afterward, and all18+8 recorded canonical process identities exited. Prior fixture-loading and Git CRLF/hash failures remain retained.

Frozen local producer: `codex-refresh-pr229-811f249d`. Actual newest source is **Release**, SHA256 `5EFA4F0CB5073B1BE4B3E5D63E876BF087F9FCF5F29B9F816FC0904183B9F791`; Release HeadlessClient `36AE40867A038F208B84B95213167BDE3D0D924DC20C71C1939D118BF820AC7B`; Release NativeQa `9CA84D1424A7C76076EAC872C61505E500A76CE8B230EF83D963E604E7287168`. Both build configurations, exact case discovery, fixture plan/build-info and247 focused checks passed. CI at347b2f88 passed6545 tests plus12 audit-policy and43 static-layer checks. Final evidence-only documentation does not change runtime, QA source, embedded fixture or binaries.

Raw local evidence: `%TEMP%/mxo-midphase-elevator-20261115/fresh-game-347b2f88`, `bootstrap-347b2f88`, and `%TEMP%/mxo-midphase-return-mxotest_run_1_65e2496bbc.json` (SHA256 `62B58EBA00AF3BE1FB61E747FF7D2A7B0E9382A9BBE03A84D2F64C111D119E8E`).
