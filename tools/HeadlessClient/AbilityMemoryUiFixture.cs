namespace HeadlessClient;
/// <summary>Tools-only level1 prerequisite-closed UI profile; no effect/activation claim.</summary>
public static class AbilityMemoryUiFixture
{
    public static readonly System.Collections.Generic.IReadOnlyList<ushort> Loaded = System.Array.AsReadOnly(new ushort[]{2,17,113,130,185,150,156,184,198,197,298,215,37,45});
    public static readonly System.Collections.Generic.IReadOnlyList<ushort> Known = System.Array.AsReadOnly(Loaded.Concat(new ushort[]{217,628,18,52,143}).ToArray());
    public static string SeedSql(uint character) => $"UPDATE characters SET level=1,exp=0 WHERE charId={character}; DELETE FROM char_abilities WHERE char_id={character} AND slot<520; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES "+string.Join(",",Known.Select((id,slot)=>$"({character},{slot},{0x80000000L|((long)id<<10)},1,{(slot<Loaded.Count?1:0)})"))+";";
    public sealed record Tuple(ushort Slot,uint Descriptor,ushort Level);
    public static Tuple[] Decode(byte[] bytes)
    {
        if(bytes.Length%6!=0) throw new ArgumentException("Margin archive rows must be exactly six bytes");
        return Enumerable.Range(0,bytes.Length/6).Select(i=>new Tuple(BitConverter.ToUInt16(bytes,i*6),BitConverter.ToUInt32(bytes,i*6+2)&0xfffffc00u,(ushort)(BitConverter.ToUInt32(bytes,i*6+2)&0x3ffu))).ToArray();
    }
}
