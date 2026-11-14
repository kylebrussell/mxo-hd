using System.Text;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task MissionContainerDeposit(ScenarioContext context)
    {
        var player=await context.AddPlayerAsync(new CharacterSeed("Deposit") { Level=50, Items=new[] {(20,2399u,1)} });
        await player.EnterWorldAsync();
        player.SendRpc("80 94 05 00 03");
        await player.WaitForTextAsync("Container Deposit Fixture");
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(context,player);
        await ApproachMissionObjectFixture(context,player,1);
        var safe=await MissionObjectView(context,player,"Safe");
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        int slot=(int)await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={player.CharId} AND goid=6786");
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 21 14"); // wrong owned item is not accepted
        await player.WaitForRpcAsync("81 22 00 00 00 00 00 00 00 00 01 00 00 00");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6786"),"Wrong deposit preserves mission disk");
        player.SendRpc($"81 21 {slot:x2}");
        await player.WaitForRpcAsync("81 22 82 1a 00 00 ?? ?? ?? 82 00 00 00 00");
        await player.WaitForRpcAsync("80 a0 02 06 00 01");
        Check.Equal(0L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6786"),"Accepted deposit consumes the disk once");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=2399"),"Other bag item survives");
        player.SendRpc($"81 21 {slot:x2}");
        await player.WaitForRpcAsync("81 22 00 00 00 00 00 00 00 00 01 00 00 00");
        Check.Equal(0,player.Received("81 2a").Count,"Container deposits do not open the marketplace");
    }

    private static async Task DeathPenaltyPersistence(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("Penalty") {Level=50}))[0];
        p.SendChat("?die");
        await p.WaitForViewUpdateAsync(2,"02 80 80 80 c1 01 00 00 80 80 01 00 00 00 00");
        await p.WaitForRpcAsync("80 bc");
        await p.WaitUntilAsync(()=>context.Server.DebugLog().Contains("[COMBAT] Player died"),"death path",TimeSpan.FromSeconds(10));
        double saved=double.Parse(await context.Database.ScalarAsync($"SELECT factor FROM char_death_penalty WHERE charId={p.CharId}"),System.Globalization.CultureInfo.InvariantCulture);
        Check.That(saved>.849 && saved<.851,"Ordinary death stores fifteen percent loss");
        string username=await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
        using var server=await ServerInstance.StartAsync(context.RepositoryRoot,context.Database.Name,StillNpcs);
        using var again=await HeadlessPlayer.LoginAsync(server.Address,username,p.Handle);
        await again.EnterWorldAsync();
        await again.WaitUntilAsync(()=>again.World.ViewSnapshot().Any(section=> BytePattern.IndexOf(section,
            WireFormat.FromHex("02 00 02 80 80 80 80 80 08"),0)>=0),"restored penalty attribute",TimeSpan.FromSeconds(10));
        Check.That(again.Received("80 bc").Count>0,"Restored death effect is visible after a fresh server login");
        Check.Equal(0,again.World.Problems.Count,"Reloaded penalty protocol");
    }

    private static async Task PvpArchiveTransport(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("ArchiveBook") {Level=25,Organization=1,
            X=111600,Y=745,Z=-6130,Items=new[] {(3,38538u,1),(4,38537u,1)} }))[0];
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38282).Count>0,"Zion archivist view",TimeSpan.FromSeconds(15));
        var rejected=(await context.AddPlayersInWorldAsync(new CharacterSeed("ArchiveUnderlevel") {Level=19,Organization=1,
            X=111600,Y=745,Z=-6130,Items=new[] {(106,38538u,1)} }))[0];
        p.SendRpc("63 03 6a");
        await p.WaitForRpcAsync("65 03 00 6a 00");
        await p.WaitForRpcAsync("81 07",TimeSpan.FromSeconds(185));
        await rejected.WaitForTextAsync("requires level 20 through 30",TimeSpan.FromSeconds(5));
        Check.Equal(0,rejected.Received("81 07").Count,"Level gate prevents departure");
        p.SendRpc("81 08");
        await p.WaitForRpcAsync("81 08");
        var load=await p.WaitForRpcAsync("06");
        Check.Equal(11u,BitConverter.ToUInt32(load.Bytes,3),"Ashencourte uses retail construct 11");
        Check.That(Encoding.Latin1.GetString(load.Bytes).Contains("archive_ashencourte"),"Correct world resource");
        p.SendRpc("05");
        await p.WaitUntilAsync(()=>p.ViewsOfGoid(38271).Count>0,"Zion return teleporter",TimeSpan.FromSeconds(30));
        var returner=p.ViewsOfGoid(38271).Last();
        Check.Equal(6L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Legacy map index saved independently");
        int resets = p.Received("81 07").Count;
        p.SendRpc($"80 c7 {Le(returner.ViewId)} {Le((ushort)(returner.SpawnId ^ 1))} 02 00");
        p.SendRpc("81 48");
        await p.WaitForRpcAsync("81 49"); // a barrier after the forged stale handle
        Check.Equal(resets,p.Received("81 07").Count,"A stale spawn handle cannot use the returner");
        p.SendRpc($"80 c7 {MissionNpcId(returner)} 02 00");
        await p.WaitForRpcAsync("81 07");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT districtId FROM characters WHERE charId={p.CharId}"),"Return trip saved city origin");
        Check.Equal(1L,await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={p.CharId} AND goid=38538 AND slot=106"),"Book is reusable, never consumed");
    }
}
