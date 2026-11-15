# Archive02 / map5 — bounded PARTIAL

This package loads 77 validated native static keys and reuses the existing 109-sector navigation data. It does **not** establish complete Archive02 geometry: sector108 declares an exterior SID resource that is absent from the available client installation. The native Zion arrival is in this sector. Geometry acceptance there remains UNPROVEN until native inspection, and missing data cannot be recreated from the navigation mesh.

## Source inventory and omission

The shipped METR contains 109 sectors. Available static layers contain 80 references / 80 unique keys, no prefab placements or rotations, and no conflicting keys. The three known designer keys in sector108 are omitted in their entirety; 77 keys remain. The unseen exterior content is **unquantified**, so 80 is an available-layer count, not a complete-world count.

The METR resource prefix after the sector bounds declares SSR/SID/SIDI/SIDG/NAV/COD IDs. Matching paths and IDs are checked against all available Archive02 layers. Sector108 (`archive02_6_1_1`) declares SID RID `0x0c00b400`: neither that packed RID, its logical packed path, nor its loose logical path exists. Available resources and hashes, METR/index hashes, the declared missing RID, and every omitted known key are retained in [the source audit](../research/world-data/archive02-static-audit.json). The 326 hashes cover 108 exterior SID +109 SIDI +109 SIDG resources.

The existing exporter defaults to refusal before writing either output when a required SID is missing. Explicit review of this gap is required:

```text
python research/world-data/static_objects_small03.py <client-data-tools> --world 5 --omit-missing-sid-sectors
```

This flag omits every known variant in the affected sector. It does not synthesize content or permit unproven rotations. Existing map12/map13/map17 exports round-trip byte for byte. Existing `data/navmesh/5.bin.gz` reproduces 109 sectors, zero prefabs, 864931 decompressed bytes, SHA256 `66716afaa144d6bbb699245fea983dd449fbc12f2ab607dbab9f9604decc8e6c`.

There are no shipped HDL/NETS rows, no supported door fixture in the safe subset, and no grounded ambient NPC captures. Two Encounter Special Agents lie outside this world footprint and remain encounter-scoped. No booth, subway, static returner, NPC placement, or substitute polygon is invented.

## Existing normal archive route

Server map5 corresponds to retail ConstructID16 / PvP archive6. The native book definition admits Zion book38533 at levels26–35 (Machines38541, Merv38547). This package changes neither `PvpArchives` nor its timing/return rules. Zion source sender38282 is approached at grounded city position `(111600,695,-6130)`. Equip the owned book from bag slot3 to program-launcher slot106 through normal `63` / `65` traffic; wait the ordinary 180-second sender cycle. The arrival source is `(-3000,-105,12000)` before existing NAV projection; the fresh wire canary observed projected arrival `(-3000,-105,11980)`. Native returner38271 is spawned by the existing archive system and returns to the exact saved origin; the book remains owned and equipped.

The `archive02-partial-world` headless scenario observes normal equip acknowledgement, sender cycle, world handshakes, ConstructID16/resource path, map5 database state, actual returner use2, city reload, exact origin and retained book. This proves server wire behavior, **not** native geometry, menus, or LA dropdown availability. There is no hardline/LA route claim and no `80fa5` proposal.

## One-pass native checklist — Claude only

Use discovered NativeQa mode `archive02`. It starts level30 Zion at the grounded city sender with owned book38533 in bag3, normal abilities2/17, no GM/dev commands and no forced random rolls. Its final ordered SQL stage sets stored level, level-derived XP and capped ability levels for the actor and cloned helpers to30; `FixtureLevel` records30 in the read-only plan/state.

1. Confirm level30 and equip book38533 into program-launcher slot106 through the ordinary UI.
2. Stand on sender38282 and wait its normal cycle without changing its clock.
3. Confirm Archive02 loads. Inspect the arrival geometry and nearby walkability explicitly: the exterior SID for this sector is unavailable. Record missing geometry as a source gap rather than accepting a headless pass as native proof.
4. Use actual Zion returner38271; confirm city load and exact source position. Confirm the reusable book remains equipped.

Validation: source exports and audit round-trip exactly; default/missing-rotation-only requests refuse missing SID before creating outputs; 109-sector navigation reproduces exactly. The fresh normal-wire scenario passed in172.9s (runner exit0, peak1), including the full city return handshake and retained book. Its success server/debug log was saved before the disposable server was torn down.

Native QA is pending. No client setup, lock, main database or security changes are part of this package.
