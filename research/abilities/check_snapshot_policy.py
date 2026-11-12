"""Feature PRs keep the historical Markdown snapshot; docs-only refreshes may replace it."""
import argparse
import subprocess
from pathlib import Path

SNAPSHOT='docs/ABILITY-PARITY.md'

def violation(paths, snapshot_exists=True):
    changed=set(paths)
    return SNAPSHOT in changed and (changed != {SNAPSHOT} or not snapshot_exists)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base',required=True)
    args=parser.parse_args()
    paths=subprocess.check_output(['git','diff','--no-renames','--name-only',args.base,'HEAD'],text=True).splitlines()
    if violation(paths,Path(SNAPSHOT).is_file()):
        parser.exit(1,'Feature PRs must preserve docs/ABILITY-PARITY.md from the target branch. Generate the current CI artifact; refresh the snapshot in a separate docs-only PR. See docs/ABILITY-AUDIT.md.\n')
    print('PASS: historical Markdown snapshot policy')

if __name__=='__main__': main()
