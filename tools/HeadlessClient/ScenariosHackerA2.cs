using System.Text.RegularExpressions;
using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task HackerA2GroupHeal(ScenarioContext context)
    {
        const ushort ability = 381;
        const int heal = 28;
        CharacterSeed Seed(string name, double offset) => NearNpc(name, offset) with
        {
            Level = 50, Experience = (int)PlayerProgression.ExperienceForLevel(50),
            Health = 100, MaxHealth = 5000, InnerStrength = 2000, MaxInnerStrength = 2000
        };
        // Recreate the shared Hacker sweep input: Heal Damage +10%, Buff Area Radius +24%.
        // The actor enters with the ordinary owned and loaded archive rows.
        var actors = await context.AddPlayersInWorldAsync(
            Seed("GroupHealer", 0) with
            {
                OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 50), (17, 50), (71, 50), (74, 50), (370, 50), (381, 50) }
            },
            Seed("GroupInside", 700), Seed("GroupOutside", 925), Seed("GroupOutsider", 300));
        var (caster, inside, outside, outsider) = (actors[0], actors[1], actors[2], actors[3]);
        foreach (var member in new[] { inside, outside })
        {
            caster.SendRpc(GroupInvite(Team, member.Handle, ""));
            await member.WaitForRpcAsync("80 8f 0c 00");
            member.SendRpc(InviteAnswer(Team, 0, caster.Handle));
            await caster.WaitForRpcAsync($"70 03 {Le(member.CharId)} 09 00");
        }

        var selected = await caster.WaitForViewNamedAsync(outsider.Handle);
        var cast = new AbilityUseRecord(ability, DuelPackets.Handle(selected.ViewId, selected.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        Check.Equal(43, cast.Length, "Full stock ability activation");
        int start = context.Server.DebugLog().Length;
        ushort initialIs = RefusalOwnValue(caster, "InnerStrengthAvailable", "02 80 80 80 10");
        var cursors = actors.ToDictionary(actor => actor, actor => actor.World.ViewSnapshot().Count);
        caster.SendRpc(cast);
        var bar = await caster.WaitForRpcAsync($"80 ac {Le(ability)}");
        Check.Equal(4f, BitConverter.ToSingle(bar.Bytes, bar.Bytes.Length - 4), "Group Heal cast time");
        var debit = BytePattern.Parse($"02 00 02 80 80 80 10 {Le((ushort)(initialIs - 20))}");
        await caster.WaitUntilAsync(() => caster.World.ViewSnapshot().Skip(cursors[caster]).Any(debit.Occurs),
            "fresh Group Heal 20 IS debit", HeadlessPlayer.DefaultTimeout);
        await caster.WaitForRpcAsync($"80 bd 01 {Le(ability)} 04 00 00 80 40 01");
        await caster.WaitForRpcAsync($"80 bd 01 {Le(ability)} 04 00 00 80 40 00");

        string trace = context.Server.DebugLog()[start..];
        foreach (var expected in new[] { caster, inside })
        {
            var delta = Regex.Match(trace, @"\[ABILITY\] Healed (\d+) -> (\d+)\r?\n[^\r\n]*" +
                Regex.Escape($"[ABILITY] Heal {ability} -> {expected.Handle} for {heal}") + @"(?:\r?\n|$)");
            Check.That(delta.Success, $"Group Heal reached {expected.Handle} for {heal}");
            ushort after = ushort.Parse(delta.Groups[2].Value);
            Check.Equal(heal, after - ushort.Parse(delta.Groups[1].Value), "Actual heal before natural regeneration");
            var hp = BytePattern.Parse($"02 00 02 80 80 80 40 {Le(after)}");
            await expected.WaitUntilAsync(() => expected.World.ViewSnapshot().Skip(cursors[expected]).Any(hp.Occurs),
                $"fresh own HP for {expected.Handle}", HeadlessPlayer.DefaultTimeout);
        }
        foreach (var excluded in new[] { outside, outsider })
            Check.That(!trace.Contains($"Heal {ability} -> {excluded.Handle} for ", StringComparison.Ordinal),
                $"Group Heal excludes {excluded.Handle}");
        Check.Equal(0, actors.Sum(actor => actor.World.Problems.Count), "Group Heal packet framing");
    }
}
