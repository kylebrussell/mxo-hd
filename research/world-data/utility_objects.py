"""Export stored data-node and luggable properties, read-only, from the 7.6005 GOB.

Usage: python research/world-data/utility_objects.py --client C:/.../MxO
Coordinates are deliberately not invented: the three district cooked_cod files have
no DataNode/Luggable placements. See docs/DATA-MINING.md.
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'abilities'))
from extract_catalog import extract

args = argparse.ArgumentParser(description=__doc__)
args.add_argument('--client', type=Path, required=True)
args.add_argument('--output', type=Path, default=Path('data/utility_objects.json'))
opts = args.parse_args()
catalog = extract(opts.client, raw_goids=True)
rows = []
for row in catalog['abilities']:
    f = row['fields']
    if row['id'] == 606 or 'Luggable/Size' in f:
        rows.append(dict(goid=row['id'], name=row['name'],
                         pickup_use=next((i for i, c in enumerate(row['components']) if c['name'] == 'Usable'), -1),
                         fields={k: v for k, v in f.items() if k.startswith((
                             'Luggable/', 'CharStatsPrerequisite/', 'Usable/', 'InvItem/Singleton',
                             'InvItem/PickupExclusive', 'Model/HalfExtents', 'Model/Orientation',
                             'FX/FXID', 'DataNode/', 'Normal/HalfExtents'))}))
opts.output.write_text(json.dumps(dict(source=catalog['source'], gob_sha256=catalog['gob_sha256'],
                                     objects=rows), indent=2) + '\n', encoding='utf-8')
print(f'{len(rows)} utility objects')
