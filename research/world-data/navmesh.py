r"""Pack the client's nav meshes (sector .nav files, floor prefab INAV meshes and their placements) into data/navmesh/<metr id>.bin.gz (read-only on the client).

Usage: python -I navmesh.py <mxo-research/tools/client-data dir> <data/sector_properties.csv> <out dir> [world IDs, default 0,1,2,3]
  The .nav files are packed; rezmap_ltb.py (in the tools dir) reads them from the client.

.nav layout (found by inspection, see docs/WORLD-DATA.md):
  u32 0, u32 0, u32 N, u32 0, then N x (u32 offset, u32 size). Section 0 is SNAV (sector edges); section i >= 1 is the
  nav mesh of the sector's outdoor region i - 1: a chunk header, then 'NVPS', u32 size, u32 size | 0x81000000,
  u32 polygon count, and per polygon: plane a, b, c, d (f32; floor y = -(a x + c z + d) / b), u32 edge count, and per edge
  u16 neighbour region, u16 neighbour polygon (0xffff = wall; 0x1000/0x2000/0x3000 + n = across a sector side),
  f32 normal x, f32 normal z, f32 x, f32 z (the edge's first vertex). Then 'NVPL'.
  x and z are sector-local: world x - the sector's min x, world z - its min z.

Retail's own-view position record 03 02 00 01 1c <u8> <u16 sector> <u16 region> <u16 polygon> <u8> <x y z f32> names the
polygon the player stands on: outdoors, region = section - 1 and polygon = the index in that section (20 of 20 outdoor
records in the repo captures). Region ids from N - 1 up are interiors: the k-th floor prefab placed by the sector's .sidi
is region N - 1 + k, and the polygon is the index in the prefab's INAV mesh (54 of 54 interior records); see placements().

Output (little-endian, gzip): see docs/WORLD-DATA.md ('MXNV' version 2: the prefab nav meshes, then per sector the
outdoor regions and the prefab placements).
"""
import struct, sys, os, csv, gzip, re


def sections(d):
    n = struct.unpack_from('<I', d, 8)[0]
    return [struct.unpack_from('<2I', d, 16 + 8 * i) for i in range(n)]


def polygons(d, o, s):
    p = d.find(b'NVPS', o, o + s)
    if p < 0:
        return None
    count = struct.unpack_from('<I', d, p + 12)[0]
    q = p + 16
    out = []
    for _ in range(count):
        plane = struct.unpack_from('<4f', d, q)
        q += 16
        ne = struct.unpack_from('<I', d, q)[0]
        q += 4
        verts = []
        for _ in range(ne):
            x, z = struct.unpack_from('<2f', d, q + 12)
            verts.append((x, z))
            q += 20
        out.append((plane, verts))
    return out


def inside(verts, x, z):
    s = [(verts[(j + 1) % len(verts)][0] - verts[j][0]) * (z - verts[j][1]) -
         (verts[(j + 1) % len(verts)][1] - verts[j][1]) * (x - verts[j][0]) for j in range(len(verts))]
    return all(v >= -1e-3 for v in s) or all(v <= 1e-3 for v in s)


def inav(d):
    """A floor prefab's nav mesh: 'INAV', u32 size, u32 size | 0x81000000, u16 polygon count, u16, then polygons as in NVPS."""
    p = d.find(b'INAV')
    if p < 0:
        return None
    count = struct.unpack_from('<H', d, p + 12)[0]
    q = p + 16
    out = []
    for _ in range(count):
        plane = struct.unpack_from('<4f', d, q)
        q += 16
        ne = struct.unpack_from('<I', d, q)[0]
        q += 4
        verts = []
        for _ in range(ne):
            verts.append(struct.unpack_from('<2f', d, q + 12))
            q += 20
        out.append((plane, verts))
    return out


# the type bytes of the .iprf resource ids in rezmap.ltb
PREFAB_PACKS = {0x04, 0x06, 0x08, 0x0c, 0x1a, 0x2c, 0x32, 0x38, 0x70, 0x82, 0x8a, 0x8e, 0x92, 0x98, 0xa4, 0xa8, 0xac, 0xba}


def placements(d, known):
    """The prefab instances of a sector's .sidi, in file order: u32 prefab resource id, then i16 pivot x, y, z, i16 box
    min x, y, z, i16 box max x, y, z (all in tens of units, sector-local), u16 rotation (quarter turns), u16 0. Found by
    their shape; the k-th is interior region (outdoor region count + k)."""
    out = []
    last = -24
    # candidates: rotation 0-3 and the u16 0 at +22 (scanning byte by byte in Python is too slow)
    for m in re.finditer(rb'(?=[\x00-\x03]\x00\x00\x00)', d):
        i = m.start() - 22
        if i < last + 24 or i < 0 or i + 26 > len(d):
            continue
        b = struct.unpack_from('<9h', d, i + 4)
        rot = d[i + 22]
        if (b[3] < b[6] and b[4] < b[7] and b[5] < b[8] and 0 < b[7] - b[4] <= 2000 and
                b[6] - b[3] <= 3300 and b[8] - b[5] <= 3300 and b[0] in (b[3], b[6]) and b[2] in (b[5], b[8]) and
                b[1] == b[4]):
            rid = struct.unpack_from('<I', d, i)[0]
            # a resource id (0xTT00nnnn); some placed prefabs are missing from the client's index but still count
            if rid in known or (rid >> 24 in PREFAB_PACKS and not rid & 0x00ff0000 and
                                all((b[6 + j] - b[3 + j]) % 20 == 0 for j in range(3))):
                out.append((rid, b, rot))
                last = i
    return out


def pack_polygons(polys, wide=False):
    body = bytearray(struct.pack('<H', len(polys)))
    for (a, b, c, dd), verts in polys:
        flat = abs(a) < 1e-6 and abs(c) < 1e-6 and abs(b) > 1e-6
        body += struct.pack('<B', len(verts) | (0 if flat else 0x80))
        body += struct.pack('<f', -dd / b) if flat else struct.pack('<4f', a, b, c, dd)
        for x, z in verts:
            assert wide or (-32768 <= x <= 32767 and -32768 <= z <= 32767)
            body += struct.pack('<ii' if wide else '<hh', int(round(x)), int(round(z)))
    return body


def main(tools, sectors_csv, out_dir, worlds='0,1,2,3'):
    sys.path.insert(0, tools)
    import rezmap_ltb as R
    index = R.index()
    bp = R.by_path()
    by_name = {}
    for k in bp:
        if k.endswith('.nav') and '/worlds/' in k:
            by_name.setdefault(k.rsplit('/', 1)[1][:-4], k)
    os.makedirs(out_dir, exist_ok=True)
    selected = worlds.split(',')
    rows = [r for r in csv.DictReader(open(sectors_csv)) if r['metr_id'] in selected]
    for metr in selected:
        wide = metr not in ('0', '1', '2', '3') # Datamine's sector-local polygons exceed int16 coordinates
        secs = [r for r in rows if r['metr_id'] == metr]
        body = bytearray()
        prefabs = {}  # resource id -> index in the prefab table
        prefab_body = bytearray()
        count = 0
        for r in secs:
            name = r['file'].replace(chr(92), '/').split('/')[-1].lower()
            if name not in by_name:
                continue
            d = R.read(bp[by_name[name]])
            regions = [polygons(d, o, s) or [] for o, s in sections(d)[1:]]
            body += struct.pack('<HH', int(r['sector']), len(regions))
            for polys in regions:
                body += pack_polygons(polys, wide)
            sidi = by_name[name][:-3] + 'sidi'
            insts = placements(R.read(bp[sidi]), index) if sidi in bp else []
            body += struct.pack('<H', len(insts))
            for rid, b, rot in insts:
                if rid not in prefabs:
                    polys = (inav(R.read(index[rid])) or []) if rid in index else []
                    prefabs[rid] = len(prefabs)
                    prefab_body += struct.pack('<I', rid) + pack_polygons(polys, wide)
                body += struct.pack('<H9hB', prefabs[rid], *b, rot)
            count += 1
        data = (b'MXNV' + struct.pack('<HHI', 3 if wide else 2, int(metr), len(prefabs)) + bytes(prefab_body) + struct.pack('<I', count) +
                bytes(body))
        path = os.path.join(out_dir, '%s.bin.gz' % metr)
        with gzip.open(path, 'wb', compresslevel=9) as f:
            f.write(data)
        print(metr, count, 'sectors', len(prefabs), 'prefabs', len(data), 'bytes,', os.path.getsize(path), 'gzipped')


if __name__ == '__main__':
    main(*sys.argv[1:5])
