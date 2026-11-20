using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class TinkeringTargetAdmissionMode : NativeQaMode
{
    private const int Level = 11, Rank = 4;
    private const ushort CoderStyle = 1008;
    private const string Notice = "Select a living enemy before using this ability.";
    private const string Tinkering = "80b924000400020000000000000000000000000000000000000000007802c50001000000086b0000000000";
    private const string AdvancedWithoutTarget = "80b90202000000000000000000000000000000000000000000000000585cac0001000000086b0000000000";
    private const string AdvancedSelected = "80b902020400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 36, 514, CoderStyle).Order().ToArray();

    public override string Name => "ability-parity-tinkering-target-admission";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=200,innerStrM=200,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END WHERE charId IN (110,6114)",
            $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110",
            "UPDATE characters SET x=25200,z=-1498 WHERE charId=6114"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, readyMessages: new[]
        {
            "Choose Coder Style. Clear selection and try Tinkering (36) and Advanced Tinkering (514). Capture the view-zero request if sent. It should show Select a living enemy before using this ability. Verify there is no IS debit, cast bar or reuse.",
            "Select QAEnemy 4 m away and immediately try the ability again. The ordinary 6 s cast should land for 4 with Tinkering or 6 with Advanced Tinkering, with 30 s own reuse."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var target = await caster.WaitForViewNamedAsync(enemy.Handle);
        await enemy.WaitForViewNamedAsync(caster.Handle);
        await StarterFixedDamageMode.SelectStyle(caster, self, CoderStyle, 172);

        var proof = new List<object>();
        foreach (var sample in new[] { (Id: (ushort)36, Damage: 4, Capture: Tinkering), (Id: (ushort)514, Damage: 6, Capture: AdvancedSelected) })
        {
            byte[] none = sample.Id == 514 ? Convert.FromHexString(AdvancedWithoutTarget) : WithHandle(sample.Capture, 0);
            var missing = await RefusedAsync(caster, sample.Id, none);
            ushort wrongSpawn = unchecked((ushort)(target.SpawnId + 1));
            byte[] stale = WithHandle(sample.Capture, DuelPackets.Handle(target.ViewId, wrongSpawn));
            var expired = await RefusedAsync(caster, sample.Id, stale);
            byte[] selected = await StarterFixedDamageMode.Land(caster, enemy, target, sample.Id, sample.Damage, 6, 30, sample.Capture);
            proof.Add(new { sample.Id, sample.Damage, Missing = Convert.ToHexString(none), MissingNotice = missing.Hex,
                Stale = Convert.ToHexString(stale), StaleNotice = expired.Hex, Selected = Convert.ToHexString(selected) });
        }
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count, "Tinkering request and response framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "tinkering-target-admission.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static byte[] WithHandle(string capture, uint handle)
    {
        byte[] bytes = Convert.FromHexString(capture);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), handle);
        return bytes;
    }

    internal static async Task<RpcRecord> RefusedAsync(HeadlessPlayer caster, ushort id, byte[] request)
    {
        int rpcs = caster.World.RpcSnapshot().Count;
        var priorViews = caster.World.ViewSnapshot();
        ushort before = BitConverter.ToUInt16(caster.World.CreationSnapshot().Last(view => view.ViewId == 2).Attributes["InnerStrengthAvailable"]);
        foreach (byte[] bytes in priorViews)
            if (BuffDrainLifetimeFixture.TryInnerStrength(bytes, out ushort value))
                before = value;
        caster.SendRpc(request);
        var chat = await StockAbilityProfile.ObserveAsync(caster, rpcs,
            packet => packet.Bytes.AsSpan().StartsWith(new byte[] { 0x2e, 0xc7 }) &&
                System.Text.Encoding.ASCII.GetString(packet.Bytes).Contains(Notice, StringComparison.Ordinal), $"target refusal for {id}");
        int afterNotice = caster.World.RpcSnapshot().Count;
        var barrier = BytePattern.Parse("81 49");
        caster.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(caster, afterNotice, packet => barrier.IsMatch(packet.Bytes), "post-refusal barrier");
        string idHex = StockAbilityProfile.LittleEndianHex(id);
        var bar = BytePattern.Parse($"80 ac {idHex}");
        var reuse = BytePattern.Parse($"80 bd 01 {idHex}");
        Check.That(!caster.World.RpcSnapshot().Skip(rpcs).Any(packet => bar.IsMatch(packet.Bytes) || reuse.IsMatch(packet.Bytes)),
            $"No {id} cast bar or reuse for an invalid target");
        Check.That(!caster.World.ViewSnapshot().Skip(priorViews.Count).Any(bytes => BuffDrainLifetimeFixture.TryInnerStrength(bytes, out ushort value) && value < before),
            $"No {id} Inner Strength debit for an invalid target");
        return chat;
    }
}
