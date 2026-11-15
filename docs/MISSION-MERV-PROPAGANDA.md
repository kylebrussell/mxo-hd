# Propaganda final-building count

Mission301202, Merovingian1.2.2, Flood/org3, now requires3/5 existing Box uses across two buildings. The first retains the authored minimum of3: the archived line only says "at least three". The final-building line explicitly says five. Previously XML truncated it to3 using an obsolete capacity assumption. Final objects/goals4 and5 are now mandatory; neither3 nor4 completes that area.

Source: [archived transcript](../research/missions/transcripts/Merv1-2-2.md), screenshots54-59. Both briefings, camera done line, enemy counts3/5, Normal sites, rewards and Box representation remain unchanged. Objectives tab was never shown: ordinal objective labels remain authored. Rewards68750XP/91000info are borrowed authored values under current scaling, not the captured525000info at level50 or reconstructed retail rewards. Camera detection/guard timing and looks remain substitutions.

## Diagnostic

Discovered NativeQa mode and headless case: `merv-propaganda-count`. Normal CSR110/org3 level50/XP50 fixture at17000,495,2300, existing abilities2/17/1006/77, no GM/forced outcome. Existing layout-only seed301101 is reused without search. Exactly21 earlier receipts:1201/1204/1203/1205-1208,2101-2110,301101-301103,301201. Selected301202 is never granted.

One-pass native checklist: Call Flood > Critical > Propaganda; accept the actual title. In the first building, approach and normally Open each of3 Box proxies, exit. In the final building, approach and Open each of5. Confirm after3 and4 that the area remains unfinished; after5 exit normally and observe completion/reward. This checks current proxy usability; it does not validate real poster assets, wall attachment or Destroy2/event30.

Headless uses the unchanged shared normal contact/title/accepted-ID driver and mandatory unique preteardown raw/debug, RPC/all03, source DLL/config/hash, active payload and receipt evidence. It must earn8 goal-done records and one completion/receipt. Fresh execution passed as recorded below.

## Offline allocation and limits

Actual normal Choose/PlaceObjects with seed301101 and fixed17000/2300 input per phase allocates3+5 distinct sources without forced spots. First phase selects1412 keys4be00166/4be003fd/4be00405. Second selects1415 keys0d100314/0d1003dc/0d1002f1 plus furniture326/key0d1002f4 and1448/key0d1003db. All produce548. Temp/mxo-merv-propaganda-offline-selection.json preserves full source/pose inventory. Fixed offline phase inputs are not a live transition replay; actual accepted-stage choices will be recorded separately.

The proxy/source policy is existing OURS, not a proved retail7914 mapping. No mapper, anchor, pivot, model, chooser or runtime change is included. Native model picking, wall/room reach and walking remain unverified. StillNpcs suppresses unsolicited aggro; movement helpers can cross walls, so headless does not prove ordinary attack survival or native reachability. No claim of exact retail rewards/content mechanics.

Validation before fresh: full 4,574 tests passed; 16 focused tests passed (including normal offline allocation). Both actual Debug/Release builds and read-only fixture plans/provenance passed. Python audit/policy tests 12 passed; executable all-module audit and current ignored artifact generation/roundtrip passed. Historical tracked Markdown/coverage unchanged. Regeneration exposed seven already-stale master CSV rows unrelated to this mission; tracked CSV restored rather than including unrelated changes. Fresh proof is recorded below; native remains pending.


## Actual normal-wire proof

At frozen source `5161be52c014a7add062a9f1225e13bafb1bdca6`, the single serial `merv-propaganda-count` case passed in 32.5 seconds, exit 0, peak one server; normal disposal completed. All eight actual GO548 creations and quaternions match the offline inventory above. They are free0c records without a source key: source attribution is the offline selection/unique pose match, not direct wire binding proof. Actual boxes were used normally through Open/use3. Eight goal-done records and one80A2 completion were received; receipt301202 count1, final active payloadNULL, protocol problems0. Final goals4 and5 both ticked before the final area completed.

Required preteardown evidence: `Temp/mxo-merv-propaganda-mxotest_run_1_0132e9048c.json` (109 RPCs,27 view lists; SHA256 `E2ED07BF9B483C7B699DDF313B90E1A1B812591D340D04EF699C7D67CEB9B22F`) and sibling `-debug.log`. Durable stdout/exit: `Temp/mxo-merv-propaganda-fresh-5161-stdout.txt` / `-exit.txt`. Existing WireFormat.FindCreations decoded actual records to `Temp/mxo-merv-propaganda-live548.json`.

The Debug harness selected the newer **Release** server DLL using its normal selection policy: own worktree `hds/bin/Release/net8.0/hds.dll`, SHA256 `47ACE9E5DBC4ADA27926F52C5F5073EDD5667A17A9B0F4CA319A529940472EC2`. Both configurations were built/reviewed before the run. No source/build changes occurred while it ran. Native Box/source picking, walking and real poster representation remain unproven; this headless proof does not clear those gates or ordinary attack survival.
