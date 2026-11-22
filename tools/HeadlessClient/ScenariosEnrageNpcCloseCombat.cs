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
        const ushort wound = 33, helperRank = 12;
        var helperAbilities = new ushort[] { 2, 17, CombatStyles.CoderStyle, wound }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct();
        var receiver = await context.AddPlayerAsync(Seed("EnrageNearestEnemy", HelperX, HelperZ, helperRank) with
        {
            OwnedLoadedAbilities = helperAbilities.Select(id => (id, helperRank)).ToArray()
        });
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
        // Make the helper's wound deterministic with the retained stock Static Blast request.
        // The NPC is already facing this helper, and it stays in the same interlock.
        const string stockStaticBlast = "80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
        var woundRequest = Convert.FromHexString(stockStaticBlast);
        BitConverter.GetBytes(DuelPackets.Handle(receiverView.ViewId, receiverView.SpawnId)).CopyTo(woundRequest, 4);
        receiver.SendRpc(woundRequest);

        const string expiry = "source=588 stat=93 remove";
        await receiver.WaitUntilAsync(() => context.Server.DebugLog()[initial..].Contains(expiry),
            "The original NPC Enrage expires", TimeSpan.FromSeconds(14));
        string expired = context.Server.DebugLog()[initial..];
        int expiryAt = expired.IndexOf(expiry, StringComparison.Ordinal);
        string helperHit = $"[ABILITY] {wound} lands on view {receiverView.ViewId} for ";
        Check.That(expired[..expiryAt].Contains(helperHit, StringComparison.Ordinal),
            "The helper's stock Static Blast wounded Blackwood before Enrage expired");
        await receiver.WaitUntilAsync(() => context.Server.DebugLog()[initial..][expiryAt..].Contains("[COMBAT] Round "),
            "The helper remains interlocked for another round after Enrage expires", TimeSpan.FromSeconds(6));
        string held = context.Server.DebugLog()[initial..];
        Check.That(!held.Contains($"[AGGRO] {NpcName} opens fire on {actor.CharId}") &&
            !held.Contains($"[AGGRO] {NpcName} attacks {actor.CharId} ("),
            "Enrage expiry cannot start a second engagement against the caster while the wounded NPC is still interlocked");
        Check.That(!actor.World.RpcSnapshot().Any(frame => frame.Opcode == 0x46 ||
                ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == 670 && record.AddRemoveType == 0),
            "The caster receives neither an interlock opening nor its own combat-effect records");
        Check.That(!receiver.World.RpcSnapshot().Any(record => CombatFlag(record, receiverView, 0)),
            "The helper's same interlock remains active through expiry and the following round");
        receiver.SendRpc(Release);
        await receiver.WaitForRpcAsync(record => CombatFlag(record, receiverView, 0),
            "The helper releases the same armed NPC's interlock");
        var updates = context.Server.DebugLog()[initial..].Split('\n').Where(line => line.Contains("[ENRAGE] source=588")).ToArray();
        Check.Equal(1, updates.Length, "Later pulses leave the existing engagement alone through its release");

        // A second cast takes the same NPC from wounded and idle into the timed close-only pursuit again.
        byte[] reuseExpired = AbilityCooldowns.BuildTimer(Ability, AbilityCooldowns.OwnTimer, 28, start: false);
        await actor.WaitForRpcAsync(record => record.Bytes.AsSpan().SequenceEqual(reuseExpired),
            "Enrage own reuse expires before casting at the still-wounded NPC", TimeSpan.FromSeconds(28));
        actor.SkipReceived();
        receiver.SkipReceived();
        int recast = context.Server.DebugLog().Length;
        actor.SendRpc(request);
        await actor.WaitForRpcAsync("80 ac 4c 02");
        await receiver.WaitForRpcAsync(record => CombatFlag(record, receiverView, 1),
            "Enrage chooses the nearby helper even when the target started wounded", TimeSpan.FromSeconds(15));
        string second = context.Server.DebugLog()[recast..];
        Check.That(second.Contains($"[AGGRO] {NpcName} attacks {receiver.CharId} (") &&
            !second.Contains($"[AGGRO] {NpcName} opens fire on {actor.CharId}"),
            "Casting on the wounded NPC still waits for the Enrage close-combat callback");
        receiver.SendRpc(Release);
        await receiver.WaitForRpcAsync(record => CombatFlag(record, receiverView, 0),
            "The second helper interlock also releases normally");

        Check.Equal(0, actor.World.Problems.Count + receiver.World.Problems.Count, "The armed close-combat path decodes cleanly");
        Console.WriteLine("ENRAGE NPC CLOSE COMBAT SERVER PASS: armed chase, wounded expiry, exact helper release, and a wounded-target recast.");
    }
}
