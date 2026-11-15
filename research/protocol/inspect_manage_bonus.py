"""Read-only source verification for SERVER_MANAGE_BONUS, never launches the client.
Requires pefile/capstone (the existing client-research Python environment is sufficient).
"""
import argparse, hashlib, json, sys
p=argparse.ArgumentParser()
p.add_argument("--client",required=True)
p.add_argument("--python-deps")
a=p.parse_args()
if a.python_deps: sys.path.insert(0,a.python_deps)
import pefile,capstone
raw=open(a.client,"rb").read()
sha=hashlib.sha256(raw).hexdigest()
assert sha=="cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a",sha
pe=pefile.PE(data=raw); data=pe.get_memory_mapped_image();base=pe.OPTIONAL_HEADER.ImageBase
md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_32)
ctor=list(md.disasm(data[0x6213b94d-base:0x6213b999-base],0x6213b94d))
assert any(i.mnemonic=="push" and i.op_str=="0x16" for i in ctor)
fields=[("Type",2,1),("Locator",3,4),("SourceID",7,4),("TargetID",11,2),("Value",13,4),("AddRemoveType",17,1),("Duration",18,4)]
for _,offset,width in fields:
 spelling={1:"byte",2:"word",4:"dword"}[width]+" ptr"
 assert any(i.mnemonic=="mov" and i.op_str.startswith(spelling) and
            ("+ "+hex(offset)+"]" in i.op_str if offset>=10 else "+ "+str(offset)+"]" in i.op_str)
            for i in ctor),(offset,width)
assert any(i.op_str=="word ptr [eax], 0xbc80" for i in ctor)
labels=[("Type:",0x62876708),("Locator:",0x62880f24),("SourceID:",0x62880f18),("TargetID:",0x6288068c),("Value:",0x6287b438),("AddRemoveType:",0x62880f08),("Duration:",0x62880efc)]
printer=list(md.disasm(data[0x62151b00-base:0x62151d65-base],0x62151b00))
for label,address in labels:
 start=address-base;actual=data[start:data.index(b"\0",start)].decode("ascii")
 assert label in actual,(label,actual)
 assert any(i.mnemonic=="push" and i.op_str==hex(address) for i in printer),label
print(json.dumps({"ClientSha256":sha,"Constructor":"6213b94d","Printer":"62151b00","RpcBytes":22,
                 "Fields":[{"Name":n,"RpcOffset":o,"Bytes":w} for n,o,w in fields]},indent=2))
