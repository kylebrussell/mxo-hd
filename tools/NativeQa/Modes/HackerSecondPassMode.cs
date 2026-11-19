using System.Text;
using System.Text.Json;
using HeadlessClient;
using hds;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerSecondPassMode : NativeQaMode
{
    private const ushort GroupHeal = 56, Movement = 62, Pathogenist = 67, PassiveCode = 170,
        ClearMind = 409, Restore = 413, Virologist = 98, VirologistUpgrade = 1077, PathogenistUpgrade = 1085;

    internal static IReadOnlyList<(string Name, int X, int Z)> Stands { get; } = Array.AsReadOnly(
        new[] { ("QAFriend", 25200, -1498), ("QAFar", 24800, -573), ("QAOther", 24500, -1498) });

    public override string Name => "ability-parity-hacker-second-pass";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, GroupHeal, Movement, Pathogenist, PassiveCode, ClearMind, Restore, Virologist }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), HackerParitySweepMode.WoundedGroupSeedSql(Stands),
        DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, readyMessages: new[]
        {
            "Stay on the seeded stand until fully in world, then submit {\"actor\":\"QAFriend\",\"action\":\"hacker-group-arrive\"}. The normal late-arrival action used by the stock Hacker sweep brings all three helpers into view afterward.",
            "Use the radial Recruit to Mission on QAFriend (4 m east) and QAFar (9.25 m north). For each submit {\"actor\":\"QAFriend\",\"action\":\"accept\",\"inviter\":\"CSR_Tester\"} with the invited handle. Leave QAOther (3 m west) outside the team.",
            "Keep the opening loadout intact for the heals. Select QAOther and cast Restore Group 1.0 (56, Group Repairs 1.0 in the source). It heals CSR and QAFriend only. Compare Restore Health 3.0 (413) on QAFriend before, during and after Passive Code 170 (+25% Healing for 20 s). Respect 413 own and shared reuse.",
            "Cast Movement Accelerator 1.0 (62) on QAFriend. Record the +10 movement modifier and the +0.10 movement scale in the helper's capture, with no own modifier. Request {\"actor\":\"QAFriend\",\"action\":\"chat\",\"text\":\"?dismissbuff 62\"} and capture the zero movement scale. For the native movement view, also cast 62 on self after reuse and dismiss it normally.",
            "Pathogenist 67: record the granted 1085 Actions entry, then ?qaunload 67 and ?qaload 67. Capture the grant removal and restoration. No 1085 active-effect claim.",
            "Clear Mind 409: record its +10% melee and ballistic defense and +5% Healing contributions. Use ?qaunload 409 and ?qaload 409 and capture all three changes.",
            "Virologist Upgrade 1077: use its granted Actions entry with QAOther selected. Capture +20 Viral Resistance, +5 Concentration and +3 Viral Accuracy on CSR only, and their disappearance on normal dismissal. See docs/ABILITY-HACKER-SECOND-PASS.md."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        await HackerParitySweepMode.StartGroupHelpersAsync(context, Stands);
        if (context.StockProbe is not { } caster)
            return Array.Empty<byte>();

        await caster.EnterWorldAsync();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable second Hacker sweep.");
        foreach (var handle in context.Helpers.Keys)
            Check.That(!caster.World.CreationSnapshot().Any(view => view.Name == handle), $"{handle} starts outside stock visibility");
        await HackerParitySweepMode.ArriveAsync(context.Helpers, Stands);
        foreach (var (handle, x, z) in Stands)
        {
            var view = await caster.WaitForViewNamedAsync(handle);
            Check.Equal((X: (double)x, Y: (double)HackerParitySweepMode.GroundY, Z: (double)z),
                HackerParitySweepMode.Position(view), $"{handle} is visible on the exact lateral stand");
        }
        var near = context.Helpers["QAFriend"];
        var far = context.Helpers["QAFar"];
        var outsider = context.Helpers["QAOther"];
        var nearby = await caster.WaitForViewNamedAsync(near.Handle);
        var selected = await caster.WaitForViewNamedAsync(outsider.Handle);
        await caster.WaitForViewNamedAsync(far.Handle);
        foreach (var member in new[] { near, far })
            await InviteAsync(caster, member);

        int beforeGroup = server.DebugLog().Length;
        int beforeGroupTimer = caster.World.RpcSnapshot().Count;
        var groupRequest = EvidenceRetestWire.Cast(caster, GroupHeal, selected);
        var included = new[] { caster, near };
        string Line(HeadlessPlayer player) => $"Heal {GroupHeal} -> {player.Handle} for 9";
        await caster.WaitUntilAsync(() => included.All(player => server.DebugLog()[beforeGroup..].Contains(Line(player),
            StringComparison.Ordinal)), "Restore Group 1.0 heals exactly CSR and the near teammate", HeadlessPlayer.DefaultTimeout);
        foreach (var excluded in new[] { far, outsider })
            Check.Equal((ushort)100, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(excluded), $"{excluded.Handle} is not healed");
        Check.Equal((ushort)109, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(near), "Near teammate's actual group heal");
        await EvidenceRetestWire.TimerAsync(caster, beforeGroupTimer, 685, 0, 4, ended: true);

        var healDeltas = new List<int>();
        async Task CastMeasuredHeal(int amount)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            ushort start = await AbilityEvidenceDirectHealsMode.ReadHealthAsync(near);
            EvidenceRetestWire.Cast(caster, Restore, nearby);
            await EvidenceRetestWire.TimerAsync(caster, cursor, Restore, 4, 4);
            ushort after = await AbilityEvidenceDirectHealsMode.ReadHealthAsync(near);
            Check.Equal(amount, after - start, "Passive Code real heal delta");
            healDeltas.Add(after - start);
            await EvidenceRetestWire.TimerAsync(caster, cursor, 685, 0, 4, ended: true);
        }

        await CastMeasuredHeal(84);
        int passiveCodeAt = caster.World.RpcSnapshot().Count;
        var passiveCodeRequest = EvidenceRetestWire.Cast(caster, PassiveCode);
        await ModifierAsync(caster, passiveCodeAt, PassiveCode, 174, 25, 20);
        await CastMeasuredHeal(101);

        int movementAt = near.World.ViewSnapshot().Count;
        int casterMovementAt = caster.World.RpcSnapshot().Count;
        var movementRequest = EvidenceRetestWire.Cast(caster, Movement, nearby);
        await ModifierAsync(near, 0, Movement, 115, 10, 600);
        await EvidenceRetestWire.ViewSinceAsync(near, movementAt, 2, "02 80 88 cd cc cc 3d 80 80 80 02 00 00");
        Check.That(!HasModifier(caster, casterMovementAt, Movement, 115, 10, 600, false), "Friend's movement does not modify the caster");
        movementAt = near.World.ViewSnapshot().Count;
        near.SendChat("?dismissbuff 62");
        await ModifierAsync(near, 0, Movement, 115, 10, 600, remove: true);
        await EvidenceRetestWire.ViewSinceAsync(near, movementAt, 2, "02 80 88 00 00 00 00 80 80 80 02 00 00");

        Check.That(caster.Received("80 b2 " + EvidenceRetestWire.Le(PathogenistUpgrade) + " 00 00 08 02").Count > 0,
            "Parent 67 publishes its grant on ordinary world readiness");
        caster.SendChat("?qaunload 67");
        await caster.WaitForRpcAsync("80 b3 " + EvidenceRetestWire.Le(PathogenistUpgrade));
        caster.SendChat("?qaload 67");
        await caster.WaitForRpcAsync("80 b2 " + EvidenceRetestWire.Le(PathogenistUpgrade) + " 00 00 08 02");

        await ModifierAsync(caster, passiveCodeAt, PassiveCode, 174, 25, 20, remove: true, timeout: TimeSpan.FromSeconds(25));
        await CastMeasuredHeal(84);

        var clearMind = new (ushort Stat, int Value)[] { (1143, 10), (1027, 10), (1141, 5) };
        foreach (var (stat, value) in clearMind)
            await ModifierAsync(caster, 0, ClearMind, stat, value, 0);
        int clearMindAt = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaunload 409");
        await caster.WaitForRpcAsync("80 b3 " + EvidenceRetestWire.Le(ClearMind));
        foreach (var (stat, value) in clearMind)
            await ModifierAsync(caster, clearMindAt, ClearMind, stat, value, 0, remove: true);
        clearMindAt = caster.World.RpcSnapshot().Count;
        caster.SendChat("?qaload 409");
        await caster.WaitForRpcAsync("80 b2 " + EvidenceRetestWire.Le(ClearMind));
        foreach (var (stat, value) in clearMind)
            await ModifierAsync(caster, clearMindAt, ClearMind, stat, value, 0);

        Check.That(caster.Received("80 b2 " + EvidenceRetestWire.Le(VirologistUpgrade) + " 00 00 08 02").Count > 0,
            "Parent 98 publishes its grant on ordinary world readiness");
        var virologist = new (ushort Stat, int Value)[] { (157, 20), (564, 5), (342, 3) };
        int upgradeAt = caster.World.RpcSnapshot().Count;
        var upgradeRequest = EvidenceRetestWire.Cast(caster, VirologistUpgrade, selected);
        foreach (var (stat, value) in virologist)
            await ModifierAsync(caster, upgradeAt, VirologistUpgrade, stat, value, 0);
        Check.That(virologist.All(mod => !HasModifier(outsider, 0, VirologistUpgrade, mod.Stat, mod.Value, 0, false)),
            "The selected nonmember receives no Virologist modifiers");
        caster.SendRpc("80 bb " + EvidenceRetestWire.Le(VirologistUpgrade));
        foreach (var (stat, value) in virologist)
            await ModifierAsync(caster, upgradeAt, VirologistUpgrade, stat, value, 0, remove: true);

        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(player => player.World.Problems.Count),
            "Second Hacker sweep packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "hacker-second-pass.json"), JsonSerializer.Serialize(new
        {
            GroupHeal = Convert.ToHexString(groupRequest), GroupDelta = 9, Movement = Convert.ToHexString(movementRequest),
            PassiveCode = Convert.ToHexString(passiveCodeRequest), HealDeltas = healDeltas, PathogenistGrant = PathogenistUpgrade,
            ClearMind = clearMind.Select(mod => new { mod.Stat, mod.Value }),
            Virologist = Convert.ToHexString(upgradeRequest), Upgrade = virologist.Select(mod => new { mod.Stat, mod.Value })
        }));
        return Array.Empty<byte>();
    }

    private static async Task InviteAsync(HeadlessPlayer caster, HeadlessPlayer member)
    {
        caster.SendRpc(new byte[] { 0x6e }.Concat(SocialRequests.BuildGroupInvite(3, member.Handle, "")).ToArray());
        await member.WaitForRpcAsync("80 8f 0c 00");
        byte[] inviter = Encoding.ASCII.GetBytes(caster.Handle + "\0");
        member.SendRpc(new byte[] { 0x6f, 3, 8, 0, 0, 0, 0, 0 }
            .Concat(BitConverter.GetBytes((ushort)inviter.Length)).Concat(inviter).ToArray());
        await member.WaitForRpcAsync("80 8e");
        await caster.WaitForRpcAsync($"70 03 {WireFormat.ToHex(BitConverter.GetBytes(member.CharId))} 09 00");
    }

    private static bool HasModifier(HeadlessPlayer actor, int start, ushort id, ushort stat, int value, float seconds,
        bool remove) => actor.World.RpcSnapshot().Skip(start).Any(rpc => ManageBonusRecord.TryDecode(rpc.Bytes, out var m) &&
            m!.SourceId == id && m.TargetId == stat && m.SignedValue == value && m.Duration == seconds &&
            m.AddRemoveType == (remove ? 1 : 0));

    private static Task ModifierAsync(HeadlessPlayer actor, int start, ushort id, ushort stat, int value, float seconds,
        bool remove = false, TimeSpan? timeout = null) => actor.WaitUntilAsync(() => HasModifier(actor, start, id, stat, value, seconds, remove),
            $"{actor.Handle} modifier {id}/{stat} {value}, duration {seconds}, remove={remove}", timeout ?? HeadlessPlayer.DefaultTimeout);
}
