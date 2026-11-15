import struct
import unittest
import mission_open_floor_aux_bounds as A


def epi_empty(rotation=0):
    h = bytearray(96)
    struct.pack_into('<I9hIIH', h, 0, 123, 10, -2, 30, 0, 0, 0, 10, 20, 30, rotation, 0, 1)
    values = b'ISPI' + struct.pack('<3I', 0x101, 4, 0xffffffff) + struct.pack('<I', 0)
    schema = b'ISHD' + struct.pack('<3I', 0x101, len(values), 0xffffffff) + values
    # links, GOIN count, PROP count/extra, NAV count, schema, extra schemas.
    return struct.pack('<I', 1) + bytes(h) + bytes(20) + schema + bytes(4)


class AuxiliaryBoundsTests(unittest.TestCase):
    def test_epi_strict_framing_and_opaque_header(self):
        q = A.epi_records(epi_empty())
        self.assertEqual(len(q), 1)
        self.assertEqual(q[0]['pivot'], [10, -2, 30])
        self.assertEqual(q[0]['props'], [])
        self.assertEqual(q[0]['gos'], [])
        for b in (epi_empty()[:-1], epi_empty()+b'x', epi_empty(4)):
            with self.assertRaises(ValueError):
                A.epi_records(b)

    def test_literal_epi_cardinal_world_transform(self):
        c = dict(pivot=[10, -2, 30], rotation=1)
        self.assertEqual(A.epi_point([4, 5, 8], c, [1000, 0, -2000]), [1108, -15, -1704])
        lo, hi = A.epi_bound([-3, -4, -5], [7, 6, 9], c, [1000, 0, -2000])
        self.assertEqual(lo, [1095, -24, -1707])
        self.assertEqual(hi, [1109, -14, -1697])

    def test_conservative_local_boundary_is_not_cast_inward(self):
        lo, hi = A.epi_bound([-15.00000017, 0, -1], [15.00000017, 1, 1],
                             dict(pivot=[0, 0, 0], rotation=0), [0, 0, 0])
        self.assertLessEqual(lo[0], -15.00000017)
        self.assertGreaterEqual(hi[0], 15.00000017)
        self.assertEqual(lo[1], 0)

    def test_sid8_zero_thickness_and_blob_are_not_static_rids(self):
        data = struct.pack('<II6fI', 1, 0x7057307e, 1, 2, 3, 0, 5, 7, 3) + b'xyz'
        q = A.sid8_records(data, [1000, 0, -2000])
        self.assertEqual(q[0]['keyRawU32'], 0x7057307e)
        self.assertEqual(q[0]['min'], [1001, -3, -2004])
        self.assertEqual(q[0]['max'], [1001, 7, -1990])
        for b in (data[:-1], data+b'x'):
            with self.assertRaises(ValueError):
                A.sid8_records(b, [0, 0, 0])
        bad = struct.pack('<II6fI', 1, 1, 1, 2, 3, -1, 2, 3, 0)
        with self.assertRaises(ValueError):
            A.sid8_records(bad, [0, 0, 0])


if __name__ == '__main__':
    unittest.main()
