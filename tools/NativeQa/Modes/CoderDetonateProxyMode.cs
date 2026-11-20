using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class CoderDetonateProxyMode : NativeQaMode
{
    private const ushort LogicDaemon = 22, Detonate = 34, CoderStyle = 1008;
    // Complete native PR312 cast from b0d0eb7ff285:10220.
    private const string StockDetonate = "80b92200000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string StockCoderStyle = "80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private const string StartPattern = @"Detonate Proxy: (\d+) to (\d+) NPC.*start ([0-9.]+)";
    private const string EndPattern = @"Detonate Proxy term: pet \d+, at ([0-9.]+)";

    public override string Name => "ability-parity-coder-detonate-proxy";

    public override NativeQaModePlan Plan { get; } = new(
        new ushort[] { 2, 17, 26, 1008, LogicDaemon, Detonate }, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110"
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: "coder-detonate-proxy.csv",
        hotbarAbilities: new[] { CoderStyle, LogicDaemon, Detonate },
        extraQaMetadata: new() { ["ScopedStockRows"] = "1008,22,34", ["NativeCloseableRows"] = 0 },
        environmentOverrides: new() { ["MXO_COMBAT_FOUNDATIONS_QA"] = "1", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Use ?combatdepth foundations-detonate for the nearby fixed targets, then stay on the seeded stand. Enter submits the command. Use page 2 slot 1 Coder Style and slot 2 Logic Daemon 1.0. Set the summoned pet Passive.",
            "Keep selection clear without Escape. Cast Detonate Proxy from page 2 slot 3. Retain the 2s cast and 60s reuse, HP and visible FX on neutral and viral-resistant recipients, and pet removal after its half-second term.",
            "Detonate Resisted should lose less HP than Detonate Neutral. Detonate Outside is beyond 8m from the pet and should stay full. Detonate Credit should die and credit normal XP/loot to CSR. Record exact damage and the pet HP before detonation."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer ?? throw new InvalidOperationException("Expected the disposable Detonate server.");
        await actor.EnterWorldAsync();
        actor.SendChat("?combatdepth foundations-detonate");
        await actor.WaitForTextAsync("Detonate fixture ready");
        var neutral = await actor.WaitForViewNamedAsync("Detonate Neutral");
        var resisted = await actor.WaitForViewNamedAsync("Detonate Resisted");
        var outside = await actor.WaitForViewNamedAsync("Detonate Outside");
        var credit = await actor.WaitForViewNamedAsync("Detonate Credit");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StockCoderStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        actor.SendChat("?pet summon 22");
        await actor.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(18));
        var pet = await actor.WaitForViewNamedAsync("Nuker Simulacrum");
        actor.SendRpc("81 7c fe 05 02");

        ushort maxHp = BitConverter.ToUInt16(pet.Attributes["MaxHealth"]);
        ushort health = BitConverter.ToUInt16(pet.Attributes["Health"]);
        int raw = CoderPets.DetonateValue(health, maxHp);
        int reduced = TypedAbilityResistance.Amount(raw, 7, 7, 75);
        Check.That(reduced < raw, "A level-seven daemon meets a real positive viral resistance");
        int cursor = actor.World.ViewSnapshot().Count;
        int rpcCursor = actor.World.RpcSnapshot().Count;
        byte[] detonate = Convert.FromHexString(StockDetonate);
        actor.SendRpc(detonate);
        await EvidenceRetestWire.CastBarAsync(actor, Detonate, 2);
        await EvidenceRetestWire.TimerAsync(actor, rpcCursor, Detonate, 4, 60);
        await Hit(neutral, 5000 - raw);
        await Hit(resisted, 5000 - reduced);
        await Hit(credit, 0);
        await actor.WaitForRpcAsync("81 77");
        var excluded = BytePattern.Parse($"{EvidenceRetestWire.Le(outside.ViewId)} 04 80 80 80 c0 ?? ?? c0 43 0a 00 28");
        Check.That(!actor.World.ViewSnapshot().Skip(cursor).Any(excluded.Occurs), "The outside target receives no blast");
        await actor.WaitUntilAsync(() => server.DebugLog().Contains($"Awarded {KillExperience.ForKill(50, 50)} experience") &&
            Regex.IsMatch(server.DebugLog(), EndPattern), "The blast's lethal damage reaches ordinary owner rewards and the pet term", TimeSpan.FromSeconds(10));
        string log = server.DebugLog();
        var start = Regex.Match(log, StartPattern);
        var term = Regex.Match(log, EndPattern);
        Check.That(start.Success && int.Parse(start.Groups[1].Value) == raw, "The live pet HP determines the blast request");
        double delay = double.Parse(term.Groups[1].Value, CultureInfo.InvariantCulture) -
            double.Parse(start.Groups[3].Value, CultureInfo.InvariantCulture);
        Check.That(delay is >= .49 and < 1.5, "The original pet terminates after the authored half-second delay");
        Check.Equal(0, actor.World.Problems.Count, "Detonate packet framing");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "coder-detonate-proxy.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(detonate), Raw = raw, Resisted = reduced,
                PetMaxHealth = maxHp, DelaySeconds = delay, OutsideHit = false, OwnerReward = true }));
        return Array.Empty<byte>();

        async Task Hit(ViewCreation victim, int hp) => await actor.WaitForViewUpdateAsync(victim.ViewId,
            $"04 80 80 80 c0 {EvidenceRetestWire.Le((ushort)hp)} c0 43 0a 00 28");
    }

}
