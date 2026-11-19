using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CoderStaticBlastMode : NativeQaMode
{
    private const ushort Blast = 33;

    public override string Name => "ability-parity-coder-static-blast";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 1008, Blast }, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            "DELETE FROM inventory WHERE charId=110 AND slot IN (2,105); INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,7899,2,1,0)"
        }, DevCommands: true, loadAbilityAncestors: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_COMBAT_FOUNDATIONS_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: new[]
        {
            "Run ?combatdepth foundations-cone. Use Coder Style and stay out of interlock. Select AB1 Aim and activate Static Blast 1.0 from Actions. Capture the full request, 15 IS, 2s cast, 6s own reuse, a landed hit and HP decrease of 27.",
            "After reuse, aim at AB1 Resisted 2m west. Expect 22 damage with +75 Viral Resistance and a targeted hit FX. The fixture forces only ordinary viral contest draws so you can measure damage."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Static Blast server.");
        await actor.EnterWorldAsync();
        actor.SendChat("?combatdepth foundations-cone");
        await actor.WaitForTextAsync("AB1 Outside stays full");
        actor.SendRpc("3e 6b f0 03 00 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var proof = new List<object>();
        foreach (var (name, damage) in new[] { ("AB1 Aim", 27), ("AB1 Resisted", 22) })
        {
            var victim = await actor.WaitForViewNamedAsync(name);
            int rpcAt = actor.World.RpcSnapshot().Count;
            // PR312 stock targeted Static Blast, b0d0eb7ff285:6342, with only the live target handle replaced.
            byte[] cast = Convert.FromHexString("80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
            BinaryPrimitives.WriteUInt32LittleEndian(cast.AsSpan(4), DuelPackets.Handle(victim.ViewId, victim.SpawnId));
            actor.SendRpc(cast);
            await EvidenceRetestWire.CastBarAsync(actor, Blast, 2);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, Blast, 4, 6);
            await actor.WaitForViewUpdateAsync(victim.ViewId,
                $"04 80 80 80 c0 {EvidenceRetestWire.Le((ushort)(5000 - damage))} c0 ad 08 00 28");
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, Blast, 4, 6, ended: true);
            proof.Add(new { Target = name, Damage = damage, Request = Convert.ToHexString(cast) });
        }
        Check.That(server.DebugLog().Contains("[TYPED-RESISTANCE OURS] 33 type2 total75 raw27 final22"),
            "Static Blast uses the matching viral resistance consumer with scaled raw 27");
        Check.Equal(0, actor.World.Problems.Count, "Static Blast packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "static-blast.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();
    }
}
