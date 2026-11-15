"""Read-only export of shipped health boost item permissions and exact triggered templates."""
import argparse, json
from pathlib import Path
from extract_catalog import extract
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--client', type=Path, required=True)
p.add_argument('--output', type=Path, default=Path('data/health_boost_pills.json'))
a = p.parse_args()
c = extract(a.client, raw_goids=True)
items = [r for r in c['abilities'] if r['id'] in (2407, 39411, 39412, 39413, 39414)]
ids = {r['fields']['TriggeredAbility/ReqAbilityID'] for r in items}
abilities = [r for r in c['abilities'] if r['id'] in ids]
assert len(items) == len(abilities) == 5
a.output.write_text(json.dumps(dict(gob_sha256=c['gob_sha256'], items=items, abilities=abilities), indent=2) + '\n')
