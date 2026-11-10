"""Read per-sector designer properties from MxO client .metr world files (read-only).

Usage: python -I metr_sectors.py <client MxO dir> <string table TSV dir> <out.json> [<out.csv>]
  The TSV dir is mxo-research/client-data/dumps/strings (the client .sta tables as id, name, text).

Layout (found by inspection, see docs/WORLD-DATA.md):
  'METR' header, TSEC, DATA, 'SINS' u32 size, u32 count, then `count` SCIN records.
  SCIN: tag, u32 size (bytes after the 12-byte header), u32 (size | flags << 24), u16 path length, path,
        u16 sector index, 6 x i16 grid box (in 1000s), 9 x f32 (min, max, size), resource ids, ...
        DSHD (property schema) { DSST strings, DSIT items, DSPD types }, u32 next id,
        ISHD { ISPI values }.
  Chunk header: tag, u32 version (0x101), u32 size (after this 16-byte header), u32 0xffffffff.
  DSST: u32 n, n x { u32 id, u32 k, k x (u16 len, chars), u32 k, k x u16, u32 0 }.
  DSIT: u32 n, n x { u32 item id, u32 a, a x { u32 k, k x (u16 string id, u16 part) }, u32 a, a x u16, u32 0 }.
        Each of the a paths names a property, e.g. Boundary/Gameplay/PVP Zone; an item can carry several
        (medium03's item 0x55 is Boundary/Contact/Contact Type and AudioVolume/Audio/Reverb Type).
  DSPD: u32 n, n x u32 id, n x { u32 id, u32 type, u64 0 }; id = item id | path index << 16.
  ISPI: u32 n, n x u32 (item id | path index << 16), then the values in that order:
        type 1 u32, 2 f32, 3 string (u16 len + chars), 4 GUID (16 bytes), 6 colour (u32 0x00BBGGRR).
"""
import struct, sys, json, os

TYPE_SIZES = {1: 4, 2: 4, 4: 16, 6: 4}


class Reader:
    def __init__(self, d, p=0):
        self.d, self.p = d, p

    def u16(self):
        v = struct.unpack_from('<H', self.d, self.p)[0]; self.p += 2; return v

    def u32(self):
        v = struct.unpack_from('<I', self.d, self.p)[0]; self.p += 4; return v

    def f32(self):
        v = struct.unpack_from('<f', self.d, self.p)[0]; self.p += 4; return v

    def raw(self, n):
        v = self.d[self.p:self.p + n]; self.p += n; return v

    def s16(self):
        return self.raw(self.u16()).decode('latin1')

    def chunk(self, tag):
        t = self.raw(4)
        assert t == tag, (t, tag, hex(self.p - 4))
        ver, size, m1 = struct.unpack_from('<3I', self.d, self.p); self.p += 12
        return size


def read_schema(r):
    size = r.chunk(b'DSHD')
    end = r.p + size
    r.chunk(b'DSST')
    strs = {}
    for _ in range(r.u32()):
        sid = r.u32()
        parts = [r.s16() for _ in range(r.u32())]
        r.raw(2 * r.u32()); r.u32()
        strs[sid] = parts
    r.chunk(b'DSIT')
    items = {}
    for _ in range(r.u32()):
        iid = r.u32()
        paths = []
        for _ in range(r.u32()):
            path = []
            for _ in range(r.u32()):
                s, alt = r.u16(), r.u16()
                path.append(strs[s][alt])
            paths.append('/'.join(path))
        r.raw(2 * r.u32() + 4)
        items[iid] = paths
    r.chunk(b'DSPD')
    types = {}
    n = r.u32()
    r.raw(4 * n)  # the same ids again
    for _ in range(n):
        iid, t = r.u32(), r.u32(); r.raw(8)
        types[iid] = t
    assert r.p == end, (hex(r.p), hex(end))
    r.u32()  # next free item id
    return items, types


def read_values(r, items, types):
    r.chunk(b'ISHD')
    size = r.chunk(b'ISPI')
    end = r.p + size
    ids = [r.u32() for _ in range(r.u32())]
    vals = {}
    for i in ids:
        t = types[i]
        if t == 3:
            v = r.s16()
        elif t == 2:
            v = round(r.f32(), 4)
        elif t == 4:
            v = r.raw(16).hex()
        elif t in (1, 6):
            v = r.u32()
        else:
            raise ValueError('type %d at %x' % (t, r.p))
        vals[items[i & 0xffff][i >> 16]] = v  # the high half picks which of the item's paths it is
    return vals, r.d[r.p:end]


def read_metr(path):
    d = open(path, 'rb').read()
    p = d.find(b'SINS')
    count = struct.unpack_from('<I', d, p + 8)[0]
    p += 12
    out = []
    for _ in range(count):
        assert d[p:p + 4] == b'SCIN', hex(p)
        size = struct.unpack_from('<I', d, p + 4)[0]
        end = p + 12 + size
        r = Reader(d, p + 12)
        spath = r.s16()
        idx = r.u16()
        grid = struct.unpack_from('<6h', d, r.p); r.p += 12
        box = struct.unpack_from('<9f', d, r.p); r.p += 36
        dshd = d.find(b'DSHD', r.p, end)
        r.p = dshd
        items, types = read_schema(r)
        vals, rest = read_values(r, items, types)
        out.append(dict(sector=idx, path=spath, name=spath.replace(chr(92), '/').split('/')[-1], grid=list(grid),
                        min=[round(v, 1) for v in box[0:3]], max=[round(v, 1) for v in box[3:6]],
                        props=vals, types={items[i & 0xffff][i >> 16]: t for i, t in types.items()},
                        tail=rest.hex() if rest else ''))
        p = end
    return out


# (metr id as the server numbers districts in MxOLocations, district, file)
WORLDS = [(1, 'slums', 'resource/worlds/final_world/slums_barrens_full.metr'),
          (2, 'downtown', 'resource/worlds/final_world/downtown/dt_world.metr'),
          (3, 'international', 'resource/worlds/final_world/international/it.metr'),
          (0, 'tutorial', 'resource/worlds/final_world/tutorial_v2/tutorial_v2.metr'),
          (4, 'archive01', 'resource/worlds/final_world/constructs/archive/archive01/archive01.metr'),
          (5, 'archive02', 'resource/worlds/final_world/constructs/archive/archive02/archive02.metr'),
          (6, 'ashencourt', 'resource/worlds/final_world/constructs/archive/archive_ashencourte/archive_ashencourte.metr'),
          (7, 'datamine', 'resource/worlds/final_world/constructs/archive/archive_datamine/datamine.metr'),
          (8, 'sakura', 'resource/worlds/final_world/constructs/archive/archive_sakura/archive_sakura.metr'),
          (9, 'sati', 'resource/worlds/final_world/constructs/archive/archive_sati/sati.metr'),
          (10, 'widowsmoor', 'resource/worlds/final_world/constructs/archive/archive_widowsmoor/archive_widowsmoor.metr'),
          (11, 'yuki', 'resource/worlds/final_world/constructs/archive/archive_yuki/archive_yuki.metr'),
          (12, 'large01', 'resource/worlds/final_world/constructs/large/large01/large01.metr'),
          (13, 'large02', 'resource/worlds/final_world/constructs/large/large02/large02.metr'),
          (14, 'medium01', 'resource/worlds/final_world/constructs/medium/medium01/medium01.metr'),
          (15, 'medium02', 'resource/worlds/final_world/constructs/medium/medium02/medium02.metr'),
          (16, 'medium03', 'resource/worlds/final_world/constructs/medium/medium03/medium03.metr'),
          (17, 'small03', 'resource/worlds/final_world/constructs/small/small03/small03.metr'),
          (901, 'loading_area', 'resource/worlds/loading_area/la.metr')]

# Cars / Pedestrians resource ids (resolved with rezmap.ltb; 5c000039 is not in rezmap.lta)
RLB = {0x28001186: 'vehicles/barrens', 0x28001187: 'vehicles/downtown', 0x28001188: 'vehicles/international',
       0x28001189: 'vehicles/slums', 0x5c000039: 'vehicles/richvehicles', 0x2800118a: 'npc/richworld_slums',
       0x2800118b: 'npc/richworld_barrens', 0x2800118c: 'npc/richworld_downtown', 0x2800118d: 'npc/richworld_international'}


def fourcc(v):
    return struct.pack('<I', v).decode('latin1') if v else ''


def neighbourhood_names(tsv_dir):
    """st_neighborhoodnames: string id -> text (ids are only unique within one table)."""
    out = {}
    for line in open(os.path.join(tsv_dir, 'st_neighborhoodnames.tsv'), encoding='utf-8').read().splitlines()[1:]:
        parts = line.split('	')
        if len(parts) >= 3:
            out[int(parts[0], 16)] = parts[2]
    return out


def contacts(path):
    import re
    s = open(path, encoding='latin1').read()
    return {int(m.group(1)): m.group(2) for m in re.finditer(r'<contact\s+id="(\d+)"\s+org="\d+"\s+name="([^"]*)"', s)}


COLUMNS = ['metr_id', 'district', 'sector', 'file', 'min_x', 'min_z', 'max_x', 'max_z', 'name', 'neighbourhood_id',
           'neighbourhood', 'level', 'contact_type', 'contact', 'gang_type', 'gang_area', 'gang_tier', 'pvp_zone',
           'enable_mission_areas', 'enable_navigation_mesh', 'music_group', 'cars', 'pedestrians', 'hardline_id',
           'soft_boundary_id', 'worldtint_group', 'color', 'trash_scaler', 'validated', 'weld_distance_thresh',
           'edge_distance_thresh', 'height_field_mesh_grid_size', 'minimum_building_height', 'marker_show',
           'marker_type', 'layer']


def row(metr_id, district, s, strs, cons):
    p = s['props']
    g = lambda k: p.get(k, '')
    nb = g('Boundary/Info/Neighborhood')
    ct = g('Boundary/Contact/Contact Type')
    gt = g('Boundary/Gangs/Gang Type')
    gang_area = gang_tier = ''
    if gt == 0xffffffff:
        gt = -1
    elif gt not in ('', 0):
        gang_area, gang_tier = gt % 200, gt // 200  # a guess at the split; see docs/WORLD-DATA.md
    guid = lambda k: '' if g(k) in ('', '0' * 32) else g(k)
    rlb = lambda k: RLB.get(g(k), '%08x' % g(k)) if g(k) else ''
    color = g('Boundary/Info/Color')
    return [metr_id, district, s['sector'], s['name'], int(s['min'][0]), int(s['min'][2]), int(s['max'][0]), int(s['max'][2]),
            g('Boundary/Info/Name'), '%08x' % nb if nb else '', strs.get(nb, '') if nb else '',
            g('Boundary/Access Node/Level'), ct, cons.get(ct, '') if ct else '', gt, gang_area, gang_tier,
            g('Boundary/Gameplay/PVP Zone'), g('Boundary/Enable Mission Areas'), g('Boundary/Enable Navigation Mesh'),
            fourcc(g('Boundary/Info/Music Group') or 0), rlb('Boundary/Rich World/Cars'), rlb('Boundary/Rich World/Pedestrians'),
            guid('Boundary/Hardline/ID'), guid('Boundary/Soft Boundary ID'), g('Boundary/Info/WorldTint Group'),
            '%06x' % color if color != '' else '', g('Boundary/Gameplay/Trash Scaler'),
            g('Boundary/Bundle/Validated'), g('Boundary/SLODS/Weld Distance Thresh'), g('Boundary/SLODS/Edge Distance Thresh'),
            g('Boundary/SLODS/Height Field Mesh Grid Size'), g('Boundary/SLODS/Minimum Building Height'),
            g('Entity/Marker/Show'), g('Entity/Marker/Type'), g('Entity/Layers/Layer')]


if __name__ == '__main__':
    import csv
    base, tsv_dir, out_json = sys.argv[1:4]
    strs = neighbourhood_names(tsv_dir)
    cons = contacts(os.path.join(base, 'resource/missions/definitions/sponsors.xml'))
    res, rows = {}, []
    for metr_id, district, rel in WORLDS:
        secs = read_metr(os.path.join(base, rel))
        res[district] = secs
        rows += [row(metr_id, district, s, strs, cons) for s in secs]
        print(district, len(secs))
    json.dump(res, open(out_json, 'w'), indent=1)
    if len(sys.argv) > 4:
        with open(sys.argv[4], 'w', newline='') as f:
            w = csv.writer(f, lineterminator='\n')
            w.writerow(COLUMNS)
            w.writerows(rows)
