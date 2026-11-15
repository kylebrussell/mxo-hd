"""Strict source-only Slums mission anchors, associated with exact legacy rows.

No chooser/eligibility/prototype changes. All native variants are considered.
Conflicting native/legacy keys and every key in missing-layer sectors are omitted.
Per-sector gzip CSV uses the existing 16-column anchor schema; placement -1 is
an actual direct SID/SIDGO record, never an invented IPI ordinal.
"""
import argparse
import collections
import csv
import gzip
import hashlib
import io
import json
from pathlib import Path
import struct
import sys
import mission_open_floor_world_bounds as B

HEADER = 'metr,sector,static_id,native_id,source_goid,native_x,native_y,native_z,native_quat,legacy_x,legacy_y,legacy_z,legacy_quat,source_rid,placement,record_offset'

def canonical_sources(tools):
    sys.path.insert(0,str(Path(tools).resolve()))
    import rezmap_ltb as R
    index = R.index()
    path = next(p for ident,_,p in B.M.WORLDS if ident==1)
    raw = (Path(R.D)/path).read_bytes()
    parents = B.M.read_metr(str(Path(R.D)/path),include_resource_refs=True)
    found, conflicts, missing, sources = {}, set(), [], []
    def add(parent,rid,g,offset,placement=-1,context=None):
        sector=parent['sector'];goid,static=g[:2]
        if sector>0x7ff or static>0xfffff or goid>0xffff:raise ValueError('Invalid canonical source identity')
        if context:
            if context['upper'] or context['rotation']not in range(4):raise ValueError('Unsupported IPI transform')
            pos,q=B.T.sector_pose(g[2:5],g[5:9],context['pivot'],context['rotation'],parent['min'],parent_kind='direct-sector')
        else:pos,q=(parent['min'][0]+g[2],g[3],parent['min'][2]+g[4]),g[5:9]
        qhex=struct.pack('<4f',*q).hex();key=(sector,static);signature=(goid,*pos,qhex)
        row=(1,sector,static,sector<<20|static,goid,*pos,qhex,rid,placement,offset)
        if key in found:
            if found[key][0]!=signature:conflicts.add(key)
        else:found[key]=(signature,row)
    for parent in parents:
        if parent['min'][1]!=0:raise ValueError('Unsupported SCIN Y')
        for kind in ('SID','SIDG','SIDI'):
            rid=parent['resource_refs'][kind]
            if rid not in index:
                missing.append(dict(sector=parent['sector'],layer=kind,rid=rid));continue
            rec=index[rid]
            if rec['flags']!=4:raise ValueError('Unsupported static layer flags')
            data=R.read(rec);sources.append(dict(sector=parent['sector'],layer=kind,rid=rid,sha256=B.digest(data)))
            if kind=='SID':
                buffers=B.osid_buffers(data)
                for off,g in B.go_records(buffers[3][1]):add(parent,rid,g,buffers[3][0]+off)
            elif kind=='SIDG':
                if data[:4]!=b'SIDG':raise ValueError('Unknown SIDGO')
                rr=B.M.Reader(data,16);B.M.read_schema(rr);start=rr.p-4
                for off,g in B.go_records(data[start:]):add(parent,rid,g,start+off)
            else:
                buffers=B.osid_buffers(data)
                for context in B.sidi_go_contexts(buffers[0][1],index):
                    for off,g in context['gos']:add(parent,rid,g,off,context['ordinal'],context)
    return found,conflicts,missing,sources,B.digest(raw)

def legacy_rows(paths):
    found,conflicts={},set()
    for path in paths:
        with Path(path).open(newline='')as f:
            for r in csv.DictReader(f):
                world=int(r['metr_id']);sector=int(r['sector_id']);static_bytes=bytes.fromhex(r['staticId']);mxo=bytes.fromhex(r['mxoId'])
                if len(static_bytes)!=4 or len(mxo)!=4:raise ValueError('Invalid legacy identity width')
                static=int.from_bytes(static_bytes,'little')
                if world!=1:continue
                if sector>0x7ff or static>0xfffff or int.from_bytes(mxo,'little')!=(sector<<20|static):raise ValueError('Invalid legacy identity')
                # Preserve all numeric/raw fields needed by the source-only association.
                descriptor=bytes.fromhex(r['type'])
                if len(descriptor)not in (2,4):raise ValueError('Invalid legacy descriptor width')
                goid=int.from_bytes(descriptor,'little')
                q=bytes.fromhex(r['quat']);B.struct.unpack('<4f',q)
                pos=tuple(float(r[k])for k in ('pos_x','pos_y','pos_z'))
                if not all(B.math.isfinite(v)for v in (*pos,*struct.unpack('<4f',q))):raise ValueError('Nonfinite legacy pose')
                signature=(world,sector,static,goid,*pos,q.hex(),r['exterior'].lower())
                key=(sector,static)
                if key in found and found[key]!=signature:conflicts.add(key)
                else:found.setdefault(key,signature)
    return found,conflicts

def associate(native,native_conflicts,legacy,legacy_conflicts,missing):
    excluded_sectors={m['sector']for m in missing};rows=[];counts=collections.Counter()
    for key,(signature,n)in sorted(native.items()):
        if key[0]in excluded_sectors:counts['missingLayerSector']+=1;continue
        if key in native_conflicts:counts['nativeConflict']+=1;continue
        if key in legacy_conflicts:counts['legacyConflict']+=1;continue
        old=legacy.get(key)
        if old is None:counts['noLegacyAssociation']+=1;continue
        if old[3]!=n[4] or old[-1]!='false':counts['typeOrExteriorMismatch']+=1;continue
        # One canonical native pose + one exact legacy pose, not a key-only match.
        rows.append((*n[:9],*old[4:8],*n[9:]));counts['accepted']+=1
    return rows,dict(counts)

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--tools',required=True);p.add_argument('--output',required=True)
    p.add_argument('--legacy',action='append',help='repeatable CSV path; defaults to both repository Slums files')
    args=p.parse_args();repo=Path(__file__).resolve().parents[2]
    paths=args.legacy or [repo/'data/staticObjects_slums_1.csv',repo/'data/staticObjects_slums_2.csv']
    native,nc,missing,sources,metr=canonical_sources(args.tools)
    print('Canonical source traversal complete',len(native),'conflicts',len(nc),'missing',missing,flush=True)
    legacy,lc=legacy_rows(paths);rows,counts=associate(native,nc,legacy,lc,missing)
    output=Path(args.output);output.mkdir(parents=True,exist_ok=True);files=[]
    for sector,rs in __import__('itertools').groupby(rows,key=lambda r:r[1]):
        rs=list(rs);s=io.StringIO(newline='');w=csv.writer(s,lineterminator='\n');w.writerow(HEADER.split(','));w.writerows(rs)
        target=output/f'{sector}.csv.gz'
        with gzip.GzipFile(filename=str(target),mode='wb',mtime=0)as z:z.write(s.getvalue().encode())
        files.append(dict(sector=sector,file=target.name,rows=len(rs),sha256=B.digest(target.read_bytes())))
    manifest=dict(format=1,header=HEADER,world=1,counts=counts,sourceLayers=sources,missingDeclaredLayers=missing,
        nativeConflictKeys=[list(k)for k in sorted(nc)],legacyConflictKeys=[list(k)for k in sorted(lc)],files=files,
        metrSha256=metr,toolSha256=B.digest(Path(__file__).read_bytes()),readerSha256=B.digest(Path(B.__file__).read_bytes()),
        transformSha256=B.digest(Path(B.T.__file__).read_bytes()),legacy=[dict(file=Path(x).name,sha256=B.digest(Path(x).read_bytes()))for x in paths],
        scope='Source identity/pose evidence only; no concrete prototype, source Model6 status, eligibility or placement policy',
        rawGuard='Require exact world/sector/static/key/source GOID and full known legacy XYZ/quaternion, or canonical source XYZ with existing +145 source representation; never key-only',
        conflicts='All native and raw legacy variants considered; conflicting keys omitted, every key in any missing-layer sector omitted',
        directRecords='placement=-1 for direct SID/SIDGO; record_offset absolute within source. SIDI placement>=0 and offset relative to SIDI buffer0 as existing anchor schema.')
    target=output/'manifest.json';target.write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps(dict(counts=counts,sectors=len(files),manifestSha256=B.digest(target.read_bytes()))),flush=True)

if __name__=='__main__':main()
