import unittest
import tempfile
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parent))
import mission_open_floor_anchors as A
class AnchorAssociationTests(unittest.TestCase):
    def native(self,goid=1444):
        q='0000000000000000000000000000803f';n=(1,127,7,127<<20|7,goid,10.,20.,30.,q,123,-1,8)
        return {(127,7):((goid,10.,20.,30.,q),n)}
    def legacy(self,goid=1444):return {(127,7):(1,127,7,goid,40.,165.,60.,'000000000000803f0000000000000000','false')}
    def test_exact_independent_legacy_and_native_poses_preserved(self):
        rows,c=A.associate(self.native(),set(),self.legacy(),set(),[])
        self.assertEqual(1,c['accepted']);self.assertEqual(16,len(rows[0]));self.assertEqual((10.,20.,30.),rows[0][5:8]);self.assertEqual((40.,165.,60.),rows[0][9:12]);self.assertEqual(-1,rows[0][14])
    def test_native_conflict_legacy_conflict_and_missing_whole_sector_refuse(self):
        for nc,lc,missing in [({(127,7)},set(),[]),(set(),{(127,7)},[]),(set(),set(),[{'sector':127}])]:
            self.assertEqual([],A.associate(self.native(),nc,self.legacy(),lc,missing)[0])
    def test_type_mismatch_missing_association_and_exterior_refuse(self):
        self.assertEqual([],A.associate(self.native(),set(),self.legacy(1412),set(),[])[0])
        self.assertEqual([],A.associate(self.native(),set(),{},set(),[])[0])
        legacy=self.legacy();legacy[(127,7)]=(*legacy[(127,7)][:-1],'true')
        self.assertEqual([],A.associate(self.native(),set(),legacy,set(),[])[0])
    def test_mainframe_furniture_and_other_goids_are_not_filtered_by_concrete_proxy(self):
        for goid in (1407,1408,1410,1412,1415,1444,1448,573):
            self.assertEqual(goid,A.associate(self.native(goid),set(),self.legacy(goid),set(),[])[0][0][4])
    def test_actual_csv_identity_width_and_raw_pose_conflicts(self):
        header='metr_id,sector_id,mxoId,staticId,type,exterior,pos_x,pos_y,pos_z,rotation,quat\n'
        row='1,127,0700f007,07000000,a4050000,False,40,165,60,0,0000000000000000000000000000803f\n'
        with tempfile.TemporaryDirectory() as directory:
            path=Path(directory)/'legacy.csv';path.write_text(header+row)
            found,conflicts=A.legacy_rows([path]);self.assertEqual(1444,found[(127,7)][3]);self.assertFalse(conflicts)
            path.write_text(header+row+row.replace(',40,165,60,',',41,165,60,'))
            self.assertEqual({(127,7)},A.legacy_rows([path])[1])
            for bad in [row.replace('0700f007','0800f007'),row.replace('a4050000','a405000000')]:
                path.write_text(header+bad)
                with self.assertRaises(ValueError):A.legacy_rows([path])
if __name__=='__main__':unittest.main()
