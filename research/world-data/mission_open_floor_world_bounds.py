"""Portable authored-static spatial profiles. No production admission or source mutation.

Domains: canonical counted GO Model6 registration, root SID OSID PROP, direct
SIDI IPI inline PROP. Structural NAV, EPI and SID buffer8 are separate profiles.
All source variants are retained; a missing declared layer is never empty.
"""
import argparse
import collections
import gzip
import hashlib
import itertools
import json
import math
from pathlib import Path
import struct
import sys
import native_prefab_transform as T
import metr_sectors as M
import navmesh as N

CLIENT_SHA256 = 'cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a'
GOB_SHA256 = '3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103'
F = T.f32

def digest(data):
    return hashlib.sha256(data).hexdigest()

class Reader:
    def __init__(self, data):
        self.data, self.at = data, 0
    def take(self, size):
        if size < 0 or self.at + size > len(self.data):
            raise ValueError('Truncated source record')
        result = self.data[self.at:self.at + size]
        self.at += size
        return result
    def read(self, fmt):
        return struct.unpack(fmt, self.take(struct.calcsize(fmt)))
    def u32(self):
        return self.read('<I')[0]
    def eof(self):
        if self.at != len(self.data):
            raise ValueError('Unexpected trailing source bytes')

def osid_buffers(data):
    if data[:4] != b'OSID' or len(data) < 16 or struct.unpack_from('<I', data, 4)[0] != 0x115:
        raise ValueError('Unsupported OSID source version')
    r = Reader(data)
    r.at = struct.unpack_from('<I', data, 12)[0]
    if r.at < 16 or r.at > len(data):
        raise ValueError('Invalid OSID buffer table offset')
    out = []
    for _ in range(10):
        count, offset = r.read('<II')
        if offset != r.at:
            raise ValueError('Noncontiguous OSID buffer')
        out.append((offset, r.take(count)))
    r.eof()
    return out

def go_records(data):
    r = Reader(data)
    for _ in range(r.u32()):
        header = r.take(16)
        if header[:4] != b'ISHD':
            raise ValueError('Unknown GO schema record')
        r.take(struct.unpack_from('<I', header, 8)[0])
    count = r.u32()
    rows = []
    for _ in range(count):
        offset = r.at
        rows.append((offset, struct.unpack('<II3f4f6f3IH', r.take(74))))
    r.eof()
    return rows

def prop_records(r, count):
    out = []
    if count > (len(r.data) - r.at) // 80:
        raise ValueError('Impossible PROP count')
    for ordinal in range(count):
        start = r.at
        raw = r.take(80)
        asset = struct.unpack_from('<I', raw)[0]
        values = struct.unpack_from('<13f', raw, 4)
        if not all(math.isfinite(v) for v in values):
            raise ValueError('Nonfinite PROP')
        bounds = values[7:]
        if any(bounds[i] > bounds[i + 3] for i in range(3)):
            raise ValueError('Reversed PROP bounds')
        r.take(72 * struct.unpack_from('<I', raw, 76)[0])
        out.append(dict(ordinal=ordinal, offset=start, assetRid=asset,
                        position=list(values[:3]), quaternion=list(values[3:7]),
                        rawBounds=list(bounds), bytes=r.at-start,
                        recordSha256=digest(r.data[start:r.at])))
    return out

def compact_bound(value, bias):
    n = math.trunc(F((value + bias) * F(.1)))
    if not -32768 <= n <= 32767:
        raise ValueError('Compact PROP bound overflow')
    return F(n * 10)

def prop_world_bounds(raw, pivot=(0, 0, 0), rotation=0, corner=(0, 0, 0)):
    native = [compact_bound(v, -9) for v in raw[:3]] + [compact_bound(v, 9) for v in raw[3:]]
    def transform(bounds, stored):
        points = [T.quarter_turn(v, rotation) for v in itertools.product(*[(bounds[i], bounds[i+3]) for i in range(3)])]
        if stored:
            points = [[F(F(v[i] + F(pivot[i]*10)) + F(corner[i])) for i in range(3)] for v in points]
        else:
            points = [[v[i] + pivot[i]*10 + corner[i] for i in range(3)] for v in points]
        return [min(v[i] for v in points) for i in range(3)] + [max(v[i] for v in points) for i in range(3)]
    a, b = transform(raw, False), transform(native, True)
    return [min(a[i], b[i]) for i in range(3)], [max(a[i+3], b[i+3]) for i in range(3)]

def model_world_bounds(position, q, half):
    if len(q) != 4 or len(half) != 3 or not all(math.isfinite(v) for v in (*position, *q, *half)) or any(v <= 0 for v in half) or sum(v*v for v in q) == 0:
        raise ValueError('Unknown Model6 bounds')
    x, y, z, w = q
    tx, ty, tz = (F(v*2) for v in (x, y, z))
    xx, yy, zz = F(tx*x), F(ty*y), F(tz*z)
    xy, xz, yz = F(ty*x), F(tz*x), F(tz*y)
    wx, wy, wz = F(tx*w), F(ty*w), F(tz*w)
    rows = [[F(1-(zz+yy)), F(xy+wz), F(xz-wy)], [F(xy-wz), F(1-(zz+xx)), F(yz+wx)], [F(xz+wy), F(yz-wx), F(1-(yy+xx))]]
    corners = []
    for signs in itertools.product((-1, 1), repeat=3):
        terms = [[F(a*h*s) for a in row] for row, h, s in zip(rows, half, signs)]
        corners.append([F(F(terms[1][i]+terms[0][i])+terms[2][i]) for i in range(3)])
    full = [max(abs(c[i]) for c in corners) for i in range(3)]
    eps, sq = F(1e-5), F(.7071067690849304)
    if abs(F(w-1)) <= eps or abs(F(y-1)) <= eps or abs(F(y+1)) <= eps:
        native = half
    elif abs(x) <= eps and abs(z) <= eps and (abs(F(y-sq)) <= eps or abs(F(y+sq)) <= eps):
        native = [half[2], half[1], half[0]]
    else:
        native = [max(0., *(c[i] for c in corners)) for i in range(3)]
    lo = [min(p-e, F(F(math.floor(p))-n)) for p, e, n in zip(position, full, native)]
    hi = [max(p+e, F(F(math.ceil(p))+n)) for p, e, n in zip(position, full, native)]
    return lo, hi

def inline_placements(data, index):
    placements = N.placements(data, index)
    if len(placements) != struct.unpack_from('<I', data)[0]:
        raise ValueError('Unrepresented SIDI placement count')
    result = []
    for ordinal, (rid, box, rotation) in enumerate(placements):
        header = struct.pack('<I9hHH', rid, *box, rotation, 0)
        at = data.find(header)
        if at < 4 or data.find(header, at+1) >= 0:
            raise ValueError('Ambiguous SIDI placement header')
        upper = struct.unpack_from('<H', data, at+24)[0]
        secondary = struct.unpack_from('<I', data, at+26)[0]
        r = Reader(data)
        r.at = at+32
        count = r.u32()
        gos = []
        for _ in range(count):
            offset = r.at
            gos.append((offset, struct.unpack('<II3f4f6f3IH', r.take(74))))
        r.take(26*r.u32())  #62476224:2+2+1+2+2+1+8+8 source bytes
        count, extra = r.read('<II')
        props = prop_records(r, count)
        result.append(dict(ordinal=ordinal, prefab=rid, secondary=secondary, rotation=rotation,
                           upper=upper, pivot=box[:3], offset=at, typedEnd=r.at,
                           resourceAvailability=[reason for ok, reason in ((rid in index, 'missing-primary-prefab'), (not secondary or secondary in index, 'missing-secondary-resource')) if not ok],
                           propHeaderExtra=extra, gos=gos, props=props))
    ordered = sorted(result, key=lambda x: x['offset'])
    for i, p in enumerate(ordered):
        end = ordered[i+1]['offset'] if i+1 < len(ordered) else len(data)
        if p['typedEnd'] > end:
            raise ValueError('Overlapping SIDI typed payload')
        p['opaqueNonObstacleTailSha256'] = digest(data[p['typedEnd']:end])
        p['opaqueNonObstacleTailBytes'] = end-p['typedEnd']
    return result

def export(tools, sectors, catalog_path):
    sys.path.insert(0, str(Path(tools).resolve()))
    import rezmap_ltb as R
    index = R.index()
    catalog_bytes = Path(catalog_path).read_bytes()
    catalog = json.loads(catalog_bytes)
    if catalog['sourceGobSha256'] != GOB_SHA256:
        raise ValueError('Unexpected GOB catalog source')
    templates = {t['goid']: t for t in catalog['templates']}
    if len(templates) != len(catalog['templates']):
        raise ValueError('Ambiguous GOB catalog')
    path = next(p for ident, _, p in M.WORLDS if ident == 1)
    metr_bytes = (Path(R.D)/path).read_bytes()
    parents = M.read_metr(str(Path(R.D)/path), include_resource_refs=True)
    ledger, missing, obstacles, absent, placements = [], [], [], [], []
    keys = collections.defaultdict(set)
    for parent in parents:
        sector = parent['sector']
        if sectors is not None and sector not in sectors:
            continue
        corner = parent['min']
        if corner[1] != 0:
            raise ValueError('Unsupported nonzero SCIN Y')
        for kind in ('SID', 'SIDG', 'SIDI'):
            rid = parent['resource_refs'][kind]
            if rid not in index:
                missing.append(dict(sector=sector, layer=kind, sourceRid=rid, parentMin=parent['min'], parentMax=parent['max']))
                continue
            entry = index[rid]
            if entry['flags'] != 4:
                raise ValueError('Unsupported resource flags')
            data = R.read(entry)
            evidence = dict(sector=sector, layer=kind, sourceRid=rid, sha256=digest(data))
            ledger.append(evidence)
            go_inputs = []
            if kind == 'SID':
                buffers = osid_buffers(data)
                go_inputs = [(buffers[3][0]+off, g, None) for off, g in go_records(buffers[3][1])]
                pr = Reader(buffers[6][1])
                count, extra = pr.read('<II')
                for prop in prop_records(pr, count):
                    lo, hi = prop_world_bounds(prop['rawBounds'], corner=corner)
                    obstacles.append(dict(identity=f"root-osid:{rid:08x}:{prop['offset']}", domain='ROOT_OSID', **evidence, **prop, absoluteSourceOffset=buffers[6][0]+prop['offset'], min=lo, max=hi))
                pr.eof()
            elif kind == 'SIDG':
                if data[:4] != b'SIDG':
                    raise ValueError('Unknown SIDGO signature')
                rr = M.Reader(data, 16)
                M.read_schema(rr)
                go_inputs = [(rr.p-4+off, g, None) for off, g in go_records(data[rr.p-4:])]
            else:
                buffers = osid_buffers(data)
                for placement in inline_placements(buffers[0][1], index):
                    if placement['upper'] or placement['rotation'] not in range(4):
                        raise ValueError('Unsupported IPI parent transform')
                    context = {k: v for k, v in placement.items() if k not in ('gos', 'props')}
                    placements.append(dict(**evidence, **context))
                    go_inputs.extend((buffers[0][0]+off, g, context) for off, g in placement['gos'])
                    for prop in placement['props']:
                        lo, hi = prop_world_bounds(prop['rawBounds'], placement['pivot'], placement['rotation'], corner)
                        obstacles.append(dict(identity=f"ipi-prop:{rid:08x}:{placement['ordinal']}:{prop['offset']}", domain='IPI_PROP', **evidence, **prop, absoluteSourceOffset=buffers[0][0]+prop['offset'], parent=context, min=lo, max=hi))
            for offset, g, context in go_inputs:
                goid, static = g[:2]
                if static > 0xfffff or sector > 0x7ff:
                    raise ValueError('Invalid native GO identity')
                if context:
                    pos, quat = T.sector_pose(g[2:5], g[5:9], context['pivot'], context['rotation'], corner, parent_kind='direct-sector')
                else:
                    pos, quat = (corner[0]+g[2], g[3], corner[2]+g[4]), g[5:9]
                key = sector << 20 | static
                keys[key].add((goid, *pos, *quat))
                template = templates.get(goid)
                if template is None or template['status'] not in ('Model6Absent', 'PositiveFiniteAuthoredBounds'):
                    raise ValueError(f'Unknown template bounds:{goid}')
                common = dict(**evidence, key=key, goid=goid, position=pos, quaternion=quat,
                              quaternionHex=struct.pack('<4f', *quat).hex(), absoluteSourceOffset=offset,
                              recordSha256=digest(struct.pack('<II3f4f6f3IH', *g)), parent=context)
                if template['status'] == 'Model6Absent':
                    absent.append(common)
                else:
                    for model in template['models']:
                        lo, hi = model_world_bounds(pos, quat, model['halfExtents'])
                        obstacles.append(dict(identity=f"go:{rid:08x}:{context['ordinal'] if context else -1}:{offset}:{model['componentOrdinal']}", domain='GO_MODEL6', **common, model=model, min=lo, max=hi))
    return dict(format=1, scope='Authored persistent static spatial bounds, not full rendering/physics/animation/avatar clearance',
                originSectorSelection=None if sectors is None else sorted(sectors),
                crossSectorSourceTraversal=sectors is None,
                clientSha256=CLIENT_SHA256, gobSha256=GOB_SHA256, metrSha256=digest(metr_bytes), catalogSha256=digest(catalog_bytes),
                sourceSha256=digest(Path(__file__).read_bytes()), sourceLayers=ledger, missingDeclaredLayers=missing,
                obstacles=obstacles, model6Absent=absent, placementContexts=placements,
                conflictingGoKeys=sorted(k for k, variants in keys.items() if len(variants)>1),
                counts=dict(collections.Counter(o['domain'] for o in obstacles)),
                limits=['No first/last conflict resolution; all variants retained.',
                        '--sectors selects source origins only; use all and intersect world bounds to certify cross-sector overhang coverage.',
                        'Missing declared source layers remain unsupported; no legacy geometry fallback.',
                        'SIDI non-obstacle tail bytes are preserved by hash, not reinterpreted as new pose arrays.',
                        'Model6Absent is domain-specific, not a global no-render claim.',
                        'EPI, SID buffer8 and structural NAV require their separate reviewed profiles.'])

def main():
    a = argparse.ArgumentParser(description=__doc__)
    a.add_argument('--tools', required=True)
    a.add_argument('--sectors', default='all', help='all or comma-separated SOURCE ORIGINS; subset output does not certify cross-sector coverage')
    a.add_argument('--catalog', required=True)
    a.add_argument('--output', required=True)
    args = a.parse_args()
    sectors = None if args.sectors == 'all' else {int(v) for v in args.sectors.split(',')}
    result = export(args.tools, sectors, args.catalog)
    output = Path(args.output)
    data = (json.dumps(result, separators=(',', ':'), allow_nan=False)+'\n').encode()
    if output.suffix == '.gz':
        with gzip.GzipFile(filename=str(output), mode='wb', mtime=0) as f:
            f.write(data)
    else:
        output.write_bytes(data)
    print(json.dumps(dict(counts=result['counts'], missing=result['missingDeclaredLayers'], sha256=digest(output.read_bytes()))))

if __name__ == '__main__':
    main()
