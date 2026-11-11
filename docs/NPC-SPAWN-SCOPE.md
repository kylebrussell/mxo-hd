# Captured NPC scope

`data/mob_parsed.csv` is a historical sightings file, not a list of permanent street spawns. The original 13,141 rows remain unchanged. `dotnet run --project tools/NpcCatalog -- .` partitions them into `data/npc_spawns/`, with a source SHA-256 and source line / navmesh provenance in `manifest.json`.

| Scope | Rows | Ambient loading |
|---|---:|---|
| OpenWorld | 8,976 | yes |
| PublicInterior | 346 | yes |
| Unresolved | 592 | yes |
| MissionInterior | 872 | no |
| InstanceInterior | 855 | no |
| Instance | 880 | yes, restricted by the existing district visibility |
| Encounter | 620 | no |

This is an explicit reconstruction policy, not a claim to have recovered the original mission owner of each sighting. City indoor/outdoor context comes from the shipped navmesh. Unassigned indoor sightings go to MissionInterior; named public club guards and hosts remain public. Missing navmesh context stays Unresolved and is retained. Non-city districts keep their populations separately so the split does not empty constructs.

Corrupted and Special Agent are encounter families. The repeated starts at exactly 4 m in captures support treating their historical sightings as arrivals; that distance is not asserted for every row. Runtime mission and encounter spawns still come from mission definitions, with their own ownership and cleanup.

`research/world-data/npc_instance_models.csv` records the 128 appearance IDs whose shipped character resource path contains `/Dungeons/`. Regenerate it with `python research/world-data/extract_instance_models.py <npc_appearances.json>`; the input is the existing read-only `mxo-research/client-data/npc_appearances.json` export (SHA-256 `6eab3fc73b2ac4189292f6ef40179902290aa80cbcd299319f7fc0429d893f48`). These private interior models are separated before the generic indoor policy. This classification does not reconstruct an instance or assign a mission; it prevents an unowned historical sighting from becoming a permanent city resident.

The runtime loads only the four ambient scopes, plus the existing hand-placed `mob.csv`. Duplicate sightings require matching district/name, less than 5 m horizontally and less than 1.5 m vertically; same-name NPCs on different floors survive. Every partition has a header (the source has none), so its first sighting is no longer silently skipped by the CSV loader.

Unit coverage checks the lossless partition, source hash and scope policy. `ambient-npc-scope` checks a fresh disposable server: the historical Special Agent arrival is absent and a public interior Succubus remains visible. The other world-loot scenarios exercise retained city populations.
