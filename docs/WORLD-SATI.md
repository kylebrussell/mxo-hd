# Sati's Playground / map9 — PARTIAL

This package loads2,599 strictly safe native static keys alongside the existing31-sector navigation. It does not add an engine, transport packet, actor population or retail arrival formula. Runtime DB key remains `archive_sati`, the shipped resource is `archive_sati/sati.metr`, server map9 is retail construct18. PvpArchives is unchanged.

## Source boundaries

The METR has31 sectors; all31 SID/SIDGO/SIDI/NAV layers are available.268 prefab placements comprise62 rotation0,73 rotation1,45 rotation2 and88 rotation3.8,177 references produce7,987 distinct `(sector,staticId)` keys.5,381 keys have a nonzero prefab rotation;70 have conflicting variants;63 overlap. **Every variant of all5,388 such keys is omitted**, leaving2,599. No first-wins or guessed quaternion transform is used. Raw unsupported transformed coordinates in the audit are UNPROVEN diagnostics, never exported placements.

Strict default refuses unsupported rotations before output. Explicit partial export:

```
python research/world-data/static_objects_small03.py C:/Users/russe/Developer/mxo-research/tools/client-data --world 9 --omit-unproven-rotations
```

`archive_sati-static-audit.json` retains all8,177 references, conflicts, excluded keys and source hashes. `sati-resources.json` audits all124 sector layers plus METR/HDL,58 prefab source hashes and all95 safe GOID contracts from the raw GOB and extracted usable definitions. Identically named resource aliases must have identical bytes. These include doors, props, spawners, sounds and four generic returners; source markers/props are **not claims that all NPCs/effects spawn**. Zero map9 NPC captures occur across all captured-scope CSVs; existing population behavior is unchanged. No NPC visibility is claimed.

Packed HDL has NextID2 and zero hardline nodes; proper NETS parsing finds zero CNET networks. No hardline/subway/node is fabricated. Existing MXNV3/map9 contains31 sectors/58 prefab meshes,512,010 decompressed bytes, SHA256 `2af0a19c11c77af5dfa6a388739061a1ef2ebd1880a651bd78813cb36b31f930`; source NAV reproduction must match exactly. Static CSV+145Y convention is retained, not claimed as a native pivot.

## Existing transport remains an OURS fallback

The recovered archive script lacks a Sati arrival. Existing PvpArchives uses OURS `NearestEntry` near `(-500,2100,500)`, resulting in `(14070,95,-4070)`, sector2/region3/polygon6. This is grounded but **not a recovered retail arrival**. The generated organization returner ALWAYS remains. Owned Zion book47726 has source PvPID8, organization1, levels1–100; ordinary equip106 and the actual180-second sender cycle remain authoritative. Return uses the existing city-origin session; no fake session is seeded.

Four safe source `Generic_ArchivistBase_Returner`47267 placements remain **UNBOUND**:23/1335 `(30200,145,-18800)`,23/1347 `(23500,145,-16200)`,27/411 `(23400,145,-9400)`,28/1434 `(33200,145,-11200)`. Each has NAV standing95. Their GOB schema3602/GeneralSuccess usable does not justify silently reusing the map7 binding engine here. This PR adds no proxy renderer/executor, relocation or PvpArchives change. Use of those source proxies and exact retail arrival remain explicit gaps.

## One safe source door — separate diagnostic

GOID365 at sector0/local8/static `0x00000008`, source `(34800,145,-13700)`, is unique, rotation0, identity quaternion and outside every omitted-key set. Verified standing `(34800,95,-13800)` is on indoor NAV sector0/region68/floor0. Source schema364 offers ordinary Open/use3/rule4004 at range1000, default Closed835; Open834 naturally becomes Closing838 after20s and Closed835 one second later. **No manual Close is sourced.**

The new `sati-door` profile reuses PR173's generic door fixture/mode/scenario. Its optional explicit resource suffix accounts for `archive_sati/sati.metr`; the old default remains exactly `$District/$District.metr`, and both old profiles' serialized plans remain byte-identical. Their complete manifests differ only by two empty environment keys imported upstream from156. No production resource-path code changes.

This is a direct diagnostic standing point, **not proof of a walk route from the normal arrival**, normal arrival itself, interior accessibility or complete geometry. The ordinary `sati` mode remains at the real city sender.

## Claude-only native one-pass checklist

1. Normal mode `sati`: actual default50/XP50, Zion, abilities2/17, safe protocol heading0, owned47726 bag3 at real Richland sender38282 `111600/695/-6130`. No dev/GM/forced rolls. Double-click/equip into launcher106 (an Equip menu may be absent), stand on the pad and wait up to **three real minutes** for ordinary timing plus loading.
2. Confirm Sati world/grounding at existing OURS fallback14070/95/-4070. Right-click the generated Zion38271→Use2; confirm full city reload, exact original sender position and retained reusable book. Do not substitute a generic47267 source proxy, hardline, Loading Area selection or teleport.
3. Separately run discovered `sati-door` using the existing NativeQa workflow. This explicitly starts directly at34800/95/-13800, heading0. Confirm nearby door/interior render, right-click Open, observe Open834→natural Closing838 after20s→Closed835 after1s. No manual Close, timing override or walk-route claim.
4. Record missing geometry/menus/collision separately. No headless packet assertion establishes native rendering, traversal, source-proxy usability or retail parity.

Headless `sati-partial-world` performs normal book/cycle/world/full-city-return then invokes the shared door diagnostic with a separate fixture player; it never pretends that actor walked from arrival. One driver saves owned raw logs before teardown. Validation/native status is reported in the PR and append-only handoff. No generated parity snapshots are changed.
