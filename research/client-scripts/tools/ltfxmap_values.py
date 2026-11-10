"""Writes ltfxmap (a constants-only module too long for uncompyle6) as NAME = value lines, recovered by
pymodule_values.values() (runs the module's top-level bytecode as data). Usage: ltfxmap_values.py TOOLS_DIR PYC OUT"""
import sys
sys.path.insert(0, sys.argv[1])
from pymodule_values import values

HEADER = [
    '# ltfxmap: effect ids by name. uncompyle6 timed out on this module (one long run of assignments), so the',
    '# values were recovered by running its top-level bytecode as data (tools/client-data/pymodule_values.py).',
    '# Values are shown as unsigned 32-bit hex.',
    r'# Embedded file name: c:/PatchStaging/game/matrix/resource\Python\Monolith\ltfxmap.py',
    '# Compiled at: 2007-07-06 10:14:10',
]

ns = values(sys.argv[2])
with open(sys.argv[3], 'w', encoding='utf-8', newline='\n') as fh:
    fh.write('\n'.join(HEADER) + '\n')
    for k, v in ns.items():
        k = k.decode('latin-1') if isinstance(k, bytes) else k
        if isinstance(v, bytes):
            v = v.decode('latin-1')
        fh.write('%s = %s\n' % (k, '0x%08x' % (v & 0xffffffff) if isinstance(v, int) else repr(v)))
print(len(ns))
