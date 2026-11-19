using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task ForceMultiplierGroupSupport(ScenarioContext context)
    {
        const ushort sweep = 487;
        const ushort improvedHacking = 489;
        CharacterSeed Seed(string name, int offset) => NearNpc(name, offset) with
        {
            Level = 50, Experience = (int)PlayerProgression.ExperienceForLevel(50),
            InnerStrength = 2000, MaxInnerStrength = 2000
        };
        var actors = await context.AddPlayersInWorldAsync(
            Seed("ForceMultiplier", 0) with
            {
                OwnedLoadedAbilities = new (ushort, ushort)[]
                {
                    (2, 50), (17, 50), (71, 50), (369, 50), (383, 50), (484, 50),
                    (485, 50), (487, 50), (489, 50), (490, 50)
                }
            },
            Seed("ForceNear", 500), Seed("ForceMiddle", 700),
            Seed("ForceFar", 1000), Seed("ForceOutsider", 400));
        var (caster, near, middle, far, outsider) = (actors[0], actors[1], actors[2], actors[3], actors[4]);
        foreach (var member in new[] { near, middle, far })
        {
            caster.SendRpc(GroupInvite(Team, member.Handle, ""));
            await member.WaitForRpcAsync("80 8f 0c 00");
            member.SendRpc(InviteAnswer(Team, 0, caster.Handle));
            await caster.WaitForRpcAsync($"70 03 {Le(member.CharId)} 09 00");
        }

        var selected = await caster.WaitForViewNamedAsync(outsider.Handle);
        void CastAtOutsider(ushort ability) => caster.SendRpc(new AbilityUseRecord(ability,
            DuelPackets.Handle(selected.ViewId, selected.SpawnId), 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode());
        CastAtOutsider(sweep);
        foreach (var recipient in new[] { caster, near, middle })
            await recipient.WaitForRpcAsync(ModPattern(sweep, sweep));
        CastAtOutsider(improvedHacking);
        foreach (var recipient in new[] { caster, near })
        {
            await recipient.WaitForRpcAsync(ModPattern(improvedHacking, 172));
            await recipient.WaitForRpcAsync(ModPattern(improvedHacking, 174));
        }
        foreach (var actor in actors)
        {
            actor.SendRpc("81 48");
            await actor.WaitForRpcAsync("81 49");
        }
        foreach (var excluded in new[] { middle, far, outsider })
            Check.Equal(0, excluded.Received(ModPattern(improvedHacking, 174)).Count,
                $"Improved Hacking excludes {excluded.Handle}");
        foreach (var excluded in new[] { far, outsider })
            Check.Equal(0, excluded.Received(ModPattern(sweep, sweep)).Count,
                $"Sweep Party excludes {excluded.Handle}");
        Check.Equal(0, actors.Sum(actor => actor.World.Problems.Count), "Group support packet framing");
    }
}
