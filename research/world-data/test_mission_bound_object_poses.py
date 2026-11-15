import struct
import unittest
import mission_bound_object_poses as P


class BoundPositionTests(unittest.TestCase):
    def test_original_326_record_has_native_bounds_centre_not_origin_or_145(self):
        g = (326, 418, 3565., 2., 840., 0., 0., 0., 1., 3540., 2., 810., 3590., 162., 870., 0, 0, 0, 0)
        lo, hi = P.transformed_bounds(g, {'min': (22000., 0., 10000.)},
            {'pivot': (520, 120, 500), 'rotation': 1})
        self.assertEqual([28010., 1202., 11410.], lo)
        self.assertEqual([28070., 1362., 11460.], hi)
        self.assertEqual([28040., 1282., 11435.], P.model_position(lo, hi))
        self.assertEqual(2., g[3])

    def test_native_sum_store_rounds_before_promoting_to_double(self):
        f = lambda bits: struct.unpack('<f', struct.pack('<I', bits))[0]
        self.assertEqual(f(0x3f800002), P.model_position(
            [f(0x3f800001), 0, 0], [f(0x3f800002), 2, 2])[0])
        self.assertNotEqual((f(0x3f800001)+f(0x3f800002))*.5,
            P.model_position([f(0x3f800001), 0, 0], [f(0x3f800002), 2, 2])[0])

    def test_rejects_unknown_parent_rotation_and_invalid_bounds(self):
        for lo, hi in [([0, 0, 0], [1, 0, 1]), ([0, 0, 0], [1, float('nan'), 1])]:
            with self.assertRaises(ValueError):
                P.model_position(lo, hi)
        g = (326, 418, 1., 2., 3., 0., 0., 0., 1., 0., 0., 0., 1., 2., 3., 0, 0, 0, 0)
        with self.assertRaises(ValueError):
            P.transformed_bounds(g, {'min': (0., 0., 0.)}, {'pivot': (0, 0, 0), 'rotation': 4})


if __name__ == '__main__':
    unittest.main()
