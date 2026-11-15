"""Primary counted interior topology and GO-specific door evidence. No runtime adoption.
Explicit world/sector selection; original ordinals survive missing resources. See docs/MISSION-NPC-MEASURED-ELEVATORS.md.
"""
import argparse, collections, csv, gzip, hashlib, itertools, json, math, re, struct, sys
from pathlib import Path
import mission_open_floor_world_bounds as B


def digest(data): return hashlib.sha256(data).hexdigest()
def encode(value): return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()

EPSILON = 1e-7  # Existing MissionOpenFloorResolver.Epsilon, not an expanded tolerance.
def same(a,b): return abs(a-b) <= EPSILON
def point_equal(a,b): return all(same(x,y) for x,y in zip(a,b))
def segment_equal(a,b): return (point_equal(a[0],b[0]) and point_equal(a[1],b[1])) or (point_equal(a[0],b[1]) and point_equal(a[1],b[0]))
def on_segment(point,segment):
    (x,z),(tx,tz)=segment;px,pz=point
    return same((px-x)*(tz-z),(pz-z)*(tx-x)) and min(x,tx)<=px<=max(x,tx) and min(z,tz)<=pz<=max(z,tz)
def local_portal(portal):
    raw=bytes.fromhex(portal['Raw']);return [list(struct.unpack_from('<2f',raw,10)),list(struct.unpack_from('<2f',raw,18))]
def sector_segment(region,polygon,edge):
    p=region['Polygons'][polygon];v=p['Edges'];result=[]
    for e in [v[edge],v[(edge+1)%len(v)]]:
        x,z=struct.unpack_from('<2f',bytes.fromhex(e['Raw']),12);rotated=B.T.quarter_turn((x,0,z),region['Rotation'])
        result.append([B.T.f32(rotated[0]+B.T.f32(region['Pivot'][0]*10)),B.T.f32(rotated[2]+B.T.f32(region['Pivot'][2]*10))])
    return result
def source_floor(doc,region,polygon,point):
    translation=[B.T.f32(B.T.f32(region['Pivot'][i]*10)+B.T.f32(doc['ParentCorner'][i])) for i in range(3)]
    x,z=point[0]-translation[0],point[1]-translation[2]
    x,z=((x,z),(-z,x),(-x,-z),(z,-x))[region['Rotation']]
    a,b,c,d=struct.unpack_from('<4f',bytes.fromhex(region['Polygons'][polygon]['RawHeader']))
    return -(a*x+c*z+d)/b+translation[1]
def classify_portal(doc,portal):
    regions={r['Id']:r for r in doc['Regions']};a=regions.get(portal['SourceRegion']);b=regions.get(portal['TargetRegion'])
    if (a and a['Available'] and not a['Polygons']) or (b and b['Available'] and not b['Polygons']): return 'EmptyRegion'
    if not a or not b or not a['Available'] or not b['Available']: return 'UnavailableRegion'
    points=local_portal(portal)
    if point_equal(points[0],points[1]): return 'DegeneratePortal'
    reverse=[p for p in doc['Portals'] if (p['SourceRegion'],p['SourcePolygon'],p['SourceEdge'],p['TargetRegion'],p['TargetPolygon'],p['TargetEdge'])==(portal['TargetRegion'],portal['TargetPolygon'],portal['TargetEdge'],portal['SourceRegion'],portal['SourcePolygon'],portal['SourceEdge'])]
    if len(reverse)!=1 or not segment_equal(points,local_portal(reverse[0])): return 'UnprovedReverse'
    left=sector_segment(a,portal['SourcePolygon'],portal['SourceEdge']);right=sector_segment(b,portal['TargetPolygon'],portal['TargetEdge'])
    if not segment_equal(points,left) or not segment_equal(points,right):
        return 'PartialPortal' if all(on_segment(p,left) and on_segment(p,right) for p in points) else 'UnprovedEndpoints'
    af=[source_floor(doc,a,portal['SourcePolygon'],p) for p in portal['Endpoints']];bf=[source_floor(doc,b,portal['TargetPolygon'],p) for p in portal['Endpoints']]
    if not all(same(x,y) for x,y in zip(af,bf)):return 'DifferentFloor'
    if not same(*af) or not same(*bf):return 'NonLevelPortal'
    return 'SameFloor'

def refresh_derived(doc):
    """Rebuild format2 derived fields from retained raw evidence; never rewrite raw records."""
    if doc['Format'] != 2: raise ValueError('Format2 required')
    for region in doc['Regions']:
        translation=[B.T.f32(B.T.f32(region['Pivot'][i]*10)+B.T.f32(doc['ParentCorner'][i])) for i in range(3)]
        for polygon in region['Polygons']:
            plane=struct.unpack_from('<4f',bytes.fromhex(polygon['RawHeader']))
            normal=B.T.quarter_turn(plane[:3],region['Rotation'])
            products=[a*b for a,b in zip(normal,translation)]
            polygon['Plane']=list(normal)+[plane[3]-((products[0]+products[1])+products[2])]
    for portal in doc['Portals']: portal['Transition']=classify_portal(doc,portal)
    return doc

def export(tools, world, sectors, output, format_version=1):
    if format_version not in (1,2): raise ValueError("Unsupported interior format")
    if world not in (1, 2): raise ValueError('Explicit supported world required')
    if not sectors: raise ValueError('Explicit sector scope required')
    sys.path.insert(0, str(Path(tools).resolve()))
    import rezmap_ltb as R
    idx, bp = R.index(), R.by_path()
    metr = Path(R.D) / next(f for i, _, f in B.M.WORLDS if i == world)
    parents = {x['sector']: x for x in B.M.read_metr(str(metr), include_resource_refs=True)}
    files = sorted(Path('data').glob('staticObjects_' + ('slums' if world == 1 else 'dt') + '_*.csv'))
    legacy = collections.defaultdict(list)
    # Same typed class catalog used by MissionInteriorDoorTypes; independent original-GOB proof guards drift.
    repository = Path(__file__).resolve().parents[2]
    catalog = repository / 'hds/resources/gameobjects/definitions/GameObjectDefinitions.cs'
    doors = {int(value) for value in re.findall(r'public AttributeClass364 Object(\d+)\s*=', catalog.read_text())}
    proof = json.loads((repository / 'tests/hds.Tests/Fixtures/Class364DoorContracts.json').read_text())
    if proof['SourceGobSha256'].lower() != B.GOB_SHA256.lower() or doors != {r['Goid'] for r in proof['Rows']}:
        raise ValueError('Class364 catalog disagrees with original GOB')
    if any(r['Fields'].get('OpenUsage/ClientRuleID') != 4004 or r['Fields'].get('OpenUsage/ExecuteRuleID') != 4004 or r['Fields'].get('OpenUsage/Defaultable') != 1 for r in proof['Rows']):
        raise ValueError('Unsupported class364 ordinary Open contract')
    family = {19, 417, 419} | doors
    for f in files:
        for line, row in enumerate(csv.DictReader(f.open()), 2):
            sector = int(row['sector_id']); goid = int.from_bytes(bytes.fromhex(row['type']), 'little')
            if int(row['metr_id']) != world or sector not in sectors or row['exterior'].lower() != 'false' or goid not in family: continue
            key = int.from_bytes(bytes.fromhex(row['mxoId']), 'little')
            if key != sector << 20 | int.from_bytes(bytes.fromhex(row['staticId']), 'little'): raise ValueError('Raw static identity')
            sig = dict(Goid=goid, Position=[float(row[x]) for x in ('pos_x', 'pos_y', 'pos_z')], Quaternion=row['quat'].lower(), Rotation=float(row['rotation']))
            if not any(v['Signature'] == sig for v in legacy[key]): legacy[key].append(dict(Signature=sig, File=str(f).replace('\\', '/'), Line=line))
    output = Path(output); output.mkdir(parents=True, exist_ok=True); manifest = []
    for sector in sorted(sectors):
        par = parents[sector]
        if par['min'][1] != 0: raise ValueError('Unrepresented parent height')
        srid = par['resource_refs']['SIDI']; sidi = R.read(idx[srid]); base, data = B.osid_buffers(sidi)[0]
        navpath = idx[srid]['path'][:-5] + '.nav'; nav = R.read(bp[navpath]); outdoors = len(B.N.sections(nav)) - 1
        contexts = B.sidi_go_contexts(data, idx); regions = []; portals = []; native = collections.defaultdict(list)
        for c in contexts:
            if c['upper'] or c['rotation'] not in range(4): raise ValueError('Unsupported placement hierarchy')
            region = outdoors + c['ordinal']; available = c['prefab'] in idx
            rd = R.read(idx[c['prefab']]) if available else b''; polygons = []; inav_offset = -1; raw_inav_header = None
            if available:
                at = rd.find(b'INAV'); inav_offset = at
                if at < 0: available = False
                else:
                    count = struct.unpack_from('<H', rd, at + 12)[0]; q = at + 16; raw_inav_header = rd[at:at+16].hex()
                    for pi in range(count):
                        plane_offset = q; raw_plane = rd[q:q+20].hex(); plane = struct.unpack_from('<4f', rd, q); ne = struct.unpack_from('<I', rd, q + 16)[0]; q += 20; edges = []
                        if abs(plane[1]) < 1e-12: raise ValueError('Non-walkable INAV plane')
                        normal = B.T.quarter_turn(plane[:3], c['rotation']); translation = [par['min'][i] + c['pivot'][i] * 10 for i in range(3)]
                        if format_version == 2: translation = [B.T.f32(B.T.f32(c['pivot'][i]*10)+B.T.f32(par['min'][i])) for i in range(3)]
                        products = [a*b for a,b in zip(normal, translation)]
                        translated_d = plane[3] - ((products[0] + products[1]) + products[2]) if format_version == 2 else plane[3] - sum(products)
                        wp = list(normal) + [translated_d]
                        for ei in range(ne):
                            offset = q; rg, pg, nx, nz, x, z = struct.unpack_from('<HH4f', rd, q); q += 20
                            y = -(plane[0]*x + plane[2]*z + plane[3]) / plane[1]
                            v, _ = B.T.sector_pose((x,y,z), (0,0,0,1), c['pivot'], c['rotation'], par['min'], parent_kind='direct-sector')
                            edge = dict(RegionTag=rg, Neighbor=pg, Vertex=v, SourceOffset=offset)
                            if format_version == 2: edge.update(Raw=rd[offset:offset+20].hex(), Reference='Boundary' if pg==65535 else 'Local' if rg==65535 else 'UnprovedRegionReference')
                            edges.append(edge)
                        polygon = dict(Index=pi, Plane=wp, Edges=edges)
                        if format_version == 2: polygon.update(SourceOffset=plane_offset, RawHeader=raw_plane)
                        polygons.append(polygon)
            regions.append(dict(Id=region, Ordinal=c['ordinal'], PrefabRid=c['prefab'], PrefabSha256=digest(rd) if rd else None,
                Available=available, Refusal=None if available else ('MissingPrefab' if not rd else 'MissingINAV'), Pivot=c['pivot'], Rotation=c['rotation'], Polygons=polygons))
            table = c['goEnd']; count = struct.unpack_from('<I', data, table)[0]; end = table + 4 + count * 26
            if format_version == 2: regions[-1].update(PrefabLength=len(rd), InavOffset=inav_offset, RawInavHeader=raw_inav_header, PortalCountOffset=table, PortalCount=count, RawPortalCount=data[table:table+4].hex())
            if end > len(data): raise ValueError('Truncated counted portal table')
            for i in range(count):
                at = table + 4 + i*26; raw = data[at:at+26]; a,ap,ae,b,bp_,be,x,z,tx,tz = struct.unpack('<HHBHHB4f', raw)
                if a != region: raise ValueError('Portal source ordinal disagreement')
                portals.append(dict(SourceRegion=a, SourcePolygon=ap, SourceEdge=ae, TargetRegion=b, TargetPolygon=bp_, TargetEdge=be,
                    Endpoints=[[par['min'][0]+x,par['min'][2]+z],[par['min'][0]+tx,par['min'][2]+tz]],
                    CountOffset=table, Count=count, RecordOffset=at, RecordSha256=digest(raw), Raw=raw.hex(), DoorKeys=[]))
            for at,g in c['gos']:
                if g[0] not in family: continue
                key = sector << 20 | g[1]
                if key not in legacy: continue
                pos,q = B.T.sector_pose(g[2:5],g[5:9],c['pivot'],c['rotation'],par['min'],parent_kind='direct-sector')
                rot = B.T.quarter_turn(g[2:5],c['rotation']); tr=[a-b for a,b in zip(pos,rot)]
                corners=[[a+b for a,b in zip(B.T.quarter_turn(corner,c['rotation']),tr)] for corner in itertools.product(*zip(g[9:12],g[12:15]))]
                lo=[min(v[i] for v in corners) for i in range(3)];hi=[max(v[i] for v in corners) for i in range(3)]
                midpoint=[B.T.f32(B.T.f32(a+b)*B.T.f32(.5)) for a,b in zip(lo,hi)]
                native[key].append(dict(Goid=g[0], Region=region, Placement=c['ordinal'], Position=pos, ModelPosition=midpoint, Quaternion=struct.pack('<4f',*q).hex(),
                    Minimum=lo, Maximum=hi, RecordOffset=at, RecordSha256=digest(data[at:at+74]), Raw=data[at:at+74].hex()))
        # A portal association is an exact original segment/door logical-origin membership,
        # not a gameplay interaction radius. Both paired side records remain independently typed.
        for link in portals:
            (x,z),(tx,tz)=link['Endpoints']; dx,dz=tx-x,tz-z
            for key, occurrences in native.items():
                for o in occurrences:
                    if o['Goid'] not in (doors | {417,419}) or o['Region'] not in (link['SourceRegion'],link['TargetRegion']): continue
                    px,pz=o['Position'][0],o['Position'][2]
                    if format_version == 2:
                        own=regions[o['Placement']];raw=bytes.fromhex(o['Raw']);position=struct.unpack_from('<3f',raw,8);v=B.T.quarter_turn(position,own['Rotation']);px=B.T.f32(v[0]+B.T.f32(own['Pivot'][0]*10));pz=B.T.f32(v[2]+B.T.f32(own['Pivot'][2]*10))
                        if on_segment((px,pz),local_portal(link)):link['DoorKeys'].append(key)
                    elif (px-x)*dz==(pz-z)*dx and min(x,tx)<=px<=max(x,tx) and min(z,tz)<=pz<=max(z,tz):link['DoorKeys'].append(key)
            link['DoorKeys']=sorted(set(link['DoorKeys']))
        groups=[dict(Key=k,Legacy=legacy[k],Occurrences=v) for k,v in sorted(native.items())]
        doc=dict(Format=format_version,World=world,Sector=sector,OutdoorRegions=outdoors,SourceRid=srid,ParentCorner=par['min'],MetrSha256=digest(metr.read_bytes()),
            SidiSha256=digest(sidi),SidiBufferOffset=base,SidiBufferLength=len(data),NavSha256=digest(nav),Regions=regions,Portals=portals,Groups=groups)
        if format_version == 2:
            refresh_derived(doc)
        payload=encode(doc); path=output/(str(sector)+'.json.gz')
        with path.open('wb') as f:
            with gzip.GzipFile(filename='',mode='wb',fileobj=f,mtime=0) as gz:gz.write(payload)
        manifest.append(dict(Sector=sector,Digest=digest(payload).upper(),Sha256=digest(path.read_bytes()),Regions=len(regions),Portals=len(portals),Groups=len(groups)))
    (output/'manifest.json').write_text(json.dumps(dict(Format=format_version,World=world,ClientSha256=B.CLIENT_SHA256,GobSha256=B.GOB_SHA256,
        MetrSha256=digest(metr.read_bytes()),TransformSha256=digest(Path(B.T.__file__).read_bytes()),
        DoorCatalog=dict(Class=364,Goids=sorted(doors),Source='hds/resources/gameobjects/definitions/GameObjectDefinitions.cs'),
        LegacyFiles=[dict(File=str(f).replace('\\','/'),Sha256=digest(f.read_bytes())) for f in files],Sectors=manifest),indent=2)+'\n')
    return manifest

if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('tools');a.add_argument('--world',type=int,required=True);a.add_argument('--sectors',type=int,nargs='+',required=True);a.add_argument('--output',required=True);a.add_argument('--format',type=int,choices=[1,2],default=1);x=a.parse_args();print(json.dumps(export(x.tools,x.world,set(x.sectors),x.output,x.format),indent=2))
