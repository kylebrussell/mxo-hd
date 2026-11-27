using System.Text.RegularExpressions;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CorpseLootUiReopen(ScenarioContext context)
    {
        var p=(await context.AddPlayersInWorldAsync(new CharacterSeed("CorpseUi"){Level=50,X=24499,Y=495,Z=-1498,Health=5000,MaxHealth=5000}))[0];
        var first=await p.WaitForViewNamedAsync("WinterFrost");var second=await p.WaitForViewNamedAsync("Trainman");
        var ui=new CorpseLootWindowState();
        int cursor=p.World.RpcSnapshot().Count;
        await DefeatMissionNpc(p,first);
        foreach(var rpc in p.World.RpcSnapshot().Skip(cursor))ui.Receive(rpc.Bytes);
        Check.That(ui.Visible,"First corpse creates a visible container window");
        ui.CloseLocally();p.SendRpc("81 1a");p.SendRpc("81 48");await p.WaitForRpcAsync("81 49");
        cursor=p.World.RpcSnapshot().Count;
        await DefeatMissionNpc(p,second);
        foreach(var rpc in p.World.RpcSnapshot().Skip(cursor))ui.Receive(rpc.Bytes);
        Check.That(ui.Visible,"Second corpse reactivates the locally closed cached window");
        p.SendRpc("81 1f");await p.WaitForRpcAsync("81 20");
        p.SkipReceived();p.SendRpc($"80 c7 {MissionNpcId(second)} 05 00");await p.WaitForRpcAsync("81 19");
        p.SendRpc("81 1f");await p.WaitForRpcAsync("81 20");
    }

    private static async Task CorpseWindowFourKills(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("CorpseFour")
            {
                Level = 50, X = 19403, Y = -705, Z = 60401, Health = 5000, MaxHealth = 5000
            },
            new CharacterSeed("CorpseWitness")
            {
                Level = 50, X = 19403, Y = -705, Z = 40000, Health = 5000, MaxHealth = 5000
            });
        var p = players[0];
        var witness = players[1];
        ViewCreation[] targets = Array.Empty<ViewCreation>();
        await p.WaitUntilAsync(() =>
        {
            targets = p.ViewsOfGoid(599)
                .Where(v => v.Name == "Chopper Runner"
                    && v.Attributes.TryGetValue("Level", out var lvl) && lvl.Length == 1 && lvl[0] == 1
                    && v.Attributes.TryGetValue("Health", out var hp) && hp.Length == 2 && BitConverter.ToUInt16(hp) > 0
                    && v.Attributes.TryGetValue("Position", out var pos) && pos.Length == 24)
                .OrderBy(v =>
                {
                    byte[] pos = v.Attributes["Position"];
                    double dx = BitConverter.ToDouble(pos, 0) - 19403d;
                    double dz = BitConverter.ToDouble(pos, 16) - 60401d;
                    return dx * dx + dz * dz;
                })
                .Take(4)
                .ToArray();
            return targets.Length == 4;
        }, "four nearby Chopper Runners", TimeSpan.FromSeconds(20));

        var ui = new CorpseLootWindowState();
        byte[] deathSignature = WireFormat.FromHex("fd 07 00 00");
        ushort previousCombatView = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            var target = targets[i];
            byte[] pos = target.Attributes["Position"];
            if (i != 1)
            {
                await MoveForMissionTest(p, BitConverter.ToDouble(pos, 0), BitConverter.ToDouble(pos, 8), BitConverter.ToDouble(pos, 16) - 100);
            }
            else
            {
                // Keep CSR at Kill #1's stand so Kill #2 exercises a direct out-of-range 0x40 engage request:
                // verify the server confirms a real combat session and tears down CombatView cleanly rather than
                // stranding the client in a phantom combat state.
                byte[] prevPos = targets[0].Attributes["Position"];
                double gapM = Math.Sqrt(Math.Pow(BitConverter.ToDouble(pos, 0) - BitConverter.ToDouble(prevPos, 0), 2)
                    + Math.Pow(BitConverter.ToDouble(pos, 16) - (BitConverter.ToDouble(prevPos, 16) - 100), 2)) / 100.0;
                Check.That(!hds.CombatHandler.InMeleeReach((float)gapM),
                    $"Kill #2 starts from outside melee reach ({gapM:0.00} m)");
            }
            int rpcCursor = p.World.RpcSnapshot().Count;
            p.SkipReceived();
            p.SendChat("?weaken 1");
            p.SendRpc($"40 {MissionNpcId(target)} 01 6b");

            await p.WaitForRpcAsync($"40 {MissionNpcId(target)} 00 6b", TimeSpan.FromSeconds(10));
            ushort combatView = await WaitForCombatView(p, previousCombatView);
            previousCombatView = combatView;
            var combatEnd = await p.WaitForRpcAsync($"46 {MissionNpcId(target)} 00", TimeSpan.FromSeconds(30));
            int creationCursor = p.World.CreationSnapshot().Count;
            if (i == 2)
            {
                // Force a live 0x03 view-creation packet into the 0.25 s window between CombatEnd and 81 1a / 81 19.
                witness.SendChat(FormattableString.Invariant(
                    $"?tp {BitConverter.ToDouble(pos, 0)} {BitConverter.ToDouble(pos, 8)} {BitConverter.ToDouble(pos, 16) - 150} 0 0 0"));
            }
            await p.WaitForViewUpdateAsync(1, $"01 01 00 {Le(combatView)}", TimeSpan.FromSeconds(10));
            var lootWindow = await p.WaitForRpcAsync("81 19", TimeSpan.FromSeconds(10));
            var killRpcs = p.World.RpcSnapshot().Skip(rpcCursor).ToArray();
            var lootClose = killRpcs.LastOrDefault(r => r.Bytes.Length == 2 && r.Bytes[0] == 0x81 && r.Bytes[1] == 0x1a);
            Check.That(lootClose != null, $"Kill #{i + 1}: 81 1a precedes 81 19");
            Check.That(lootWindow.DatagramViewSection == null && lootClose!.DatagramViewSection == null,
                $"Kill #{i + 1}: 81 1a / 81 19 went out in an RPC-only datagram isolated from 0x03 view bursts");
            if (i == 2)
            {
                await p.WaitUntilAsync(() => p.World.CreationSnapshot().Skip(creationCursor).Any(v => v.Name == "CorpseWitness"),
                    "Kill #3: concurrent view creation delivered cleanly alongside deferred loot open", TimeSpan.FromSeconds(5));
            }
            Check.That(combatEnd.DatagramViewSection == null || BytePattern.IndexOf(combatEnd.DatagramViewSection, deathSignature, 0) < 0,
                $"Kill #{i + 1}: SendNpcDies went out before SendCombatEnd (not coalesced with 46 00)");
            Check.That(combatEnd.DatagramSequence.HasValue && lootWindow.DatagramSequence.HasValue && lootClose!.DatagramSequence.HasValue,
                $"Kill #{i + 1}: datagram sequences recorded");
            int seqDelta = (lootWindow.DatagramSequence!.Value - combatEnd.DatagramSequence!.Value) & 0x0FFF;
            Check.That(seqDelta > 0 && seqDelta < 2048,
                $"Kill #{i + 1}: 81 19 arrived in a later datagram than 46 00 (end={combatEnd.DatagramSequence}, loot={lootWindow.DatagramSequence})");
            int closeDelta = (lootClose!.DatagramSequence!.Value - combatEnd.DatagramSequence!.Value) & 0x0FFF;
            Check.That(closeDelta > 0 && closeDelta < 2048,
                $"Kill #{i + 1}: 81 1a arrived in a later datagram than 46 00 (end={combatEnd.DatagramSequence}, close={lootClose.DatagramSequence})");
            Check.Equal(0, p.World.Problems.Count, $"Kill #{i + 1}: zero protocol/bargeguar problems");

            foreach (var rpc in killRpcs) ui.Receive(rpc.Bytes);
            Check.That(ui.Visible, $"Kill #{i + 1}: Corpse window is visible after combat teardown");

            if (i % 2 == 0)
            {
                p.SendRpc("81 1f");
                await p.WaitForRpcAsync("81 20");
                ui.CloseLocally();
            }
            else
            {
                ui.CloseLocally();
                p.SendRpc("81 1a");
            }
        }
    }

    private static async Task ContainerAndCorpseLoot(ScenarioContext context)
    {
        HeadlessPlayer player = await StartFullBagMission(context, "machines_01_the_end_of_the_beginning.xml");
        var evidence=new List<object>();
        var saved=await MervFollowupCheckpoint(context,player);
        var mission=System.Xml.Linq.XElement.Load(Path.Combine(context.RepositoryRoot,"data/missions/machines_01_the_end_of_the_beginning.xml"));
        await SweepRide(context,player,saved,mission.Elements("phase").ElementAt(saved.Phase),true,evidence);
        async Task OpenSafe()
        {
            var safe=await MervFollowupObject(context,player,1,0,evidence);
            player.SendRpc($"80 c7 {MissionNpcId(safe)} 03 00");
            await player.WaitForRpcAsync("81 19");
        }
        await OpenSafe();
        await DefeatMissionNpc(player, player.ViewsOfGoid(599).Last());
        await FreeLastBagSlot(player);
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("81 20");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6799"),
            "Taking a corpse's loot leaves the previously opened safe untouched");
        Check.Equal(0, player.Received("80 a0 01 06 00 01").Count, "Corpse loot cannot complete the safe's objective");

        await OpenSafe();
        player.SendRpc("81 1a");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("81 20");
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6799"), "Closed mission loot selection cannot grant its item");
        await OpenSafe();
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
        Check.Equal((ushort)0, BitConverter.ToUInt16(full.Bytes, 12), "Full bag acknowledges no claimed identities");
        Check.Equal(0, player.Received("80 a0 18 06 00 01").Count, "The tutorial loot goal waits for its items");

        await FreeLastBagSlot(player);
        player.SendRpc($"80 c7 {MissionNpcId(bob)} 05 00");
        await player.WaitForRpcAsync("81 19");
        player.SendRpc("81 1f");
        await player.WaitForRpcAsync("5e 60 00 05 0a 00 00");
        RpcRecord partial = await player.WaitForRpcAsync("81 20");
        Check.Equal((ushort)1, BitConverter.ToUInt16(partial.Bytes, 12), "The successfully claimed revolver is removed from the loot UI");
        Check.Equal(2565u, BitConverter.ToUInt32(partial.Bytes, 18), "Partial acknowledgement identifies the revolver, not the unclaimed letter");
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
