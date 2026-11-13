"""Actual shipped NAV regression cases, independent of production placement."""
import json
from pathlib import Path
import unittest

from box_nav_audit import audit


class ActualNavCases(unittest.TestCase):
    def test_four_explicit_contexts(self):
        here = Path(__file__).resolve().parent
        cases = json.loads((here/'box_walkability_cases.json').read_text(encoding='utf8'))
        output = audit(here.parents[1],cases)
        expected = ((31730,1240,-6850),(6855,840,15070),(25100,2040,14100),(24250,2040,15470))
        for case, entry, centre in zip(cases['cases'],output['results'],expected):
            with self.subTest(case=case['name']):
                r = entry['result']
                self.assertEqual('Plan',r['status'])
                for a,b in zip(centre,r['centre']):self.assertAlmostEqual(a,b,places=4)
                self.assertEqual(tuple(case['rawPosition']),r['rawPosition'])
                self.assertEqual(case['rawQuaternion'],r['rawQuaternion'])
                self.assertEqual('MissingExplicitRootEvidence',entry['withoutExplicitRoot']['reason'])
                self.assertEqual(r['surfaceY']+40,r['centre'][1])
                self.assertEqual(r['surfaceY']+95,r['approach'][1])
                # Independent half-plane check on every returned footprint
                # corner, not the planner's inside()/erode() helpers.
                vertices=r['surfaceVertices']
                for point in r['baseFootprintXZ']:
                    for a,b in zip(vertices,vertices[1:]+vertices[:1]):
                        side=(b[0]-a[0])*(point[1]-a[1])-(b[1]-a[1])*(point[0]-a[0])
                        self.assertGreaterEqual(side,-1e-6)
                self.assertEqual(r['root']['surface'],r['routePolygons'][0])
                self.assertEqual(r['surface'],r['routePolygons'][-1])
                self.assertTrue(all(p[1]==r['surfaceY']+95 for p in r['route']))
                self.assertEqual(64,len(entry['source']['navSha256']))
        # Source phase2's FALSE Project provisional output is not the root.
        self.assertNotEqual([6715,895,15005],output['results'][1]['result']['root']['standing'])
        # Proven open-floor control requires no horizontal relocation.
        self.assertEqual(0,output['results'][2]['result']['displacementXZ'])


if __name__ == '__main__':unittest.main()
