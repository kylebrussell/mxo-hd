using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterSoldierDamageBuffsMode : NativeQaMode
{
    private const ushort Intensify = 185, Firearms = 124, Prowess = 135;
    private const byte Level = 19;

    // Stock complete self casts: c15509643a25:1003, 0940f9af648d:412, 33779fd94eb0:4314.
    private const string IntensifyRequest = "80b9b900000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    private const string FirearmsRequest = "80b97c000000000000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string ProwessRequest = "80b98700000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";

    public override string Name => "ability-parity-soldier-damage-buff-stacking";
    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, Intensify, Firearms, Prowess }, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { Intensify, Firearms, Prowess },
        extraQaMetadata: new() { ["ScopedStockRows"] = "185,124,135", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Level 19, no target or combat needed. Open Status and page 2. Use slot 1 Intensify Damage for +1 melee and +1 ballistic. Immediately use slot 2 Firearms Skill. Check both Intensify modifiers disappear and Firearms adds +9 ballistic damage.",
            "While Firearms Skill is active, cast slot 3 Martial Arts Prowess. Check Firearms is removed before the captured +40 melee bonus appears. After 20 seconds Prowess goes away and both damage values return to baseline.",
            "Capture the buff records, removal order and native reuse icons. All three share the shipped stacking type 38."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Soldier server.");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var proof = new List<string>();

        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(IntensifyRequest));
        await RecordAsync(cursor, Intensify, 159, 1, 15);
        await RecordAsync(cursor, Intensify, 171, 1, 15);

        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(FirearmsRequest));
        var meleeOff = await RecordAsync(cursor, Intensify, 159, 1, 15, remove: true);
        var ballisticOff = await RecordAsync(cursor, Intensify, 171, 1, 15, remove: true);
        var firearmsOn = await RecordAsync(cursor, Firearms, 171, 9, 20);
        var sequence = actor.World.RpcSnapshot().ToList();
        Check.That(sequence.IndexOf(meleeOff) < sequence.IndexOf(firearmsOn) &&
            sequence.IndexOf(ballisticOff) < sequence.IndexOf(firearmsOn),
            "Both Intensify records clear before Firearms applies");

        cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(Convert.FromHexString(ProwessRequest));
        var firearmsOff = await RecordAsync(cursor, Firearms, 171, 9, 20, remove: true);
        var prowessOn = await RecordAsync(cursor, Prowess, 159, 40, 20);
        sequence = actor.World.RpcSnapshot().ToList();
        Check.That(sequence.IndexOf(firearmsOff) < sequence.IndexOf(prowessOn), "Firearms clears before Prowess applies");
        byte[] timer = AbilityCooldowns.BuildTimer(Prowess, AbilityCooldowns.OwnTimer, 90, true);
        await StockAbilityProfile.ObserveAsync(actor, cursor, rpc => rpc.Bytes.AsSpan().SequenceEqual(timer), "Prowess owns a 90-second reuse");
        await RecordAsync(cursor, Prowess, 159, 40, 20, remove: true, timeout: TimeSpan.FromSeconds(23));
        Check.Equal(0, actor.World.Problems.Count, "Soldier buff replacement and expiry framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "soldier-damage-buffs.json"), JsonSerializer.Serialize(proof));
        return Array.Empty<byte>();

        async Task<RpcRecord> RecordAsync(int start, ushort source, ushort stat, int value, int seconds, bool remove = false, TimeSpan? timeout = null)
        {
            bool Match(RpcRecord rpc) => StockAbilityProfile.Bonus(rpc, source, stat, value, seconds, remove);
            await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(start).Any(Match),
                $"{source}/{stat} value {value} remove={remove}", timeout ?? HeadlessPlayer.DefaultTimeout);
            var record = actor.World.RpcSnapshot().Skip(start).First(Match);
            proof.Add(record.Hex);
            return record;
        }
    }
}
