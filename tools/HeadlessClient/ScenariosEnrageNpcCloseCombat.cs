using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task EnrageNpcCloseCombat(ScenarioContext context)
    {
        const ushort ability = 588, level = 25;
        const string name = "Blackwood Mushfake";
        double[] origin = { 16304, 95, -4284 };
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        var (near, far) = Stands();
        var profile = new ushort[] { 2, 17, 1008, ability }.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().Select(id => (id, level)).ToArray();
        CharacterSeed Seed(string handle, NavPoint point) => new(handle)
        {
            X = point.x, Y = point.y, Z = point.z, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
            Health = 5000, MaxHealth = 5000, InnerStrength = 250, MaxInnerStrength = 250,
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1) }
        };
        var actor = await context.AddPlayerAsync(Seed("EnrageRifleCaster", far) with { OwnedLoadedAbilities = profile });
        var receiver = await context.AddPlayerAsync(Seed("EnrageNearestEnemy", near) with
        {
            Level = 5, Experience = (int)PlayerProgression.ExperienceForLevel(5)
        });
        await receiver.EnterWorldAsync();
        await actor.EnterWorldAsync();
        var npc = await FindNpcAsync(actor);
        var receiverView = await FindNpcAsync(receiver);
        uint weapon = BitConverter.ToUInt32(npc.Attributes["EquippedItemID"]);
        Check.Equal(2568u, weapon, "The actual ambient NPC carries the shipped rifle");
        double initialDistance = GroundDistance(origin, near.Position);
        Check.That(initialDistance > MobAggro.MeleeDistanceUnits && MobAggro.OpensFire(weapon, initialDistance),
            "The receiver starts outside melee reach and inside the equipped rifle's range");
        actor.SendRpc("3e 6b f0 03 00 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        // Generated server-only cast input. Keep the separately captured 588
        // stock activation in native evidence when it becomes available.
        byte[] request = new AbilityUseRecord(ability, DuelPackets.Handle(npc.ViewId, npc.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        actor.SendRpc(request);
        await actor.WaitForRpcAsync("80 ac 4c 02");
        int initial = context.Server.DebugLog().Length;
        await receiver.WaitForRpcAsync("46", TimeSpan.FromSeconds(15));
        string onset = context.Server.DebugLog()[initial..];
        string pursue = $"[AGGRO] {name} pursues {receiver.CharId}";
        string attack = $"[AGGRO] {name} attacks {receiver.CharId} (view {receiverView.ViewId})";
        Check.That(onset.Contains(pursue) && onset.Contains(attack) && !onset.Contains($"[AGGRO] {name} opens fire"),
            "Enrage uses a close-only chase and targets the nearest eligible player");
        Check.That(!actor.World.RpcSnapshot().Any(record => record.Opcode == 0x46), "The farther caster does not receive the opening");
        await receiver.WaitUntilAsync(() => context.Server.DebugLog()[initial..].Contains("source=588 stat=93 remove"),
            "The original NPC Enrage expires", TimeSpan.FromSeconds(14));
        var updates = context.Server.DebugLog()[initial..].Split('\n').Where(line => line.Contains("[ENRAGE] source=588")).ToArray();
        Check.Equal(1, updates.Length, "Later pulses leave the existing engagement alone");
        Check.Equal(0, actor.World.Problems.Count + receiver.World.Problems.Count, "The armed close-combat path decodes cleanly");
        receiver.SendRpc("44 00");
        Console.WriteLine("ENRAGE NPC CLOSE COMBAT SERVER PASS: live rifle carrier chooses the nearest target, chases, opens one interlock and expires.");

        async Task<ViewCreation> FindNpcAsync(HeadlessPlayer player)
        {
            ViewCreation? found = null;
            await player.WaitUntilAsync(() => (found = player.World.CreationSnapshot().LastOrDefault(view =>
                view.Goid == 599 && view.Name == name && view.Attributes["Level"][0] == 5 &&
                view.Attributes["MoreInfoID"].SequenceEqual(Convert.FromHexString("b6010058")) &&
                EngageFoesFixture.Position(view).Zip(origin).All(pair => Math.Abs(pair.First - pair.Second) < 1))) != null,
                $"Birth position of the armed {name}", HeadlessPlayer.DefaultTimeout);
            return found!;
        }

        (NavPoint Near, NavPoint Far) Stands()
        {
            for (int i = 0; i < 16; i++)
            {
                double theta = i * Math.PI / 8;
                if (Stand(700, theta, out var n) && Stand(900, theta, out var f)) return (n, f);
            }
            throw new InvalidDataException("The existing armed Mara NPC needs two grounded, visible stands within ten metres.");
        }

        bool Stand(double distance, double theta, out NavPoint ground) =>
            NavMesh.Project(1, origin[0] + distance * Math.Cos(theta), origin[1], origin[2] + distance * Math.Sin(theta),
                out ground, maxDistance: 20, reachableFrom: origin, requireDirect: true) &&
            !ground.location.indoors && Math.Abs(ground.y - origin[1]) < 1 &&
            NavMesh.HasSightPath(1, origin[0], origin[1], origin[2], ground.x, ground.y, ground.z) &&
            NavMesh.HasSightPath(1, ground.x, ground.y, ground.z, origin[0], origin[1], origin[2]);
    }
}
