import unittest
from static_layer_policy import require_explicit_omission

class StaticLayerPolicyTests(unittest.TestCase):
    def test_default_refuses_every_missing_layer(self):
        for kind in ('SID','SIDI','SIDG'):
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                require_explicit_omission([kind])

    def test_legacy_sid_flag_cannot_authorize_other_layers(self):
        require_explicit_omission(['SID'],allow_sid=True)
        for kinds in (['SIDI'],['SIDG'],['SID','SIDI']):
            with self.subTest(kinds=kinds), self.assertRaises(ValueError):
                require_explicit_omission(kinds,allow_sid=True)

    def test_new_opt_in_and_complete_worlds(self):
        require_explicit_omission([])
        require_explicit_omission(['SID','SIDI','SIDG'],allow_static_layers=True)

if __name__ == '__main__':
    unittest.main()
