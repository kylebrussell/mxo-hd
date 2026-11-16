import json
from pathlib import Path
import struct
import unittest
import mission_bound_furniture_poses as P
class NativeFurnitureTests(unittest.TestCase):
    def test_exact_separate_family(self):
        self.assertEqual(20,len(P.GOIDS)); self.assertFalse({3,326,1401} & P.GOIDS)
        c=json.loads(Path(P.__file__).with_name('native_bound_furniture_contracts.json').read_text(encoding='utf-8'))
        self.assertEqual(P.GOIDS,{x['Goid'] for x in c['Models']}); self.assertTrue(all(x['OpenMaxDistance']==100 for x in c['Models']))
    def test_actual_1450_native_midpoint(self):
        self.assertEqual([41445.,1049.98193359375,-27900.],P.model_position([41415.80859375,1000.,-27984.],[41474.19140625,1099.9639892578125,-27816.]))
    def test_f32_store_order(self):
        f=lambda b:struct.unpack('<f',struct.pack('<I',b))[0]
        self.assertEqual(f(0x3f800002),P.model_position([f(0x3f800001),0,0],[f(0x3f800002),2,2])[0])
    def test_invalid_bounds(self):
        for lo,hi in [([0,0,0],[1,0,1]),([0,0,0],[1,float('nan'),1])]:
            with self.assertRaises(ValueError):P.model_position(lo,hi)
if __name__=='__main__':unittest.main()
