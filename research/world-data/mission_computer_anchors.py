import sys,struct,csv,collections,importlib.util,json,hashlib
from pathlib import Path
import argparse
a=argparse.ArgumentParser();a.add_argument('tools');a.add_argument('--output',required=True);a.add_argument('--manifest',required=True);args=a.parse_args()
sys.path.insert(0,args.tools)
import rezmap_ltb as R
base=Path(__file__).resolve().parent
def mod(n):
 s=importlib.util.spec_from_file_location(n,base/(n+'.py'));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
N=mod('navmesh');M=mod('metr_sectors');T=mod('native_prefab_transform');bp=R.by_path();idx=R.index()
rows=[r for r in csv.DictReader(open('data/sector_properties.csv'))if r['metr_id']=='1']
anchors={1415};groups=collections.defaultdict(list);issues=[];sources={}
def buffers(d):
 assert d[:4]==b'OSID';p=struct.unpack_from('<I',d,12)[0];out=[]
 for _ in range(10):
  n,o=struct.unpack_from('<II',d,p);assert o==p+8;out.append(d[o:o+n]);p=o+n
 return out

parentPath=next(rel for ident,_,rel in M.WORLDS if ident==1)
parents={r['sector']:r for r in M.read_metr(str(Path(R.D)/parentPath),include_resource_refs=True)}
def gos(d):
 p=0;n=struct.unpack_from('<I',d,p)[0];p+=4
 for _ in range(n):
  assert d[p:p+4]==b'ISHD';p+=16+struct.unpack_from('<I',d,p+8)[0]
 n=struct.unpack_from('<I',d,p)[0];p+=4
 assert len(d)==p+n*74
 return [struct.unpack_from('<II3f4f6f3IH',d,p+i*74)for i in range(n)]
metadata=collections.Counter();elig={};allgroups=collections.defaultdict(list)
for row in rows:
 sector=int(row['sector']);corner=(float(row['min_x']),0,float(row['min_z']));parent=parents[sector];assert parent['min'][1]==0
 assert parent['name'].lower()==row['file'].lower() and (parent['min'][0],parent['min'][2])==(corner[0],corner[2])
 prefix=parent['path'].lower()
 for suffix in ['.sid','.sidgo','.sidi']:
  name=prefix+suffix
  declared=parent['resource_refs'][{'.sid':'SID','.sidi':'SIDI','.sidgo':'SIDG'}[suffix]]
  if declared not in idx:
   assert not (Path(R.D)/name).exists(), 'Unrepresented loose static layer'
   issues.append(dict(sector=sector,layer=suffix,declaredRid=declared,reason='unavailable declared static layer'));continue
  rec=idx[declared];assert rec['flags']==4
  if name in bp:
   alias=bp[name];assert (rec['pack'],rec['off'],rec['size'])==(alias['pack'],alias['off'],alias['size'])
  d=R.read(rec);sources[name]=hashlib.sha256(d).hexdigest();metadata['read'+suffix]+=1
  if suffix=='.sidi':
   q=buffers(d)[0];ps=N.placements(q,idx)
   if struct.unpack_from('<I',q)[0]!=len(ps):issues.append(dict(sector=sector,issue='placement-count'));continue
   records=[]
   for ordinal,(rid,box,rot)in enumerate(ps):
    h=struct.pack('<I9hHH',rid,*box,rot,0);p=q.find(h)
    assert p>=0 and q.find(h,p+1)<0
    upper=struct.unpack_from('<H',q,p+24)[0];secondary,opaque=struct.unpack_from('<IH',q,p+26)
    count=struct.unpack_from('<I',q,p+32)[0];assert p+36+count*74<=len(q)
    reason=[]
    if upper:reason.append('rotation-upper-word')
    if rid not in idx:reason.append('missing-primary-prefab')
    if secondary and secondary not in idx:reason.append('missing-secondary-resource')
    metadata['rotation'+str(rot)]+=1;metadata['upper'+str(upper)]+=1
    for j in range(count):
     g=struct.unpack_from('<II3f4f6f3IH',q,p+36+j*74)
     pose,quat=T.sector_pose(g[2:5],g[5:9],box[:3],rot,corner,parent_kind='direct-sector')
     records.append((g,pose,quat,dict(layer='SIDI',placement=ordinal,offset=p+36+j*74,prefab=rid,secondary=secondary,opaque=opaque,upper=upper,rotation=rot,pivot=box[:3],unsupported=reason)))
  else:
   if suffix=='.sid':recordsRaw=gos(buffers(d)[3])
   else:
    assert d[:4]==b'SIDG';rr=M.Reader(d,16);M.read_schema(rr);recordsRaw=gos(d[rr.p-4:])
   records=[(g,(corner[0]+g[2],g[3],corner[2]+g[4]),g[5:9],dict(layer='SID'if suffix=='.sid'else'SIDGO',unsupported=[]))for g in recordsRaw]
  for g,pose,quat,extra in records:
   assert sector<=0x7ff and g[1]<=0xfffff
   key=(sector,g[1]);allgroups[key].append(dict(goid=g[0],sector=sector,staticId=g[1],nativeKey=(sector<<20)|g[1],position=pose,quaternion=quat,source=name,sourceRid=declared,**extra))
selected={k:v for k,v in allgroups.items()if any(t['goid']==1415 for t in v)}
conflicts={k:v for k,v in selected.items()if len({(t['goid'],tuple(t['position']),tuple(t['quaternion']))for t in v})>1}
unsupported={k:v for k,v in selected.items()if any(t['unsupported']for t in v)}
safe={k:v[0] for k,v in selected.items()if k not in conflicts and k not in unsupported}
# An unavailable layer can conceal conflicting keys: omit every known key in that sector.
missing={i['sector']for i in issues};safe={k:v for k,v in safe.items()if k[0]not in missing}
legacy={}
for file in ['data/staticObjects_slums_1.csv','data/staticObjects_slums_2.csv']:
 for r in csv.DictReader(open(file)):
  if int.from_bytes(bytes.fromhex(r['type']),'little')!=1415:continue
  k=(int(r['sector_id']),int.from_bytes(bytes.fromhex(r['staticId']),'little'))
  assert k not in legacy;legacy[k]=r
assert len(legacy)==47434 and len(safe)==47365 and not(set(safe)-set(legacy))
resourceHashes={}
for rid in sorted({r[k]for r in safe.values()for k in ['prefab','secondary']if k in r and r[k]}):
 rec=idx[rid];assert rec['flags']==4
 resourceHashes[str(rid)]=hashlib.sha256(R.read(rec)).hexdigest()
header=['metr','sector','static_id','native_id','source_goid','native_x','native_y','native_z','native_quat','legacy_x','legacy_y','legacy_z','legacy_quat','source_rid','placement','record_offset']
out=Path(args.output)
with out.open('w',newline='',encoding='utf-8')as f:
 w=csv.writer(f,lineterminator='\n');w.writerow(header)
 for k,r in sorted(safe.items()):
  old=legacy[k];q=struct.pack('<4f',*r['quaternion']).hex()
  w.writerow([1,*k,r['nativeKey'],1415,*r['position'],q,old['pos_x'],old['pos_y'],old['pos_z'],old['quat'].lower(),r['sourceRid'],r.get('placement',-1),r.get('offset',-1)])
def digest(records):return hashlib.sha256(json.dumps(records,sort_keys=True,separators=(',',':')).encode()).hexdigest()
manifest=dict(format=1,scope='Dormant Slums1415 to freeGO3; no runtime wiring',legacyKeys=len(legacy),acceptedKeys=len(safe),conflictingKeys=len(conflicts),unsupportedKeys=len(unsupported),missingLayerExcludedKeys=len(selected)-len(safe)-len(conflicts)-len(unsupported),unavailableLayers=issues,parentSha256=hashlib.sha256((Path(R.D)/parentPath).read_bytes()).hexdigest(),sourceCount=len(sources),sourceInventorySha256=digest(sources),referencedResourceCount=len(resourceHashes),resourceInventorySha256=digest(resourceHashes),csvSha256=hashlib.sha256(out.read_bytes()).hexdigest(),sourceConvention='Native direct-sector IPI quarterturn/pivot*10, full Hamilton quaternion and float32 stores; NO server+145 in output pose; legacy pose retained solely as provenance guard.')
Path(args.manifest).write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8');print(json.dumps(manifest,indent=2))
