# Mission computer source anchors

Slums Computer_Spawner1415 markers used for free GO3 Computers now resolve to their recovered native pose. The existing site choice, saved source spot, global world CSV, NAV, waypoint and distance/floor algorithms stay unchanged. Native PR194's full-source diagnostic passed visibility, upright desk placement, hover/Take and actual80c7 Search completion; control and native-Y-only variants were not run. This is evidence for the full source pose, not a universal mission-prop correction.

## Source and bounded eligibility

`data/mission_computer_anchors.csv` contains 47,365 Slums/map1 Computer_Spawner1415 anchors for a future free GO3 Computer consumer. Both Slums CSV files contain 47,434 such keys; every key matches the client's available declared static layers. All known key variants were compared across SID, SIDI and SIDGO: zero conflicting GOID/position/quaternion groups, zero duplicate1415 records. If future input has conflicts, every variant is excluded, never first/last wins.

Missing layers can conceal collisions. All69 known1415 keys in sector1139 are excluded because declared SIDG0xd0000400 is unavailable in packed and loose data. Sector556's SIDI0x3800d000 is also unavailable but contributes no known1415 keys. Unseen contents remain unquantified. Sector860's unnamed declared SID0x0c007f60 is indexed/readable and has zero GO records; it is not treated as missing.

The generator reads actual METR paths, including Barrens/Westview, and verifies sector names/minXZ plus declared resource identity. Filename aliases may differ numerically only when their indexed pack/offset/size are identical. Full IPI rotation words must have upper zero and lower0..3. Constructor62479c1d→62465ff0 clears EPI parent+30; direct SIDI parser624c22e0 sets SID parent+34. Only these direct counted records are accepted; no nested resource traversal or guessed parent is performed. All referenced primary/secondary resources are readable, indexed flags4. Opaque metadata is not assigned invented meaning.

The manifest records the actual parent hash, aggregate hashes of all4,477 available static resources and532 referenced resources, omission counts and output hash. It avoids duplicating a large raw-world JSON inventory. Reproduce from repository root:

```powershell
python research/world-data/mission_computer_anchors.py <client-data-tools> --output data/mission_computer_anchors.csv --manifest research/world-data/mission-computer-anchors-manifest.json
```

The client DLL used for the primary disassembly is SHA256 `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`.

## Exact object pose contract

62476030 calls6246af00 for each counted stored GO record. It copies stored XYZ at offsets8/12/16 to internal+c/+10/+14 unchanged, and copies quaternion unchanged.624cee30 applies62111670, then adds composed matrix translation; it does not swap axes or add HalfExtents. IPI vtable628bf088+10 resolves6246b0c0. SID parent comes from the requesting sector at624c1d8b. Sector vtable628c25d0+0c resolves624db1f0: identity basis plus rawSCIN minimum XYZ, loaded at624db8fd. Quarterturn/pivot and float32 store rules are shared with [the recovered prefab transform](WORLD-MEDIUM03-PREFAB-ROTATIONS.md).

Key0x1176 is explicitly stored, not derived from an export ordinal:6246af75..84 copies it to internal+4, and624cf171..184 combines it with sector347 to native0x15b01176 (wire bytes7611b015). IPI135 has pivot(120,420,620), rotation1, local(1540,100,665), sourceGOID1415. Recovered native pose is(91865,4300,12660), quaternion bytes `00000000f304353300000000ffff7f3f`. The existing CSV/saved spot is(92740,4445,13535), quaternion `00000000f20435bf00000000f304353f`.

The full-source object centre need not lie on walkmesh. Same-floor4200 player point(91865,4295,12740), region184/poly203, is80.16 units away. Geometric NAV paths connect the existing elevator and observed player position to this approach. This is not native LOS/render/picking proof. Native model translation copies Position unchanged (622ddb50,623adeb0); asset-local computer pivot remains a separate question. No inferred HalfExtents offset is introduced.

## Strict resolver and shared consumers

`MissionComputerAnchors.Read(TextReader)` strictly validates the complete artifact, including full unsigned identities, quaternion bytes, finite values and counted SIDI provenance. Duplicate keys refuse the artifact. `TryResolve(source, concreteGoid, out pose)` accepts only map1/source1415→freeGO3, exact sector/static/native IDs, interior markers, and an exact known legacy pose or canonical native+145 source pose. It preserves the recovered full quaternion without normalization. The output pose is native; server+145 is only the canonical source representation.

Identity alone is insufficient: a changed XYZ/quaternion refuses resolution even if a custom tool retained its key. Existing MoveObjectToPlayer replaces the spot with type0 and no source identity, so it also refuses. Bound same-GOID statics, real furniture, other markers/worlds and unknown/missing keys refuse. Neither source objects nor saved checkpoints are mutated.

One derived pose is used by MissionStaging.SpawnObject and MissionRuntime's Put reach, object:N NearPlace, object-relative arrivals, TickObjects and escort AtObject, plus DataMining's mission-object locator. No canonical MissionSites input copies are introduced: selection/bounds remain exact. Old SavedObject.Spot identity resolves through the same function without rerolling site/phase or migrating item state. Current mission waypoint packets refer to the saved elevator door and remain unchanged. Source quaternion is serialized in full; these arrival/trigger paths do not read spot.rot. Their existing index-based arrival angle and site-floor height remain unchanged. Fallback quaternion parsing stays lazy, so position-only custom/type0 callers retain their previous behavior even with null/malformed quaternion text.

Validation: focused resolver tests cover an old serialized checkpoint, no source mutation, returned orientation immutability, canonical-source vs native-output separation, key-preserving custom pose refusal, bound/type0/wrong-world/refused aliases, invalid/nonfinite/truncated/duplicate rows, complete artifact count/hash and unavailable-sector exclusion. Default-path fresh validation is recorded separately below; no new native result is inferred from headless execution.

## One-pass native checklist

Mode `mission-computer-anchors` uses default production resolution, no pose-probe environment, no GM character and no forced combat/callback outcome. The only dev fixture policy is the existing deterministic3072 site seed on the disposable QA database.

1. As CSR110, right-click The Chessman, Use, choose Retrieval and accept the normal3072 offer. Use the ordinary mission-elevator dialogue.
2. Quit/relog before Take. Confirm the same mission and site. Approach the upright computer on the red desk next to the white dresser, full source pose(91865,4300,12660); same-floor player approach(91865,4295,12740). Observe normal walking/visibility/hover and radial Take. Headless movement is not native walking proof.
3. Use Take. Verify the disk dialogue, completed Search row, owned disk and Leave-area instruction. Leave through the normal elevator/area route; verify one reward.
4. Accept Retrieval again and repeat Take/leave. Verify a separate acceptance and one reward pair, with no duplicated disk or payout after relog.

The headless `mission-computer-anchors` scenario reuses the ordinary contact/list/accept/elevator/Take/leave driver, adds a cold process restore before the first Search, verifies the old raw source spot remains unchanged, checks actual source XYZ/quaternion bytes, and completes twice through normal client requests. This variant skips the existing repository-level completion-replay helper entirely. It preserves final receivedRPC/view bytes, selected assembly path/configuration/hash, checkpoints and owned process-disposal evidence before teardown.
