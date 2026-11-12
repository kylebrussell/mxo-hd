"""Export shipped Tool/Disguise model resources; client files remain read-only."""
import argparse, json
from pathlib import Path
from extract_catalog import extract
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--client', type=Path, required=True)
parser.add_argument('--output', type=Path, default=Path('data/disguise_tools.json'))
a = parser.parse_args()
catalog = extract(a.client, raw_goids=True)
rows = [dict(goid=r['id'], name=r['name'], model=r['fields']['Tool/Disguise'])
        for r in catalog['abilities'] if r['fields'].get('Tool/Disguise')]
a.output.write_text(json.dumps(dict(source=catalog['source'], gob_sha256=catalog['gob_sha256'], tools=rows), indent=2) + '\n', encoding='utf8')
print(f'Exported {len(rows)} disguise tools')
