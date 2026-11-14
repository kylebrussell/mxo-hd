import math
from pathlib import Path
import struct
import sys
import unittest
sys.path.insert(0,str(Path(__file__).resolve().parent))
import mission_open_floor_world_bounds as B

class BoundsTests(unittest.TestCase):
    def test_compact_signed_truncation_and_union(self):
        self.assertEqual(0,B.compact_bound(-.1,-9))
        self.assertEqual(-110,B.compact_bound(-101,-9))
        self.assertEqual(-90,B.compact_bound(-101,9))
        lo,hi=B.prop_world_bounds([-.1,-.1,-.1,.1,.1,.1])
        self.assertLessEqual(lo[0],-.1);self.assertGreaterEqual(hi[0],.1)
        with self.assertRaises(ValueError):B.compact_bound(400000,9)
    def test_all_quarterturns_pivot_and_scin_no_double_prop_position(self):
        for r,expected in [(0,(100,0,300)),(1,(300,0,-100)),(2,(-100,0,-300)),(3,(-300,0,100))]:
            lo,hi=B.prop_world_bounds([100,0,300,100,0,300],(2,3,4),r,(1000,0,2000))
            expected=[expected[0]+1020,30,expected[2]+2040]
            self.assertTrue(all(a<=v<=b for a,v,b in zip(lo,expected,hi)))
    def test_model_registration_fractional_negative_and_invalid(self):
        lo,hi=B.model_world_bounds((10.25,-20.75,30.5),(0,0,0,1),(2,3,4))
        self.assertEqual([8,-24,26],lo);self.assertEqual([13,-17,35],hi)
        for h in [(0,1,1),(-1,1,1),(math.inf,1,1)]:
            with self.assertRaises(ValueError):B.model_world_bounds((0,0,0),(0,0,0,1),h)
    def test_root_buffer_table_exact_eof_truncation_version_and_offsets(self):
        d=bytearray(b'OSID'+struct.pack('<III',0x115,0,16))
        for _ in range(10):d+=struct.pack('<II',0,len(d)+8)
        self.assertEqual(10,len(B.osid_buffers(d)))
        for bad in [d[:-1],d+b'x',d[:4]+struct.pack('<I',0x114)+d[8:]]:
            with self.assertRaises(ValueError):B.osid_buffers(bad)
    def test_prop_count_tail_and_nonfinite_refuse(self):
        record=struct.pack('<I13f16sII',1,1,2,3,0,0,0,1,-1,-2,-3,1,2,3,b'',0,1)+bytes(72)
        r=B.Reader(record);p=B.prop_records(r,1);r.eof();self.assertEqual(152,p[0]['bytes'])
        self.assertEqual([1,2,3],p[0]['position']);self.assertEqual([-1,-2,-3,1,2,3],p[0]['rawBounds'])
        with self.assertRaises(ValueError):B.prop_records(B.Reader(record[:-1]),1)
        bad=bytearray(record);struct.pack_into('<f',bad,4,math.nan)
        with self.assertRaises(ValueError):B.prop_records(B.Reader(bad),1)
    def test_go_exact_count_offsets_and_extra_bytes(self):
        row=struct.pack('<II3f4f6f3IH',3,5,0,0,0,0,0,0,1,*([0]*6),0,0,0,0)
        data=struct.pack('<II',0,2)+row+row
        self.assertEqual([8,82],[off for off,_ in B.go_records(data)])
        for bad in [data[:-1],data+b'x']:
            with self.assertRaises(ValueError):B.go_records(bad)
    def test_sidi_inline_props_after_26_byte_links_with_opaque_tail(self):
        rid=0x38000001
        header=struct.pack('<I9hHHIH',rid,0,0,0,0,0,0,20,20,20,1,0,0,0)
        prop=struct.pack('<I13f16sII',2,0,0,0,0,0,0,1,-10,-10,-10,10,10,10,b'',0,0)
        d=struct.pack('<I',1)+header+struct.pack('<II',0,1)+bytes(26)+struct.pack('<II',1,0)+prop+b'tail'
        p=B.inline_placements(d,{rid:1})[0]
        self.assertEqual(1,len(p['props']));self.assertEqual(4,p['opaqueNonObstacleTailBytes'])
        self.assertEqual(26+4+32+4+4+8,p['props'][0]['offset'])
        with self.assertRaises(ValueError):B.inline_placements(d[:100],{rid:1})

if __name__=='__main__':unittest.main()
