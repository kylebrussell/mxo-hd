"""Read-only ability memory descriptor audit. No output files or client mutation.
python research/abilities/inspect_ability_memory.py --client C:/.../MxO
"""
import argparse, collections, hashlib, json, struct
from pathlib import Path
from extract_catalog import string, u32

def inspect(client, catalog):
    packed=(client/'packmaps/gameobjects.pkb').read_bytes()
    data=packed[0x8af9d1:0x476488a]
    version,attrs,components,layouts,types,count=struct.unpack_from('<6I',data,16)
    assert version==7
    tables=[];p=attrs+4
    for _ in range(u32(data,attrs)):
        n=struct.unpack_from('<H',data,p)[0];p+=2;table=[]
        for _ in range(n):
            name,p=string(data,p);typ,offset,flags=struct.unpack_from('<3I',data,p);p+=12
            table.append((name,typ,offset,flags))
        tables.append(table)
    known={r['id']:r for r in json.loads(catalog.read_text())['abilities']}
    cases=collections.Counter();costs=collections.Counter();examples=[]
    for i in range(count):
        key,offset,size=struct.unpack_from('<3I',data,40+i*12)
        if not key&0x80000000: continue
        ident=(key&0x7fffffff)>>10
        name,p=string(data,offset);kind,unknown,ncomponents,unknown2=struct.unpack_from('<4I',data,p)
        p+=16+5*ncomponents
        declared,layout,table,component,unknown3,stored=struct.unpack_from('<I4HI',data,p)
        storage=data[p+16:p+16+stored]
        fields={n:(t,o,f) for n,t,o,f in tables[table]}
        # Same ordered lookup/one-byte width contract as62516370/620483a0/622ffce0.
        selected=None;value=0
        for field in ('MemCost','InventoryMemCost'):
            if field in fields and fields[field][0]==3:
                selected=field;at=fields[field][1];value=storage[at] if at<stored else 0;break
        cases[selected or 'neither']+=1;costs[value]+=1
        assert value==known[ident]['fields'].get('Ability/InventoryMemCost',0),(ident,value)
        if selected: assert fields[selected][1]<stored,('unstored',ident)
        assert not ('MemCost' in fields and 'InventoryMemCost' in fields),('dual',ident)
        if ident in (2,32,75,356,1087): examples.append(dict(id=ident,schema=table,stored=stored,declared=declared,selected=selected,descriptor=fields.get(selected),value=value))
    assert sum(cases.values())==1404
    assert dict(cases)=={'MemCost':586,'InventoryMemCost':290,'neither':528}
    return dict(gobSha256=hashlib.sha256(data).hexdigest(),counts=dict(cases),costs=dict(costs),catalogMatches=1404,examples=examples)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client',type=Path,required=True)
    parser.add_argument('--catalog',type=Path,default=Path(__file__).resolve().parents[2]/'data/ability_catalog.json')
    args=parser.parse_args();print(json.dumps(inspect(args.client,args.catalog),indent=2))
