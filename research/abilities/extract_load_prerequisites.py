"""Reproduce the stored component39 graph; no server admission/default invention."""
import argparse, hashlib, json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser()
p.add_argument('--output', type=Path, default=Path(__file__).with_name('ability-load-prerequisites.json'))
args = p.parse_args()
source = ROOT / 'data/ability_catalog.json'
rows = []
for row in json.loads(source.read_text(encoding='utf-8-sig'))['abilities']:
    fields = row['fields']; direct = {}
    for component in row['components']:
        if component['type'] != 39: continue
        prefix = component['name'] + '/'
        descriptor = fields.get(prefix + 'ReqAbilityID', 0)
        if descriptor & 0xfc000000 != 0x80000000: continue
        target = (descriptor >> 10) & 0xfff
        if not target: continue
        direct[target] = max(direct.get(target, -2147483648), fields.get(prefix + 'ReqAbilityLevel', 0))
    char = [v for k,v in fields.items() if k.endswith('/ReqCharLevel')]
    assert len(char) <= 1
    rows.append({'id': row['id'], 'characterLevel': char[0] if char else 0,
                 'direct': [[i,level] for i,level in sorted(direct.items())]})
lookup = {row['id']: row for row in rows}
def closure(root):
    result = {}; pending = [root]; seen = set()
    while pending:
        node = pending.pop()
        if node in seen: continue
        seen.add(node)
        for parent, level in lookup[node]['direct']:
            result[parent] = max(result.get(parent, -2147483648), level)
            pending.append(parent)
    return result
counterexamples = []
for root, row in lookup.items():
    parents = closure(root)
    minimum = max([1, row['characterLevel'], *parents.values()])
    excluded = [[parent, level, lookup[parent]['characterLevel']] for parent, level in sorted(parents.items())
                if lookup[parent]['characterLevel'] > minimum]
    if minimum <= 50 and excluded:
        counterexamples.append({'root':root,'characterLevel':minimum,'parents':excluded})
result = {'schema':1, 'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),
 'clientSha256':'cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a',
 'primary':{'collector':'6251d3c0 component39', 'closure':'6251d660; max6251d76e',
 'loadValidator':'621de9f0', 'actualLoad':'621e07a0 -> 621d4150 / 80ae',
 'characterByteGetter':'625182b0; caller-initialized0',
 'livePrerequisiteBase':'621d5ad0 -> 625c4e90(flags0): I node+14, no numeric/link bonus'},
 'limits':['Pure metadata, no admission or implicit loading.',
 'Strict family guard for corrupt catalog is conservative; all shipped edges have the real ability family.',
 'Initial/default descriptor evidence is not a recovered retail server admission policy.'],
 'parentCharacterCounterexamples':counterexamples, 'templates':len(rows), 'templatesWithRequirements':sum(bool(x['direct']) for x in rows),
 'edges':sum(len(x['direct']) for x in rows), 'positiveCharacterRequirements':sum(x['characterLevel'] > 0 for x in rows),
 'abilities':sorted(rows,key=lambda x:x['id'])}
rows = result.pop('abilities')
header = json.dumps(result,indent=2)[:-2]
body = ',\n'.join('    '+json.dumps(row,separators=(',',':')) for row in rows)
args.output.write_text(header+',\n  \"abilities\": [\n'+body+'\n  ]\n}\n',encoding='utf-8',newline='\n')
