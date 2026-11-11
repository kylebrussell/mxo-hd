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

## Hardline network (`.hdl`)

`data/hardline_network.csv` has every hardline the client knows: 55 in the Slums (Richland and Westview), 65 in Downtown, 23 in International, and one in each combat construct. It is made by `research/world-data/hdl_network.py` from the packed `.hdl` files (extract them first with `mxo-research/tools/client-data/extract.py '\.hdl$' <dir>`). The client draws its hardline map from the same files, so these are the ids, names and places the player picks from.

| Column | What it is |
|---|---|
| `hid` | The hardline id: what 6c lists, 80 fb prompts with, and 81 8e and 80 ff ask for |
| `name_id`, `name` | FriendlyNameID and its text in `st_hardlinenames` ("Richland.Mara (Central)"). Empty on Slums 154 and Downtown 30 and 32 |
| `address`, `booth_goid` | The booth in the district's `.cooked_cod` Hardline class at the same place: its own name ("96 Kaplan Course") and kind (592 Hardline_PhoneBooth, 451 Hardline_NoModel, 2400 Hardline_PhoneBooth_Newbie) |
| `sector` | The sector the booth is in (`data/sector_properties.csv`) |
| `x`, `y`, `z` | The booth (Position). Slums booth statics sit 145 above it |
| `spot_x` .. `spot_z` | Position + Offset: 100 units in front of the booth and 95 up. **Retail put an arriving player exactly here in x and z** for all 62 hardlines whose arrival the repo captures show; its y was the ground, up to 10 below |
| `yaw` | The Offset's direction, `atan2(x, z)` |
| `flags` | 1 on Slums 153 (the newbie booth where the tutorial ends) and on Downtown 30 and 32; 0 elsewhere. A guess: the starting hardline |
| `socket_guid` | Socket GUID. Not used |
| `links`, `unmatched_portals` | Portals: positions of other hardlines' spots (as hids), and how many point at no hardline. The server doesn't use them; retail let a player go from any hardline to any uploaded one |

The old `data_hardlines` table had players' positions near the booths (3 to 1,169 units from the spot, median 113), "Tagged By ..." for names, and lacked Slums 83, 143, 154 and Downtown 7, 30, 32, 65, 72, so picking those on the map did nothing.

### File layout

u32 `0x18faeb0a`, u16 1, u16 1, zero padding, then a schema: named fields with a type and a default (Next Hardline ID, Next ID, Hardline with HID, Socket GUID, Position, Offset, Portals, Type, Destination ID, Flags, FriendlyNameID). The data starts at the second `07 "Next ID"`:

- a node: u8 length + name, u32 field count, then per field u32 index, u32 count, and count × (u32 type, value). Types: 1 u8, 2 u32, 4 three f32, 5 a 16-byte GUID.
- the root node "Next ID" (field 0: the next free hid), then u32 1 + a "Hardline" node per hardline, to the end of the file.
- Hardline fields: 0 HID, 1 Socket GUID, 2 Position, 3 Offset, 4 Portals (a list of positions), 5 Type, 6 Destination ID, 7 Flags, 8 FriendlyNameID. Type and Destination ID are 0 in every hardline.

## Subway network (`.metr` NETS)

`data/subway_network.csv` has every subway connector the client knows: 44 in the three cities, on 11 named lines plus two unnamed test lines. It is made by `research/world-data/subway_network.py` from the loose `.metr` files and the string tables `st_subway_locations` and `st_subway_networks`:

```
python -I research/world-data/subway_network.py <MxO dir> <dumps/strings dir> data/subway_network.csv
```

client.dll's subway map (`viewsubwaymap.cpp`) reads the same chunk of the same files, and numbers lines and connectors in file order. That order is what the client sends when a station is picked (`81 3c <world> <line> <connector>`, docs/CLIENT-ACTIONS.md).

| Column | What it is |
|---|---|
| `world_id` | The district's id in `resource/worlds/universe.btb`: 12 Richland and Westview (`slums_barrens_full.metr`), 7 Downtown (`dt_world.metr`), 169 International (`it.metr`). The client sends this one |
| `metr_id`, `district` | The server's district (`MxOLocations`) and its key |
| `network` | The line's index in the file: the client's NetworkID |
| `line_id`, `line` | The line's name in `st_subway_networks` (Red, Green, Blue, Orange, Yellow, Purple, Black Line). 0 on the test lines (Richland and Downtown network 0) |
| `connector` | The connector's index in its line: the client's ConnectorID |
| `name_id`, `name` | Its name in `st_subway_locations` ("Richland - Tabor Park [ Red Line ]"). `0c000008` (International, Purple Line) has no text |
| `x`, `y`, `z` | Its position. Every station connector lies on its district's nav mesh floor, within 10 units (`SubwayTests`) |
| `yaw` | From the stored rotation, read as w x y z (a guess: the values are only 0 and ±1, so it is 0 or ±π) |
| `link_world`, `link_metr_id` | Set on six connectors named after another district ("Downtown", "Richland", "International District"): the world they lead to. The client's map switches to that district's line of the same name when one is clicked (with `Client_AllowCrossWorldSubwayTeleport`, on by default). Richland - Tabor Park [ Yellow Line ] names its own world, which the client treats as an ordinary station |
| `map_a`, `map_b` | Two small numbers per connector. A guess: its place on the map drawing |

Lines per district: Richland and Westview have the Red (8 stations), Green (4), Blue (3, plus the International District link) and Yellow (3, plus the Downtown link) lines; Downtown the Orange (7), Yellow (3, plus Richland) and Black (2, plus International District); International the Blue (1, plus Richland), Purple (3) and Black (1, plus Downtown). No cost, level or organisation check was found in the map's code or in this data. The map registers three more strings with its own (`%s [ 1 ], %s(s) [ %u - %u ]`, `%s(s) [ %u - %u ]`, `ON: %s %u`); what fills them was not traced.

### Chunk layout

After the sectors (and `WdID`, `RDMP`, `AcNo`, `Hdln`): `NETS`. A chunk is a tag, a u32 size of what follows its 12-byte header, and a u32 that is `0x01000000 | child count` for a container and `0x8?000000 | size` for data. `NETS` holds one `CNET` per line; a `CNET` holds `NDAT` (u32 `0x32000518`, u32 the line's name id), `CORS` (u32 count, then 52-byte connectors) and `CTNS` (u32 count, then u32 pairs: the connectors the track joins). A connector is u32 `0x32000519`, u32 name id, three i32 x y z, four f32 (rotation), then four i32: `map_a`, `map_b`, the linked world, 0.

## What the server uses

- **`SectorProperties`** (`hds/world/Structures/SectorProperties.cs`) loads `sector_properties.csv` on first use. `At(district, x, z)` and `Get(district, sector)` give a sector's row; `LevelAt`, `IsPvpZone`, `MissionAreasEnabled(district, sector)` and `SectorsOfContact(contactId)` are the lookups other code calls. Nothing in mission choice calls them yet.
- **`HardlineNetwork`** (`hds/world/Structures/HardlineNetwork.cs`) loads `hardline_network.csv`. For the three cities it replaces `data_hardlines`: the 6c list (the named hardlines), the hardline a booth belongs to, and where a player lands after 81 8e, 80 ff, the tutorial and a Loading Area fallback (`TeleportHandler.MoveToHardline`). The constructs still use `data_hardlines`.
- **`SubwayNetwork`** (`hds/world/Structures/SubwayNetwork.cs`) loads `subway_network.csv`: which station a subway car belongs to, and where a ride picked on the map ends (`SubwayHandler`).
- **World NPC levels** come from `data/mob_parsed.csv` (retail spawns, with their own levels), which already agree with the sector levels, so nothing is overridden.
- **PvP:** `IsPvpZone` is part of who may attack whom: two players of different organisations who both stand in a PvP sector are opponents whatever their flags (`PvpRules`, `docs/PVP.md`).
- **`?sector`** (with `MXO_DEV_COMMANDS=1`) prints the sector where the player stands and the nearest hardline.

## Nav meshes (`.nav`) and the own-view position record

Retail's own-view record `03 02 00 01 1c <u8> <u16 sector> <u16 region> <u16 polygon> <u8> <x y z f32>` names where the player stands on the client's nav mesh. Our server used to send one fixed copy of it (sector 759, region 92, polygon 3) for every position.

Each sector has a packed `.nav`: u32 0, u32 0, u32 N, u32 0, then N × (u32 offset, u32 size). Section 0 is `SNAV` (the sector's edges). Section i ≥ 1 is the nav mesh of the sector's outdoor region i − 1: a chunk header, then `NVPS`, u32 size, u32 size | 0x81000000, u32 polygon count, and per polygon the plane a, b, c, d (f32; floor y = −(a x + c z + d) / b), u32 edge count, and per edge u16 neighbour region, u16 neighbour polygon (0xffff is a wall; 0x1000, 0x2000, 0x3000 + n cross a sector side), f32 normal x, f32 normal z, f32 x, f32 z (the edge's first vertex), then `NVPL`. Coordinates are sector-local: world x − the sector's min x, world z − its min z (boxes in `sector_properties.csv`).

**Outdoors, region = section − 1 and polygon = its index in the section.** This matched all 20 distinct outdoor records in the repo captures (Slums, Downtown, International), and the player's y is the polygon's floor + 95.

**Indoors, regions continue after the outdoor ones, one per floor prefab placed in the sector.** The sector's `.sidi` places floor prefabs (`.iprf`): u32 prefab resource id, i16 pivot x, y, z, i16 box min x, y, z, i16 box max x, y, z (all in tens of units, sector-local), u16 quarter turns, u16 0. The k-th placement in file order is region N − 1 + k; these are the elevator panel's `81 3f` regions too. The polygon is the index in that prefab's `INAV` mesh (`INAV`, u32 size, u32 size | 0x81000000, u16 polygon count, u16, then polygons as in `NVPS`), at the position moved into the prefab's frame: subtract the pivot, then turn back (0: (dx, dz); 1: (−dz, dx); 2: (−dx, −dz); 3: (dz, −dx)). This matched all 54 distinct interior records: elevator rides in Downtown 265, Slums 759, 405, 1214 and 1334, building floors, the tutorial's rooms. Where a position sits on a shared edge or corner, retail named the later polygon. The placements are found by their shape, since the rest of the `.sidi` isn't decoded; every retail record's region came out right.

The last byte changed over time at one spot (b7, bf, c9), so it is probably not about the place; we send 0.

`research/world-data/navmesh.py` packs both into `data/navmesh/<district>.bin.gz` for the tutorial and the three cities (about 13 MB): `MXNV`, u16 version 2, u16 district, u32 prefab count, per prefab u32 resource id + polygons, u32 sector count, then per sector u16 sector, u16 outdoor region count, per region its polygons, u16 placement count, per placement u16 prefab index, the nine i16 above and u8 quarter turns. Polygons: u16 count, then per polygon u8 vertex count (| 0x80 when sloped), f32 floor y (flat) or f32 a, b, c, d (sloped), and per vertex i16 x, i16 z (whole units). `NavMesh.Locate(district, x, y, z)` (`hds/world/Structures/NavMesh.cs`) finds the polygon, outdoors or indoors, whose floor is nearest y − 95 (within 200 units), and `NavLocation.RecordBytes()` gives the 7 bytes. `?sector` shows it.
