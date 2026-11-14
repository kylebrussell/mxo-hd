"""Package audited native static-bound corpora into lazy mission scene files.

This is a deterministic packaging pass, not another geometry decoder. Inputs are
the source-profile exports listed and hashed in manifest.json. All source
variants and all heights are kept. A grid accelerates exact AABB/sector overlap;
it never supplies a radius or a parent-containment assumption.
"""
from __future__ import annotations

import argparse
import collections
import csv
import gc
import gzip
import hashlib
import json
import math
from pathlib import Path

DLL_SHA = "cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a"
PROFILES = {
    "go": "Authored-GOB-Model6-native-static-registration-v1",
    "osid": "ROOT-OSID-authored-union-bound-v1",
    "sid8": "ROOT-SID-buffer8-native-centre-half-bound-v1",
    "epi": "EPI-authored-PROP-and-GOIN-parent-union-bound-v1",
    "ipi": "IPI-inline-PROP-authored-parent-union-bound-v1",
}
INPUTS = {
    "go": "mxo-open-floor-all-slums-go-bounds.json.gz",
    "osid": "mxo-open-floor-all-slums-root-osid-bounds.json.gz",
    "sid8": "mxo-open-floor-slums-buffer8-bounds.json",
    "epi": "mxo-open-floor-all-slums-epi-bounds.json.gz",
    "ipi": "mxo-open-floor-all-slums-ipi-prop-bounds.json.gz",
}


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode("utf-8")


def digest_file(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def read_json(path):
    opener = gzip.open if path.suffix == ".gz" else open
    with opener(path, "rt", encoding="utf-8-sig") as f:
        return json.load(f)


def sectors_from_csv(path, world=1):
    out = {}
    with path.open(newline="", encoding="utf-8-sig") as f:
        for r in csv.DictReader(f):
            if int(r["metr_id"]) != world:
                continue
            sec = int(r["sector"])
            box = [float(r[k]) for k in ("min_x", "max_x", "min_z", "max_z")]
            assert sec not in out and all(math.isfinite(v) for v in box)
            assert box[0] < box[1] and box[2] < box[3]
            out[sec] = box
    assert out
    return out


def overlap(a, box):
    # a is [minX,minY,minZ,maxX,maxY,maxZ], box is [minX,maxX,minZ,maxZ].
    return a[0] <= box[1] and a[3] >= box[0] and a[2] <= box[3] and a[5] >= box[2]


class SectorIndex:
    """Exact intersection accelerated by METR's native 1,000-unit grid."""
    def __init__(self, sectors):
        self.sectors = sectors
        self.cells = collections.defaultdict(set)
        for sec, (x0, x1, z0, z1) in sectors.items():
            for x in range(math.floor(x0 / 1000), math.floor(x1 / 1000) + 1):
                for z in range(math.floor(z0 / 1000), math.floor(z1 / 1000) + 1):
                    self.cells[x, z].add(sec)

    def query(self, a):
        candidates = set()
        for x in range(math.floor(a[0] / 1000), math.floor(a[3] / 1000) + 1):
            for z in range(math.floor(a[2] / 1000), math.floor(a[5] / 1000) + 1):
                candidates.update(self.cells.get((x, z), ()))
        return sorted(s for s in candidates if overlap(a, self.sectors[s]))


def normalized(kind, identity_fields, bounds, key=None, goid=None, evidence_fields=None):
    assert len(bounds) == 6 and all(math.isfinite(v) for v in bounds)
    assert all(bounds[k] <= bounds[k + 3] for k in range(3))
    assert key is None or isinstance(key, int) and 0 <= key <= 0xffffffff
    assert goid is None or isinstance(goid, int) and 0 < goid <= 0xffff
    # Evidence is scoped to this source row and its profile. A different distant
    # file/corpus hash does not change an unchanged scene's input fingerprint.
    evidence = hashlib.sha256(canonical([kind, PROFILES[kind], identity_fields, bounds, evidence_fields])).hexdigest()
    identity = kind + ":" + evidence
    return [identity, key, goid, *bounds, evidence]


def corpus_rows(kind, q, go_catalog=None):
    if kind == "go":
        assert not q["unknown"]
        cols = q["columns"]
        expected = ["sector", "staticId", "goid", "x", "y", "z", "qx", "qy", "qz", "qw", "minX", "minY", "minZ", "maxX", "maxY", "maxZ"]
        assert cols[:16] == expected
        for r in q["records"]:
            assert len(r) == len(cols)
            # Inline GOIN and its direct parent remain positive source records
            # when a separate primary mesh or secondary minimap is unavailable.
            # Keep their authored bounds conservatively. No other unsupported
            # transform reason is admitted by this profile.
            assert set(r[25]) <= {"missing-primary-prefab", "missing-secondary-resource"}
            assert r[0] <= 0x7ff and r[1] <= 0xfffff
            key = (r[0] << 20) | r[1]
            layer = q["sourceLayers"][r[17]]
            template = go_catalog[r[2]]
            yield normalized(kind, [r[0], r[1], r[16], layer["path"], *r[18:]], r[10:16], key, r[2],
                             [r[:10], layer["sha256"], template["recordSha256"]])
    elif kind == "osid":
        assert q["columns"][19:25] == ["worldMinX", "worldMinY", "worldMinZ", "worldMaxX", "worldMaxY", "worldMaxZ"]
        for r in q["records"]:
            assert len(r) == len(q["columns"])
            yield normalized(kind, r[:5], r[19:25], evidence_fields=r[-1])
    elif kind == "ipi":
        assert q["columns"][19:25] == ["minX", "minY", "minZ", "maxX", "maxY", "maxZ"]
        for r in q["records"]:
            assert len(r) == len(q["columns"])
            yield normalized(kind, r[:5], r[19:25], evidence_fields=r[-1])
    elif kind in ("sid8", "epi"):
        assert q["fields"] == ["identity", "sourceSector", "minX", "minY", "minZ", "maxX", "maxY", "maxZ"]
        if kind == "epi":
            assert not q["unknown"]
        for r in q["rows"]:
            assert len(r) == 8
            yield normalized(kind, r[:2], r[2:8])
    else:
        raise ValueError(kind)


def write_gzip_json(path, payload):
    # Empty filename plus mtime=0 prevents machine/path/time-dependent gzip headers.
    with path.open("wb") as raw:
        with gzip.GzipFile(filename="", fileobj=raw, mode="wb", mtime=0, compresslevel=6) as f:
            f.write(canonical(payload) + b"\n")


def pack(input_dir, sectors_csv, output_dir, selected=None):
    sectors = sectors_from_csv(sectors_csv)
    index = SectorIndex(sectors)
    targets = set(sectors) if selected is None else set(selected)
    assert targets <= set(sectors)
    bucket = {sec: [] for sec in targets}
    inventory = {}
    counts = {}
    incomplete = collections.defaultdict(list)
    catalog_path = input_dir / "mxo-open-floor-all-slums-gob-bounds-catalog.json"
    cat = read_json(catalog_path)
    go_catalog = {r["goid"]: r for r in cat["templates"]}
    for kind, name in INPUTS.items():
        path = input_dir / name
        q = read_json(path)
        inventory[kind] = dict(file=name, sha256=digest_file(path), profile=PROFILES[kind])
        if kind == "go":
            for row in q["unavailableLayers"]:
                incomplete[row["sector"]].append(row)
        if kind == "ipi":
            # The primary exporter exposes the absent declared SIDI in its
            # ledger; the GO manifest already carries the same source refusal.
            assert q.get("unknown", []) == []
        n = 0
        for row in corpus_rows(kind, q, go_catalog):
            n += 1
            for sec in index.query(row[3:9]):
                if sec in targets:
                    bucket[sec].append(row)
        counts[kind] = n
        del q
        gc.collect()
        print(f"{kind}: {n} source bounds packaged", flush=True)
    output_dir.mkdir(parents=True, exist_ok=True)
    files = {}
    for sec in sorted(targets):
        # Exact duplicate normalized occurrences may collapse. Conflicting
        # variants have different immutable identities and remain obstacles.
        rows = sorted({r[0]: r for r in bucket[sec]}.values(), key=lambda r: r[0])
        payload = dict(format=1, world=1, sector=sec, coverage=sectors[sec],
                       complete=not incomplete[sec], rows=rows, profiles=PROFILES,
                       unsupportedLayers=incomplete[sec], allHeights=True)
        path = output_dir / f"{sec}.json.gz"
        write_gzip_json(path, payload)
        files[str(sec)] = dict(file=path.name, sha256=digest_file(path), rows=len(rows),
                               complete=payload["complete"], coverage=sectors[sec])
    manifest = dict(format=1, world=1, profiles=PROFILES, clientDllSha256=DLL_SHA,
                    inputCorpora=inventory, sourceRowCounts=counts,
                    catalogSha256=digest_file(catalog_path),
                    sectorsCsvSha256=digest_file(sectors_csv),
                    packerSha256=digest_file(Path(__file__)), sectors=files,
                    qualification="Audited authored static spatial bounds and structural NAV are separate inputs. This corpus is not a physics/GPU or whole-renderer certification.",
                    policy="Every exact XZ sector/AABB overlap at all heights; all source-conflict variants. Missing declared layers explicitly refuse their scene; no unsafe legacy placement fallback.")
    (output_dir / "manifest.json").write_bytes(json.dumps(manifest, sort_keys=True, indent=2, allow_nan=False).encode() + b"\n")
    return manifest


def main():
    a = argparse.ArgumentParser(description=__doc__)
    a.add_argument("--inputs", type=Path, required=True, help="Directory of audited primary profile exports")
    a.add_argument("--sectors", type=Path, required=True, help="data/sector_properties.csv")
    a.add_argument("--output", type=Path, required=True, help="data/mission_open_floor/1")
    a.add_argument("--only", help="Optional comma-separated sectors for an explicitly partial development pack")
    args = a.parse_args()
    result = pack(args.inputs, args.sectors, args.output,
                  None if args.only is None else [int(x) for x in args.only.split(",")])
    print(f"Wrote {len(result['sectors'])} sector packs")


if __name__ == "__main__":
    main()
