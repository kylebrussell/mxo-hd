"""Export validated native construct statics. Defaults preserve small03; see WORLD-SMALL03/WORLD-LARGE01."""
import sys,struct,csv,collections,importlib.util,json,math,hashlib,argparse
from pathlib import Path
parser=argparse.ArgumentParser(description='Bounded read-only construct static decoder; strict default rejects unsupported placement rotations.')
parser.add_argument('tools')
parser.add_argument('--world',type=int,choices=(4,5,6,7,8,9,10,11,12,13,14,15,16,17),default=17)
parser.add_argument('--omit-unproven-rotations',action='store_true',help='Exclude EVERY native key touched by a nonzero prefab rotation; never export its variants.')
parser.add_argument('--omit-missing-sectors',action='store_true',help='Explicitly omit the WHOLE sector if its required exterior SID is absent; unknown exterior counts remain unknown.')
parser.add_argument('--omit-missing-sid-sectors',action='store_true',help='Explicitly omit EVERY known key in a sector whose required exterior SID is unavailable; total missing contents remain unknown.')
parser.add_argument('--omit-missing-static-layer-sectors',action='store_true',help='Explicitly omit every known key in a reviewed sector with unavailable SID/SIDI/SIDG data; unseen contents remain unquantified.')
parser.add_argument('--native-prefab-rotations',action='store_true',help='Use primary native top-level IPI transforms; reviewed worlds7/13/15/16 ONLY. Other worlds retain strict omission policy.')
parser.add_argument('--output');parser.add_argument('--audit');args=parser.parse_args()
if args.native_prefab_rotations and (args.world not in (7,13,15,16) or args.omit_unproven_rotations):
 parser.error('--native-prefab-rotations requires a reviewed world and cannot be combined with historical omission mode')
world_name={4:'archive01',5:'archive02',6:'archive_ashencourte',7:'datamine',8:'archive_sakura',9:'archive_sati',10:'widowsmoor',11:'yuki',12:'large01',13:'large02',14:'medium01',15:'medium02',16:'medium03',17:'small03'}[args.world]
args.output=args.output or f'data/staticObjects_{world_name}.csv'
args.audit=args.audit or f'research/world-data/{world_name}-static-audit.json'
sys.path.insert(0,args.tools)
import rezmap_ltb as R
def mod(n):
 s=importlib.util.spec_from_file_location(n,str(Path(__file__).with_name(n+'.py')));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
N=mod('navmesh');M=mod('metr_sectors');bp=R.by_path();idx=R.index();sources={};native_poses={};placement_sources=[]
T=mod('native_prefab_transform') if args.native_prefab_rotations else None
def read(path):
 d=R.read(bp[path]);sources[path]=hashlib.sha256(d).hexdigest();return d
byname={Path(p).name:p for p in bp if p.endswith(('.sid','.sidi','.sidgo'))}
rows=list(csv.DictReader(open('data/sector_properties.csv')))
sector_parents={}
if args.native_prefab_rotations:
 # Only direct METR SCIN -> sector SIDI. No nested/EPI resource traversal or assumed parent graph.
 rel=next(p for ident,_,p in M.WORLDS if ident==args.world);path=Path(R.D)/rel
 sources[rel]=hashlib.sha256(path.read_bytes()).hexdigest()
 sector_parents={v['sector']:v for v in M.read_metr(path)}
 for row in (r for r in rows if r['metr_id']==str(args.world)):
  parent=sector_parents[int(row['sector'])]
  assert parent['name'].lower()==row['file'].lower(), 'Unvalidated direct sector filename'
  assert (float(row['min_x']),float(row['min_z']))==(parent['min'][0],parent['min'][2]), 'Sector parent translation mismatch'

def buffers(d):
 assert d[:4]==b'OSID';p=struct.unpack_from('<I',d,12)[0];out=[]
 for _ in range(10):
  n,o=struct.unpack_from('<II',d,p);assert o==p+8 and o+n<=len(d);out.append(d[o:o+n]);p=o+n
 assert p==len(d);return out
def gos(d):
 p=0;n=struct.unpack_from('<I',d,p)[0];p+=4
 for _ in range(n):
  assert d[p:p+4]==b'ISHD';p+=16+struct.unpack_from('<I',d,p+8)[0]
 n=struct.unpack_from('<I',d,p)[0];p+=4
 assert len(d)==p+n*74,(len(d),p,n)
 return [struct.unpack_from('<II3f4f6f3IH',d,p+i*74) for i in range(n)]
def decode(s):
 out=[];name=s['file'].lower();x0=float(s['min_x']);z0=float(s['min_z'])
 if name+'.sid' in byname:
  d=read(byname[name+'.sid']);b=buffers(d)
  for g in gos(b[3]):out.append((g[1],g[0],(x0+g[2],g[3]+145,z0+g[4]),g[5:9],0,'ext'))
 if name+'.sidgo' in byname:
  d=read(byname[name+'.sidgo']);assert d[:4]==b'SIDG';r=M.Reader(d,16);M.read_schema(r)
  for g in gos(d[r.p-4:]):out.append((g[1],g[0],(x0+g[2],g[3]+145,z0+g[4]),g[5:9],0,'designer'))
 if name+'.sidi' in byname:
  d=read(byname[name+'.sidi']);b=buffers(d);q=b[0]
  ps=N.placements(q,idx);assert args.omit_unproven_rotations or args.native_prefab_rotations or all(rot==0 for _,_,rot in ps),'Unproven placement rotations: strict default refuses export';count=struct.unpack_from('<I',q)[0];assert count==len(ps),(name,count,len(ps))
  positions=[]
  for rid,box,rot in ps:
   h=struct.pack('<I9hHH',rid,*box,rot,0);p=q.find(h);assert p>=0 and q.find(h,p+1)<0;assert not positions or p>positions[-1];positions.append(p)
  for j,((rid,box,rot),p) in enumerate(zip(ps,positions)):
   count=struct.unpack_from('<I',q,p+32)[0];end=p+36+count*74
   assert end <= (positions[j+1] if j+1<len(ps) else len(q)),(name,p,count)
   for i in range(count):
    g=struct.unpack_from('<II3f4f6f3IH',q,p+36+i*74);x,y,z=g[2:5];x,z=[(x,z),(z,-x),(-x,-z),(-z,x)][rot]
    out.append((g[1],g[0],(x0+box[0]*10+x,y+box[1]*10+145,z0+box[2]*10+z),g[5:9],rot,'int'))
    if args.native_prefab_rotations:
     assert rid in idx, ('Missing native IPI resource',hex(rid))
     native_pos,native_q=T.sector_pose(g[2:5],g[5:9],box[:3],rot,(x0,0,z0),parent_kind='direct-sector')
     # Raw diagnostics above remain historically byte-identical; +145 is SERVER presentation, not native pivot math.
     native_poses[(int(s['sector']),*out[-1])]=(int(s['sector']),g[1],g[0],(native_pos[0],native_pos[1]+145,native_pos[2]),native_q,rot,'int')
   if args.native_prefab_rotations:
    rec=idx[rid];data=R.read(rec);digest=hashlib.sha256(data).hexdigest();sources['RID:'+format(rid,'08x')]=digest
    evidence=dict(sector=int(s['sector']),source=byname[name+'.sidi'],rid=rid,pivot=list(box[:3]),rotation=rot,parent='direct-sector',prefab=rec.get('path',''),resourcePack=rec['pack'],resourceOffset=rec['off'],resourceSize=rec['size'],resourceSha256=digest)
    if args.world==13:
     #6247608a copies this second RID to IPI+dc; the following optional u16 goes to+9c.
     # Neither enters6246b0c0's pivot/rotation matrix. Preserve opaque metadata, never treat it as another parent.
     secondary,metadata=struct.unpack_from('<IH',q,p+26)
     assert secondary in idx, ('Missing secondary native IPI resource',hex(secondary))
     second=idx[secondary];second_data=R.read(second);second_sha=hashlib.sha256(second_data).hexdigest();sources['RID:'+format(secondary,'08x')]=second_sha
     evidence.update(rotationUpperWord=struct.unpack_from('<H',q,p+24)[0],resourceIndexFlags=rec['flags'],resourceType=rec['type'],secondaryRid=secondary,opaqueMetadataU16=metadata,
      secondaryResourceIndexFlags=second['flags'],secondaryResourceType=second['type'],secondaryResourcePath=second.get('path',''),
      secondaryResourcePack=second['pack'],secondaryResourceOffset=second['off'],secondaryResourceSize=second['size'],secondaryResourceSha256=second_sha)
    if args.world==7:
     upper=struct.unpack_from('<H',q,p+24)[0];secondary,opaque=struct.unpack_from('<IH',q,p+26)
     assert upper==0 and rec['flags']==4 and rec['type']==6400
     assert secondary in idx, ('Missing secondary IPI resource',hex(secondary))
     extra=idx[secondary];extra_data=R.read(extra);extra_digest=hashlib.sha256(extra_data).hexdigest()
     assert extra['flags']==4 and extra['type']==12544 and len(extra_data)==extra['size']
     sources['RID:'+format(secondary,'08x')]=extra_digest
     evidence.update(rotationUpperWord=upper,resourceIndexFlags=rec['flags'],resourceType=rec['type'],secondaryRid=secondary,opaqueMetadataU16=opaque,secondaryResourceIndexFlags=extra['flags'],secondaryResourceType=extra['type'],secondaryResourcePath=extra.get('path',''),secondaryResourcePack=extra['pack'],secondaryResourceOffset=extra['off'],secondaryResourceSize=extra['size'],secondaryResourceSha256=extra_digest)
    placement_sources.append(evidence)
 return out

missing=[]
missing_evidence=[]
missing_sectors=set()
# Retain each reviewed world-specific refusal/omission contract and its historical audit shape.
if args.world==7:
 missing=[]
 selected=[r for r in rows if r['metr_id']==str(args.world)]
 for row in selected:
  if row['file'].lower()+'.sid' not in byname:
   assert args.world==7 and row['sector']=='34' and row['file']=='dataminene_3','Unreviewed missing static sector'
   metr_path='resource/worlds/final_world/constructs/archive/archive_datamine/datamine.metr'
   d=(Path(R.D)/metr_path).read_bytes();sources[metr_path]=hashlib.sha256(d).hexdigest();q=d.find(b'SINS')+12;rid=None
   for _ in range(struct.unpack_from('<I',d,q-4)[0]):
    end=q+12+struct.unpack_from('<I',d,q+4)[0];r=M.Reader(d,q+12);r.s16();sector=r.u16();r.p+=48
    if sector==34:
     rid=struct.unpack_from('<I',d,r.p+4)[0]
     assert struct.unpack_from('<I',d,r.p+8)[0]==bp[byname['dataminene_3.sidi']]['rid']
    q=end
   assert rid==0x04000800 and rid not in idx
   path=metr_path.rsplit('/',1)[0]+'/'+row['file']+'.sid';assert path not in bp and not (Path(R.D)/path).exists()
   missing.append(dict(sector=34,file=row['file'],missingPath=path,declaredSidRid=rid,packedAbsent=True,looseAbsent=True,unknownExteriorCount=None,policy='Whole sector omitted, including available companions; unknown exterior object count.'))
   for ext in ('.sidi','.sidgo'):read(byname[row['file']+ext])
 assert args.omit_missing_sectors or not missing,'Missing required SID: strict default refuses export; explicit --omit-missing-sectors required'
 raw=[]
 for row in selected:
  if int(row['sector']) not in {m['sector'] for m in missing}:raw.extend((int(row['sector']),*o) for o in decode(row))
else:
 selected=[r for r in rows if r['metr_id']==str(args.world)]
 missing_layers=[(r,label,ext) for r in selected for label,ext in [('SID','.sid'),('SIDI','.sidi'),('SIDG','.sidgo')] if r['file'].lower()+ext not in byname]
 from static_layer_policy import require_explicit_omission
 require_explicit_omission([label for _,label,_ in missing_layers],args.omit_missing_sid_sectors,args.omit_missing_static_layer_sectors)
 missing_sectors={int(r['sector']) for r,_,_ in missing_layers}
 missing_pairs={(int(r['sector']),label) for r,label,_ in missing_layers}
 missing_evidence=[]
 if missing_layers:
  assert (args.world,missing_pairs) in [(5,{(108,'SID')}),(11,{(5,'SIDI')})], 'Unreviewed missing static layer sector'
  metr_rel=next(path for world,_,path in M.WORLDS if world==args.world)
  metr_path=Path(R.D)/metr_rel
  references={r['sector']:r for r in M.read_metr(str(metr_path),include_resource_refs=True)}
  for row in selected:
   native=references[int(row['sector'])]
   for label,ext in [('SID','.sid'),('SIDI','.sidi'),('SIDG','.sidgo'),('NAV','.nav')]:
    path=native['path'].replace(chr(92),'/')+ext
    rid=native['resource_refs'][label]
    if (int(row['sector']),label) in missing_pairs:
     assert rid=={(5,108,'SID'):0x0c00b400,(11,5,'SIDI'):0x0c00a800}[(args.world,int(row['sector']),label)] and rid not in idx and path not in bp and not (Path(R.D)/path).exists()
     missing_evidence.append(dict(sector=int(row['sector']),**({'kind':label} if args.world==11 else {}),path=path,declaredResourceId=rid,packedRidPresent=False,packedPathPresent=False,loosePathPresent=False))
    else:
     assert path in bp and bp[path]['rid']==rid,(row['sector'],label,path,rid)
 raw=[]
 for row in selected: raw.extend((int(row['sector']),*o) for o in decode(row))
groups=collections.defaultdict(list)
for o in raw:groups[o[:2]].append(o)
conflicts={k:v for k,v in groups.items() if len({(z[2],z[3],z[4],z[5]) for z in v})>1}
unproven={k:v for k,v in groups.items() if any(z[-2]!=0 for z in v)}
incomplete={k:v for k,v in groups.items() if k[0] in missing_sectors}
excluded=(set(conflicts) if args.native_prefab_rotations else set(conflicts)|set(unproven))|set(incomplete)
objects=[v[0] for k,v in sorted(groups.items()) if k not in excluded]
if args.native_prefab_rotations:objects=[native_poses.get(o,o) for o in objects]
if args.world==10:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(70,70,0,0,70)
 assert len(sources)==258
elif args.world==14:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(39,39,0,0,39)
elif args.world==9:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(8177,7987,5381,70,2599)
elif args.world==6:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(223,223,0,0,223)
elif args.world==8:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(177,177,0,0,177)
elif args.world==4:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(156,156,0,0,156)
elif args.world==17:
 assert set(conflicts)=={(13,13),(13,15),(13,16)}
 assert len(raw)==370 and len(objects)==358
elif args.world==7:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(13219,12960,6821,175,12785 if args.native_prefab_rotations else 6105)
elif args.world==16:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(4762,4625,3321,134,4491 if args.native_prefab_rotations else 1282)
elif args.world==12:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(1025,1001,46,22,953)
elif args.world==13:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(2164,2096,442,44,2052 if args.native_prefab_rotations else 1619)
elif args.world==5:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(incomplete),len(objects))==(80,80,0,0,3,77)
 assert len(sources)==326
elif args.world==15:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(1061,1023,584,26,997 if args.native_prefab_rotations else 426)
elif args.world==11:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(incomplete),len(objects))==(272,272,0,0,26,246)
 assert len(sources)==146
with open(args.output,'w',newline='') as f:
 w=csv.writer(f);w.writerow(['metr_id','sector_id','mxoId','staticId','type','exterior','pos_x','pos_y','pos_z','rotation','quat'])
 for sector,ident,go,pos,quat,rot,kind in objects:
  le=lambda n:struct.pack('<I',n).hex()
  x,y,z,wq=quat;yaw=math.degrees(math.atan2(2*(wq*y+x*z),1-2*(y*y+z*z)))
  w.writerow([args.world,sector,le((sector<<20)|ident),le(ident),le(go),kind!='int',*pos,yaw,struct.pack('<4f',*quat).hex()])
audit=dict(world=args.world,rawReferences=len(raw),uniqueKeys=len(groups),exportedKeys=len(objects),excludedKeys=[list(k) for k in sorted(excluded)],
 convention='METR sector corner + native local position; +145Y retains existing server CSV convention, not a native pivot claim. Only placement rotation0 supported.',
 sourceSha256=sources,raw=raw,conflicts={str(k):v for k,v in conflicts.items()})
if missing:audit.update(missingSectors=missing,knownInputScope='Available resources only; missing whole sector excluded; no total-world completeness claim.')
if args.native_prefab_rotations:
 audit.update(convention='Native direct-sector IPI pivot*10 + exact quarterturn, parent*local quaternion; float32 native stores. Server +145Y remains separate. Raw retains historical diagnostic convention.',
  nativePrefabRotations=dict(reviewedWorld=args.world,algorithm='client.dll6246b0c0/62111670/62046a40',parentEligibility='Direct METR SCIN sector SIDI only; constructor resets EPI parent+30, sector parser sets SID parent+34. Unknown/nested/EPI refused.',floatStores='signedi16*10; composed translation; rotated point; translated point; each final quaternion component. No normalization.',placements=placement_sources),
  historicallyUnprovenRotationKeys=[list(k) for k in sorted(unproven)],
  omissionPolicy='Every conflicting native key variant remains excluded, even if transformed poses coincide. Unsupported actual nested/EPI parents, invalid flags, and missing resources refuse export; blank resource aliases are recorded, not guessed. No first/last variant policy.')

if args.omit_unproven_rotations:
 audit.update(unprovenRotationKeys=[list(k) for k in sorted(unproven)],
  rotationAmbiguityOverlap=len(set(unproven)&set(conflicts)),
  omissionPolicy='Every variant of any nonzero-placement or conflicting native key is excluded. Raw nonzero transformed positions are UNPROVEN diagnostics, not exported placements.')
if args.world==13:
 nav_names={Path(p).stem for p in bp if p.endswith('.nav')}
 missing=[dict(sector=int(r['sector']),file=r['file'],enableNavigationMesh=r['enable_navigation_mesh'])
  for r in rows if r['metr_id']=='13' and r['file'].lower() not in nav_names]
 assert missing==[dict(sector=47,file='large02_137',enableNavigationMesh='1')]
 audit.update(missingNavigationResources=missing,
  navigationBoundary='225 available sectors; sector47 declares EnableNavigationMesh1 but has no packed nav resource. Unavailable, not proven NoNav; no fabricated polygon.',
  capturedPopulationBoundary='No ambient map13 captured rows. One Special Agent in Encounter.csv remains encounter-scoped, not made ambient.')
if missing_evidence:
 audit.update(rawReferencesAreComplete=False,unquantifiedMissingStaticData=True,missingStaticLayers=missing_evidence,
  knownIncompleteSectorKeys=[list(k) for k in sorted(incomplete)],metrSha256=hashlib.sha256(metr_path.read_bytes()).hexdigest(),
  resourceIndexSha256=hashlib.sha256((Path(R.D)/'rezmap.ltb').read_bytes()).hexdigest(),
  omissionPolicy='Every known variant in missing-SID sector108 is omitted; its unseen exterior contents are unquantified. Raw references cover available layers only, not a complete world.',
  capturedPopulationBoundary='No ambient Archive02 captures; two Encounter rows are outside its footprint and remain encounter-scoped.')
if args.world==11:
 usables=list(csv.DictReader(open('data/object_usables.csv')));states=list(csv.DictReader(open('data/object_states.csv')))
 goids=collections.Counter(r[2] for r in raw)
 audit.update(availableNavigationSectors=49,knownAvailablePrefabPlacements=0,navigationCompletenessProven=False,
  omissionPolicy='Every known variant in missing-SIDI sector5 is omitted; its unseen interior contents and prefab extent are unquantified. Raw references cover available layers only.',
  capturedPopulationBoundary='179 Instance map11 rows are all outside the native 2D world footprint; no relocation, NPC visibility or gameplay claim.',
  nativeGoidContracts={str(goid):dict(placements=count,usables=[u for u in usables if int(u["goid"])==goid],states=[u for u in states if int(u["goid"])==goid]) for goid,count in sorted(goids.items())})
if args.world==15:
 audit.update(capturedPopulationValidated=False,
  capturedPopulationBoundary='54 Instance rows,23 inside METR footprint/31 outside; no grounded NPC validated. Captures unchanged, no NPC gameplay/visibility claim.')
if args.world==10:
 usables=list(csv.DictReader(open('data/object_usables.csv')))
 states=list(csv.DictReader(open('data/object_states.csv')))
 goids=collections.Counter(r[2] for r in raw)
 audit.update(availableSectors=86,availableNavigationSectors=86,prefabPlacements=0,
  nativeGoidContracts={str(goid):dict(placements=count,usables=[u for u in usables if int(u['goid'])==goid],states=[u for u in states if int(u['goid'])==goid]) for goid,count in sorted(goids.items())},
  populationBoundary='17 captured Instance rows:16 inside 2D sector footprint and1 outside; all17 fail exact recorded-coordinate NAV lookup. Preserve captures without relocation or NPC visibility/gameplay claim.',
  transportBoundary='Native source returners38271/38272/38273 are preserved. Existing dynamic arrival returners and origin return rules remain unchanged; source-static returner usability is a separate unresolved gap.')
Path(args.audit).write_text(json.dumps(audit,indent=2)+'\n')
print(f'{len(objects)} statics; {len(excluded)} excluded keys ({len(conflicts)} ambiguous); primary native top-level rotations; {len(raw)} historical raw references retained' if args.native_prefab_rotations else f'{len(objects)} statics; {len(excluded)} excluded keys ({len(conflicts)} ambiguous, {len(unproven)} unproven-rotation); {len(raw)} raw references retained')
