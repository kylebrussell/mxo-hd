using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static IReadOnlyDictionary<string, string> CoderEnrage588Environment =>
        new Dictionary<string, string>(PvpOpponentsFixture.Environment)
        {
            ["MXO_DEV_COMMANDS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        };

    private static async Task CoderEnrage588(ScenarioContext context)
    {
        const ushort enrage = 588, level = 25;
        ushort[] abilities = new ushort[] { 2, 17, CombatStyles.CoderStyle, enrage };
        var profile = abilities.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().Select(id => (id, level)).ToArray();
        Check.That(profile.Sum(row => AbilityMemory.Cost(row.id)) <= AbilityMemory.ForLevel(level),
            "Enrage and its ranked prerequisite chain fit memory at level 25");
        CharacterSeed Seed(string name, int x, int organization) => new(name)
        {
            X = x, Y = 495, Z = 6400, Level = level, Experience = checked((int)PlayerProgression.ExperienceForLevel(level)),
            Health = 5000, MaxHealth = 5000, InnerStrength = 250, MaxInnerStrength = 250, Organization = organization
        };
        var actor = await context.AddPlayerAsync(Seed("EnrageCoder", 17000, 1) with { OwnedLoadedAbilities = profile });
        var target = await context.AddPlayerAsync(Seed("EnrageOpponent", 17400, 2) with
        {
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1) }
        });
        var friend = await context.AddPlayerAsync(Seed("EnrageAlly", 16800, 1));
        var far = await context.AddPlayerAsync(Seed("EnrageFarOpponent", 18601, 2));
        foreach (var player in new[] { actor, target, friend, far })
            await PvpOpponentsFixture.EnterAsync(player);
        actor.SendRpc("3e 6b f0 03 00 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var targetView = await actor.WaitForViewNamedAsync(target.Handle);
        var friendView = await actor.WaitForViewNamedAsync(friend.Handle);
        var farView = await actor.WaitForViewNamedAsync(far.Handle);
        string Added(HeadlessPlayer player) => $"[MODIFIER] char={player.CharId} source={enrage} stat=93 add";
        byte[] Request(uint selection) =>
            new AbilityUseRecord(enrage, selection, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();

        // The production request is exercised with generated inputs here. Native QA
        // separately records 588's actual stock activation envelope.
        int initial = RefusalOwnValue(actor, "InnerStrengthAvailable", "02 80 80 80 10");
        int cursor = context.Server.DebugLog().Length;
        await Refuse(0, AbilityRefusals.InvalidTarget);
        await Refuse(DuelPackets.Handle(friendView.ViewId, friendView.SpawnId), AbilityRefusals.InvalidTarget);
        await Refuse(DuelPackets.Handle(farView.ViewId, farView.SpawnId), AbilityRefusals.TargetOutOfRange);
        await Refuse(DuelPackets.Handle(targetView.ViewId, (ushort)(targetView.SpawnId + 1)), AbilityRefusals.InvalidTarget);
        Check.Equal(initial, RefusalOwnValue(actor, "InnerStrengthAvailable", "02 80 80 80 10"),
            "Empty, friendly, far and wrong-spawn requests preserve IS");
        Check.That(!context.Server.DebugLog()[cursor..].Contains($"[ABILITY-COST] char={actor.CharId} ability={enrage}"),
            "Refused target selections never reach cost");

        actor.SkipReceived();
        int firstView = actor.World.ViewSnapshot().Count;
        actor.SendRpc(Request(DuelPackets.Handle(targetView.ViewId, targetView.SpawnId)));
        var cast = await actor.WaitForRpcAsync("80 ac 4c 02");
        Check.Equal(2f, BitConverter.ToSingle(cast.Bytes.AsSpan(^4)), "Two-second normal cast record");
        await actor.WaitForRpcAsync("80 bd 01 4c 02 04 00 00 e0 41 01");
        string add = Added(target) + " value=0 duration=12s";
        string remove = $"[MODIFIER] char={target.CharId} source={enrage} stat=93 remove";
        await actor.WaitUntilAsync(() => context.Server.DebugLog()[cursor..].Contains(add),
            "One selected living opponent receives the twelve-second Enrage", TimeSpan.FromSeconds(8));
        string after = context.Server.DebugLog()[cursor..];
        Check.That(ViewUpdateValues(actor, 2, "02 80 80 80 10", firstView).Contains((ushort)(initial - 27)) &&
            after.Contains($"[ABILITY-COST] char={actor.CharId} ability={enrage} paid=27 IS={initial}->{initial - 27}"),
            "Exactly 27 IS is paid before any ordinary regeneration");
        Check.That(!after.Contains(Added(friend)) && !after.Contains(Added(far)),
            "The selected Enrage does not spread");
        await actor.WaitUntilAsync(() => context.Server.DebugLog()[cursor..].Contains(remove),
            "Enrage is removed by expiry", TimeSpan.FromSeconds(16));
        Check.Equal((ushort)5000, RefusalOwnValue(target, "Health", "02 80 80 80 40"),
            "The state-only cast preserves the selected opponent's HP");
        Check.Equal(0, actor.World.Problems.Count + target.World.Problems.Count + friend.World.Problems.Count + far.World.Problems.Count,
            "Normal target and modifier handling retains packet framing");
        Console.WriteLine("ENRAGE 588 SERVER PASS: empty, ally, 16.01 m and stale-spawn no-debit refusals, 27 IS, 28 s reuse, and unique 12 s selected-player state.");

        async Task Refuse(uint selection, uint reason)
        {
            actor.SkipReceived();
            actor.SendRpc(Request(selection));
            byte[] expected = AbilityRefusals.Encode(new AbilityRefusals.Record(enrage, reason));
            await actor.WaitForRpcAsync(record => record.Bytes.SequenceEqual(expected), $"Enrage refusal reason {reason}");
        }
    }
}
