"""Verify a fresh --families-json report against the bounded, hash-pinned capture manifest."""
import argparse,hashlib,json
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument("--report",type=Path,required=True);p.add_argument("--repo",type=Path,default=Path(__file__).resolve().parents[2])
a=p.parse_args()
manifest=json.loads((a.repo/"research/protocol/family-corpus-manifest.json").read_text())
report=json.loads(a.report.read_text(encoding="utf-8-sig"))
actual={r["File"].replace("\\","/"):r for r in report["Files"]}
assert set(actual)=={r["path"] for r in manifest["captures"]},"capture set changed"
for expected in manifest["captures"]:
 path=a.repo/expected["path"];assert hashlib.sha256(path.read_bytes()).hexdigest()==expected["sha256"],path
 row=actual[expected["path"]]
 for key in ("manageBonusTotal","manageBonusDecoded","manageBonusOpaque","protocol03Total","protocol03Complete","protocol03Partial","playerCreationRecords","namedProfiles"):
  assert row[key[0].upper()+key[1:]]==expected[key],(path,key)
 assert row["ManageBonusTotal"]==row["ManageBonusDecoded"]+row["ManageBonusOpaque"]
 assert row["Protocol03Total"]==row["Protocol03Complete"]+row["Protocol03Partial"]
for key,value in manifest["totals"].items():assert report[key]==value,(key,report[key],value)
print("PASS: 8 capture hashes; 940 native80bc; 11214 protocol03=9057 complete+2157 partial. Partial means partial.")
