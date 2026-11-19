"""Read the shipped 7.6005 ILDB without modifying the client.

Export static tactic pairs for the martial-arts and weapon exchange sets, their stand-to-tactic
transitions and special moves. The letters come from the ILDB's own
group names: 'Stand Transitions:To KungFuAbility' holds 'Trans Stand-WS'..'Trans Stand-WB', and so on.
IDs are assigned in file order starting at 255 (client.dll 62649920/62649a4a).
The bounds identify interlock_master.ilmb in interface_interlock_materials.pkb;
rezmap.ltb resource 0x24000b8b independently records these same bounds.
"""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path


# The retail interlock rules carry length-prefixed canonical GOB ability symbols. Restrict this
# extraction to reviewed close-combat bindings; similarly named single-round or generic moves
# can have different rules. Punch Reversal has a family of tactic variants with the same symbol.
SPECIAL_BINDINGS = {
    250: ('controlledburstability', 'SubM Burst MR', 'SubM Special Moves'),
    256: ('threeroundburstability', '3 round burst MR', 'Rifle Special Moves'),
    407: ('punchreversalability', 'AikidoAbility RevSlamChicken', 'AikidoAbility Special Moves:AikidoAbility RevSlamChicken'),
    412: ('tomonageability', 'AikidoAbility TomoNage', 'AikidoAbility Special Moves'),
    532: ('machinegunkickability', 'Karate MachGunKick long', 'KarateAbility Special Moves'),
}


def extract(client):
    with (client / 'packmaps/interface_interlock_materials.pkb').open('rb') as f:
        f.seek(74324101)
        data = f.read(9108938)
    assert data[:4] == b'ILDB' and len(data) == 9108938

    def chunks(start, end):
        while start < end:
            tag, size, count = struct.unpack_from('<4sII', data, start)
            assert start + 12 + size <= end
            yield tag, start + 12, start + 12 + size, count
            start += 12 + size
        assert start == end

    def text(start):
        size = struct.unpack_from('<H', data, start)[0]
        return data[start + 2:start + 2 + size].decode('latin1').rstrip('\0')

    rows, next_id = [], 255
    bound = set()
    for tag, start, end, _ in chunks(32, len(data)):
        assert tag == b'GRP '
        group = ''
        for tag, a, b, _ in chunks(start, end):
            if tag == b'NAME':
                group = text(a)
            elif tag == b'DATA':
                for tag, c, d, _ in chunks(a, b):
                    assert tag == b'MOVE'
                    row = dict(id=next_id, group=group)
                    next_id += 1
                    rules = b''
                    for tag, e, f, _ in chunks(c, d):
                        if tag == b'DATA':
                            row['name'] = text(e)
                            row['flags'], row['duration_ms'] = struct.unpack_from('<IH', data, f - 6)
                        elif tag == b'RULS':
                            rules = data[e:f]
                    if row['flags'] == 0x100:
                        for ability, (symbol, move, rule_group) in SPECIAL_BINDINGS.items():
                            encoded = symbol.encode('ascii')
                            rule = b'\x06\x01' + struct.pack('<H', len(encoded)) + encoded + b'\0\0'
                            if group == rule_group and row['name'].split(' vs ', 1)[0] == move and rule in rules:
                                row['ability_rule'] = ability
                                bound.add(ability)
                    if row['flags'] == 0x100 or (re.match(r'^[ADGKPRSW][SPDB] vs [ADGKPRSW][SPDB]:', group) and 'static' in row['name'] and
                            row['flags'] in (1, 4, 8, 0x41, 0x48)) or row['id'] in (
                                748, 8198, 9062, 23965, 23966, 23967) or re.match(
                                r'^Stand Transitions:To (Self-Defense|AikidoAbility|KarateAbility|KungFuAbility|Pistol|Rifle|SubM|DualWeapon)$', group):
                        rows.append(row)
    assert next_id - 255 == 25695
    assert bound == SPECIAL_BINDINGS.keys()
    return dict(schema_version=1, resource_id=0x24000b8b,
                source='MxO 7.6005 interlock_master.ilmb',
                sha256=hashlib.sha256(data).hexdigest(), total_source_moves=25695, moves=rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path('data/combat_animations.json'))
    args = parser.parse_args()
    result = extract(args.client)
    metadata = json.dumps({k: v for k, v in result.items() if k != 'moves'}, indent=2)
    rows = ',\n'.join('    ' + json.dumps(row, separators=(',', ':')) for row in result['moves'])
    args.output.write_text(metadata[:-2] + ',\n  "moves": [\n' + rows + '\n  ]\n}\n')
    print(len(result['moves']), 'moves;', result['sha256'])
