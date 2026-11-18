"""Review evidence and compare generated ability audit rows."""
from collections import Counter
from csv import DictReader
from io import StringIO
import json
import re

STATUSES = ('done', 'partial', 'cosmetic', 'missing', 'inaccessible')


class Evidence:
    def __init__(self):
        self.families = {}
        self.abilities = {}

    @classmethod
    def load(cls, directory, candidates):
        evidence = cls()
        for path in sorted(directory.glob('*.json')):
            document = json.loads(path.read_text(encoding='utf-8'))
            for family, result in document.get('families', {}).items():
                if (result.get('representative') not in candidates or result.get('result') != 'pass'
                        or not re.fullmatch(r'[0-9a-f]{7,40}', result.get('head', ''))
                        or not result.get('native', '').startswith('https://')):
                    raise ValueError(f'{path.name}: {family} needs a native PASS, tested head and evidence link')
                if family in evidence.families and evidence.families[family] != result:
                    raise ValueError(f'{path.name}: conflicting evidence for family {family}')
                evidence.families[family] = result
            for ability_id, review in document.get('abilities', {}).items():
                ability_id = int(ability_id)
                if ability_id not in candidates:
                    raise ValueError(f'{path.name}: ability {ability_id} is outside the audit')
                row = evidence.abilities.setdefault(ability_id, {'checks': {}})
                if family := review.get('family'):
                    if row.get('family', family) != family:
                        raise ValueError(f'{path.name}: conflicting family for ability {ability_id}')
                    row['family'] = family
                for item, proof in review.get('checks', {}).items():
                    if not all(isinstance(proof.get(key), str) and proof[key].strip()
                               for key in ('source', 'runtime', 'test')):
                        raise ValueError(f'{path.name}: ability {ability_id} {item} needs source, runtime and test evidence')
                    if item in row['checks'] and row['checks'][item] != proof:
                        raise ValueError(f'{path.name}: conflicting {item} check for ability {ability_id}')
                    row['checks'][item] = proof
        for family, result in evidence.families.items():
            if evidence.abilities.get(result['representative'], {}).get('family') != family:
                raise ValueError(f'Native representative for {family} must be assigned to that family')
        return evidence

    def evaluate(self, ability_id, status, remaining):
        if status == 'inaccessible':
            return status, remaining

        review = self.abilities.get(ability_id, {})
        remaining = {item: detail for item, detail in remaining.items() if item not in review.get('checks', {})}
        if status in ('cosmetic', 'missing'):
            remaining.setdefault('executor', 'Establish executable gameplay coverage')
        if review.get('family') not in self.families:
            remaining['native-family'] = 'Record a passing stock-client representative for the ability family'
        return ('done' if status == 'partial' and not remaining else status), remaining


def remaining_counts(rows):
    counts = Counter()
    for row in rows:
        counts.update({item.partition(': ')[0] for item in row.split('; ') if item})
    return counts


def summarize(rows, baseline=None):
    totals = Counter(row[7] for row in rows)
    pending = remaining_counts(row[8] for row in rows)
    lines = [
        'Status totals: ' + ', '.join(f'{key} {totals[key]}' for key in STATUSES),
        'Remaining-item totals: ' + (', '.join(f'{key} {value}' for key, value in sorted(pending.items())) or 'none'),
    ]
    if baseline is None:
        return '\n'.join(lines)

    reader = DictReader(StringIO(baseline))
    before = {row['id']: row for row in reader}
    after = {row[0]: row[7] for row in rows}
    previous = Counter(row['status'] for row in before.values())
    changes = Counter((before[key]['status'], after[key]) for key in before.keys() & after.keys()
                      if before[key]['status'] != after[key])
    lines.append('Status delta: ' + ', '.join(f'{key} {previous[key]} -> {totals[key]} ({totals[key] - previous[key]:+d})'
                                             for key in STATUSES))
    lines.append('Rows moved: ' + (', '.join(f'{old} -> {new}: {count}' for (old, new), count in sorted(changes.items()))
                                    or 'none'))
    lines.append(f'Rows added: {len(after.keys() - before.keys())}, removed: {len(before.keys() - after.keys())}')
    if 'remaining' in (reader.fieldnames or []):
        previous_pending = remaining_counts(row['remaining'] for row in before.values())
        differences = ', '.join(f'{key} {previous_pending[key]} -> {pending[key]} ({pending[key] - previous_pending[key]:+d})'
                                for key in sorted(previous_pending.keys() | pending.keys())
                                if previous_pending[key] != pending[key])
        lines.append('Remaining-item delta: ' + (differences or 'none'))
    else:
        lines.append('Remaining-item delta: baseline predates remaining-item tracking')
    return '\n'.join(lines)
