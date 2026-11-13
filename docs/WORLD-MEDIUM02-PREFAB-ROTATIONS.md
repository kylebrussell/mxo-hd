# Medium02 native prefab rotations (bounded partial)

The explicit reviewed native-transform export restores571 Medium02/map15 static keys:997 exported from1023 unique keys/1061 raw references. All26 conflicting keys and every variant remain omitted. All426 historical safe keys remain;9 positions now use the native float32 stores (maximum difference0.0009765625), with no old quaternion changes. This does not complete the world, population or retail rendering parity.

## Source and eligibility

This reuses the merged [native prefab math](WORLD-MEDIUM03-PREFAB-ROTATIONS.md): signed-i16 pivot*10, exact quarter turns, Hamilton parent*local, explicit native float32 stores and no normalization. Server CSV +145Y is separate from the native transform.

All25 actual placements have validated direct METR SCIN→sector SIDI parents and indexed/readable IPI resources:12 rotation0,12 rotation1,1 rotation2. METR filenames and X/Z corners match sector_properties.csv.8 optional resource aliases are blank; RID, pack bounds and SHA256 remain recorded. A missing alias is not an unknown parent. Unknown actual nested/EPI context, invalid flags and unavailable resources still refuse export. The reviewed opt-in whitelist is15/16 only; other worlds are not regenerated.

The audit retains every historical raw reference and conflict variant unchanged. `--omit-unproven-rotations` preserves its historical426-key result and597 omissions; strict default still refuses before output. This rollout changes only map15 CSV/audit. Existing navigation/hardline data and54 captured NPC rows remain unchanged:23 are within the footprint but fail the existing recorded-coordinate NAV lookup,31 outside; no relocation or NPC visibility/gameplay PASS is claimed. Existing transport/LA boundaries remain as documented in [Medium02](WORLD-MEDIUM02.md).

## Newly restored diagnostic door

Actual GO414, sector76/local56, native key0x04c00038 (`3800c004` little-endian), comes from `medium02_53.sidi` direct placement RID0x2c00054b, rotation2, pivot(600,0,880). The RID has a blank optional alias, but its139878-byte source is indexed in prefabs_4e.pkb at11389150 with SHA25678060b7d805954afaa858bcbd3968f1606098db332db0d5fa16533275e0fa805.

Local point(0,0,700) gives native(-6000,0,17100), then server(-6000,145,17100). The independent literal quaternion is `00000000f304353f00000000f204353f`: parent(0,1,0,0) times stored local(0,-0.7071067094802856,0,0.7071067690849304), without normalization. The ordinary renderer already forwards the full CSV quaternion.

Standing point(-6100,95,17100) maps to source INAV polygon24 at floor0 (local100,700); overlapping floor400/800 polygons are not that standing floor. The seemingly convenient(-6000,95,17000) has no NAV polygon and is explicitly rejected. This is a direct diagnostic start, not a walking route proof.

GO414 has schema364, OpenUsage3 and rule4004, with states834→838 after20s→835 after1s. Actual Model/DescriptionID0x2800005a resolves to nonempty902-byte `resource/GameObjects/Doors/Rotating/single_push/sl_push_knob_graymetal01texvar.moa`, SHA2561a23438fafd7cf1ca1d4a785bef908689f3c4fc6c8a69534066bd75e31fdc559. Source record and hashes: `research/world-data/medium02-prefab-door.json`. Native appearance/orientation remain pending.

## Reproduce and verify

    python research/world-data/static_objects_small03.py <client-data-tools> --world 15 --native-prefab-rotations

Use temporary output/audit paths for historical/default/other-world checks. Full/math/fixture tests and actual wire proof are recorded with the final PR. Dedicated `medium02-prefab-door` mode reuses the existing WorldDoorModePlan and normal door fixture; no dev/GM/random authority, gameplay helper or state override is added.

Claude-only native one-pass, from CSR110 alone:

1. Start the discovered `medium02-prefab-door` fixture, log in normally, confirm Medium02 and grounded standing point(-6100,95,17100), safe byte heading0. The restored gray-metal door is100 units east at(-6000,145,17100); turn the camera to it as needed.
2. Inspect actual placement, full orientation, hinge and rendered model relative to surrounding geometry. The wire quaternion test does not establish native appearance or pivot correctness by itself.
3. Right-click that real door and select **Open**. Confirm normal radial option/use and opening, natural closing after20 seconds and closed state another second later. No invented Close command or state mutation.
4. Repeat Open once; verify the ordinary interaction remains usable. Report missing model, orientation, collision/reach or radial discrepancies. Do not treat this diagnostic as arrival/hardline/LA/subway/NPC/walk-route validation.

## Recorded validation

Full4347 tests passed; final scoped Medium02/Medium03/Archive02/actual-CLI checks47 passed after the pure master437fd4c6 import. Python native-math6 and audit/link12 passed. Current map15 CSV/audit reproduce byte-for-byte; historical15 and unchanged native16/default17/historical12 reproduce the baseline with exact content after newline normalization. Strict map15 refuses before output. No other world CSV/audit was regenerated in this PR.

One actual-wire case passed36.3s, exit0, peak1, with its throwaway server disposed. Frozen fresh sourcec7b69f0d526c1aa73bbee4681e3ee292ecbcec3c used actual **Debug** hds.dll SHA256B8A3520259AE223DB0F5AC81079F7AED71313F0648B06EFC2B6ED23840B82A9C. The fixture saved Temp/mxo-medium02-prefab-door-actual-wire.json before teardown: actual config/path/hash, five received03 view lists, exact position/quaternion and834→838→835 records. NativeRenderingProven remainsfalse. Raw server success and runner logs were also retained.

After the pure Archive02/master import, both configurations and read-only plans/provenance passed again: Debug60A745E592A03CB8DFF87C002AAFD206E9B0EF9B8EF1DCC68BEBE4343632C402 / Release44A1B560B5EFAF40184B2803EED106FEEA5886E1B3D42D9944D496DA06D6C088. Own map15 data/fixture/door-helper bodies remain byte-identical to the fresh source; upstream loader/exporter additions preserve the Archive02 missing-resource guard. No native result is claimed.

The export audit records322 source hashes, including21 distinct IPI RIDs. Current CSV SHA256b3a29cee7e6c4563538b593054c477baf1d69eb8a9c9153ad2cb1e54fa3743cb; audit673bec6b2980896230a8c99d90b47bcfd5bc58b0a13e4a1a4e0d1e3887c6c202.
