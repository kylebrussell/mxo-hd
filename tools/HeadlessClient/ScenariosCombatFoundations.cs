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
            Items = new[] { (1, 692u, 1), (2, 7899u, 1) },
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

        async Task CheckTeleport(int viewCursor, byte yaw, float x, float y, float z)
        {
            byte[] header = { 2, 0, 1, 0x1c };
            byte[] destination = new[] { yaw }.Concat(BitConverter.GetBytes(x)).Concat(BitConverter.GetBytes(y))
                .Concat(BitConverter.GetBytes(z)).ToArray();
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewCursor).Any(record =>
            {
                int destinationAt = BytePattern.IndexOf(record, destination, 0);
                int start = destinationAt - 11;
                return start >= 0 && destinationAt + destination.Length + 4 <= record.Length &&
                    record.AsSpan(start, header.Length).SequenceEqual(header) && record[start + 4] != 0 &&
                    Enumerable.Range(0, 3).All(i => BitConverter.ToUInt16(record, start + 5 + i * 2) != ushort.MaxValue);
            }), "The first setup sends an allocated stock teleport with a mapped destination, yaw and XYZ",
                HeadlessPlayer.DefaultTimeout);
        }

        int sightViews = actor.World.ViewSnapshot().Count;
        actor.SendChat("?combatdepth foundations-sight");
        await actor.WaitForTextAsync("across a mapped gap");
        await CheckTeleport(sightViews, 192, 22700, 1095, 16100);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 08 b4 02 00 00");
        Check.Equal(692L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={actor.CharId} AND slot=105"),
            "Sight command equipped the rifle in the stock hand slot");
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

        // A direct close-combat request is permitted against a passive target. The setup
        // must release this fight and carry the new NAV location in the first teleport.
        actor.SendRpc($"40 {HandleOf(clear)} 01 6b");
        await actor.WaitForRpcAsync($"40 {HandleOf(clear)} 00 6b");
        await actor.WaitForRpcAsync("47");
        int coneViews = actor.World.ViewSnapshot().Count;
        actor.SendChat("?combatdepth foundations-cone");
        await actor.WaitForTextAsync("AB1 Outside stays full");
        await CheckTeleport(coneViews, 64, 24800, 495, -1498);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 08 db 1e 00 00");
        Check.Equal(7899L, await context.Database.LongAsync($"SELECT goid FROM inventory WHERE charId={actor.CharId} AND slot=105"),
            "Cone command equipped the dual SMGs in the stock hand slot");
        var aim = await actor.WaitForViewNamedAsync("AB1 Aim");
        var inside = await actor.WaitForViewNamedAsync("AB1 Inside");
        var outside = await actor.WaitForViewNamedAsync("AB1 Outside");
        var resisted = await actor.WaitForViewNamedAsync("AB1 Resisted");
        // PR330 stock Submachine Guns Actions activation. Substitute only the current aim handle.
        byte[] submachine = Convert.FromHexString("80b99e00040002000000000000000000000000000000000000000000d557377c20000000086b0000000000");
        BitConverter.GetBytes(DuelPackets.Handle(aim.ViewId, aim.SpawnId)).CopyTo(submachine, 4);
        actor.SendRpc(submachine);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 9e 00 00 00");
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
        Check.That(!context.Server.DebugLog().Contains("[AGGRO] AB1", StringComparison.Ordinal),
            "Damage never causes a passive AB1 target to pursue or re-engage");
        Console.WriteLine("  First sight and cone setup sent mapped teleports; passive AB1 targets stayed in place; Direct Fire hit two NPCs.");
        Check.Equal(0, actor.World.Problems.Count, "Combat foundation packet parsing");
    }
}
