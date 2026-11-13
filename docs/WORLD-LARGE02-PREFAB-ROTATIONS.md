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

Automated validation is pending for this revision.
