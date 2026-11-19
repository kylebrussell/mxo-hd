using System.Text;
using System.Text.Json;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class AbilityParityHackerA2GroupHealMode : NativeQaMode
{
    private const ushort GroupHeal = 381;
    private const ushort Wound = 100;
    private static readonly (string Handle, int X, int Z)[] Stands =
        { ("QAFriend", 25500, -1498), ("QAFar", 24800, -573), ("QAOther", 24800, -1798) };

    public override string Name => "ability-parity-hacker-a2-group-heal";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 71, 74, 370, GroupHeal }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET healthC=100,healthM=5000,innerStrC=4000,innerStrM=2000,y=495,z=-1498,alignment=1,pvpflag=0 WHERE charId IN (110,6111,6112,6113,6114)",
            $"UPDATE characters SET x=CASE charId WHEN 6111 THEN {Stands[0].X} ELSE 24800 END," +
                $"z=CASE charId WHEN 6112 THEN {Stands[1].Z} WHEN 6113 THEN {Stands[2].Z} ELSE -1498 END WHERE charId IN (6111,6112,6113,6114)",
            "DELETE FROM inventory WHERE charId=110"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, readyMessages: new[]
        {
            "Group Heal 1.0 (381). QAFriend at (25500,495,-1498) is 7 m away, QAFar at (24800,495,-573) is 9.25 m away, and QAOther at (24800,495,-1798) is 3 m away. Their view angles are separate. All start at 100 HP, with ordinary HP regen held for this run.",
            "Invite QAFriend and QAFar to the Mission Team through the stock UI. After each invite send {\"actor\":\"QAFriend\",\"action\":\"accept\",\"inviter\":\"CSR_Tester\"}, substituting QAFar for the second invitation. Leave QAOther outside the team.",
            "Select QAOther and cast Group Heal 1.0 from Actions. Keep the seeded +10% Heal Damage and +24% Buff Area Radius loadout. Expect 4 s cast, 20 IS, 4 s own reuse and no family timer. CSR and QAFriend gain 28 HP; QAFar and QAOther stay at 100. Retain stock RPCs and the four measured recipient results."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Group Heal needs the isolated fixture server.");
        var members = new List<HeadlessPlayer>();
        foreach (var (handle, x, z) in Stands)
        {
            var actor = await HeadlessPlayer.LoginAsync(context.Address, handle, handle, stockCharacterLoad: true);
            context.Helpers.Add(handle, actor);
            await actor.EnterWorldAsync();
            byte[] position = actor.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["Position"];
            Check.Equal(((double)x, 495d, (double)z),
                (BitConverter.ToDouble(position, 0), BitConverter.ToDouble(position, 8), BitConverter.ToDouble(position, 16)),
                $"{handle} enters at the separate picking stand");
            Check.Equal(Wound, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(actor), $"{handle} starts at the staged wound");
            NativeQaReadiness.RequireNoAmbientAggro(server, actor);
            members.Add(actor);
        }
        if (context.Server == null)
            return Array.Empty<byte>();

        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        await caster.EnterWorldAsync();
        var selected = await caster.WaitForViewNamedAsync(members[2].Handle);
        foreach (var member in members.Take(2))
            await InviteAsync(caster, member);

        int start = server.DebugLog().Length;
        int timer = caster.World.RpcSnapshot().Count;
        byte[] request = EvidenceRetestWire.Cast(caster, GroupHeal, selected);
        await EvidenceRetestWire.CastBarAsync(caster, GroupHeal, 4);
        await EvidenceRetestWire.TimerAsync(caster, timer, GroupHeal, 4, 4);
        await EvidenceRetestWire.TimerAsync(caster, timer, GroupHeal, 4, 4, ended: true);
        var results = new List<object>();
        foreach (var actor in new[] { caster }.Concat(members))
        {
            bool included = actor == caster || actor == members[0];
            ushort health = await AbilityEvidenceDirectHealsMode.ReadHealthAsync(actor);
            Check.Equal(included ? (ushort)128 : Wound, health, $"{actor.Handle} Group Heal recipient HP");
            Check.Equal(included, server.DebugLog()[start..].Contains($"Heal {GroupHeal} -> {actor.Handle} for 28",
                StringComparison.Ordinal), $"{actor.Handle} Group Heal landing");
            Check.Equal(0, actor.World.Problems.Count, $"{actor.Handle} packet framing");
            results.Add(new { actor.Handle, Health = health });
        }
        members[0].SendChat($"?health {Wound}");
        Check.Equal(Wound, await AbilityEvidenceDirectHealsMode.ReadHealthAsync(members[0]), "Restore the near helper's wound");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "group-heal-proof.json"),
            JsonSerializer.Serialize(new { Rpc = Convert.ToHexString(request), Results = results }));
        return Array.Empty<byte>();
    }

    private static async Task InviteAsync(HeadlessPlayer caster, HeadlessPlayer member)
    {
        static byte[] NameBytes(string name) => BitConverter.GetBytes((ushort)(name.Length + 1))
            .Concat(Encoding.ASCII.GetBytes(name)).Append((byte)0).ToArray();
        var target = NameBytes(member.Handle);
        caster.SendRpc(new byte[] { 0x6e, 3, 6, 0 }.Concat(BitConverter.GetBytes((ushort)(6 + target.Length)))
            .Concat(target).Concat(NameBytes("")).ToArray());
        await member.WaitForRpcAsync("80 8f 0c 00");
        member.SendRpc(new byte[] { 0x6f, 3, 8, 0, 0, 0, 0, 0 }.Concat(NameBytes(caster.Handle)).ToArray());
        await caster.WaitForRpcAsync($"70 03 {WireFormat.ToHex(BitConverter.GetBytes(member.CharId))} 09 00");
    }
}
