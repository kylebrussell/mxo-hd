namespace HeadlessClient;
public static class DebuffRadiusFixture
{
    public static ushort[] Abilities => new ushort[]{2,17,1006,333,345};
    public const int NativeCasterX=17000,NativePrimaryX=17500,NativeZ=2300,NativeNearX=17880,NativeEdgeX=17940,NativeOutsideX=17500,NativeOutsideZ=2799;
    public static string NativeTargetsSql(uint near,uint edge,uint far,uint primary)=>$"UPDATE characters SET level=50,alignment=1,pvpflag=0,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,x=CASE charId WHEN {near} THEN {NativeNearX} WHEN {edge} THEN {NativeEdgeX} WHEN {far} THEN {NativeOutsideX} ELSE {NativePrimaryX} END,y=495,z=CASE charId WHEN {far} THEN {NativeOutsideZ} ELSE {NativeZ} END,rotation=0 WHERE charId IN ({near},{edge},{far},{primary})";
    public static string TargetsSql(uint near,uint edge,uint far,uint primary) => $"UPDATE characters SET level=50,alignment=1,pvpflag=1,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,x=CASE charId WHEN {near} THEN 24879 WHEN {edge} THEN 24939 WHEN {far} THEN 24999 ELSE 24499 END,y=495,z=-1498 WHERE charId IN ({near},{edge},{far},{primary})";
    public static async Task<object> EnterFlaggedAsync(HeadlessPlayer player)
    {
        await player.EnterWorldAsync();
        await player.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 01 00 00 00 01");
        return new{Name=player.Handle,player.CharId,Self=player.World.CreationSnapshot().Last(v=>v.ViewId==2).Attributes.ToDictionary(k=>k.Key,v=>WireFormat.ToHex(v.Value)),RawViews=player.World.ViewSnapshot().Select(WireFormat.ToHex).ToArray()};
    }
}
