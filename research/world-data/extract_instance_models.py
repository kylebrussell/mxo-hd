"""Select private dungeon appearances from the read-only client NPC appearance export."""
import argparse
import csv
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('appearances', type=Path)
parser.add_argument('--output', type=Path, default=Path('research/world-data/npc_instance_models.csv'))
args = parser.parse_args()
rows = json.loads(args.appearances.read_text())
models = sorted({(r['rsi_wire'].lower(), r['path']) for r in rows if '/dungeons/' in r['path'].lower()})
with args.output.open('w', newline='', encoding='utf-8') as stream:
    writer = csv.writer(stream, lineterminator='\n')
    writer.writerow(['rsi_wire', 'client_resource'])
    writer.writerows(models)
print(f'{len(models)} private interior models')
