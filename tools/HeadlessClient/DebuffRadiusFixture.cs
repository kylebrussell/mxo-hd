namespace HeadlessClient;
public static class DebuffRadiusFixture
{
    public static ushort[] Abilities => new ushort[]{2,17,1006,333,345};
    public static string TargetsSql(uint near,uint edge,uint far,uint primary) => $"UPDATE characters SET level=50,alignment=1,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,x=CASE charId WHEN {near} THEN 24879 WHEN {edge} THEN 24939 WHEN {far} THEN 24999 ELSE 24499 END,y=495,z=-1498 WHERE charId IN ({near},{edge},{far},{primary})";
}
