using System.Globalization;
using System.Text.RegularExpressions;
using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CoderPetNpcRetaliation(ScenarioContext context)
    {
        const int level = 18;
        const string npc = "Trainman";
        var profile = new ushort[] { 2, 17, CombatStyles.CoderStyle, 27 }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();
        Check.That(profile.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(level), "Both Proxy profiles fit L18 memory");
        CharacterSeed Seed(string name, double offset) => NearNpc(name, offset) with
        {
            Level = level, Experience = checked((int)PlayerProgression.ExperienceForLevel(level)),
            Health = 5000, MaxHealth = 5000, InnerStrength = 500, MaxInnerStrength = 500,
            OwnedLoadedAbilities = profile.Select(id => (id, (ushort)level)).ToArray()
        };

        var first = await context.AddPlayerAsync(Seed("PetRetaliationA", -400));
        var second = await context.AddPlayerAsync(Seed("PetRetaliationB", -300));
        foreach (var owner in new[] { first, second }) await owner.EnterWorldAsync();
        await Task.WhenAll(SummonAsync(first), SummonAsync(second));
        string firstHandle = TargetHandle(await first.WaitForViewNamedAsync(npc));
        string secondHandle = TargetHandle(await second.WaitForViewNamedAsync(npc));

        var returns = new Regex(@"\[PET RETALIATION\] attacker=Trainman .*? owner=(\w+) at=([0-9.]+)");
        int attackAt = context.Server.DebugLog().Length;
        Attack(first, firstHandle);
        Attack(second, secondHandle);
        await first.WaitUntilAsync(() => context.Server.DebugLog()[attackAt..].Split('\n')
            .Count(line => line.Contains("command Attack", StringComparison.Ordinal)) >= 2,
            "Both stock Attack orders reach the same ordinary NPC", HeadlessPlayer.DefaultTimeout);
        await first.WaitUntilAsync(() => ReturnsSince(attackAt).Count >= 2,
            "Trainman retaliates across successive live pet ticks", TimeSpan.FromSeconds(12));
        var rounds = ReturnsSince(attackAt).ToArray();
        for (int i = 1; i < rounds.Length; i++)
            Check.That(At(rounds[i]) - At(rounds[i - 1]) >= CoderPets.AttackPeriodSeconds - .01,
                "Two Proxy orders cannot multiply Trainman's retaliation cadence");

        var parked = rounds[^1].Groups[1].Value == first.Handle ? first : second;
        var remaining = parked == first ? second : first;
        var parkedHandle = parked == first ? firstHandle : secondHandle;
        int joinAt = context.Server.DebugLog().Length;
        parked.SendRpc("81 7a fe 00"); // Stock Stay
        await parked.WaitUntilAsync(() => context.Server.DebugLog()[joinAt..].Contains("command Stay", StringComparison.Ordinal),
            "One owner parks their Proxy before joining Close Combat", HeadlessPlayer.DefaultTimeout);
        await remaining.WaitUntilAsync(() => ReturnsSince(joinAt).Any(round => round.Groups[1].Value == remaining.Handle),
            "The still-attacking Proxy can receive Trainman's next action", TimeSpan.FromSeconds(10));

        parked.SendRpc($"40 {parkedHandle} 01 6b");
        await parked.WaitForRpcAsync($"46 {parkedHandle} 01");
        int interlockAt = context.Server.DebugLog().Length;
        await parked.WaitUntilAsync(() => context.Server.DebugLog()[interlockAt..].Contains("[COMBAT-NPC] action Trainman", StringComparison.Ordinal),
            "The ordinary player interlock takes Trainman's next action", TimeSpan.FromSeconds(10));
        Check.Equal(0, ReturnsSince(interlockAt).Count, "Trainman does not retaliate against a Proxy during the player interlock");
        parked.SendRpc("44 00");
        await parked.WaitForRpcAsync($"46 {parkedHandle} 00", TimeSpan.FromSeconds(18));
        int disengagedAt = context.Server.DebugLog().Length;
        await remaining.WaitUntilAsync(() => ReturnsSince(disengagedAt).Any(round => round.Groups[1].Value == remaining.Handle),
            "The surviving pet attack receives retaliation after the player leaves interlock", TimeSpan.FromSeconds(10));
        Check.Equal(0, first.World.Problems.Count + second.World.Problems.Count, "Simulacra and player interlock framing");

        MatchCollection ReturnsSince(int after) => returns.Matches(context.Server.DebugLog()[after..]);
        static double At(Match round) => double.Parse(round.Groups[2].Value, CultureInfo.InvariantCulture);
        static string TargetHandle(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
        static void Attack(HeadlessPlayer owner, string handle) => owner.World.SendRpcs(
            WireFormat.FromHex($"81 51 {handle}"), WireFormat.FromHex($"81 7b fe 02 {handle}"));
        static async Task SummonAsync(HeadlessPlayer owner)
        {
            owner.SendRpc(AdvancedSimulacraNoticeFixture.CoderStyle);
            await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
            owner.SendRpc(AdvancedSimulacraNoticeFixture.Proxy1);
            var registered = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
            await AdvancedSimulacraNoticeFixture.PetViewAsync(owner, owner.CharId, registered.Bytes[3]);
        }
    }
}
