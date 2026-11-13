# Attribute allocation authority

Ordinary `80 b5` now validates and commits the entire attribute-point batch. It locks the actual character EXP and five reserved attribute slots, checks one shared point budget and the native shown-value cap, and preserves existing physical row IDs and raw typed descriptors. No Info is spent. Failure leaves every attribute and cash unchanged.

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

## Validation

Focused tests cover strict full-width whole-batch parsing, shared point budget, shown cap30, normalized response, independently positive error status, exact typed slots, unchanged no-row fallback, accepted partial preservation and callback order/staleness. The current full Release suite passed4,647 tests after all attribute runtime changes; final focused tests passed92 and link-policy tests passed3. Both actual Debug/Release NativeQa builds, read-only plans and selected-DLL provenance passed. The disposable wire/transaction case awaits root review/grant. No fresh or native UI pass is claimed.
