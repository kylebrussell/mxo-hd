"""Portable ROOT-SID buffer8 and EPI static-bound source profiles.

Native readers:624c0b70/624c0130 and624bfd80/624b93b0. EPI parent:
628c1a28+10=624b4a30; actual SCIN translation is passed by6249f093..ba,
added at624b81ba..8208, and sent to the inline PROP draw at624b869c.
These are authored spatial bounds, not decoded model-mesh/physics guarantees.
"""
import argparse
import gzip
import hashlib
import itertools
import json
import math
from pathlib import Path
import struct
import sys

import metr_sectors as M
import native_prefab_transform as T
from mission_open_floor_world_bounds import Reader, osid_buffers, prop_records, compact_bound, model_world_bounds

F = T.f32


def digest(b):
    return hashlib.sha256(b).hexdigest()


def schema(r):
    h = r.take(16)
    if h[:4] != b'ISHD' or struct.unpack_from('<I', h, 4)[0] != 0x101 or struct.unpack_from('<I', h, 12)[0] != 0xffffffff:
        raise ValueError('Unknown EPI reflected schema')
    body = r.take(struct.unpack_from('<I', h, 8)[0])
    if body[:4] != b'ISPI':
        raise ValueError('Unknown EPI schema values')


def epi_records(data):
    r = Reader(data)
    out = []
    for ordinal in range(r.u32()):
        start = r.at
        header = r.take(96)
        rid, *box = struct.unpack_from('<I9h', header)
        rotation, flags, extra = struct.unpack_from('<IIH', header, 22)
        if rotation not in range(4):
            raise ValueError('Unknown EPI rotation')
        links = r.take(r.u32() * 8)  # Four-u16 locator links, not child 3D poses.
        gos = []
        for j in range(r.u32()):
            at = r.at
            raw = r.take(74)
            gos.append(dict(ordinal=j, offset=at, raw=raw.hex(), fields=struct.unpack('<II3f4f6f3IH', raw)))
        count, prop_extra = r.read('<II')
        props = prop_records(r, count)
        r.take(r.u32() * 12)
        schema(r)
        for _ in range(r.u32()):
            schema(r)
        out.append(dict(ordinal=ordinal, offset=start, assetRid=rid, pivot=box[:3], box=box[3:],
                        rotation=rotation, flags=flags, extra=extra, opaque64Sha256=digest(header[32:]),
                        locatorLinksSha256=digest(links), gos=gos, props=props,
                        recordSha256=digest(data[start:r.at])))
    r.eof()
    return out


def epi_translation(context, corner):
    return [F(F(v * 10) + c) for v, c in zip(context['pivot'], corner)]


def epi_point(v, context, corner):
    # Independently decoded EPI parent; do not bypass sector_pose's intentional
    # refusal of nested/EPI input by passing a fake direct-sector parent kind.
    return [F(x + t) for x, t in zip(T.quarter_turn(v, context['rotation']), epi_translation(context, corner))]


def epi_bound(lo, hi, context, corner):
    points = []
    tr = epi_translation(context, corner)
    for v in itertools.product(*zip(lo, hi)):
        # Preserve the exact conservative local boundary before the parent
        # store. Casting it to f32 here can shrink a nonrepresentable bound.
        x, y, z = v
        a = ((x, y, z), (z, y, -x), (-x, y, -z), (-z, y, x))[context['rotation']]
        points.append([x + t for x, t in zip(a, tr)])
        points.append([F(x + t) for x, t in zip(a, tr)])
    return [min(v[k] for v in points) for k in range(3)], [max(v[k] for v in points) for k in range(3)]


def sid8_records(data, corner):
    r = Reader(data)
    out = []
    for ordinal in range(r.u32()):
        at = r.at
        key, *values = r.read('<I6f')
        centre, half = values[:3], values[3:]
        if not all(math.isfinite(v) for v in values) or any(v < 0 for v in half):
            raise ValueError('Unknown buffer8 authored bounds')
        blob = r.take(r.u32())
        lo, hi = [], []
        for k in range(3):
            lo.append(min(centre[k] - half[k] + corner[k], F(F(centre[k] - half[k]) + corner[k])))
            hi.append(max(centre[k] + half[k] + corner[k], F(F(centre[k] + half[k]) + corner[k])))
        out.append(dict(ordinal=ordinal, offset=at, keyRawU32=key, centre=centre, halfExtent=half,
                        min=lo, max=hi, blobSha256=digest(blob), recordSha256=digest(data[at:r.at])))
    r.eof()
    return out


def export(tools, catalogs, sectors):
    sys.path.insert(0, str(Path(tools).resolve()))
    import rezmap_ltb as R
    index = R.index()
    templates = {}
    catalog_hashes = {}
    for path in catalogs:
        raw = Path(path).read_bytes()
        catalog_hashes[Path(path).name] = digest(raw)
        for t in json.loads(raw)['templates']:
            if t['goid'] in templates and templates[t['goid']] != t:
                raise ValueError('Conflicting GOB catalog template')
            templates[t['goid']] = t
    path = next(p for ident, _, p in M.WORLDS if ident == 1)
    parents = M.read_metr(str(Path(R.D) / path), include_resource_refs=True)
    rows, ledger, missing = [], [], []
    counts = dict(epiProps=0, epiModel6=0, epiModel6Absent=0, sid8=0)
    for parent in parents:
        sec = parent['sector']
        if sectors is not None and sec not in sectors:
            continue
        rid = parent['resource_refs']['SID']
        if rid not in index:
            missing.append(dict(sector=sec, layer='SID', sourceRid=rid))
            continue
        if index[rid]['flags'] != 4:
            raise ValueError('Unsupported root SID resource')
        data = R.read(index[rid]); buffers = osid_buffers(data); corner = parent['min']
        source = dict(sector=sec, sourceRid=rid, sourceSha256=digest(data))
        epis = epi_records(buffers[1][1])
        mesh = sid8_records(buffers[8][1], corner)
        for e in epis:
            context = {k: v for k, v in e.items() if k not in ('gos', 'props')}
            for prop in e['props']:
                raw = prop['rawBounds']
                lo = [min(v, compact_bound(v, -9)) for v in raw[:3]]
                hi = [max(v, compact_bound(v, 9)) for v in raw[3:]]
                lo, hi = epi_bound(lo, hi, context, corner)
                rows.append(dict(identity=f"epiprop:{rid:08x}:{e['ordinal']}:{prop['ordinal']}:{buffers[1][0]+prop['offset']}", domain='EPI_PROP', **source,
                                 context=context, **prop, min=lo, max=hi))
                counts['epiProps'] += 1
            for g in e['gos']:
                values = g['fields']; goid = values[0]; template = templates.get(goid)
                if template is None:
                    raise ValueError(f'Missing EPI template:{goid}')
                if template['status'] == 'Model6Absent':
                    counts['epiModel6Absent'] += 1
                    continue
                if template['status'] != 'PositiveFiniteAuthoredBounds':
                    raise ValueError('Unknown EPI Model6')
                for i, model in enumerate(template['models']):
                    lo, hi = model_world_bounds(values[2:5], values[5:9], model['halfExtents'])
                    lo, hi = epi_bound(lo, hi, context, corner)
                    world = epi_point(values[2:5], context, corner)
                    quat = T.compose_quaternion(T.rotation_quaternion(context['rotation']), values[5:9])
                    wl, wh = model_world_bounds(world, quat, model['halfExtents'])
                    lo = [min(a, b) for a, b in zip(lo, wl)]; hi = [max(a, b) for a, b in zip(hi, wh)]
                    rows.append(dict(identity=f"epigo:{rid:08x}:{e['ordinal']}:{g['ordinal']}:{i}:{buffers[1][0]+g['offset']}", domain='EPI_MODEL6', **source,
                                     context=context, goid=goid, rawRecord=g['raw'], model=model, min=lo, max=hi))
                    counts['epiModel6'] += 1
        for m in mesh:
            rows.append(dict(identity=f"sid8:{rid:08x}:{m['ordinal']}:{buffers[8][0]+m['offset']}", domain='SID8', **source, **m))
            counts['sid8'] += 1
        ledger.append(dict(**source, epiCount=len(epis), sid8Count=len(mesh), exactEof=True))
    return dict(format=1, world=1, sourceOriginSelection='all' if sectors is None else sorted(sectors),
                crossSectorSourceTraversal=sectors is None, catalogHashes=catalog_hashes,
                sourceSha256=digest(Path(__file__).read_bytes()), sourceLayers=ledger,
                missingDeclaredLayers=missing, counts=counts, obstacles=rows,
                qualification='Authored static spatial/culling bounds only; all source props and GOIN variants retained. Separate from structural NAV, mesh physics and GPU arithmetic parity.')


def main():
    a = argparse.ArgumentParser(description=__doc__)
    a.add_argument('--tools', required=True)
    a.add_argument('--catalog', action='append', required=True)
    a.add_argument('--sectors', default='all', help='SOURCE ORIGINS, not a complete bounded overlap query')
    a.add_argument('--output', required=True)
    args = a.parse_args()
    result = export(args.tools, args.catalog, None if args.sectors == 'all' else {int(v) for v in args.sectors.split(',')})
    out = Path(args.output)
    b = (json.dumps(result, separators=(',', ':'), allow_nan=False) + '\n').encode()
    if out.suffix == '.gz':
        with out.open('wb') as raw:
            with gzip.GzipFile(filename='', fileobj=raw, mode='wb', mtime=0) as f:
                f.write(b)
    else:
        out.write_bytes(b)
    print(json.dumps(dict(counts=result['counts'], missing=result['missingDeclaredLayers'], sha256=digest(out.read_bytes()))))


if __name__ == '__main__':
    main()
