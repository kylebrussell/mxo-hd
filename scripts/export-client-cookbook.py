"""Export the MxO client's crafting cookbook to data/recipes.csv.

Usage: python scripts/export-client-cookbook.py <client>/resource/GameObjects/Cookbook.cbb data/recipes.csv

Cookbook.cbb layout (little-endian, read exactly to the end of the file):
  u32 version (1), u32 recipe count (38663 in the 7.6005 client), then per recipe:
  u32 0, u32 level (1-50), u32 type tag (4 ASCII bytes, e.g. "ICP0"), u32 field A, u32 field B,
  u32 ingredient count n, u32 result goid, n x u32 ingredient goids.
Ability codes have result goid 0x80000000 | (ability id << 10).
"""
import struct
import sys


def main(src, dst):
    data = open(src, 'rb').read()
    version, count = struct.unpack_from('<II', data, 0)
    pos = 8
    rows = []
    while pos < len(data):
        _zero, level, tag, field_a, field_b, n, result = struct.unpack_from('<7I', data, pos)
        pos += 28
        ingredients = struct.unpack_from('<%dI' % n, data, pos)
        pos += 4 * n
        text = ''.join(ch if 32 <= ord(ch) < 127 and ch != ';' else '?'
                       for ch in struct.pack('<I', tag).decode('latin1'))
        rows.append('%d;%d;%s;%08x;%08x;%s' % (result, level, text, field_a, field_b,
                                                ' '.join(str(g) for g in ingredients)))
    if pos != len(data) or len(rows) != count:
        raise SystemExit('unexpected layout: %d recipes read, header says %d' % (len(rows), count))
    with open(dst, 'w', newline='\n') as out:
        out.write('# Generated from the MxO client file resource/GameObjects/Cookbook.cbb (see docs/CRAFTING.md).\n')
        out.write('ResultGOID;Level;Type;FieldA;FieldB;Ingredients\n')
        out.write('\n'.join(rows) + '\n')
    print('version %d, %d recipes -> %s' % (version, len(rows), dst))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
