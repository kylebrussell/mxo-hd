using hds;
namespace HeadlessClient;

/// <summary>Owned, initially unloaded archive for the ordinary Open Locks load transition.
/// Only explicit prerequisite graph edges are included; no discipline/folder expansion or hidden grants.</summary>
public static class LockedSafeLoadoutFixture
{
    public static IReadOnlyList<AbilityMemory.Row> Rows()
    {
        var closure=AbilityPrerequisites.Current.Closure(142);
        if(closure.ContainsKey(142))throw new InvalidOperationException("Cyclic Open Locks fixture graph");
        var rows=new List<AbilityMemory.Row>();uint key=1;ushort slot=301;
        foreach(var parent in closure.OrderBy(p=>p.Key))
        {
            if(AbilityCatalog.Find(parent.Key)?.Number("Ability/AbilityClass")!=0)throw new InvalidOperationException("Unqualified Open Locks prerequisite");
            rows.Add(new AbilityMemory.Row(key++,slot++,0x80000000u|((uint)parent.Key<<10),checked((ushort)Math.Max(1,parent.Value)),false));
        }
        rows.Add(new AbilityMemory.Row(key,300,0x80000000u|(142u<<10),10,false)); // Selected ability last, as ordinary native batches.
        return rows.AsReadOnly();
    }
    public static string SeedSql(uint character)=>$"DELETE FROM char_abilities WHERE char_id={character}; INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES "+
        string.Join(",",Rows().Select(r=>$"({character},{r.Slot},{unchecked((int)r.Descriptor)},{r.Level},0)"));
    public static byte[] LoadRequest()
    {
        var rows=Rows();var bytes=new List<byte>{0x80,0xae,0,0,0,0,0,0,10,0};bytes.AddRange(BitConverter.GetBytes(checked((ushort)rows.Count)));
        foreach(var row in rows){bytes.Add(0);bytes.AddRange(BitConverter.GetBytes(row.Slot));bytes.AddRange(BitConverter.GetBytes((uint)row.Ability));bytes.AddRange(BitConverter.GetBytes(row.Level));}
        return bytes.ToArray();
    }
}
