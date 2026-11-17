"""Read-only Old1124 resource, original-bytecode and indexed-capture queries."""
import argparse
import collections
import json
from pathlib import Path
import re
import struct
import sys

from extract_catalog import extract


def inspect(client, pyc_root, marshal_tools, repository):
    catalog = extract(client, raw_goids=True)
    old_goid = 0x80119000
    resource_refs = []
    for row in catalog['abilities']:
        hits = {key: value for key, value in row['fields'].items()
                if value == old_goid or isinstance(value, str) and 'DisruptConnection' in value}
        if hits:
            resource_refs.append(dict(id=row['id'], name=row['name'], fields=hits))
    gob = (client / 'packmaps/gameobjects.pkb').read_bytes()[0x8af9d1:0x476488a]
    byte_hits = [match.start() for match in re.finditer(re.escape(struct.pack('<I', old_goid)), gob)]

    # Existing research parser reads Python2.3 marshal without executing the scripts.
    sys.path.insert(0, str(marshal_tools))
    from py23marshal import Code, load_pyc
    script_hits = []
    modules = sorted(pyc_root.rglob('*.pyc'))
    for path in modules:
        def visit(code):
            hits = []
            for value in code.names + code.consts:
                if isinstance(value, bytes) and re.search(rb'disrupt.?connection|disrupt_booted', value, re.I):
                    hits.append(repr(value))
                elif isinstance(value, int) and value in (1124, old_goid, old_goid - 2**32):
                    hits.append(str(value))
            if hits:
                script_hits.append(dict(file=str(path.relative_to(pyc_root)), function=repr(code.name),
                                        line=code.firstlineno, hits=hits))
            for value in code.consts:
                if isinstance(value, Code):
                    visit(value)
        visit(load_pyc(str(path)))

    index = json.loads((repository / 'docs/generated/packet-research-summary.json').read_text(encoding='utf-8-sig'))
    files = [row['File'] for row in index['PacketDumpFiles']
             if any(key.lower().startswith(('80 b9', '80 b4')) for key in row.get('KnownHeaderCounts', {}))]
    counts = collections.Counter()
    packets = 0
    missing_files = []
    interesting = []
    for filename in files:
        path = repository / filename
        if not path.exists():
            missing_files.append(filename)
            continue
        active, chunks, start = False, [], 0

        def flush():
            nonlocal packets
            if not active:
                return
            body = b''.join(chunks)
            if not body.startswith((b'\x02\x04', b'\x04')):
                return
            packets += 1
            for match in re.finditer(rb'\x80[\xb9\xb4]', body):
                offset = match.start()
                if offset + 4 > len(body):
                    continue
                raw = int.from_bytes(body[offset + 2:offset + 4], 'little')
                ability = raw % 4096
                counts[ability] += 1
                if ability in (1124, 1125):
                    interesting.append(dict(file=filename, packet_line=start, raw=raw, ability=ability))

        for line_number, line in enumerate(path.read_text(errors='replace').splitlines(), 1):
            if re.search(r'(?:Client|GAME|WORLD)\s*->\s*(?:Client|GAME|WORLD)', line, re.I):
                flush()
                active = bool(re.search(r'Client\s*->\s*(GAME|WORLD)', line, re.I))
                chunks, start = [], line_number
            elif re.fullmatch(r'\s*(?:[0-9a-fA-F]{2}\s+)*[0-9a-fA-F]{2}\s*', line):
                chunks.append(bytes.fromhex(line))
        flush()
    return dict(gob_sha256=catalog['gob_sha256'], resource_count=len(catalog['abilities']),
                old_goid_byte_offsets=[hex(offset) for offset in byte_hits], resource_refs=resource_refs,
                original_script_modules=len(modules), script_hits=script_hits,
                indexed_capture_files=len(index['PacketDumpFiles']), cast_lead_files=len(files),
                missing_capture_files=missing_files, client_protocol04_packets=packets,
                candidate_cast_headers=sum(counts.values()), normalized1124=counts[1124],
                normalized1125=counts[1125], interesting_cast_candidates=interesting,
                common_cast_candidates=counts.most_common(8))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--pyc-root', type=Path, required=True)
    parser.add_argument('--marshal-tools', type=Path, required=True)
    parser.add_argument('--repository', type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    print(json.dumps(inspect(args.client, args.pyc_root, args.marshal_tools, args.repository), indent=2))
