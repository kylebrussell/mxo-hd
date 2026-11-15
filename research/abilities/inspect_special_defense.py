"""Read-only reproduction of client abilitylib.getDefenseTacticBonusType's native jump table.
Usage: python research/abilities/inspect_special_defense.py <client.dll>
No client process is opened or launched. Requires only Python's standard library.
"""
import hashlib
import json
import struct
import sys
from pathlib import Path

blob = Path(sys.argv[1]).read_bytes()
sha = hashlib.sha256(blob).hexdigest()
assert sha == "cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a", sha
u16 = lambda p: struct.unpack_from("<H", blob, p)[0]
u32 = lambda p: struct.unpack_from("<I", blob, p)[0]
pe = u32(0x3c)
assert blob[pe:pe+4] == b"PE\0\0"
optional = pe + 24
image_base = u32(optional + 28)
sections = optional + u16(pe + 20)
def read(va, size):
    rva = va - image_base
    for i in range(u16(pe + 6)):
        section = sections + 40*i
        length, start, raw_size, offset = struct.unpack_from("<IIII", blob, section+8)
        if start <= rva and rva+size <= start+raw_size:
            return blob[offset+rva-start:offset+rva-start+size]
    raise ValueError(hex(va))
assert read(0x628c8a5c, 13) == b"TestFunction\0"
assert read(0x62516a66, 4) == bytes.fromhex("807dff06")  # isEscapeAbility: TestFunction == 6
assert read(0x62520036, 9) == bytes.fromhex("83f8060f8793000000")
assert read(0x6252003f, 7) == bytes.fromhex("ff248558015262")
rows=[]
for test in range(7):
    target=struct.unpack("<I", read(0x62520158+4*test,4))[0]
    if target == 0x625200d2:
        rows.append({"TestFunction":test,"stat":None,"branch":hex(target),"behaviour":"diagnostic/failure"})
    else:
        code=read(target,9)
        assert code[:5] == bytes.fromhex("8b750cc706"), code.hex()
        stat=struct.unpack("<I",code[5:])[0]
        rows.append({"TestFunction":test,"stat":None if stat==4096 else stat,"branch":hex(target)})
print(json.dumps({"client_sha256":sha,"table":"0x62520158","rows":rows},indent=2))
