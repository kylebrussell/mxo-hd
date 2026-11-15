"""Read-only shipped client/GOB proof. Requires existing pefile and capstone research dependencies."""
import argparse, hashlib, json, sys
from pathlib import Path

p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--client',type=Path,required=True)
p.add_argument('--python-deps')
a=p.parse_args()
if a.python_deps: sys.path.insert(0,a.python_deps)
import pefile,capstone
from extract_catalog import extract

raw=(a.client/'client.dll').read_bytes();sha=hashlib.sha256(raw).hexdigest()
assert sha=='cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a'
pe=pefile.PE(data=raw);image=pe.get_memory_mapped_image();base=pe.OPTIONAL_HEADER.ImageBase
md=capstone.Cs(capstone.CS_ARCH_X86,capstone.CS_MODE_32)
def instructions(start,end): return list(md.disasm(image[start-base:end-base],start))
def call(start,end,target):
    assert any(i.mnemonic=='call' and i.op_str==hex(target) for i in instructions(start,end)),(hex(start),hex(target))
def literal(address,text):
    at=address-base;assert image[at:image.index(b'\0',at)].decode('ascii')==text
literal(0x62871018,'ReqAbilityID')
call(0x621d09f0,0x621d0a34,0x620087d0)
call(0x621d83de,0x621d8420,0x621d09f0)
call(0x621d83de,0x621d8420,0x62024870)
call(0x62024870,0x62024890,0x621d5ad0)
call(0x621d5b3b,0x621d5b60,0x625c2720)
call(0x625ebb90,0x625ebc20,0x625e97e0)
live=instructions(0x625c2720,0x625c2764)
assert any(i.op_str=='esi, [edi + 0x110]' for i in live)
assert any(i.op_str=='ecx, [edi + 0x134]' for i in live)
catalog=extract(a.client)
assert catalog['gob_sha256']=='3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103'
# extract() produces a schema-versioned dictionary, with only original stored fields.
rows={r['id']:r for r in catalog['abilities']}
parent=rows[175]['fields'];child=rows[1116]['fields']
assert parent['StatModifier/TargetAbilityGrant']==2148626432
assert parent['StatModifier/WhenEnabled']==0 and parent['StatModifier/DurationType']==2
assert all('StatModifier/'+f not in parent for f in ('ValueFunction','MinValue','MaxValue'))
assert child['Ability/RequiresTool']==1
assert child['Ability/AbilityInnerStrengthCost']==50 and child['Ability/CastingTime']==3
assert child['Ability/RecastTime']==30
tool=next(r for r in extract(a.client,True)['abilities'] if r['id']==460)
assert tool['fields']['AbilityPrerequisite/ReqAbilityID']==2148626432
assert any(c['type']==10 for c in tool['components'])
print(json.dumps(dict(clientSha256=sha,parent=175,child=1116,qualification='stored GOB relation and native live-actor tool consumer; no new retail capture claim',
    chain=['621d84d0','621d8380','621d09f0','62024870','621d5ad0','625c2720'],
    nativeAdd='625ebb90 -> 625e97e0',liveMapOffset=0x110,disabledSetOffset=0x134,
    fields={k:v for k,v in parent.items() if k.startswith('StatModifier/')},requiresTool=child['Ability/RequiresTool']),indent=2))
