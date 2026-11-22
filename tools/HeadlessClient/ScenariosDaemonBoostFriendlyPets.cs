using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task DaemonBoostFriendlyPetsServer(ScenarioContext context)
    {
        const ushort level = 29, helperLevel = 22, boost = 609;
        const string nukerStock = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";
        const string healerStock = "80b95a02000000000000000000000000000000000000000000000000585ca80001000000086b0000000000";
        static (ushort, ushort)[] Profile(ushort rank, params ushort[] ids) => ids.SelectMany(id =>
            AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().Select(id => (id, rank)).ToArray();
        var seed = new CharacterSeed("DaemonBoostOwner")
        {
            X = 17000, Y = 495, Z = 6400, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
            Health = 5000, MaxHealth = 5000, InnerStrength = 79, MaxInnerStrength = 1000, Organization = 1,
            OwnedLoadedAbilities = Profile(level, 2, 17, 1008, 22, boost)
        };
        var owner = await context.AddPlayerAsync(seed);
        var helper = seed with
        {
            Level = helperLevel, Experience = (int)PlayerProgression.ExperienceForLevel(helperLevel), Health = 100,
            InnerStrength = 500, OwnedLoadedAbilities = Profile(helperLevel, 2, 17, 1008, 602)
        };
        var friend = await context.AddPlayerAsync(helper with { Handle = "DaemonBoostFriend", X = 17400 });
        var enemy = await context.AddPlayerAsync(helper with { Handle = "DaemonBoostEnemy", X = 16600, Organization = 2 });
        var distant = await context.AddPlayerAsync(helper with { Handle = "DaemonBoostDistant", Z = 2300 });
        foreach (var player in new[] { owner, friend, enemy, distant })
            await PvpOpponentsFixture.EnterAsync(player);
        await Task.WhenAll(new[] { friend, enemy, distant }.Select(async player =>
        {
            player.SendRpc(healerStock);
            await player.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
            await player.WaitForViewNamedAsync("Healer Simulacrum");
        }));
        owner.SendRpc("80b9f003000000000000000000000000000000000000000000000000000090767241086b0000000000");
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        owner.SendRpc(nukerStock);
        await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        var nuker = await owner.WaitForViewNamedAsync("Nuker Simulacrum");
        owner.SendChat("?pet dummy 1");
        var dummy = await owner.WaitForViewNamedAsync("Simulacra Dummy");
        string target = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        int from = context.Server.DebugLog().Length;
        // 609 has no retained stock activation in this lane. This is a server-only cast-handler test.
        owner.SendChat("?pet cast 609");
        var cast = await owner.WaitForRpcAsync("80 ac 61 02");
        Check.Equal(2f, BitConverter.ToSingle(cast.Bytes, cast.Bytes.Length - 4), "Two-second parent cast");
        await owner.WaitForRpcAsync("80 bd 01 61 02 04 00 00 70 41 01");
        await owner.WaitForViewUpdateAsync(nuker.ViewId, "04 80 80 80 c0 ?? ?? c0 44 08 00 28");
        owner.SendRpc("81 51 " + target);
        owner.SendRpc("81 7b fe 02 " + target);
        await owner.WaitUntilAsync(() =>
        {
            string changes = context.Server.DebugLog()[from..];
            return changes.Contains("[PET] typed influence Nuker base") && changes.Contains("[PET] typed healing base") &&
                changes.Contains($"[DAEMON BOOST] 609 upkeep source={owner.CharId} paid=5 ");
        }, "Observed typed damage, typed healing and first upkeep", TimeSpan.FromSeconds(12));

        int stopAt = context.Server.DebugLog().Length;
        owner.SendChat("?pet cast 609");
        await owner.WaitUntilAsync(() => context.Server.DebugLog()[stopAt..].Contains("[AURA] 609 off"),
            "Re-activation switches off the parent", HeadlessPlayer.DefaultTimeout);
        var lines = context.Server.DebugLog()[from..].Split('\n');
        Check.Equal(1, lines.Count(line => line.Contains($"[ABILITY-COST] char={owner.CharId} ability=609 paid=10 ")), "One activation debit");
        var applied = lines.Where(line => line.Contains($"[DAEMON BOOST] 699 source={owner.CharId} ")).ToArray();
        int own = applied.Count(line => line.Contains($"recipient={owner.CharId} ") && line.Contains("viral=14 healing=14"));
        int friendly = applied.Count(line => line.Contains($"recipient={friend.CharId} ") && line.Contains("viral=14 healing=14"));
        Check.That(own >= 2 && own == friendly && applied.Length == own + friendly,
            "Only the caster's and nearby friendly pets receive the immediate and periodic pulses");
        int afterOff = context.Server.DebugLog().Length;
        await Task.Delay(TimeSpan.FromSeconds(5.2));
        Check.That(!context.Server.DebugLog()[afterOff..].Contains("[DAEMON BOOST]"), "No upkeep or child refresh after toggle-off");
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("aura viral/heal 0/0");
    }
}
