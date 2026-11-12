"""Read-only A1 evidence/inventory. Never loads or executes client.dll.
Usage: python inspect_typed_resistance.py <client.dll> <Monolith.ltp> <client-data-tools>
Run from repository root; redirect generated output into ignored artifacts or owned Temp.
"""
import csv
import hashlib
import json
import struct
import sys
from pathlib import Path

raw=Path(sys.argv[1]).read_bytes()
sha=hashlib.sha256(raw).hexdigest()
assert sha=="cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
pe=struct.unpack_from("<I",raw,0x3c)[0]
count=struct.unpack_from("<H",raw,pe+6)[0]
opt=struct.unpack_from("<H",raw,pe+20)[0]
base=struct.unpack_from("<I",raw,pe+52)[0]
sections=[struct.unpack_from("<IIII",raw,pe+24+opt+40*i+8) for i in range(count)]
def read(va,n):
 for length,start,size,offset in sections:
  if start<=va-base and va-base+n<=start+size:
   return raw[offset+va-base-start:offset+va-base-start+n]
 raise ValueError(hex(va))
slices={hex(a):read(a,n).hex() for a,n in [(0x6258d830,76),(0x6258db50,116),(0x625ec512,120)]}
assert read(0x6258d83c,4)==bytes.fromhex("0fb6550c")
assert read(0x6258d848,5)==bytes.fromhex("68b0838d62")
# Bounded relative call/jump byte candidates in shipped executable file-backed text.
calls=[]
for length,start,size,offset in sections:
 if start!=0x1000:continue
 text=raw[offset:offset+size]
 for i in range(len(text)-5):
  if text[i] in (0xe8,0xe9) and base+start+i+5+struct.unpack_from("<i",text,i+1)[0]==0x6258d830:
   calls.append(hex(base+start+i))
sys.path.insert(0,sys.argv[3])
import ltp,py23marshal
functions={}
for name,pyc in ltp.entries(sys.argv[2]):
 if name.lower() not in ("combat_calculations.pyc","tempmods.pyc"):continue
 module=py23marshal.Reader(pyc,8).obj()
 selected={}
 for c in module.consts:
  if isinstance(c,py23marshal.Code) and c.name in (b"GetDamageReductionValue",b"PhysicalDOT",b"RangedDOT",b"ViralDOT",b"ThrownDOT"):
   selected[c.name.decode()]={"flags":c.flags,"names":[n.decode() for n in c.names],"constants":[repr(v) for v in c.consts],"bytecode":c.code.hex()}
 functions[name]={"pyc_sha256":hashlib.sha256(pyc).hexdigest(),"functions":selected}
players={int(r["id"]) for r in csv.DictReader(Path("docs/ABILITY-PARITY.csv").open(encoding="utf-8-sig"))}
catalog=json.loads(Path("data/ability_catalog.json").read_text(encoding="utf-8"))
contributors=[]
for ability in catalog["abilities"]:
 if ability["id"] not in players:continue
 for field,value in ability["fields"].items():
  if field.endswith("/TargetAttribute") and isinstance(value,int) and ((value&0x7fffffff)>>10) in (144,180,181,157,1033,1140,1144):
   contributors.append({"id":ability["id"],"name":ability["name"],"component":field.split("/")[0],"stat":(value&0x7fffffff)>>10})
print(json.dumps({"client_sha256":sha,"native_slices":slices,"relative_call_jump_candidates_to6258d830":calls,
 "dealAbilityDamage_literal_present":b"dealAbilityDamage" in raw,"original_pyc":functions,
 "potential_contributor_rows":len(set(r["id"] for r in contributors)),"potential_modifier_count":len(contributors),
 "contributors":contributors,"limits":"Direct byte candidates are not proof against indirect/server-only callers. Typed health/DOT ratio application is OURS; no complete baseline or retail amount recovered."},indent=2))
