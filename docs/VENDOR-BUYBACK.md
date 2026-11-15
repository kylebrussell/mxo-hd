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

Only bag slots 1 through 96 may create a receipt; permanent slot 0 is excluded. High-bit receipt items must have genuine `0x80000000` family under mask `0xfc000000`, and resolve to a known canonical class 0 template before legacy ability packing. Stored-code-family aliases, non-class-0 templates and unknown templates refuse locally; shared pricing and code-bit economics are unchanged. Genuine canonical aliases retain their exact raw identity.

All occupied bag slots count, including unrelated/corrupt rows. Duplicate selected slots refuse. No stack merging or quantity multiplication is introduced. Existing `ItemStacks.MaxStack = 64` and descriptor encoding are retained. Invalid raw descriptors outside signed/unsigned32 representation are not converted into another item.

Raw DB purity is preserved exactly. Arbitrary purity presentation remains legacy-unclosed: packets retain the existing known descriptor convention (ability-code value, otherwise count/default type), without forwarding unknown low bits. Margin's legacy purity presentation is unchanged. No claim of complete native item-property parity is made.

SQL commits before encoding, callbacks or packets. Encoding occurs outside the transport fence. A short current-session gate only updates the wallet cache and directly queues world/epoch-bound records; Flush occurs afterwards. Reset can suppress UI after a durable commit; relog recovers durable inventory/cash. Already numbered reliable records keep existing semantics. Independent legacy writers that do not participate in these locks remain a documented concurrency boundary; this package does not rewrite storage, marketplace or mixed upload.

## Native one-pass checklist — Claude only

Use discovered mode `vendor-buyback`, CSR110. It enables the existing dev-only `?clientactions vendor` QA window opener for tools vendor98; no rendered NPC or reachability is claimed. No RNG, price, inventory or transaction bypass is provided.

1. Open the window. Appraise shirt slot11; expect680, unchanged cash/item and no Buy Back receipt change. Sell it. Observe the single Buy Back row and price680.
2. Click the actual **Buy Back** control. Observe shirt restoration, cash reversal and the row clearing. Do not inject an RPC for native proof.
3. Repeat Sell/Buy Back with Code Bits slot12 (whole stack) and leveled ability code slot13. Check that their count/level survives. This does not close arbitrary purity rendering.
4. Sell one item, then another. Verify the last row replaces the previous one. After successful Buy Back, observe that the row clears and its control disables. Empty-request backend refusal is headless-covered; do not inject a request for native proof.
5. Close/reopen the same vendor. Do not expect a fabricated/replayed row. If the client retains and offers it, test the same-vendor receipt; report what the UI actually does.
6. Relog after a sale and reopen the vendor. Report whether the client offers the old row. If offered, click once and observe refusal; if absent, record that UI preflight prevented a request. Headless separately proves backend expiry. Check durable cash/inventory and retain actual `810e`, `8112`, `810f` and `8110` evidence; do not synthesize a native request.

Native UI proof remains pending. The accepted headless proof below establishes server/protocol behavior, not client widget rendering, retail lifetime or retail repurchase pricing.

## Validation

- Full Release suite: **4,820 passed** at `451220a3`, before the narrow slot/family/session qualification fixes. Final qualification: **123 focused tests passed** with those fixes. Do not label the earlier full run as testing the later guards.
- Fresh `vendor-buyback`: **PASS, 69.5 seconds, exit 0**, source `a52923285585964e054f1a2c4e1cae4afef5a804`, `--parallel 1`, peak one owned server. Actual runtime was Release `20DC8B295C308B2E841F93D9A1BAE54411A2EB88310EFAFE65C2C37F7558B8D6`.
- Retained proof: `C:/Users/russe/AppData/Local/Temp/mxo-vendor-buyback-mxotest_run_1_cb14ccf2ca.json`, SHA256 `F86B1E5065BA343E8A308D4EABC532972902B5D448186EEC8FD5ABAD40E46858`. Step complete, Failure null; sessions contained 98 RPCs/3 views and 56 RPCs/2 views, both with zero parser problems. All three participating tables were InnoDB; actual isolation was REPEATABLE-READ.
- The actual shirt sale paid 680 and removed the selected item with `8112`; Buy Back debited 680, added it once and sent `810f` final byte 1. Duplicate, late rollback and relog refusals emitted MODAL plus `8110(0,0)` without positive inventory/cash/success records. Raw count 0/purity, whole stack, full/poor, protected slot 0, foreign-family/Focus/high64, cross-vendor/session and two independent-connection races passed. These are headless facts, not native UI claims.
- Runner output, exit and disposal checks are retained under `C:/Users/russe/AppData/Local/Temp/mxo-buyback-a529-fresh-0de0c1a4df`. The owned runtime process count and exact owned-schema count were both zero after disposal. The case did not record the exact child PID; no PID is fabricated.
- Actual master `a28e573c` (#217) was merged afterwards. The interface preserves both APIs; zero-field buyback precedes the unsupported/nonzero ability-buy handling. Own helper, SQL, driver and mode git blobs are **byte-identical** to the fresh pin. No new fresh run was performed for this upstream union.
- Combined validation after that merge: **165 focused vendor/acquisition/publication/fence tests passed**, both actual NativeQa builds, both read-only `vendor-buyback` plans and requested 2,17 provenance passed. Combined runtime hashes: Debug `DAEC0C9DAC2B0D69D8980841D6F06FFFD0A5B2DE5B5827F19A9CDF980A679A38`; Release `BD7F845B6547C199B90156AA30B466154735DA8D8BE8669B323681B94A51E59A`. These are build/unit results, not the binaries used for the earlier fresh proof.

No generated ability coverage or parity snapshots are changed. Native one-pass remains Claude-only and pending.
