"""Exercise the evidence gate and delta report used by the generated audit."""
import json
from pathlib import Path
import tempfile
import unittest

from parity_progress import Evidence, summarize


class ParityProgressTests(unittest.TestCase):
    def load(self, document):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp)
            (path / 'example.json').write_text(json.dumps(document), encoding='utf-8')
            return Evidence.load(path, {149})

    def test_done_needs_runtime_checks_and_a_reviewed_native_family(self):
        review = {'family': 'fixture-example', 'checks': {}}
        document = {'abilities': {'149': review}}
        pending = {'behavior-tests': 'check every identified behavior'}
        evidence = self.load(document)
        status, remaining = evidence.evaluate(149, 'partial', pending)
        self.assertEqual('partial', status)
        self.assertIn('native-family', remaining)
        self.assertIn('behavior-tests', remaining)

        review['checks']['behavior-tests'] = {'source': 'catalog entry', 'runtime': 'entry point', 'test': 'test case'}
        status, remaining = self.load(document).evaluate(149, 'partial', pending)
        self.assertEqual('partial', status)
        self.assertEqual({'native-family'}, set(remaining))
        document['families'] = {'fixture-example': {
            'representative': 149, 'result': 'pass', 'head': 'abcdef0', 'native': 'https://example.com/native-test'
        }}
        evidence = self.load(document)
        self.assertEqual(('done', {}), evidence.evaluate(149, 'partial', pending))
        self.assertEqual('missing', evidence.evaluate(149, 'missing', pending)[0])
        self.assertEqual('cosmetic', evidence.evaluate(149, 'cosmetic', pending)[0])
        self.assertEqual('inaccessible', evidence.evaluate(149, 'inaccessible', pending)[0])
        self.assertEqual('partial', evidence.evaluate(149, 'partial', {**pending, 'controls': 'open'})[0])

    def test_family_requires_a_pass_and_the_representative_on_the_same_family(self):
        native = {'representative': 149, 'result': 'fail', 'head': 'abcdef0', 'native': 'https://example.com/comment'}
        document = {'families': {'family': native}, 'abilities': {'149': {'family': 'different'}}}
        with self.assertRaisesRegex(ValueError, 'native PASS'):
            self.load(document)
        native['result'] = 'pass'
        with self.assertRaisesRegex(ValueError, 'must be assigned'):
            self.load(document)

    def test_delta_counts_status_transitions_and_remaining_types(self):
        rows = [['149', '', '', '', '', '', '', 'partial', 'stats: a; script: b']]
        baseline = 'id,status,remaining\n149,cosmetic,consumer: c; script: b\n'
        report = summarize(rows, baseline)
        self.assertIn('Rows moved: cosmetic -> partial: 1', report)
        self.assertIn('consumer 1 -> 0 (-1)', report)
        self.assertIn('stats 0 -> 1 (+1)', report)
        self.assertNotIn('script 1 -> 1', report)
        self.assertIn('baseline predates remaining-item tracking', summarize(rows, 'id,status\n149,cosmetic\n'))
