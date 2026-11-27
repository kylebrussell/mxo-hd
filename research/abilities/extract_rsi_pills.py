"""Export the stock RSI consumable item's callback, descriptor and avatar restriction."""
import argparse
import json
from pathlib import Path

from extract_catalog import extract


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path('data/rsi_pills.json'))
    args = parser.parse_args()
    catalog = extract(args.client, raw_goids=True)
    rows = []
    unresolved = []
    ability = 0x80000000 | (1245 << 10)
    for item in catalog['abilities']:
        fields = item['fields']
        if fields.get('TriggeredAbility/ReqAbilityID') != ability:
            continue
        assert fields['InvItem/IsAConsumable'] == 1 and fields['Usable/ExecuteRuleID'] == 10001
        female = fields['CharStatsPrerequisite/ReqGenderFemale']
        assert (fields['CharStatsPrerequisite/ReqGenderMale'], female) in ((1, 0), (0, 1))
        callback, descriptor = fields['InvItem/RSICall'], fields['InvItem/NewRSIID']
        # These early aliases call the hair color setter with body descriptor IDs.
        if callback == 'SetHairColor' and descriptor > 31:
            unresolved.append(item['id'])
            continue
        rows.append([item['id'], callback, descriptor, female])
    assert len(rows) == 235 and unresolved == [46736, 46737, 46738]
    with args.output.open('w', encoding='utf-8', newline='\n') as output:
        output.write('{"gob_sha256": "' + catalog['gob_sha256'] + '",\n')
        output.write(' "columns": ["goid", "RSICall", "NewRSIID", "ReqGenderFemale"],\n')
        output.write(' "items": [\n')
        output.write(',\n'.join('    ' + json.dumps(row) for row in rows))
        output.write('\n ]\n}\n')


if __name__ == '__main__':
    main()
