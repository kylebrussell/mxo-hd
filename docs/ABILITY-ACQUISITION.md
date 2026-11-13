# Atomic ability acquisition

## Scope and source boundary

Normal vendor purchases and learning a bag ability code now commit their durable changes before publishing inventory, wallet or archive records. This covers genuine known ability descriptors only. Code Bit storage, stacks, training, loadout, prerequisites, automatic grants and all catalog prices remain unchanged.

Current stock contains 144 distinct ability descriptors across 44 vendor rows. All are base-rank genuine ability-family descriptors with positive vendor-price entries. Purchases retain the source-backed bag item 46, the descriptor and packet order: `5e`, `80 e7` debit reason 8, `80 e4` balance reason 8, `81 0f`. The positive retail Awakened example is pinned by existing tests. Repeated ordinary BUY requests represent separate purchases and charge once for each committed item.

Native `81 0e` names ItemTypeID and BuyBackLastItemSold (ctor `6210a060`, printer `6210a970`). A true buyback flag is refused as unsupported; it is not interpreted as another normal purchase. Native `81 0f` names ItemClassData, ItemInstanceData, InfoPrice and BuyBackLastSold. No buyback codec or price formula is invented.

Learning uses the same ability-only transaction for ordinary hardline Upload (`80 c7` action 4) and Loading Area Central Storage (`62`). Shipped `serverrules.HardlineUpload`, ID10021, returns success and breaks shadow; it does not establish new training or character-level admission rules. Native `81 18` has SingleItemUploaded, AbilityCodeUploaded and Status fields. The existing known-archive `5e` layout is native-compatible shared-list inference: no positive retail capture of uploading an ability has been recovered. This package does not promote that inference to retail proof or claim complete ability parity.

## Transaction and explicit server policy

Every operation owns its connection and checks that character, inventory and archive tables are already InnoDB. It performs no schema migration. Lock order is character row, inventory rows/range, then archive rows/range for learning. Queries use `FOR UPDATE` under the configured isolation. Exact inventory primary key, raw GOID, slot and count protect deletion. Real archive slots are 0 through 519, and every occupied family reserves its slot. The hidden/attribute region is never allocated.

The server's explicit policies are: genuine family validation precedes canonical identity; unknown codes and ambiguous slots refuse; canonical known duplicates retain the bag code; a full archive retains it; stacked ability codes refuse without destroying their stack. Existing rank aliases are recognized without copying code-family IDs into the ability namespace. Newly learned entries preserve the existing level-1, unloaded representation. Learning does not load, train or grant anything.

The hardline's unchanged Code Bit plan reserves its pending new slots while the ability allocator runs. Ability transfers are atomic individually; this is not a new all-or-nothing bulk Code Bit transaction. Independent legacy inventory/archive writers that do not take these locks remain a documented concurrency limit. There is no global storage or inventory lock rewrite.

Handlers serialize acquisition through the existing per-player crafting lock. SQL transactions contain no mission, player-life, transport or packet callbacks. Successful receipts are returned after commit, then normal packets are queued. Failed SQL operations return ordinary UI notices and the existing Central Storage failure result where that route needs one; no fabricated purchase opcode or error code is added. The existing one-live-world-session model still governs handler publication order.

## Native one-pass checklist (Claude only)

Use discovered mode `ability-acquisition`. It owns only ordinary abilities 2 and 17, removes any copied real archive ownership of 1 and 5, provides 200,000 information and an empty bag, and retains the framework's consistent level-50 profile.

1. Type `?abilityacquisition vendor`. This explicit disposable CSR110-only QA control opens existing vendor277874480 through normal `processVendorOpen`, without changing stock or executing a purchase. It is a window opener, not evidence that the catalog NPC is rendered, nearby or reachable.
2. In the real vendor window, buy **Calm Mind Calm Body** for 40,000 and **Adrenaline Booster** for 40,000. Both are stocked, initially unowned ordinary abilities with character-level qualification below 50. Observe the two bag codes and the two cash debits.
3. Close the window and use the Mara hardline's ordinary Upload action. Verify both abilities are now known and unloaded in the Loading Area archive. Relog and verify ownership persists. This checks the normal UI route; it does not claim the inferred upload response came from a retail capture.
4. Open the same vendor again, buy another Calm Mind Calm Body code and upload it. The duplicate learning refusal must retain its bag code. Repeated BUY still charges for that second code.

The opener requires exact mode opt-in, `MXO_DEV_COMMANDS=1`, a fully validated actual `mxotest_` database name, CSR110, a live ready Slums world and no pending world change. Default modes refuse it. It has no GM, RNG, buy, upload, grant or teleport operation.

## Validation contract

Unit cases cover genuine family versus code-family aliases, canonical duplicates, ambiguous slots, archive slot519/full, reserved code-plan slots, wallet representability and unchanged literal bag/buy records. The fresh case uses ordinary vendor requests, hardline Upload and LA upload, then cold login. It deliberately fails the second wallet or archive write and checks both durable sides plus the current-operation RPC window. Independent connections test purchase overspend and same-source learning races. Exact source DLL/configuration/hash, all received RPC/03, operation cursors, SQL outcomes, server output and step/failure are retained before teardown. Native menu behavior remains pending until observed.
