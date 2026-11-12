# Ashencourte / map6 — PARTIAL

This package adds223 validated native static references using the existing shared exporter/loader. Existing archive navigation, owned-book gates, ordinary180-second sender, dynamic returners and origin-session return are unchanged. Retail construct11 is distinct from server map6. No PvpArchives executor, hardline, loading-area reply, NPC placement or spawn classification is added.

## Primary inventory

`resource/worlds/final_world/constructs/archive/archive_ashencourte/archive_ashencourte.metr` has86 sectors. All86 have packed SID, SIDI, SIDGO and NAV; no missing layer. Existing `data/navmesh/6.bin.gz` is unchanged: MXNV3/map6/86 sectors,1,148,495 decompressed bytes, SHA256 `2c7b23625a4b78a94cf6f0ccd982d305798d71f11cd947f79b005d7289086631`.

223 raw references/223 native keys are safe: zero conflicting keys, prefab placements, nonzero prefab rotations or omissions. All11 distinct GOIDs were checked against names and actual usability definitions:

|GOID|Source name|Count|
|---|---|---:|
|587|oildrum_fire|18|
|38489|Candle Tall|91|
|38488|Candle Short|52|
|2506|GangMaster|40|
|39416|Garbage_Bag_Flies_01|5|
|7342|Bonfire|7|
|38273|Merovingian_ArchivistBase_Returner|1|
|39418|Garbage_Bag_Flies_03|5|
|38272|Machine_ArchivistBase_Returner|1|
|38298|traffic_barrier|2|
|38271|Zion_ArchivistBase_Returner|1|

No doors occur in this set. Only the three returners have usable rows: Use/action2, TeleporterOrgCheck.40 GangMaster and FX/spawner references are source data, **not40 ambient NPC actors** or proof that every native effect renders. Packed HDL parses NextID3 with zero hardline nodes. Proper NETS parsing yields zero CNET networks. No subway or booth is fabricated.

Source SID returners remain at their recorded coordinates: Zion sector77/static3 `(-17550,145,-5500)` over NAV standing95; Machines sector50/static4 `(19350,145,-5400)` over95; Merovingian sector5/static5 `(8800,171,18100)` over124.2308. These records retain the established CSV+145Y convention; no pivot or relocation claim. Existing PvpArchives dynamic returners remain at its source-derived projected arrivals.

Six captured map6 rows remain byte unchanged in Instance.csv. Three Blackwood Goof records are grounded: `(11070,95,3974)` and twice `(11376,95,3846)`; existing skipRepeats naturally deduplicates the latter. Three Blackwood Mushfake rows have no containing XZ NAV polygon and remain unsupported. All six lie in the sector footprint. Existing NpcSpawnScope classifies map6 as Instance and LoadAmbient(Instance) is true, so normal world loading includes them; no force-Instance/Encounter route is introduced. Fixture visibility tests do not establish native actor grounding/presentation or all-world population.

## Reproduce and validate

```
python research/world-data/static_objects_small03.py C:/Users/russe/Developer/mxo-research/tools/client-data --world 6
```

Every reference and source SHA256 is retained in `research/world-data/archive_ashencourte-static-audit.json`. Shared small03/large01/large02 source outputs must remain exact. The ordinary `ashencourte-partial-world` scenario equips owned38538, waits the real sender cycle, verifies retail11/map6, uses the existing Zion returner, checks exact saved origin and retains the book. A separate ordinary fixture player at the existing grounded Goof capture checks natural ambient loading, without new actor placement.

## Claude-only native one-pass

Discovered mode `ashencourte`: copied CSR110, **actual SQL level25 plus matching XP25 in final AfterHelpers**, Zion, abilities2/17, owned Ashencourte book38538 bag3. No dev, GM, random overrides or fake session. Start at real Richland Zion sender38282 `111600/695/-6130` (grounded standing695, not static CSV745). Book GOB fields are organization1, PvPID1, inclusive level20–30. Machine/Merovingian book counterparts38546/38552 retain their own organization gates.

1. Open inventory and equip the owned book into program-launcher slot106 using normal UI. Stand on the rendered Zion sender pad; the sender has no manual Use action.
2. **Wait up to three real minutes** for the ordinary180-second cycle. Confirm Ashencourte terrain/world load, standing floor and existing Zion return teleporter38271 near the source arrival. Unit validation confirms the actual NavMesh.Project arrival remains `(-18500,95,-7500)` on sector77/region2/polygon0; it needs no inset adjustment.
3. Use the Zion returner through normal right-click/radial **Use** (action2). Confirm exact original Richland sender position and reusable book still equipped. No direct80fa/80ff, guessed loading-area selection, booth or chat teleport.
4. Optional only if naturally walkable: the grounded Goof capture is about320m from the source arrival, at11070/95/3974 (another at11376/95/3846). Inspect normal visible actor/floor presentation; then walk back to returner. There is no teleport/dev control and no claim that a continuous path has been validated. Do not travel to unsupported Mushfake coordinates or infer40 actors from GangMaster markers.

Native UI and geometry remain pending until Claude records the actual one-pass. Headless wire success is not native UI proof. Partial limits include missing/unsupported captured NPC floors, authored server population behavior, FX rendering and untested loading-area access.

Fresh validation: **PASS64.8s, EXIT0, parallel1**, real sender timing, retail11/map6, exact grounded arrival, returner Use2, exact session origin, book retained, natural captured Goof view and zero parser problems. Raw success log saved before teardown SHA256 `0d09b443eb9f2292bf28e53fd6ce25f10beaf778c4a1f7afb285a74aae8b010b`. No NPC actor/rendering or native UI claim follows from the wire test.47 focused tests pass; full/native status is reported in the PR/handoff.

Native fixture correction: final AfterHelpers sets copied actor/helpers to level25+ExperienceForLevel(25) and caps their learned ability levels at25, after generic fixture XP50. This prevents the client deriving level50 and refusing the level20–30 book. World/book/executor/source data and original64.8s normal headless route are unchanged; this NativeQa profile changed and requires a new Claude native check. Pure master165/162 integration retains exact original mixed DataLoader bytes plus one own load line, with exporter4/6/12/13/15/17 union.
