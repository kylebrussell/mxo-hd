import gzip
import math
from pathlib import Path
import struct
import tempfile
import unittest

from box_walkability import Root, RawPose, Surface, erode, footprint, inside, plan
from box_nav_audit import read_nav
from box_route_test_oracle import route_is_clear

IDENTITY = '0000000000000000000000000000803f'


def square(key=(1,0,0), x=0, z=0, size=400, floor=0):
    return Surface(key, ((x,z),(x+size,z),(x+size,z+size),(x,z+size)), (0,0,floor))


class PlannerTests(unittest.TestCase):
    def setUp(self):
        self.s = square()
        self.root = Root(self.s.id,(10,95,10),'literal test standing context')
        self.raw = RawPose((200,999,200),IDENTITY)

    def test_identity_grounding_distinct_from_standing_and_raw(self):
        r = plan((self.s,),self.root,self.raw)
        self.assertEqual('Plan',r['status'])
        self.assertEqual((200,40,200),r['centre'])
        self.assertEqual(95,r['approach'][1])
        self.assertEqual((200,999,200),r['rawPosition'])
        self.assertEqual(IDENTITY,r['rawQuaternion'])

    def test_ingress_distance_is_reported_without_a_use_range_filter(self):
        s=square(size=1000)
        r=plan((s,),Root(s.id,(10,95,10),'far actual root'),RawPose((800,999,800),IDENTITY))
        self.assertEqual('Plan',r['status'])
        self.assertGreater(r['approachDistanceXZ'],1000)
        self.assertAlmostEqual(math.hypot(790,790),r['approachDistanceXZ'])
        self.assertAlmostEqual(math.sqrt(790**2+790**2+55**2),r['approachDistance3D'])
        self.assertFalse(r['useRangeValidated'])
        self.assertEqual((10,95,10),r['approach'])

    def test_nearest_erosion_is_not_polygon_centre_or_grid(self):
        r = plan((self.s,),self.root,RawPose((-10,95,135.25),IDENTITY))
        self.assertEqual((50.,40,135.25),r['centre'])

    def test_yaw_preserves_raw_bytes_and_rotates_footprint(self):
        q = struct.pack('<4f',0,math.sqrt(.5),0,math.sqrt(.5)).hex()
        r = plan((self.s,),self.root,RawPose((-10,95,140),q))
        self.assertAlmostEqual(70,r['centre'][0])
        self.assertEqual(q,r['rawQuaternion'])
        self.assertTrue(all(inside(self.s.vertices,(r['centre'][0]+x,r['centre'][2]+z)) for x,z in footprint(RawPose((0,0,0),q))))

    def test_narrow_polygon_does_not_fallback_to_smaller_inset(self):
        s = Surface((1,0,0),((0,0),(99,0),(99,400),(0,400)),(0,0,0))
        self.assertEqual('NoPlan',plan((s,),self.root,self.raw)['status'])

    def test_corner_touch_and_different_height_are_not_connected(self):
        distant = square((1,0,1),400,400)
        upper = square((1,0,2),400,0,floor=.001)
        for other in (distant,upper):
            r = plan((self.s,other),self.root,RawPose((600,95,200),IDENTITY))
            self.assertEqual(self.s.id,r['surface'])
            self.assertEqual(1,r['componentPolygons'])

    def test_shared_edge_path_and_stable_tie_break(self):
        b = square((1,0,1),400,0)
        raw = RawPose((600,95,200),IDENTITY)
        a = plan((self.s,b),self.root,raw)
        self.assertEqual(a,plan((b,self.s),self.root,raw))
        self.assertEqual([self.s.id,b.id],a['routePolygons'])
        self.assertEqual((600,40,200),a['centre'])

    def test_final_ingress_replaces_literal_old_self_crossing_route(self):
        # A three-turn route enters the final square opposite the root's
        # closest boundary. The old projected approach cut through the Box.
        ss=(Surface((1,0,0),((0,0),(100,0),(100,100),(0,100)),(0,0,0)),
            Surface((1,0,1),((100,0),(400,0),(400,100),(100,100)),(0,0,0)),
            square((1,0,2),400,0),square((1,0,3),400,400),square((1,0,4),0,400))
        r=plan(ss,Root((1,0,0),(10,95,10),'literal route'),RawPose((300,999,600),IDENTITY))
        self.assertEqual('Plan',r['status'])
        self.assertEqual((300,40,600),r['centre'])
        self.assertEqual((400,95,600),r['approach'])
        self.assertEqual(r['approach'],r['route'][-1])
        old_route=r['route']+[(10,95,400)]
        self.assertFalse(route_is_clear(old_route,r['baseFootprintXZ']))
        self.assertTrue(route_is_clear(r['route'],r['baseFootprintXZ']))
        self.assertEqual(r,plan(tuple(reversed(ss)),Root((1,0,0),(10,95,10),'literal route'),RawPose((300,999,600),IDENTITY)))

    def test_root_inside_or_touching_closed_base_refuses_without_repacking(self):
        for x,z in ((200,200),(150,200),(150,130),(200,130)):
            with self.subTest(root=(x,z)):
                r=plan((self.s,),Root(self.s.id,(x,95,z),'inside/edge/corner'),self.raw)
                self.assertEqual('NoPlan',r['status'])
                self.assertEqual('NoPositiveAreaFootprintWithClearIngressRoute',r['reason'])
                self.assertEqual(self.raw.position,r['rawPosition'])

    def test_intermediate_portal_touch_refuses_candidate(self):
        # Nearest destination footprint starts on the shared edge. Its actual
        # ingress touches the closed base, even though its centre is on NAV.
        b=square((1,0,1),400,0)
        r=plan((self.s,b),self.root,RawPose((450,999,200),IDENTITY))
        # Destination candidate is refused; the remaining nearer-first valid
        # root-polygon candidate is retained rather than moving within b.
        self.assertEqual('Plan',r['status'])
        self.assertEqual(self.s.id,r['surface'])
        self.assertEqual((350.,40,200.),r['centre'])
        self.assertTrue(route_is_clear(r['route'],r['baseFootprintXZ']))

    def test_independent_oracle_rejects_crossing_tangent_and_collinear_contact(self):
        shape=((150,130),(250,130),(250,270),(150,270))
        for route in (((100,95,200),(300,95,200)),
                      ((100,95,130),(300,95,130)),
                      ((100,95,80),(150,95,130)),
                      ((200,95,200),)):
            self.assertFalse(route_is_clear(route,shape))
        self.assertTrue(route_is_clear(((100,95,100),(300,95,100)),shape))

    def test_root_validated_by_planner_not_label(self):
        for root in (None,Root(self.s.id,(10,95,10),''),Root((99,0,0),(10,95,10),'x'),
                     Root(self.s.id,(999,95,10),'x'),Root(self.s.id,(10,96,10),'x'),
                     Root(self.s.id,(math.nan,95,10),'x')):
            self.assertEqual('NoPlan',plan((self.s,),root,self.raw)['status'])

    def test_ramps_and_tilts_rejected(self):
        ramp = Surface(self.s.id,self.s.vertices,(.001,0,0))
        self.assertEqual('RootIsNotFlat',plan((ramp,),self.root,self.raw)['reason'])
        q = struct.pack('<4f',.01,0,0,math.sqrt(1-.0001)).hex()
        self.assertEqual('TiltedQuaternion',plan((self.s,),self.root,RawPose((0,0,0),q))['reason'])

    def test_bad_values_and_duplicate_ids_fail_closed(self):
        for raw in (RawPose((0,math.inf,0),IDENTITY),RawPose((0,0,0),'ff'),RawPose((0,0,0),'00'*16)):
            self.assertEqual('NoPlan',plan((self.s,),self.root,raw)['status'])
        self.assertEqual('DuplicateSurfaceIdentity',plan((self.s,self.s),self.root,self.raw)['reason'])

    def test_concave_input_refused(self):
        s = Surface(self.s.id,((0,0),(400,0),(100,100),(400,400),(0,400)),(0,0,0))
        self.assertEqual('MalformedRootSurface',plan((s,),self.root,self.raw)['reason'])

    def test_self_intersecting_star_is_not_convex(self):
        points = tuple((200+200*math.cos(i*4*math.pi/5),200+200*math.sin(i*4*math.pi/5)) for i in range(5))
        s = Surface(self.s.id,points,(0,0,0))
        self.assertEqual('MalformedRootSurface',plan((s,),self.root,self.raw)['reason'])

    def test_uncertain_nonroot_polygon_is_excluded_not_traversed(self):
        bad = Surface((1,0,1),((400,0),(800,0),(500,100),(800,400),(400,400)),(0,0,0))
        r = plan((self.s,bad),self.root,self.raw)
        self.assertEqual([bad.id],r['excludedUncertainSurfaces'])
        self.assertEqual(1,r['componentPolygons'])

    def test_exact_triangle_erosion_footprint_not_just_centre(self):
        p = ((0,0),(500,0),(0,500))
        feasible = erode(p,footprint(self.raw))
        c = plan((Surface(self.s.id,p,(0,0,0)),),self.root,RawPose((250,95,250),IDENTITY))['centre']
        self.assertAlmostEqual(380,c[0]+c[2])
        self.assertTrue(inside(feasible,(c[0],c[2])))


class AdapterTests(unittest.TestCase):
    def pack(self, version=3):
        # One flat polygon, no prefab placements, sector7.
        return (b'MXNV'+struct.pack('<HHIIHHHBf',version,1,0,1,7,1,1,4,1200)
                +struct.pack('<8i',0,0,400,0,400,400,0,400)+struct.pack('<H',0))

    def read(self,b):
        with tempfile.TemporaryDirectory() as d:
            p=Path(d)/'1.bin.gz';p.write_bytes(gzip.compress(b))
            csv=Path(d)/'sectors.csv';csv.write_text('metr_id,sector,min_x,min_z\n1,7,1000,2000\n')
            return read_nav(p,csv,1,frozenset({7}))

    def test_complete_parse_and_world_translation(self):
        ss,proof=self.read(self.pack())
        self.assertEqual(((1000.,2000.),(1400.,2000.),(1400.,2400.),(1000.,2400.)),ss[0].vertices)
        self.assertEqual((0,0,1200),ss[0].plane)
        self.assertEqual(64,len(proof['navSha256']))

    def test_truncation_trailing_unsupported_and_count(self):
        good=self.pack()
        badcount=bytearray(good);struct.pack_into('<I',badcount,12,0xffffffff)
        for b in (good[:-1],good+b'X',self.pack(9),bytes(badcount)):
            with self.assertRaises(ValueError): self.read(b)

    def test_district_mismatch_and_missing_sector_refused(self):
        raw = bytearray(self.pack());struct.pack_into('<H',raw,6,2)
        with self.assertRaises(ValueError):self.read(raw)
        raw = bytearray(self.pack());struct.pack_into('<H',raw,16,8)
        with self.assertRaises(ValueError):self.read(raw)


if __name__ == '__main__':
    unittest.main()
