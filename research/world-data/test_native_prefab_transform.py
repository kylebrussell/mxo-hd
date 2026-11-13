import importlib.util
import math
import unittest
import subprocess
import sys
from pathlib import Path

spec = importlib.util.spec_from_file_location('native_prefab_transform', Path(__file__).with_name('native_prefab_transform.py'))
T = importlib.util.module_from_spec(spec)
spec.loader.exec_module(T)


class NativePrefabTransformTests(unittest.TestCase):
    def test_all_four_native_matrices_and_signed_pivot(self):
        expected = ((981.,32.,-1957.),(983.,32.,-1961.),(979.,32.,-1963.),(977.,32.,-1959.))
        for rotation, point in enumerate(expected):
            with self.subTest(rotation=rotation):
                actual, q = T.sector_pose((1,2,3),(0,0,0,1),(-2,3,4),rotation,(1000,0,-2000),parent_kind='direct-sector')
                self.assertEqual(point, actual)
                self.assertEqual(T.rotation_quaternion(rotation), q)

    def test_parent_times_tilted_local_is_not_yaw_addition_or_reverse_order(self):
        h = 0.7071067690849304
        low = 0.4999999701976776
        self.assertEqual((low,low,-low,low), T.compose_quaternion((0,h,0,h),(h,0,0,h)))
        self.assertEqual((-low,-low,low,low), T.compose_quaternion((0,-h,0,h),(0,0,h,h)))
        self.assertNotEqual(T.compose_quaternion((0,h,0,h),(h,0,0,h)),T.compose_quaternion((h,0,0,h),(0,h,0,h)))

    def test_float32_store_before_translation_and_no_normalization(self):
        point, q = T.sector_pose((.0001,0,0),(0,0,0,.5),(0,0,0),0,(100000,0,0),parent_kind='direct-sector')
        self.assertEqual((100000.,0.,0.),point)
        self.assertEqual((0.,0.,0.,.5),q)
        self.assertEqual(-327680.,T.sector_pose((0,0,0),(0,0,0,1),(-32768,0,0),0,(0,0,0),parent_kind='direct-sector')[0][0])

    def test_exact_key_city_examples_of_each_rotation(self):
        # Native-primary results corroborated by matching world/sector/staticID/GOID legacy variants.
        # Signed pivot/sector fields are deliberately separate from the server's +145 presentation offset.
        cases = (
            ((1,3,36,38338),0,(50,0,940),(20,260,370),(82000,0,-59000),(82250,2600,-54360)),
            ((1,10,916,6958),1,(700,0,0),(400,-60,320),(102000,0,-114000),(106000,-600,-111500)),
            ((1,10,121,6959),2,(3000,0,1700),(780,100,760),(102000,0,-114000),(106800,1000,-108100)),
            ((1,10,897,6958),3,(700,0,0),(400,-60,680),(102000,0,-114000),(106000,-600,-106500)),
        )
        for key,rotation,local,pivot,corner,expected in cases:
            with self.subTest(exact_native_key=key):
                self.assertEqual(expected,T.sector_pose(local,(0,0,0,1),pivot,rotation,corner,parent_kind='direct-sector')[0])
        self.assertEqual((103006.,-977.,14003.),T.sector_pose((897,23,4606),(0,0,0,1),(40,-100,690),1,(98000,0,8000),parent_kind='direct-sector')[0])
        self.assertNotEqual((99297.,-977.,10294.),T.sector_pose((897,23,4606),(0,0,0,1),(40,-100,690),1,(98000,0,8000),parent_kind='direct-sector')[0])

    def test_optin_is_limited_to_reviewed_world_and_not_historical_mode(self):
        script = Path(__file__).with_name('static_objects_small03.py')
        for args in (('--world','17','--native-prefab-rotations'),('--world','12','--native-prefab-rotations'),('--world','15','--native-prefab-rotations','--omit-unproven-rotations'),('--world','16','--native-prefab-rotations','--omit-unproven-rotations')):
            result = subprocess.run([sys.executable,str(script),'unused-tools',*args],capture_output=True,text=True)
            self.assertEqual(2,result.returncode)
            self.assertIn('reviewed world',result.stderr)

    def test_unknown_parent_nonfinite_and_invalid_rotation_are_refused(self):
        for kind in ('nested-IPI','EPI','unknown'):
            with self.assertRaises(ValueError):T.sector_pose((0,0,0),(0,0,0,1),(0,0,0),0,(0,0,0),parent_kind=kind)
        for rotation in (-1,4):
            with self.assertRaises(ValueError):T.quarter_turn((1,2,3),rotation)
        for value in (math.nan,math.inf,-math.inf):
            with self.assertRaises(ValueError):T.f32(value)
        with self.assertRaises(ValueError):T.sector_pose((0,0,0),(0,0,0,1),(32768,0,0),0,(0,0,0),parent_kind='direct-sector')


if __name__ == '__main__':
    unittest.main()
