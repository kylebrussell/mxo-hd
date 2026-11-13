namespace HeadlessClient;
public static class UiLagAreaFixture
{
    public static ushort[] Abilities=>new ushort[]{2,17,1006,97};
    public const int X=17000,Y=495,Z=2300;
    public static string HelpersSql(uint friendly,uint outside,uint floor,uint near)=>$"UPDATE characters SET level=50,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,alignment=CASE WHEN charId={friendly} THEN 2 ELSE 1 END,x=CASE charId WHEN {near} THEN 17200 WHEN {outside} THEN 17301 ELSE 17100 END,y=CASE WHEN charId={floor} THEN 695 ELSE 495 END,z=2300,rotation=0 WHERE charId IN ({friendly},{outside},{floor},{near})";
}
