"""Read-only Medium03 resource crosscheck. No database/client process or extraction mutation.
python research/world-data/medium03_resources.py <client-data tools> <MxO directory> [output JSON]
"""
import csv,hashlib,importlib.util,json,sys
from pathlib import Path
sys.path.insert(0,sys.argv[1])
import rezmap_ltb as R,cooked,codf
import hdl_network as H,navmesh as N,subway_network as S
client=Path(sys.argv[2]);output=Path(sys.argv[3] if len(sys.argv)>3 else 'research/world-data/medium03-resources.json')
base='resource/worlds/final_world/constructs/medium/medium03/'
bp=R.by_path();hashes={}
def read(path):
    loose=client/path
    data=loose.read_bytes() if loose.is_file() else R.read(bp[path])
    hashes[path]=hashlib.sha256(data).hexdigest();return data
rows=[r for r in csv.DictReader(open('data/sector_properties.csv')) if r['metr_id']=='16'];assert len(rows)==60
navpaths={Path(k).stem:k for k in bp if k.endswith('.nav') and '/medium03/' in k}
for r in rows:read(navpaths[r['file']])
next_id,hls=H.parse(read(base+'medium03.hdl'));assert next_id==3 and len(hls)==1
h=hls[0];assert h[0]==[2] and h[2]==[(37800.,400.,31700.)]
pos=h[2][0];offset=h[3][0];arrival=[round(pos[i]+offset[i],3) for i in range(3)];assert arrival==[37700.,495.,31700.]
raw=read(base+'medium03.cooked_cod');chunks=cooked.chunks(str(client/(base+'medium03.cooked_cod')));assert len(chunks)==60
classes=[codf.parse(c) for c in chunks];booths=[dict(sector=i,**b) for i,p in enumerate(classes) for b in p['Hardline']['instances']]
assert booths==[dict(sector=52,GUID=1,RTID=592,NAME='467 Reitman Road',SPOS=[7800.,400.,1700.],FWRD=[-1.,0.,0.],OFFS=100.)]
nav=read(navpaths['medium03_11']);locations=[]
for ri,(o,s) in enumerate(N.sections(nav)[1:]):
    for pi,(plane,verts) in enumerate(N.polygons(nav,o,s) or []):
        if N.inside(verts,7700,1700):
            a,b,c,d=plane;locations.append(dict(region=ri,polygon=pi,floorY=-(a*7700+c*1700+d)/b))
assert locations==[dict(region=7,polygon=4,floorY=400.)]
metr=read(base+'medium03.metr');nets=list(S.networks(metr));assert not nets
result=dict(world=16,sectorCount=60,navSectorCount=len(navpaths),hardlineNextId=next_id,hardlineHid=2,hardlineRawFields=h,hardlineCooked=booths,
            arrival=arrival,arrivalNav=locations,transportGroups=sum(len(p['TransportGroup']['instances']) for p in classes),
            transportSpaces=sum(len(p['TransportSpace']['instances']) for p in classes),subwayCnetCount=0,
            elevatorMapping='Static panels only. No proven native-key to TransportSpace mapping exported.',sourceSha256=hashes)
output.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print('60 NAV sectors; HDL16/2 exact walkable floor400; cooked592 crosscheck; zero CNET; '+str(result['transportGroups'])+' transport groups/'+str(result['transportSpaces'])+' spaces (unmapped)')
