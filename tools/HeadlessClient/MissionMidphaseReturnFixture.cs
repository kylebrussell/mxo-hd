using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using hds;
namespace HeadlessClient;

/// <summary>Captured native state replay only. No new earned chain or placement authority.</summary>
public static class MissionMidphaseReturnFixture
{
    public const string Name="mission-midphase-return";
    public const string SourcePin="MXO_MIDPHASE_RETURN_SOURCE_SHA256";
    public const string RawSha="30363052ED5F7847D7F89CD4DD748420537CA09944F73CCDFF7264DA125D2D12";
    public const string NormalizedSha="AAB1935022D4C1030189C8C8C9D0F379831D8A7F2CFCFAE21BECA16DF1B35EB9";
    public const string Identity="d7ae1ed76aee4d4bab40f73d1a2182d0";
    public const uint FloorKey=270532686,LobbyKey=270532869;
    public static ushort[] Abilities=>new ushort[]{2,17,1006,77};
    public static int[] EarlierReceipts=>new[]{1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207};
    public static readonly IReadOnlyDictionary<string,string> Environment=new Dictionary<string,string>{["MXO_DEV_COMMANDS"]="0",["MXO_MOB_WANDER"]="0",["MXO_MOBS"]="test",["MXO_AGGRO"]="0",["MXO_PACKET_LOG"]="1"};
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    static void Need(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    public static void RequireSource(string? expected,string actual)
    {Need(expected?.Length==64&&expected.All(Uri.IsHexDigit)&&expected.Equals(actual,StringComparison.OrdinalIgnoreCase),"Exact midphase return source pin required");}
    public static byte[] RawBatch
    {
        get{var a=typeof(MissionMidphaseReturnFixture).Assembly;using var s=a.GetManifestResourceStream(a.GetManifestResourceNames().Single(n=>n.EndsWith("mission-midphase-return.native-batch.json",StringComparison.Ordinal)))!;using var m=new MemoryStream();s.CopyTo(m);return m.ToArray();}
    }
    public static byte[] Normalize(byte[] raw)
    {
        Need(Convert.ToHexString(SHA256.HashData(raw))==RawSha,"Captured raw MySQL batch hash differs");
        // This EXACT capture doubled embedded quote escapes. Translation is fixture framing only.
        string text=new UTF8Encoding(false,true).GetString(raw).Replace(@"\\u0022",@"\u0022");
        byte[] payload=Encoding.UTF8.GetBytes(text);
        Need(Convert.ToHexString(SHA256.HashData(payload))==NormalizedSha,"Normalized fixture hash differs");
        Verify(payload);return payload;
    }
    public static byte[] Payload=>Normalize(RawBatch);
    public static MissionProgress.SavedState Verify(byte[] payload)
    {
        using var doc=JsonDocument.Parse(payload);var e=doc.RootElement;
        Need(e.EnumerateObject().Select(x=>x.Name).Distinct().Count()==e.EnumerateObject().Count(),"Duplicate checkpoint fields");
        var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,Fields)??throw new InvalidDataException("Missing checkpoint");
        Need(s.Version==2&&s.Mission==101208&&s.Phase==0&&s.Identity==Identity&&s.Started==1794793133&&s.Instance==47,"Wrong captured mission owner/phase");
        Need(s.Done.SequenceEqual(new[]{1,3,2})&&s.EverDone.SequenceEqual(new[]{1,3,2})&&!s.Done.Contains(4),"Upload4 must remain open");
        Need(s.Flags.GetValueOrDefault("entered")&&!s.Flags.GetValueOrDefault("waitingForExit")&&!s.Flags.GetValueOrDefault("failed"),"Wrong captured flags");
        Need(s.Items.Count==1&&s.Items.TryGetValue(6831,out ushort slot)&&slot==6&&s.Objects.Count==0,"Exact captured CD bag ownership");
        Need(s.Following.Length==0,"No following actor can be injected into this replay");
        var plan=MissionNpcInteriorRide.ForSite(s)??throw new InvalidDataException("Missing captured NPC measured entry");
        Need(plan.Floor.Key==FloorKey&&plan.Lobby.Key==LobbyKey&&plan.Floor.Position.SequenceEqual(new[]{3700d,1345,13300})&&plan.Lobby.Position.SequenceEqual(new[]{3700d,145,13300})&&
            plan.Floor.Standing!.SequenceEqual(new[]{3700d,1295,13100})&&plan.Lobby.Standing!.SequenceEqual(new[]{3700d,95,13100}),"Wrong captured measured doors/standing");
        Need(EarlierReceipts.Length==27&&EarlierReceipts.Distinct().Count()==27&&!EarlierReceipts.Contains(s.Mission),"Earlier receipt boundary");return s;
    }
    public static void RequireOwned(string schema)
    {Need(schema.StartsWith(ThrowawayDatabase.Prefix,StringComparison.Ordinal)&&schema.Length>ThrowawayDatabase.Prefix.Length&&schema.All(c=>char.IsAsciiLetterOrDigit(c)||c=='_'),"Owned disposable schema required");}
    public static string ActiveSql(uint character)
    {
        Need(character==110||character==1001,"Only explicit disposable fixture identities");
        return "CREATE TABLE IF NOT EXISTS char_active_missions(charId INT UNSIGNED NOT NULL PRIMARY KEY,payload LONGTEXT NULL,receipt LONGTEXT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"+
            $"DELETE FROM char_active_missions WHERE charId={character};INSERT INTO char_active_missions(charId,payload,receipt) VALUES({character},CONVERT(0x{Convert.ToHexString(Payload)} USING utf8mb4),NULL);";
    }
    public static string NativeSql()=>ActiveSql(110)+
        "DELETE FROM char_missions WHERE charId=110;INSERT INTO char_missions(charId,missionId) VALUES"+string.Join(",",EarlierReceipts.Select(id=>$"(110,{id})"))+";"+
        "DELETE FROM inventory WHERE charId=110 AND (slot=6 OR goid=6831);INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,6831,6,0,0);"+
        "UPDATE characters SET x=3700,y=1295,z=13100,level=50,healthC=5000,healthM=5000,innerStrC=1000,innerStrM=1000,district='slums',districtId=1,alignment=1,pvpflag=0 WHERE charId=110;";
    public static string StrictHex(string hex)
    {Need(hex.Length>0&&hex.Length%2==0&&hex.All(Uri.IsHexDigit),"Missing/malformed checkpoint HEX");return new UTF8Encoding(false,true).GetString(Convert.FromHexString(hex));}
    public static MissionProgress.SavedState ReadHex(string hex)=>JsonSerializer.Deserialize<MissionProgress.SavedState>(StrictHex(hex),Fields)??throw new InvalidDataException("Missing active mission");
    public static void RequireUnchanged(MissionProgress.SavedState s)
    {
        // Validate exact captured non-mob state; current actor presentation/state can legitimately refresh on reconnect.
        Verify(JsonSerializer.SerializeToUtf8Bytes(s,Fields));var expected=Verify(Payload);
        foreach(string field in new[]{"SiteDoors","Objects","OpenFloorCurrentSiteIndex","OpenFloorSites","NpcInterior","Waypoints","Exits","Items","Done","EverDone"})
        {
            var a=JsonSerializer.SerializeToElement(expected,Fields).GetProperty(field);var b=JsonSerializer.SerializeToElement(s,Fields).GetProperty(field);
            Need(a.GetRawText()==b.GetRawText(),"Captured field changed: "+field);
        }
        var before=JsonSerializer.SerializeToElement(expected,Fields).GetProperty("Mobs").EnumerateArray().Select(m=>m.GetProperty("Identity").GetRawText()).Order().ToArray();
        var after=JsonSerializer.SerializeToElement(s,Fields).GetProperty("Mobs").EnumerateArray().Select(m=>m.GetProperty("Identity").GetRawText()).Order().ToArray();
        Need(before.SequenceEqual(after)&&after.Distinct().Count()==after.Length,"No duplicated/replaced captured actors");
    }
    public sealed record Arrival(int TeleportLine,ushort Sequence,int AckLine,ushort Ack,ushort AckBits,double[] Standing);
    public static bool ExactTeleport(byte[] section,double[] standing)=>section.Length==28&&section.AsSpan(0,4).SequenceEqual(new byte[]{2,0,1,0x1c})&&
        Enumerable.Range(0,3).All(i=>BitConverter.ToSingle(section,12+i*4)==(float)standing[i]);
    // Exact bytes are supplied ONLY after FreshTeleport qualified the current-cursor/current-own-life frame.
    // Mirrored from reviewed MissionPublicStreetWire.Ack; byte matching here locates its datagram, not a position.
    public static Arrival RequireArrival(string packetLog,uint character,int afterLine,NpcInteriorComputerState.Teleport arrival)
    {
        byte[] raw=Convert.FromHexString(arrival.Raw);
        Need(raw.Length==28&&raw.AsSpan(0,4).SequenceEqual(Convert.FromHexString("0200011C")),"Exact typed arrival required");
        var rows=InteriorAdmissionDeliveryProof.Parse(packetLog,character).Where(p=>p.Line>afterLine).ToArray();
        bool Carries(InteriorAdmissionDeliveryProof.Packet p)
        {
            if(!p.Server||p.Bytes.Length==0)return false;int at=(p.Bytes[0]&0x80)!=0?5:1;
            if(p.Bytes.Length<=at+2||p.Bytes[at]!=3||p.Bytes[^1]!=0||p.Bytes[^2]!=0)return false;
            var body=p.Bytes[(at+1)..^2];
            for(int i=0;i+raw.Length<=body.Length;i++)if(body.AsSpan(i,raw.Length).SequenceEqual(raw))return true;
            return false;
        }
        var ride=rows.FirstOrDefault(Carries)??throw new InvalidDataException("Typed arrival missing from retained current-character wire");
        var ack=rows.FirstOrDefault(p=>!p.Server&&p.Line>ride.Line&&InteriorAdmissionDeliveryProof.ExactAck(ride.Sequence,p.Sequence,p.Pss))??throw new InvalidDataException("No actual client receipt of typed arrival datagram");
        return new(ride.Line,ride.Sequence,ack.Line,ack.Sequence,ack.Pss,new[]{(double)arrival.X,arrival.Y,arrival.Z});
    }
}
