"""Read-only native74/radius evidence. Usage: python inspect_resist_damage_radius.py <client.dll>."""
import hashlib,json,struct,sys
from pathlib import Path
b=Path(sys.argv[1]).read_bytes()
sha=hashlib.sha256(b).hexdigest()
assert sha=="cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
# This shipped image's section raw offsets equal RVAs at all asserted addresses.
def read(va,n):return b[va-0x62000000:va-0x62000000+n]
assert read(0x628c8acc,15)==b"StatusCategory\0"
assert read(0x62515f0c,7)==bytes.fromhex("c745fc55424630") # exactUBF0 selector
assert read(0x625c553f,2)==bytes.fromhex("6a7f")
assert read(0x625c5548,3)==bytes.fromhex("0fbfc8") # signedAX
assert read(0x625c5587,9)==bytes.fromhex("d835a8068762d95dfc") # /100 then binary32 store
assert read(0x625c5590,14)==bytes.fromhex("d945fc5fd805489286625ed84df8") # (1+fraction)*base
assert struct.unpack("<f",read(0x628706a8,4))[0]==100
assert struct.unpack("<f",read(0x62869248,4))[0]==1
assert read(0x625191ea,6)==bytes.fromhex("d80da8068762") # stored radius metres->world units
print(json.dumps({"sha256":sha,"caster_radius":"0x625c5500","selector":"Ability/StatusCategory exactly UBF0","stat":127,"formula":"base_radius*(1+binary32(signed_total127/100))","base_units":"stored metres, native getter multiplies100","remaining":"1052 SweepBonus consumer/value formula unresolved; UDB0/stat177 excluded"},indent=2))
