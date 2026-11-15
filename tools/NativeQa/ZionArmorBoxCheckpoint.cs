using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
namespace NativeQa;

/// <summary>One immutable earned capture, not a newly earned chain or a placement repair.</summary>
public static class ZionArmorBoxCheckpoint
{
    public const string Mode="zion-armor-box-checkpoint",SourcePin="MXO_ZION_ARMOR_SOURCE_SHA256";
    public const string PayloadSha256="349F3FA5F90F4323829F279271958A51CDBB1F3BEAB20A6B22AD5944C457031D";
    public const string RawCaseSha256="6C1674B6AEA3F699A13D4958DDF93D315B4F696D771DF022F815C1540777CF1F";
    public const string Identity="d57dd5e3a9e9470cba8b369e6bb93eb8";
    public static int[] EarlierReceipts=>new[]{1201,1204,1203,1205,1206,1207,1208,2301,2302,2303,2304,2305,2306,2307,2308,2309,2310,101101,101102,101103,101201,101202,101203,101204,101205,101206,101207,101208,101209,101210,101211,101212,101213,101214,101215};
    static byte[] Resource(string suffix)
    {
        var assembly=typeof(ZionArmorBoxCheckpoint).Assembly;string name=assembly.GetManifestResourceNames().Single(n=>n.EndsWith(suffix,StringComparison.Ordinal));
        using var stream=assembly.GetManifestResourceStream(name)!;using var bytes=new MemoryStream();stream.CopyTo(bytes);return bytes.ToArray();
    }
    public static byte[] Payload=>Resource("zion-armor-box.earned-checkpoint.json");
    public static MissionProgress.SavedState Verified()
        =>Verify(Payload,Resource("zion-armor-box.manifest.json"));
    public static MissionProgress.SavedState Verify(byte[] payload,byte[] manifest)
    {
        if(Convert.ToHexString(SHA256.HashData(payload))!=PayloadSha256)throw new InvalidDataException("Earned checkpoint bytes differ");
        using var m=JsonDocument.Parse(manifest);var r=m.RootElement;
        if(r.GetProperty("Mode").GetString()!=Mode||r.GetProperty("PayloadSha256").GetString()!=PayloadSha256||r.GetProperty("RawCaseSha256").GetString()!=RawCaseSha256||
            r.GetProperty("Mission").GetInt32()!=101216||r.GetProperty("Phase").GetInt32()!=3||r.GetProperty("CurrentMissionReceiptSeeded").GetBoolean()||!r.GetProperty("NoPayloadEdits").GetBoolean())throw new InvalidDataException("Earned checkpoint manifest differs");
        var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,new JsonSerializerOptions{IncludeFields=true})??throw new InvalidDataException("Missing earned state");
        if(s.Version!=2||s.Mission!=101216||s.Phase!=3||s.Identity!=Identity||s.Started!=1794736946||s.Items.Count!=0||s.ElevatorWitnesses?.Length!=1||s.Objects.Count!=1||s.Objects[0].Goid!=548||s.Objects[0].OpenFloor?.SourceKey!=0x0d1000d0)throw new InvalidDataException("Wrong earned state identity");
        return s;
    }
    public static void RequireSource(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Exact "+SourcePin+" required");}
    public static void RequireOwnedSchema(string schema)
    {if(!schema.StartsWith(ThrowawayDatabase.Prefix,StringComparison.Ordinal)||schema.Length<=ThrowawayDatabase.Prefix.Length||schema.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c=='_')))throw new InvalidDataException("Owned disposable schema required");}
    public static string SeedSql()
    {
        Verified();string hex=Convert.ToHexString(Payload);
        return "CREATE TABLE IF NOT EXISTS char_active_missions(charId INT UNSIGNED NOT NULL PRIMARY KEY,payload LONGTEXT NULL,receipt LONGTEXT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"+
            "DELETE FROM char_active_missions WHERE charId=110;INSERT INTO char_active_missions(charId,payload,receipt) VALUES(110,CONVERT(0x"+hex+" USING utf8mb4),NULL);"+
            "DELETE FROM char_missions WHERE charId=110;INSERT INTO char_missions(charId,missionId) VALUES"+string.Join(",",EarlierReceipts.Select(id=>"(110,"+id+")"))+";"+
            "UPDATE characters SET x=28500,y=495,z=14400,district='slums',districtId=1,alignment=1,pvpflag=0 WHERE charId=110;";
    }
    public static string StrictUtf8Hex(string hex)
    {if(hex.Length%2!=0||hex.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid checkpoint HEX");return new UTF8Encoding(false,true).GetString(Convert.FromHexString(hex));}
    public static void RequireContext(NativeQaBootstrapContext context)
    {
        if(context.OwnedDatabase==null||context.ActualServer==null)throw new InvalidDataException("Owned live checkpoint context required");
        RequireOwnedSchema(context.OwnedDatabase.Name);
        if(context.ActualServer.DatabaseName!=context.OwnedDatabase.Name||context.ActualServer.ConfiguredAddress!=context.Address||context.ActualServer.HasExited)throw new InvalidDataException("Checkpoint server identity differs");
        RequireSource(Environment.GetEnvironmentVariable(SourcePin),context.ActualServer.SourceAssemblySha256);
        if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.ActualServer.SourceAssembly)))!=context.ActualServer.SourceAssemblySha256)throw new InvalidDataException("Actual checkpoint assembly changed");
    }
    public static ViewCreation? CurrentBody(IReadOnlyList<ViewCreation> creations,int after,ViewCreation own,MissionOpenFloorPlacement pose)
    {
        if(!ReferenceEquals(creations.LastOrDefault(v=>v.ViewId==2),own)||after<1||after>creations.Count)return null;
        int ownIndex=-1;for(int i=0;i<creations.Count;i++)if(ReferenceEquals(creations[i],own))ownIndex=i;
        if(ownIndex<0||after<=ownIndex)return null;
        return creations.Skip(after).SingleOrDefault(v=>v.Goid==548&&v.ViewId!=2&&
            v.Attributes.TryGetValue("Position",out var p)&&p.Length==24&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(p,i*8)==pose.Centre[i])&&
            v.Attributes.TryGetValue("Orientation",out var q)&&q.SequenceEqual(Convert.FromHexString(pose.Quaternion))&&
            v.Attributes.TryGetValue("MissionKey",out var key)&&key.SequenceEqual(Convert.FromHexString("FF320002")));
    }
}
