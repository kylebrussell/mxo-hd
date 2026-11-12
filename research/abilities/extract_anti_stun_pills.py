"""Extract the exact stored AntiStun item permissions and triggered abilities, read-only."""
import argparse
import json
from pathlib import Path
from extract_catalog import extract

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--client', type=Path, required=True)
parser.add_argument('--output', type=Path, default=Path('data/anti_stun_pills.json'))
args = parser.parse_args()
catalog = extract(args.client, raw_goids=True)
rows = [row for row in catalog['abilities'] if row['name'] in ('AntiStunPill1', 'AntiStunPill2')]
assert len(rows) == 2
result = dict(gob_sha256=catalog['gob_sha256'], items=rows)
args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf8')
