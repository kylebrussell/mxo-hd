"""Read canonical ability templates from the shipped GOB; never modify the client.

Usage: python research/abilities/extract_catalog.py --client C:/.../MxO
Only decoded, stored attributes are exported. Omitted engine defaults remain absent.
"""
import argparse
import collections
import hashlib
import json
import struct
from pathlib import Path


def u32(data, offset):
    return struct.unpack_from('<I', data, offset)[0]


def string(data, offset):
    count = u32(data, offset)
    end = offset + 4 + count
    return data[offset + 4:end].decode('latin1'), end


def extract(client):
    packed = (client / 'packmaps/gameobjects.pkb').read_bytes()
    # The GOB resource has no rezmap.ltb entry. These are the verified 7.6005 resource bounds.
    data = packed[0x8af9d1:0x476488a]
    version, attrs_at, components_at, layouts_at, types_at, count = struct.unpack_from('<6I', data, 16)
    assert version == 7 and count < 100000
    templates = {}
    for i in range(count):
        key, offset, size = struct.unpack_from('<3I', data, 40 + i * 12)
        if key & 0x80000000:
            templates[(key & 0x7fffffff) >> 10] = (offset, size)
    tables = []
    p = attrs_at + 4
    widths = collections.defaultdict(list)
    for _ in range(u32(data, attrs_at)):
        n = struct.unpack_from('<H', data, p)[0]
        p += 2
        table = []
        for _ in range(n):
            name, p = string(data, p)
            typ, offset, flag = struct.unpack_from('<3I', data, p)
            p += 12
            table.append((name, typ, offset, flag))
        tables.append(table)
        offsets = sorted({row[2] for row in table})
        following = dict(zip(offsets, offsets[1:]))
        for name, typ, offset, flag in table:
            if offset in following:
                widths[typ].append(following[offset] - offset)
    widths = {key: min(value) for key, value in widths.items()}
    assert {t: widths[t] for t in (1, 3, 4, 7, 8, 13, 17, 30, 58, 59, 61)} == {
        1: 4, 3: 1, 4: 2, 7: 2, 8: 4, 13: 1, 17: 32, 30: 4, 58: 4, 59: 4, 61: 4}
    components = []
    p = components_at + 4
    for _ in range(u32(data, components_at)):
        n = struct.unpack_from('<H', data, p)[0]
        p += 2
        group = []
        for _ in range(n):
            name, p = string(data, p)
            group.append((name, u32(data, p)))
            p += 4
        components.append(group)
    rows = []
    for ability_id, (offset, size) in sorted(templates.items()):
        name, p = string(data, offset)
        kind, unknown, ncomponents, unknown2 = struct.unpack_from('<4I', data, p)
        p += 16 + 5 * ncomponents
        storage_size, layout, table_id, component_id, unknown3, stored_size = struct.unpack_from('<I4HI', data, p)
        storage = data[p + 16:p + 16 + stored_size]
        assert len(storage) == stored_size and stored_size <= storage_size
        values = {}
        for field, typ, at, flag in tables[table_id]:
            if '/' not in field:
                continue  # aliases of the same component property
            width = widths.get(typ)
            if width is None or at + width > stored_size:
                continue
            raw = storage[at:at + width]
            if typ == 1:
                value = round(struct.unpack('<f', raw)[0], 7)
            elif typ in (16, 17):
                value = raw.split(b'\0')[0].decode('latin1')
            elif width in (1, 2, 4):
                value = int.from_bytes(raw, 'little', signed=typ in (6, 7, 8))
            else:
                value = raw.hex()
            values[field] = value
        rows.append(dict(id=ability_id, name=name, schema=table_id,
                         components=[dict(name=n, type=t) for n, t in components[component_id]], fields=values))
    assert len(rows) == 1404
    return dict(source='MxO 7.6005 gameobjects.gob in packmaps/gameobjects.pkb',
                gob_sha256=hashlib.sha256(data).hexdigest(), schema_version=1,
                omitted_fields='Not stored; engine defaults have not been reconstructed.', abilities=rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=Path('data/ability_catalog.json'))
    options = parser.parse_args()
    result = extract(options.client)
    options.output.parent.mkdir(parents=True, exist_ok=True)
    options.output.write_text(json.dumps(result, indent=2, ensure_ascii=True) + '\n', encoding='utf-8')
    print(f"Exported {len(result['abilities'])} canonical templates to {options.output}")
