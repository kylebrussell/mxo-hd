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
