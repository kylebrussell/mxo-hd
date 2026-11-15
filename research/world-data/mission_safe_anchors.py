"""Safe1410-only companion to the immutable Computer195 reader; no separate compositor."""
import sys,struct,csv,collections,json,hashlib,re
from pathlib import Path
source=Path(__file__).with_name('mission_computer_anchors.py')
code=source.read_text()
# The unused family-selection set is deliberately excluded; Computer1407/1415 filtering below this prefix is independent.
reader=code[:code.index('selected={')]
normalized,count=re.subn(r'anchors=\{[0-9, ]+\};','anchors={};',reader)
assert count==1 and hashlib.sha256(normalized.encode()).hexdigest() == 'efcc4d721c2c41ef29f2dc7afb5a8ff2d1aee5fd7e2e8e4fbb4e077834d2dac4', 'Reviewed source parser changed; re-audit before generation'
ns={'__file__':str(source)}
exec(compile(code[:code.index('selected={')],str(source),'exec'),ns)
args=ns['args'];allgroups=ns['allgroups'];issues=ns['issues']
selected={k:v for k,v in allgroups.items() if any(t['goid']==1410 for t in v)}
conflicts={k:v for k,v in selected.items() if len({(t['goid'],tuple(t['position']),tuple(t['quaternion'])) for t in v})>1}
unsupported={k:v for k,v in selected.items() if any(t['unsupported'] for t in v)}
missing={i['sector'] for i in issues}
safe={k:v[0] for k,v in selected.items() if k not in conflicts and k not in unsupported and k[0] not in missing}
legacy={}
for file in ['data/staticObjects_slums_1.csv','data/staticObjects_slums_2.csv']:
 for r in csv.DictReader(open(file)):
  if int.from_bytes(bytes.fromhex(r['type']),'little')!=1410:continue
  k=(int(r['sector_id']),int.from_bytes(bytes.fromhex(r['staticId']),'little'))
  assert k not in legacy;legacy[k]=r
assert len(legacy)==4654 and len(safe)==4650 and not(set(safe)-set(legacy))
header=['metr','sector','static_id','native_id','source_goid','native_x','native_y','native_z','native_quat','legacy_x','legacy_y','legacy_z','legacy_quat','source_rid','placement','record_offset']
out=Path(args.output)
with out.open('w',newline='',encoding='utf-8') as f:
 w=csv.writer(f,lineterminator='\n');w.writerow(header)
 for k,r in sorted(safe.items()):
  old=legacy[k];w.writerow([1,*k,r['nativeKey'],1410,*r['position'],struct.pack('<4f',*r['quaternion']).hex(),old['pos_x'],old['pos_y'],old['pos_z'],old['quat'].lower(),r['sourceRid'],r['placement'],r['offset']])
manifest=dict(format=1,scope='Slums1410 to freeGO552; unique SpawnType6 pairing inference; source/checkpoint immutable',legacyKeys=len(legacy),acceptedKeys=len(safe),conflictingKeys=len(conflicts),unsupportedKeys=len(unsupported),missingLayerExcludedKeys=len(selected)-len(safe),unavailableLayers=issues,normalizedReaderPrefixSha256=hashlib.sha256(normalized.encode()).hexdigest(),readerFingerprintScope='UTF-8 decoded reader prefix before selected={; exactly one unused numeric anchors={...}; declaration normalized to anchors={}; all parser/compositor bytes retained',sourceHashes=ns['sources'],csvSha256=hashlib.sha256(out.read_bytes()).hexdigest(),sourceConvention='Exact native direct-sector IPI float32; no added145 or asset-pivot correction')
Path(args.manifest).write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps({k:v for k,v in manifest.items() if k!='sourceHashes'},indent=2))
