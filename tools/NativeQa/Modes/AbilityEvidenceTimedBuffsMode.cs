using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityEvidenceTimedBuffsMode : NativeQaMode
{
    private sealed record Modifier(ushort Stat, int Value);
    private sealed record Sample(ushort Id, bool Friendly, float Seconds, Modifier[] Modifiers);

    private static readonly Sample[] Samples =
    {
        new(321, true, 300, new[] { new Modifier(117, 15) }),
        new(124, false, 20, new[] { new Modifier(171, 25) }),
        new(373, false, 300, new[] { new Modifier(522, 64) }),
        new(374, false, 300, new[] { new Modifier(524, 64) }),
        new(421, true, 300, new[] { new Modifier(1009, 47), new Modifier(1010, 47) }),
        new(39, true, 600, new[] { new Modifier(106, 50) }),
        new(322, true, 600, new[] { new Modifier(106, 200) }),
        new(417, true, 300, new[] { new Modifier(106, 500) }),
        new(419, true, 300, new[] { new Modifier(106, 500) }),
    };

    // The full stock targeted 80 b9 framing used by the Status fixture. The headless
    // producer substitutes the selected ability and live subject handle.
    private const string CastRecord =
        "80 b9 3a 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 08 6b 00 00 00 00 00";

    public override string Name => "ability-evidence-timed-buffs";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 77 }.Concat(Samples.Select(sample => sample.Id)).ToArray(),
        24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            // World entry clamps current IS to the live maximum after the fixture's ancestors are active.
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=4000,innerStrM=2000 WHERE charId IN (110,6111,6113)",
            "UPDATE characters SET x=24900,z=-1498 WHERE charId=6111",
            "UPDATE characters SET x=25000,z=-1498 WHERE charId=6113"
        },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Record level 50 Status before and after each Actions cast. Use the normal Status buff dismissal between samples; leave Firearms Skill running through its 20 s natural expiry.",
            "Self casts: Firearms Skill 124 gives +25 Ballistic Damage; Combat Training 373 gives +64 Melee Accuracy; Weapons Boost 374 gives +64 Ballistic Accuracy.",
            "Cast Efficiency 321 on self. Compare the observed IS debit of Firearms Skill (20 without Efficiency, headless expects 17 with it) and Repair RSI 1.0 (10 versus 9). The fractional native value is part of the check.",
            "Cast Enhanced Dodge 421 on self for +47 melee and ballistic defense. Dismiss it, then sample Bolster Health 39/322/419 and Upgrade Health 417 separately. Their expected raw max-HP increases are +50/+200/+500/+500.",
            "Repeat a friendly cast on QAOther to verify its recipient. Save each full activation RPC, modifier add/removal, Status or maximum-HP delta, and any difference between the caster and recipient."
        }, loadAbilityAncestors: true);

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("The timed-buff fixture requires its disposable server.");
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        var recipient = await HeadlessPlayer.LoginAsync(context.Address, "QAOther", "QAOther", stockCharacterLoad: true);
        context.Helpers.Add(recipient.Handle, recipient);
        await caster.EnterWorldAsync();
        await recipient.EnterWorldAsync();
        var target = await caster.WaitForViewNamedAsync(recipient.Handle);
        var self = recipient.World.CreationSnapshot().Last(view => view.ViewId == 2);
        ushort baselineHealth = BinaryPrimitives.ReadUInt16LittleEndian(self.Attributes["MaxHealth"]);
        ushort baselineIs = BinaryPrimitives.ReadUInt16LittleEndian(self.Attributes["InnerStrengthMax"]);
        Check.Equal(baselineIs, BinaryPrimitives.ReadUInt16LittleEndian(self.Attributes["InnerStrengthAvailable"]),
            "The friendly recipient enters with full live IS");
        var proof = new List<object>();

        var casterSelf = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        ushort casterIs = BinaryPrimitives.ReadUInt16LittleEndian(casterSelf.Attributes["InnerStrengthMax"]);
        Check.Equal(casterIs, BinaryPrimitives.ReadUInt16LittleEndian(casterSelf.Attributes["InnerStrengthAvailable"]),
            "The unbuffed comparison caster enters with full live IS");
        Sample firearms = Samples[1];
        int plainRpcs = caster.World.RpcSnapshot().Count;
        int plainViews = caster.World.ViewSnapshot().Count;
        SendCast(caster, firearms.Id, null);
        await WaitForVital(caster, plainViews, "02 80 80 80 10", (ushort)(casterIs - 20), "Unbuffed Firearms Skill debits 20 IS");
        await WaitForModifier(caster, plainRpcs, firearms, firearms.Modifiers[0], remove: false);
        int plainOff = caster.World.RpcSnapshot().Count;
        caster.SendRpc($"80 bb {LittleEndian(firearms.Id)}");
        await WaitForModifier(caster, plainOff, firearms, firearms.Modifiers[0], remove: true);

        foreach (Sample sample in Samples)
        {
            // The efficient recipient pays for Firearms Skill and keeps it until expiry.
            HeadlessPlayer actor = sample.Id == 124 ? recipient : caster;
            HeadlessPlayer observer = sample.Friendly ? recipient : actor;
            int rpcStart = observer.World.RpcSnapshot().Count;
            int viewStart = observer.World.ViewSnapshot().Count;
            byte[] request = SendCast(actor, sample.Id, sample.Friendly ? target : null);
            var adds = new List<RpcRecord>();
            foreach (Modifier modifier in sample.Modifiers)
                adds.Add(await WaitForModifier(observer, rpcStart, sample, modifier, remove: false));

            if (sample.Id == 124)
                await WaitForVital(recipient, viewStart, "02 80 80 80 10", (ushort)(baselineIs - 17),
                    "Efficiency lowers the real Firearms Skill debit from 20 to 17");

            int healthDelta = sample.Modifiers.FirstOrDefault(modifier => modifier.Stat == 106)?.Value ?? 0;
            if (healthDelta != 0)
                await WaitForVital(recipient, viewStart, "02 80 80 01", (ushort)(baselineHealth + healthDelta),
                    $"Ability {sample.Id} changes the recipient's maximum HP");

            var removes = new List<RpcRecord>();
            if (sample.Id != 321)
            {
                int removeStart = observer.World.RpcSnapshot().Count;
                int resetStart = observer.World.ViewSnapshot().Count;
                if (sample.Id != 124)
                    observer.SendRpc($"80 bb {LittleEndian(sample.Id)}");
                foreach (Modifier modifier in sample.Modifiers)
                    removes.Add(await WaitForModifier(observer, removeStart, sample, modifier, remove: true));
                if (healthDelta != 0)
                    await WaitForVital(recipient, resetStart, "02 80 80 01", baselineHealth,
                        $"Dismissing {sample.Id} restores the baseline maximum HP");
            }

            if (sample.Id == 124)
            {
                int discountedRepair = recipient.World.ViewSnapshot().Count;
                SendCast(recipient, 77, null);
                await WaitForVital(recipient, discountedRepair, "02 80 80 80 10", (ushort)(baselineIs - 9),
                    "Efficiency's fractional reduction makes Repair RSI debit 9 IS");
            }

            proof.Add(new
            {
                sample.Id,
                Recipient = observer.Handle,
                Request = Convert.ToHexString(request),
                Applies = adds.Select(record => record.Hex),
                Removes = removes.Select(record => record.Hex)
            });
        }

        Sample efficiency = Samples[0];
        int cleanup = recipient.World.RpcSnapshot().Count;
        recipient.SendRpc($"80 bb {LittleEndian(efficiency.Id)}");
        var efficiencyOff = await WaitForModifier(recipient, cleanup, efficiency, efficiency.Modifiers[0], remove: true);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, recipient);
        Check.Equal(0, caster.World.Problems.Count, "Timed-buff caster protocol");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "timed-buff-evidence-bootstrap.json"),
            JsonSerializer.Serialize(new { BaselineHealth = baselineHealth, BaselineIs = baselineIs, Cases = proof,
                FirearmsCost = 20, EfficientFirearmsCost = 17, EfficientRepairCost = 9, EfficiencyOff = efficiencyOff.Hex },
                new JsonSerializerOptions { WriteIndented = true }));
        return efficiencyOff.Bytes;
    }

    private static byte[] SendCast(HeadlessPlayer actor, ushort ability, ViewCreation? target)
    {
        byte[] request = WireFormat.FromHex(CastRecord);
        Check.That(AbilityUseRecord.TryDecode(request, out _), "The cast uses the stock 43-byte ability layout");
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), ability);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(4), target?.ViewId ?? 0);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(6), target?.SpawnId ?? 0);
        actor.SendRpc(request);
        return request;
    }

    private static async Task<RpcRecord> WaitForModifier(HeadlessPlayer player, int cursor, Sample sample, Modifier modifier, bool remove)
    {
        RpcRecord? observed = null;
        await player.WaitUntilAsync(() =>
        {
            observed = player.World.RpcSnapshot().Skip(cursor).FirstOrDefault(record =>
                ManageBonusRecord.TryDecode(record.Bytes, out var bonus) && bonus!.SourceId == sample.Id &&
                bonus.TargetId == modifier.Stat && bonus.SignedValue == modifier.Value &&
                bonus.Duration == sample.Seconds && bonus.AddRemoveType == (remove ? 1 : 0));
            return observed != null;
        }, $"ability {sample.Id}, stat {modifier.Stat}, value {modifier.Value}, duration {sample.Seconds}, remove={remove}",
            remove && sample.Id == 124 ? TimeSpan.FromSeconds(24) : HeadlessPlayer.DefaultTimeout);
        return observed!;
    }

    private static Task WaitForVital(HeadlessPlayer player, int cursor, string mask, ushort value, string reason)
    {
        var expected = BytePattern.Parse($"02 00 {mask} {LittleEndian(value)}");
        return player.WaitUntilAsync(() => player.World.ViewSnapshot().Skip(cursor).Any(expected.Occurs),
            reason, HeadlessPlayer.DefaultTimeout);
    }

    private static string LittleEndian(ushort value) => WireFormat.ToHex(BitConverter.GetBytes(value));
}
