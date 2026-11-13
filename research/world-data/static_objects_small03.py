"""Export validated native construct statics. Defaults preserve small03; see WORLD-SMALL03/WORLD-LARGE01."""
import sys,struct,csv,collections,importlib.util,json,math,hashlib,argparse
from pathlib import Path
parser=argparse.ArgumentParser(description='Bounded read-only construct static decoder; strict default rejects unsupported placement rotations.')
parser.add_argument('tools')
parser.add_argument('--world',type=int,choices=(4,9,12,13,14,15,16,17),default=17)
parser.add_argument('--omit-unproven-rotations',action='store_true',help='Exclude EVERY native key touched by a nonzero prefab rotation; never export its variants.')
parser.add_argument('--output');parser.add_argument('--audit');args=parser.parse_args()
world_name={4:'archive01',9:'archive_sati',12:'large01',13:'large02',14:'medium01',15:'medium02',16:'medium03',17:'small03'}[args.world]
args.output=args.output or f'data/staticObjects_{world_name}.csv'
args.audit=args.audit or f'research/world-data/{world_name}-static-audit.json'
sys.path.insert(0,args.tools)
import rezmap_ltb as R
def mod(n):
 s=importlib.util.spec_from_file_location(n,str(Path(__file__).with_name(n+'.py')));m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
N=mod('navmesh');M=mod('metr_sectors');bp=R.by_path();idx=R.index();sources={}
def read(path):
 d=R.read(bp[path]);sources[path]=hashlib.sha256(d).hexdigest();return d
byname={Path(p).name:p for p in bp if p.endswith(('.sid','.sidi','.sidgo'))}
rows=list(csv.DictReader(open('data/sector_properties.csv')))
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
 d=read(byname[name+'.sid']);b=buffers(d)
 for g in gos(b[3]):out.append((g[1],g[0],(x0+g[2],g[3]+145,z0+g[4]),g[5:9],0,'ext'))
 if name+'.sidgo' in byname:
  d=read(byname[name+'.sidgo']);assert d[:4]==b'SIDG';r=M.Reader(d,16);M.read_schema(r)
  for g in gos(d[r.p-4:]):out.append((g[1],g[0],(x0+g[2],g[3]+145,z0+g[4]),g[5:9],0,'designer'))
 if name+'.sidi' in byname:
  d=read(byname[name+'.sidi']);b=buffers(d);q=b[0]
  ps=N.placements(q,idx);assert args.omit_unproven_rotations or all(rot==0 for _,_,rot in ps),'Unproven placement rotations: strict default refuses export';count=struct.unpack_from('<I',q)[0];assert count==len(ps),(name,count,len(ps))
  positions=[]
  for rid,box,rot in ps:
   h=struct.pack('<I9hHH',rid,*box,rot,0);p=q.find(h);assert p>=0 and q.find(h,p+1)<0;assert not positions or p>positions[-1];positions.append(p)
  for j,((rid,box,rot),p) in enumerate(zip(ps,positions)):
   count=struct.unpack_from('<I',q,p+32)[0];end=p+36+count*74
   assert end <= (positions[j+1] if j+1<len(ps) else len(q)),(name,p,count)
   for i in range(count):
    g=struct.unpack_from('<II3f4f6f3IH',q,p+36+i*74);x,y,z=g[2:5];x,z=[(x,z),(z,-x),(-x,-z),(-z,x)][rot]
    out.append((g[1],g[0],(x0+box[0]*10+x,y+box[1]*10+145,z0+box[2]*10+z),g[5:9],rot,'int'))
 return out

raw=[]
for row in rows:
 if row['metr_id']==str(args.world): raw.extend((int(row['sector']),*o) for o in decode(row))
groups=collections.defaultdict(list)
for o in raw:groups[o[:2]].append(o)
conflicts={k:v for k,v in groups.items() if len({(z[2],z[3],z[4],z[5]) for z in v})>1}
unproven={k:v for k,v in groups.items() if any(z[-2]!=0 for z in v)}
excluded=set(conflicts)|set(unproven)
objects=[v[0] for k,v in sorted(groups.items()) if k not in excluded]
if args.world==14:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(39,39,0,0,39)
elif args.world==9:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(8177,7987,5381,70,2599)
elif args.world==4:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(156,156,0,0,156)
elif args.world==17:
 assert set(conflicts)=={(13,13),(13,15),(13,16)}
 assert len(raw)==370 and len(objects)==358
elif args.world==16:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(4762,4625,3321,134,1282)
elif args.world==12:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(1025,1001,46,22,953)
elif args.world==13:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(2164,2096,442,44,1619)
else:
 assert (len(raw),len(groups),len(unproven),len(conflicts),len(objects))==(1061,1023,584,26,426)
with open(args.output,'w',newline='') as f:
 w=csv.writer(f);w.writerow(['metr_id','sector_id','mxoId','staticId','type','exterior','pos_x','pos_y','pos_z','rotation','quat'])
 for sector,ident,go,pos,quat,rot,kind in objects:
  le=lambda n:struct.pack('<I',n).hex()
  x,y,z,wq=quat;yaw=math.degrees(math.atan2(2*(wq*y+x*z),1-2*(y*y+z*z)))
  w.writerow([args.world,sector,le((sector<<20)|ident),le(ident),le(go),kind!='int',*pos,yaw,struct.pack('<4f',*quat).hex()])
audit=dict(world=args.world,rawReferences=len(raw),uniqueKeys=len(groups),exportedKeys=len(objects),excludedKeys=[list(k) for k in sorted(excluded)],
 convention='METR sector corner + native local position; +145Y retains existing server CSV convention, not a native pivot claim. Only placement rotation0 supported.',
 sourceSha256=sources,raw=raw,conflicts={str(k):v for k,v in conflicts.items()})
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
if args.world==15:
 audit.update(capturedPopulationValidated=False,
  capturedPopulationBoundary='54 Instance rows,23 inside METR footprint/31 outside; no grounded NPC validated. Captures unchanged, no NPC gameplay/visibility claim.')
Path(args.audit).write_text(json.dumps(audit,indent=2)+'\n')
print(f'{len(objects)} statics; {len(excluded)} excluded keys ({len(conflicts)} ambiguous, {len(unproven)} unproven-rotation); {len(raw)} raw references retained')
