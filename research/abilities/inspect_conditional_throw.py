"""Read original Python2.3 code objects and stored components; never execute them.
Usage: python inspect_conditional_throw.py <Monolith.ltp> <client-data-tools>
"""
import hashlib,json,sys
from pathlib import Path
sys.path.insert(0,sys.argv[2])
import ltp,py23marshal
found=[]
for name,data in ltp.entries(sys.argv[1]):
 if name!="tempmods.pyc":continue
 assert hashlib.sha256(data).hexdigest()=="968a7ab4525d5b62ed37f3703539da2b2ad66b8a7f1b1ba0cf60041fe0c5f060"
 module=py23marshal.Reader(data,8).obj()
 for code in module.consts:
  if not isinstance(code,py23marshal.Code) or code.name!=b"DealDamageIfOffBalance":continue
  assert code.code.hex()=="7c0100690100690200740300690400690500406f2c00017c01006901006906007c00006908007c00006909007c0000690a00740b007c0000690c00830500016e01000164000053"
  found.append(dict(module=name,sha256=hashlib.sha256(data).hexdigest(),line=code.firstlineno,flags=code.flags,names=[n.decode() for n in code.names],constants=[repr(v) for v in code.consts],bytecode=code.code.hex()))
assert len(found)==1
root=Path(__file__).resolve().parents[2]
catalog=json.loads((root/"data/ability_catalog.json").read_text(encoding="utf8"))
rows=[a for a in catalog["abilities"] if any(v=="DealDamageIfOffBalance" for v in a["fields"].values())]
assert [a["id"] for a in rows]==[279,282]
print(json.dumps(dict(original=found,stored_producers=rows,limit="Separate conditional Throw call proved; full server math and modifier callback order are unavailable. Existing influence/resistance/base policy is not recovered by this inspection."),indent=2))
