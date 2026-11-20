using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerControlTargetMode : NativeQaMode
{
    private const int Level = 14, Rank = 12;
    private const ushort HackerStyle = 1006;
    private sealed record Sample(ushort Id, string Name, int Reuse, ushort State, int Seconds, string Request);

    // Native 6971d24474bd:4113, 5109 and 4933. Only the destination handle changes.
    private static readonly Sample[] Samples =
    {
        new(40, "Code Freeze 1.0", 58, 99, 25, "80b92800040002000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000"),
        new(43, "Disrupt Inputs 1.0", 43, 90, 16, "80b92b00060004000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000"),
        new(48, "Freeze System 1.0", 43, 85, 6, "80b93000070005000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000")
    };

    public override string Name => "ability-parity-starter-hacker-control-targets";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, HackerStyle }.Concat(Samples.Select(sample => sample.Id))
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray(),
        CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=200,innerStrM=200 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Rank}) WHERE char_id=110"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        }, readyMessages: new[]
        {
            "Use Hacker Style outside interlock. Clear selection and try Code Freeze, Disrupt Inputs and Freeze System. Expect one native Target is not valid line for each request sent, with no IS debit, cast bar or reuse.",
            "After each refusal, select Melee Dummy and retry immediately. Expect a two-second cast and the matching Rooted 25s, Blinded 16s or Pacified 6s effect. Note the 58s, 43s and 43s own reuse respectively."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var loadout = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]);
        Check.Equal(37, loadout.Sum(row => AbilityMemory.Cost(AbilityCatalog.IdOf(row.Descriptor))), "Level-14 profile loads its 37-point prerequisite closure");
        var server = context.ActualServer!;
        await caster.EnterWorldAsync();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        uint handle = DuelPackets.Handle(dummy.ViewId, dummy.SpawnId);
        await StarterFixedDamageMode.SelectStyle(caster, self, HackerStyle, 172);
        var proof = new List<object>();
        foreach (var sample in Samples)
        {
            var missing = await RefusedAsync(caster, sample.Id, WithHandle(sample.Request, 0));
            var stale = await RefusedAsync(caster, sample.Id, WithHandle(sample.Request, handle ^ 0x10000));
            int start = caster.World.RpcSnapshot().Count;
            byte[] request = WithHandle(sample.Request, handle);
            caster.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, 2);
            await EvidenceRetestWire.TimerAsync(caster, start, sample.Id, AbilityCooldowns.OwnTimer, sample.Reuse);
            string prefix = $"[EFFECTS] {sample.Id} on {dummy.Name}:";
            string state = $"State {sample.State} 0 {sample.Seconds} s";
            await caster.WaitUntilAsync(() => server.DebugLog().Split('\n').Any(line => line.Contains(prefix) && line.Contains(state)),
                $"{sample.Name} applies {state}", HeadlessPlayer.DefaultTimeout);
            proof.Add(new { sample.Id, Request = Convert.ToHexString(request), Missing = missing.Hex, Stale = stale.Hex, sample.Reuse, state });
        }
        Check.Equal(0, caster.World.Problems.Count, "Control request framing");
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "starter-hacker-control-targets.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();
    }

    private static byte[] WithHandle(string capture, uint handle)
    {
        byte[] request = Convert.FromHexString(capture);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), handle);
        return request;
    }

    private static async Task<RpcRecord> RefusedAsync(HeadlessPlayer caster, ushort id, byte[] request)
    {
        int start = caster.World.RpcSnapshot().Count;
        var views = caster.World.ViewSnapshot();
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        ushort before = BitConverter.ToUInt16(self.Attributes["InnerStrengthAvailable"]);
        foreach (var view in views)
            if (BuffDrainLifetimeFixture.TryInnerStrength(view, out ushort available)) before = available;
        caster.SendRpc(request);
        var refusal = await StockAbilityProfile.ObserveAsync(caster, start,
            rpc => AbilityRefusals.TryDecode(rpc.Bytes, out var record) && record.AbilityID == id && record.ErrorID == 24,
            $"{id} invalid selected target");
        int barrierStart = caster.World.RpcSnapshot().Count;
        caster.SendRpc("81 48");
        var barrier = BytePattern.Parse("81 49");
        await StockAbilityProfile.ObserveAsync(caster, barrierStart, rpc => barrier.IsMatch(rpc.Bytes), "post-refusal barrier");
        string idHex = StockAbilityProfile.LittleEndianHex(id);
        var cast = BytePattern.Parse($"80 ac {idHex}");
        var reuse = BytePattern.Parse($"80 bd 01 {idHex}");
        Check.That(!caster.World.RpcSnapshot().Skip(start).Any(rpc => cast.IsMatch(rpc.Bytes) || reuse.IsMatch(rpc.Bytes)),
            $"Invalid target does not start a cast or reuse for {id}");
        Check.That(!caster.World.ViewSnapshot().Skip(views.Count).Any(view =>
            BuffDrainLifetimeFixture.TryInnerStrength(view, out ushort available) && available < before),
            $"Invalid target does not debit Inner Strength for {id}");
        return refusal;
    }
}
