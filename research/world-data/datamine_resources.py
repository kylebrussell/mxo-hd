"""Read-only DataMine source evidence. Run from repository root: tools-dir MxO-dir [output-json]."""
import csv,hashlib,json,sys
from pathlib import Path
sys.path.insert(0,sys.argv[1]);import rezmap_ltb as R,cooked,codf
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'abilities'));from extract_catalog import extract
import hdl_network as H,subway_network as S
client=Path(sys.argv[2]);prefix='resource/worlds/final_world/constructs/archive/archive_datamine/';bp=R.by_path();hashes={}
def read(p):
 d=(client/p).read_bytes() if (client/p).is_file() else R.read(bp[p]);hashes[p]=hashlib.sha256(d).hexdigest();return d
metr=read(prefix+'datamine.metr');assert not list(S.networks(metr));nextid,hls=H.parse(read(prefix+'datamine.hdl'));assert nextid==2 and not hls
read(prefix+'datamine.cooked_cod');cs=[codf.parse(c) for c in cooked.chunks(str(client/(prefix+'datamine.cooked_cod')))];assert len(cs)==59
nav=[p for p in bp if p.startswith(prefix) and p.endswith('.nav')];assert len(nav)==59
for p in nav:read(p)
cat=extract(client,raw_goids=True);proxy=next(r for r in cat['abilities'] if r['id']==47267);f=proxy['fields'];assert f['Usable/ExecuteRuleID']==1000 and f['Archivist/ArchivistOrganization']==0 and f['Model/DetailsStringID']==0x04000339
strings=Path(sys.argv[1]).resolve().parents[1]/'client-data/dumps/strings/stringtable_client.tsv';texts={r[0]:r[2] for line in strings.read_text(encoding='utf-8').splitlines()[1:] if len(r:=line.split('\t'))>=3};tooltip=texts['04000339'];assert 'return operatives to the city' in tooltip
result=dict(world=7,retailConstruct=17,sectorCount=59,navSectorCount=59,hardlines=len(hls),hardlineNextId=nextid,subwayCnetCount=0,
 transportGroups=sum(len(c['TransportGroup']['instances']) for c in cs),transportSpaces=sum(len(c['TransportSpace']['instances']) for c in cs),
 proxy=proxy,proxyTooltip=dict(id='04000339',text=tooltip),gobSha256=cat['gob_sha256'],sourceSha256=hashes,
 policy='Exact retail arrival and city destination unresolved. Existing server nearest-floor arrival and session-origin/Mara fallback retained unchanged. No node locations inferred.')
Path(sys.argv[3] if len(sys.argv)>3 else 'research/world-data/datamine-resource-evidence.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print('DataMine59NAV/0HL/0CNET;actual47267Generic/GeneralSuccess city tooltip source evidence')
