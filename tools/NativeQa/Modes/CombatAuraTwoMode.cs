using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class CombatAuraTwoMode : NativeQaMode
{
    private const ushort Aura = 485, Pulse = 696;
    private const int X = 17000, Y = 495, Z = 6400;
    private const string StockStart = "80 b9 e5 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 20 00 00 00 78 f2 12 00 08 6b 00 00 00 00 00";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Aura).Order().ToArray();

    public override string Name => "ability-parity-combat-aura-two-shortfall";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET y={Y},rotation=0,innerStrC=51,innerStrM=1000 WHERE charId=110",
            "UPDATE char_abilities SET level=LEAST(level,50) WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Aura }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1"
        }, extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "485", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost)
        }, readyMessages: new[]
        {
            $"CSR starts at ({X},{Y},{Z}) on clear outdoor NAV. Level 50, {Loadout.Sum(AbilityMemory.Cost)}/100 with prerequisites. Page 2 slot 1 is Combat Aura 2.0. No target or helper is needed.",
            "Press slot 1 once and stay still. Expect [ABILITY-COST] paid=15 IS=51->36, parent 485 on, and the 696 modifiers now and at each paid upkeep. Watch IS step to 21, then 6. The third upkeep attempt is nine IS short, so it ends the aura without another debit or pulse.",
            "Save that packet group. Expect two linked parent removes, the 15 s own timer, the final drain-record remove, exclusive off, then localized 81 67 token c60020b4. The last admitted 696 pair expires on its own lifetime. This check credits the parent lifetime; the archived child damage magnitude remains separate.",
            "No [PVP] action or Health change is expected. Trying the shortcut during the 15 s reuse cannot restart it. At 6 IS after reuse, a new activation cannot pay its 15 IS cost."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Check.That(NavMesh.Locate(1, X, Y, Z, out var floor) && !floor.indoors && floor.floorY + 95 == Y,
            "Combat Aura 2.0 starts on verified outdoor NAV");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        await actor.EnterWorldAsync();
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(EngageFoesFixture.Position(self).SequenceEqual(new double[] { X, Y, Z }), "Exact startup stand");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        DisablingShotMode.CheckProfile(actor, Loadout, 50);

        int start = actor.World.RpcSnapshot().Count;
        int viewStart = actor.World.ViewSnapshot().Count;
        actor.SendRpc(StockStart);
        await actor.WaitForRpcAsync("80 bd 05 e5 01 00 00 00 00 00 01");
        await actor.WaitForRpcAsync("81 67 ?? ?? ?? ?? b4 20 00 c6", TimeSpan.FromSeconds(20));

        byte[][] records = actor.World.RpcSnapshot().Skip(start).Select(frame => frame.Bytes).ToArray();
        int carrier = Array.FindIndex(records, packet => ParentRemoval(packet, 710));
        int marker = Array.FindIndex(records, packet => ParentRemoval(packet, 0));
        int reuse = Array.FindIndex(records, packet => packet.AsSpan().SequenceEqual(WireFormat.FromHex("80 bd 01 e5 01 04 00 00 70 41 01")));
        int drain = Array.FindIndex(records, packet => ParentRemoval(packet, 681));
        int off = Array.FindIndex(records, packet => packet.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(Aura, false)));
        int notice = Array.FindIndex(records, packet => packet.Length >= 16 && packet[0] == 0x81 && packet[1] == 0x67 &&
            BitConverter.ToUInt32(packet, 6) == AbilityAuras.DrainDeactivated &&
            BitConverter.ToInt32(packet, packet.Length - 4) == 9);
        Check.That(carrier >= 0 && carrier < marker && marker < reuse && reuse < drain && drain < off && off < notice,
            "Retained stop order: aura carrier, term marker, own reuse, upkeep record, exclusive off, localized nine-IS deficit");
        Check.Equal(3, records.Take(notice).Count(packet => ManageBonusRecord.TryDecode(packet, out var bonus) &&
            bonus!.SourceId == Pulse && bonus.TargetId == 159 && bonus.AddRemoveType == 0), "Only the initial and two paid child pulses land");
        var balances = actor.World.ViewSnapshot().Skip(viewStart).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).ToArray();
        Check.That(balances.Contains((ushort)36) && balances.Contains((ushort)21) && balances.Contains((ushort)6),
            "The stock owner receives the activation debit and two full upkeep debits");
        Console.WriteLine("COMBAT AURA TWO BOOTSTRAP PASS: exact stock activation, two paid upkeeps and retained shortfall order.");
        return Array.Empty<byte>();
    }

    private static bool ParentRemoval(byte[] packet, ushort target) =>
        ManageBonusRecord.TryDecode(packet, out var bonus) && bonus!.SourceId == Aura &&
        bonus.TargetId == target && bonus.AddRemoveType == 1;
}
