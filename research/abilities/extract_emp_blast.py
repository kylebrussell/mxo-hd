"""Read-only EMP ownership evidence from the shipped GOB. No client/database writes.
python research/abilities/extract_emp_blast.py --client C:/.../MxO --output research/abilities/emp_blast_source.json
"""
import argparse
import json
import struct
from pathlib import Path
from extract_catalog import extract

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--client',type=Path,required=True)
parser.add_argument('--output',type=Path,required=True)
args=parser.parse_args()
source=extract(args.client,raw_goids=True)
packed=(args.client/'packmaps/gameobjects.pkb').read_bytes()[0x8af9d1:0x476488a]
count=struct.unpack_from('<I',packed,36)[0]
references={}
for ability in (1025,1026):
    encoded=0x80000000|(ability<<10)
    needle=struct.pack('<I',encoded)
    hits=[]
    for i in range(count):
        key,offset,size=struct.unpack_from('<3I',packed,40+12*i)
        record=packed[offset:offset+size]
        at=0
        while True:
            at=record.find(needle,at)
            if at<0:break
            n=struct.unpack_from('<I',record,0)[0]
            hits.append(dict(key=key,name=record[4:4+n].decode('latin1'),record_offset=at))
            at+=1
    references[str(ability)]=hits
result=dict(source=source['source'],gob_sha256=source['gob_sha256'],
    scan='Every byte offset in every indexed ordinary and class record; raw hits are references, not grant proof.',
    templates=[r for r in source['abilities'] if r['id'] in (43046,0x80100400,0x80100800)],
    references=references,
    boundary='1026 appears only as its own AbilityID. No production NPC grant owner is established. EMPDevice43046 references1025 via TriggeredAbility/ReqAbilityID; this does not prove skill1025 is item-only.')
args.output.write_text(json.dumps(result,indent=2)+'\n')
