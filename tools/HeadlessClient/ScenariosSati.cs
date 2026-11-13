using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task SatiDoorDiagnostic(ScenarioContext context)=>NativeWorldDoorDiagnostic(context,NativeWorldDoorFixtures.Sati);
    private static async Task SatiPartialWorld(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("SatiTraveller"){Level=50,Experience=(int)hds.PlayerProgression.ExperienceForLevel(50),Organization=1,
            X=111600,Y=695,Z=-6130,Items=new[]{(3,47726u,1)}}))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"actual Zion sender",TimeSpan.FromSeconds(20));
        p.SendRpc("63 03 6a");await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");
        var load=await p.WaitForRpcAsync("06");
        Check.Equal(18u,BitConverter.ToUInt32(load.Bytes,3),"retail construct18, not server map9");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive_sati/sati.metr"),"actual Sati resource");
        bool IsFallback(ViewCreation v)=>v.Goid==38271 && v.Attributes.TryGetValue("Position",out var at) && at.Length==24 && BitConverter.ToDouble(at)==14225 && BitConverter.ToDouble(at,8)==95 && BitConverter.ToDouble(at,16)==-4925;
        p.SendRpc("05");await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Any(IsFallback),"generated returner separate from Sati arrival",TimeSpan.FromSeconds(30));
        Check.Equal(9L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"map9 saved independently");
        Check.Equal("14225 95 -5225",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"OURS interior platform arrival; exact retail arrival unknown");
        var returner=p.ViewsOfGoid(38271).Last(IsFallback);int selfSpawns=p.World.CreationSnapshot().Count(v=>v.ViewId==2);
        var self=p.World.CreationSnapshot().Last(v=>v.ViewId==2);
        Check.Equal((byte)1,self.Attributes["OrganizationID"][0],"actual self-view organization matches Zion returner");
        Check.That(!returner.Attributes.ContainsKey("HalfExtents"),"No arbitrary hit-volume override; native default bounds retained");
        Check.That(returner.Attributes["Orientation"].SequenceEqual(Convert.FromHexString("0000000000000000000000000000803f")),"Unchanged identity orientation");
        // This direct-seeded helper only observes wire movement; the traveller alone entered through the real book.
        var witness=(await context.AddPlayersInWorldAsync(new CharacterSeed("SatiWitness") { Level=50,Experience=(int)hds.PlayerProgression.ExperienceForLevel(50),
            Organization=1,DistrictId=9,District="archive_sati",X=14225,Y=95,Z=-5525 }))[0];
        var watched=await witness.WaitForViewNamedAsync(p.Handle);
        foreach(float z in new[]{-5150f,-5075f})
        {
            await Task.Delay(500);
            byte[] position=BitConverter.GetBytes(14225f).Concat(BitConverter.GetBytes(95f)).Concat(BitConverter.GetBytes(z)).ToArray();
            p.World.SendViewList(WireFormat.FromHex("02 00 01 08").Concat(position).Concat(new byte[]{0,0}).ToArray());
            await witness.WaitForViewUpdateAsync(watched.ViewId,"01 08 "+BitConverter.ToString(position).Replace('-',' '));
        }
        string evidence=Path.Combine(Path.GetTempPath(),"mxo-sati-separated-returner-wire.json");
        await File.WriteAllTextAsync(evidence,JsonSerializer.Serialize(new { context.Server.SourceAssembly,
            Configuration=new DirectoryInfo(Path.GetDirectoryName(context.Server.SourceAssembly)!).Parent!.Name,
            DllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.Server.SourceAssembly))),
            Policy="OURS Sati-only separated fallback; native picking NOT proven",Arrival=new[]{14225,95,-5225},Proxy=new[]{14225,95,-4925},
            MapLoad=WireFormat.ToHex(load.Bytes),ReturnerGoid=returner.Goid,returner.ViewId,returner.SpawnId,
            ReturnerAttributes=returner.Attributes.ToDictionary(k=>k.Key,v=>Convert.ToHexString(v.Value)),
            SelfOrganization=Convert.ToHexString(self.Attributes["OrganizationID"]),
            ReceivedViews=p.World.ViewSnapshot().Select(Convert.ToHexString).ToArray(),
            WitnessViews=witness.World.ViewSnapshot().Select(Convert.ToHexString).ToArray() },new JsonSerializerOptions{WriteIndented=true}));
        p.SendRpc($"80 c7 {MissionNpcId(returner)} 02 00");await p.WaitForRpcAsync("81 07");
        p.SendRpc("81 08");await p.WaitForRpcAsync("81 08");var city=await p.WaitForRpcAsync("06");
        Check.That(Encoding.Latin1.GetString(city.Bytes).Contains("slums_barrens_full.metr"),"actual city resource reload");p.SendRpc("05");
        await p.WaitUntilAsync(()=>p.World.CreationSnapshot().Count(v=>v.ViewId==2)>selfSpawns,"new self after full city return",TimeSpan.FromSeconds(20));
        Check.Equal(1L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"session returns to city");
        Check.Equal("111600 695 -6130",await context.Database.ScalarAsync($"SELECT CONCAT(x,' ',y,' ',z) FROM characters WHERE charId={p.CharId}"),"exact original session position");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=47726 AND slot=106 AND count=1"),"owned reusable book retained");
        Check.Equal(0,p.World.Problems.Count,"archive wire parser");
        await NativeWorldDoorDiagnostic(context,NativeWorldDoorFixtures.Sati);
        await File.WriteAllTextAsync(Path.Combine(Path.GetTempPath(),"mxo-sati-success-debug.log"),context.Server.FullOutput()+"\n--- DebugLog ---\n"+context.Server.DebugLog());
        Console.WriteLine("  sati: owned47726/equip106; ordinary sender cycle; construct18/map9; OURS14225/95/-5225 arrival and separate38271 at14225/95/-4925; actual movement witnessed; exact session origin; book retained. No native picking/rendering/ambientNPC/LA UI proof. Evidence="+evidence);
    }
}
