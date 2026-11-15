"""Literal prefix/layout parsing regressions; no client, database or generated snapshot required."""
import json
import struct
import tempfile
import unittest
from pathlib import Path
import mission_open_floor_gob_catalog as exporter


# Hand-documented v7 literal: one1410 template at52/66, storage94/16, authored extents98/12,
# attrs118, components124, layouts143, types175. Layout names are not used as property aliases.
LITERAL = bytes.fromhex(
    '0000000000000000000000000000000007000000760000007c0000008f000000af00000001000000'
    '8205000034000000420000000100000047000000000000000001000000000000000006000000'
    '10000000000000000000000010000000000000000000803f00000040000040400000000000000000'
    '010000000000010000000100050000004d6f64656c060000000100000001000400000013000000'
    '00000c00000000000e00000004000b00000000000000')


class ModelCatalogTests(unittest.TestCase):
    def row(self, data=LITERAL):
        return exporter.catalog(data, [1410])['templates'][0]

    def test_literal_native_slot_and_exact_twelve_bytes(self):
        row = self.row()
        self.assertEqual((52, 66, 94, 16), (row['recordOffset'], row['recordSize'], row['storageOffset'], row['storedSize']))
        self.assertEqual([{'name': 'Model', 'type': 6}], row['components'])
        model = row['models'][0]
        self.assertEqual([1.0, 2.0, 3.0], model['halfExtents'])
        self.assertEqual((3, 11, 12, 98), (model['halfExtentsPropertySlot'], model['halfExtentsType'], model['halfExtentsWidth'], model['halfExtentsSpan']['offset']))
        self.assertEqual('0000803f0000004000004040', model['halfExtentsRawHex'])
        self.assertEqual(0, model['descriptionRid'])  # Description0 remains conservatively included.
        self.assertEqual('PositiveFiniteAuthoredBounds', row['status'])

    def test_component_type_not_label_or_alias_identifies_model(self):
        data = bytearray(LITERAL)
        data[134:139] = b'Alias'
        self.assertEqual('PositiveFiniteAuthoredBounds', self.row(data)['status'])
        struct.pack_into('<I', data, 139, 8)
        row = self.row(data)
        self.assertEqual('Model6Absent', row['status'])
        self.assertEqual([{'name': 'Alias', 'type': 8}], row['components'])
        self.assertEqual([], row['models'])

    def test_unstored_bytes_never_become_zero_or_positive_defaults(self):
        data = bytearray(LITERAL)
        struct.pack_into('<I', data, 90, 4)  # Tail still contains float patterns; declared stored span ends before them.
        model = self.row(data)['models'][0]
        self.assertEqual('MissingOrInvalidBounds', model['status'])
        self.assertIsNone(model['halfExtents'])
        self.assertIsNone(model['halfExtentsRawHex'])
        self.assertIsNone(model['halfExtentsSpan'])

    def test_zero_negative_and_nonfinite_bounds_are_explicit_unknowns(self):
        for literal in ('00000000', '000080bf', '0000807f', '0000c07f'):
            with self.subTest(literal=literal):
                data = bytearray(LITERAL)
                data[98:102] = bytes.fromhex(literal)
                row = self.row(data)
                self.assertEqual('MissingOrInvalidBounds', row['status'])
                self.assertTrue(row['models'][0]['halfExtentsRawHex'].startswith(literal))
                json.dumps(row, allow_nan=False)  # Nonfinite data retained as raw bytes, never nonstandard JSON numbers.

    def test_malformed_sections_spans_and_native_binding_refuse(self):
        changes = [(16, 8), (20, 999999), (44, 0), (48, 999999), (78, 8)]
        for offset, value in changes:
            with self.subTest(offset=offset):
                data = bytearray(LITERAL)
                struct.pack_into('<I', data, offset, value)
                with self.assertRaises(exporter.GobFormatError):
                    self.row(data)
        data = bytearray(LITERAL)
        struct.pack_into('<I', data, 167 + 2, 12)  # HalfExtents must have native type11, not guessed width24/type12.
        with self.assertRaises(exporter.GobFormatError):
            self.row(data)
        for length in (0, 39, 100, 175, 178):
            with self.subTest(length=length), self.assertRaises(exporter.GobFormatError):
                self.row(LITERAL[:length])

    def test_missing_template_is_not_empty_geometry(self):
        result = exporter.catalog(LITERAL, [1410, 1411])
        self.assertEqual({'goid': 1411, 'status': 'MissingTemplate'}, result['templates'][1])
        self.assertEqual(1, result['counts']['MissingTemplate'])

    def test_goid_paths_lists_and_canonical_dedup(self):
        self.assertEqual([3, 1410], exporter.parse_goids('1410,0x3,1410'))
        self.assertEqual(list(range(1000)), exporter.parse_goids(','.join(str(i) for i in range(1000))))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'ids.json'
            path.write_text('{"goids":[1410,3,1410]}')
            self.assertEqual([3, 1410], exporter.parse_goids(str(path)))
            for payload in ('[]', '[true]', '[1.5]', '[2147483648]', '{}'):
                path.write_text(payload)
                with self.subTest(payload=payload), self.assertRaises(ValueError):
                    exporter.parse_goids(str(path))

    def test_order_determinism_and_record_span_hashes(self):
        a = exporter.catalog(LITERAL, [1411, 1410, 1410])
        b = exporter.catalog(LITERAL, [1410, 1411])
        self.assertEqual(a, b)
        self.assertEqual(exporter.sha(LITERAL[52:118]), a['templates'][0]['recordSha256'])
        self.assertEqual(exporter.sha(LITERAL[94:110]), a['templates'][0]['storedSpanSha256'])
        self.assertEqual(exporter.sha(bytes.fromhex('0000803f0000004000004040')), a['templates'][0]['models'][0]['halfExtentsSpan']['sha256'])


if __name__ == '__main__':
    unittest.main()
