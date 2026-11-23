using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DaemonBoostAccuracyServer(ScenarioContext context)
    {
        const ushort level = 50, boost = 619;
        const int x = 17000, y = 495, z = 6400, friendX = 16500, enemyX = 17400, farZ = 7501;
        const int nukerX = 16600, nukerZ = 6700;
        var stands = new[] { (x, z), (friendX, z), (friendX + 150, z), (enemyX, z), (enemyX + 150, z), (friendX, z + 800),
            (x - 150, farZ), (x, farZ), (nukerX, nukerZ), (nukerX + 150, nukerZ) };
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        foreach (var (standX, standZ) in stands)
            Check.That(NavMesh.Locate(1, standX, y, standZ, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - y) < 1, $"Outdoor NAV ({standX},{y},{standZ})");

        (ushort, ushort)[] Profile(ushort main) => new ushort[] { 2, 17, CombatStyles.CoderStyle, main }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order()
            .Select(id => (id, level)).ToArray();
        var casterProfile = Profile(boost);
        Check.That(casterProfile.Sum(row => AbilityMemory.Cost(row.Item1)) <= AbilityMemory.ForLevel(level),
            "The full Daemon Boost 2.0 profile fits the Archive");
        CharacterSeed Seed(string handle, int standX, int standZ, int org, (ushort, ushort)[] profile) =>
            new(handle)
            {
                X = standX, Y = y, Z = standZ, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
                Health = 5000, MaxHealth = 5000, InnerStrength = 700, MaxInnerStrength = 1000, Organization = org,
                OwnedLoadedAbilities = profile
            };

        var friendlyProfile = Profile(594);
        var source = await context.AddPlayerAsync(Seed("AccuracyAuraSource", x, z, 1, casterProfile));
        var friend = await context.AddPlayerAsync(Seed("AccuracyAuraFriend", friendX, z, 1, friendlyProfile));
        var enemy = await context.AddPlayerAsync(Seed("AccuracyAuraEnemy", enemyX, z, 2, friendlyProfile));
        var far = await context.AddPlayerAsync(Seed("AccuracyAuraFar", x - 150, farZ, 1, friendlyProfile));
        var nuker = await context.AddPlayerAsync(Seed("AccuracyAuraNuker", nukerX, nukerZ, 1, Profile(22)));
        var recipients = new[] { friend, enemy, far, nuker };
        foreach (var player in recipients.Prepend(source)) await PvpOpponentsFixture.EnterAsync(player);

        // Retained stock summons. 619 runs through the server's dev cast on this disposable server only.
        await Task.WhenAll(recipients.Select(player => SummonAsync(player, ReferenceEquals(player, nuker)
            ? AdvancedSimulacraNoticeFixture.Nuker1 : AdvancedSimulacraNoticeFixture.Proxy5)));
        Check.That(!source.World.RpcSnapshot().Any(frame => frame.Opcode == 0x176), "The aura caster owns no pet");
        friend.SendChat("?pet dummy 50");
        await friend.WaitForTextAsync("stands 8 m away");
        var dummy = await friend.WaitForViewNamedAsync("Simulacra Dummy");
        friend.SendRpc("81 7b fe 02 " + Le(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        var nukerDummy = await nuker.WaitForViewNamedAsync("Simulacra Dummy");
        nuker.SendRpc("81 7b fe 02 " + Le(DuelPackets.Handle(nukerDummy.ViewId, nukerDummy.SpawnId)));
        string roll = $"attack owner={friend.Handle} bonus=0 roll=80 chance=";
        await friend.WaitUntilAsync(() => context.Server.DebugLog().Contains(roll + "75 hit=False"),
            "The friendly Proxy misses the fixed roll without 700", TimeSpan.FromSeconds(16));

        int start = context.Server.DebugLog().Length;
        source.SendChat("?pet cast 619");
        var bar = await source.WaitForRpcAsync("80 ac 6b 02");
        Check.Equal(2f, BitConverter.ToSingle(bar.Bytes.AsSpan(^4)), "Daemon Boost 2.0 cast bar");
        byte[] timer = AbilityCooldowns.BuildTimer(boost, AbilityCooldowns.OwnTimer, 15, start: true);
        await source.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(timer), "Fifteen-second own timer");
        string Pulse(HeadlessPlayer target) => $"[DAEMON BOOST] 700 source={source.CharId} recipient={target.CharId}";
        string upkeep = $"[DAEMON BOOST] 619 upkeep source={source.CharId} paid=5";
        await source.WaitUntilAsync(() => context.Server.DebugLog()[start..].Contains(Pulse(friend)),
            "The initialized aura reaches a friendly other-owner Proxy", HeadlessPlayer.DefaultTimeout);
        string initial = context.Server.DebugLog()[start..];
        Check.That(initial.Contains($"ability=619 paid=20 IS=") && !initial.Contains(upkeep),
            "The initial pulse spends the stored 20 IS and no separate upkeep");
        Check.That(!initial.Contains(Pulse(enemy)) && !initial.Contains(Pulse(far)),
            "The legal enemy and the distant friendly pet are excluded");
        await friend.WaitUntilAsync(() => context.Server.DebugLog()[start..].Contains(roll + "95 hit=True"),
            "The supported attack path consumes the friendly Proxy accuracy", TimeSpan.FromSeconds(9));
        await source.WaitUntilAsync(() => context.Server.DebugLog()[start..].Contains(
                "[DAEMON BOOST] 700 attack owner=AccuracyAuraFriend channel=melee accuracy=64") &&
            context.Server.DebugLog()[start..].Contains(
                "[DAEMON BOOST] 700 attack owner=AccuracyAuraNuker channel=viral accuracy=64"),
            "Ordinary Proxy and Nuker attacks each consume their actual linked accuracy channel", TimeSpan.FromSeconds(9));
        await source.WaitUntilAsync(() => context.Server.DebugLog()[start..].Contains(upkeep) &&
            context.Server.DebugLog()[start..].Split(Pulse(friend)).Length >= 3,
            "Five-second upkeep pays and refreshes the original friendly recipient", TimeSpan.FromSeconds(9));

        source.SendChat("?qaunload 619");
        await source.WaitForRpcAsync("80 bd 05 6b 02 00 00 00 00 00 00");
        friend.SendChat("?pet");
        await friend.WaitForTextAsync("accuracy 64");
        int afterUnload = context.Server.DebugLog().Length;
        await friend.WaitUntilAsync(() => context.Server.DebugLog()[afterUnload..].Contains(roll + "75 hit=False"),
            "The independent five-second child expires after the parent is unloaded", TimeSpan.FromSeconds(9));
        Check.That(!context.Server.DebugLog()[afterUnload..].Contains(Pulse(friend)) &&
            !context.Server.DebugLog()[afterUnload..].Contains(upkeep), "Unloaded aura publishes no further child or debit");
        Check.Equal(0, recipients.Prepend(source).Sum(player => player.World.Problems.Count), "Aura and combat framing");
        Console.WriteLine("DAEMON BOOST 2.0 PASS: a caster without a pet changes the friendly proxy roll from miss to hit; unload lets the child expire.");

        async Task SummonAsync(HeadlessPlayer player, string frame)
        {
            player.SendRpc(frame);
            await player.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
            player.SendRpc("81 7c fe 05 02");
            await player.WaitForRpcAsync("81 78");
        }
    }
}
