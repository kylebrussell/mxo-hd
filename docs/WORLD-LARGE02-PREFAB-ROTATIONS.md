# Large02 reviewed prefab rotations (partial)

This extends the existing native-prefab transform path to map13. It restores433 keys, exporting2052 of2096 native keys. All44 historically conflicting keys and every variant remain omitted, even when transformed positions coincide. Historical omission mode still exports1619 keys; strict default still refuses nonzero placements before producing output. Only reviewed worlds13 and16 accept `--native-prefab-rotations`.

## Primary transform and resource gate

All34 actual direct-sector IPI placements were checked:20 rotation0,13 rotation1 and1 rotation2; all rotation upper words are zero. The established client matrix at6246b0c0 reads the pivot and rotation; callers6247531e/62045e10 compose the direct SID parent before object creation at62475347. Every primary resource is readable (index flags4; types6400/137472). Every secondary RID is readable (type12544); the client copies it to IPI+dc, while optional metadata6/80/81 goes to+9c. Neither field enters this pose matrix. The audit preserves both resource identities, sizes, hashes and opaque metadata without interpreting them as another transform.

The audit retains2164 raw references,2096 keys and442 historical nonzero-placement keys. Nine rotated keys were already conflicting, hence433 restored keys. All1619 previous keys survive. Ten existing positions round through native float32 arithmetic (maximum delta0.00048828125); their quaternion bytes remain unchanged. No additional conflicting pose was accepted. See research/world-data/large02-prefab-door.json and large02-static-audit.json for literal evidence.

## Independent door contract

Restored GO414/schema364 is sector27/local572, static LE key3c02b001. Source large02_135.sidi RID0x2c00054b has direct parent rotation2, pivot(500,0,1780), sector corner(12000,0,-10000), local position(0,0,700). Native result(17000,0,7100), with existing CSV Y+145, is(17000,145,7100). The independent quaternion literal is `00000000f304353f00000000f204353f`: source W becomes Y and source Y changes sign into W, without normalization.

Model description0x2800005a names sl_push_knob_graymetal01texvar.moa (902bytes, SHA2561a23438fafd7cf1ca1d4a785bef908689f3c4fc6c8a69534066bd75e31fdc559). GOB defines ordinary Open3/execute4004, default835, opening834→838 after20seconds→835 after1second. No Close radial is invented.

Standing point(16900,95,7100) is independently NAV-valid sector27/region88/polygon24, indoor floor0. The north/south alternatives are invalid and unused. Source-level ground and wire records do not prove rendered geometry.

## Reproduction and boundaries

    python research/world-data/static_objects_small03.py <client-data-tools> --world 13 --native-prefab-rotations

Existing missing NAV sector47 remains unavailable despite EnableNavigationMesh1;225 available sectors are not a completeness proof. No NPC population, subway, hardline eligibility or native LA return behavior changes. The captured Encounter row remains excluded from ambient loading.

## Claude-only native one-pass

Use discovered `large02-prefab-door`, with no dev/GM/forced rolls. This diagnostic starts beside the sourced door with byte heading0. Inspect visible geometry and ordinary radial Open, then wait21seconds for natural close. Record clipping, collision and facing separately. The direct start proves neither a walk route from the hardline nor native Loading Area UI return. Existing normal profiles remain unchanged. Native rendering and acceptance are pending.

## Automated validation

Full 4,353 unit tests and 10 focused world/fixture tests passed. Six native-transform Python tests and 12 audit policy/link tests passed. Debug/Release builds, actual new/normal/unknown plans and provenance passed. Current map13 and inherited map16 CSV/audit reproduce byte-for-byte; historical map13 CSV remains byte-for-byte identical. Four strict/default or invalid-option probes refused before writing output. Existing native map16 plan semantics and evidence-document default remain unchanged.

The new actual-wire case passed in 35.6 seconds, exit0, peak one isolated server. It used the actual Debug hds.dll SHA256 AF2DB0D0657DAEC4F6CCEC2A89F0C442892F601B3C5792A999250AB981E73017. Actual orientation was00000000F304353F00000000F204353F; state bytes34080000 then view updates040001800238080000 and040001800235080000. All five received view lists and successful raw diagnostics were persisted before teardown under the owned Temp/mxo-large02-rotation-proof directory. This proves wire behavior and the tested source contract, not native rendering.

Executable ability audit remains honest: 1,404 templates, 561 player candidates/grants (532 partial,27 cosmetic,1 inaccessible,1 missing) and843 other templates. Current coverage/report artifacts are ignored outputs; historical snapshots remain unchanged.


Latest master184 integration: actual7172b423 imports the reviewed DataMine7 rotations and its exact8returner bindings. The exporter now authorizes inherited7+16 alongside this package's own reviewed world; all missing5/7/11/4guards remain. Shared door evidence retains SourceConfiguration and all ReceivedViewLists once each, with both source diagnostics and default MetrResource/EvidenceDocument seams. Own dataset/fixture/mode blobs and actual existing profile plans remain unchanged. No hds production diff, new native/full/fresh run or changed prior native result is claimed.
Validation: 16 scoped own/DataMine/Medium03/Yuki units and6 Python transform tests PASS; both actual configurations build, six own-profile plan comparisons exact, two new DataMine plans and DLL hashes pass. Seven current world CSV/audit pairs reproduce BYTE EXACT, historical own output matches modulo gitLF/CSVCRLF, and six strict probes refuse before output. Proofs remain in Temp/mxo-root-7172-maint/codex-large02-prefab-rotations.


Master180 compatibility: actualee708fec adds reviewed15 alongside7/13/16. Only the whitelist conflicted; preserve all four reviewed worlds and every5/7/11/4missing-layer guard. Own exclusive feature Git blobs and8 existing actual profiles remain exact;2 new15profiles and both DLL provenance checks pass.19 scoped own/15/7/16/11 tests and6 Python tests PASS. Eight current sourceCSV/audit pairs are BYTE EXACT; historical13 content exact (gitLF/writerCRLF);6strict refusals precede output. No hds production diff, full/fresh/native rerun or changed35.6s wire claim. Proofs Temp/mxo-root-ee708-maint/codex-large02-prefab-rotations.
