import gzip
import json
import math
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import mission_open_floor_scene_pack as P


class ScenePackTests(unittest.TestCase):
    def test_positive_source_model_presence_is_not_inferred_from_missing_obstacle(self):
        q = dict(sourceGobSha256="3e47f11012f6f219a26c9f5decfa10cbc00726c397937d03c3068254e5424103",
                 generatorNormalizedSha256="1"*64, templates=[
                     dict(goid=1415, status="Model6Absent", recordSha256="2"*64, components=[dict(type=37), dict(type=5)]),
                     dict(goid=1410, status="PositiveFiniteAuthoredBounds", recordSha256="3"*64, components=[dict(type=6)])])
        with patch.object(P, "read_json", return_value=q), patch.object(P, "digest_file", return_value="4"*64):
            out = P.source_model_lookup(Path("catalog.json"))
            self.assertEqual([(x['goid'], x['status']) for x in out['entries']], [(1410, 'AuthoredStaticBounds'), (1415, 'Model6Absent')])
            q['templates'][0]['components'].append(dict(type=6))
            with self.assertRaises(AssertionError):
                P.source_model_lookup(Path("catalog.json"))
            q['templates'][0]['components'].pop()
            q['templates'][0]['status'] = 'Unknown'
            with self.assertRaises(AssertionError):
                P.source_model_lookup(Path("catalog.json"))

    def test_exact_boundary_and_remote_source_overhang(self):
        idx = P.SectorIndex({1: [-1000, 0, -1000, 0], 2: [0, 1000, -1000, 0], 3: [9000, 10000, 9000, 10000]})
        self.assertEqual(idx.query([-20, 0, -500, 20, 100, -400]), [1, 2])
        self.assertEqual(idx.query([0, 0, -500, 0, 100, -400]), [1, 2])
        self.assertEqual(idx.query([-2000, -9000, -500, 9500, 50000, 9500]), [1, 2, 3])
        self.assertEqual(idx.query([1001, 0, 1, 8999, 10, 8999]), [])

    def test_all_heights_and_zero_thickness_are_retained(self):
        q = dict(fields=["identity", "sourceSector", "minX", "minY", "minZ", "maxX", "maxY", "maxZ"],
                 rows=[["literal", 53, -6000, 0, 2000, -6000, 130, 8000], ["roof", 5, 1, 99999, 2, 3, 100000, 4]])
        rows = list(P.corpus_rows("sid8", q))
        self.assertEqual(rows[0][3:9], [-6000, 0, 2000, -6000, 130, 8000])
        self.assertEqual(rows[1][4], 99999)
        self.assertIsNone(rows[0][1])
        self.assertIsNone(rows[0][2])

    def test_source_variants_are_distinct_and_scoped_hash_is_stable(self):
        a = P.normalized("go", [1, 2, "sid"], [0, 0, 0, 10, 20, 30], 0x00100002, 1407, "source")
        b = P.normalized("go", [1, 2, "sidi"], [0, 0, 0, 10, 20, 30], 0x00100002, 1407, "source")
        c = P.normalized("go", [1, 2, "sid"], [1, 0, 0, 11, 20, 30], 0x00100002, 1407, "source")
        self.assertEqual(a, P.normalized("go", [1, 2, "sid"], [0, 0, 0, 10, 20, 30], 0x00100002, 1407, "source"))
        self.assertNotEqual(a[0], b[0])
        self.assertNotEqual(a[9], c[9])
        self.assertEqual(a[1:3], [0x00100002, 1407])

    def test_bad_geometry_and_identity_are_not_empty(self):
        for bounds in ([1, 0, 0, 0, 1, 1], [0, 0, 0, math.inf, 1, 1], [0, 0, 0, math.nan, 1, 1]):
            with self.assertRaises(AssertionError):
                P.normalized("go", [], bounds, 1, 1407)
        for key, goid in ((-1, 1), (2**32, 1), (1, 0), (1, 65536)):
            with self.assertRaises(AssertionError):
                P.normalized("go", [], [0, 0, 0, 1, 1, 1], key, goid)

    def test_gzip_is_path_and_time_independent(self):
        with tempfile.TemporaryDirectory(prefix="mxo-scene-pack-test-") as tmp:
            target = Path(tmp).resolve()
            self.assertTrue(target.is_relative_to(Path(tempfile.gettempdir()).resolve()))
            self.assertTrue(target.name.startswith("mxo-scene-pack-test-"))
            a, b = Path(tmp)/"a.gz", Path(tmp)/"different-name.gz"
            payload = dict(format=1, world=1, sector=127, complete=True, coverage=[0, 10, 0, 10], rows=[])
            P.write_gzip_json(a, payload)
            P.write_gzip_json(b, payload)
            self.assertEqual(a.read_bytes(), b.read_bytes())
            self.assertEqual(json.loads(gzip.decompress(a.read_bytes())), payload)


if __name__ == "__main__":
    unittest.main()
