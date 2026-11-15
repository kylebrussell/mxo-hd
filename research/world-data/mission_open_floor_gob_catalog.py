"""Export authored static Model6 bounds from a configured client-resource GOB reader.

python mission_open_floor_gob_catalog.py --tools <client-resource-tools> \
    --goids <JSON-file-or-comma-separated-IDs> --output <catalog.json>

The supplied tools directory provides gob.load(); this script never edits client data.
Model6 slot3 is the native three-float HalfExtents binding (625ce7f0/625ce800,
GOB layout section3), consumed by625cc040/626600d0 and spatial registration.
This is an authored STATIC-bound domain, not a mesh/render/physics completeness claim.
"""
import argparse
import hashlib
import importlib
import json
import math
import struct
import sys
from collections import Counter
from pathlib import Path


class GobFormatError(ValueError):
    pass


class Reader:
    def __init__(self, data, start=0, end=None):
        self.data = data
        self.at = start
        self.end = len(data) if end is None else end
        if not 0 <= start <= self.end <= len(data):
            raise GobFormatError('Invalid span')

    def take(self, size):
        if size < 0 or self.at + size > self.end:
            raise GobFormatError('Truncated GOB span')
        value = self.data[self.at:self.at + size]
        self.at += size
        return value

    def unpack(self, fmt):
        return struct.unpack('<' + fmt, self.take(struct.calcsize('<' + fmt)))

    def text(self):
        length, = self.unpack('I')
        return self.take(length).decode('latin1')

    def finish(self):
        if self.at != self.end:
            raise GobFormatError('Section did not end at its declared boundary')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def parse_goids(value):
    """A JSON array/{goids:array}, or a comma-separated decimal/0x list. Canonical set only."""
    path = Path(value)
    try:
        is_file = path.is_file()
    except OSError:
        is_file = False  # A long literal list is not a host filename.
    if is_file:
        parsed = json.loads(path.read_text(encoding='utf-8-sig'))
        values = parsed.get('goids') if isinstance(parsed, dict) else parsed
    else:
        values = value.split(',')
    if not isinstance(values, list) or not values:
        raise ValueError('Expected a nonempty GOID array or comma-separated list')
    result = set()
    for value in values:
        if isinstance(value, bool) or not isinstance(value, (int, str)):
            raise ValueError('GOIDs must be integer identities')
        number = int(value, 16 if isinstance(value, str) and value.lower().startswith('0x') else 10) if isinstance(value, str) else value
        if not 0 <= number < 0x80000000:
            raise ValueError('Expected a raw non-ability GOID')
        result.add(number)
    return sorted(result)


def sections(data):
    if len(data) < 40:
        raise GobFormatError('Missing GOB header')
    version, attrs, components_at, layouts_at, types_at, count = struct.unpack_from('<6I', data, 16)
    index_end = 40 + 12 * count
    if version != 7 or not index_end <= attrs < components_at < layouts_at < types_at <= len(data) - 4:
        raise GobFormatError('Unsupported version or section boundaries')
    index = Reader(data, 40, index_end)
    templates = {}
    for _ in range(count):
        key, offset, size = index.unpack('III')
        if key in templates or offset < index_end or size == 0 or offset + size > attrs:
            raise GobFormatError('Duplicate identity or invalid template span')
        templates[key] = (offset, size)
    index.finish()
    # Attribute descriptors are checked structurally. Binding identity comes from native layout slots,
    # not an assumption that a bare or qualified property alias must exist.
    attr_reader = Reader(data, attrs, components_at)
    table_count, = attr_reader.unpack('I')
    for _ in range(table_count):
        n, = attr_reader.unpack('H')
        for _ in range(n):
            attr_reader.text()
            attr_reader.unpack('III')
    attr_reader.finish()
    component_reader = Reader(data, components_at, layouts_at)
    n, = component_reader.unpack('I')
    components = []
    for _ in range(n):
        count, = component_reader.unpack('H')
        components.append([dict(name=component_reader.text(), type=component_reader.unpack('I')[0]) for _ in range(count)])
    component_reader.finish()
    layout_reader = Reader(data, layouts_at, types_at)
    n, = layout_reader.unpack('I')
    layouts = []
    for _ in range(n):
        nc, = layout_reader.unpack('H')
        group = []
        for _ in range(nc):
            np, = layout_reader.unpack('H')
            group.append([layout_reader.unpack('HI') for _ in range(np)])
        layouts.append(group)
    layout_reader.finish()
    return templates, components, layouts, table_count, dict(
        version=version, templateCount=len(templates), layoutGroupCount=len(layouts),
        indexSha256=sha(data[40:index_end]), attributeSectionSha256=sha(data[attrs:components_at]),
        componentSectionSha256=sha(data[components_at:layouts_at]), layoutSectionSha256=sha(data[layouts_at:types_at]),
        typesSectionSha256=sha(data[types_at:]))


def catalog(data, goids):
    goids = list(goids)
    if any(isinstance(g, bool) or not isinstance(g, int) or not 0 <= g < 0x80000000 for g in goids):
        raise ValueError('Expected raw integer GOIDs')
    data = bytes(data)
    templates, component_groups, layouts, table_count, source = sections(data)
    rows = []
    for goid in sorted(set(goids)):
        if goid not in templates:
            rows.append(dict(goid=goid, status='MissingTemplate'))
            continue
        offset, size = templates[goid]
        record = Reader(data, offset, offset + size)
        name = record.text()
        kind, unknown, count, unknown2 = record.unpack('IIII')
        component_header = record.take(5 * count)
        storage_size, layout, schema, component_group, unknown3, stored_size = record.unpack('IHHHHI')
        if stored_size > storage_size or schema >= table_count or layout >= len(layouts) or component_group >= len(component_groups):
            raise GobFormatError('Invalid template metadata')
        storage_offset = record.at
        storage = record.take(stored_size)
        components = component_groups[component_group]
        if len(components) != count or len(layouts[layout]) != count:
            raise GobFormatError('Component/layout cardinality mismatch')
        models = []
        for ordinal, component in enumerate(components):
            if component['type'] != 6:
                continue
            props = layouts[layout][ordinal]
            if len(props) < 4 or props[0][1] != 19 or props[3][1] != 11:
                raise GobFormatError('Unsupported native Model6 binding layout')
            description_at = props[0][0]
            extent_at = props[3][0]
            if extent_at + 12 > storage_size or description_at + 4 > storage_size:
                raise GobFormatError('Model property exceeds declared storage')
            raw = storage[extent_at:extent_at + 12] if extent_at + 12 <= stored_size else None
            values = list(struct.unpack('<3f', raw)) if raw is not None else None
            finite = values is not None and all(math.isfinite(v) for v in values)
            valid = finite and all(v > 0 for v in values)
            description = struct.unpack_from('<I', storage, description_at)[0] if description_at + 4 <= stored_size else None
            models.append(dict(componentOrdinal=ordinal, componentName=component['name'],
                halfExtentsPropertySlot=3, halfExtentsOffset=extent_at, halfExtentsType=11,
                halfExtentsWidth=12, halfExtentsRawHex=raw.hex() if raw is not None else None,
                halfExtents=values if finite else None,
                halfExtentsSpan=dict(offset=storage_offset + extent_at, length=12, sha256=sha(raw)) if raw is not None else None,
                descriptionPropertySlot=0, descriptionOffset=description_at, descriptionRid=description,
                status='PositiveFiniteAuthoredBounds' if valid else 'MissingOrInvalidBounds',
                qualification='Authored Model6 spatial bounds; includes conditional/Description0 models conservatively, not a measured renderer union.'))
        rows.append(dict(goid=goid, name=name, schema=schema, layout=layout,
            storageSize=storage_size, storedSize=stored_size, storageOffset=storage_offset,
            storedSpanSha256=sha(storage), recordOffset=offset, recordSize=size,
            recordSha256=sha(data[offset:offset + size]), componentHeaderHex=component_header.hex(),
            components=components, models=models,
            status='Model6Absent' if not models else 'PositiveFiniteAuthoredBounds' if all(m['status'] == 'PositiveFiniteAuthoredBounds' for m in models) else 'MissingOrInvalidBounds'))
    result = dict(format=1, sourceGobSha256=sha(data), source=source,
        nativeBindingEvidence=dict(componentType=6, halfExtentsSlot=3, type=11, width=12,
            binder='625ce7f0/625ce800; GOB section3', spatialConsumer='625cc040 ->626600d0 ->62099000 ->62460250/624a1ba0'),
        domain='Authored persistent STATIC Model6 bounds only; Model6 absence is not no rendered effects/flock/spawned actors. OSID/IPRF and structural NAV are independent coverage domains.',
        goids=sorted(set(goids)), counts=dict(Counter(row['status'] for row in rows)), templates=rows)
    result['domainHash'] = sha(json.dumps(result, sort_keys=True, separators=(',', ':'), allow_nan=False).encode())
    return result


def load_tools(tools):
    tools = tools.resolve()
    if not (tools / 'gob.py').is_file():
        raise ValueError('--tools must contain the configured client-resource gob.py reader')
    sys.path.insert(0, str(tools))
    previous_bytecode = sys.dont_write_bytecode
    sys.dont_write_bytecode = True
    try:
        module = importlib.import_module('gob')
        if Path(module.__file__).resolve() != tools / 'gob.py':
            raise ValueError('A different gob module was already imported')
        return module.load()
    finally:
        sys.path.pop(0)
        sys.dont_write_bytecode = previous_bytecode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tools', type=Path, required=True)
    parser.add_argument('--goids', required=True, help='JSON path (array or {goids:[...]}) or comma-separated decimal/0x IDs')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = catalog(load_tools(args.tools), parse_goids(args.goids))
    result['generatorNormalizedSha256'] = sha(Path(__file__).read_text(encoding='utf-8-sig').encode())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf8', newline='\n')
    print(json.dumps(dict(templates=len(result['templates']), counts=result['counts'], domainHash=result['domainHash'])))


if __name__ == '__main__':
    main()
