import importlib.util
import struct
import unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location("followthrough",Path(__file__).with_name("ignored_followthrough.py"))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

class DecodeTests(unittest.TestCase):
    def test_time_is_unsigned_tick_word_not_server_clock(self):
        self.assertEqual(m.decode_rpc(bytes.fromhex("81 37 ff ff ff ff")),{"opcode":"81 37","TimeRequested":0xffffffff})
    def test_range_preserves_two_raw_words_without_units_or_authority(self):
        self.assertEqual(m.decode_rpc(bytes.fromhex("81 46 ff ff ff ff 01 00 00 80")),{"opcode":"81 46","Range":0xffffffff,"Range2":0x80000001})
    def test_interrupted_data_is_not_promoted_to_cast_token(self):
        self.assertEqual(m.decode_rpc(bytes.fromhex("81 aa 78 56 34 12 ff")),{"opcode":"81 aa","UserData":0x12345678,"Interrupted":255})
    def test_hovercraft_embedded_submessage_offsets(self):
        self.assertEqual(m.decode_rpc(bytes.fromhex("80 89 07 02 03 78 56 34 12")),{"opcode":"80 89","Submessage":7,"TransactionCode":2,"HovercraftSlot":3,"Data":0x12345678})
    def test_construct_requests_preserve_distinct_ids(self):
        self.assertEqual(m.decode_rpc(bytes.fromhex("80 f6 01 00 00 00 02 00 00 00")),{"opcode":"80 f6","FactionID":1,"ConstructID":2})
        self.assertEqual(m.decode_rpc(bytes.fromhex("80 ef ff 80")),{"opcode":"80 ef","Firewall":255,"Defence":128})
    def test_no_field_requests_require_exact_size(self):
        for raw in [b"\x3b",b"\x81\x55",b"\x81\x56"]:
            self.assertEqual(len(m.decode_rpc(raw)),1)
            with self.assertRaises(ValueError):m.decode_rpc(raw+b"\0")
    def test_all_bounded_shapes_reject_truncation_and_trailing_bytes(self):
        for raw in [bytes.fromhex(x) for x in ["81 37 00 00 00 00","81 46 00 00 00 00 00 00 00 00","81 aa 00 00 00 00 00","80 f2 00 00 00 00","80 f6 00 00 00 00 00 00 00 00","80 ef 00 00","80 89 00 00 00 00 00 00 00"]]:
            for n in range(len(raw)):
                with self.assertRaises(ValueError):m.decode_rpc(raw[:n])
            with self.assertRaises(ValueError):m.decode_rpc(raw+b"\0")
    def test_unknown_record_is_not_interpreted(self):
        with self.assertRaises(ValueError):m.decode_rpc(bytes.fromhex("81 91 00 00 00 00"))

if __name__=="__main__":unittest.main()
