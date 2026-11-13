# DataMine reviewed prefab rotations (partial)

This restores 6,680 DataMine keys through the existing client-derived direct-sector transform, exporting 12,785 known-safe keys. All 175 conflicting keys and every variant remain omitted. The whole missing-SID sector34 remains excluded; its unavailable exterior count is unquantified. This changes only map7 static placements and audit evidence, plus a diagnostic door profile. PvpArchives, returners, fallback, book admission, arrival, NPCs, hardlines and navigation data are unchanged.

## Primary placement and resource gate

All 59 METR parent names/corners match the source sector table. All 252 actual direct-sector placements are represented: rotations0/1/2/3 count125/53/65/9, all upper rotation words zero. Every primary resource is readable/indexflags4/type6400; every secondary RID is readable/indexflags4/type12544. Both resource identities, hashes, lengths and opaque metadata are retained. Secondary RID→IPI+dc and optional metadata→+9c do not enter the already recovered pivot/rotation matrix. Unknown/nested/EPI parents remain refused.

Historical available-layer counts are13,219 references/12,960 keys,6,821 rotation-touched keys and175 conflicts (141 overlap). All6,105 old keys remain. Native float32 stores round157 old positions by at most0.0009765625; no old quaternion changes. All eight GO47267 exterior returner CSV rows remain byte-for-byte identical, with no extra47267. DataMineReturners.Validate still accepts the exact eight IDs/poses/floors. New native-pose duplicate groups have no disagreement; historical conflicts are excluded even when corrected poses coincide.

Strict default refuses nonzero rotations. Native opt-in is reviewed only for7/16 and still independently requires the existing whole-missing-sector option:

    python research/world-data/static_objects_small03.py <client-data-tools> --world 7 --native-prefab-rotations --omit-missing-sectors

Historical `--omit-unproven-rotations --omit-missing-sectors` remains available. Sector34's missing required SID0x04000800 is not repaired or silently accepted.

## Independent door fixture

GO355/schema364, sector18/local21/staticLE15002001, is a real Door zz Push Knob Metal1. Source datamine1_1_7.sidi references primaryRID0x04000ad9, dt_skyscraper01_lby_03_nonpc.iprf. Direct rotation1, pivot(710,360,630), sector corner(-18000,0,-9000), local position(300,0,0) produce native(-10900,3600,-3000), existing CSV+145→(-10900,3745,-3000). Local identity quaternion gives the independent quarter-turn literal `00000000f304353f00000000f304353f`, without normalization.

Model0x280001d1 is the nonempty891-byte zz_push_knob_metal1.moa; SHA2567160c8744cf539c82fd33b7a11c89e48ea7b7fa506c920966e0a7443c22b44e3. GOB defines ordinary Open3/4004, default835, opening834→838 after20seconds→835 after1second. No manual Close action is invented.

Standing(-11000,3695,-3000) is NAV18/131/224, floor3600/indoors. This is a sourced high prefab floor, not a street relocation. Literal model/resource/pose and preserved returner rows are in research/world-data/datamine-prefab-door.json.

## Claude-only native one-pass

Select discovered `datamine-prefab-door`, with no dev/GM/RNG. It starts at that standing point with byte heading0 and loads the actual archive_datamine/datamine.metr path. Inspect visible model, frame/pivot, collision and normal radial Open; then observe natural close after21seconds. Report clipping/blocked geometry separately. Native rendering is pending.

Direct diagnostic start proves no walking route, normal book/transporter admission,180-second cycle, city-origin return, source-proxy appearance, NPC population or LA/UI behavior. The existing generated returner remains ALWAYS available. Original169 native crash causality remains unresolved; no appearance/transport fix is claimed here.

Automated validation pending for this revision.
