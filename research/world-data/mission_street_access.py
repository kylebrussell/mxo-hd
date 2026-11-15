"""Audited original outdoor NAV adjunct; existing interior profiles are never rewritten.

This exports evidence, not admission decisions. Original region/polygon/edge ordinals,
cross-sector flags, unavailable references and all counted resource boundaries survive.
"""
import argparse, csv, gzip, hashlib, json, re, struct, sys
from pathlib import Path


def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()


def outdoor(nav):
    if len(nav) < 16 or struct.unpack_from('<4I', nav)[:2] != (0, 0):
        raise ValueError('Unknown outdoor NAV header')
    count = struct.unpack_from('<I', nav, 8)[0]
    if count < 1 or 16 + 8 * count > len(nav):
        raise ValueError('Truncated outdoor section table')
    sections = [struct.unpack_from('<2I', nav, 16 + 8*i) for i in range(count)]
    regions = []
    for ordinal, (offset, length) in enumerate(sections[1:]):
        if offset < 16 + 8*count or length < 16 or offset + length > len(nav):
            raise ValueError('Outdoor section outside source resource')
        at = nav.find(b'NVPS', offset, offset+length)
        if at < 0 or at + 16 > offset+length:
            raise ValueError('Missing declared outdoor NVPS')
        header = nav[at:at+16]
        size, flags, polygons = struct.unpack_from('<3I', header, 4)
        if flags != size | 0x81000000 or polygons > (length-16)//20:
            raise ValueError('Invalid original outdoor count/flags')
        cursor = at+16; rows = []
        for index in range(polygons):
            if cursor+20 > offset+length: raise ValueError('Truncated outdoor polygon')
            start = cursor; raw = nav[cursor:cursor+20]; cursor += 20
            edges = struct.unpack_from('<I', raw, 16)[0]
            if edges > (offset+length-cursor)//20: raise ValueError('Truncated outdoor edges')
            values = []
            for edge in range(edges):
                values.append(dict(Index=edge, SourceOffset=cursor, Raw=nav[cursor:cursor+20].hex()))
                cursor += 20
            rows.append(dict(Index=index, SourceOffset=start, RawHeader=raw.hex(), Edges=values))
        regions.append(dict(Id=ordinal, SectionOffset=offset, SectionLength=length,
                            NvpsOffset=at, RawHeader=header.hex(), PolygonEnd=cursor, Polygons=rows))
    return dict(RawHeader=nav[:16].hex(), RawSectionTable=nav[16:16+8*count].hex(),
                SnavOffset=sections[0][0], SnavLength=sections[0][1], Regions=regions)


def export(tools, repository, interiors, output, sectors):
    sys.path.insert(0, str(Path(tools).resolve()))
    import rezmap_ltb as R
    import mission_open_floor_world_bounds as B
    index, paths = R.index(), R.by_path()
    metr = Path(R.D)/next(p for world, _, p in B.M.WORLDS if world == 1)
    parents = {r['sector']: r for r in B.M.read_metr(str(metr), include_resource_refs=True)}
    manifest = json.loads((Path(interiors)/'manifest.json').read_text())
    if manifest['World'] != 1 or manifest['Format'] != 2: raise ValueError('Explicit world1 interior identity required')
    definitions = (Path(repository)/'hds/resources/gameobjects/definitions/GameObjectDefinitions.cs').read_text()
    classes = {int(go): int(cls) for cls, go in re.findall(r'public AttributeClass(363|364|365) Object(\d+)\s*=', definitions)}
    legacy = {}; source_files = []
    for path in sorted((Path(repository)/'data').glob('staticObjects_slums_*.csv')):
        source_files.append(dict(File='data/'+path.name, Sha256=sha(path.read_bytes())))
        for line, row in enumerate(csv.DictReader(path.open()), 2):
            if int(row['metr_id']) != 1 or int(row['sector_id']) not in sectors: continue
            goid = int.from_bytes(bytes.fromhex(row['type']), 'little')
            if goid not in classes: continue
            key = int.from_bytes(bytes.fromhex(row['mxoId']), 'little')
            signature = dict(Goid=goid, Exterior=row['exterior'].lower() == 'true',
                             Position=[float(row[k]) for k in ('pos_x', 'pos_y', 'pos_z')],
                             Quaternion=row['quat'].lower(), Rotation=float(row['rotation']))
            legacy.setdefault(key, []).append(dict(File='data/'+path.name, Line=line, Signature=signature))
    if not source_files: raise ValueError('Declared legacy source files unavailable')
    output = Path(output); output.mkdir(parents=True, exist_ok=True); declarations = []
    for sector in sorted(set(sectors)):
        declaration = next(r for r in manifest['Sectors'] if r['Sector'] == sector)
        compressed = (Path(interiors)/(str(sector)+'.json.gz')).read_bytes()
        if sha(compressed) != declaration['Sha256'].lower(): raise ValueError('Interior payload identity')
        payload = gzip.decompress(compressed)
        if sha(payload) != declaration['Digest'].lower(): raise ValueError('Interior digest')
        interior = json.loads(payload); parent = parents[sector]; references = parent['resource_refs']
        sidi = index[references['SIDI']]; navpath = sidi['path'][:-5]+'.nav'; nav = R.read(paths[navpath])
        if sha(nav) != interior['NavSha256']: raise ValueError('Original NAV differs from interior source')
        topology = outdoor(nav)
        if len(topology['Regions']) != interior['OutdoorRegions']: raise ValueError('Original outdoor ordinal count')
        layers = []; doors = []; objects = []
        # Complete declared GO layers are required even when no door happens to match a portal.
        for kind in ('SID', 'SIDG', 'SIDI'):
            rid = references[kind]
            if rid not in index: raise ValueError('Missing declared native GO layer: '+kind)
            source = R.read(index[rid]); inputs = []
            if kind == 'SID':
                buffers = B.osid_buffers(source)
                inputs = [(buffers[3][0]+off, g, None) for off, g in B.go_records(buffers[3][1])]
            elif kind == 'SIDG':
                if source[:4] != b'SIDG': raise ValueError('Unknown native SIDG')
                reader = B.M.Reader(source, 16); B.M.read_schema(reader)
                inputs = [(reader.p-4+off, g, None) for off, g in B.go_records(source[reader.p-4:])]
            else:
                buffers = B.osid_buffers(source)
                for context in B.sidi_go_contexts(buffers[0][1], index):
                    if context['upper'] or context['rotation'] not in range(4): raise ValueError('Unsupported native parent hierarchy')
                    inputs.extend((buffers[0][0]+off, g, context) for off, g in context['gos'])
            layers.append(dict(Kind=kind, Rid=rid, Length=len(source), Sha256=sha(source), Count=len(inputs), DoorCount=sum(g[0] in classes for _,g,_ in inputs)))
            for offset, g, context in inputs:
                if g[0] > 0xffff or g[1] > 0xfffff: raise ValueError('Native static key/type outside source identity')
                raw = source[offset:offset+74]
                if len(raw) != 74 or struct.unpack_from('<II',raw) != g[:2]: raise ValueError('Native GO source offset mismatch')
                record = dict(Key=sector << 20 | g[1], Goid=g[0], Class=classes.get(g[0],0), Layer=kind,
                                  Rid=rid, SourceOffset=offset, Raw=raw.hex(), RecordSha256=sha(raw),
                                  Region=interior['OutdoorRegions']+context['ordinal'] if context else None,
                                  Pivot=context['pivot'] if context else None,
                                  Rotation=context['rotation'] if context else None)
                objects.append(record)
                if g[0] in classes: doors.append(record)
        document = dict(Format=1, World=1, Sector=sector, InteriorDigest=declaration['Digest'],
                        ParentCorner=interior['ParentCorner'], NavPath=navpath, NavSha256=sha(nav),
                        NavLength=len(nav), Outdoor=topology, Layers=layers, NativeDoors=doors, NativeObjects=objects,
                        Legacy=[dict(Key=k, Rows=legacy[k]) for k in sorted({d['Key'] for d in doors}) if k in legacy],
                        LegacySources=source_files)
        raw = canonical(document); packed = gzip.compress(raw, mtime=0)
        (output/(str(sector)+'.json.gz')).write_bytes(packed)
        declarations.append(dict(Sector=sector, Digest=sha(raw).upper(), Sha256=sha(packed).upper(),
                                 InteriorDigest=declaration['Digest'], Regions=len(topology['Regions']), NativeDoors=len(doors)))
    text=(Path(interiors)/'manifest.json').read_text(encoding='utf-8').replace('\r\n','\n')
    result = dict(Format=1, World=1, InteriorsManifest=sha(text.encode()), Sectors=declarations)
    (output/'manifest.json').write_bytes(canonical(result)+b'\n')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('tools'); parser.add_argument('repository'); parser.add_argument('interiors'); parser.add_argument('output')
    parser.add_argument('--sectors', type=int, nargs='+', required=True)
    args = parser.parse_args()
    print(json.dumps(export(args.tools, args.repository, args.interiors, args.output, args.sectors)))
