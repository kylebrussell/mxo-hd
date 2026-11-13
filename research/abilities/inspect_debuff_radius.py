"""Hash-checked read-only evidence for direct UDB0/stat177 radius; no linked75 formula claim."""
import hashlib,json,struct,sys
from pathlib import Path
b=Path(sys.argv[1]).read_bytes()
assert hashlib.sha256(b).hexdigest()=="cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
def read(va,n):return b[va-0x62000000:va-0x62000000+n]
assert read(0x62515f49,7)==bytes.fromhex("c745fc55444230") # exact UDB0
assert read(0x625c555a,5)==bytes.fromhex("e8d109f5ff") # predicate62515f30
assert read(0x625c556c,5)==bytes.fromhex("68b1000000")
assert read(0x625c5578,3)==bytes.fromhex("0fbfd0") # signedAX
assert read(0x625c5587,9)==bytes.fromhex("d835a8068762d95dfc")
assert read(0x625c5590,14)==bytes.fromhex("d945fc5fd805489286625ed84df8")
assert struct.unpack("<f",read(0x628706a8,4))[0]==100
assert read(0x621ed024,6)==bytes.fromhex("8b88b4000000") # local character -> ECX
assert read(0x625c5510,2)==bytes.fromhex("8bf1") # preserve owner ECX in ESI
# Initial materialized-template link path; no runtime-memory or later-writer claim.
assert read(0x6251d522,25)==bytes.fromhex("8d55fc5268d0908c62578bcbc745fc00000000e85672ffff8b")
assert read(0x6231bda4,6)==bytes.fromhex("894e288b45d0") # stored_size -> object+28
assert read(0x6231bdd4,15)==bytes.fromhex("ff157882866283c40489462ceb07c7") # alloc stored bytes -> +2c
assert read(0x622ffd29,21)==bytes.fromhex("8bd1c1e90233c0f3ab8bca83e103f3aa5fb0015e5d") # zero-fill fallback
print(json.dumps({"owner":"UI621ed033 passes local-character [629eee08]+b4 as ECX; template as argument, no target argument", "selector":"StatusCategory exactly UDB0", "stat":177,"formula":"base*(1+binary32(signed16(total177)/100))", "units":"base getter stored metres*100; UI /100; server selectors metres", "initialLinkDefault":{"owner":177,"linked":1055,"coefficientOwner":177,"storedBytes":22,"declaredStorageBytes":422,"scaleOffset":229,"scaleWidth":4,"initialGetterValue":0,"loader":"6231bad0 retains stored_size;622ffce0 zero-fills out-of-range output"}, "unresolved":"75 modifies1055. Threshold62b11ee0, later template/map mutations and complete baseline aggregation remain unresolved. No75 closure or native server landing caller claim."},indent=2))
