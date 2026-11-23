using System.Xml.Linq;
using hds;

namespace HeadlessClient;
public static partial class Scenarios
{
    private static Task MachinesCriticalOpening(ScenarioContext context)=>RunPrimaChain(context,"MachinesCritical",2,
        MachinesCriticalFixture.Missions,MachinesCriticalFixture.Prerequisites,"machines_1_1_*.xml",evidenceLabel:"machines-critical",
        ownedLoadout: MachinesCriticalFixture.Abilities);

    private static async Task MissionComputerHacker(ScenarioContext context)
    {
        var p = await context.AddPlayerAsync(PrimaActorSeed("ComputerHacker", 2, 17000, 2300, 19) with
        {
            OwnedLoadedAbilities = MachinesCriticalFixture.Abilities,
            CompletedMissions = MachinesCriticalFixture.Prerequisites
                .Concat(MachinesCriticalFixture.Missions.Take(2)).ToArray()
        });
        await p.EnterWorldAsync();

        int listStart = p.World.RpcSnapshot().Count;
        p.SendRpc("80 94 05 00 02");
        await p.WaitForRpcAsync("80 97 05 00");
        var entries = p.World.RpcSnapshot().Skip(listStart).Select(r => r.Bytes)
            .Where(b => b.Length > 17 && b[0] == 0x80 && b[1] == 0x96 && BitConverter.ToUInt16(b, 2) == 5).ToArray();
        int index = MissionContactChoice.CriticalIndex(entries, "Scheduled for Deletion");
        p.SendRpc($"80 98 06 00 {Le((ushort)index)} 01 02 00");
        await p.WaitForRpcAsync("80 99");
        p.SendRpc("80 9b 07 00 01");
        await p.WaitForRpcAsync("80 9e 00");

        var current = await MervFollowupCheckpoint(context, p);
        Check.Equal(MachinesCriticalFixture.CamilloMission, current.Mission, "Agent Gray offered the actual Camillo mission");
        var area = XElement.Load(Path.Combine(context.RepositoryRoot, "data", "missions",
            "machines_1_1_3_scheduled_for_deletion.xml")).Elements("phase").Single();
        var evidence = new List<object>();
        foreach (string entrance in MissionCatalogEntrances.Describe(current, context.RepositoryRoot))
            Console.WriteLine("  " + entrance);
        await SweepRide(context, p, current, area, true, evidence);
        var terminal = await MervFollowupObject(context, p, 1, 0, evidence);
        byte use = ObjectStates.UseNumber(terminal.Goid, "Hack") ?? throw new ScenarioFailure("The live terminal must support stock Hack");
        Check.Equal(1002u, ObjectStates.FindUsable(terminal.Goid, use).executeRule, "The visible action is ComputerHack");

        const ushort hacker = 52;
        const ushort slot = MachinesCriticalFixture.HackerSlot;
        const ushort level = MachinesCriticalFixture.HackerLevel;
        string hack = $"80 c7 {MissionNpcId(terminal)} {use:x2} 00";
        string account = $"SELECT exp FROM characters WHERE charId={p.CharId}";
        long initialExperience = await context.Database.LongAsync(account);
        p.SkipReceived();
        SetAbilityLoaded(p, slot, hacker, level, false);
        await p.WaitForRpcAsync("80 b3 34 00");
        await p.WaitForRpcAsync(PassiveRecord(hacker, 643, 4 * level, true));
        p.SendRpc(hack);
        await p.WaitForTextAsync("Load Hacker before attempting to hack this computer.");
        Check.Equal(0, p.Received("80 a0 01 06 00 01").Count, "No optional goal receipt while unloaded");
        Check.Equal(0, p.Received("81 19").Count, "A refused Hack opens no container");
        Check.Equal(initialExperience, await context.Database.LongAsync(account), "A refused Hack earns no experience");

        SetAbilityLoaded(p, slot, hacker, level, true);
        await p.WaitForRpcAsync($"80 b2 34 00 {Le(level)} {Le(slot)}");
        await p.WaitForRpcAsync(PassiveRecord(hacker, 643, 4 * level));
        p.SendRpc($"80 c7 {MissionNpcId(terminal)} 06 00");
        p.SendRpc("81 48");
        await p.WaitForRpcAsync("81 49");
        Check.Equal(initialExperience, await context.Database.LongAsync(account), "Wrong usable earns no experience");
        Check.Equal(0, p.Received("80 a0 01 06 00 01").Count, "Wrong usable cannot complete the Hack");

        p.SendRpc(hack);
        await p.WaitForRpcAsync("80 a0 01 06 00 01");
        const uint earned = 6030;
        string gain = $"80 e6 {Le(earned)}";
        await p.WaitForRpcAsync(gain);
        await p.WaitForViewUpdateAsync(terminal.ViewId, "01 08 60 09 00 00");
        Check.Equal(initialExperience + earned, await context.Database.LongAsync(account), "Goal and level-19 XP commit together");
        Check.Equal(1, p.Received(gain).Count, "One Hack experience gain packet");
        Check.Equal(1, p.Received("80 a0 01 06 00 01").Count, "The loaded Hack closes its exact goal once");
        Check.Equal(0, p.Received("80 a0 03 06 00 01").Count, "It does not complete the Defeat objective");

        p.SendRpc(hack);
        p.SendRpc("81 48");
        await p.WaitForRpcAsync("81 49");
        Check.Equal(initialExperience + earned, await context.Database.LongAsync(account), "Repeating the Hack earns no more XP");
        Check.Equal(1, p.Received(gain).Count, "Repeating the Hack sends no second gain");

        string username = await context.Database.ScalarAsync($"SELECT username FROM users JOIN characters USING(userId) WHERE charId={p.CharId}");
        p.SendRpc("80 fe 00");
        await p.WaitForRpcAsync("80 fd");
        p.Dispose();
        await Task.Delay(TimeSpan.FromSeconds(33)); // Existing session reservation cleanup.
        using var relogged = await HeadlessPlayer.LoginAsync(context.Server.Address, username, p.Handle);
        await relogged.EnterWorldAsync();
        relogged.SendRpc("0c 01");
        await relogged.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(initialExperience + earned, await context.Database.LongAsync(account), "Relog reproduces the goal without re-awarding XP");
        Check.Equal(0, relogged.Received(gain).Count, "Relog sends no duplicate gain");
        Console.WriteLine("  ComputerHack 1002: unload/refusal, Archive reload (+8), authored goal/idle, one 6030 XP gain, repeat/relog clean");
    }
}
