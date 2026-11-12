# Vendor appraisal and recycle pricing: partial economy repair

Normal vendor sales and recycles were already implemented. This change repairs Appraise parsing and reply semantics, source-based recycle prices, ordinary bag stack payout, and atomic account credit. It does not claim all retail economy behavior is reconstructed.

## Primary contracts

Client7.6005 DLL SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`; GOB SHA256 `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`.

| Contract | Primary evidence | Server behavior |
|---|---|---|
|`81 11 <Appraise u8> <Slot u8>`|Constructor6210a2a0 allocates4 bytes total; printer6210ab10 reads separate bytes+2/+3. Actual Appraise sender6210a5b0 sets+2=1 and+3=selected slot at6210a62f..39; normal sender6210a579 leaves flag0.|Exact two-byte body, flag0/1 and existing bag-slot range. Earlier00+slot was incorrectly described as big-endianu16; ordinary sales happened to work.|
|`81 12 <Appraise u8> <ItemClassData u32> <ItemInstanceData u32> <InfoPrice u32>`|Constructor621408cd allocates15 bytes total; printer621539c0 reads byte+2 as Appraise, not status. `docs/generated/client-rpc-classes.txt`341–342 names the same fields.|Appraise1 echoes1, quotes price, and sends no cash/removal records. Actual sale0 retains retail order80e7,80e4,5f,8112. Ability replies use existing class46/packed-ability representation; ordinary stacks retain their descriptor count.|
|`5d <Slot u16LE> <PersonalInventoryItem u8> <RecycleItem u8>`|Constructor621d2480 allocates5 bytes; actual sender621d4ae0 limits personal slot<0x6b/storage<0x208 and writes flag1 at621d4b58. Mass sender621d5c10 also sets1.|Exact body/boolean fields; existing server bag/storage slot policies retained. Flag0 has no recovered current UI producer and is explicitly refused without item/cash mutation, rather than assigning destructive semantics.|
|Per-unit recycle|`clhooks.py`328–334 and `rshooks.py`109–118: false itemID returns0; stored VendorPrice/20; only missing price defaults1000.|Zero stays0, unknown price defaults50 rather than5. Buy-price fallback100 remains separate and unchanged.|
|Ordinary bag quantity|621db720: for a nonnegative ordinary instance descriptor,621db73c..43 computes((descriptor>>18)&31)+1;621db7e3 multiplies the script result.|Whole ordinary bag stack receives per-unit price times actual selected count. Legacy DB count0 means1. Primary descriptor proof covers1..32. Existing server MaxStack64/encoding is unchanged;33..64 UI quantity-mask behavior remains a separate unresolved boundary.|
|Ability recycle|621db74b..6a: class0x2e descriptor is normalized to base ability ID for non-code values before script lookup.|Uses base VendorPrice without vendor buyback upgrade premiums.|
|Storage recycle|620403bc invokes the same helper with class0x2e and packed stored-code value.621db762..6a extracts GOID; multiplier stays1.|Preserves one-unit quote policy for a storage entry, not the ordinary bag multiplier. Whole-entry consumption is existing behavior; no claim that this settles retail storage stack economics.|

Source`rshooks.VendorBuyback` already provides normal VendorPrice/10 and the ability upgrade premium sum(i+1)^2*200; existing full-price ability exceptions remain. Vendor selling of an ordinary stack still uses the existing per-entry quote; no unsourced vendor quantity formula is added. Existing NonSellable vendor refusals remain. The native CantBeRecycled fallback625d9ca0 is a component-membership check, not a proven generic NonSellable gate; no such ban is invented. All41,788 stored GOB VendorPrice rows exactly match the current vendor_prices.csv (zero mismatches or missing entries). Raw GOB has28 stored VendorPrice0 rows and only one stored CantBeRecycled (47096=false); absent defaults are not reconstructed here.

## Transactions and account limit

A compare-and-delete of the selected GOID/count and authoritative account credit commit together on the existing InnoDB tables. No new migration, schema or alternate storage backend is added. Inventory/storage row locks precede account locks, matching mission completion's inventory-to-account order. Appraise never enters this transaction. A failed/stale selection or rejected account update leaves both item and cash intact and publishes no successful removal/payment.

**OURS:** legacy characters.cash is signed INT, so credits saturate at2147483647 with64-bit arithmetic. The reported cash delta and sale acknowledgement use the amount actually credited, not an unpaid full quote. The DB balance is authoritative even when the live cache is stale. Replay of a consumed slot cannot pay again. Vendor session/world-generation policy is reused; no retail trading radius or global commerce lock is invented.

## Validation and one-pass native checklist

Local validation:82 focused vendor/request/pricing/reply tests and the full Release4178-test suite passed. The final actual-wire case passed31.4s with one server at peak, including SQL credit-failure rollback/retry and exact credited5 at the signed cap. Earlier fixture-only CHECK failures (22.4s global constraint against richer preseeded rows;21.9s MySQL disallowing auto-increment identity in CHECK) are retained; the final constraint selects the disposable test handle instead. Runtime remained unchanged throughout these fixture corrections.

`vendor-sell-recycle-economy` uses an isolated database and the existing data-loaded vendor82837505 (static0x04f00001) near(19155,495,5313). It covers no-session refusal, Appraise/no mutation, normal sale and replay, stack8 recycle40, leveled ability base recycle500, explicit zero price, unsupported flag0, SQLCHECK credit failure rollback/retry, signed cap with actual5 credit despite stale cache, and storage-entry one-unit payout.

The first native run at91a16a2 passed Recycle cancel/confirm, the40-information Code Bit stack, the500-information ability code and Antibiotic Activate Item. Appraise/Sell was blocked: vendor82837505 has stock data but no rendered static NPC at its catalog coordinates. Those native actions are still untested.

Discovered NativeQa mode`vendor-economy` now prepares only disposable CSR110 at the known Mara street position(17000,495,2300). It seeds shirt11666 in slot11, Code Bit1x8 in12, Detect Vulnerability ability code level3 in13, Antibiotic1x4 in14, and10000 information. Dev commands are enabled for this mode so the existing `?clientactions vendor` command opens Mara tools-vendor fixture98 through the normal stock/session handler. No rendered NPC, GM permission or random override is required. Appraise and Sell are then ordinary native window actions; the shortcut does not execute either transaction.

Claude alone runs native QA and merge:

1. In chat type `?clientactions vendor` and press Enter. The existing Mara tools-vendor window should open. Select the seeded shirt in slot11 and click Appraise in that window. Expect680 information; item and balance remain. Then click Sell once: shirt disappears and balance increases680. If the window does not open, stop and retain the server log; do not search for an NPC at the old catalog position.
2. Appraise the leveled Detect Vulnerability code in slot13: expect12600 vendor buyback. Do not sell it yet. Close the vendor through its actual Close control.
3. In inventory use the observed Recycle button/action on Code Bitsx8; cancel once, confirm again. Cancel changes nothing; confirmed ordinary stack quote/payment is40 and removes that row. No invented Delete menu or flag0 UI test is required.
4. Recycle the leveled ability code: client quote/payment should500, not the12600 buyback premium. Check item removal and information messages through the actual UI.
5. Right-click Antibiotic1 → Activate Item if available to check the remaining stack is usable; this reuses the existing consumable path, not a new compile/storage feature. Use Actions and the hardline radial → Loading Area to check menus remain responsive. Optional storage Recycle retains its one-unit quote boundary; record native findings separately.

Unknown item defaults,33..64 quantity representation, storage whole-entry economics, vendor ordinary-stack multipliers, buyback-last-item UI and unrecovered recycle eligibility defaults remain partial. No generated ability coverage or parity snapshots are changed.
