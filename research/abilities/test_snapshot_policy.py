import unittest
import subprocess
import sys
import tempfile
from pathlib import Path
from research.abilities.check_snapshot_policy import violation

class SnapshotPolicyTests(unittest.TestCase):
    def test_feature_evidence_and_current_artifact_do_not_require_snapshot_update(self):
        self.assertFalse(violation(['hds/Example.cs','docs/ABILITY-PARITY.csv','research/abilities/server_coverage.json']))
    def test_separate_snapshot_refresh_is_allowed(self):
        self.assertFalse(violation(['docs/ABILITY-PARITY.md']))
    def test_snapshot_only_deletion_is_rejected(self):
        self.assertTrue(violation(['docs/ABILITY-PARITY.md'],snapshot_exists=False))

    def test_cli_rejects_renamed_snapshot_with_runtime_change(self):
        script=Path(__file__).with_name('check_snapshot_policy.py').resolve()
        with tempfile.TemporaryDirectory(prefix='mxo-snapshot-policy-') as temp:
            repo=Path(temp)
            def git(*args):
                return subprocess.check_output(['git',*args],cwd=repo,text=True,stderr=subprocess.STDOUT).strip()
            git('init','--quiet')
            (repo/'docs').mkdir()
            (repo/'docs/ABILITY-PARITY.md').write_text('historical table')
            git('add','.')
            git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','--quiet','-m','baseline')
            base=git('rev-parse','HEAD')
            git('mv','docs/ABILITY-PARITY.md','docs/RENAMED.md')
            (repo/'runtime.cs').write_text('changed')
            git('add','.')
            git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','--quiet','-m','rename plus feature')
            result=subprocess.run([sys.executable,str(script),'--base',base],cwd=repo,text=True,capture_output=True)
            self.assertEqual(1,result.returncode)
            self.assertIn('preserve docs/ABILITY-PARITY.md',result.stderr)

    def test_mixed_snapshot_feature_or_other_doc_change_is_rejected(self):
        self.assertTrue(violation(['docs/ABILITY-PARITY.md','hds/Example.cs']))
        self.assertTrue(violation(['docs/ABILITY-PARITY.md','docs/MANUAL.md']))

if __name__=='__main__': unittest.main()
