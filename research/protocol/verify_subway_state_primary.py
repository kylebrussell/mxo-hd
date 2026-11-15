"""Read-only 7.6005 native framing/schema crosscheck. Usage: python script.py <MxO directory>."""
import hashlib,json,struct,sys
from pathlib import Path

client=Path(sys.argv[1]);raw=(client/'client.dll').read_bytes()
assert hashlib.sha256(raw).hexdigest()=='cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a'
pe=struct.unpack_from('<I',raw,0x3c)[0];count=struct.unpack_from('<H',raw,pe+6)[0]
optional=pe+24;size=struct.unpack_from('<H',raw,pe+20)[0];base=struct.unpack_from('<I',raw,optional+28)[0]
sections=[]
for i in range(count):
 p=optional+size+40*i;vsize,va,rsize,at=struct.unpack_from('<4I',raw,p+8);sections.append((va,max(vsize,rsize),at))
def read(address,n):
 rva=address-base
 for va,size,at in sections:
  if va<=rva and rva+n<=va+size:return raw[at+rva-va:at+rva-va+n]
 raise ValueError(hex(address))
# Read ushort view, test zero, branch to return consumed bytes including the terminator.
assert read(0x622c3610,12)==bytes.fromhex('668b196685db0f84a7010000')
assert read(0x622c37c3,3)==bytes.fromhex('8d7a02')
# MPM reads the next protocol byte; consumed bytes advance its input cursor.
assert read(0x623b45af,5)==bytes.fromhex('8a070fb6d0')
assert read(0x623b45e4,6)==bytes.fromhex('2bf0787c03f8')
data=(client/'packmaps/gameobjects.pkb').read_bytes()[0x8af9d1:0x476488a]
assert hashlib.sha256(data).hexdigest()=='3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103'
u32=lambda p:struct.unpack_from('<I',data,p)[0]
attrs=u32(20);p=attrs+4;selected=[]
for index in range(u32(attrs)):
 n=struct.unpack_from('<H',data,p)[0];p+=2;rows=[]
 for _ in range(n):
  length=u32(p);p+=4;name=data[p:p+length].decode('latin1');p+=length
  typ,offset,flag=struct.unpack_from('<III',data,p);p+=12;rows.append((name,typ,offset,flag))
 if index==4209:selected=rows;break
assert ('CurrentState',21,16,2) in selected and ('Hidden',13,45,2) in selected
assert ('Cursor',19,20,0) in selected # The following stored offset independently bounds CurrentState to four bytes.
print(json.dumps(dict(ClientSha256=hashlib.sha256(raw).hexdigest(),GobSha256=hashlib.sha256(data).hexdigest(),
 NativeZeroViewLoop='622c3610..3616 -> 622c37c3',OuterMpm='623b45af..45e8',
 Schema4209=[r for r in selected if r[0] in ('CurrentState','Hidden','Cursor')],
 Qualification='Primary field types/width and zero-view boundary. Wire update index1 separately retained from generated AttributeClass4209; this is not a new complete native schema reconstruction.'),indent=2))
