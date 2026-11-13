# Sati prefab rotations — partial world9 rollout

This package restores 5,318 native static keys, raising Sati from 2,599 to 7,917. All 70 conflicting keys, including all 140 variants, remain excluded. It depends on PR176's Sati foundation and remains draft until that parent lands. It does not recover the retail arrival, bind source archivists, add NPCs or create transport infrastructure.

## Primary transform and eligibility

The shared helper is the already reviewed Medium03 implementation: client.dll SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`, IPI matrix6246b0c0, point62111670 and quaternion62046a40. See [native transform recovery](WORLD-MEDIUM03-PREFAB-ROTATIONS.md). No second parser or transform formula is added.

All 268 Sati placements use the same direct METR SCIN→sector SIDI constructor path as world16. Declared placement counts match the strict parser; record signatures are unique and ordered, embedded GO lists stay inside their records, and the packed rotation field contains only 0/1/2/3: 62/73/45/88 placements. Every sector filename and X/Z parent translation matches the actual METR; parent Y is zero throughout. Every one of 58 prefab RIDs is indexed, readable and hashed. There are 121 blank placement aliases, which are metadata gaps rather than missing resources or an alternative parent class. The exporter does not flatten unknown nested/EPI parents.

The opt-in now accepts only worlds9 and16. Parent lookup uses the requested world; world16 output remains byte-identical. Strict default still refuses nonzero placements. Historical `--omit-unproven-rotations` remains separate and produces the original 2,599-key Sati subset; combining it with the native option is refused.

The audit preserves all 8,177 historical raw tuples and all 70 conflict groups byte-for-byte after normalizing checkout line endings. It records the native poses separately. Every old safe key remains. Exactly 42 old rows change positions at the recovered float32 store boundaries, by at most 0.001953125 world units; none changes its quaternion. There is no first/last variant selection or normalization. Current source contracts cover 132 GOIDs, compared with 95 in the historical subset. These are object references, not 132 actors or proof every object type spawns.

## Independent door and NAV proof

The newly restored object is GO580, **Door SL Push Knob Fadedgreen02**, sector0/local126/static bytes `7e000000`. Raw GOB schema364 supplies Open3/rule4004/range1000, default Closed835, Opened834 for20 seconds, Closing838 for1 second and Closed835. No manual Close is sourced.

Source `sati_20.sidi` placement3 uses RID `0x8200007f`, pivot(250,80,530), rotation2 and local position(1000,0,500). The RID's alias is blank, but the actual prefabs_5.pkb record is readable at offset5874314, size462766, SHA256 `fa67f221ffefe4e14f2ae8df7ddbaf76e3ca5ac7a994b3760a261c71a91c2ce5`.

Independent point arithmetic gives `(34000+2500−1000,800,−22000+5300−500)=(35500,800,−17200)`. The existing server +145Y convention makes the CSV point `(35500,945,−17200)`. The parent quaternion(0,1,0,0) times local(0,−a,0,b) is exactly(0,b,0,a), giving independent literal bytes **`00000000f304353f00000000f204353f`**. Expected bytes are not computed by calling the helper under test. The distinct low bits are retained; no normalization or yaw-only replacement is applied.

Actual raw GOB Model/DescriptionID is `0x6800001a`: `resource/GameObjects/Doors/Rotating/single_push/sl_push_knob_fadedgreen02.moa`, 897 bytes, SHA256 `90db89864a6c3990a6e1f8d00186bc5074154e8c211aec99b2a0f2197d8db786`. Model identity is proven; native pivot/frame/collision appearance remains pending.

Standing `(35600,895,−17200)` is inside raw INAV and the unchanged compiled NAV, sector0/region42/polygon74, floor800. The local standing point is(900,0,500). Existing NAV SHA256 remains `2af0a19c11c77af5dfa6a388739061a1ef2ebd1880a651bd78813cb36b31f930`. This is a direct diagnostic, not a route from the normal arrival or proof of interior accessibility.

## Reproduction and Claude-only one-pass

```
python research/world-data/static_objects_small03.py <client-data-tools> --world 9 --native-prefab-rotations
python research/world-data/sati_resources.py <client-data-tools> <MxO directory>
python research/world-data/test_native_prefab_transform.py
```

Discovered mode `sati-prefab-door` uses the existing generic door fixture with actual map9/runtime key `archive_sati`, resource `archive_sati/sati.metr`, standing35600/895/−17200 and safe protocol heading0. It adds no dev commands, GM authority or random override. The existing `sati`, `sati-door`, Large01/02 and Medium03 door fixtures keep their previous authority and positions; normal Sati metadata now reports the current counts.

1. On the exact approved head, use a new NativeQa directory and this discovered mode. Enter normally and verify the green door, surrounding frame and floor align. Inspect Open on its actual radial/Actions menu.
2. Rightclick Open. Observe rotation around its correct pivot and alignment with the frame. Wait for natural closing after20 seconds and closed state1 second later. Do not inject state or use a manual Close.
3. Record native geometry, quaternion presentation and collision separately from headless byte checks. This direct start does not prove a walked route, normal archive arrival or Loading Area return.

The headless scenario reuses the generic normal-login/Open3/closing diagnostic. It records actual source assembly/path/hash, full source quaternion bytes and natural state834→838→835 before teardown, with NativeRenderingProven=false. The local result is recorded below; source and wire proof do not constitute native rendering PASS.

## Unchanged boundaries

Existing OURS NearestEntry arrival `(14070,95,−4070)` and generated organization returner ALWAYS remain. The normal owned book47726/180-second sender cycle and exact-origin session path are unchanged. Four source47267 archivist proxies remain unbound. There are no captured NPC rows, HDL nodes or CNET networks. No NPC relocation, fallback suppression, PvpArchives change or invented hardline/subway is part of this package.

## Local validation

Full 4,354 Release unit tests and 38 focused tests passed. Seven Python native-math/guard tests and twelve audit policy/link tests passed. Both NativeQa builds, actual new/old/unknown mode plans and assembly provenance passed; the complete old Medium03 Plan is unchanged. Source current world9 and world16 CSV/audit outputs reproduce byte-exactly. Historical Sati2,599 and Medium03 1,282 omission outputs match their pinned historical blobs after checkout line-ending normalization. Default9/16, unreviewed12 and conflicting native+omission options refuse before output. Source NAV and all132 raw-GOB contracts reproduce exactly; the rotation feature adds no production hds source change. Native rendering remains pending.


Fresh source `78785d53292e554572ec4269b86881cc7936870f`: **sati-prefab-door PASS35.5s, explicit exit0, peak1, disposed**. The generic diagnostic automatically retained all five received view lists, source assembly path/configuration/hash, exact GO580/static126/view4 position/orientation, Opened834 and natural Closing838/Closed835. The actual selected assembly was Debug `hds/bin/Debug/net8.0/hds.dll`, SHA256 `ed749bbc642942ad4c2688400d9e1ac8a70030ff4fddc1c3752465f3327c7af2`. Actual quaternion was `00000000F304353F00000000F204353F`; closing/closed lists were `040001800238080000` / `040001800235080000`. NativeRenderingProven=false.

Evidence was saved before teardown: Temp/mxo-sati-prefab-door-actual-wire.json, mxo-sati-prefab-door-success-debug.log (25,332 bytes, SHA256 `5b91022fbdbbed648c153144a44730e9ad240146997aa7329e2fad3eb8d75bdc`) and permanent stdout/exit mxo-sati-prefab-fresh-summary.txt. No source/build edits occurred during the driver.

After disposal, actual master437fd4c6/167 was imported, preserving its reviewed missing-SID108 guard and exact mixed loader plus the Sati line. Sati CSV/audit/resources and new mode Git blobs remain identical to the fresh pin. Final41 scoped tests, both configurations, six read-only profiles/provenance and source9+5+16/current/historical/NAV reproduction passed. Final Debug SHA256 `900b0a731c30e40c285ebfde93d8aa3c8730342513f5100c55109560ea3f2b0a`; Release `fb5ef6018043d552d54e0b9afd2bfbbf06276097e04615c380334c78882e570f`. The final hashes differ after upstream import; they are not mislabeled as the earlier fresh binaries. The draft still depends on PR176 until it actually lands.
