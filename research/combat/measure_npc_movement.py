"""Read-only measurement of retail NPC movement speeds from the checked-in packet dumps.

Server view updates carry NPC movement as `<view u16> 02 0e <mode u8> <yaw u8> <x f32> <y f32> <z f32>`
(the same record hds builds in Mob.updateAnimation). Mode 0 stands, 1 walks, 2 walks backwards, 3 runs and
4/5 turn in place. A packet's time is its own sim time (leading 0x82 then f32) or, between two such packets
at most 1.5 s apart, linear interpolation in packet order. A leg runs from a record of the measured mode to
the next record of the same view in that file; legs under 0.15 s or 5 units, over 3 s, or with more than
30 units of height change are skipped. Speeds are world units (centimetres) per second.

    python research/combat/measure_npc_movement.py research/packet-dumps research/combat/npc-movement-speeds.json
"""
import bisect
import collections
import json
import re
import struct
import sys
from pathlib import Path

HEADER = re.compile(r'^(GAME|WORLD)->Client \[')
HEX = re.compile(r'^([0-9a-f]{2}( [0-9a-f]{2})*)\s*$')
RECORD = re.compile(rb'(..)\x02\x0e([\x00-\x05])(.)', re.S)
MODES = {0: 'stand', 1: 'walk', 2: 'walk-backwards', 3: 'run', 4: 'turn', 5: 'turn'}


def packets(path):
    lines = path.read_text(errors='ignore').splitlines()
    out, i = [], 0
    while i < len(lines):
        if not HEADER.match(lines[i]):
            i += 1
            continue
        i += 1
        data = []
        while i < len(lines) and HEX.match(lines[i]):
            data += [int(x, 16) for x in lines[i].split()]
            i += 1
        out.append(bytes(data))
    return out


def legs(path):
    found = packets(path)
    known = [(n, struct.unpack_from('<f', b, 1)[0]) for n, b in enumerate(found) if len(b) >= 5 and b[0] == 0x82]
    known = [(n, s) for n, s in known if 1 < s < 1e8]
    index = [n for n, _ in known]

    def when(n):
        j = bisect.bisect_left(index, n)
        if j < len(index) and index[j] == n:
            return known[j][1]
        if j == 0 or j == len(index):
            return None
        (a, sa), (b, sb) = known[j - 1], known[j]
        return sa + (sb - sa) * (n - a) / (b - a) if 0 <= sb - sa <= 1.5 else None

    tracks = collections.defaultdict(list)
    for n, b in enumerate(found):
        t = None
        for m in RECORD.finditer(b):
            p = m.end()
            if p + 12 > len(b):
                continue
            x, y, z = struct.unpack_from('<3f', b, p)
            if not all(abs(v) < 2e5 for v in (x, y, z)):
                continue
            t = when(n) if t is None else t
            if t is not None:
                tracks[struct.unpack('<H', m.group(1))[0]].append((t, m.group(2)[0], x, y, z))
    for tr in tracks.values():
        for a, b in zip(tr, tr[1:]):
            dt, d = b[0] - a[0], ((b[2] - a[2]) ** 2 + (b[4] - a[4]) ** 2) ** .5
            if 0.15 <= dt <= 3 and d >= 5 and abs(b[3] - a[3]) <= 30:
                yield a[1], d / dt


def summary(values):
    s = sorted(values)
    n = len(s)
    return dict(legs=n, median=round(s[n // 2], 1), p25=round(s[n // 4], 1), p75=round(s[3 * n // 4], 1)) if n else dict(legs=0)


def main(root, output):
    by_mode, by_file = collections.defaultdict(list), collections.defaultdict(lambda: collections.defaultdict(list))
    for path in sorted(root.rglob('*')):
        if path.is_file() and ('.log' in path.name or path.suffix == '.txt'):
            for mode, speed in legs(path):
                by_mode[mode].append(speed)
                by_file[path.relative_to(root).as_posix()][mode].append(speed)
    result = dict(
        source=root.as_posix(), units='world units (cm) per second', record='<view u16> 02 0e <mode> <yaw> <x y z f32>',
        modes={f'{m} {MODES[m]}': summary(v) for m, v in sorted(by_mode.items())},
        files_with_run_legs={f: summary(v[3]) for f, v in sorted(by_file.items()) if v.get(3)},
        files_with_walk_legs=len([f for f, v in by_file.items() if v.get(1)]))
    output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result['modes'], indent=2))


if __name__ == '__main__':
    main(Path(sys.argv[1]), Path(sys.argv[2]))
