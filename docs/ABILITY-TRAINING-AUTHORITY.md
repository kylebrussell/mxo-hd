# Ordinary ability training authority

Ordinary `80 b7` training validates the actual owned archive row, current persisted EXP, native per-ability cap and native quote. The Info debit and one physical row's new level commit together. No new abilities, load prerequisites, memory rules or acquisition paths are introduced.

## Material price corrections

These are current-client/GOB source quotes, not newly captured retail backend prices. Check the native quote before spending. The large mastery increases are deliberate source corrections:

| Ability | Training | Previous server | Native source |
|---|---|---:|---:|
| Sneak151 |1 to2|800|400|
| Spy Style1112 |1 to2|800|400|
| Improved Sneak186 |1 to2|800|260,900|
| Find Data Node621 |1 to2|800|8,000|

Existing captured prices remain unchanged: Awakened1 to2 costs800; Write Code1 to4 costs2,900; Efficient Code32 to33 costs217,800; Detection1 to50 costs8,584,800. Write Code's actual cap is15, Download Mission Map's10; an owned code does not bypass its training cap. Existing overtrained rows are not rewritten or automatically untrained.

## Source contract

Pinned client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; GOB SHA256 `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`. Primary field records and exact native code slices are in [ability-training-native.json](../research/protocol/ability-training-native.json).

Constructor621d3250 and sender621e13af..13d1 produce exactly five body bytes: ability u16, archive slot u16, positive level-count u8. Native621e131e excludes hidden slot520. The server's existing reserved attribute range starts there too; those rows are not ordinary training sources.

621cd360 reads absolute EXP from inventory+138 through threshold converter625d81d0 and identity62513700. Incoming80e5 setter621e1c70 and initial character loaders62199007/62199288 independently establish that member's EXP meaning. Use the existing PlayerProgression thresholds starting at1 and existing maximum50; cached avatar or stored Level cannot replace persisted EXP. 621c9bc0 independently checks a positive AbilityLevelCap via625161e0. Missing/zero cap bypasses only this second limit. No ReqCharLevel or loaded prerequisite rule is added here.

Native62518f00 computes each next-level price from the OLD level:

1. Start with `200 * (old + 1)^2`.
2. LevelingCost1 multiplies2,2 multiplies4, otherwise1. This property is NOT AbilityRank. The exact native bare name is absent from all1,404 canonical schema descriptor lists, so getter62516510 retains initialized byte0. No invented rank price is used.
3. If MasteryAbility is present, recursively add the MasteryBaseAbility step priced with its own AbilityLevelCap as the OLD level (625160c0/62516130/62516250). This prices a base at cap+1 without actually training or granting the base. Only186â†’151/cap50 and621â†’215/cap5 are present in the current source.
4. Store subtotal as float32, multiply the float32 UpgradeCostMultiplier and truncate toward zero through626a92d8. Sum the converted step prices for all requested levels.

The native bare UpgradeCostMultiplier name exists only for the four stored .5 rows37/151/1006/1112; all others retain the explicit float1 initializer. Qualified-only descriptors are not mistaken for bare aliases. Generic reader622ffce0 also explicitly zero-fills a descriptor beyond stored length; loader6231bd9e/bdd4/bde9 retains only stored_size. All stored aliases were audited, rather than inferring defaults merely from catalog omission.

## Ownership, failure and publication policy

Only a defined ordinary ability in one unambiguous owned archive slot can train. Signed32 and unsigned32 legacy SQL representations are accepted as identical wire bits, while the exact original BIGINT representation is preserved. Genuine 0x80000000-family descriptors accept native canonical aliases and low descriptor bits; arbitrary 64-bit values, Code Bit families, hidden/class2 rows, unknown IDs and corrupt requested levels refuse before charge. Stored level zero remains trainable. Duplicate canonical abilities across archive slots also refuse before charge as an explicit OURS safety policy: the native response resolves a slot by ability ID, not the requested slot. A loaded corrupt snapshot is refused rather than dropping unrelated passives. Ownership comes from the actual row, not a loaded grant closure.

A dedicated connection locks the character for current EXP/cash, then archive rows. It updates only the exact primary key, raw descriptor, slot and old level, subtracts Info arithmetically and commits. No database transaction spans life/transport locks, queues, passive callbacks or tutorial callbacks. No schema migration is performed; both tables must support transactions.

Atomic failure behavior is explicit OURS policy, not a claim about retail isolation: validation or SQL failure before commit leaves cash and level intact and publishes no success/grants. A committed upgrade remains durable if delivery is interrupted. Current character identity, training generation, world/life generations and transport epoch fence postcommit publication. There is no hardline, combat, Loading Area, on-world or avatar admission restriction. Current-session Info/upgrade replies do not require a live avatar; live passive/grant refresh waits for one and ordinary world-entry loads the durable result.

The committed loaded snapshot refreshes all owned loaded parents without rereading the archive after callbacks. Unloaded training does not make hidden grants loaded. The server reconciles legacy grants from the committed effective loaded level; this is not a claim that the ID-only native response can safely update duplicate archive copies. Existing207 zero-grant registration remains responsible for its observed pairs. Passive/control reconciliation runs outside the transport fence. Short fenced training packets precede the success-only tutorial callback, also outside the fence; callbacks recheck the current generation. Only a committed, published, current live success triggers the existing tutorial upgrade event; malformed/refused requests cannot silently queue tutorial goals30â€“33.

Native response consumer621e8860 treats signed-positive Status as failure. The training-only status `0x1800000c` is explicitly recognized at621e8bac and selects STA24000106, “Ability Level-Up Failure!”; the specific modal supplies the refusal reason. A negative legacy status would enter the success path and is not used here. No failed SQL operation can publish a success with level zero.

Native80e4 constructors62129e80..9eb6 and6213dc75..9c allocate10 bytes and store the absolute Info dword at+2, with the reason dword at+6. The archived tutorial upgrade confirms `80 e4 c8 00 00 00 11 00 00 00` (200 Info, reason17; [tutorial wire step33](../research/missions/tutorial-wire-spec.md)). Training therefore refuses before mutation if the resulting 64-bit arithmetic balance cannot fit0..uint.MaxValue. An initially overlimit balance is still allowed to recover into that representable range; this is not a global wallet cap or migration.

**Economic boundary:** this serializes participating training transactions. Legacy Vendor/Market/Trade/Mail/mission/loot writers using absolute cached SaveInfo are not migrated and are not claimed globally serializable. This handler never calls that absolute writer, including disconnect/reset paths. Stale postcommit publication cannot overwrite the wallet in SQL; relog reads the committed result.

## Native one-pass checklist

Use discovered NativeQa mode `ability-training`. It owns a complete stored prerequisite/folder closure in the disposable CSR110 archive. The selected rows remain unloaded; loaded Hacker begins49 with its proper loaded prerequisite chain, and Execute Program remains50. Character Level/EXP are50 with20,000,000 Info. No dev, GM or RNG override is enabled.

1. Open Manage Abilities > Upgrade and select **one** level before comparing a quote. At the ACTUAL profile levels: Sneak25â†’26 is67,600 (previous135,200); Spy Style5â†’6 is3,600 (previous7,200); Improved Sneak1â†’2 is260,900 (previous800); Find Data Node30â†’31 is199,400 (previous192,200). The separate1â†’2 source/unit examples above are not the profile's current levels.
2. Check all four quotes before spending; stop/report if any differ. Accept each intended upgrade once and verify one Info debit and the new level.
3. Write Code14â†’15 is22,500 and stops at15. Download Mission Map is already capped10. Native UI should not offer an illegal16th/11th level; forged requests are a headless authority test, not a native UI action.
4. Train the loaded Hacker49â†’50 for500,000. Its passive196 becomes200; all unrelated loaded records and Execute Program stay intact. Relog and verify the trained archive levels, Info and loadout.

These preseeded owned rows are a QA fixture, not a new vendor/drop/acquisition claim. Native UI/quote/spend/relog validation remains pending Claude. No native rendering or retail backend atomicity is claimed from a headless pass.

## Verification

The initial full Release suite passed 4,647 tests before the bounded review corrections. The final 108 focused tests passed after callback ordering, canonical ownership, native failure status and defensive wallet bounds were corrected. Both actual Debug and Release NativeQa builds, read-only `ability-training` plans and selected-DLL provenance passed. No full-suite repeat is attributed to the final fixture-only changes.

The normal-protocol disposable case passed in **57.7 seconds, exit 0**, at runtime/fixture pin `71387a043f86f981712eebdb07fba245f522adb9`. It proved the five literal profile prices, cap refusals, positive failure status `0x1800000c`, a late SQL charge failure rolling back the earlier level update with no optimistic success or Info packets, loaded Hacker 196 to 200 with unrelated Execute Program preserved, normal logout/relog, exactly one winner across two participating training connections, canonical duplicate refusal, physical row replacement refusal (PK 1583 to 1584) and persisted EXP authority. Three sessions recorded 65/37/22 RPCs, 5/4/0 view lists and zero protocol problems. The owned database independently reported cash `COLUMN_TYPE=int`.

Actual selected server: Debug `hds.dll`, SHA256 `3b5646d1e2cf9b6ed210a3b36d4cd8dd791b7d251d6dd80e81c9d0f88bcfcb48`, unchanged after disposal. The sibling Release hash is `6704e5119d0cb33f77e0b2fbc3f605665f8f0fd62bed52862eb2d3d4ce6bfb5a`; it was built and inspected but was not the selected fresh server.

Durable proof remains in the owner's Temp directory:

- `mxo-ability-training-mxotest_run_1_6db7f7a6bc.json`: SHA256 `d80f159493193b4585a2576e65a9e5f00838e91d3eafc96ca7d12599a9b2ebdc`.
- Matching `.log`: SHA256 `b6533a53842ebb4b23cdef72ad7f8c0bcced73d29c01edfeed3c5a0e0f56464a`.
- `mxo-training-fresh-2-stdout.txt`: SHA256 `14a61247d76dc4e2dd9eb505e8f02ae998c5a2c8661535dd9a12d98dfa2a1044`, with explicit exit 0.

The first run remains a **55.9 second fixture failure**, not a pass. It attempted an impossible over-u32 cash seed and received MySQL 1264 before the remaining database cases. The stock cash column is signed INT; no database schema was widened to make that hypothetical state reachable. The defensive result guard retains seven literal tests for widened legacy schemas. Earlier normal upgrades, caps, rollback, loaded passives, relog and two-connection race had passed before this fixture error. Its raw JSON/log remain `mxo-ability-training-mxotest_run_1_5430ee5c62.*`, SHA256 `6dd3e671ebfa51694e10a199a37ac7ade133c9c6e569f6594ae1e4348efd721c` / `89a8d049eb3d6cd9492e05ed9c6fce8945f6c471272f0a99cf93baab3785b105`.

All own servers were disposed. Native Manage Abilities quotes, spending, refusal UI and relog remain pending Claude; headless wire success is not native UI proof.

## Publication-fence review correction

Prior READY was retracted after source review found that the refusal modal called the existing chat wrapper (which flushes) while the training completion transport fence was held. This was a review miss, not a reported native failure. The earlier57.7-second proof remains tied to its recorded source and is not evidence for this corrected build.

The unchanged chat encoder is now exposed as internal pure BuildSystemChatMessage; its ordinary wrapper still queues the identical bytes and flushes. Training constructs every refusal/success packet outside the transport fence after reconciliation. The short publication section only admits these prepared packets through AddWorldBoundRpc with captured world/transport generations. SQL, SetLoaded/registration callbacks, tutorial callback, encoding and flush stay outside the fence. Existing unassigned stale-message pruning now applies to training responses too, without clearing unrelated RPCs or changing reliable numbering. Pricing, SQL transaction/admission, grant policy, native profile and headless driver are unchanged.

New tests check literal modal/error bytes, unchanged cash/grant/ack order, no-avatar response behavior, cross-thread fence availability during preparation and reset after actual training responses are queued/prepared. The reset regression retains unrelated8149 with contiguous counter0 and leaves stale training messages unassigned. Combined focused checks55 PASS; complete current suite4,662 PASS in93 seconds. Both actual Debug/Release builds and ability-training read-only plans/provenance pass (Temp/mxo-training-publication-plans-provenance.json). Debug hds SHA256BC4C6456E79E737A12CCD307A98303615D00A5FBD55E8B74D948970356F44F8D; Release8AC411F63A121767C60693027F7180A885396C22C093A07D1EB98F1943096CC1. No fresh run has been launched for this correction; root review/grant and Claude-only native UI validation remain pending.

Final scalar review: the committed cash-cache assignment now executes only inside the admitted current publication fence, together with the prepared bound packet loop. SetLoaded remains outside. A two-case actual publication test proves reset during preparation leaves both cache and queue unchanged, while current publication changes both. Final57 focused checks pass; both actual builds/plans/provenance pass again (Temp/mxo-training-scalar-plans-provenance.json). Debug72366539DBD731AD721C32FDB8DBBAF0FC336D2EAA4EE1DBDB0508CA761B2DC1; Release6C957F81475E6455426A4049C46914E6F8BC3FABA3E28C93A9AC0FE03C229ECD. The4,662 full-suite pass above preceded only this scalar relocation and its tests; it is not relabeled as a post-relocation full run. No pricing/SQL/admission/profile/driver change. Fresh still awaits explicit root grant.

## Corrected publication fresh proof and master integration

Frozen aee4394158dce6c351ae5b69d411ab8a6a916aa1 passed the one ability-training-authority case in61.2 seconds, explicit exit0, peak one server; owned disposal completed and slot B released. Actual Release hds.dll SHA2566C957F81475E6455426A4049C46914E6F8BC3FABA3E28C93A9AC0FE03C229ECD. Required `Temp/mxo-ability-training-mxotest_run_1_fc3096300b.json` SHA256A5056448B0E88F9312FB529085696869EDED5684423F4B62A52C162E6BC29D97 records Step=complete/Failure=null, ten database witnesses and three parser-clean sessions (65/37/22RPC,5/5/0views). Paired log and permanent stdout/exit `mxo-training-aee43941-fresh.out/.exit` remain retained.

Actual transaction proof includes all five ordinary quotes/payments, native positive failure/refusal, late SQL rollback without optimistic success, one winner on two real connections, canonical duplicate refusal, physical row replacement refusal and normal relog. Native UI remains unproven. Prior57.7-second evidence is historical; this61.2-second result is the corrected publication build.

After disposal, actual master3d240f9621cdca195c1b4bef5b499b5b47321fcd (merged206) was integrated. Only conflicts were independent ClientData trainingSync/trainingGeneration versus loadoutSync fields, and registry union of training/memory cases. Exact master mixed bytes plus only own field lines were used. Own AbilityTraining, ChatPackets, training SQL implementation, profile, fixture and driver remain byte-identical to tested aee43941. No repeated fresh is claimed for the combined source. Combined training/memory/registration/reset checks72 PASS; both actual Debug/Release builds and ability-training/ability-memory read-only plans/provenance PASS (Temp/mxo-training-master206-plans-provenance.json). Combined Debug403299675749F52B3F695C36247EB6EF6673838D428BB485D3DF8CA955C91CE0; ReleaseB516CD3C155C06CC1B799C4A6DF0C057373054D2E0AB744158D2035D95766E37. These are post-merge build identities, not the61.2-second fresh DLL. Current CI/root review is required before READY is restored; Claude alone validates native UI and merges.


After Claude natively passed and merged PR208 as master41ef78e0, root merged it into this branch. The only manual conflict was the test Harness, resolved by retaining both the exact training-charge and Prison-move failure helpers. All owned training runtime, SQL, pure chat codec, native mode, fixture and scenario blobs remain identical to fresh-tested aee43941. Root ran77 combined training, Prison, memory, grant-registration and message-queue tests successfully, plus both actual NativeQa builds and read-only plans/provenance. The61.2-second fresh proof remains pinned to its original Release6C957F81 binary; no post-merge fresh rerun is claimed.
