"""Read-only disassembly of completed-goal widget creation (client.dll build gated).
Run in an environment with pefile and capstone: python inspect_objective_widget_gate.py CLIENT_DLL
These addresses prove the widget gate; they do not prove when a UI frame is rendered.
"""
import argparse
import hashlib
import capstone
import pefile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("client_dll")
args = parser.parse_args()
with open(args.client_dll, "rb") as source:
    raw = source.read()
assert hashlib.sha256(raw).hexdigest() == "cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
pe = pefile.PE(data=raw)
image = pe.get_memory_mapped_image()
base = pe.OPTIONAL_HEADER.ImageBase
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
for title, address, size in [
    ("Goal ID widget lookup; absent completed rows return NULL", 0x620cfe00, 0x80),
    ("Panel refresh exits before UI initialization", 0x620d04f0, 0x19),
    ("Panel initialization enables refresh", 0x620d08ce, 0x35),
    ("80a0 goal flags and panel refresh", 0x62210ed0, 0x1a0),
    ("Mission Status window rebuilds and sorts completed goals", 0x620d6080, 0x300),
    ("Mission Status goal row copies ID and completion checkbox", 0x620d5bc0, 0x160),
]:
    print(title)
    for instruction in md.disasm(image[address-base:address-base+size], address):
        print(f"{instruction.address:08x} {instruction.mnemonic:8} {instruction.op_str}")
