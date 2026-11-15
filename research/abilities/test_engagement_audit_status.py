"""Exercise the real classifier without running the artifact-writing CLI."""
import ast
from pathlib import Path
import unittest


class EngagementAuditStatusTests(unittest.TestCase):
    def classify(self, consumer):
        source = Path(__file__).with_name('build_audit.py').read_text(encoding='utf-8-sig')
        function = next(n for n in ast.parse(source).body if isinstance(n, ast.FunctionDef) and n.name == 'status')
        namespace = {
            'DATA_MINING': {}, 'PETS': {}, 'fields': lambda _: {},
            'abilities': {49: {'name': 'GaussianBlurAbility'}},
            'coverage': {49: {'buff_source': 'stored scripted', 'buff_stats': [{'stat': 520, 'consumer': consumer}],
                              'healing': False, 'direct_damage': False}},
        }
        exec(compile(ast.Module(body=[function], type_ignores=[]), 'actual build_audit.status', 'exec'), namespace)
        return namespace['status'](49)

    def test_old_inventory_without_consumer_stays_cosmetic(self):
        self.assertEqual('cosmetic', self.classify('total only'))

    def test_actual_ours_entry_consumer_is_partial_not_retail_complete(self):
        self.assertEqual('partial', self.classify('OURS deterministic new PvP interlock entry scores, only against current217'))


if __name__ == '__main__':
    unittest.main()
