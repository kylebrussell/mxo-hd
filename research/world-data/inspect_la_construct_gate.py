"""Read-only bounded LA tree availability gate probe; no proposed server reply.
Requires existing pefile/capstone research dependencies.
"""
import argparse,hashlib,json,sys
p=argparse.ArgumentParser();p.add_argument("--client",required=True);p.add_argument("--python-deps");a=p.parse_args()
if a.python_deps:sys.path.insert(0,a.python_deps)
import pefile,capstone
raw=open(a.client,"rb").read();sha=hashlib.sha256(raw).hexdigest()
assert sha=="cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
pe=pefile.PE(data=raw);d=pe.get_memory_mapped_image();base=pe.OPTIONAL_HEADER.ImageBase
md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_32)
ins={i.address:(i.mnemonic,i.op_str) for i in md.disasm(d[0x620374a0-base:0x62037790-base],0x620374a0)}
expected={0x62037522:("mov","edx, dword ptr [0x629ea21c]"),0x62037528:("mov","ecx, dword ptr [0x629ea220]"),
0x6203755a:("call","0x621c3c40"),0x6203756c:("cmp","byte ptr [esi], 0"),0x6203756f:("je","0x6203777d")}
for addr,value in expected.items():assert ins[addr]==value,(hex(addr),ins.get(addr),value)
print(json.dumps({"ClientSha256":sha,"TreeBuilder":"620374a0","VerifiedInstructions":{hex(k):v for k,v in expected.items()},
"Boundary":"Runtime record byte-zero skips tree insertion. Meaning/update source and80fa effects unresolved; no server packet proposed."},indent=2))
