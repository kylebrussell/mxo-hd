using System.Text.Json;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerTeamPatcherMode : NativeQaMode
{
    private const int OriginX = 24800, Y = 495, OriginZ = -1498;
    private const ushort Dedication = 433;
    private static readonly (string Handle, int X, int Z)[] Stands =
    {
        ("QAFriend", 25400, OriginZ), ("QAFar", OriginX, -698),
        ("QAEnemy", OriginX, -2498), ("QAOther", 24560, -1318)
    };
    private sealed record Sample(ushort Id, int Amount, float Reuse, ushort Family, string[] Members);
    private static readonly Sample[] Samples =
    {
        new(50, 12, 4, 686, new[] { "QAFriend" }),
        new(434, 36, 4, 685, new[] { "QAFriend", "QAFar" }),
        new(435, 34, 4, 686, new[] { "QAFriend", "QAFar", "QAEnemy" }),
        new(437, 141, 300, 0, new[] { "QAFriend", "QAFar" })
    };

    public override string Name => "ability-parity-hacker-team-patcher";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Dedication }.Concat(Samples.Select(sample => sample.Id)).ToArray(), OriginX, OriginZ,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET healthC=100,healthM=5000,innerStrC=65535,innerStrM=2000,y=495,z=-1498,alignment=1,pvpflag=0 WHERE charId IN (110,6111,6112,6113,6114)",
            "UPDATE characters SET x=31000+100*(charId-6111) WHERE charId BETWEEN 6111 AND 6114",
            "DELETE FROM char_abilities WHERE char_id BETWEEN 6111 AND 6114",
            "DELETE FROM inventory WHERE charId=110"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, readyMessages: new[]
        {
            "After stock CSR can move, stay on the seeded stand and send each helper a chat transport request. Example {\"actor\":\"QAFriend\",\"action\":\"chat\",\"text\":\"?tp 25400 495 -1498 0 0 0\"}.",
            "Use QAFriend 25400/495/-1498 (6 m), QAFar 24800/495/-698 (8 m), friendly QAEnemy 24800/495/-2498 (10 m), and QAOther 24560/495/-1318 (3 m, clear of the WinterHelper stack). Have QAFriend invite CSR_Tester through the helper request and accept the stock Mission Team dialog. Then have QAFriend invite QAFar and QAEnemy. Keep QAOther outside the team.",
            "Keep Self Defense and the seeded +15% Healing and +24% radius loadout. Select the nonmember QAOther. Stock retest 434 only: use Trauma Surgeon > Restore Group 3.0. Expect +36 on CSR, QAFriend and QAFar, with no heal on the 10 m teammate QAEnemy or selected nonmember QAOther. Group Repairs 3.0 is 435.",
            "Move QAEnemy to 24800 495 -2698 (12 m) before Panicked Heal 437. 437 adds 141 to CSR, QAFriend and QAFar. QAEnemy and QAOther receive no cast heal. Wounds stay fixed apart from the casts.",
            "Total Dedication 433: observe +10% IS Regeneration Bonus and +5% Healing Bonus while loaded. Use ?qaunload 433 and ?qaload 433 and retain the exact remove and add records. See docs/ABILITY-HACKER-TEAM-PATCHER.md."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Team Patcher server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Handle, stand.Handle, stockCharacterLoad: true);
            context.Helpers.Add(stand.Handle, helper);
            await helper.EnterWorldAsync();
            Check.Equal((ushort)100, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(helper), $"{helper.Handle} is wounded");
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster)
            return Array.Empty<byte>();

        await caster.EnterWorldAsync();
        Check.That(caster.World.CreationSnapshot().All(view => !Stands.Any(stand => stand.Handle == view.Name)),
            "No helper arrives before the stock CSR handshake");
        foreach (var stand in Stands)
        {
            await MoveAsync(context.Helpers[stand.Handle], stand.X, stand.Z);
            var view = await caster.WaitForViewNamedAsync(stand.Handle);
            var position = view.Attributes["Position"];
            Check.Equal((double)stand.X, BitConverter.ToDouble(position), $"{stand.Handle} stage X");
            Check.Equal((double)Y, BitConverter.ToDouble(position, 8), $"{stand.Handle} stage Y");
            Check.Equal((double)stand.Z, BitConverter.ToDouble(position, 16), $"{stand.Handle} stage Z");
        }
        var nonmember = context.Helpers["QAOther"];
        var target = await caster.WaitForViewNamedAsync(nonmember.Handle);
        var leader = context.Helpers["QAFriend"];
        await TeamBreadthMode.InviteAsync(leader, caster);
        await TeamBreadthMode.InviteAsync(leader, context.Helpers["QAFar"]);
        await TeamBreadthMode.InviteAsync(leader, context.Helpers["QAEnemy"]);

        var participants = context.Helpers.Values.Prepend(caster).ToArray();
        var health = participants.ToDictionary(actor => actor.Handle, _ => (ushort)100);
        var proof = new List<object>();
        foreach (var sample in Samples)
        {
            if (sample.Id == 437)
            {
                var moved = context.Helpers["QAEnemy"];
                await MoveAsync(moved, OriginX, -2698);
                moved.SendRpc("81 48");
                await moved.WaitForRpcAsync("81 49");
            }
            int cursor = caster.World.RpcSnapshot().Count;
            var request = EvidenceRetestWire.Cast(caster, sample.Id, target);
            if (sample.Family != 0)
                await EvidenceRetestWire.CastBarAsync(caster, sample.Id, 4);
            await EvidenceRetestWire.TimerAsync(caster, cursor, sample.Id, 4, sample.Reuse);
            foreach (var recipient in participants)
            {
                bool included = recipient == caster || sample.Members.Contains(recipient.Handle);
                if (included) health[recipient.Handle] = (ushort)(health[recipient.Handle] + sample.Amount);
                Check.Equal(health[recipient.Handle], await AbilityEvidenceDirectHealsMode.ReadHealthAsync(recipient),
                    $"{sample.Id} exact resulting HP for {recipient.Handle}");
            }
            if (sample.Family != 0)
                await EvidenceRetestWire.TimerAsync(caster, cursor, sample.Family, 0, 4, ended: true);
            proof.Add(new { sample.Id, sample.Amount, sample.Members, Rpc = Convert.ToHexString(request), Health = health.ToDictionary(row => row.Key, row => row.Value) });
        }

        var stats = new (ushort Stat, int Amount)[] { (1059, 10), (1141, 5) };
        foreach (var stat in stats)
            await WaitStatAsync(caster, 0, stat.Stat, stat.Amount, remove: false);
        int passiveAt = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaunload 433");
        await caster.WaitForRpcAsync("80 b3 " + EvidenceRetestWire.Le(Dedication));
        foreach (var stat in stats)
            await WaitStatAsync(caster, passiveAt, stat.Stat, stat.Amount, remove: true);
        passiveAt = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaload 433");
        await caster.WaitForRpcAsync("80 b2 " + EvidenceRetestWire.Le(Dedication));
        foreach (var stat in stats)
            await WaitStatAsync(caster, passiveAt, stat.Stat, stat.Amount, remove: false);

        Check.Equal(0, participants.Sum(actor => actor.World.Problems.Count), "Team Patcher packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-team-patcher.json"),
            JsonSerializer.Serialize(new { Heals = proof, Dedication = stats.Select(stat => new { stat.Stat, stat.Amount }) }));
        return Array.Empty<byte>();
    }

    private static async Task MoveAsync(HeadlessPlayer actor, int x, int z)
    {
        actor.SendChat(FormattableString.Invariant($"?tp {x} {Y} {z} 0 0 0"));
        await actor.WaitForTextAsync("Teleported to 0/0");
    }

    private static Task WaitStatAsync(HeadlessPlayer actor, int cursor, ushort stat, int value, bool remove) =>
        actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor).Any(record =>
            ManageBonusRecord.TryDecode(record.Bytes, out var m) && m!.SourceId == Dedication &&
            m.TargetId == stat && m.SignedValue == value && m.Duration == 0 && m.AddRemoveType == (remove ? 1 : 0)),
            $"Total Dedication {stat} {value:+#} remove={remove}", HeadlessPlayer.DefaultTimeout);
}
