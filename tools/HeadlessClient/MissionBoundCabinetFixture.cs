using hds;
using System.Globalization;
using System.Text;
namespace HeadlessClient;

public static class MissionBoundCabinetFixture
{
    public sealed record InventoryProof(uint Character,uint Goid,ushort Slot,uint RawCount);
    public static string DecodeCheckpointHex(string hex)
    {
        if(string.IsNullOrEmpty(hex)||hex.Length%2!=0||hex.Any(c=>!Uri.IsHexDigit(c)))
            throw new InvalidDataException("Checkpoint must be nonempty HEX(payload)");
        try{return new UTF8Encoding(false,true).GetString(Convert.FromHexString(hex));}
        catch(DecoderFallbackException error){throw new InvalidDataException("Checkpoint is not UTF8",error);}
    }
    public static InventoryProof RequireMissionInventory(string rows,uint character,uint goid,ushort slot)
    {
        var lines=rows.TrimEnd('\r','\n').Split('\n');
        if(lines.Length!=1)throw new InvalidDataException("Exactly one nonstackable mission inventory row is required");
        var fields=lines[0].TrimEnd('\r').Split('\t');
        if(fields.Length!=4||!uint.TryParse(fields[0],NumberStyles.None,CultureInfo.InvariantCulture,out uint owner)||
            !uint.TryParse(fields[1],NumberStyles.None,CultureInfo.InvariantCulture,out uint item)||
            !ushort.TryParse(fields[2],NumberStyles.None,CultureInfo.InvariantCulture,out ushort savedSlot)||
            !uint.TryParse(fields[3],NumberStyles.None,CultureInfo.InvariantCulture,out uint rawCount)||
            owner!=character||item!=goid||savedSlot!=slot||rawCount!=0)
            throw new InvalidDataException("Mission inventory owner/item/slot/raw-count differs from the earned nonstackable row");
        return new(owner,item,savedSlot,rawCount);
    }
    public const string Name="mission-bound-filing-cabinet",SourcePin="MXO_BOUND_OBJECT_SOURCE_SHA256";
    public const int Mission=201203;
    public const uint NativeKey=0x0d1001a2;
    public const string Quaternion="00000000F304353F00000000F304353F";
    public static double[] Centre=>new[]{28040d,1282d,11435d};
    public static double[] Standing=>new[]{28160d,1295d,11435d};
    public static ushort[] Abilities=>new ushort[]{2,17,1006,77};
    public static int[] Prerequisites=>new[]{986,1201,1204,1203,1205,1206,1207,1208,2201,2202,2203,2204,2205,2206,2207,2208,2209,2210,201101,201102,201103,201201,201202};
    public static string SeedPrerequisites(long character)=>"INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",Prerequisites.Select(i=>$"({character},{i})"));
    public static Dictionary<string,string> Environment=>new(){["MXO_DEV_COMMANDS"]="1",["MXO_MISSION_SITE_QA_SEED"]="201202",["MXO_PACKET_LOG"]="1",
        ["MXO_MOBS"]="test",["MXO_MOB_WANDER"]="0",["MXO_AGGRO"]="0"};
    public static void RequireSource(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Bound-object proof requires exact "+SourcePin);}
    public static void RequireOriginal(MissionProgress.SavedState state)
    {
        Check.Equal(Mission,state.Mission,"Ordinary Hard Time selection");Check.Equal(0,state.Phase,"Original authored first phase");
        var obj=state.Objects.Single(o=>o.Id==1);var raw=obj.Spot;
        Check.That(obj.Goid==326&&obj.OpenFloor==null&&MissionSites.Goid(raw)==326&&raw.mxoStaticId==NativeKey&&raw.sectorID==209&&raw.staticId==418&&
            raw.pos_x==30765&&raw.pos_y==1347&&raw.pos_z==14160&&raw.rot==90&&raw.quat.Equals("0000000000000000000000000000803f",StringComparison.OrdinalIgnoreCase),
            "Normal chooser retained the exact original bound326 source, not a substituted/forced source");
    }
    public static byte NpcLevel(IReadOnlyDictionary<string,byte[]> attributes)
    {if(!attributes.TryGetValue("Level",out var level)||level.Length!=1)throw new InvalidDataException("NPC Level is exactly one byte");return level[0];}
    public static ViewCreation? Body(IEnumerable<ViewCreation> views,int cursor)=>views.Skip(cursor).GroupBy(v=>v.ViewId).Select(g=>g.Last()).LastOrDefault(v=>
        v.Goid==326&&v.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(p,i*8)==Centre[i])&&
        v.Attributes.TryGetValue("Orientation",out var q)&&Convert.ToHexString(q)==Quaternion);
    public static string RequireBoundRecord(IEnumerable<byte[]> sections,ViewCreation view)
    {
        foreach(var section in sections)
        for(int i=0;i+12<section.Length;i++)
        {
            if(section[i]!=1||section[i+1]!=0||section[i+2]!=8||BitConverter.ToUInt16(section,i+3)!=326||BitConverter.ToUInt32(section,i+5)!=NativeKey||section[i+9]!=view.SpawnId||section[i+10]!=0xcd||section[i+11]!=0xab)continue;
            int at=i+12;
            if(WireFormat.TryReadCreationAttributes(section,326,ref at,out _)&&at+3<=section.Length&&BitConverter.ToUInt16(section,at)==view.ViewId&&section[at+2]==0)
                return Convert.ToHexString(section);
        }
        throw new ScenarioFailure("Actual bound03 must retain exact native static key/view/spawn; no free326 substitute");
    }
    // Actual BuildMissionItemAdd:5e +slot2 +GOID4 +element1 +instance2 +key1 +quantity1.
    public static bool MissionItemGrant(byte[] packet,uint item,ushort instance)=>packet.Length==12&&packet[0]==0x5e&&
        BitConverter.ToUInt32(packet,3)==item&&BitConverter.ToUInt16(packet,8)==instance&&packet[10]==0x82&&packet[11]==1;
    public static string[] LootIdentities(byte[] packet,bool accepted)
    {
        int countAt=accepted?12:16,itemsAt=countAt+2;
        if(packet.Length<itemsAt||packet[0]!=0x81||packet[1]!=(accepted?0x20:0x19))throw new InvalidDataException("Wrong loot record");
        int count=BitConverter.ToUInt16(packet,countAt);if(packet.Length!=itemsAt+count*8)throw new InvalidDataException("Malformed loot identity array");
        return Enumerable.Range(0,count).Select(i=>Convert.ToHexString(packet,itemsAt+i*8,8)).ToArray();
    }
}
