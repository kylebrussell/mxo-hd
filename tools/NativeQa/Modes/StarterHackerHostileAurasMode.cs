using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterHackerHostileAurasMode : NativeQaMode
{
    private const ushort Miasma = 61, MiasmaPulse = 701, StaticField = 89, StaticPulse = 694;
    // PR320 stock CSR requests at 1837–38 (Miasma) and 2054–55 (Static Field).
    private const string MiasmaCast = "80b93d00000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string StaticCast = "80b95900000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Miasma, StaticField).Order().ToArray();
    private static readonly (string Name, uint Id, int X, int Z, int Org)[] Stands =
    {
        ("QAFriend", 6111, 17250, 2300, 1), ("QAEnemy", 6114, 17550, 2300, 2),
        ("QAFar", 6112, 17000, 2990, 2)
    };

    public override string Name => "ability-parity-starter-hacker-hostile-auras";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 17000, 2300, Array.Empty<string>(), Array.Empty<string>(),
        new[] { "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", Loadout.Select((id, index) => $"(6111,{400 + index},{AbilityMessages.AbilityGameObject(id)},50,1)")),
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y=495,healthC=3000,healthM=3000,innerStrC=500,innerStrM=500,alignment=1,pvpflag=0 WHERE charId=110"
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},x={stand.X},y=495,z={stand.Z},healthC=3000,healthM=3000,innerStrC=500,innerStrM=500,alignment={stand.Org},pvpflag=0 WHERE charId={stand.Id}")).ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Miasma, StaticField }, environmentOverrides: new()
        {
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            ["MXO_MOBS"] = "test", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "61,701,89,694", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Remain on the start mark with no target. QAFriend stands 2.5 m east, hostile QAEnemy is 5.5 m east, hostile QAFar is 6.9 m along +Z. Page 2 slot 1 Miasma, slot 2 Static Field.",
            "Slot 1 first. Then /tell QAFriend miasma. Wait until you see the helper say Miasma is on. Press slot 1 again and wait for a helper pulse with CSR off. The hostile receives two -14 defense records per pulse, with one active pair and a five-second lifetime. QAFar and QAFriend are excluded. /tell QAFriend miasma again. Capture the final removals after 5 s.",
            "Slot 2 next, then /tell QAFriend static. Wait at least six seconds after CSR starts, then press slot 2 again. Wait for another helper-only pulse, then /tell QAFriend static again. Both casters should send their own hits to QAEnemy. Capture first stock casts, recurring IS and off/expiry."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var ally = context.Helpers["QAFriend"];
        var enemy = context.Helpers["QAEnemy"];
        var distant = context.Helpers["QAFar"];
        await ally.WaitForViewNamedAsync(enemy.Handle);
        await ally.WaitForViewNamedAsync(distant.Handle);
        if (context.StockProbe is not { } actor)
        {
            _ = RunAllyAsync(context, ally);
            return Array.Empty<byte>();
        }

        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var helper in context.Helpers.Values)
            await actor.WaitForViewNamedAsync(helper.Handle);
        int nearAt = enemy.World.RpcSnapshot().Count;
        int farAt = distant.World.RpcSnapshot().Count;
        int allyAt = ally.World.RpcSnapshot().Count;
        await ToggleAsync(actor, Miasma, MiasmaCast, true);
        var first = await MiasmaPairAsync(nearAt);
        await Task.Delay(TimeSpan.FromSeconds(1));
        int overlapAt = enemy.World.RpcSnapshot().Count;
        await ToggleAsync(ally, Miasma, MiasmaCast, true);
        var overlap = await MiasmaPairAsync(overlapAt);
        foreach (var previous in first)
            await ObserveRemovalAsync(overlapAt, previous);
        await ToggleAsync(actor, Miasma, MiasmaCast, false);
        int nextAt = enemy.World.RpcSnapshot().Count;
        var surviving = await MiasmaPairAsync(nextAt);
        foreach (var previous in overlap)
            await ObserveRemovalAsync(overlapAt, previous);
        await ToggleAsync(ally, Miasma, MiasmaCast, false);
        foreach (var previous in surviving)
            await ObserveRemovalAsync(nextAt, previous);

        nearAt = enemy.World.RpcSnapshot().Count;
        await ToggleAsync(actor, StaticField, StaticCast, true);
        var initialHit = await ObserveHitAsync(nearAt, actor);
        await ToggleAsync(ally, StaticField, StaticCast, true);
        await ObserveHitAsync(nearAt, ally);
        await StockAbilityProfile.ObserveAsync(enemy, nearAt,
            packet => packet.ListCounter != initialHit.ListCounter && IsHit(packet, actor, enemy),
            "Static Field emits a second hit after the recurring IS payment");
        await ToggleAsync(actor, StaticField, StaticCast, false);
        await ObserveHitAsync(enemy.World.RpcSnapshot().Count, ally);
        await ToggleAsync(ally, StaticField, StaticCast, false);
        foreach (var (helper, cursor) in new[] { (distant, farAt), (ally, allyAt) })
            Check.That(!helper.World.RpcSnapshot().Skip(cursor).Any(packet =>
                ManageBonusRecord.TryDecode(packet.Bytes, out var mod) && mod!.SourceId == MiasmaPulse ||
                IsHit(packet, actor, helper) || IsHit(packet, ally, helper)),
                helper.Handle + " receives neither hostile pulse");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Hostile aura fixture packet framing");
        return Array.Empty<byte>();

        async Task<ManageBonusRecord[]> MiasmaPairAsync(int after)
        {
            var pair = new List<ManageBonusRecord>();
            foreach (ushort stat in new ushort[] { 1009, 1010 })
            {
                var rpc = await StockAbilityProfile.ObserveAsync(enemy, after,
                    packet => StockAbilityProfile.Bonus(packet, MiasmaPulse, stat, -14, 5, false),
                    "Miasma applies the five-second defense modifier " + stat);
                ManageBonusRecord.TryDecode(rpc.Bytes, out var mod);
                pair.Add(mod!);
            }
            return pair.ToArray();
        }

        Task<RpcRecord> ObserveRemovalAsync(int after, ManageBonusRecord previous) =>
            StockAbilityProfile.ObserveAsync(enemy, after,
                packet => ManageBonusRecord.TryDecode(packet.Bytes, out var mod) &&
                    mod!.SourceId == MiasmaPulse && mod.RecordId == previous.RecordId && mod.AddRemoveType == 1,
                "The former Miasma record ends " + previous.RecordId);

        Task<RpcRecord> ObserveHitAsync(int after, HeadlessPlayer source) =>
            StockAbilityProfile.ObserveAsync(enemy, after, packet => IsHit(packet, source, enemy),
                "Static Field hits QAEnemy from " + source.Handle);
    }

    private static bool IsHit(RpcRecord packet, HeadlessPlayer source, HeadlessPlayer recipient) =>
        packet.Bytes.AsSpan().SequenceEqual(AbilityMessages.Damage(false, StaticPulse, source.Handle, recipient.Handle, 1, 0));

    private static async Task ToggleAsync(HeadlessPlayer actor, ushort ability, string request, bool on)
    {
        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(request);
        await StockAbilityProfile.ObserveAsync(actor, cursor,
            packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(ability, on)),
            $"{actor.Handle} toggles {ability} {(on ? "on" : "off")}");
    }

    private static async Task RunAllyAsync(NativeQaBootstrapContext context, HeadlessPlayer ally)
    {
        int cursor = ally.World.RpcSnapshot().Count;
        var active = new HashSet<ushort>();
        try
        {
            while (!context.ActualServer!.HasExited && ally.World.CurrentOwnViewLife().Current)
            {
                var records = ally.World.RpcSnapshot();
                foreach (var packet in records.Skip(cursor))
                {
                    var bytes = packet.Bytes;
                    // Server tell sender IDs may be zero. Match the parsed sender handle.
                    if (bytes.Length < 15 || bytes[0] != 0x2e || bytes[1] != 0x11 ||
                        !SocialRequests.TryReadSized(bytes, BitConverter.ToInt32(bytes, 7), out string sender) ||
                        !sender[(sender.LastIndexOf('+') + 1)..].Equals("CSR_Tester", StringComparison.OrdinalIgnoreCase) ||
                        !SocialRequests.TryReadSized(bytes, BitConverter.ToInt32(bytes, 11), out string text))
                        continue;
                    var (ability, request) = text.Trim().ToLowerInvariant() switch
                    {
                        "miasma" => (Miasma, MiasmaCast),
                        "static" => (StaticField, StaticCast),
                        _ => ((ushort)0, "")
                    };
                    if (ability == 0) continue;
                    bool on = !active.Contains(ability);
                    await ToggleAsync(ally, ability, request, on);
                    if (on) active.Add(ability);
                    else active.Remove(ability);
                    ally.SendChat($"{text.Trim()} is {(on ? "on" : "off")}");
                }
                cursor = records.Count;
                await Task.Delay(125);
            }
        }
        catch (Exception error)
        {
            Console.WriteLine("Hostile aura helper stopped: " + error.Message);
        }
    }
}
