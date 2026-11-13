# Sati's Playground / map9 — PARTIAL

This package loads2,599 strictly safe native static keys alongside the existing31-sector navigation. It does not add an engine, transport packet, actor population or retail arrival formula. Runtime DB key remains `archive_sati`, the shipped resource is `archive_sati/sati.metr`, server map9 is retail construct18. A Sati-only authored fallback placement now separates the arrival and generated returner on a validated source floor; other archive layouts remain unchanged.

## Source boundaries

The METR has31 sectors; all31 SID/SIDGO/SIDI/NAV layers are available.268 prefab placements comprise62 rotation0,73 rotation1,45 rotation2 and88 rotation3.8,177 references produce7,987 distinct `(sector,staticId)` keys.5,381 keys have a nonzero prefab rotation;70 have conflicting variants;63 overlap. **Every variant of all5,388 such keys is omitted**, leaving2,599. No first-wins or guessed quaternion transform is used. Raw unsupported transformed coordinates in the audit are UNPROVEN diagnostics, never exported placements.

Strict default refuses unsupported rotations before output. Explicit partial export:

```
python research/world-data/static_objects_small03.py C:/Users/russe/Developer/mxo-research/tools/client-data --world 9 --omit-unproven-rotations
```

`archive_sati-static-audit.json` retains all8,177 references, conflicts, excluded keys and source hashes. `sati-resources.json` audits all124 sector layers plus METR/HDL,58 prefab source hashes and all95 safe GOID contracts from the raw GOB and extracted usable definitions. Identically named resource aliases must have identical bytes. These include doors, props, spawners, sounds and four generic returners; source markers/props are **not claims that all NPCs/effects spawn**. Zero map9 NPC captures occur across all captured-scope CSVs; existing population behavior is unchanged. No NPC visibility is claimed.

Packed HDL has NextID2 and zero hardline nodes; proper NETS parsing finds zero CNET networks. No hardline/subway/node is fabricated. Existing MXNV3/map9 contains31 sectors/58 prefab meshes,512,010 decompressed bytes, SHA256 `2af0a19c11c77af5dfa6a388739061a1ef2ebd1880a651bd78813cb36b31f930`; source NAV reproduction must match exactly. Static CSV+145Y convention is retained, not claimed as a native pivot.

## Existing transport remains an OURS fallback

The recovered archive script lacks a Sati arrival. The previous OURS nearest-entry fallback placed both the traveller and returner at `(14070,95,-4070)`, only20 units inside a corner of sector2/region3/polygon6. Three corners of the default38271 footprint lay outside the available same-floor NAV. Claude reported a hover-only returner with337zero and no80c7; that report does not prove the native picking cause.

The new **OURS** arrival is `(14225,95,-5225)` and the separate generated returner is `(14225,95,-4925)`,300 units/3m ahead at heading0. Both are on the same flat floor0 polygon; the complete default25/95/95 returner footprint is inside it. Production `Project`/`Locate` require the exact points, a direct same-floor route and all footprint corners. No arbitrary bounds resize, model, organization, rule9007, default-component,0c or identity-orientation change is made. Source proof: `research/world-data/sati-fallback-layout.json`. **Retail arrival remains unrecovered; Claude passed native targeting/radial/Use on2026-11-13 atcb051e11. Native walking from arrival to returner remains unverified.**

The generated organization returner ALWAYS remains. Owned Zion book47726 has source PvPID8, organization1, levels1–100; ordinary equip106 and the actual180-second sender cycle remain authoritative. Return uses the existing city-origin session and distance to the actual returner, without a fake session. All other archive layouts and the four unbound source proxies are unchanged.

Four safe source `Generic_ArchivistBase_Returner`47267 placements remain **UNBOUND**:23/1335 `(30200,145,-18800)`,23/1347 `(23500,145,-16200)`,27/411 `(23400,145,-9400)`,28/1434 `(33200,145,-11200)`. Each has NAV standing95. Their GOB schema3602/GeneralSuccess usable does not justify silently reusing the map7 binding engine here. This PR adds no proxy renderer/executor, relocation or PvpArchives change. Use of those source proxies and exact retail arrival remain explicit gaps.

## One safe source door — separate diagnostic

GOID365 at sector0/local8/static `0x00000008`, source `(34800,145,-13700)`, is unique, rotation0, identity quaternion and outside every omitted-key set. Verified standing `(34800,95,-13800)` is on indoor NAV sector0/region68/floor0. Source schema364 offers ordinary Open/use3/rule4004 at range1000, default Closed835; Open834 naturally becomes Closing838 after20s and Closed835 one second later. **No manual Close is sourced.**

The new `sati-door` profile reuses PR173's generic door fixture/mode/scenario. Its optional explicit resource suffix accounts for `archive_sati/sati.metr`; the old default remains exactly `$District/$District.metr`, and both old profiles' serialized plans remain byte-identical. Their complete manifests differ only by two empty environment keys imported upstream from156. No production resource-path code changes.

This is a direct diagnostic standing point, **not proof of a walk route from the normal arrival**, normal arrival itself, interior accessibility or complete geometry. The ordinary `sati` mode remains at the real city sender.

## Claude-only native one-pass checklist

1. Normal mode `sati`: actual default50/XP50, Zion, abilities2/17, safe protocol heading0, owned47726 bag3 at real Richland sender38282 `111600/695/-6130`. No dev/GM/forced rolls. Double-click/equip into launcher106 (an Equip menu may be absent), stand on the pad and wait up to **three real minutes** for ordinary timing plus loading.
2. Confirm Sati world/grounding at OURS arrival14225/95/-5225. The generated Zion38271 is3m separate at14225/95/-4925, on the same platform. Walk toward it normally; confirm actual targeting/radial interaction, then use it and confirm full city reload, exact original sender position and retained reusable book. If clicks still produce only337zero, record that failure; do not claim clickability from a headless80c7 alone. Do not substitute a generic47267 source proxy, hardline, Loading Area selection or teleport.
3. Separately run discovered `sati-door` using the existing NativeQa workflow. This explicitly starts directly at34800/95/-13800, heading0. Confirm nearby door/interior render, right-click Open, observe Open834→natural Closing838 after20s→Closed835 after1s. No manual Close, timing override or walk-route claim.
4. Record missing geometry/menus/collision separately. No headless packet assertion establishes native rendering, traversal, source-proxy usability or retail parity.

Headless `sati-partial-world` performs normal book/cycle/world/full-city-return and verifies ordinary movement through a witness. The unchanged door diagnostic remains a separate registered case; it never pretends that actor walked from arrival. One driver saves owned raw logs before teardown. Validation/native status is reported in the PR and append-only handoff. No generated parity snapshots are changed.

## Validation

Full Release4330 tests PASS; initial24 focused tests PASS and final27 focused tests PASS after upstream-only Widow10 integration. Both NativeQa configurations build; actual new/unknown profiles and provenance pass. Both old173 serialized Plan blocks are BYTE-EXACT; full manifests differ only by two empty environment keys imported upstream from156. Python12/snapshot policy pass; strict9 refuses before output, CSV/raw audit/resource95-contract audit reproduce exactly, and source NAV reproduces all512,010 decompressed bytes exactly. Current all-module ignored audit has532partial/27cosmetic/1missing/1inaccessible/0done and exact3-artifact CRLF roundtrip; historical snapshots stay unchanged.

Combined ordinary-wire fresh **PASS86.4s, EXIT0, parallel1/peak1, disposed**:47726 equip106, real sender cycle, retail18/map9, existing OURS arrival, generated38271 Use2, full city resource/new-self reload and exact origin/book retained. A SEPARATE actor then used exact source365/0/8 Open3 and observed834→natural838→835. No walking-route or native UI claim. Owned raw logs were saved before teardown (main and door SHA256 `caf3878d1e4f72bf28a0d63438f3b673107ef9723d5548146cfac538ef7331da`). Fresh-tested0bb342e3 data/modes/driver/QA helper remain byte-identical after pure upstream10 union; final changes document results only. Native one-pass remains pending.

### Separated fallback repair (13 November)

Full Release4405 tests,17 focused tests and Python12 policy checks passed before the fixture-only evidence update. Both NativeQa configurations and actual read-only plans/provenance passed. Fresh source `dac9aa8b8e4ca91a42d30f081ad54591dd88b960` passed the normal book route in96.3s, EXIT0, parallel1/peak1, disposed. Actual selected Debug DLL SHA256 `58451bb4142bbc5fb747b6164c2002af846108a4311b30677c087d71df649041`. Actual06/map9, actor14225/95/-5225, separate38271 at14225/95/-4925 with identity orientation, ordinary witnessed movement, Use2, full city reload, exact origin and retained book were verified. Raw and wire evidence were saved before teardown. The separate unchanged door case was not repeated. Earlier86.4s proof above is historical baseline. Native targeting/clickability remains PENDING Claude retest; this route does not establish the native picking cause.


## Native pass and current-master fixture maintenance

Claude passed cb051e119ea27da75521fb2918edc7950ab68250 natively at14:55ET on2026-11-13. The normal Zion book47726 equipped to106, the ordinary sender cycle reached Sati, the separate returner targeted and offered Details/Use, and Use returned through a full city reload to exactly111600/695/-6130 with the book retained. Separate sati-door rendered/hovered/targeted, opened and naturally closed after about21seconds. No walk route from arrival to returner or four unbound47267 proxies was tested. Earlier native-pending statements describe historical evidence.

Current masterc31c9d8b was merged after that pass. The only conflict was the NativeWorldDoorFixtures registry: both Sati and Large02Prefab definitions are retained. SatiFallbackLayout.cs, PvpArchives.cs and the Sati static CSV remain byte-identical to the native-tested head; all Sati source/mode/driver artifacts are unchanged. DataLoader.cs is not wholly byte-identical because master adds the unrelated Courier standard-mission ContactOfferWithStandards method. Its current bytes are verified to equal exact master plus only the unchanged Sati load line, preserving mixed line endings. No prior native evidence is relabeled as a test of that upstream method.

Twenty combined Sati/fallback/door/contact checks and nine Python world-data checks passed. Both actual NativeQa builds and read-only sati/sati-door plans/provenance passed: Debug29D73C65DCD0BE617563EB71237E51604DCA64E9A3D0D74DD19EA257F1F276E6; Release5FC7CE487A746A64BF30FAD0AED7DCA5C78D2927CF8A6398D28D109ACB0D51B8. Original96.3-second fresh proof remains pinned to its source. No new full/fresh/native run was performed for this registry union; final documentation is after the checked merge.


## Reviewed prefab rotation extension (PR181)

The reviewed explicit world 9 native-prefab-rotations export restores 5,318 keys: current dataset 7,917, all 70 conflicting keys/all 140 variants omitted. All 2,599 historical safe keys remain; no source proxies are newly bound and no parent arrival/returner policy changes. See WORLD-SATI-PREFAB-ROTATIONS.md for primary transform evidence, omissions and separate native door checklist. The parent native results above retain their original dataset/source attribution.
