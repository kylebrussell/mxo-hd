using System.Text.RegularExpressions;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task ContainerAndCorpseLoot(ScenarioContext context)
    {
        HeadlessPlayer player = await StartFullBagMission(context, "machines_01_the_end_of_the_beginning.xml");
        await EnterMissionArea(context, player);
        const string placement = @"\[MISSION\] Object 1 \(Safe\) is a (\d+)";
        await player.WaitUntilAsync(() => Regex.IsMatch(context.Server.DebugLog(), placement),
            "Safe placement", TimeSpan.FromSeconds(15));
        ushort goid = ushort.Parse(Regex.Matches(context.Server.DebugLog(), placement).Last().Groups[1].Value);
        await player.WaitUntilAsync(() => player.ViewsOfGoid(goid).Count > 0 && player.ViewsOfGoid(599).Count > 0,
            "Safe and hostile NPC views", TimeSpan.FromSeconds(15));
        ViewCreation safe = player.ViewsOfGoid(goid).Last();
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
        await player.WaitForRpcAsync("81 19");
        await DefeatMissionNpc(player, player.ViewsOfGoid(599).Last());
        await FreeLastBagSlot(player);
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("81 20");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6799"),
            "Taking a corpse's loot leaves the previously opened safe untouched");
        Check.Equal(0, player.Received("80 a0 01 06 00 01").Count, "Corpse loot cannot complete the safe's objective");

        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 1a");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("81 20");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6799"), "Closed mission loot selection cannot grant its item");
        player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e 60 00 8f 1a 00 00");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(6799L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={player.CharId} AND slot=96"),
            "Reopening the safe selects its own remaining item");
    }

    private static async Task FullBagTutorialLoot(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("TutorialLoot")
        {
            District = "tutorial", DistrictId = 0, X = 5800, Y = -1105, Z = -10400, Level = 50,
            Items = Enumerable.Range(1, 96).Select(slot => (slot, 2568u, 1)).ToArray()
        }))[0];
        ViewCreation bob = await player.WaitForViewNamedAsync("Bob", TimeSpan.FromSeconds(15));
        player.SendChat("?weaken 1");
        player.SendRpc($"40 {MissionNpcId(bob)} 01 6b");
        await player.WaitForRpcAsync("81 19", TimeSpan.FromSeconds(30));
        await player.WaitUntilAsync(() => player.Received("80 a0 18 06 00 00").Count > 0,
            "Tutorial corpse-loot goal opens", TimeSpan.FromSeconds(15));
        player.SendRpc("81 1f");
        RpcRecord full = await player.WaitForRpcAsync("81 20");
        Check.Equal((ushort)2, BitConverter.ToUInt16(full.Bytes, 12), "Both tutorial drops remain in a full bag");
        Check.Equal(0, player.Received("80 a0 18 06 00 01").Count, "The tutorial loot goal waits for its items");

        await FreeLastBagSlot(player);
        player.SendRpc($"80 c7 {MissionNpcId(bob)} 05 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e 60 00 05 0a 00 00");
        RpcRecord partial = await player.WaitForRpcAsync("81 20");
        Check.Equal((ushort)1, BitConverter.ToUInt16(partial.Bytes, 12), "The letter remains after the revolver fits");
        Check.Equal(0, player.Received("80 a0 18 06 00 01").Count, "A partial claim keeps the tutorial loot goal open");

        player.SendRpc("5d 5f 00 01 01");
        await player.WaitForRpcAsync("5f 5f 00 01");
        player.SendRpc($"80 c7 {MissionNpcId(bob)} 05 00");
        RpcRecord reopened = await player.WaitForRpcAsync("81 19");
        Check.Equal((ushort)1, BitConverter.ToUInt16(reopened.Bytes, 16), "Reopening preserves the partial claim");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e 5f 00 89 1a 00 00");
        await player.WaitForRpcAsync("80 a0 18 06 00 01");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=2565"),
            "Reopening Bob cannot duplicate the revolver");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6793"),
            "The tutorial completes after the letter fits");
    }

    private static async Task RetainedCorpseLoot(ScenarioContext context)
    {
        HeadlessPlayer[] players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Killer")
            {
                District = "dt", DistrictId = 2, X = -146900, Y = 295, Z = 50200, Level = 50,
                Items = Enumerable.Range(1, 96).Select(slot => (slot, 2568u, 1)).ToArray()
            },
            new CharacterSeed("Observer")
            {
                District = "dt", DistrictId = 2, X = -146900, Y = 295, Z = 50200, Level = 50
            });
        HeadlessPlayer killer = players[0];
        HeadlessPlayer observer = players[1];
        await killer.WaitUntilAsync(() => killer.ViewsOfGoid(599).Count(view => view.Name == "Override Function") >= 2,
            "Two Override Functions", TimeSpan.FromSeconds(20));
        ViewCreation[] functions = killer.ViewsOfGoid(599).Where(view => view.Name == "Override Function").Take(2).ToArray();
        byte[] position = functions[0].Attributes["Position"];
        await MoveForMissionTest(observer, BitConverter.ToDouble(position, 0) + 100,
            BitConverter.ToDouble(position, 8), BitConverter.ToDouble(position, 16));
        await observer.WaitUntilAsync(() => observer.ViewsOfGoid(599).Any(view =>
                view.Attributes["Position"].SequenceEqual(position)),
            "Observer sees the first corpse's NPC", TimeSpan.FromSeconds(15));
        ViewCreation seenByObserver = observer.ViewsOfGoid(599).Last(view =>
            view.Attributes["Position"].SequenceEqual(position));

        await DefeatMissionNpc(killer, functions[0]);
        uint firstInfo = BitConverter.ToUInt32(killer.Received("81 19").Last().Bytes, 8);
        killer.SendRpc("81 1f");
        await killer.WaitForRpcAsync("81 20");
        Check.Equal(1000L + firstInfo, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={killer.CharId}"),
            "The first corpse pays its money once even with a full bag");

        observer.SendRpc($"80 c7 {MissionNpcId(seenByObserver)} 05 00");
        RpcRecord denied = await observer.WaitForRpcAsync("81 19");
        Check.Equal(0u, BitConverter.ToUInt32(denied.Bytes, 8), "Another player cannot open the killer's money");
        Check.Equal((ushort)0, BitConverter.ToUInt16(denied.Bytes, 16), "Another player cannot open the killer's items");
        observer.SendRpc("81 1f");
        await observer.WaitForRpcAsync("81 20");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={observer.CharId}"),
            "Another player cannot claim corpse contents");

        await DefeatMissionNpc(killer, functions[1]);
        uint secondInfo = BitConverter.ToUInt32(killer.Received("81 19").Last().Bytes, 8);
        killer.SendRpc($"80 c7 {MissionNpcId(functions[0])} 05 00");
        RpcRecord reopened = await killer.WaitForRpcAsync("81 19");
        Check.Equal(0u, BitConverter.ToUInt32(reopened.Bytes, 8), "Reopening the first corpse cannot pay its money again");
        Check.That(BytePattern.IndexOf(reopened.Bytes, WireFormat.FromHex("16 bc 00 00"), 0) >= 0,
            "The first corpse retains its Function Data after another kill");
        await FreeLastBagSlot(killer);
        long beforeClaim = await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={killer.CharId}");
        killer.SendRpc("81 1f");
        await killer.WaitForRpcAsync("5e 60 00 16 bc 00 00");
        await killer.WaitForRpcAsync("81 20");
        Check.Equal(1L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={killer.CharId} AND goid=48150"),
            "The retained first-corpse drop fits into the freed slot");
        Check.Equal(beforeClaim, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={killer.CharId}"),
            "The first corpse still cannot duplicate its money");

        killer.SendRpc($"80 c7 {MissionNpcId(functions[1])} 05 00");
        RpcRecord second = await killer.WaitForRpcAsync("81 19");
        Check.Equal(secondInfo, BitConverter.ToUInt32(second.Bytes, 8), "The second corpse keeps its own money");
        killer.SendRpc("81 1f");
        await killer.WaitForRpcAsync("81 20");
        Check.Equal(beforeClaim + secondInfo, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={killer.CharId}"),
            "The second corpse pays independently");
        killer.SendRpc("81 1f");
        await killer.WaitForRpcAsync("81 20");
        Check.Equal(beforeClaim + secondInfo, await context.Database.LongAsync($"SELECT cash FROM characters WHERE charId={killer.CharId}"),
            "Repeated Take All cannot pay the second corpse twice");
    }
}
