"""Counted native bound-model Position profiles; logical-origin anchors are untouched.

Separate Slums20-type native furniture family; existing326 profiles stay untouched. Native 6231ae73..aeff uses f32(min+max)*.5,
promoted to doubles, for Model.Position. Never infer a centre from mesh HalfY.
"""
import io
import shutil
import argparse
import collections
import csv
import gzip
import itertools
import json
import math
from pathlib import Path
import struct
import sys
import mission_open_floor_world_bounds as B
import mission_open_floor_anchors as A

FORMAT = 1
GOIDS = frozenset([573,1431,1432,1433,1434,1435,1438,1439,1443,1444,1445,1446,1447,1448,1449,1450,1451,6987,37913,38484])
KIND = 'NativeBoundFurniture'
F = B.T.f32


def model_position(minimum, maximum):
    if len(minimum) != 3 or len(maximum) != 3 or any(
            not math.isfinite(v) for v in (*minimum, *maximum)) or any(
            lo >= hi for lo, hi in zip(minimum, maximum)):
        raise ValueError('Invalid counted model bounds')
    return [F(F(lo + hi) * .5) for lo, hi in zip(minimum, maximum)]


def transformed_bounds(g, parent, context):
    lo, hi = g[9:12], g[12:15]
    if any(not math.isfinite(v) for v in (*lo, *hi)) or any(a >= b for a, b in zip(lo, hi)):
        raise ValueError('Invalid GO record bounds')
    points = []
    for point in itertools.product(*zip(lo, hi)):
        if context:
            point, _ = B.T.sector_pose(point, (0, 0, 0, 1), context['pivot'],
                context['rotation'], parent['min'], parent_kind='direct-sector')
        else:
            point = tuple(F(F(v) + F(c)) for v, c in zip(point, parent['min']))
        points.append(point)
    return ([min(p[i] for p in points) for i in range(3)],
            [max(p[i] for p in points) for i in range(3)])


def export(tools, legacy_paths):
    sys.path.insert(0, str(Path(tools).resolve()))
    import rezmap_ltb as R
    index = R.index()
    path = next(p for ident, _, p in B.M.WORLDS if ident == 1)
    metr = (Path(R.D) / path).read_bytes()
    parents = B.M.read_metr(str(Path(R.D) / path), include_resource_refs=True)
    legacy, legacy_conflicts = A.legacy_rows(legacy_paths)
    rotations = {}
    for legacy_path in legacy_paths:
        with Path(legacy_path).open(newline='') as stream:
            for raw in csv.DictReader(stream):
                if int.from_bytes(bytes.fromhex(raw['type']), 'little') not in GOIDS:
                    continue
                key = (int(raw['sector_id']), int.from_bytes(bytes.fromhex(raw['staticId']), 'little'))
                rotation = float(raw['rotation'])
                if not math.isfinite(rotation):
                    raise ValueError('Nonfinite legacy rotation')
                if key in rotations and rotations[key] != rotation:
                    legacy_conflicts.add(key)
                rotations.setdefault(key, rotation)
    variants = collections.defaultdict(list)
    missing, sources = [], []
    for parent in parents:
        if parent['min'][1] != 0:
            raise ValueError('Unrepresented SCIN Y')
        sector = parent['sector']
        for layer in ('SID', 'SIDG', 'SIDI'):
            rid = parent['resource_refs'][layer]
            if rid not in index:
                missing.append(dict(Sector=sector, Layer=layer, Rid=rid))
                continue
            source = index[rid]
            if source['flags'] != 4:
                raise ValueError('Unsupported source layer flags')
            data = R.read(source)
            source_hash = B.digest(data)
            sources.append(dict(Sector=sector, Layer=layer, Rid=rid, Sha256=source_hash))
            records = []
            if layer == 'SID':
                buf = B.osid_buffers(data)[3]
                records = [(buf[0] + offset, g, None) for offset, g in B.go_records(buf[1])]
            elif layer == 'SIDG':
                if data[:4] != b'SIDG':
                    raise ValueError('Unknown SIDGO')
                reader = B.M.Reader(data, 16)
                B.M.read_schema(reader)
                start = reader.p - 4
                records = [(start + offset, g, None) for offset, g in B.go_records(data[start:])]
            else:
                buffers = B.osid_buffers(data)
                nav_rid = parent['resource_refs']['NAV']
                if nav_rid not in index:
                    missing.append(dict(Sector=sector, Layer='NAV', Rid=nav_rid))
                    continue
                nav_data = R.read(index[nav_rid])
                outdoor = len(B.N.sections(nav_data)) - 1
                for context in B.sidi_go_contexts(buffers[0][1], index):
                    if context['upper'] or context['rotation'] not in range(4):
                        raise ValueError('Unrepresented IPI parent')
                    context = dict(context, Region=outdoor + context['ordinal'], OutdoorRegions=outdoor,
                                   NavRid=nav_rid, NavSha256=B.digest(nav_data))
                    records.extend((offset, g, context) for offset, g in context['gos'])
            for offset, g, context in records:
                key = (sector, g[1])
                # Other GOIDs on a selected furniture key remain ambiguity evidence.
                if g[0] not in GOIDS and legacy.get(key, (0, 0, 0, 0))[3] not in GOIDS:
                    continue
                if sector > 0x7ff or g[1] > 0xfffff:
                    raise ValueError('Native identity overflow')
                if context:
                    origin, quaternion = B.T.sector_pose(g[2:5], g[5:9], context['pivot'],
                        context['rotation'], parent['min'], parent_kind='direct-sector')
                else:
                    origin, quaternion = tuple(F(F(v) + F(c)) for v, c in zip(g[2:5], parent['min'])), g[5:9]
                minimum, maximum = transformed_bounds(g, parent, context)
                raw = struct.pack('<II3f4f6f3IH', *g)
                variants[key].append(dict(Static=g[1], NativeKey=sector << 20 | g[1], Goid=g[0],
                    Origin=list(origin), Minimum=minimum, Maximum=maximum,
                    Centre=model_position(minimum, maximum), Quaternion=struct.pack('<4f', *quaternion).hex(),
                    SourceRid=rid, SourceSha256=source_hash, RecordOffset=offset,
                    RecordSha256=B.digest(raw), Layer=layer,
                    Placement=context['ordinal'] if context else -1,
                    Region=context['Region'] if context else -1,
                    OutdoorRegions=context['OutdoorRegions'] if context else -1,
                    NavRid=context['NavRid'] if context else 0, NavSha256=context['NavSha256'] if context else None,
                    PrefabRid=context['prefab'] if context else 0,
                    PrefabSha256=B.digest(R.read(index[context['prefab']])) if context else None))
    omitted_sectors = {row['Sector'] for row in missing}
    rows, excluded = collections.defaultdict(list), []
    for key, choices in sorted(variants.items()):
        old = legacy.get(key)
        # Require exactly one physical placement/record. Do not choose precedence.
        signatures = {json.dumps({k: v for k, v in row.items() if k not in
            ('SourceRid', 'SourceSha256', 'RecordOffset', 'RecordSha256', 'Layer')}, sort_keys=True)
            for row in choices}
        reason = ('MissingDeclaredLayer' if key[0] in omitted_sectors else
                  'AmbiguousLegacy' if key in legacy_conflicts else
                  'AmbiguousNative' if len(signatures) != 1 else
                  'MissingLegacy' if old is None else
                  'WrongTypeOrExterior' if old[3] not in GOIDS or old[-1] != 'false' else None)
        if reason:
            excluded.append(dict(Sector=key[0], Static=key[1], Refusal=reason))
            continue
        row = dict(choices[0], LegacyPosition=list(old[4:7]), LegacyQuaternion=old[7], LegacyRotation=rotations[key])
        rows[key[0]].append(row)
    return rows, dict(Format=FORMAT, Kind=KIND, World=1, Goids=sorted(GOIDS), SourceLayers=sources,
        MissingDeclaredLayers=missing, Excluded=excluded, MetrSha256=B.digest(metr),
        Legacy=[dict(File=Path(p).name, Sha256=B.digest(Path(p).read_bytes())) for p in legacy_paths],
        PositionContract='6231ae73..aeff:f32(f32(minimum+maximum)*0.5),then-double',
        Scope='20 non326 bound furniture Model.Position only; existing326 profile and geometry fingerprints unchanged')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tools', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    repo = Path(__file__).resolve().parents[2]
    contracts_path = Path(__file__).with_name('native_bound_furniture_contracts.json')
    contracts = json.loads(contracts_path.read_text(encoding='utf-8'))
    if set(x['Goid'] for x in contracts['Models']) != GOIDS or any(x['OpenMaxDistance'] != 100 for x in contracts['Models']):
        raise ValueError('Original furniture numeric contract set differs')
    rows, manifest = export(args.tools, [repo / 'data/staticObjects_slums_1.csv', repo / 'data/staticObjects_slums_2.csv'])
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    files = []
    for sector, entries in sorted(rows.items()):
        payload = json.dumps(dict(Format=FORMAT, Kind=KIND, World=1, Sector=sector, Rows=entries),
                             separators=(',', ':'), allow_nan=False).encode()
        target = output / f'{sector}.json.gz'
        buffer = io.BytesIO()
        with gzip.GzipFile(filename='', fileobj=buffer, mode='wb', mtime=0) as stream:
            stream.write(payload)
        compressed = buffer.getvalue()
        if shutil.disk_usage(output).free - len(compressed) < 5 * 1024**3:
            raise RuntimeError('C: profile export would cross5GiB floor; retained partial output')
        target.write_bytes(compressed)
        files.append(dict(Sector=sector, Rows=len(entries), File=target.name, Sha256=B.digest(target.read_bytes())))
    manifest.update(Files=files, ModelContracts=contracts, ModelContractsSha256=B.digest(contracts_path.read_bytes()), ToolSha256=B.digest(Path(__file__).read_bytes()),
        ReaderSha256=B.digest(Path(B.__file__).read_bytes()), TransformSha256=B.digest(Path(B.T.__file__).read_bytes()))
    manifest_bytes = (json.dumps(manifest, indent=2) + '\n').encode('utf-8')
    if shutil.disk_usage(output).free - len(manifest_bytes) < 5 * 1024**3:
        raise RuntimeError('C: manifest export would cross5GiB floor')
    (output / 'manifest.json').write_bytes(manifest_bytes)
    print(json.dumps(dict(Sectors=len(files), Rows=sum(len(v) for v in rows.values()), Excluded=len(manifest['Excluded']))))


if __name__ == '__main__':
    main()
