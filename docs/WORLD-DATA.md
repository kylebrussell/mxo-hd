# World data

What the client's world files say about each district, how to read them, and what the server takes from them. Everything here comes from the 7.6005 client's own files; no retail capture adds to it unless it says so.

## Sector properties (`.metr`)

`data/sector_properties.csv` has one row per sector of every district the server can load: 4,288 sectors in 19 worlds (Slums 1,493, Downtown 792, International 391, the tutorial, the archive and combat constructs, and the loading area). It is made by `research/world-data/metr_sectors.py` from the `.metr` files under `resource/worlds/` (loose files, not packed):

```
python -I research/world-data/metr_sectors.py <MxO dir> <dumps/strings dir> sectors.json data/sector_properties.csv
```

A sector is one cell of the designers' world grid. Each `.metr` record is that cell's file path (`resource/worlds/final_world/slums/slums_527`), its box, and the properties set on it in the world editor.

### Columns

| Column | Property in the file | What it is |
|---|---|---|
| `metr_id` | | The district as the server numbers it (`MxOLocations`: 0 tutorial, 1 Slums, 2 Downtown, 3 International, 4 to 17 constructs, 901 loading area) |
| `sector` | | The sector index. The same number as `sector_id` in `data/staticObjects_*.csv` and the `SectorID` the client puts in elevator records |
| `file` | | The sector's world file name |
| `min_x` .. `max_z` | | Its box on the ground, in world units. Boxes never overlap within a district, so a position falls in exactly one sector (98% of static objects sit inside their own sector's box; the rest overhang by a few hundred units) |
| `name` | Boundary/Info/Name | The designers' label: a neighbourhood name, "Facade" for the scenery ring, "No name", "Machine Interiors" |
| `neighbourhood_id`, `neighbourhood` | Boundary/Info/Neighborhood | A string id in `st_neighborhoodnames`, and its text. 0 on scenery and interiors |
| `level` | Boundary/Access Node/Level | The area's level. Checked against the retail NPCs in `data/mob_parsed.csv`: the median NPC level per neighbourhood equals it (Tabor Park 4, Moriah Projects 7, Gacy Heights 17, Ridge Way 21; Downtown 26/31/36/41). 50 on organisation interiors, 0 on scenery |
| `contact_type`, `contact` | Boundary/Contact/Contact Type | A neighbourhood contact's `id` in `resource/missions/definitions/sponsors.xml`, and its name. Set on 58 sectors for 27 contacts, most of them on one sector (Amber on 18, Sister Margaret on 13) |
| `gang_type` | Boundary/Gangs/Gang Type | A number per neighbourhood plus 200, 400 or 600 on some of its sectors. -1 on all of Richland, 0 on scenery |
| `gang_area`, `gang_tier` | | `gang_type % 200` and `gang_type / 200`. **A guess:** the area number is unique to one neighbourhood across all districts (Barrens 25 to 42, International 44 to 64, Downtown 66 to 98), and in the retail NPC data sectors with a higher tier have somewhat higher NPC levels (Downtown level 41: median 41, 46, 50 for tiers 0, 1, 2). Retail's gang spawn tables were server data and are lost |
| `pvp_zone` | Boundary/Gameplay/PVP Zone | 1 on the archive and combat constructs only; 0 everywhere in the three cities |
| `enable_mission_areas` | Boundary/Enable Mission Areas | 1 where missions may be set. In the three cities it is 0 only on the "Facade" scenery ring and 6 "No name" sectors; it is 0 on all constructs |
| `enable_navigation_mesh` | Boundary/Enable Navigation Mesh | Whether the sector has a nav mesh. Set wherever mission areas are, and on 807 more (constructs, some scenery) |
| `music_group` | Boundary/Info/Music Group | A four-character code: `DRic` Richland, `DWes` Westview (the Barrens), `DDtn` Downtown, `DInt` International, `MNio`/`MMer`/`MMac` the Zion, Merovingian and Machine interiors, `D01A`, `DAcA`, `DSkA`, ... the archives |
| `cars`, `pedestrians` | Boundary/Rich World/Cars, Pedestrians | The `.rlb` list the client draws traffic and crowds from (`vehicles/slums` is `resource/GameObjects/vehicles/Richworld/slums.rlb`, `npc/richworld_slums` is `resource/characters-NPC/richworld_slums.rlb`). Empty where there are none |
| `hardline_id` | Boundary/Hardline/ID | A GUID, set on 33 sectors: 16 Slums "No name" rooms, 16 Downtown "GM WhiteRoom" rooms and one in Uriah Industrial Park |
| `soft_boundary_id` | Boundary/Soft Boundary ID | A GUID per neighbourhood area |
| `worldtint_group` | Boundary/Info/WorldTint Group | A number per neighbourhood (the client tints the sky and fog by it) |
| `color` | Boundary/Info/Color | The editor's colour for the sector, the u32 as stored |
| `trash_scaler`, `validated`, `weld_distance_thresh`, `edge_distance_thresh`, `height_field_mesh_grid_size`, `minimum_building_height`, `marker_show`, `marker_type`, `layer` | | Build settings. Nearly constant |

Validation: the `neighbourhood` column equals the older `data/slums_neighbourhoods.csv` for all 1,134 Slums sectors that have a neighbourhood id. That file filled the other 359 with the designer's label ("Facade", "No name").

### File layout

All little-endian. Chunks have a 16-byte header: tag, u32 version (`0x101`), u32 size of what follows, u32 `0xffffffff`.

- `METR` header, `TSEC`, `DATA`, then `SINS`: u32 size, u32 sector count, and that many `SCIN` records.
- `SCIN`: tag, u32 size (after a 12-byte header), u32, then u16 length + the sector path, u16 sector index, six i16 (the box in thousands: min x, min z, max x, max z, size x, size z), nine f32 (min, max, size in world units), resource ids, then `DSHD` and `ISHD`.
- `DSHD` is the property schema, repeated in every record:
  - `DSST` strings: u32 n, then n of { u32 id, u32 k, k × (u16 length, text), u32 k, k × u16, u32 0 }.
  - `DSIT` items: u32 n, then n of { u32 item id, u32 a, a × { u32 k, k × (u16 string id, u16 part) }, u32 a, a × u16, u32 0 }. Each of the a string paths names the property, e.g. Boundary / Gameplay / PVP Zone. An item can have more than one: medium03's item 0x55 is both Boundary/Contact/Contact Type and AudioVolume/Audio/Reverb Type.
  - `DSPD` types: u32 n, n × u32 id, then n of { u32 id, u32 type, u64 0 }. The id is the item id, with the path index in the high 16 bits.
  - then a u32 (the next free item id).
- `ISHD` holds `ISPI`, the values: u32 n, n × u32 id (as in `DSPD`), then the values in that order. Type 1 is a u32, 2 an f32, 3 a string (u16 length + text), 4 a 16-byte GUID, 6 a colour (u32).

After the sectors come district-wide lists not read yet: `WdID`, `RDMP`, `AcNo`, `Hdln`, the traffic network (`CNET`, `NETS`, `NDAT`, `CORS`), then `POI1` (298 `Poi1` points of interest in the Slums: a type such as `SubW`, `Plza`, `Dung`, `Park`, `Coll`, a position and a string id) and `Vend`.
