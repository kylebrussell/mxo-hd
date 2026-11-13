"""Bounded read-only Sati resource/GOID inventory; reuses the shared native readers.
python research/world-data/sati_resources.py <client-data tools> <MxO directory> [output JSON]
"""
import collections,csv,gzip,hashlib,json,struct,sys
from pathlib import Path
sys.path.insert(0,sys.argv[1])
import rezmap_ltb as R,gob
import hdl_network as H,navmesh as N,subway_network as S
client=Path(sys.argv[2]);output=Path(sys.argv[3] if len(sys.argv)>3 else 'research/world-data/sati-resources.json')
bp=R.by_path();index=R.index();names=collections.defaultdict(list);hashes={}
for path in bp:names[Path(path).name].append(path)
def read(path):
    loose=client/path;data=loose.read_bytes() if loose.is_file() else R.read(bp[path]);hashes[path]=hashlib.sha256(data).hexdigest();return data
def named(name):
    paths=names[name];assert paths,name
    values=[read(p) for p in paths];assert len({hashlib.sha256(v).hexdigest() for v in values})==1,('unproven basename alias',name,paths)
    return values[0]
rows=[r for r in csv.DictReader(open('data/sector_properties.csv',encoding='utf-8')) if r['metr_id']=='9'];assert len(rows)==31
prefabs=set();layers=collections.Counter()
for row in rows:
    name=row['file'].lower()
    for ext in ('.sid','.sidgo','.sidi','.nav'):
        data=named(name+ext);layers[ext]+=1
        if ext=='.sidi':prefabs.update(rid for rid,_,_ in N.placements(data,index))
prefab_hashes={str(rid):hashlib.sha256(R.read(index[rid])).hexdigest() for rid in sorted(prefabs)};assert len(prefabs)==58
base='resource/worlds/final_world/constructs/archive/archive_sati/sati'
next_id,hardlines=H.parse(read(base+'.hdl'));assert next_id==2 and not hardlines
assert not list(S.networks(read(base+'.metr')))
nav=gzip.decompress(Path('data/navmesh/9.bin.gz').read_bytes());assert len(nav)==512010
audit=json.loads(Path('research/world-data/archive_sati-static-audit.json').read_text(encoding='utf-8'))
excluded={tuple(k) for k in audit['excludedKeys']};raw=audit['raw'];safe={}
for row in raw:
    if tuple(row[:2]) not in excluded:safe.setdefault(tuple(row[:2]),row)
counts=collections.Counter(r[2] for r in safe.values());assert len(counts)==95 and len(safe)==2599
usables=collections.defaultdict(list)
for r in csv.DictReader(open('data/object_usables.csv',encoding='utf-8-sig')):
    # Retain the actual column names/values instead of guessing a common object behavior.
    goid=int(next(iter(r.values())))
    if goid in counts:usables[goid].append(r)
g=gob.load();header,goids,_=gob.index(g);components,_=gob.components(g,header);contracts=[]
for goid,count in sorted(counts.items()):
    offset,size=goids[goid];h=gob.record_head(g,offset);storage,layout,schema,comp,unknown,stored=struct.unpack_from('<I4HI',g,h['end'])
    contracts.append(dict(goid=goid,count=count,name=h['name'],schema=schema,componentFields=components[comp],storedBytes=stored,recordSha256=hashlib.sha256(g[offset:offset+size]).hexdigest(),usables=usables[goid]))
captured={}
for p in sorted(Path('data/npc_spawns').glob('*.csv')):
    n=sum(r.get('District')=='9' for r in csv.DictReader(open(p,encoding='utf-8-sig'),delimiter=';'))
    if n:captured[p.name]=n
book=next(r for r in json.loads(Path('data/pvp_archives.json').read_text(encoding='utf-8'))['objects'] if r['goid']==47726)
result=dict(world=9,retailConstructId=18,sectorCount=31,layerCounts=dict(layers),prefabCount=58,prefabSourceSha256=prefab_hashes,
            hardlineNextId=next_id,hardlineCount=0,cnetCount=0,navBytes=len(nav),navSha256=hashlib.sha256(nav).hexdigest(),
            capturedNpcRows=captured,safeGoidContracts=contracts,archiveBook=book,gameobjectsGobSha256=hashlib.sha256(g).hexdigest(),
            sourceSha256=hashes,arrivalPolicy='UNCHANGED existing OURS NearestEntry near(-500,2100,500); exact retail arrival unknown',
            unboundGenericSourceProxyKeys=[list(k) for k,v in sorted(safe.items()) if v[2]==47267])
output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print('31 complete layers/58 prefab sources;95 GOID contracts;0 HDL/CNET; captured='+str(captured))
