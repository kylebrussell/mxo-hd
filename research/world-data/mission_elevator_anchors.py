"""Audited mission-elevator occurrence groups. Raw CSV membership is NOT region ownership.
Original counted SIDI records + direct SCIN parent, using established native transform.
No source selection, world/static mutation, runtime fallback, or height heuristic.
"""
import argparse, collections, csv, gzip, hashlib, itertools, json, struct, sys
from pathlib import Path
import mission_open_floor_world_bounds as B

def export(tools, output, sectors=None):
    sys.path.insert(0,str(Path(tools).resolve()))
    import rezmap_ltb as R
    idx=R.index();bp=R.by_path();parents=B.M.read_metr(str(Path(R.D)/next(p for i,_,p in B.M.WORLDS if i==1)),include_resource_refs=True)
    legacy=collections.defaultdict(list)
    for path in sorted(Path('data').glob('staticObjects_slums_*.csv')):
        for line,row in enumerate(csv.DictReader(path.open()),2):
            if row['metr_id']!='1' or row['exterior'].lower()!='false':continue
            sector=int(row['sector_id']);key=int.from_bytes(bytes.fromhex(row['mxoId']),'little');static=int.from_bytes(bytes.fromhex(row['staticId']),'little')
            if key!=(sector<<20|static):raise ValueError('Raw identity mismatch')
            goid=int.from_bytes(bytes.fromhex(row['type']),'little')
            if goid not in (417,419,19,1407,1415,1410,1408,1412,3,552,1416,357):continue
            sig=dict(Goid=goid,Position=[float(row[k]) for k in ('pos_x','pos_y','pos_z')],Quaternion=row['quat'].lower())
            if sig not in [x['Signature'] for x in legacy[(sector,static)]]:
                legacy[(sector,static)].append(dict(Signature=sig,File=str(path).replace('\\','/'),Line=line))
    by_sector=collections.defaultdict(dict)
    for key,members in legacy.items():by_sector[key[0]][key]=members
    manifest=[];parent_path=Path(R.D)/next(p for i,_,p in B.M.WORLDS if i==1);output=Path(output);output.mkdir(parents=True,exist_ok=True)
    for parent in parents:
        sector=parent['sector']
        if sectors and sector not in sectors:continue
        if parent['min'][1]!=0:raise ValueError('Unsupported parent')
        srid=parent['resource_refs']['SIDI']
        if srid not in idx:continue
        raw=R.read(idx[srid]);data=B.osid_buffers(raw)[0][1]
        # Read original NAV independently; same region numbering as shipped navmesh exporter.
        sidi_path=idx[srid].get('path',''); nav_path=sidi_path[:-5]+'.nav' if sidi_path.endswith('.sidi') else None
        if not nav_path or nav_path not in bp:continue
        nav=R.read(bp[nav_path]);outdoors=len(B.N.sections(nav))-1
        native=collections.defaultdict(list)
        for c in B.sidi_go_contexts(data,idx):
            if c['upper'] or c['rotation'] not in range(4):raise ValueError('Unsupported native transform')
            for offset,g in c['gos']:
                key=(sector,g[1])
                if key not in legacy:continue
                pos,q=B.T.sector_pose(g[2:5],g[5:9],c['pivot'],c['rotation'],parent['min'],parent_kind='direct-sector')
                if g[0] in (417,419):
                    # Concrete6231ae00 uses bound midpoint, not the logical GO origin.
                    # The existing +145 convention is admitted only after this exact record proves it.
                    rotated=B.T.quarter_turn(g[2:5],c['rotation']);translation=[p-v for p,v in zip(pos,rotated)]
                    corners=[[v+t for v,t in zip(B.T.quarter_turn(corner,c['rotation']),translation)] for corner in itertools.product(*zip(g[9:12],g[12:15]))]
                    low=[min(corner[i] for corner in corners) for i in range(3)];high=[max(corner[i] for corner in corners) for i in range(3)]
                    centre=[B.T.f32(B.T.f32(a+b)*B.T.f32(.5)) for a,b in zip(low,high)]
                    if centre!=[pos[0],pos[1]+145,pos[2]]:raise ValueError('Unproved mission elevator bound-centre convention')
                native[key].append(dict(Goid=g[0],Region=outdoors+c['ordinal'],Position=pos,Quaternion=struct.pack('<4f',*q).hex(),Placement=c['ordinal'],SourceRid=srid,PrefabRid=c['prefab'],RecordOffset=offset))
        groups=[];sources=[]
        for key,members in sorted(by_sector[sector].items()):
            if key not in native:continue
            occ=native[key];door=all(x['Signature']['Goid'] in (417,419) for x in members)
            if door and not all(x['Goid'] in (417,419) for x in occ):continue
            target=groups if door else sources
            target.append(dict(StaticId=key[1],Key=sector<<20|key[1],Legacy=members,Occurrences=occ))
        if not groups:continue
        body=dict(Format=1,World=1,Sector=sector,OriginalOutdoorRegionCount=outdoors,SidiSha256=B.digest(raw),NavSha256=B.digest(nav),Groups=groups,Sources=sources)
        encoded=json.dumps(body,sort_keys=True,separators=(',',':')).encode();path=output/(str(sector)+'.json.gz')
        with path.open('wb') as file:
            with gzip.GzipFile(filename='',mode='wb',fileobj=file,mtime=0) as gz:gz.write(encoded)
        manifest.append(dict(Sector=sector,Groups=len(groups),Sources=len(sources),Sha256=B.digest(path.read_bytes())))
    (output/'manifest.json').write_text(json.dumps(dict(Format=1,World=1,MetrSha256=B.digest(parent_path.read_bytes()),NativeTransformSha256=B.digest(Path(B.T.__file__).read_bytes()),LegacyFiles=[dict(File=str(f).replace('\\','/'),Sha256=B.digest(f.read_bytes())) for f in sorted(Path('data').glob('staticObjects_slums_*.csv'))],Scope='Exact legacy signature membership in original region-qualified counted SIDI occurrence groups; membership does not assert legacy region ownership.',Sectors=manifest),indent=2)+'\n')

if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('tools');a.add_argument('--output',required=True);a.add_argument('--sectors',type=int,nargs='*');args=a.parse_args();export(args.tools,args.output,args.sectors)
