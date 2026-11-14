"""Read-only, hash-pinned native Compile60 evidence; never executes client.dll."""
import hashlib, json, struct, sys
from pathlib import Path
b=Path(sys.argv[1]).read_bytes()
sha=hashlib.sha256(b).hexdigest()
assert sha=='cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a'
# Raw offsets equal RVA for this exact shipped PE at these addresses.
def read(va,n): return b[va-0x62000000:va-0x62000000+n]
for va,stat in [(0x6207f662,706),(0x6207f67d,954),(0x6207f6f6,955),(0x6208067a,530)]:
    assert read(va,5)==b'\x68'+struct.pack('<I',stat)
assert read(0x6207f6c9,3)==bytes.fromhex('80f949') # category[0]=='I'
assert read(0x6207f6e4,4)==bytes.fromhex('80780143') # category[1]=='C'
assert read(0x6207f697,3)==bytes.fromhex('0fbfc1') # signed16 total706
assert read(0x62080689,4)==bytes.fromhex('0fbf55fc') # signed16 modifier530
constants={hex(va):struct.unpack(fmt,read(va,struct.calcsize(fmt)))[0] for va,fmt in
    [(0x6286d8a8,'<f'),(0x62872fa0,'<d'),(0x62870698,'<f'),(0x6286dae4,'<f'),(0x62872f20,'<f')]}
assert constants['0x6286d8a8']==2 and constants['0x62870698']==3 and constants['0x6286dae4']==50
assert constants['0x62872f20']==struct.unpack('<f',struct.pack('<f',.12))[0]
print(json.dumps({'client_sha256':sha,'timer':'6207f650','local_caster_stat_wrapper':'621d5ad0',
    'request60_producer':'620836b0','completion61':'62083820','direct_charge_label':'62081650',
    'cost':'62080630','constants':constants,'scope':'ordinary IC code; 81 Code Writer excluded',
    'remaining':'UI timer proven; server scheduling and one-instance output are explicit OURS policy; no positive retail60 success/timing capture'},indent=2))
