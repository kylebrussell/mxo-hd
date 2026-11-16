using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
namespace NativeQa;
public static class ZionBluepillLobbyCheckpoint
{
    public const string Mode="zion-bluepill-lobby-checkpoint",SourcePin="MXO_ZION_BLUEPILL_LOBBY_SOURCE_SHA256";
    public const string PayloadSha256="20784EF7E2BAAFBF4EB38F765F9BE8D48F5B0B28CFB746475569AEED8D2AE872";
    static readonly JsonSerializerOptions Fields=new(){IncludeFields=true};
    public static byte[] Payload
    {
        get{var assembly=typeof(ZionBluepillLobbyCheckpoint).Assembly;using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith("zion-bluepill-lobby.native-capture.json",StringComparison.Ordinal)))!;using var bytes=new MemoryStream();stream.CopyTo(bytes);return bytes.ToArray();}
    }
    public static MissionProgress.SavedState Verify(byte[] payload)
    {
        if(Convert.ToHexString(SHA256.HashData(payload))!=PayloadSha256)throw new InvalidDataException("Verbatim native0940 capture required");
        var s=JsonSerializer.Deserialize<MissionProgress.SavedState>(payload,Fields)??throw new InvalidDataException("Missing captured state");
        if(s.Version!=2||s.Mission!=101210||s.Phase!=1||s.Instance!=49||s.Identity!="66184ccc743a4ba4a4f07f97746eecdf"||s.Started!=1794838109||s.Items.Count!=0||s.Done.Length!=0||!s.EverDone.SequenceEqual(new[]{1,2})||s.OpenFloorCurrentSiteIndex!=1||s.OpenFloorSites![1].DoorKey!=921699392)throw new InvalidDataException("Wrong captured owner/phase/site/goals");
        var current=MervFollowupStreetEntry.ForSite(s)??throw new InvalidDataException("Captured current Street context required");
        if(current.Raw.GetProperty("Phase").GetInt32()!=1||current.Lobby.Key!=921699406||!current.Lobby.Position.SequenceEqual(new[]{-2800d,145,-8300})||!current.Lobby.Standing!.SequenceEqual(new[]{-2600d,95,-8300}))throw new InvalidDataException("Captured lobby identity differs");
        return s;
    }
    public static int[] EarlierReceipts=>ZionNextFiveFixture.Prerequisites.Concat(new[]{101208,101209}).Append(986).Distinct().ToArray();
    public static void RequireSource(string? expected,string actual)
    {if(expected==null||expected.Length!=64||expected.Any(c=>!Uri.IsHexDigit(c))||!expected.Equals(actual,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Exact "+SourcePin+" required");}
    public static void RequireContext(NativeQaBootstrapContext context)
    {
        if(context.OwnedDatabase==null||context.ActualServer==null)throw new InvalidDataException("Owned actual lobby context required before seed");
        ZionArmorBoxCheckpoint.RequireOwnedSchema(context.OwnedDatabase.Name);
        var server=context.ActualServer;
        if(server.DatabaseName!=context.OwnedDatabase.Name||server.ConfiguredAddress!=context.Address||server.HasExited)throw new InvalidDataException("Actual lobby server/address differs");
        RequireSource(Environment.GetEnvironmentVariable(SourcePin),server.SourceAssemblySha256);
        if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(server.SourceAssembly)))!=server.SourceAssemblySha256)throw new InvalidDataException("Actual lobby source changed");
        Verify(Payload);
    }
    // Caller MUST pass actual owned context before this executes; no unowned plan/bootstrap SQL exists.
    public static string SeedSql(NativeQaBootstrapContext context,double[] outside)
    {
        RequireContext(context);if(outside.Length!=3||!outside.All(double.IsFinite))throw new InvalidDataException("Verified grounded outside point required");
        return "CREATE TABLE IF NOT EXISTS char_active_missions(charId INT UNSIGNED NOT NULL PRIMARY KEY,payload LONGTEXT NULL,receipt LONGTEXT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"+
            "DELETE FROM char_active_missions WHERE charId=110;INSERT INTO char_active_missions(charId,payload,receipt) VALUES(110,CONVERT(0x"+Convert.ToHexString(Payload)+" USING utf8mb4),NULL);"+
            "DELETE FROM char_missions WHERE charId=110;INSERT INTO char_missions(charId,missionId) VALUES"+string.Join(",",EarlierReceipts.Select(id=>$"(110,{id})"))+";"+
            FormattableString.Invariant($"UPDATE characters SET x={outside[0]},y={outside[1]},z={outside[2]},district='slums',districtId=1,alignment=1,pvpflag=0 WHERE charId=110;");
    }
    public static void RequireUnchanged(MissionProgress.SavedState expected,MissionProgress.SavedState actual)
    {
        var a=JsonSerializer.SerializeToElement(expected,Fields);var b=JsonSerializer.SerializeToElement(actual,Fields);
        foreach(string key in new[]{"Version","Identity","Mission","Instance","Started","Phase","Done","EverDone","Items","Following","Objects","SiteDoors","OpenFloorSites","OpenFloorCurrentSiteIndex","NpcInterior","StreetAccess","Waypoints","Exits","NpcElapsed","WaveElapsed","GoalElapsed"})
            if(a.GetProperty(key).GetRawText()!=b.GetProperty(key).GetRawText())throw new InvalidDataException("Captured lobby field changed: "+key);
        foreach(var flag in expected.Flags.Where(p=>p.Key!="entered"))if(!actual.Flags.TryGetValue(flag.Key,out var value)||value!=flag.Value)throw new InvalidDataException("Captured lobby flag changed: "+flag.Key);
        if(actual.Flags.GetValueOrDefault("failed"))throw new InvalidDataException("Captured lobby mission failed");
        MissionPublicStreetProof.RequireStableActors(expected.Mobs,actual.Mobs);
    }
    public static bool LobbyWaypoint(byte[] rpc)=>rpc.Length==35&&rpc.AsSpan(0,3).SequenceEqual(Convert.FromHexString("809E00"))&&BitConverter.ToUInt32(rpc,31)==921699406&&Enumerable.Range(0,3).All(i=>BitConverter.ToDouble(rpc,3+i*8)==new[]{-2800d,145,-8300}[i]);
}
