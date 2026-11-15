"""Primary counted interior topology and GO-specific door evidence. No runtime adoption.
Explicit world/sector selection; original ordinals survive missing resources. See docs/MISSION-NPC-MEASURED-ELEVATORS.md.
"""
import argparse, collections, csv, gzip, hashlib, itertools, json, struct, sys
from pathlib import Path
import mission_open_floor_world_bounds as B


def digest(data): return hashlib.sha256(data).hexdigest()
def encode(value): return json.dumps(value, sort_keys=True, separators=(',', ':')).encode()

def export(tools, world, sectors, output):
    if world not in (1, 2): raise ValueError('Explicit supported world required')
    if not sectors: raise ValueError('Explicit sector scope required')
    sys.path.insert(0, str(Path(tools).resolve()))
    import rezmap_ltb as R
    idx, bp = R.index(), R.by_path()
    metr = Path(R.D) / next(f for i, _, f in B.M.WORLDS if i == world)
    parents = {x['sector']: x for x in B.M.read_metr(str(metr), include_resource_refs=True)}
    files = sorted(Path('data').glob('staticObjects_' + ('slums' if world == 1 else 'dt') + '_*.csv'))
    legacy = collections.defaultdict(list)
    family = {19, 343, 346, 417, 419}
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
            rd = R.read(idx[c['prefab']]) if available else b''; polygons = []
            if available:
                at = rd.find(b'INAV')
                if at < 0: available = False
                else:
                    count = struct.unpack_from('<H', rd, at + 12)[0]; q = at + 16
                    for pi in range(count):
                        plane = struct.unpack_from('<4f', rd, q); ne = struct.unpack_from('<I', rd, q + 16)[0]; q += 20; edges = []
                        if abs(plane[1]) < 1e-12: raise ValueError('Non-walkable INAV plane')
                        normal = B.T.quarter_turn(plane[:3], c['rotation']); translation = [par['min'][i] + c['pivot'][i] * 10 for i in range(3)]
                        wp = list(normal) + [plane[3] - sum(a*b for a,b in zip(normal, translation))]
                        for ei in range(ne):
                            offset = q; rg, pg, nx, nz, x, z = struct.unpack_from('<HH4f', rd, q); q += 20
                            y = -(plane[0]*x + plane[2]*z + plane[3]) / plane[1]
                            v, _ = B.T.sector_pose((x,y,z), (0,0,0,1), c['pivot'], c['rotation'], par['min'], parent_kind='direct-sector')
                            edges.append(dict(RegionTag=rg, Neighbor=pg, Vertex=v, SourceOffset=offset))
                        polygons.append(dict(Index=pi, Plane=wp, Edges=edges))
            regions.append(dict(Id=region, Ordinal=c['ordinal'], PrefabRid=c['prefab'], PrefabSha256=digest(rd) if rd else None,
                Available=available, Refusal=None if available else ('MissingPrefab' if not rd else 'MissingINAV'), Pivot=c['pivot'], Rotation=c['rotation'], Polygons=polygons))
            table = c['goEnd']; count = struct.unpack_from('<I', data, table)[0]; end = table + 4 + count * 26
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
                    if o['Goid'] not in (343,346,417,419) or o['Region'] not in (link['SourceRegion'],link['TargetRegion']): continue
                    px,pz=o['Position'][0],o['Position'][2]
                    if (px-x)*dz==(pz-z)*dx and min(x,tx)<=px<=max(x,tx) and min(z,tz)<=pz<=max(z,tz):link['DoorKeys'].append(key)
            link['DoorKeys']=sorted(set(link['DoorKeys']))
        groups=[dict(Key=k,Legacy=legacy[k],Occurrences=v) for k,v in sorted(native.items())]
        doc=dict(Format=1,World=world,Sector=sector,OutdoorRegions=outdoors,SourceRid=srid,ParentCorner=par['min'],MetrSha256=digest(metr.read_bytes()),
            SidiSha256=digest(sidi),SidiBufferOffset=base,SidiBufferLength=len(data),NavSha256=digest(nav),Regions=regions,Portals=portals,Groups=groups)
        payload=encode(doc); path=output/(str(sector)+'.json.gz')
        with path.open('wb') as f:
            with gzip.GzipFile(filename='',mode='wb',fileobj=f,mtime=0) as gz:gz.write(payload)
        manifest.append(dict(Sector=sector,Sha256=digest(path.read_bytes()),Regions=len(regions),Portals=len(portals),Groups=len(groups)))
    (output/'manifest.json').write_text(json.dumps(dict(Format=1,World=world,ClientSha256=B.CLIENT_SHA256,GobSha256=B.GOB_SHA256,
        MetrSha256=digest(metr.read_bytes()),TransformSha256=digest(Path(B.T.__file__).read_bytes()),
        LegacyFiles=[dict(File=str(f).replace('\\','/'),Sha256=digest(f.read_bytes())) for f in files],Sectors=manifest),indent=2)+'\n')
    return manifest

if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('tools');a.add_argument('--world',type=int,required=True);a.add_argument('--sectors',type=int,nargs='+',required=True);a.add_argument('--output',required=True);x=a.parse_args();print(json.dumps(export(x.tools,x.world,set(x.sectors),x.output),indent=2))
