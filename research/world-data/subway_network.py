r"""Read the subway networks from MxO client .metr files (read-only) and write data/subway_network.csv.

Usage: python -I subway_network.py <MxO dir> <string table TSV dir> <out.csv>
  MxO dir: the client folder (the .metr files are loose under resource/worlds).
  TSV dir: mxo-research/client-data/dumps/strings (st_subway_locations.tsv, st_subway_networks.tsv).

Layout (found by inspection, see docs/WORLD-DATA.md, "Subway network"):
  After the sectors, a NETS chunk. A chunk is a tag, u32 size of what follows the 12-byte header, and a u32 that is
  0x01000000 | child count for a container or 0x8?000000 | size for data. NETS holds one CNET per line; a CNET holds
  NDAT (u32 0x32000518, u32 line name id in st_subway_networks), CORS (u32 count, then 52-byte connectors) and CTNS
  (u32 count, then (u32, u32) pairs: which connectors the track joins).
  A connector: u32 0x32000519, u32 name id (st_subway_locations), three i32 (x, y, z), four f32 (a rotation; read
  as w x y z, which is a guess: the values are only ever 0 and +-1), then four i32: two small numbers (the station's place on the map drawing; a guess), the universe world
  id it links to (0 for a station, else 7 Downtown, 12 Richland, 169 International), 0.
  client.dll (viewsubwaymap.cpp) numbers networks and connectors by their order here: 81 3c sends
  <universe world u16> <network index u16> <connector index u16>.
"""
import csv, math, os, struct, sys

WORLDS = [  # universe.btb id, the server's district (MxOLocations), key, file
    (12, 1, 'slums', 'final_world/slums_barrens_full.metr'),
    (7, 2, 'downtown', 'final_world/downtown/dt_world.metr'),
    (169, 3, 'international', 'final_world/international/it.metr'),
]


def strings(path):
    table = {}
    with open(path, encoding='utf-8') as f:
        for row in csv.DictReader(f, delimiter='\t'):
            table[int(row['id'], 16)] = row['text']
    return table


def chunks(d, o, end):
    while o + 12 <= end:
        tag = d[o:o + 4]
        size, info = struct.unpack_from('<II', d, o + 4)
        yield tag, info, o + 12, o + 12 + size
        o += 12 + size


def networks(d):
    k = d.find(b'NETS')
    size, _ = struct.unpack_from('<II', d, k + 4)
    for tag, _, b, e in chunks(d, k + 12, k + 12 + size):
        if tag != b'CNET':
            continue
        net = {'line': 0, 'connectors': []}
        for t2, _, b2, e2 in chunks(d, b, e):
            body = d[b2:e2]
            if t2 == b'NDAT':
                net['line'] = struct.unpack_from('<I', body, 4)[0]
            elif t2 == b'CORS':
                n = struct.unpack_from('<I', body, 0)[0]
                for i in range(n):
                    r = body[4 + i * 52:4 + (i + 1) * 52]
                    _, name, x, y, z = struct.unpack_from('<IIiii', r, 0)
                    w, qx, qy, qz = struct.unpack_from('<4f', r, 20)
                    map_a, map_b, link, _ = struct.unpack_from('<4i', r, 36)
                    yaw = 2 * math.atan2(qy, w)
                    yaw = (yaw + math.pi) % (2 * math.pi) - math.pi
                    net['connectors'].append((name, x, y, z, round(yaw, 4), link, map_a, map_b))
        yield net


def main():
    mxo, tsv, out = sys.argv[1:4]
    loc = strings(os.path.join(tsv, 'st_subway_locations.tsv'))
    lines = strings(os.path.join(tsv, 'st_subway_networks.tsv'))
    by_world = {w: (m, key) for w, m, key, _ in WORLDS}
    rows = []
    for world, metr, key, path in WORLDS:
        with open(os.path.join(mxo, 'resource', 'worlds', path), 'rb') as f:
            d = f.read()
        for ni, net in enumerate(networks(d)):
            for ci, (name, x, y, z, yaw, link, a, b) in enumerate(net['connectors']):
                rows.append([world, metr, key, ni, '%08x' % net['line'], lines.get(net['line'], '') if net['line'] else '',
                             ci, '%08x' % name, loc.get(name, '') if name else '', x, y, z, yaw, link,
                             by_world[link][0] if link in by_world else '', a, b])
    with open(out, 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f, lineterminator='\n')
        w.writerow(['world_id', 'metr_id', 'district', 'network', 'line_id', 'line', 'connector', 'name_id', 'name',
                    'x', 'y', 'z', 'yaw', 'link_world', 'link_metr_id', 'map_a', 'map_b'])
        w.writerows(rows)
    print(len(rows), 'connectors')


if __name__ == '__main__':
    main()
