r"""Pack the outdoor nav meshes of the client's sector .nav files into data/navmesh/<metr id>.bin.gz (read-only on the client).

Usage: python -I navmesh.py <mxo-research/tools/client-data dir> <data/sector_properties.csv> <out dir>
  The .nav files are packed; rezmap_ltb.py (in the tools dir) reads them from the client.

.nav layout (found by inspection, see docs/WORLD-DATA.md):
  u32 0, u32 0, u32 N, u32 0, then N x (u32 offset, u32 size). Section 0 is SNAV (sector edges); section i >= 1 is the
  nav mesh of the sector's outdoor region i - 1: a chunk header, then 'NVPS', u32 size, u32 size | 0x81000000,
  u32 polygon count, and per polygon: plane a, b, c, d (f32; floor y = -(a x + c z + d) / b), u32 edge count, and per edge
  u16 neighbour region, u16 neighbour polygon (0xffff = wall; 0x1000/0x2000/0x3000 + n = across a sector side),
  f32 normal x, f32 normal z, f32 x, f32 z (the edge's first vertex). Then 'NVPL'.
  x and z are sector-local: world x - the sector's min x, world z - its min z.

Retail's own-view position record 03 02 00 01 1c <u8> <u16 sector> <u16 region> <u16 polygon> <u8> <x y z f32> names the
polygon the player stands on: outdoors, region = section - 1 and polygon = the index in that section (24 of 24 outdoor
records in the repo captures). Region ids at or above N - 1 are interior spaces, whose nav meshes are in the prefabs
(.iprf INAV) and aren't packed here.

Output (little-endian, gzip): 'MXNV', u16 version 1, u16 metr id, u32 sector count, then per sector: u16 sector, u16 region
count, per region u16 polygon count, per polygon: u8 vertex count (| 0x80 when the floor slopes), then f32 floor y (flat)
or f32 a, b, c, d (sloped), then per vertex i16 x, i16 z (sector-local, rounded to whole units).
"""
import struct, sys, os, csv, gzip


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


def main(tools, sectors_csv, out_dir):
    sys.path.insert(0, tools)
    import rezmap_ltb as R
    bp = R.by_path()
    by_name = {}
    for k in bp:
        if k.endswith('.nav') and '/worlds/' in k:
            by_name.setdefault(k.rsplit('/', 1)[1][:-4], k)
    os.makedirs(out_dir, exist_ok=True)
    rows = [r for r in csv.DictReader(open(sectors_csv)) if r['metr_id'] in ('0', '1', '2', '3')]
    for metr in ('0', '1', '2', '3'):
        secs = [r for r in rows if r['metr_id'] == metr]
        body = bytearray()
        count = 0
        for r in secs:
            name = r['file'].replace('\\', '/').split('/')[-1].lower()
            if name not in by_name:
                continue
            d = R.read(bp[by_name[name]])
            regions = [polygons(d, o, s) or [] for o, s in sections(d)[1:]]
            body += struct.pack('<HH', int(r['sector']), len(regions))
            for polys in regions:
                body += struct.pack('<H', len(polys))
                for (a, b, c, dd), verts in polys:
                    flat = abs(a) < 1e-6 and abs(c) < 1e-6 and abs(b) > 1e-6
                    body += struct.pack('<B', len(verts) | (0 if flat else 0x80))
                    body += struct.pack('<f', -dd / b) if flat else struct.pack('<4f', a, b, c, dd)
                    for x, z in verts:
                        body += struct.pack('<hh', int(round(x)), int(round(z)))
            count += 1
        data = b'MXNV' + struct.pack('<HHI', 1, int(metr), count) + bytes(body)
        path = os.path.join(out_dir, '%s.bin.gz' % metr)
        with gzip.open(path, 'wb', compresslevel=9) as f:
            f.write(data)
        print(metr, count, 'sectors', len(data), 'bytes,', os.path.getsize(path), 'gzipped')


if __name__ == '__main__':
    main(*sys.argv[1:4])
