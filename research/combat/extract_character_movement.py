"""Read-only evidence for the shipped7.6005 character movement constants and corpse assets.
No client execution. The PE and movement master addresses/bounds are profile-specific.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path


def extract(client):
    dll = (client / 'client.dll').read_bytes()
    assert hashlib.sha256(dll).hexdigest() == 'cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a'
    pe = struct.unpack_from('<I', dll, 0x3c)[0]
    count, optional = struct.unpack_from('<H', dll, pe + 6)[0], struct.unpack_from('<H', dll, pe + 20)[0]
    base = struct.unpack_from('<I', dll, pe + 24 + 28)[0]
    sections = []
    for i in range(count):
        a = pe + 24 + optional + i * 40
        size, rva, rawsize, raw = struct.unpack_from('<4I', dll, a + 8)
        sections.append((rva, max(size, rawsize), raw))

    def offset(va):
        rva = va - base
        return next(raw + rva - start for start, size, raw in sections if start <= rva < start + size)

    def names(table, count):
        result = []
        for i in range(count):
            va = struct.unpack_from('<I', dll, offset(table) + 4 * i)[0]
            at = offset(va)
            result.append(dll[at:dll.index(b'\0', at)].decode('ascii'))
        return result

    stances, actions = names(0x629ce160, 27), names(0x629ce1d0, 213)
    assert stances[5] == 'Prone' and actions[0] == 'Idle' and actions[63] == 'Resurrect'
    with (client / 'packmaps/interface_interlock_materials.pkb').open('rb') as f:
        f.seek(71858414)
        data = f.read(63002)
    assert data[:4] == b'ILDB' and len(data) == 63002

    def chunks(a, b):
        while a < b:
            tag, size, count = struct.unpack_from('<4sII', data, a)
            assert a + 12 + size <= b
            yield tag, a + 12, a + 12 + size
            a += 12 + size
        assert a == b

    moves = []
    for _, a, b in chunks(32, len(data)):
        group = ''
        for tag, e, f in chunks(a, b):
            if tag == b'NAME':
                group = data[e + 2:f].decode('ascii')
            elif tag == b'DATA':
                for _, g, h in chunks(e, f):
                    parts = {t.decode('ascii'): data[i:j] for t, i, j in chunks(g, h)}
                    name = parts['DATA'][2:-6].decode('ascii')
                    if ' to Prone' in name:
                        moves.append(dict(group=group, name=name, rules_hex=parts['RULS'].hex(),
                                          animation_chunks_hex=parts['AGGR'].hex()))
    return dict(client_sha256=hashlib.sha256(dll).hexdigest(),
                constant_initializer='62692ea0/62692ee3', stance_table='629ce160', action_table='629ce1d0',
                stance_names=stances, action_names=actions,
                movement_resource=dict(id='3a0002f7', pack='interface_interlock_materials.pkb',
                                       offset=71858414, size=63002, sha256=hashlib.sha256(data).hexdigest()),
                corpse_transitions=moves)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', required=True, type=Path)
    parser.add_argument('--output', type=Path, default=Path('data/character_movement_evidence.json'))
    args = parser.parse_args()
    args.output.write_text(json.dumps(extract(args.client), indent=2) + '\n', encoding='utf-8')
