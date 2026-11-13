"""Read-only fixed native80b9 inline class/layout proof. Usage: python inspect_ability_use_record.py client.dll"""
import hashlib,json,struct,sys
from pathlib import Path
b=Path(sys.argv[1]).read_bytes(); sha=hashlib.sha256(b).hexdigest()
assert sha=="cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
def read(va,n):return b[va-0x62000000:va-0x62000000+n]
assert struct.unpack("<5I",read(0x6288c1e4,20))==(0x621d33b0,0x6201c3a0,0x621d7a70,0x621d33e0,0x621d33a0)
assert read(0x621e5112,2)==bytes.fromhex("6a2b")
assert read(0x621d343d,4)==bytes.fromhex("6a006a2b")
assert read(0x621cf659,5)==bytes.fromhex("66c70080b9")
labels={2:(0x62880e64,"Id:"),4:(0x62881a7c,"Target:"),8:(0x6288c3d0,"ObjectTargetCreate:"),12:(0x6288c3b8,"TargetGroundLocation:"),36:(0x6288c3a0,"TargetInventorySlot:"),37:(0x62872fe4,"InvSlotOfToolUsed:"),38:(0x6288c394,"FromPill:"),39:(0x6288c37c,"TargetMiniTeamMemberID:")}
for _,(va,label) in labels.items():assert read(va,len(label)+1)==label.encode()+b"\0"
# Producer's last ground-word store, independent inventory-byte overwrite, and explicit flag copy.
assert read(0x621e5246,6)==bytes.fromhex("8b5214895014")
assert read(0x621e5255,3)==bytes.fromhex("884124")
assert read(0x621e5138,3)==bytes.fromhex("885026")
print(json.dumps({"ClientDllSha256":sha,"RpcBytes":43,"BodyBytes":41,"Vtable":"6288c1e4","Printer":"621d7a70","Serializer":"621d33e0","PacketLabels":{str(k):v[1] for k,v in labels.items()},"GroundBytes":"24 raw immutable bytes, no coordinate/finite validation","Semantics":"Printer names are not universal activation or field-purpose proof"},indent=2))
