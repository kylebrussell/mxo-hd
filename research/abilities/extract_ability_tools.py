"""Reproduce native component-10 tools and their stored ReqAbilityID (read-only)."""
import argparse
import json
from pathlib import Path
from extract_catalog import extract

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--client', type=Path, required=True)
p.add_argument('--output', type=Path, default=Path('data/ability_tools.json'))
a = p.parse_args()
d = extract(a.client, raw_goids=True)
rows = []
for r in d['abilities']:
    if not any(c['type'] == 10 for c in r['components']):
        continue
    req = {v for k, v in r['fields'].items() if k.endswith('/ReqAbilityID')}
    assert len(req) <= 1, r['name']
    rows.append([r['id'], r['name'], next(iter(req), None)])
a.output.write_text(json.dumps(dict(gob_sha256=d['gob_sha256'], tools=rows), separators=(',', ':')) + '\n', encoding='utf8')
