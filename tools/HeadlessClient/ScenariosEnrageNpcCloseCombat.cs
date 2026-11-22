using hds;
using MxoHd.PacketResearch;
using static HeadlessClient.EnrageNpcCloseCombatFixture;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task EnrageNpcCloseCombat(ScenarioContext context)
    {
        CheckGeometry(context.RepositoryRoot);
        CharacterSeed Seed(string handle, int x, int z, ushort level) => new(handle)
        {
            X = x, Y = Y, Z = z, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
            Health = 5000, MaxHealth = 5000, InnerStrength = 250, MaxInnerStrength = 250,
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1) }
        };
        var actor = await context.AddPlayerAsync(Seed("EnrageRifleCaster", CasterX, CasterZ, Level) with
        {
            OwnedLoadedAbilities = Abilities.Select(id => (id, Level)).ToArray()
        });
        var receiver = await context.AddPlayerAsync(Seed("EnrageNearestEnemy", HelperX, HelperZ, HelperLevel));
        await receiver.EnterWorldAsync();
        await actor.EnterWorldAsync();
        var npc = await FindNpcAsync(actor);
        var receiverView = await FindNpcAsync(receiver);
        actor.SendRpc("3e 6b f0 03 00 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        // Generated server-only cast input. Keep the separately captured 588
        // stock activation in native evidence when it becomes available.
        byte[] request = new AbilityUseRecord(Ability, DuelPackets.Handle(npc.ViewId, npc.SpawnId),
            0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        actor.SendRpc(request);
        await actor.WaitForRpcAsync("80 ac 4c 02");
        int initial = context.Server.DebugLog().Length;
        await receiver.WaitForRpcAsync(record => CombatFlag(record, receiverView, 1),
            "Enrage opens the specific armed NPC's interlock", TimeSpan.FromSeconds(15));
        string onset = context.Server.DebugLog()[initial..];
        string pursue = $"[AGGRO] {NpcName} pursues {receiver.CharId}";
        string attack = $"[AGGRO] {NpcName} attacks {receiver.CharId} (view {receiverView.ViewId})";
        Check.That(onset.Contains(pursue) && onset.Contains(attack) && !onset.Contains($"[AGGRO] {NpcName} opens fire"),
            "Enrage uses a close-only chase and targets the nearest eligible player");
        Check.That(!actor.World.RpcSnapshot().Any(record => record.Opcode == 0x46), "The farther caster does not receive the opening");
        await receiver.WaitUntilAsync(() => context.Server.DebugLog()[initial..].Contains("source=588 stat=93 remove"),
            "The original NPC Enrage expires", TimeSpan.FromSeconds(14));
        Check.That(!receiver.World.RpcSnapshot().Any(record => CombatFlag(record, receiverView, 0)),
            "The same interlock is held through Enrage expiry");
        receiver.SendRpc(Release);
        await receiver.WaitForRpcAsync(record => CombatFlag(record, receiverView, 0),
            "The helper releases the same armed NPC's interlock");
        var updates = context.Server.DebugLog()[initial..].Split('\n').Where(line => line.Contains("[ENRAGE] source=588")).ToArray();
        Check.Equal(1, updates.Length, "Later pulses leave the existing engagement alone through its release");
        Check.Equal(0, actor.World.Problems.Count + receiver.World.Problems.Count, "The armed close-combat path decodes cleanly");
        Console.WriteLine("ENRAGE NPC CLOSE COMBAT SERVER PASS: armed chase, one interlock held through expiry, and exact helper release.");
    }
}
