using System.Buffers.Binary;
using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ProxyCoderDerezMode : NativeQaMode
{
    private const ushort Derez = 26;
    private const byte Level = 18;
    private static readonly (string Name, int X, int Z)[] Stands =
    {
        ("QAFriend", 25300, -1850), ("QAEnemy", 24300, -1850), ("QAFar", 26420, -1498)
    };

    public override string Name => "ability-parity-proxy-coder-derez";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17, 1008, 22, Derez }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,healthC=1000,healthM=1000,innerStrC=200,innerStrM=200,pvpflag=0 WHERE charId IN (110,6111,6112,6114)",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "UPDATE characters SET alignment=1 WHERE charId IN (110,6111)",
            "UPDATE characters SET alignment=2 WHERE charId IN (6112,6114)",
            "UPDATE characters SET x=31000+100*(charId-6111),z=-1498 WHERE charId IN (6111,6112,6114)",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)"
        }, DevCommands: true, loadAbilityAncestors: true, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10"
        }, readyMessages: new[]
        {
            "After CSR is fully in world, use helper chat requests to move QAFriend to ?tp 25300 495 -1850 0 0 0 and QAEnemy to ?tp 24300 495 -1850 0 0 0. Also move hostile QAFar to ?tp 26420 495 -1498 0 0 0. Have all three helpers use ?pet summon 22. Their Nuker Simulacra spawn 1.5 m east of the distinct stands, putting QAFar’s 17.7 m away.",
            "Use Coder Style and stay outside interlock. With no selection, then with WinterFrost, then with QAFriend's or QAFar's pet selected, try Proxy Coder from Actions. The client may suppress an invalid request. If sent, it should receive refusal without cast or reuse.",
            "Select QAEnemy's Nuker Simulacrum and use Proxy Coder. Capture the full request, 40 IS, 2s cast, 13.5s own reuse, the derezzes line, and pet death/removal. QAEnemy and the friendly simulacrum stay alive."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Proxy Coder server.");
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await helper.EnterWorldAsync();
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        await caster.EnterWorldAsync();
        caster.SendRpc("3e 6b f0 03 00 00 00 00");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        foreach (var stand in Stands)
        {
            var helper = context.Helpers[stand.Name];
            helper.SendChat($"?tp {stand.X} 495 {stand.Z} 0 0 0");
            await caster.WaitForViewNamedAsync(stand.Name);
            helper.SendChat("?pet summon 22");
            await helper.WaitForRpcAsync("81 76");
        }
        var friend = context.Helpers["QAFriend"];
        var enemy = context.Helpers["QAEnemy"];
        var friendPet = await SeePetAsync(caster, friend.CharId);
        var enemyPet = await SeePetAsync(caster, enemy.CharId);
        var far = context.Helpers["QAFar"];
        var farPet = await SeePetAsync(caster, far.CharId);
        var npc = await caster.WaitForViewNamedAsync("WinterFrost");
        var refusals = new List<string>();
        foreach (uint handle in new[] { 0u, Handle(npc), Handle(friendPet), Handle(farPet), Handle(enemyPet) ^ 0x10000 })
        {
            int start = caster.World.RpcSnapshot().Count;
            caster.SendRpc(Use(handle));
            var denial = await StockAbilityProfile.ObserveAsync(caster, start,
                packet => BytePattern.Parse("80 ba 1a 00").IsMatch(packet.Bytes), "invalid Proxy Coder selection refusal");
            Check.That(!caster.World.RpcSnapshot().Skip(start).Any(rpc => BytePattern.Parse("80 ac 1a 00").IsMatch(rpc.Bytes) ||
                BytePattern.Parse("80 bd 01 1a 00").IsMatch(rpc.Bytes)), "Refusal never starts a cast or reuse");
            refusals.Add(denial.Hex);
            await Task.Delay(TimeSpan.FromMilliseconds(600));
        }

        int enemyAt = enemy.World.RpcSnapshot().Count;
        int friendAt = friend.World.RpcSnapshot().Count;
        int farAt = far.World.RpcSnapshot().Count;
        int casterAt = caster.World.RpcSnapshot().Count;
        byte[] request = Use(Handle(enemyPet));
        caster.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(caster, Derez, 2);
        await EvidenceRetestWire.TimerAsync(caster, casterAt, Derez, 4, 13.5f);
        byte[] message = AbilityMessages.AboutAbility(0xc6000038, true, Derez, caster.Handle, enemyPet.Name);
        var derez = await StockAbilityProfile.ObserveAsync(caster, casterAt,
            packet => packet.Bytes.AsSpan().SequenceEqual(message), "sourced Proxy Coder derez caster line");
        var removed = await StockAbilityProfile.ObserveAsync(enemy, enemyAt,
            packet => BytePattern.Parse("81 77").IsMatch(packet.Bytes), "only the target owner's pet window is removed");
        await caster.WaitForViewUpdateAsync(enemyPet.ViewId, "04 80 80 80 c0 00 00 c0 0d 06 00 28");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 10 a0 00");
        Check.That(!friend.World.RpcSnapshot().Skip(friendAt).Concat(far.World.RpcSnapshot().Skip(farAt))
            .Any(rpc => BytePattern.Parse("81 77").IsMatch(rpc.Bytes)), "The friendly and out-of-range pet windows remain active");
        Check.That(server.DebugLog().Contains("[PET] Proxy Coder derezzed Nuker Simulacrum of QAEnemy for 13000"),
            "The derez uses the fixed GOB amount on the selected pet");
        Check.Equal(0, caster.World.Problems.Count + enemy.World.Problems.Count + friend.World.Problems.Count + far.World.Problems.Count, "Proxy Coder packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "proxy-coder-derez.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(request), Refusals = refusals, Derez = derez.Hex, Removed = removed.Hex }));
        return Array.Empty<byte>();
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);

    private static byte[] Use(uint selectedHandle)
    {
        // A captured full targeted Coder request (PR312 Static Blast); replace ability and live target handle.
        byte[] request = Convert.FromHexString("80b91a00050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), selectedHandle);
        return request;
    }

    private static async Task<ViewCreation> SeePetAsync(HeadlessPlayer caster, uint owner)
    {
        ViewCreation? pet = null;
        await caster.WaitUntilAsync(() => (pet = caster.World.CreationSnapshot().LastOrDefault(view =>
            view.Name == "Nuker Simulacrum" && view.Attributes.TryGetValue("OwnerCharacterID", out var field) &&
            BitConverter.ToUInt32(field) == owner)) != null, $"Nuker Simulacrum owned by {owner}", HeadlessPlayer.DefaultTimeout);
        return pet!;
    }
}
