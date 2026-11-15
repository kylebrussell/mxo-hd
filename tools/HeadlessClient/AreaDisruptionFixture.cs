namespace HeadlessClient;
public static class AreaDisruptionFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,1006,38};
    public const int X=17000,Y=495,Z=2300;
    public static string HelpersSql(uint friendly,uint outside,uint floor,uint edge)=>$"UPDATE characters SET level=50,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,alignment=CASE WHEN charId={friendly} THEN 2 ELSE 1 END,x=CASE charId WHEN {edge} THEN 17800 WHEN {outside} THEN 17801 ELSE 17100 END,y=CASE WHEN charId={floor} THEN 695 ELSE 495 END,z=2300,rotation=0 WHERE charId IN ({friendly},{outside},{floor},{edge})";
}
