# Attribute allocation authority

Ordinary `80 b5` now validates and commits the entire attribute-point batch. It locks the actual character EXP and five reserved attribute slots, checks one shared point budget and the native shown-value cap, and preserves existing physical row IDs and raw typed descriptors. No Info is spent. Validation or pre-commit SQL failure leaves every attribute and cash unchanged.

## Native contract

Client DLL SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; GOB SHA256 `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`. Exact source index: [attribute-allocation-native.json](../research/protocol/attribute-allocation-native.json).

Confirm620c1343 calls producer621d4680. The request body is offset4, count u16, then unique six-byte records: discriminator0, full AttributeID u32, positive Points u8. Only the five actual attributes are accepted, with exact whole-body length. A malformed tail never commits its valid prefix, and high ID bytes cannot alias a valid low byte.

Native625c5320 starts with max(character level minus1,0) and subtracts spent points. UI620c2cb4 derives the level from actual EXP through625d81d0. Requested points consume this pool one-for-one. All five GOB attribute templates78/79/81/82/84 have class1, StartingValue8 and cap30. UI620c2e7a..8c and621c9b90 apply cap30 to the **shown** attribute value, including pending points.

Response consumer621e8c30 reads Status at RPC+4 and branches to failure only for signed-positive values (621e8c53..58). The attribute-specific `0x1800000c` is explicitly recognized at621e8fbc and displays STA24000106, “Ability Level-Up Failure!”. A modal gives the actual server refusal reason. The old negative `0x9800000e` bypassed that error branch; this correction is based on the independent attribute consumer, not a global status rewrite.

Success80b6 returns one array containing normalized NewLevel. At621e8ce7..8cf5 the client adds its existing archetype contribution to reconstruct the shown value. Existing `NewLevelOf`, fixed `BaseValue` and `LegacyValue` policy is retained. No new base-stat, archetype or linked-stat formula is introduced. The normal tutorial capture around29634..29660 in `research/packet-dumps/community-intake/20260608T120313Z-hdneo-public-create-spawn-session-sanitized-selected/logs/mxopackets1_crashover2k_002_create_mxoemu_character2.log.txt` contains the actual52+1 request and status0/NewLevel9 response.

## Atomic and legacy policies

A dedicated connection locks the character first, then the five exact owned reserved slots. Every requested point is validated before any mutation. Existing rows use exact PK/raw descriptor/old-value compare-and-set. Missing rows are inserted without deleting anything. Occupied wrong-typed or duplicate reserved slots refuse; unrelated high slots cannot wrap into attributes in the world or margin reader.

Whole-batch atomicity and participating-transaction serialization are explicit **OURS** backend safety policies. No retail backend isolation or refusal-consumption capture is claimed. No new hardline, combat, Loading Area, on-world or avatar admission restriction is added. SQL is disposed before short current-session response publication. The success-only tutorial callback runs after the reply, outside the transport fence, with character/generation/world/life/transport checks. Malformed, refused or stale requests cannot queue tutorial progress.

For an accepted eligible partial snapshot, all five exact logical values are materialized in the same transaction. Unrequested missing attributes retain their **existing** LegacyValue; no new points or archetype values are invented. Existing rows retain their PK/raw descriptor. A late failure also rolls back inserted fallback rows. Refused snapshots never materialize anything or automatically untrain/repair data.

The old no-row legacy policy remains a known limitation: shown values24/27/28/25/26 imply90 spent points under existing BaseValue8/11/8/5/8, exceeding the maximum49, while the margin's historical no-row metadata advertises16. It therefore still refuses a first raise and remains unchanged on relog. This package does not invent a replacement baseline or claim that mismatch fixed.

## Claude-only native one-pass

Use discovered NativeQa mode `attribute-allocation`: CSR110, actual level/EXP50, five legitimate existing base rows,49 available points and1,000,000 Info. No dev, GM or RNG override.

1. Open Status > Attributes. Verify49 available points and Focus8, Perception11, Reason8, Belief5, Vitality8.
2. Queue one Focus and one Reason point, then confirm once. Verify shown9/11/9/5/8,47 points left and unchanged Info.
3. Relog. Verify all five values and remaining points, with no duplicate allocation.
4. Native cap and failure UI remain checks for Claude; forged illegal requests are headless authority tests, not a claim that the normal UI offers them.

## Validation and retained evidence

The full Release suite passed **4,660 tests** on fresh-tested source `e44b6fe21bf123b18080326420f00ad6dc952815`, after the modal-fence correction and master206 integration. The later master208 import (`41ef78e0`) preserved 13 attribute runtime/SQL/reader/mode/driver/seed Git blobs byte-for-byte; only the shared harness helper conflict needed a union. Final combined focused tests passed **118**. Both actual Debug/Release builds, read-only mode plans and selected-DLL provenance passed after that import. No combined fresh pass or final-head full-suite rerun is claimed from this pure upstream merge.

The corrected normal-protocol case **passed in 62.5 seconds, exit 0**, with one disposable server, which was disposed before the upstream merge. It proved:

- One two-attribute batch and one whole-batch response; cap refusal; a late second-write failure rolling back the first update; normal retry; original PK/raw descriptors/load flags preserved; no Info delta/current or cash debit.
- Eligible partial values24/11/8/5/8 becoming24/11/9/5/8, all five rows materialized,17 total points spent and0 remaining at level18; all values and margin metadata preserved after normal logout/relog.
- The no-row level50 legacy refusal creating no rows, with unchanged logical values and historical16-point metadata after relog. Its90-versus16 limitation remains unresolved.
- Two real participating connections competing on **different** attributes for one shared final point, with exactly one winner; a late failure rolling back a newly inserted fallback row; typed physical replacement PK2079 to2084 refusing without overwriting; persisted EXP and duplicate-row refusal.

Actual selected fresh DLL: **Debug**, SHA256 `45f824c4af2245fa1d86e268eb3f8d3d098ae4024f4f6fc6711772b69ad01781`, unchanged after disposal. Final post-import build hashes are Debug `c61096b6231d64bad5b69e98c26b8fecbf67dd215918003199a937a4f969470d` and Release `5fbf123cba681bc220baf99555d84bd1305117b4a8a1061c8bc784e4a17df39f`; these were built/inspected, not rerun in fresh.

Required raw proof was saved before teardown in the owner's Temp directory:

- `mxo-attribute-allocation-mxotest_run_1_f1b7d11bb8.json`: SHA256 `1b5d9d2f748f63b69b1b38e64bb1ee3df0e507fcca5c5b19223cbb286e1ee4d9`.
- Matching `.log`: SHA256 `27674203f4702b43291f2fe420b42aa6b1b5237b671e630c65f1e5997ad7c6ae`.
- `mxo-attribute-fresh-2-stdout.txt`: SHA256 `7ebc216e83a2ed73eb53e12751bbc12e0406336ba96201401b07f29881e58cad`, including explicit exit0.

Seven sessions retained60/56/56/55/55/55/22 RPCs and zero protocol problems, including actual pre-login PK/raw snapshots and both refusal/success80b6 records. Native UI/visual/quote validation remains pending Claude.

The first attempt remains a **40.0 second fixture failure**, before any allocation. Initial UDP setup queued legacy values before the old post-login seed. MultiProtocolManager173..178 calls processPlayerSetup immediately after its UDP reply. The corrected optional `AttributeValues` seed is installed and recorded before LoginAsync; null preserves all other scenario defaults, and the initial-value assertion was not relaxed. Failed proof remains `mxo-attribute-allocation-mxotest_run_1_17e7de0c6d.*`, JSON SHA256 `8af3f16bc5000594619a81ba49f6771ca28daecc8709dbfa4ee4f5555d00e723`, log `99fcec5bc641160a2f5dfe0adf2c824933be008c102bf7a1f83783b746a2efb1`.

A source review also found that the old system-chat sender flushes internally. Refusal bytes are now built by the shared pure chat builder before the transport gate; that gate only enqueues world-bound modal/reply bytes. Flush and tutorial callbacks remain outside, with unit tests proving another thread can enter the fence during both callbacks.

Isolation is scoped to participating attribute allocations. At validation base `41ef78e0`, the old separate80b7 training route could still address hidden attribute rows; pending PR215 addresses that path independently and should land first. No pending215 code was imported here, and this package does not claim global integrity against every legacy writer.


Master refresh after PR215: both independent training and attribute synchronization fields are retained, and the shared pure chat encoder/wrapper is exactly the native-tested master version. Attribute runtime, SQL, readers, mode and driver remain unchanged. The original 62.5-second fresh proof remains source-qualified; this upstream merge was checked with 142 combined focused tests and both actual build/profile/provenance checks, without another fresh run. Native attribute UI verification is still pending.
