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
# UI quote and compile affordability call the very same function; neither adds one.
for caller in (0x62081a08,0x6208372b):
    assert read(caller,1)==b'\xe8'
    assert caller+5+struct.unpack('<i',read(caller+1,4))[0]==0x62080630
assert read(0x620806b0,23)==bytes.fromhex('d945f4d84df0d80d202f8762d835a8068762e8118c6200')
# _ftol2-style helper corrects FISTP's rounded integer towards zero, not ceil/nearest.
assert read(0x626a931f,22)==bytes.fromhex('d91c248b0c2481c1ffffff7f83d8008b54241483da00')
constants={hex(va):struct.unpack(fmt,read(va,struct.calcsize(fmt)))[0] for va,fmt in
    [(0x6286d8a8,'<f'),(0x62872fa0,'<d'),(0x62870698,'<f'),(0x6286dae4,'<f'),(0x62872f20,'<f')]}
assert constants['0x6286d8a8']==2 and constants['0x62870698']==3 and constants['0x6286dae4']==50
assert constants['0x62872f20']==struct.unpack('<f',struct.pack('<f',.12))[0]
print(json.dumps({'client_sha256':sha,'timer':'6207f650','local_caster_stat_wrapper':'621d5ad0',
    'request60_producer':'620836b0','completion61':'62083820','direct_charge_label':'62081650',
    'cost':'62080630','cost_ui_caller':'62081a08','cost_affordability_caller':'6208372b',
    'cost_integer_conversion':'626a92d8 truncation; no UI-only ceil',
    'precision_boundary':'single-precision arithmetic reproduces observed native9/16440; double promotion underquotes8/16439',
    'constants':constants,'scope':'ordinary IC code; 81 Code Writer excluded',
    'remaining':'UI timer proven; server scheduling and one-instance output are explicit OURS policy; no positive retail60 success/timing capture'},indent=2))
