"""Export stored crafting and archive-ticket rules from the shipped GOB, without changing the client.
python research/crafting/extract_rules.py --client C:/.../MxO
Missing properties stay null; they are not inferred from recipe tiers or names.
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'abilities'))
from extract_catalog import extract

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--client', type=Path, required=True)
parser.add_argument('--output', type=Path, default=Path('data/crafting_rules.json'))
args = parser.parse_args()
data = extract(args.client, raw_goids=True)
fields = ['InvItem/Complexity', 'InvItem/VendorPrice', 'InvItem/GOCategoryID',
          'InvItem/CantBeCrafted', 'InvItem/CantBeDecompiled', 'InvItem/LearnedByDecompile',
          'Tool/ActivatedAbility']
rows = [[r['id'], r['name']] + [r['fields'].get(f) for f in fields]
        for r in data['abilities'] if any(f in r['fields'] for f in fields)]
result = {k: data[k] for k in ('source', 'gob_sha256', 'schema_version')}
result['columns'] = ['goid', 'name'] + fields
header = json.dumps(result, indent=2)
args.output.write_text(header[:-2] + ',\n  "items": [\n' + ',\n'.join(
    '    ' + json.dumps(row, separators=(',', ':')) for row in rows) + '\n  ]\n}\n')
archives = [dict(goid=r['id'], name=r['name'], fields={k: v for k, v in r['fields'].items()
    if k.startswith(('Ticket/', 'Archivist/', 'GenericTeleporter/', 'Usable/'))})
    for r in data['abilities'] if r['name'].startswith('PvP_Pass') or
    any(k.startswith('Archivist/') for k in r['fields'])]
Path('data/pvp_archives.json').write_text(json.dumps(dict(source=data['source'],
    gob_sha256=data['gob_sha256'], schema_version=1, objects=archives), indent=2) + '\n')
print(f'Exported {len(rows)} crafting items and {len(archives)} archive objects.')
