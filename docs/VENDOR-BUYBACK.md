# Vendor Buy Back

The normal Sell path now records the exact gameplay fields of the sold inventory entry in the same transaction as its removal and payment. Buy Back restores that whole entry into one free bag slot and consumes the receipt with the cash debit. Appraise changes neither inventory nor the receipt.

## Native evidence and explicit server policy

The pinned client DLL has SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Primary byte slices are in [the source evidence](../research/economy/vendor-buyback-native-proof.json).

| Boundary | Primary consumer/producer |
|---|---|
| Buy Back button | Event `4b42424d`, `6210c8c9 → 6210a390`, sends exactly `81 0e 00 00 00 00 01`. There is no selected item identity. |
| Last-sale row | Real sale `81 12` handler `621768dd` passes class, instance and price into `6210c6e0`. That replaces the single row. Appraise does not. |
| Price display | `6210b889` stores the supplied price in row `+1c`; `6210b899` reads it for text. The Buy Back sender performs no wallet preflight. |
| Completion | `81 0f` handler `62176a57` reads its final byte at `+0e`; nonzero clears the last-sale row through `6210c6e0(0,0,0)`. |
| Refusal | `81 10` is seven bytes: header, u32 StringID, u8 CloseUI. `62176a03` displays a nonzero StringID and closes vendor view `3f` only when CloseUI is nonzero. |

The request identifies the last sale implicitly. The following lifetime, payment and atomicity rules are **OURS**, not a recovered retail server law:

- One latest receipt per authenticated character, tied to a unique current world-session token and the same vendor ID. A successful subsequent sale replaces it. Appraise and failed trades do not.
- Close/reopen does not create a refund or replay `8112`. The same vendor can honor the receipt if the client still offers Buy Back. World-session reset/relog expires it. There is no invented timer.
- Repurchase costs exactly the amount actually credited by that sale, including signed account-cap clipping. No fee or new vendor price curve is added.
- Refusal uses truthful MODAL text plus `8110` with StringID `0`, CloseUI `0`. No retail nonzero failure reason is claimed. No positive inventory, wallet or `810f` records are sent on refusal.

A bounded raw protocol04 search found 48 ordinary buy request candidates, all flag `0`. No retail buyback-positive request/reply pair or retail receipt lifetime was recovered. Normal buy reply ordering is reused; a retail buyback trigger/order capture remains absent.

## Identity, persistence and limits

`vendor_last_sale` is the only new table. Startup creates it before listeners and qualifies it, `characters` and `inventory` as InnoDB. Each participating transaction uses its own connection and locks character → all owned inventory rows/range → receipt. It captures exact signed raw GOID, raw count (including legacy `0`), signed purity, original slot and original row identity before deletion. Restore creates a new inventory PK/current timestamps; the original PK and possibly-zero timestamps are receipt provenance. This is lossless **gameplay-field** restoration, not byte-identical row restoration or SQL-mode repair.

All occupied bag slots count, including unrelated/corrupt rows. Duplicate selected slots refuse. No stack merging or quantity multiplication is introduced. Existing `ItemStacks.MaxStack = 64` and descriptor encoding are retained. Invalid raw descriptors outside signed/unsigned32 representation are not converted into another item.

Raw DB purity is preserved exactly. Arbitrary purity presentation remains legacy-unclosed: packets retain the existing known descriptor convention (ability-code value, otherwise count/default type), without forwarding unknown low bits. Margin's legacy purity presentation is unchanged. No claim of complete native item-property parity is made.

SQL commits before encoding, callbacks or packets. Encoding occurs outside the transport fence. A short current-session gate only updates the wallet cache and directly queues world/epoch-bound records; Flush occurs afterwards. Reset can suppress UI after a durable commit; relog recovers durable inventory/cash. Already numbered reliable records keep existing semantics. Independent legacy writers that do not participate in these locks remain a documented concurrency boundary; this package does not rewrite storage, marketplace or mixed upload.

## Native one-pass checklist — Claude only

Use discovered mode `vendor-buyback`, CSR110. It enables the existing dev-only `?clientactions vendor` QA window opener for tools vendor98; no rendered NPC or reachability is claimed. No RNG, price, inventory or transaction bypass is provided.

1. Open the window. Appraise shirt slot11; expect680, unchanged cash/item and no Buy Back receipt change. Sell it. Observe the single Buy Back row and price680.
2. Click the actual **Buy Back** control. Observe shirt restoration, cash reversal and the row clearing. Do not inject an RPC for native proof.
3. Repeat Sell/Buy Back with Code Bits slot12 (whole stack) and leveled ability code slot13. Check that their count/level survives. This does not close arbitrary purity rendering.
4. Sell one item, then another. Verify the last row replaces the previous one. A failed/empty repeated buyback must not award another item or spend information.
5. Close/reopen the same vendor. Do not expect a fabricated/replayed row. If the client retains and offers it, test the same-vendor receipt; report what the UI actually does.
6. Relog after a sale. Confirm the expired world-session receipt cannot be used. Check durable cash/inventory and retain raw `810e`, `8112`, `810f` and `8110` evidence.

Native UI proof is pending. The owned headless case independently covers normal wire actions, raw count/purity, full/poor refusals, late SQL rollback, duplicate requests, independent connection races and world-session expiry. Its results must be recorded before claiming PASS.
