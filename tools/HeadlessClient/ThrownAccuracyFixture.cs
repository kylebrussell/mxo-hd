using hds;
namespace HeadlessClient;
public static class ThrownAccuracyFixture
{
    // Full catalog prerequisite closure for normal 164 reload and ordinary SpyStyle/LegSweep; fixed across all stages.
    public static ushort[] Abilities=>new ushort[]{2,17,1112,134,113,120,121,123,130,142,143,151,152,155,185,1125,164};
    public static ushort WeakPointsSlot=>(ushort)(400+Array.IndexOf(Abilities,(ushort)164));
    public static Dictionary<string,string> Environment=>new(PvpOpponentsFixture.Environment){["MXO_DEV_COMMANDS"]="1",["MXO_PACKET_LOG"]="1"};
    public static CharacterSeed Seed(string name,bool enemy)=>new(name){X=enemy?17120:17000,Y=495,Z=2300,Level=50,
        Experience=(int)PlayerProgression.ExperienceForLevel(50),Organization=enemy?2:1,Health=20000,MaxHealth=20000,
        InnerStrength=2000,MaxInnerStrength=2000,OwnedLoadedAbilities=Abilities.Select(a=>(a,(ushort)50)).ToArray()};
    // Exact RPC47 kind6 shape from CombatInterlock.BuildRoundState, current/next move slots only; no substring scan.
    public static bool QueuedLegSweep(byte[] record)=>record.Length==47&&record[0]==0x47&&record[1]==0&&record[4]==1&&
        record[20]==0x1a&&record[21]==0&&BitConverter.ToUInt32(record,22)==6&&record[26]==1&&record[27]==0&&record[28]==0&&
        record[29]==1&&record[31]==1&&BitConverter.ToUInt32(record,35)==0x80000086u;
}
