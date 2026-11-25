"""Resolve each vendor_items.csv row to the 7.6005 merchant static the stock client draws at that row's position.

The vendor_static_id column came from older packet captures; most of those ids name doors, benches or nothing in the
shipped sector statics. Each row's x;y;z, however, sits 50 units from a merchant static of the same district. This
script writes that static's id as npc_static_id (decimal, empty when no unique merchant is within MAX_DISTANCE), so
80 c8 <id> 02 00 on the visible merchant opens the row's stock. Run from the repository root:

    python research/world-data/resolve_vendor_npcs.py
"""
import csv
import glob
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAX_DISTANCE = 60.0
MERCHANT = re.compile(r"Merchant|Seller|Vendor|Hacker|Patcher", re.I)

names = {int(m.group(2)): m.group(1) for m in re.finditer(
    r'new \w+\("([^"]*)", (\d+)\)',
    (ROOT / "hds/resources/gameobjects/definitions/GameObjectDefinitions.cs").read_text(encoding="utf-8"))}

statics = []
for path in sorted(glob.glob(str(ROOT / "data/staticObjects_*.csv"))):
    with open(path, encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            goid = int.from_bytes(bytes.fromhex(row["type"])[:2], "little")
            if MERCHANT.search(names.get(goid, "")):
                statics.append((row["metr_id"], int.from_bytes(bytes.fromhex(row["mxoId"]), "little"),
                                (float(row["pos_x"]), float(row["pos_y"]), float(row["pos_z"]))))

path = ROOT / "data/vendor_items.csv"
raw = path.read_bytes()
newline = "\r\n" if b"\r\n" in raw else "\n"
lines = raw.decode("utf-8").splitlines()
out = ["metr_id;vendor_static_id;items;x;y;z;npc_static_id"]
for line in lines[1:]:
    if not line.strip():
        continue
    fields = line.split(";")[:6]
    resolved = ""
    if len(fields) == 6:
        position = tuple(float(value.replace(",", ".")) for value in fields[3:6])
        near = [mxo for metr, mxo, pos in statics if metr == fields[0] and math.dist(position, pos) <= MAX_DISTANCE]
        if len(near) == 1:
            resolved = str(near[0])
    while len(fields) < 6:
        fields.append("")
    out.append(";".join(fields + [resolved]))
path.write_bytes((newline.join(out) + newline).encode("utf-8"))
print(sum(1 for line in out[1:] if not line.endswith(";")), "of", len(out) - 1, "rows resolved")
