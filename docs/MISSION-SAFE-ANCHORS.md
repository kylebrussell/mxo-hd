# Mission Safe source anchors

PR197 remains on native HOLD. Its original source-anchor placement failed picking: the marker was on a filing cabinet and no separate Safe model, tooltip or Open radial was available. The shared open-floor candidate now passes the normal-protocol Safe case, including exact cold replay. Native scene acceptance is still pending. Source statics remain untouched; no hiding, binding, moving or deletion is authorized by this repair.

## Source evidence and limits

Raw GOB source 1410 declares `Spawner/SpawnTypeID = 6`. Safe_Large 552 is the only concrete object with `MissionElement/SpawnType = 6`. Wall Safe 1416 has type 5, which is also shared by several unrelated templates. The pairing **1410 → 552 is a qualified unique-type inference**; the retail template chooser has not been recovered.

The index contains **4,650 validated anchors** out of 4,654 known source keys. Four keys in sectors 556 and 1139 are excluded because a declared static layer is unavailable. No conflicting variants or unsupported parent transforms were accepted. The source key, source GOID and exact known legacy or canonical pose must match. The full native float32 quaternion is preserved; no normalization, half-height offset or guessed asset pivot is added.

Primary GOB fields and archive hash are retained in `research/world-data/mission-safe-prototype-evidence.json`. The archive SHA256 is `3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103`. The companion manifest records every available static-layer hash and all omissions. The Safe CSV SHA256 is `f73e9db1dc1c68fc4a0859a1c4ff42c9bd1f40dab1c5989b0f50a3a777eed6b3`.

## Original runtime boundary and repair scope

The historical source-only implementation generated prototype 552 for audited source1410 and used a separate Safe native-anchor resolver. That pose fallback is now removed. The retained Safe CSV/parser is source evidence and a QA cross-check, not a second production placement factory.

The current shared chooser and restore expectation classify a newly generated source1410 Safe as552, then require the central audited anchor, scene and fixed-pose admission. Missing proof refuses the candidate; it does not silently choose1416 or publish the source anchor. Existing same-prototype bound552/1416 furniture and unrelated legacy poses remain unchanged. The shared versioned checkpoint policy distinguishes retained legacy phases from admitted fixed placements; it never rewrites SavedSpot or rerolls a fixed placement on replay.

Prototype 552 already uses class 46 and its existing Closed `0x835`, Open `0x834`, Open action 3, container `8119` and Take `811f` paths. No new object executor, reward, bounds or reach policy is introduced. Its stored Open `MaxDistance = 100` remains unchanged. **Native Open availability, picking and asset geometry remain pending**; headless server acceptance does not prove those client checks.

## Reproduction

Run from the repository root:

```
python -B research/world-data/mission_safe_anchors.py <client-data-tools> --output data/mission_safe_anchors.csv --manifest research/world-data/mission-safe-anchors-manifest.json
```

The companion generator reuses the reviewed Computer parser read phase, guarded by its normalized hash. Only the unused family-selection set is excluded from that hash so an independently reviewed Computer family extension does not invalidate the identical reader. It never runs the Computer writer or changes Computer outputs. Git preserves the Safe CSV's audited LF bytes across checkouts.

## Shared open-floor repair

The Safe path uses the same central request factory, pose facade, chooser classification and persistence admission as Computer and Box. The obsolete Safe-only request builder and unsafe native-anchor pose fallback have been removed. Generated source1410 is classified as552 before shared proof; missing audit or scene evidence refuses preflight rather than silently choosing1416. Existing same-prototype bound furniture and untouched legacy poses remain unchanged. It qualifies only the audited source1410/free 552 pairing, exact known source provenance, unchanged full native quaternion, measured support minY `-50`, and existing Open3/MaxDistance100/HalfExtents40,50,35. The shared scene and persistence layer must publish `SavedObject.OpenFloor` before this harness can run. Missing or unqualified witnesses fail closed; the driver has no old-coordinate fallback.

The independent harness reader records the published policy, source/object identity, raw/input fingerprints, centre, standing point, route, surface identities and quaternion. It checks the actual received free 552 pose against that witness, follows the published route with the existing headless `?tp` movement fixture, and checks the actual NAV floor and existing interaction range. Those movements are QA protocol fixtures, not ordinary native movement, walking or scene-clearance proof. The actual Ride executor is checked against the published route root; this case skips the shared fixture helper's extra jump to a legacy site centre. A source-bound creation-header check rules out the legal source1410 binding path that a Hidden update would require; retained complete03 lists permit independent review. It is scoped source-identity evidence, not a new general03 decoder.

The case uses the original mission2201, seed220110 and source0d10003e route. It aborts before use, returns to the original offer origin and reaccepts in the same world, then restarts only its owned server and cold-relogs before Open. Centre, standing, route, quaternion, fingerprints and immutable SavedSpot must survive fixed replay. Normal Open/Take earns dossier 6799 and normal exit advances the existing phase. There is no direct completion, reward injection, pose fallback or source hiding. The accepted run below proves this backend path; it does not establish native rendering or picking.

## Shared candidate validation and retained failures

The first shared Safe run at `b57ccdbbad94219973d877cc7686567c6ba36318` failed after **50.9 s**, exit 1. Headless QA teleport movement had not been explicitly saved before the owned cold stop, so the cold self03 returned to Mara 17000/495/2300 instead of the published Standing. Proof `mxo-mission-safe-open-floor-mxotest_run_1_2fe4a64280.json`, SHA256 `8fc9f4f3f498ecadae5393c95ebd4c878140a37df515aaba95a7d7719e773db4`, is retained unchanged. Both sessions were parser-clean and the original/cold processes and schema were disposed.

The fixture-only correction uses the existing `?save` path and waits for its actual save acknowledgement. It records the pre-save and persisted ownedDB coordinates, native-f32 coordinates and published Standing before shutdown. This prepares QA teleport persistence; it is not a native full-jackout claim and performs no SQL position UPDATE.

The retry at `ba19716071866e592787a150752a06bef41a0ca8` failed after **49.5 s**, exit 1. Save acknowledgement and exact persisted Standing passed, but cold self03 was 29060.599609375/2095/14759.900390625 instead of 29060.59375/2095/14759.8603515625. Proof `mxo-mission-safe-open-floor-mxotest_run_1_c0308e60fe.json`, SHA256 `60669e9d813b8f583a2f2882664c6204e51f9c5eb68f165f2ae50da2a3fc8f8b`, is retained unchanged; both clean sessions and owned disposal are recorded.

The underlying world-load query selected bare MySQL FLOAT columns, whose text representation shortened the stored values to 29060.6 and 14759.9 before parsing. A read-only literal CAST witness reproduced that exact loss; adding 0e0 preserved the actual stored f32 values. Isolated production commit `3355ab9f7138e29c4c25466104d2f9359961c36d` changes only SetPlayerValues' x/y/z projections to `x+0e0 AS x,y+0e0 AS y,z+0e0 AS z`. It preserves column order/aliases, all other fields, schema and stored data. No standing, geometry, range or exact assertion was relaxed.

At that frozen pin the case **passed 56.9 s, exit 0**, phase complete, null failure. Proof `mxo-mission-safe-open-floor-mxotest_run_1_21b5cb8da2.json`, SHA256 `969b7228dcdeba67650cf5a76add470cd95bd180a997d5302db8713226e529f6`. Actual selected original/cold Release DLL SHA256 `EC6240B41BDC5D0AAFC220E68A74C33B0AEBAA627A01B521E7562B1D245043D2`; mtime 2026-11-14T06:14:51.6087373Z. Runner stdout, exit and assembly provenance are retained under `mxo-safe-3355ab-fresh-20261114-011620`.

The savedDB position and raw cold self03 exactly match Standing 29060.59375/2095/14759.8603515625. Five raw checkpoints were retained before decoding. The original/cold sessions contain 93/74 RPCs and 98/16 view lists, both with zero parser problems. Same-world Abort/reaccept, immutable source geometry, fixed centre/quaternion/route/fingerprints, actual Open8119, Take811f, earned dossier 6799, goal tick and normal exit to phase 1 passed. Original PID 46016, cold PID 51836 and the exact throwaway schema were confirmed absent after teardown.

Qualification: shared core Release full 5,082 passed before the tools-only policy/evidence corrections and narrow position-read fix; combined Safe/open-floor/Poster focus 145 passed, reader interoperability 13 passed, and position-read/Safe relevance 61 passed. Both actual Debug/Release builds, compiled Safe read-only plans/provenance and actual PR-diff whitespace checks passed. No generated coverage/parity snapshots were changed. Later legacy checks and retained failures are recorded below.

## Final shared integration and legacy fixture checks

The final bounded common planner was imported at `b9e7f9eee4980aeb9508391e260d5ac50a1e70b6`; the shared full Release suite passed 5,091 tests. Combined Safe/open-floor/Poster/heading focus passed 166 tests. Both actual build profiles, Safe read-only plans/provenance and PR-diff whitespace checks passed. The single-input performance path preserves the complete canonical Safe DTO byte-for-byte against the accepted 56.9 s proof, including centre, standing, quaternion, every route point/surface and both fingerprints. This is equivalence/performance evidence, not another fresh run or new geometry evidence.

The four conditional legacy-fixture migrations were then exercised serially with unchanged production Release SHA256 `442FFC78B46335E312F4B3E08B89C9C81235705029CED37D0FEE0298294C0D98`:

- `mission-container-deposit`: PASS 24.0 s.
- `utility-mission-locked-safe`: initial FAIL 30.7 s. Its old fixture archived only unloaded 142; the now-authoritative normal load correctly refused the missing owned prerequisite graph, so the next Pick Lock still reported NoAbility. Original failed stdout SHA256 `c12e45f713001a67e037e87c8c9e591e7ea6682e9180f37787507562164a0a67` remains under `mxo-safe-legacy-d9865b-20261114-015834`. This was a loadout fixture error, not an open-floor or target1416 change.
- The approved fixture-only correction uses coherent level/EXP 50 and the exact ordinary class 0 prerequisite closure, all initially unloaded. Explicit zero-threshold presence requirements are trained at legitimate 1; positive requirements retain their source levels; selected 142/level10/slot300 is last in the normal 80ae batch. Actual 80b2 acknowledgement and all 14 durable loaded rows must precede the missing-tool assertion. The pure real planner accepts 32/100 memory. The original NoAbility, tool, target, site and goal checks remain. Corrected retry PASS 21.3 s.
- `data-mining-mission-map`: PASS 20.2 s.
- `mission-relog-object-rollback`: FAIL 31.1 s waiting for the next briefing within its unchanged 10 s wait. Courier Get and its phase 0 goal completed, but no next Safe view or admission-refusal result appeared before the timeout. This does not establish whether planning was still running or ultimately refused. Original stdout SHA256 `c9826a28deee36d4e07074b9c63caf95bdb79a07e24a8606d4ce2cb2bb93a2c9` is retained under `mxo-safe-legacy-231019-20261114-021157`.

Both serial allocations stopped at their first failure. All recorded owned processes and exact throwaway schemas were disposed. The rollback case initially failed before its first checkpoint read, leaving a concrete source-context proof gap. Evidence-only commit `4502fd2bbadaf711f10a59f3674cafe6cf19cc73` now captures raw checkpoints before decoding and in finally, current-operation cursors/timing, Started/character/phase inputs for exact unchanged LayoutRandom reconstruction, received courier/teleport records, and owned PID/DLL/hash/mtime/logs. It does not claim a pending selection exists until it is actually persisted. No timeout, geometry, source, seed or runtime rule changed. Its diagnostic result and exact controlled regression are recorded below. Native mission scene acceptance remains pending.

## Exact refused-input regression

The unchanged random rollback diagnostic at `ec699b620453503626cbc6ce7216d25d67945e7d` failed after **29.3 s**, exit 1. Durable PendingOpenFloor explicitly recorded `IncompleteStaticEnvelope`, closing the earlier source-context proof gap. This was an admission refusal, not evidence of slow planning. Proof `mxo-mission-relog-object-rollback-mxotest_run_1_98d89a144c.json`, SHA256 `5d771372f897a25a687d4b6e43c75018e71ed682c308f115426cd1d725176f13`, remains retained alongside the original 31.1 s failure. Both owned runs were disposed. No post-fix random-case rerun is claimed.

The selected site spans X `[8800,13500]`, Z `[7825,12769]`; qualified scene 842 covers X `[4000,14000]`, Z `[8000,12000]`, with its normal entry root inside. Shared production fix `c444f4fe` preserves the original site/request bounds, scene coverage and fingerprints. It erodes only the internal `Site intersect Coverage` candidate domain and explicitly checks original and native-f32-cast full footprints against coverage. Root, route, NAV and closed-static guards remain. No chooser, range or source authority is expanded.

The separate tools-only `mission-relog-object-rollback-842` case reproduces both failed normal choices using the existing scoped seed control and sequential owned servers. Raw phase0 seed `1821579883` gives captured effective RNG seed `1814967188`, selecting door 219152420/site fingerprint `EE14642429DDB504E87EB5DA29BCCF7C368B045FAAFE2CBE89F56D7EBE31F7A0`. The ordinary Courier approach is saved with `?save` and its real acknowledgement. Read-only stored position and cold self03 both equal `(27480,895,14650)`. The predecessor exits before the same owned database cold-restores the unchanged identity, Started and phase0 checkpoint under raw seed `1821541235`, giving phase1 effective seed `1814967189`. No pose, Started or choice SQL rewrite is used.

Ordinary Courier Get must reproduce door 882901607/source `0x34a00462`/1410/generated 552, site fingerprint `B35F28E2919D869A7844C6F4C04A4519803C7440F196F1EE23260B591C7BF1FA` and canonical choice hash `18EE2EB80B361613983F46FB5B4932A94153F93320B85C4578413886784F97BD`. These assertions precede a regression pass. The pre-Get creation cursor 3 selects actual Safe view 6/spawn 4 by published centre, full quaternion and current mission key, avoiding historical handles.

At frozen `e8bd31018617a5724c3f983a6a2daee6f32ad449`, this controlled case **passed 88.5 s**, exit 0, peak 1. Proof `mxo-mission-relog-object-rollback-mxotest_run_1_a0503b86aa.json`, SHA256 `028390710cc61aa1292f3cd1305d95a9c38f2dbe46a45952c2580fd5e69e770e`, retains 14 raw checkpoints and 12 operation records. All four sequential servers used Debug SHA256 `B55D702A26AE3F3891EAD9C0B0A92CBA65776FCFE40A26C95E7BAF69088A8EB4`, mtime `2026-11-14T08:30:04.0047103Z`. Sessions recorded 30/54/45/23 RPCs and 7/22/7/3 view lists, each with zero parser problems. Normal unlock/Open/Take/Put, owned-item counts, cold restore, deliberate late completion SQL rollback/retry, earned receipt and final cold no-replay assertions all passed. PIDs 41428/38628/56076/57768 and schema `mxotest_run_1_a0503b86aa` were confirmed absent. Raw logs, exit, upfront assembly and disposal records remain under `mxo-safe-842-e8bd31-20261114-033126`.

Final qualification: combined Release full **5,139 passed** before the last tools-only cursor supplement; final **23 focused checks passed** afterward. Both actual NativeQa configurations, Safe read-only plans/build-info and actual PR-diff whitespace passed. Release SHA256 is `88381A6DBC64FC222C86FD863242DD8EE0FC4921980E4D3050E843C5F18EFD4F`, mtime `2026-11-14T08:26:57.8516847Z`; actual fresh used Debug above. No generated audit/coverage snapshots changed. The accepted normal Safe 56.9 s proof and complete DTO equivalence remain qualified. This exact refused-input regression adds backend evidence; native rendering, walking, picking, relog and unchanged-static observations remain pending.

## Native asset control and current measured witness

The retained [PR223 Safe-floor control](https://github.com/kylebrussell/mxo-hd/pull/223) independently demonstrated the actual free 552 model, hover, target selection and Open radial on open floor with omitted CurrentState. Two on/Open/off cycles removed only the private diagnostic Safe; surrounding scenery remained. That proves asset/default picking capability in that control, not acceptance of this mission placement.

For the accepted normal 2201/seed 220110/source 0d10003e checkpoint, the unchanged resolved witness is Centre `(29100,2050,14759.8603515625)`, Standing `(29060.59375,2095,14759.8603515625)`, full quaternion `00000000FFFF7F3F00000000F3043533`. The route begins at the actual Ride root `(28880.215714661983,2095,14516.791268981888)`, then `(29000,2095,14495)`, `(29030.296875,2095,14600)`, and Standing. These are observed fixed metadata for this exact context, not new literals used by the fixture, an alternative anchor or permission to teleport the native player. Native validation must follow the runtime-published witness and ordinary route, and report any mismatch rather than substituting these numbers.

## Claude-only native one-pass — pending scene acceptance

Do not start this checklist until root marks the joined runtime and scene ready. Mode remains `mission-safe-anchors`. CSR110 starts at `(17000,495,2300)`, level50, organization2 with the same existing prerequisite fixture and seed220110. No GM or random-outcome override is used.

1. The mode starts CSR110 at the original Mara offer origin `(17000,495,2300)`, not at a Safe or an approach. Use Agent Gray's normal remote Missions contact, choose Critical > **The End of the Beginning**, accept and use the ordinary mission elevator. Verify the naturally chosen Object1 is source `0x0d10003e` and free Safe552.
2. Read the published shared open-floor Centre/Standing/Route witness from the retained mission checkpoint/runtime evidence. The ordinary elevator Ride supplies the published Root. Follow its published Route to Standing; never use Centre as a player spawn or movement destination. No advisory coordinate is a frozen fixture. Do not use the failed historical centre or approach. Confirm visible Safe geometry, hover/selection and radial **Open**. Record the nearby static furniture before any action; it must never hide, move, bind or disappear because of this proxy.
3. Relog before opening. Verify the same fixed centre, full quaternion and standing witness replay; do not accept a newly planned nearby pose as persistence proof. Recheck the model and Open radial.
4. Abort normally at the desk. Confirm the source furniture remains unchanged. Return to the original Mara offer origin by ordinary game travel and reaccept the same Critical mission in the same world. Confirm the new Safe placement has a current published witness and the same source identity.
5. Use ordinary Open3, take dossier 6799, verify Search completion, then leave for the authored next phase. Existing exit proximity may advance automatically after Take; report the actual branch. No full mission reward or retail chooser parity is claimed.

Native rendering, picking, walking and cleanup remain pending. A backend Open response alone does not establish these client observations.

## Historical validation of the original source-only placement

These results predate the shared open-floor repair and do not override the native HOLD above.

The full Release suite passed **4,577 tests** before the subsequent fixture-only and literal-transform checks. Final scoped tests passed **48 tests** after importing master `064b0460`. Both actual Debug and Release NativeQa builds, read-only mode plans and assembly provenance passed. Python snapshot policy passed nine tests. No generated ability snapshots were changed.

The initial fresh attempt failed after **24.1 seconds**, exit 1: the fixture indexed a missing `CurrentState` creation attribute. The free `0c` object uses its GOB default, Closed `0x835`; no production fix was needed. Normal mission selection, source key, prototype 552 and source pose/quaternion checks had already passed. Failed evidence remains in `Temp/mxo-mission-safe-anchors-mxotest_run_1_90797972c4.json` and `.log`, with the original stdout preserved separately. The regressions were not run after that failure.

A separate pre-fresh fixture check also found that NAV Project insets the proposed Z coordinate from 14790 to 14780. The failed assertion is retained in `Temp/mxo-safe-nav-diagnostic.log`; only the fixture approach was corrected.

The corrected fixture uses received state or the proven GOB default, checks parser problems for every recorded session, saves unique evidence in `finally`, and refuses to report success if durable proof writing fails. A primary failure is preserved rather than masked by a secondary evidence error.

On frozen source `2a9c32d3ae57aee628e37d2523d3ab5706add171`, all three serial cases passed with exit 0 and peak parallelism 1:

| Case | Duration | Result |
| --- | ---: | --- |
| `mission-safe-anchors` | 61.4 s | Normal acceptance, exact source/552 wire pose, pre-Open relog, Open, Take 6799 and ordinary exit |
| `mission-computer-anchors` | 65.9 s | Existing Computer anchor/persistence regression |
| `mission-container-deposit` | 26.5 s | Existing container deposit regression |

All owned servers disposed; the fresh slot was released. Actual selected configuration was **Debug**, with hds SHA256 `7a6658f589e5bdfc964488ae771a132748e59d4544a21e65443c8c468b864376`. This is not labeled a Release fresh run.

Safe evidence was saved before teardown in `Temp/mxo-mission-safe-anchors-mxotest_run_1_dd3c8c1a81.json` and `.log`. JSON SHA256: `8fe18d92e2ecb3c78e8a17758660d0b9cdd2e27d95ff93fdcfa73f7df3eb30f9`; log SHA256: `d3ad94c0dc833b0cbd3221709aceeb9c65cc9497e94a73da0a0daa296e2ee3e3`. Both Safe sessions had zero parser problems. The earned dossier and completed goal stayed in phase 0 after Take; ordinary exit advanced to phase 1. The new 552 prototype and immutable source spot survived normal relog.

Computer regression proof: `Temp/mxo-mission-computer-anchors-mxotest_run_1_151ae1c054.json`, SHA256 `db8b0379346d5a58b5e8fb8bd0f20abe60292d5e00a0169d9dff19414c78e729`. Permanent stdout and explicit driver exit records are under the `mxo-safe1410-retry2-*` Temp paths.

The final provenance-only check regenerated the Safe CSV and manifest independently using the original merged Computer reader and the reviewed Computer 1407 extension (`f70d515b`). Both CSV files were byte-identical to the committed Safe data, and both manifests were byte-identical. The manifest fingerprints the exact normalized reader prefix (`efcc4d721c2c41ef29f2dc7afb5a8ff2d1aee5fd7e2e8e4fbb4e077834d2dac4`) with its normalization scope stated explicitly; it does not claim a full-file Computer generator fingerprint. All static resource hashes remain present. No runtime change or fresh repeat was needed.
