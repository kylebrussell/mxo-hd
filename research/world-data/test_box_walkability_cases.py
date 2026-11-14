"""Actual shipped NAV regression cases, independent of production placement."""
import json
from pathlib import Path
import unittest

from box_nav_audit import audit
from box_route_test_oracle import route_is_clear


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
                self.assertTrue(route_is_clear(r['route'],r['baseFootprintXZ']))
                self.assertEqual(r['approach'],r['route'][-1])
                self.assertEqual(r['root']['surface'],r['routePolygons'][0])
                self.assertEqual(r['surface'],r['routePolygons'][-1])
                self.assertTrue(all(p[1]==r['surfaceY']+95 for p in r['route']))
                self.assertEqual(64,len(entry['source']['navSha256']))
        # Source phase2's FALSE Project provisional output is not the root.
        self.assertNotEqual([6715,895,15005],output['results'][1]['result']['root']['standing'])
        # Proven open-floor control requires no horizontal relocation.
        self.assertEqual(0,output['results'][2]['result']['displacementXZ'])


    def test_four_actual_live218_contexts_with_independent_route_oracle(self):
        here=Path(__file__).resolve().parent
        cases=json.loads((here/'box_walkability_live218_cases.json').read_text())
        before=json.dumps(cases,sort_keys=True)
        output=audit(here.parents[1],cases)
        expected=((24110,3240,15120),(39929.999995785314,440,14460),
                  (30110,440,-7130),(35150,1240,-20270))
        ingress=((24200,3295,15125),(39900,495,14400),
                 (30130,495,-7000),(35130,1295,-20400))
        for c,row,centre,approach in zip(cases['cases'],output['results'],expected,ingress):
            with self.subTest(case=c['name']):
                r=row['result'];self.assertEqual('Plan',r['status'])
                for a,b in zip(centre,r['centre']):self.assertAlmostEqual(a,b,places=5)
                self.assertEqual(approach,r['approach'])
                self.assertEqual(r['approach'],r['route'][-1])
                self.assertTrue(route_is_clear(r['route'],r['baseFootprintXZ']))
                self.assertEqual(tuple(c['rawPosition']),r['rawPosition'])
                self.assertEqual(c['rawQuaternion'],r['rawQuaternion'])
                e=c['sourceEvidence'];self.assertEqual(1,e['sourceMembershipCount'])
                self.assertEqual(548,e['concreteGoid'])
                self.assertIn(e['sourceGoid'],(1412,1415))
                bounds=e['siteBounds']
                for x,z in r['baseFootprintXZ']:
                    self.assertGreaterEqual(x,bounds['minX']-1e-6)
                    self.assertLessEqual(x,bounds['maxX']+1e-6)
                    self.assertGreaterEqual(z,bounds['minZ']-1e-6)
                    self.assertLessEqual(z,bounds['maxZ']+1e-6)
                # Independently retain the exact former final segment as a
                # known bad witness, without calling any planner predicate.
                old_approach={'51e509462f-phase1-site0':(24060,3295,15200),
                              '51e509462f-phase2-site0':(39950,495,14530),
                              'cae96c70b5-phase1-site0':(30200,495,-7200)}.get(c['name'])
                if old_approach:
                    self.assertFalse(route_is_clear(r['route']+[old_approach],r['baseFootprintXZ']))
        self.assertEqual(before,json.dumps(cases,sort_keys=True))

if __name__ == '__main__':unittest.main()
