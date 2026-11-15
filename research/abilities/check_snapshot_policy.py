"""Feature PRs keep historical audit snapshots; isolated snapshot refreshes may replace them."""
import argparse
import subprocess
from pathlib import Path

SNAPSHOT='docs/ABILITY-PARITY.md'
COVERAGE_SNAPSHOT='research/abilities/server_coverage.json'
SNAPSHOTS={SNAPSHOT,COVERAGE_SNAPSHOT}

def violation(paths, snapshot_exists=True, coverage_exists=True):
    changed=set(paths)
    return bool(changed & SNAPSHOTS) and (
        not changed <= SNAPSHOTS or
        SNAPSHOT in changed and not snapshot_exists or
        COVERAGE_SNAPSHOT in changed and not coverage_exists)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base',required=True)
    args=parser.parse_args()
    paths=subprocess.check_output(['git','diff','--no-renames','--name-only',args.base,'HEAD'],text=True).splitlines()
    if violation(paths,Path(SNAPSHOT).is_file(),Path(COVERAGE_SNAPSHOT).is_file()):
        parser.exit(1,'Feature PRs must preserve docs/ABILITY-PARITY.md and research/abilities/server_coverage.json from the target branch. Generate the current CI artifact; refresh historical snapshots in a separate snapshot-only PR. See docs/ABILITY-AUDIT.md.\n')
    print('PASS: historical audit snapshot policy')

if __name__=='__main__': main()
