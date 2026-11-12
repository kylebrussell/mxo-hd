# Yuki / map11 — bounded PARTIAL

This package loads 246 validated native static keys and reuses 49 **available** NAV sectors. It does not prove complete Yuki geometry: sector5 declares a nonzero SIDI resource that is unavailable, so its interior contents and prefab extent are unquantified. No substitute interior, placement, polygon, hardline, NPC or transport rule is invented.

## Primary source and omission

The METR has49 sectors. Available static data contains49 exterior SID,48 SIDI and49 SIDG resources, recorded with146 hashes in [the source audit](../research/world-data/yuki-static-audit.json). Available layers contain272 references/272unique keys:204 exterior and68 designer, no known prefab placements, nonzero rotations or ambiguities. Every known variant in missing-layer sector5 is omitted:26 known keys removed,246 remain. The unseen interior is not counted as zero content.

The METR resource prefix explicitly declares `archive_yuki_22.sidi`, RID `0x0c00a800`. That RID is absent from the packed index, its logical packed path is absent, and its loose logical path is absent. All other SID/SIDI/SIDG/NAV resource ID/path pairs match. The audit retains all272 available references, all26 omitted keys, the missing RID/path/absence evidence, METR/index hashes and the unknown-extent boundary.

The existing exporter refuses required missing static layers **before opening outputs** unless the matching explicit permission is given:

```text
python research/world-data/static_objects_small03.py <client-data-tools> --world 11 --omit-missing-static-layer-sectors
```

The older `--omit-missing-sid-sectors` remains SID-only. It must refuse this missing SIDI; it cannot silently broaden into interior permission. The new opt-in allows the reviewed whole-sector omission and does not bypass rotation/ambiguity guards. Available NAV11 is49 sectors/zero known prefabs,1409293 decompressed bytes, SHA256 `7243cf253508060d7e141d3cca18fa2cf806a4f558d037176fcd473f0444a58e`. This does not recover or establish completeness of missing SIDI-derived interiors.

The proper HDL parser returns NextID3 with zero hardline nodes; the proper NETS/CNET container decoder returns zero networks. Every source GOID is classified against usable/state contracts:505×95,2506×65,38479×43,38481×33,38483×33,38271/38272/38273×1 each. Only the faction returners have stored usables (class292, Use2,9007 TeleporterOrgCheck). No door interaction contract exists in this subset.

All captured population sources were inspected. Instance.csv has179 map11 rows; all lie outside the native2Dworld footprint. They are preserved without relocation or a grounded NPC/visibility/gameplay claim.

## Existing book route and separate source-static gap

PvP archive4 maps to servermap11 / retailConstructID14. Native GOB books38535(Zion),38543(Machines),38549(Merovingian) admit levels15–25. The mode uses actuallevel20 Zion at grounded citysender38282 `(111600,695,-6130)`, with owned38535 in bag3. Equip into program-launcher slot106 through normal63/65 traffic, then wait the ordinary180-second sender cycle. Existing arrival projection returns `(15500,295,15500)`, sector27/region46/polygon2, floor200+95. It is outside the missing-layer sector. The existing dynamic Zion returner38271 at arrival provides Use2 and the exact saved-session origin return; no runtime transport changes are made.

Source-static returners are preserved: Zion27/22 `(17700,145,15550)`, Machines48/23 `(-25500,145,37500)`, Merovingian20/23 `(-24450,145,-7850)`. Their source-static usability differs from the existing dynamic arrival path and remains unresolved. The scenario selects the dynamic returner by exact Position, rather than accidentally testing a source-static object. There is no independent47267, NPC engine or PvpArchives change, nor any guessed80fa11 or LA UI claim.

## One-pass native checklist — Claude only

Use discovered NativeQa mode `yuki`. The final generic SQL stage sets actuallevel20, matchingXP20 and actor/helper loadouts capped20 after the shared default XP/loadout stage. Heading is explicit byte0. Metadata records FixtureLevel20; no dev/GM permissions or forced rolls are enabled.

1. Confirm level20. Double-click owned book38535 from bag3 to equip it into program-launcher106 (Archive01 native evidence found double-click rather than an Equip menu item).
2. Stand on actual Zion sender38282; wait its normal cycle and confirm Yuki/retail14 loads.
3. Inspect arrival geometry and a short local walk. Missing sector5 interiors remain a separate source gap even if arrival renders.
4. Use the dynamic Zion returner38271 at arrival. Confirm full city reload, exact saved origin and retained book. Record source-static returner usability as unresolved.

Native QA is pending. Build/unit/fresh verification is pending available disk capacity; no native setup/client/lock/mainDB/settings/cleanup actions are part of this package.

## Existing Archive01 gap exposed by the stricter exporter

The generic SID/SIDI/SIDG preflight also exposes a pre-existing Archive01/map4 gap. Sector2 declares `archive01_15.sidgo`, nonzero RID `0x96000400`, but its packed RID, logical packed path and loose path are absent. The resource-prefix SIDG field is crosschecked against392 other Archive01 SIDGO path/RID pairs,109 Archive02 pairs and49 Yuki pairs; neighboring `0x960003ff` and `0x96000401` are actual adjacent Archive01 SIDGO resources of the same packed type13824. It is not a mislabelled SIDI field or a null reference. Whether the native engine treats this absent nonzero reference as optional/reserved remains UNPROVEN; this exporter conservatively refuses it.

There are zero known available-layer keys in Archive01 sector2; unseen designer contents remain unquantified. This supersedes the earlier claim that the current strict exporter reproduces a complete Archive01 source inventory by default. The tracked Archive01 CSV/runtime and prior native observations are unchanged. Default Archive01 export now refuses before output; even the broad opt-in remains blocked as an unreviewed world4 omission pending a separate follow-up inventory/decision. This Yuki package does not silently exempt the gap, regenerate Archive01 data or implement its missing content.
