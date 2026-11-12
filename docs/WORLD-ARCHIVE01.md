# Archive01 / Zero One — PARTIAL

This package adds 156 validated native static references to the existing server map4. Retail construct ID is **15**, independently present in the captured Archive01 world load and `rshooks.LookupCID`; map4 does not establish loading-area eligibility. Existing archive books, ordinary timed senders, dynamic returners, navigation and origin sessions remain unchanged. No loading-area packet or hardline is invented.

## Primary resource gate

Client `resource/worlds/final_world/constructs/archive/archive01/archive01.metr` has393 sectors. All393 have SID/SIDI;392 have NAV. `archive01_100_1_1_17_1_11_1_0` has no packed NAV: navigation there is unavailable; no interpretation of an unrecovered raw flag is claimed. Existing `data/navmesh/4.bin.gz` is MXNV3,392 sectors,1,747,995 decompressed bytes and remains unchanged.

The existing shared exporter reads156 raw references/156 distinct native `(sector,staticId)` keys, with zero ambiguous keys, nonzero prefab rotations or prefab placements. Strict mode exports all156 without exclusions. Actual object inventory:

| GOID | Source name | Count |
|---|---|---:|
|405|steam_large|52|
|2506|GangMaster|83|
|38273|Merovingian_ArchivistBase_Returner|1|
|502|black smoke large|13|
|38272|Machine_ArchivistBase_Returner|1|
|37910|construct_windmill|4|
|504|black smoke medium|2|

All seven were checked against gameobjects/usables definitions; there are no door definitions in this set. Only38272/38273 have usable rows (Use/action2, TeleporterOrgCheck). FX/GangMaster/spawner references are data, **not**83 ambient actors or a claim that all client FX spawn. Packed HDL parses NextID2 with zero hardline nodes. Proper METR NETS parsing finds zero CNET networks. No subway or booth is authored.

Native SID returners retain their raw locations:38273 sector44/static3 `(-46000,945,5000)`,38272 sector167/static1 `(26000,-53,5000)`. Corresponding NAV standing heights895 and-105 differ by50/52, consistent with the existing CSV+145 versus standing+95 conventions. These rows do not replace the existing dynamic returners at source-defined arrivals.

All six existing source arrivals have exact NAV standing floors; Zion is `(-4000,95,-46000)`, sector2/region5/polygon0. Existing `NavMesh.Project` then applies its20-unit inward inset: actual Zion arrival is `(-4020,95,-46020)`, verified in the executable unit.28 captured NPC rows remain byte unchanged. Although all28 lie inside sector XZ footprints,10 have no containing XZ NAV polygon and the other18 have nearest standing-height discrepancies greater than100. No grounded NPC visibility, relocation, deduplication, gang population or full world completeness is claimed.

## Reproduce

Use the existing shared decoder, not a second parser:

```
python research/world-data/static_objects_small03.py C:/Users/russe/Developer/mxo-research/tools/client-data --world 4
```

Source hashes and every decoded reference are retained in `research/world-data/archive01-static-audit.json`. The common convention remains sector corner plus native local position, +145Y; it is not a new native pivot claim. Existing large01/small03 outputs must roundtrip unchanged.

## Claude-only native one-pass checklist

Discovered mode: `archive01`. It copies CSR110 into the normal disposable fixture, level50/Zion, abilities2/17, with owned Zero One book38534 in bag3. No dev commands, GM, forced rolls or fake archive session. Start at actual Richland Zion sender38282, `111600/695/-6130`;695 is grounded standing height, not static CSV745. This sender's usability definition has no manual Use action: stand on its pad and let its existing state cycle operate.

1. Open inventory; equip the owned Zero One level40–50 Zion book into program-launcher/book slot106 using normal UI. Bag possession alone does not authorize departure. Confirm rendered sender/pad and stand on it.
2. **Wait up to three real minutes** for the ordinary180s sender cycle. No timer acceleration or fabricated reply. Confirm Zero One terrain/resource loads; distinguish retail construct15 from saved map4.
3. At projected Zion arrival `-4020/95/-46020`, inspect grounding and the existing Zion return teleporter38271. Its proven usable action is2, native label **Use**. Use normal right-click/radial Use; confirm return to the original Richland sender position and that the book remains equipped/reusable.
4. Record any missing rendering/menu/transition separately. Do not teleport to unsupported NPC captures or claim GangMaster actors/ambient FX from static data. Loading-area UI access, geometry beyond observed points, and full population remain unverified.

Headless scenario `archive01-partial-world` follows the same owned-book equip, ordinary timed sender, world-load, actual returner action and exact origin-session return. Its packet assertions are not native UI proof. Fresh/native results are reported in the PR and append-only handoff.

Fresh validation: **PASS170.3s, EXIT0, parallel1**, including the full ordinary sender wait, exact projected arrival, origin-session return, reusable book and zero wire-parser problems. Raw runtime was copied before teardown (SHA256 `2462ecbeee1a71c0e9e1a584fcc5ea3ba3390262130ee466a915d4f2bcc38cb0`). Initial unit assertions incorrectly assumed the raw arrival survived projection, then omitted its Z inset; both failures were retained and only fixture expectations changed.44 focused tests pass. Native UI remains pending.
