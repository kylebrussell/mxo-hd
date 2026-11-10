r"""Read the hardline network from MxO client .hdl files (read-only) and write data/hardline_network.csv.

Usage: python -I hdl_network.py <extracted dir> <string table TSV dir> <sector_properties.csv> <hardlines.json> <out.csv>
  The .hdl files are packed: extract them first with mxo-research/tools/client-data/extract.py '\.hdl$' <extracted dir>.
  TSV dir: mxo-research/client-data/dumps/strings (st_hardlinenames.tsv).
  hardlines.json: mxo-research/client-data/hardlines.json, the booths of the <world>.cooked_cod Hardline class
  (address name, goid, sector, sector-relative position); optional, pass - to skip.

Layout (found by inspection, see docs/WORLD-DATA.md):
  u32 0x18faeb0a, u16 1, u16 1, zero padding, then a schema of named fields (Next Hardline ID, Next ID,
  Hardline { HID, Socket GUID, Position, Offset, Portals, Type, Destination ID, Flags, FriendlyNameID }) with defaults.
  The data starts at the second u8 7 "Next ID": a node, then (u32 1, node) for each hardline.
  node = u8 length, name, u32 field count, then per field: u32 index, u32 count, count x (u32 type, value).
  Value types: 1 u8, 2 u32, 4 three f32, 5 16-byte GUID.
"""
import struct, sys, os, json, csv, math

SIZES = {1: 1, 2: 4, 4: 12, 5: 16}


def parse(d):
    p = d.find(b'\x07Next ID', 0x1a0)

    def node(p):
        n = d[p]
        name = d[p + 1:p + 1 + n].decode('latin1')
        p += 1 + n
        fields = {}
        nf = struct.unpack_from('<I', d, p)[0]
        p += 4
        for _ in range(nf):
            idx, cnt = struct.unpack_from('<2I', d, p)
            p += 8
            vals = []
            for _ in range(cnt):
                t = struct.unpack_from('<I', d, p)[0]
                p += 4
                if t == 1:
                    v = d[p]
                elif t == 2:
                    v = struct.unpack_from('<I', d, p)[0]
                elif t == 4:
                    v = struct.unpack_from('<3f', d, p)
                elif t == 5:
                    v = d[p:p + 16].hex()
                else:
                    raise ValueError('type %d at %x' % (t, p))
                p += SIZES[t]
                vals.append(v)
            fields[idx] = vals
        return name, fields, p

    root, rf, p = node(p)
    out = []
    while p < len(d):
        p += 4  # u32 1
        name, f, p = node(p)
        assert name == 'Hardline', name
        out.append(f)
    assert p == len(d)
    return rf[0][0], out


# (metr id, district, file); the constructs' hardlines are all HID 1 or 2 and listed for completeness
WORLDS = [(1, 'slums', 'resource/worlds/final_world/slums_barrens_full.hdl'),
          (2, 'downtown', 'resource/worlds/final_world/downtown/dt_world.hdl'),
          (3, 'international', 'resource/worlds/final_world/international/it.hdl'),
          (12, 'large01', 'resource/worlds/final_world/constructs/large/large01/large01.hdl'),
          (13, 'large02', 'resource/worlds/final_world/constructs/large/large02/large02.hdl'),
          (14, 'medium01', 'resource/worlds/final_world/constructs/medium/medium01/medium01.hdl'),
          (15, 'medium02', 'resource/worlds/final_world/constructs/medium/medium02/medium02.hdl'),
          (16, 'medium03', 'resource/worlds/final_world/constructs/medium/medium03/medium03.hdl'),
          (17, 'small03', 'resource/worlds/final_world/constructs/small/small03/small03.hdl')]

COLUMNS = ['metr_id', 'district', 'hid', 'name_id', 'name', 'address', 'booth_goid', 'sector', 'x', 'y', 'z',
           'spot_x', 'spot_y', 'spot_z', 'yaw', 'flags', 'socket_guid', 'links', 'unmatched_portals']


def r1(v):
    v = round(v, 1)
    return int(v) if v == int(v) else v


def main(base, tsv_dir, sectors_csv, booths_json, out_csv):
    names = {}
    for line in open(os.path.join(tsv_dir, 'st_hardlinenames.tsv'), encoding='utf-8').read().splitlines()[1:]:
        f = line.split('\t')
        names[int(f[0], 16)] = f[2]
    boxes = {}
    for r in csv.DictReader(open(sectors_csv)):
        boxes.setdefault(int(r['metr_id']), []).append((int(r['sector']), int(r['min_x']), int(r['min_z']),
                                                        int(r['max_x']), int(r['max_z'])))
    booths = []
    if booths_json != '-':
        world_ids = {'slums': 1, 'downtown': 2, 'international': 3}
        for b in json.load(open(booths_json))['hardlines']:
            m = world_ids.get(b['world'])
            box = next((s for s in boxes.get(m, []) if s[0] == b['sector']), None)
            if box:  # sector_pos is relative to the sector's min corner
                sp = b['sector_pos']
                booths.append((m, (box[1] + sp[0], sp[1], box[2] + sp[2]), b))

    def sector_at(m, x, z):
        return next((s[0] for s in boxes.get(m, []) if s[1] <= x < s[3] and s[2] <= z < s[4]), '')

    rows = []
    for m, district, rel in WORLDS:
        next_id, hls = parse(open(os.path.join(base, rel), 'rb').read())
        spots = {}
        for f in hls:
            pos, off = f[2][0], f[3][0]
            spots[f[0][0]] = tuple(pos[i] + off[i] for i in range(3))
        for f in hls:
            hid, guid, pos, off = f[0][0], f[1][0], f[2][0], f[3][0]
            spot = spots[hid]
            links, unmatched = [], 0
            for pt in f[4]:
                if pt == (0.0, 0.0, 0.0):
                    continue
                near = min(spots, key=lambda h: math.dist(spots[h], pt))
                if math.dist(spots[near], pt) < 50 and near != hid:
                    links.append(near)
                else:
                    unmatched += 1
            booth = next((b for bm, bp, b in booths if bm == m and math.dist(bp, pos) < 2), None)
            name_id = f[8][0]
            rows.append([m, district, hid, '%08x' % name_id if name_id else '', names.get(name_id, ''),
                         booth['name'] if booth else '', booth['goid'] if booth else '', sector_at(m, pos[0], pos[2]),
                         r1(pos[0]), r1(pos[1]), r1(pos[2]), r1(spot[0]), r1(spot[1]), r1(spot[2]),
                         round(math.atan2(off[0], off[2]), 4), f[7][0], guid, ' '.join(str(h) for h in sorted(links)),
                         unmatched])
        print(district, len(hls), 'hardlines, next id', next_id)
    with open(out_csv, 'w', newline='') as fo:
        w = csv.writer(fo, lineterminator='\n')
        w.writerow(COLUMNS)
        w.writerows(rows)


if __name__ == '__main__':
    main(*sys.argv[1:6])
