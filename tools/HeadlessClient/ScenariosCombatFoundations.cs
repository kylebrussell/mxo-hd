using MxoHd.PacketResearch;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CombatFoundations(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(new CharacterSeed("FoundationsActor")
        {
            X = 17000, Y = 495, Z = 2300, Level = 50, Experience = (int)PlayerProgression.ExperienceForLevel(50), Organization = 1, Health = 5000, MaxHealth = 5000,
            InnerStrength = 2000, MaxInnerStrength = 2000,
            OwnedLoadedAbilities = new ushort[] { 2, 17, 158, 330, 493, 1006 }.Select(id => (id, (ushort)50)).ToArray()
        });
        await actor.EnterWorldAsync();

        void Cast(ushort ability, ViewCreation view)
        {
            var rpc = new AbilityUseRecord(ability, DuelPackets.Handle(view.ViewId, view.SpawnId),
                0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
            Check.Equal(43, rpc.Length, "Stock 80 b9 target record size");
            actor.SendRpc(rpc);
        }

        actor.SendChat("?combatdepth foundations-sight");
        await actor.WaitForTextAsync("across a mapped gap");
        var blocked = await actor.WaitForViewNamedAsync("AB1 Blocked");
        var clear = await actor.WaitForViewNamedAsync("AB1 Clear");
        actor.SendRpc($"41 {HandleOf(blocked)}");
        await actor.WaitForTextAsync("Target is out of sight");
        Cast(330, blocked);
        await actor.WaitUntilAsync(() => context.Server.DebugLog().Contains("[ABILITY] Sight blocked ability 330"),
            "cast finished against mapped gap", TimeSpan.FromSeconds(12));
        string Lands(ushort id, ViewCreation target) => $"[ABILITY] {id} lands on view {target.ViewId} for ";
        Check.That(!context.Server.DebugLog().Contains(Lands(330, blocked)), "Blocked ability does not land");
        const string hitRecord = "04 80 80 80 c0";
        Check.Equal(0, ViewUpdateValues(actor, blocked.ViewId, hitRecord).Count, "Blocked target receives no hit update");

        // The blocked request still uses the existing cast/cooldown lifecycle.
        await Task.Delay(TimeSpan.FromSeconds(4.5));
        Cast(330, clear);
        await actor.WaitUntilAsync(() => context.Server.DebugLog().Contains(Lands(330, clear) + "424"),
            "same cast lands for 424 on continuous mapped floor", TimeSpan.FromSeconds(12));
        await actor.WaitForViewUpdateAsync(clear.ViewId, hitRecord + " " + Le((ushort)4576));

        actor.SendChat("?combatdepth foundations-cone");
        await actor.WaitForTextAsync("AB1 Outside stays full");
        var aim = await actor.WaitForViewNamedAsync("AB1 Aim");
        var inside = await actor.WaitForViewNamedAsync("AB1 Inside");
        var outside = await actor.WaitForViewNamedAsync("AB1 Outside");
        var resisted = await actor.WaitForViewNamedAsync("AB1 Resisted");
        Cast(493, aim);
        await actor.WaitUntilAsync(() => context.Server.DebugLog().Contains(Lands(493, aim)) &&
            context.Server.DebugLog().Contains(Lands(493, inside)), "template cone reaches two NPCs",
            TimeSpan.FromSeconds(12));
        await actor.WaitForViewUpdateAsync(aim.ViewId, NpcHealthUpdate);
        await actor.WaitForViewUpdateAsync(inside.ViewId, NpcHealthUpdate);
        Check.That(ViewUpdateValues(actor, aim.ViewId, NpcHealthUpdate).First() < 5000 &&
            ViewUpdateValues(actor, inside.ViewId, NpcHealthUpdate).First() < 5000, "Both cone hits lower NPC health");

        await Task.Delay(TimeSpan.FromSeconds(4.5));
        Cast(330, resisted);
        await actor.WaitUntilAsync(() => context.Server.DebugLog().Contains(Lands(330, resisted) + "348"),
            "typed resistance reduces the same cast to 348", TimeSpan.FromSeconds(12));
        await actor.WaitForViewUpdateAsync(resisted.ViewId, hitRecord + " " + Le((ushort)4652));
        Check.That(!context.Server.DebugLog().Contains(Lands(493, outside)), "NPC within 8m but outside the cone stays untouched");
        Check.Equal(0, ViewUpdateValues(actor, outside.ViewId, NpcHealthUpdate).Count, "Outside NPC receives no hit update");
        Console.WriteLine("  LOS blocked ranged and ability hits; Direct Fire hit two of three NPCs; Logic Barrage did 424 and 348 damage.");
        Check.Equal(0, actor.World.Problems.Count, "Combat foundation packet parsing");
    }
}
