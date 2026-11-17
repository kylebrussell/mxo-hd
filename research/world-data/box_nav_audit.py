"""Read-only MXNV adapter and explicit-case Box geometry audit.

Usage: python box_nav_audit.py --repo ../.. --cases cases.json --output <own path>
No client archive extraction, mission selection, database, or server execution.
"""
from __future__ import annotations

import argparse
import csv
import gzip
import hashlib
import json
import math
from pathlib import Path
import struct

from box_walkability import RawPose, Root, Surface, area, plan


class Reader:
    def __init__(self, raw: bytes):
        self.raw, self.offset = raw, 0

    def get(self, fmt):
        size = struct.calcsize('<'+fmt)
        if size > len(self.raw)-self.offset:
            raise ValueError('Truncated MXNV')
        result = struct.unpack_from('<'+fmt, self.raw, self.offset)
        self.offset += size
        return result[0] if len(result) == 1 else result

    def count(self, fmt, minimum_size):
        n = self.get(fmt)
        if n > (len(self.raw)-self.offset)//minimum_size:
            raise ValueError('Malformed MXNV count')
        return n


def read_nav(path: Path, properties: Path, district: int, sectors: frozenset[int]):
    """Decode the shipped immutable server pack, all bytes checked before use.

    Layout matches research/world-data/navmesh.py and NavMesh.ReadPolygons;
    placement coordinates are signed tens, rotation exact quarter turns.
    Only requested sectors are materialized; the output records that boundary.
    """
    compressed = path.read_bytes()
    raw = gzip.decompress(compressed)
    r = Reader(raw)
    if r.get('I') != 0x564e584d:
        raise ValueError('Bad MXNV magic')
    version = r.get('H')
    if version not in (2, 3) or r.get('H') != district:
        raise ValueError('Unsupported MXNV version or district mismatch')

    def polygons():
        out = []
        for _ in range(r.count('H', 5)):
            head = r.get('B')
            n = head & 127
            if n < 3:
                raise ValueError('Malformed polygon vertex count')
            plane = r.get('4f') if head & 128 else (0., -1., 0., r.get('f'))
            if not all(math.isfinite(v) for v in plane) or plane[1] == 0:
                raise ValueError('Invalid NAV plane')
            vertices = tuple(r.get('2i' if version == 3 else '2h') for _ in range(n))
            out.append((plane, vertices))
        return tuple(out)

    prefabs = []
    for _ in range(r.count('I', 6)):
        rid = r.get('I')
        prefabs.append((rid, polygons()))
    data = {}
    seen = set()
    for _ in range(r.count('I', 6)):
        sid = r.get('H')
        if sid in seen:
            raise ValueError('Duplicate sector')
        seen.add(sid)
        regions = tuple(polygons() for _ in range(r.count('H', 2)))
        placements = tuple(r.get('H9hB') for _ in range(r.count('H', 21)))
        for pi, *rest in placements:
            if pi >= len(prefabs) or rest[-1] > 3:
                raise ValueError('Invalid prefab/rotation')
        if sid in sectors:
            data[sid] = regions, placements
    if r.offset != len(raw):
        raise ValueError('Trailing MXNV bytes')
    if sectors != frozenset(data):
        raise ValueError('Requested sector absent')
    props_bytes = properties.read_bytes()
    with properties.open(encoding='utf-8-sig', newline='') as f:
        rows = [row for row in csv.DictReader(f) if int(row['metr_id']) == district and int(row['sector']) in sectors]
    if len(rows) != len(sectors) or len({int(row['sector']) for row in rows}) != len(rows):
        raise ValueError('Missing/duplicate sector properties')
    props = {int(row['sector']): row for row in rows}
    surfaces = []
    def rot(x, z, turn):
        return ((x,z), (z,-x), (-x,-z), (-z,x))[turn]
    for sid, (regions, placements) in sorted(data.items()):
        ox, oz = float(props[sid]['min_x']), float(props[sid]['min_z'])
        def add(poly, region, index, x, y, z, turn):
            (a,b,c,d), points = poly
            if b == 0:
                raise ValueError('Vertical/invalid NAV plane')
            vertices = tuple((x+rot(px,pz,turn)[0], z+rot(px,pz,turn)[1]) for px,pz in points)
            # Quantized packs can contain zero-area polygons. Record no surface
            # for these; they cannot be a root or footprint candidate.
            if abs(area(vertices)) <= 1e-7:
                return
            if area(vertices) < 0:
                vertices = tuple(reversed(vertices))
            fx, fz = (-a,-c,a,c)[turn]/b, (-c,a,c,-a)[turn]/b
            surfaces.append(Surface((sid,region,index), vertices, (fx,fz,y-d/b-fx*x-fz*z)))
        for g, ps in enumerate(regions):
            for i, poly in enumerate(ps):
                add(poly,g,i,ox,0,oz,0)
        for k, (pi,px,py,pz,*rest) in enumerate(placements):
            for i, poly in enumerate(prefabs[pi][1]):
                add(poly,len(regions)+k,i,ox+px*10,py*10,oz+pz*10,rest[-1])
    return tuple(surfaces), dict(navSha256=hashlib.sha256(compressed).hexdigest(),
        decompressedSha256=hashlib.sha256(raw).hexdigest(), sectorPropertiesSha256=hashlib.sha256(props_bytes).hexdigest(),
        version=version, district=district, loadedSectors=sorted(sectors),
        scope='Exact requested sector set only; no claim of connectivity outside it')


def audit(repo: Path, cases: dict):
    results = []
    for case in cases['cases']:
        surfaces, source = read_nav(repo/'data/navmesh'/f"{case['district']}.bin.gz",
            repo/'data/sector_properties.csv', case['district'], frozenset(case['sectors']))
        root = case.get('root')
        if root is not None:
            root = Root(tuple(root['surface']), tuple(root['standing']), root['evidence'])
        raw = RawPose(tuple(case['rawPosition']),case['rawQuaternion'])
        result = plan(surfaces,root,raw)
        results.append(dict(name=case['name'], context=case['context'], source=source, result=result,
            withoutExplicitRoot=plan(surfaces,None,raw),
            integration='No production entry/root adapter; caller input is required. A Plan is conditional geometry, not mission admission.'))
    code = Path(__file__).resolve().parent
    return dict(scope='Offline geometry feasibility only; no mission/runtime mutation',
        implementationSha256={name:hashlib.sha256((code/name).read_bytes()).hexdigest()
                              for name in ('box_nav_audit.py','box_walkability.py')}, results=results)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo',type=Path,required=True)
    parser.add_argument('--cases',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    output = audit(args.repo,json.loads(args.cases.read_text(encoding='utf8')))
    output['caseInputSha256'] = hashlib.sha256(args.cases.read_bytes()).hexdigest()
    args.output.write_text(json.dumps(output,indent=2,allow_nan=False)+'\n',encoding='utf8')
    print(json.dumps([(r['name'],r['result']['status'],r['result'].get('reason',r['result'].get('centre'))) for r in output['results']]))
