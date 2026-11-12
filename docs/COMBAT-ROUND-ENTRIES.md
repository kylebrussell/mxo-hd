# Combat view round entries

The combat view's subview 1 carries animation exchanges for both fighters and bystanders. RPC 45 supplies participant combat UI; it does not replace these entries. The old server appended one captured exchange to the init and sent no later exchanges. That exchange contained somebody else's positions, moves, weapon and results.

## Evidence and framing

Client 7.6005 `client.dll`:

- `6263ab10` sizes the record, reading the flags at offset 28, optional fighter-removal mask, changed fighter handles, a byte count, then `count * 0x79` bytes.
- `6263ac00` reads it and `6263addb` advances each entry by 121 bytes. It resolves fighter handles into slots, queues entries, then calls `625f7350`.
- `625f7350` deduplicates/sorts by the entry sequence at offset 32 and dispatches to `625f6580`, which schedules approach, transition, main exchange, recovery and timed result commands.
- `625f47f0` resolves each 64-bit animation reference by move ID and database resource ID. `62649920` loads the ILDB in file order; `62649a4a` assigns the move ID from the per-resource counter, whose initial value at `629ccb84` is 255.

The round record is `<combat view u16> 01 <centre f64 x3> <update counter u32> <fighter-change mask u32> <entry count u8> <entries>`. The init's master-view record supplies slots 1 and 2 and now ends with count zero; a complete dynamic exchange follows on the combat view. No fixed exchange tail is appended. The init prefix `01 00 02 a7 00` contains a sized-message offset, not a fixed combat opcode: 167 includes the two-byte offset and a body with one 121-byte entry. Without an entry the offset is 46 (`2e 00`). The builder computes this from the complete body and tests both forms.

| Entry offset | Width | Meaning |
|---|---|---|
| 0, 1 | u8 each | First and second fighter's slot in the init, unchanged across receivers |
| 2, 4 | u16 each | Approach time in milliseconds |
| 6, 18 | f32 x3 each | Fighter offsets from the centre |
| 30, 31 | u8 each | Direction/facing parameters; zero in the reproduced exchange |
| 32 | u16 | Sequence used for deduplication and ordering |
| 34 | u32 | Time relative to the combat view's start |
| 38 | i16 | Second fighter's relative start delay |
| 40, 42 | u16 each | Per-fighter exchange timing |
| 44 | u8 | Scheduling/command flags; the implementation enables late-time correction |
| 45, 53 | u64 each | First and second transition move references |
| 61 | u64 | Main paired exchange reference |
| 69, 77 | u64 each | Recovery move references |
| 85 | 36 bytes | Timed tactical/ability/style/result parameters; partially decoded, retained explicitly |

`lastday2`, 21:54:49, LocalSeq2435, view12/counter10 has mirrored offsets of radius 70, sequence10, start37869ms, durations2000/2500 and main reference `(748, 0x24000b8b)`. `CombatRoundEntryTests.LastdayDuelEntrySerializesExactly` reproduces all 121 bytes. The same reader handles NPC and PvP entries. NPC/PvP captures remain in `research/packet-dumps/community-intake`; lastday2 is under `20260607T015831Z-hdneo-public-inventory-lastday-rajko-nearend-selected/logs/`.

The high bit of the fighter-change mask introduces a separate **fighter-removal bitmask**. Client `6263ac39..6263acd1` walks that mask and calls `625f5130(slot + 1, null)` for each set bit. A retail final value of 2 removes slot 2; it is not a result code. Full teardown of this server's two-fighter handler removes both slots (3) before deletion. Removing only slot 2 left a withdrawing slot-1 player attached, stalled the native cleanup RPCs and prevented re-entry. The exact retail mask-2 fixture remains available through the builder's explicit mask parameter; live whole-fight teardown uses 3.

## Sourced moves and current limits

`research/combat/extract_animations.py` reads the shipped master ILDB, resource `0x24000b8b`, at its verified pack-map bounds. Its SHA-256 is `51caac6551d1b87cac1f1c4d2fffa820dc6053c7bc8e8c982a4971b903a04a4e`; all 25,695 source moves are walked in order. The generated `data/combat_animations.json` retains 126 static Self-Defense tactic exchanges and the referenced transition/opening moves, with source names, outcome flags and durations. Move748 resolves to `AikidoAbility KickRevBlowout` (2500ms); 21012 is `Trans Stand-SS`, corroborating captured references and the ID assignment.

Live NPC and PvP rounds choose sourced hit/miss/both-hit/finishing exchanges for the tactics, keep fighters on opposite sides of the centre, and send identical slot-relative entries to both fighters and observers. Each player's active style picks its ILDB exchange set and opening transition (Aikido A, Karate K, Kung-Fu W, otherwise Self-Defense S; see [COMBAT-STYLES.md](COMBAT-STYLES.md)). Special ability choreography still falls back to the tactic exchanges. This does not implement the full ILDB rule language, arbitrary multi-fighter choreography or all result-tail semantics.

Observers get the handler creation, init, live rounds, final record and deletion. They get no participant RPC45/47, target lock, own combat flags or own position handoff. Visibility updates admit late arrivals and remove observers who lose the fighter views; a world's cleared views are never reused for late cleanup. PvP death preserves the existing delayed teardown.

## Validation

All 3,497 unit tests pass in Debug and Release on 0e7892a. All 60 fresh-server headless scenarios pass in 502.5s at parallel4. The enhanced combat case also passes in isolation (73.3s): exact NPC/PvP fixtures, sized init and participant boundaries, shared bystander entries, late arrival, observer departure/return, Enrage rejection/expiry, withdrawal/re-entry, no participant RPC leakage and both-slot detachment before stop/death cleanup.

Native checks on 2026-11-11 used only CSR_Tester110 in the disposable fixture. A native bystander watched two headless fighters approach, punch and recoil (session d). With the participant prelude fixed, DuelBot approached and exchanged repeated punches/kicks/recoils with CSR110 over 18 rounds; surrender cleared the combat UI and returned both idle (session e). A player-started Blackwoods fight showed approach/exchanges/hit reactions and ended in corpse/loot/reward. The legacy WinterFrost mesh partly obscured that cluster, so the camera was rotated to inspect the fighters. The harness verified CSR110 reset to Mara `(23828,495,13984)`, health250/250, PvP0 and mission986 only, then released the client lock. Temporary diagnostic client config was restored byte-for-byte.

Native session g verified Enrage with timed real-client requests: effect 588 landed at 20:36:27.56Z, RPC44 at 20:36:35 was rejected, and RPC44 at 20:36:52 after expiry succeeded. Session h verified the corrected removal mask on 0e7892a: withdrawal at 20:50:25 returned idle without stuck RPC resends; re-entry at 20:50:58 produced fresh punches/recoils; a second withdrawal at 20:51:43 again returned idle. Each session closed the client and verified the same mandatory CSR110 reset before releasing the lock.

Two boundaries were essential: the init's former `a7 00` length was wrong after removing its captured entry, and the participant-only combat-mode prelude had an extra zero after its last attribute mask. The latter shifted the next master-view ID from 1 to 256, matching the client's error log; bystanders skipped that prelude and animated correctly. The headless check now accepts legal packet splits while rejecting partial or shifted view IDs.

Combined headless validation also exposed an old mission-helper flaw: teleporting to a target-floor doorway bypassed the elevator ride and sometimes never entered the site bounds. The helper now rides from the street, waits for arrival and enters the area, so followers and deadline clocks are exercised. All 14 focused mission cases and the final 60-case suite pass after that helper correction. A packet-only pass still does not establish native acceptance. Other-style/special choreography and complete timed result semantics remain partial as described above.
