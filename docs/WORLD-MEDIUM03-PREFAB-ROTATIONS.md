# Medium03 prefab rotations: bounded native transform recovery

The explicit `--native-prefab-rotations` option exports **4,491 unique Medium03 keys**, restoring3,209 over the historical1,282-row subset. **All134 ambiguous native keys remain omitted, including every variant.** This is one district's rollout; other districts are unchanged and remain subject to their existing gates. It does not complete Medium03 population, elevator mappings or world data.

## Primary contract and parent eligibility

Client SHA256: `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`.

* SIDI reader624c2240 allocates the same0xe4-byte IPI class for each placement and calls62476030 regardless of RID alias. Constructor62479b00 calls62465ff0, which clears the EPI-parent pointer at+0x30. Direct sector parser624c22e0 supplies the SID parent at+0x34. The opt-in validates the actual METR SCIN filename and X/Z translation against every sector CSV row; it never traverses an arbitrary nested/EPI resource graph.
*62476030 reads RID, signedi16 pivot/bounds, rotationu32 and the embedded GO list.6246af00 reads the72+2-byte GO record and copies source position/quaternion. Placement counts, record bounds and flags remain strict; unrecognized records are not flattened.
* IPI matrix6246b0c0 uses signed pivot×10 through624685b0 (constant628bed60=`0x41200000`). It uses the supplied pivot, not a guessed box center. Actual point routine62111670 establishes quarter turns0:(x,z),1:(z,-x),2:(-x,-z),3:(-z,x).
* Interior caller6247531e obtains that matrix, composes local then parent via62045e10 and invokes GO creation624cee30 at62475347. The point is rotated then translated.624cf156 extracts parent orientation with62046d40;624cf169/62046a40 computes the full Hamilton product **parent quaternion × local quaternion**, including X/Z tilt. No arbitrary normalization or yaw-only addition is used.
* Float32 stores occur after signed pivot scaling, matrix composition, point rotation, point translation and each final quaternion component. The Python helper models those stores. The server's existing **+145Y** is applied separately after the native position; it is not claimed as a native prefab pivot.

All173 direct placements qualify:49/74/33/17 have rotations0/1/2/3. Packed resource records are indexed/readable even when147 placement aliases are blank. Their RID, pack, offset, size and hash are recorded; a missing tooling alias is not a different native parent class or a guessed geometry name. Unknown actual nested/EPI parents are rejected by the helper and unsupported flags fail the strict parser.

The original4,762 raw diagnostic references and all134 conflict groups remain unchanged. Historical raw rotated positions/quaternions keep their old diagnostic convention; current CSV poses are generated separately from the native rule. All1,282 old keys remain.87 old rows now round positions at the recovered native float32 store boundary (maximum difference0.001953125 units); none changes its quaternion. Current hashes cover180 static inputs, the parent METR and23 unique prefab resources.

## Independent checks and remaining limits

Exact-ID city crosschecks use(world,sector,staticID,GOID), never nearest coordinates. Corrected legacy variants corroborate all four rotations; many older CSV variants have wrong positions or uncomposed local quaternions, so those CSVs are not a blanket oracle. Golden tests include all four native matrices, signed pivots, noncommuting X/Z-tilted quaternions, float32 boundaries and refusal of unknown parents/nonfinite values. The native creation key masks sector/object IDs; correct algebra does not determine ambiguous variant precedence.

Cooked RichWorldGOs supplies(PRFB,GOBJ) references, not a generic absolute pose table. TransportSpace coordinates have a different identity domain and are not joined heuristically to doors. No uniquely identified retail rotated-door packet pose has been recovered. Native appearance/alignment remains Claude's pending check; source math and headless NAV/wire proof must not be reported as native rendering success.

## Source580 diagnostic

There are no newly restored rotated GO365/414 doors in Medium03. The approved diagnostic uses **GO580**, the real `Door SL Push Knob Fadedgreen02`, sector33/static1380 (`64051002`). Source `medium03_25.sidi` placement RID`0x38005f6f` is `resource/prefab_database/floors/sl/sl_projects02/sl_projects02_small_hacker_01.iprf`, rotation3, pivot(560,240,420), local position(1000,0,300). Native point is(25300,2400,43200); server CSV point is(25300,2545,43200). The NAV-proven standing point is(25300,2495,43100), floor2400, headingbyte0. This is a diagnostic start, not a proven walking route.

The expected wire quaternion is independently fixed as **`00000000f30435b300000000ffff7f3f`**, approximately(0,-4.21468478e-8,0,0.9999999404). It is deliberately not normalized to identity. The reusable headless door helper checks those literal bytes from the actual03 creation, then Opened834 → natural Closing838 after20 seconds → Closed835 after1 second.

Actual GOB schema364 stores Model/DescriptionID`0x6800001a`, resolving to the nonempty `resource/GameObjects/Doors/Rotating/single_push/sl_push_knob_fadedgreen02.moa`; the model and GOB hashes are recorded in [the source fixture](../research/world-data/medium03-prefab-door.json). Open use3 invokes4004 DoorOpenGeneric. No manual Close usable is sourced. Runtime `PrepareDoor` already copies the full CSV quaternion to enabled Orientation; this package adds no gameplay/protocol changes.

## Reproduction and Claude-only one-pass

```text
python research/world-data/test_native_prefab_transform.py
python research/world-data/static_objects_small03.py <client-data-tools> --world 16 --native-prefab-rotations
```

Only world16 accepts the opt-in. The strict default still refuses nonzero placements. `--omit-unproven-rotations` continues to produce the exact historical1,282-row CSV/audit; it cannot be combined with the new option. No other world's CSV/audit is regenerated for this rollout.

Use discovered mode `medium03-prefab-door`, CSR110, no dev/GM/RNG grants:

1. Confirm the real green door and surrounding frame/floor align at the diagnostic location; inspect the actual Open radial.
2. Rightclick Open. Observe the geometry opening around its correct pivot and its relation to the frame. Wait for natural closing after20 seconds and closed state1 second later; no injected state or manual Close.
3. Inspect Actions/menu. Record any native model/tilt/collision mismatch independently of headless quaternion bytes. The source door's final quaternion is almost identity because its local90-degree orientation and placement270 degrees compose; do not simplify the stored bytes.
4. Reset/release only through Claude's normal native-QA procedure. This fixture is not proof of Loading Area return, elevator travel or ambient NPC visibility.

## Local proof

Full4,330 unit tests and14 focused tests passed. Six Python native-math/CLI-gate tests and12 audit/link/policy tests passed. Both NativeQa configurations built; actual read-only mode plans and provenance passed. Current CSV/audit reproduce byte-exactly in the Windows checkout; historical world16 omission and unchanged world17 outputs match their original blobs after line-ending normalization. Strict default world16 export exits1 before creating output. The reviewed opt-in rejects other worlds and cannot combine with the historical omission flag. No generated ability coverage/parity snapshots changed.

Final evidence fixture pin `9cbc12ef38f180649a6db07202cebd440cd623a8`: actual headless door case **PASS35.8s, exit0, peakone server, disposed**. The stock harness chose **Debug**, recorded automatically inside the evidence JSON: hds SHA256 `E4BEFBCFCC0EE7DD4406BAF80043C128BD5480403B593E6C3D2DFE34A58DB515`. This is not labeled a Release fresh test. Temp/mxo-medium03-prefab-door-actual-wire.json was saved before teardown with source assembly/hash, static34604388/GO580/view4, actual quaternion `00000000F30435B300000000FFFF7F3F`, OpenedState`34080000`, Closing03 list`040001800238080000` and Closed03 list`040001800235080000`. NativeRenderingProven is explicitly false. The24864-byte server raw log and runner stdout/exit were retained. An earlier35.7s passing run remains separately preserved; the rerun added persisted client-byte evidence only, with unchanged data/gameplay.

Latest master checked after disposal: `05bccebff0a2888907aeee98dc488e7590b4f8b7`, already included. Native model/frame/quaternion presentation is still Claude-only pending; neither NAV agreement nor these wire bytes establish a native visual PASS.
