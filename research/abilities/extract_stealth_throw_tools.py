"""Read-only GOB export of the ITOt category required by Disable Evasion1125."""
import argparse,json
from pathlib import Path
from extract_catalog import extract
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--client',type=Path,required=True);p.add_argument('--output',type=Path,default=Path('data/stealth_throw_tools.json'))
a=p.parse_args();data=extract(a.client,raw_goids=True)
rows=[r['id'] for r in data['abilities'] if r['fields'].get('InvItem/GOCategoryID')==1951355977]
metadata=json.dumps(dict(gob_sha256=data['gob_sha256'],category=1951355977),indent=2)
lines=',\n'.join('    '+','.join(str(i) for i in rows[k:k+20]) for k in range(0,len(rows),20))
a.output.write_text(metadata[:-2]+',\n  \"tools\": [\n'+lines+'\n  ]\n}\n',encoding='utf8')
print(len(rows),'ITOt tools')
