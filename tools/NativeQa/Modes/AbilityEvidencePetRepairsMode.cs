using System.Buffers.Binary;
using System.Text.Json;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class AbilityEvidencePetRepairsMode : NativeQaMode
{
    private sealed record Sample(ushort Id, ushort Expected, float Reuse, uint Effect);
    private static readonly Sample[] Samples =
    {
        new(30, 20, 13, 671090845), new(31, 34, 28, 671090849),
        new(587, 117, 28, 671090853), new(596, 174, 28, 671090857), new(615, 83, 30, 671091041)
    };

    public override string Name => "ability-evidence-pet-repairs";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 20, 594 }.Concat(Samples.Select(sample => sample.Id)).ToArray(), 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            "UPDATE characters SET healthC=500,healthM=5000,innerStrC=2000,innerStrM=2000,y=495 WHERE charId IN (110,6111,6112)",
            "UPDATE characters SET x=CASE WHEN charId=6111 THEN 24600 ELSE 24800 END,z=-1498 WHERE charId IN (6111,6112)",
            "DELETE FROM inventory WHERE charId=110"
        }, loadAbilityAncestors: true, environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "From Actions, summon Remote Proxy 5.0 (594) and select the pet by clicking its model. Cast Fortify (20) to create a 750 HP deficit by raising the live maximum.",
            "With the own proxy selected, cast Repair Simulacra 1–4 and Fix Simulacra (30 +20, 31 +34, 587 +117, 596 +174, 615 +83). Capture each view update and stable maximum.",
            "Wait for each two-second cast to finish. Then select QAFriend and attempt Repair Simulacra 1.0 or Fix Simulacra. The stock client can send an activation and the server replies 'That is not your simulacrum.' See docs/ABILITY-EVIDENCE-RETESTS.md."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var ally = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", ally);
        await ally.EnterWorldAsync();
        if (context.Server == null) return Array.Empty<byte>();

        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "QAFar", "QAFar");
        await caster.EnterWorldAsync();
        var friend = await caster.WaitForViewNamedAsync(ally.Handle);
        EvidenceRetestWire.Cast(caster, 594);
        var registration = await caster.WaitForRpcAsync("81 76");
        byte simulacrumId = registration.Bytes[3];
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Any(view => view.Attributes.ContainsKey("SimulacraID")),
            "ordinary proxy creation", HeadlessPlayer.DefaultTimeout);
        var pet = caster.World.CreationSnapshot().Last(view => view.Attributes.ContainsKey("SimulacraID"));
        ushort health = BinaryPrimitives.ReadUInt16LittleEndian(pet.Attributes["Health"]);
        ushort baseMax = BinaryPrimitives.ReadUInt16LittleEndian(pet.Attributes["MaxHealth"]);
        Check.Equal(baseMax, health, "Summoned proxy starts full");
        ushort fortifiedMax = checked((ushort)(baseMax + 750));
        int viewAt = caster.World.ViewSnapshot().Count;
        EvidenceRetestWire.Cast(caster, 20, pet);
        await EvidenceRetestWire.ViewSinceAsync(caster, viewAt, pet.ViewId,
            $"04 80 80 84 {EvidenceRetestWire.Le(fortifiedMax)} 40 {EvidenceRetestWire.Le(health)}");

        var refusals = new Dictionary<ushort, string>();
        foreach (var sample in Samples)
        {
            int beforeRefusal = caster.World.RpcSnapshot().Count;
            EvidenceRetestWire.Cast(caster, sample.Id, friend);
            var denied = await caster.WaitForRpcAsync("80 ba " + EvidenceRetestWire.Le(sample.Id));
            refusals.Add(sample.Id, denied.Hex);
            await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(beforeRefusal).Any(r =>
                System.Text.Encoding.ASCII.GetString(r.Bytes).Contains("That is not your simulacrum.", StringComparison.Ordinal)),
                $"selected player refusal for {sample.Id}", HeadlessPlayer.DefaultTimeout);
        }

        var proof = new List<object>();
        foreach (var sample in Samples)
        {
            viewAt = caster.World.ViewSnapshot().Count;
            int timerAt = caster.World.RpcSnapshot().Count;
            byte[] request = EvidenceRetestWire.Cast(caster, sample.Id, pet);
            await EvidenceRetestWire.CastBarAsync(caster, sample.Id, 2);
            await EvidenceRetestWire.TimerAsync(caster, timerAt, sample.Id, 4, sample.Reuse);
            ushort before = health;
            health = checked((ushort)(health + sample.Expected));
            Check.That(health < fortifiedMax, "Every repair measures an unclipped increase");
            string fx = WireFormat.ToHex(BitConverter.GetBytes(sample.Effect));
            await EvidenceRetestWire.ViewSinceAsync(caster, viewAt, pet.ViewId,
                $"04 80 80 80 c0 {EvidenceRetestWire.Le(health)} c0 {fx}");
            var window = await caster.WaitForRpcAsync(
                $"81 78 {simulacrumId:x2} 00 {EvidenceRetestWire.Le(health)} {EvidenceRetestWire.Le(fortifiedMax)}");
            proof.Add(new { sample.Id, Before = before, After = health, Max = fortifiedMax,
                RpcHex = Convert.ToHexString(request), PlayerTargetDenied = refusals[sample.Id], Window = window.Hex });
        }
        Check.Equal(0, caster.World.Problems.Count + ally.World.Problems.Count, "Own-pet repair packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ability-evidence-pet-repairs.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }
}
