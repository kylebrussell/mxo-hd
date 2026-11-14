"""Read-only reproduction of the pinned Safe552 closed asset bounds; no server or source writes."""
import argparse
import hashlib
import importlib
import json
from pathlib import Path
import struct
import sys


def inspect(tools: Path):
    sys.path.insert(0,str(tools))
    resources=importlib.import_module('rezmap_ltb')
    index=resources.index()
    moa=resources.read(index[0x28000654])
    assert hashlib.sha256(moa).hexdigest()=='2c86628f698b840d97b813895b3b1a3eea48613270ece9ea9639bdc1eb522604'
    meshes=[]
    for offset,rid in zip((97,209,321,433),(0x2800064f,0x28000651,0x28000652,0x28000653)):
        assert struct.unpack_from('<I',moa,offset)[0]==rid
        record=index[rid];raw=resources.read(record)
        vertices,triangles=struct.unpack_from('<II',raw,27)
        assert len(raw)==63+vertices*32+triangles*6
        xyz=[struct.unpack_from('<3f',raw,63+i*32) for i in range(vertices)]
        meshes.append(dict(rid=hex(rid),alias=record['path'],sha256=hashlib.sha256(raw).hexdigest(),
            moaOffset=offset,vertices=vertices,triangles=triangles,bone=struct.unpack_from('<I',raw,59)[0],
            minimum=[min(p[i] for p in xyz) for i in range(3)],maximum=[max(p[i] for p in xyz) for i in range(3)]))
    skeleton=resources.read(index[0x2800064e])
    assert hashlib.sha256(skeleton).hexdigest()=='b8361d43f14a032924eb0b602ecd2ab1d3cb5eebc5f9878b810767bdeb818e56'
    root=skeleton.index(b'joint_base')+len(b'joint_base')
    assert struct.unpack_from('<Hh',skeleton,root)==(0,-1)
    matrices=[struct.unpack_from('<16f',skeleton,root+4+64*i) for i in range(2)]
    identity=tuple(float(i%5==0) for i in range(16))
    assert all(m==identity for m in matrices)
    return dict(moa=index[0x28000654],moaSha256=hashlib.sha256(moa).hexdigest(),meshes=meshes,
        skeletonSha256=hashlib.sha256(skeleton).hexdigest(),rootMatrices=matrices,
        limitation='Closed source mesh only; no native draw, animation sweep, collision, placement or chooser proof')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tools',type=Path,required=True,help='Existing read-only client-data tools directory')
    parser.add_argument('--output',type=Path,help='Explicit owned output; otherwise JSON goes to stdout')
    args=parser.parse_args()
    output=json.dumps(inspect(args.tools),indent=2)+'\n'
    if args.output:args.output.write_text(output,encoding='utf-8')
    else:print(output,end='')
