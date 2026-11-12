# Widow's Moor / map10 — bounded PARTIAL

This package loads 70 validated native static keys through the existing world loader and reuses the existing 86-sector NAV mesh. It does not establish full native geometry, NPC gameplay or the usability of every source object. Server map10 is retail ConstructID13 / PvP archive3.

## Source boundary

The shipped METR has 86 sectors. All 86 exterior SID, 86 SIDI, 86 SIDG and 86 NAV resources are available. The static audit retains 258 resource hashes and all 70 references: 27 exterior and43 designer keys, no prefab placements, nonzero rotations, ambiguities, missing layers or omitted keys. The default exporter remains strict about unsupported rotations. Regenerate through the existing exporter:

```text
python research/world-data/static_objects_small03.py <client-data-tools> --world 10
```

[The audit](../research/world-data/widowsmoor-static-audit.json) classifies every GOID against stored usable/state definitions:587×18,2506×40,7342×7,38488×1,38489×1 and38271/38272/38273×1 each. Only the latter three expose an existing usable: class292, Use2, execute9007 TeleporterOrgCheck. There are no door contracts among these keys. The proper HDL parser returns NextID2 with zero hardlines; the existing METR NETS/CNET container decoder returns zero networks. Raw schema tags are not network instances. No hardline booth, subway or LA dropdown is invented.

The existing compressed NAV reproduces exactly: 86 sectors, zero prefabs,1172937 decompressed bytes, SHA256 `91228b8c4861511b4baba19b93a57067423801deeee305b8fee3b61b6516de42`.

All captured population sources were inspected. Instance.csv has17 map10 rows:16 within the 2D footprint and1 outside. Every row fails NAV lookup at its exact recorded coordinates. The rows remain untouched; there is no NPC relocation, visibility, grounded combat or level-normalization claim. Native mode disables ambient fixture mobs so inspection does not imply those captures were repaired.

## Normal book and return route

Native GOB tickets38536(Zion),38544(Machines),38550(Merovingian) specify archive3 and levels36–45. The fixture uses actual level40 Zion at grounded city sender38282 `(111600,695,-6130)`, with owned book38536 in bag3. Equip it in program-launcher slot106 using ordinary63/65 traffic, then wait the normal180-second sender cycle. Existing `PvpArchives` projects Zion arrival to `(-18500,95,-7500)`, sector54; the actual projection is tested. It saves the exact session origin and provides an ordinary dynamic Zion returner38271 at arrival. Use2 returns to that saved origin and retains the reusable book. No transporter timing, book rule or runtime return logic changes are made.

The native static source also contains returners: Zion54/3 at `(-17550,145,-5500)`, Machines31/4 at `(19350,145,-5400)`, Merovingian46/4 at `(8800,171,18100)`. These placements are preserved. They differ from existing dynamic arrival returners. Current return validation is near the existing arrival; usability of the source-static returner away from that point remains a separate gap. The fixture and checklist use the existing dynamic returner at arrival and do not claim this gap fixed.

## One-pass native checklist — Claude only

Use discovered NativeQa mode `widowsmoor`. Its final generic SQL stage sets stored level40, corresponding XP and actor/helper ability levels capped40 after the shared default loadout stage. Read-only metadata records FixtureLevel40. No dev/GM permissions, forced rolls, new clock or shared NativeQa changes are required.

1. Confirm level40 and equip owned book38536 from bag3 into program-launcher slot106.
2. Stand at Zion sender38282 and wait the normal cycle. Confirm Widow's Moor / retail13 loads.
3. Inspect the actual arrival geometry and a short local walk. This remains native QA pending; a headless wire pass does not prove geometry acceptance.
4. Use the existing dynamic Zion returner38271 at arrival. Confirm full city reload, exact saved origin and retained book. Record the separate source-static returner discrepancy without treating it as fixed.

No NPC, door, subway, hardline or LA UI acceptance claim is part of this checklist. Native QA is pending.

## Validation

Full4255 units passed on the master160 base; after importing master163,34 focused Widow/mode/mission-replay tests passed. Both configurations build and their actual read-only NativeQa plans agree. Python12 audit-policy tests passed; source70/map12/map13/map17 CSV+audit exports round-trip exactly, and NAV10 reproduces exactly.

The fresh normal-wire scenario passed in144.2s, exit0, peak1, disposed. It observed book38536 equip106, the ordinary sender cycle, retail13/map10 load, arrival `(-18500,95,-7500)`, dynamic38271 selected by that exact Position, Use2, full city reload, exact origin `(111600,695,-6130)` and retained book. The success server/debug log was saved before teardown. This is server wire proof; native geometry and source-static returner usability remain pending/unresolved.
