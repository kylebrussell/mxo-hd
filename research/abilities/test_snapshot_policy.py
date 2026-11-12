import unittest
from research.abilities.check_snapshot_policy import violation

class SnapshotPolicyTests(unittest.TestCase):
    def test_feature_evidence_and_current_artifact_do_not_require_snapshot_update(self):
        self.assertFalse(violation(['hds/Example.cs','docs/ABILITY-PARITY.csv','research/abilities/server_coverage.json']))
    def test_separate_snapshot_refresh_is_allowed(self):
        self.assertFalse(violation(['docs/ABILITY-PARITY.md']))
    def test_mixed_snapshot_feature_or_other_doc_change_is_rejected(self):
        self.assertTrue(violation(['docs/ABILITY-PARITY.md','hds/Example.cs']))
        self.assertTrue(violation(['docs/ABILITY-PARITY.md','docs/MANUAL.md']))

if __name__=='__main__': unittest.main()
