import unittest
import subprocess
import sys
import tempfile
from pathlib import Path
from research.abilities.check_snapshot_policy import violation

class SnapshotPolicyTests(unittest.TestCase):
    def test_feature_evidence_and_current_artifact_do_not_require_snapshot_update(self):
        self.assertFalse(violation(['hds/Example.cs','docs/ABILITY-PARITY.csv','artifacts/ability-audit/server_coverage.json']))
    def test_separate_snapshot_refresh_is_allowed(self):
        self.assertFalse(violation(['docs/ABILITY-PARITY.md']))
    def test_separate_coverage_or_combined_snapshot_refresh_is_allowed(self):
        self.assertFalse(violation(['research/abilities/server_coverage.json']))
        self.assertFalse(violation(['docs/ABILITY-PARITY.md','research/abilities/server_coverage.json']))

    def test_coverage_change_with_feature_or_manual_is_rejected(self):
        self.assertTrue(violation(['research/abilities/server_coverage.json','hds/Example.cs']))
        self.assertTrue(violation(['research/abilities/server_coverage.json','docs/MANUAL.md']))
        self.assertTrue(violation(['research/abilities/server_coverage.json'],coverage_exists=False))

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

    def test_cli_rejects_coverage_rename_with_runtime_change(self):
        script=Path(__file__).with_name('check_snapshot_policy.py').resolve()
        with tempfile.TemporaryDirectory(prefix='mxo-coverage-policy-') as temp:
            repo=Path(temp)
            def git(*args):
                return subprocess.check_output(['git',*args],cwd=repo,text=True,stderr=subprocess.STDOUT).strip()
            git('init','--quiet')
            original=repo/'research/abilities/server_coverage.json'
            original.parent.mkdir(parents=True)
            original.write_text('[]')
            git('add','.')
            git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','--quiet','-m','baseline')
            base=git('rev-parse','HEAD')
            git('mv','research/abilities/server_coverage.json','research/abilities/RENAMED.json')
            (repo/'runtime.cs').write_text('changed')
            git('add','.')
            git('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','--quiet','-m','rename plus feature')
            result=subprocess.run([sys.executable,str(script),'--base',base],cwd=repo,text=True,capture_output=True)
            self.assertEqual(1,result.returncode)
            self.assertIn('research/abilities/server_coverage.json',result.stderr)

    def test_generator_refuses_historical_fallback_when_current_inventory_is_missing(self):
        import shutil
        source=Path(__file__).parent
        with tempfile.TemporaryDirectory(prefix='mxo-coverage-default-') as temp:
            scripts=Path(temp)/'research/abilities'
            scripts.mkdir(parents=True)
            for name in ['build_audit.py','audit_links.py']:
                shutil.copyfile(source/name,scripts/name)
            (scripts/'server_coverage.json').write_text('[]')
            result=subprocess.run([sys.executable,str(scripts/'build_audit.py')],text=True,capture_output=True)
            self.assertEqual(2,result.returncode)
            self.assertIn('dotnet run --project tools/AbilityAudit',result.stderr)
            self.assertIn('not a current fallback',result.stderr)
            self.assertFalse((Path(temp)/'artifacts').exists())

    def test_mixed_snapshot_feature_or_other_doc_change_is_rejected(self):
        self.assertTrue(violation(['docs/ABILITY-PARITY.md','hds/Example.cs']))
        self.assertTrue(violation(['docs/ABILITY-PARITY.md','docs/MANUAL.md']))

if __name__=='__main__': unittest.main()
