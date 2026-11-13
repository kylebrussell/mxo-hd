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

**Economic boundary:** this serializes participating training transactions. Legacy Vendor/Market/Trade/Mail/mission/loot writers using absolute cached SaveInfo are not migrated and are not claimed globally serializable. This handler never calls that absolute writer, including disconnect/reset paths. Stale postcommit publication cannot overwrite the wallet in SQL; relog reads the committed result.

## Native one-pass checklist

Use discovered NativeQa mode `ability-training`. It owns a complete stored prerequisite/folder closure in the disposable CSR110 archive. The selected rows remain unloaded; loaded Hacker begins49 with its proper loaded prerequisite chain, and Execute Program remains50. Character Level/EXP are50 with20,000,000 Info. No dev, GM or RNG override is enabled.

1. Open Manage Abilities > Upgrade and select **one** level before comparing a quote. At the ACTUAL profile levels: Sneak25â†’26 is67,600 (previous135,200); Spy Style5â†’6 is3,600 (previous7,200); Improved Sneak1â†’2 is260,900 (previous800); Find Data Node30â†’31 is199,400 (previous192,200). The separate1â†’2 source/unit examples above are not the profile's current levels.
2. Check all four quotes before spending; stop/report if any differ. Accept each intended upgrade once and verify one Info debit and the new level.
3. Write Code14â†’15 is22,500 and stops at15. Download Mission Map is already capped10. Native UI should not offer an illegal16th/11th level; forged requests are a headless authority test, not a native UI action.
4. Train the loaded Hacker49â†’50 for500,000. Its passive196 becomes200; all unrelated loaded records and Execute Program stay intact. Relog and verify the trained archive levels, Info and loadout.

These preseeded owned rows are a QA fixture, not a new vendor/drop/acquisition claim. Native UI/quote/spend/relog validation remains pending Claude. No native rendering or retail backend atomicity is claimed from a headless pass.

## Verification

Pure/progression tests cover independent literal native quotes, exact request size/count, EXP and ability caps, typed signed/unsigned and canonical-alias descriptors, level-zero admission, duplicate-canonical refusal, callback lock order and packet-before-tutorial ordering, immutable loaded snapshots, unloaded-grant refusal, stale character/generation/world/life/transport publication and failed tutorial progress. Normal-protocol fresh proof is pending root review/grant. Its disposable case includes actual upgrades, a late SQL charge failure after the level UPDATE, unchanged row/cash on rollback, two real participating training connections, duplicate slots, a locked physical-row replacement, loaded Hacker49â†’50 with other loaded records unchanged and earned relog. All raw RPC/03, database witnesses and actual selected DLL/configuration/hash are required before teardown, including failures.

Pre-fresh local verification: the initial full Release suite passed 4,647 tests before the review fixes. After the callback lock-order, native failure-status and canonical-authority corrections, 101 focused training/progression/registration tests passed. Both final Debug and Release NativeQa builds, actual read-only ability-training plans and selected-DLL provenance checks passed. These are source/unit/build results; no fresh or native training pass is claimed yet.
