# Ability refusal feedback

This changes feedback at two existing server decisions. A proven running cooldown now sends native reason 5 (reuse wait), and the existing insufficient-IS check sends reason 17 with the exact positive difference between its snapshotted effective cost and available IS. Admission order, effective cost, timers, casting, healing, combat, ownership and effects are unchanged.

The native `80 ba` record is 19 bytes: header, u32 AbilityID at offset 2, u8 ToolsSlot at 6 (default `6b`), u32 ErrorID at 7, u32 InnerstrengthDeficit at 11 and u32 StatesNotMet at 15. The codec preserves all raw fields, including unknown full-width values. The old ushort/byte builder remains a compatibility wrapper.

The exact constructor, printer, adapter, 36-entry message table and special-format arguments are preserved in [the consumer evidence](../research/abilities/ability-refusal-consumer-evidence.md) and [pinned bytes/literals](../research/abilities/ability-refusal-native-proof.json). The client DLL hash is pinned there. Two bounded retail logs provide positive reason-5 and reason-6 records; this is not a claim to have recovered retail backend cooldown or cost law.

Reason 6 means current tactic. Its existing aggregate callers retain the explicit legacy alias, so unresolved busy/control/item/target feedback remains imperfect. No generic replacement is invented. Reason 7 means not loaded, but remains codec/display-only: current style/search predicates do not prove owned-but-unloaded and may include loaded level 0. Wrong-style reason 13 stays unchanged. Missing-state masks, tool-category distinctions and other target reasons are not newly inferred from English strings.

## Native display check (Claude only)

Use discovered mode `ability-refusal-feedback`. It reuses the proven ordinary Repair RSI prerequisite loadout 2, 17, 45, 1006 and 77, its folder closure, consistent level 50/EXP and initial health 100/1000. No GM or random override is enabled.

1. Clear the selected target. If regeneration filled health, use the existing `?health 100` QA preparation. In Actions, normally cast **Repair RSI 1.0** on yourself. Observe ordinary cast, healing, IS payment and timer separately from the probes.
2. Type `?abilityrefusal reuse`. Expect the native reuse-wait message naming Repair RSI. Its number comes from the client's local recast lookup or fallback, not a server remaining-duration field in this packet.
3. Type `?abilityrefusal inner`. Expect the native message saying **15 more Inner Strength**. This is a fixed display example, not an assertion that the actor's real IS deficit is 15.
4. Type `?abilityrefusal unloaded`. Expect the native not-loaded message, preceded by **DISPLAY DIAGNOSTIC ONLY**. Ability 77 remains loaded; this probe does not establish an ownership/admission decision.
5. After the ordinary timer expires, prepare health below max if needed and normally cast Repair RSI again. The probes must not change loaded membership, resources or timers.

All three controls accept fixed names only, with no ability ID, reason, amount or slot argument. They require exact opt-in and dev flags, a validated actual disposable `mxotest_` database, CSR110, the current client reference and a live ready world with no death, pending transition, shutdown or Loading Area. Packets are encoded before the transport gate. Only current scalar checks and direct world-bound enqueues occur inside it; log and Flush happen afterward. Stale unassigned records use existing reset pruning.

These are message-rendering probes. They do not prove that native UI generated a server request: client preflight may stop a reuse or insufficient-IS attempt locally. Normal headless requests independently exercise the actual server predicates without using the probes.

## Validation contract

Literal tests independently pin the 19-byte field layout, a retail reuse record, reason 17 with exact deficit, reason 7 and unchanged reason 13. Full-width unknown fields round-trip without truncation; malformed lengths/headers refuse. Actual handler tests verify positive deficit, no spending/cast/timer changes on refusal, and a normal successful cast still paying once, healing and starting the existing timer. Probe tests cover strict guards, fixed names, unchanged state and stale queue pruning.

The fresh `ability-refusal-feedback` case uses ordinary full ability-use records. It performs a successful self heal, then a request while its timer actually runs, and an independent zero-IS actor's normal request. Operation cursors isolate refusal records, positive echo/modifier/timer absence, resource history and heal executor output. A cold login normally casts again. Unique evidence records the actual DLL path/configuration/hash, sent requests, all received RPC/03/Margin data, step/failure and both server outputs before teardown. Native UI observations remain pending.
