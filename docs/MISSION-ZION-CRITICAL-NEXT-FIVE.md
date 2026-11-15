# Zion critical missions 101208–101212

Current publication is refreshed normally onto actual master `08d961eb47810f754d3ab4cd55cd23f7dcc8b897`, retaining published PR218 ancestry `0ba9e2ba` and the qualified five-mission driver. It includes the reviewed runtime dependencies [PR249](https://github.com/kylebrussell/mxo-hd/pull/249) (native-floor source preselection/cache and supported Safe compatibility), [PR251](https://github.com/kylebrussell/mxo-hd/pull/251) (exact V1 saved-door restoration), and [PR255](https://github.com/kylebrussell/mxo-hd/pull/255) (generated escort exit after committed downride). Those dependencies must be preserved when merging this package. No source XML, transcript, data, range, search budget or authored timer is changed.

The runtime union also retains the default-off, disposable-test-only departure tick diagnostics used to establish the original failure. They observe existing calls, do not invoke admission twice, and are not a gameplay fix.

## Scope and fixture

| Mission | Existing title | Normal actions |
|---|---|---|
| 101208 | Hardlined Learning | Talk Diego, fight, corpse Loot6831, hardline Upload |
| 101209 | Hardline Hunter | Ordinary Open of both junction Boxes, clear second site |
| 101210 | Interview With a Bluepill | Talk Maynard, recruit/escort outside, escort to Lonny, Talk |
| 101211 | Witness Rescue | Defeat boss, Talk Deja, clear kidnappers, Talk Alonzo, Protect both |
| 101212 | Monkey Wrench Roundup | Talk/fight Burdette, corpse Loot6835, hardline Upload |

Seven phases and 20 goals are retained. Protect is not directly completed by the driver. Source definitions remain `data/missions/zion_1_2_8...12`; primary archived dialogue remains `research/missions/transcripts/Zion1-2-8.md` through `Zion1-2-12.md`.

The discovered NativeQa mode and headless filter are both `zion-critical-next-five`. Exactly 27 earlier receipts are seeded before Margin; the selected five and next101213 are not granted. The fixture uses level/EXP50, HP5000/IS2000, no GM and no global RNG override. Layout seed101208 is disclosed; actual sources are never forced or rerolled. Headless movement, StillNpcs and `?weaken1` are assistance. Weakening can affect nearby friendly followers too. No unaided survival claim follows from this test.

Current doors come from the persisted current site. Measured floor/lobby witnesses and raw identities are validated; only genuinely unwitnessed legacy sites use exact raw lookup. Free props are observed before route/NPC aids, with actual arrival receipt/body order where a ride occurs. Fixed raw source, full DTO, Root/Route/Standing and orientation are retained. NPC checks use current life/generation, actual saved identity, side and position; the wire phase byte is distinct from the authored stage.

After an exit movement aid, the driver waits for the actual current follower movement and persisted position to agree within the existing1500 range before issuing one ordinary floor-door request. Readiness and ride share the original15s budget. Ground-floor/legacy policy is unchanged. A new player/session after carry relog gets its own completion-count baseline.

## Current whole-chain proof

Qualified producer `7c820942b03926d70fc612df4aafb7ace1d552b8` passed one whole run in **227.7s, held exit0**, with no automatic retry. The current master refresh and mode wording are separately qualified by affected tests/builds/actual fixture bootstrap/CI; the whole run is not relabeled as a new publication-head run.

Actual source was newest Debug `4EEA19B973BAB11FDBEB09060ECBDC5A1A9A65885E8679B78A79FA66240200E5`; Release driver `2E321F9582228574B235410B8AB9B70F507A985AD14C6975104EA347F4CD5BDE`. Raw proof `Temp/mxo-zion-critical-next-five-mxotest_run_1_5c2052073a.json`, SHA256 `B3F0251CCCFD72797B81DF10BAEFF6383BD9AEAF591EF8DF25367ADD39F253CF`. Compact proof is under `Temp/mxo-pr218-summary-cursor-20261115/fresh-game-7c820942/compact-proof.json`.

- Five receipts were each earned exactly once. Mission rewards were independently checked as XP `[68625,90750,137500,68625,68750]` and information91000 each.
- The exact ledger reconciled baseline296082500XP/1000info, mission434250XP/455000info, fighting288060XP and distinct corpse cash25000 to **296804810XP/481000info**, matching final wire totals and durable DB. Current claimed8120 identities are linked once to their actual8119 windows; no empty-success assumption or reused acceptance is allowed.
- Active carry relogs in101208/101210/101212 used normal logout and at least33s release. Items6831/6835 retained exact character1001/slot1/rawcount0 in this run; Maynard's saved identity/following, instance, phase, start and full site identity survived. Slot1 is captured evidence, not a native fixed-slot instruction.
- The actual downride reached54250/95/-16700 before Maynard's exit goal and next waypoint. Lonny's phase completed through ordinary Talk and another downride.
- Old server44928 exited before cold replacement53420. Cold login preserved totals and all five receipts, no active payload and no completion replay. The normal Critical offer for101213 was checked before cold restart. All five sessions had parser problems0.
- Exact owned schema `mxotest_run_1_5c2052073a` count0; all45 recorded UTC process identities gone, no PID reuse. The600ms watcher is not exhaustive process-history proof.

Anonymous `RawSource` fields may serialize as `{}`. They are not identity evidence: use the complete saved checkpoint strings and actual03 packets retained in the raw proof. A fixed seed does not imply identical source choices across different preview positions or candidate pools.

## Native one-pass checklist — still unproved

1. Inspect `NativeQa --fixture-plan zion-critical-next-five`, then use the normal disposable CSR110 fixture. Call Tyndall > Critical, verify the expected title and accept normally.
2. For every phase, use the actual current street/floor door through normal hover/radial controls. Walk into the room and inspect NPC floors, models, Boxes and their approaches before Open. Stop on a wall, void, invisible prop or inaccessible target; do not force a source, seed or mission-floor teleport.
3. Complete the five missions' normal Talk/combat/Loot/Upload actions above. In101209 inspect both actual Box models and Open from their current legal approach, not historical coordinates.
4. In101208 and101212, carry the earned item through normal jack-out, release wait and relog before Upload. In101210 recruit Maynard and carry the living follower through relog; wait until he is visibly nearby/following before the ordinary downride. Confirm street arrival precedes exit completion, then escort him to Lonny. V1 restore and escort semantics depend on251/255.
5. Check Deja/Alonzo's actual Protect behavior; the assisted headless run does not prove ordinary survival. Use only normal owned Actions for support abilities.
6. Verify one summary/payout/receipt per mission, no item duplication, no replay after completed relog, and the next normal101213 offer. Retain exact source/DTO and camera evidence separately.

Claude alone owns native/client lock and merges. Reset main110 to Mara23828/495/13984, full health, flag0 and mission986 on release. This worker does not operate the native client or mainDB.

## Retained history and limits

Earlier source-specific74.6s proof and its ledger remain historical, not this runtime. The8e/f2b admission refusals,0b hostile-wire-phase oracle error,86 V1 alias restoration failure,507 premature escort completion,402 too-early follower door request and994 cross-session completion counter failure are preserved with raw packets/source pins. The last two were fixture issues after the relevant runtime behavior was correct. No failure was overwritten or relabeled as a pass.

The publication refresh adds unrelated merged master code and truthful mode/docs provenance. It does not claim a repeated whole-chain run, a full native matrix, native rendering/picking/walking, or unaided survival.
