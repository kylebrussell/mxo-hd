using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HackerCallbacksMode : NativeQaMode
{
    // Complete stock requests from proxy_024, lines 12961–62 and 13475.
    internal const string StockDeflectVirus = "80 b9 7a 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 15 00 00 e0 d1 2c 08 6b 00 00 00 00 00";
    internal const string StockDismissDeflect = "80 bb 7a 01";

    private static readonly (ushort Id, string Name, string Effect)[] Cases =
    {
        (61, "Miasma 1.0", "WinterFrost: −14 melee and ballistic defense for 5 s"),
        (89, "Static Field", "WinterFrost: repeated viral hits from stored raw 1 before influence"),
        (42, "Combat Aura 1.0", "self/team: melee and ballistic accuracy"),
        (485, "Combat Aura 2.0", "self/team: melee and ballistic damage"),
        (378, "Deflect Virus 1.0", "self/team: viral and melee defense, delayed FX, 4% cleanse per eligible debuff at child termination"),
        (380, "Deflect Bullets 1.0", "self/team: ballistic and thrown defense, concentration +10, delayed FX")
    };

    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(new ushort[] { 2, 17 }
        .Concat(Cases.Select(entry => entry.Id)).ToArray()).Order().ToArray();

    public override string Name => "hacker-callbacks";
    public override NativeQaModePlan Plan { get; } = new(
        Loadout, 24699, -1498,
        Array.Empty<string>(), Array.Empty<string>(),
        new[] { "UPDATE characters SET y=495,healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId IN (110,6111)" },
        Bootstrap: "hacker-callbacks-stock-world-entry",
        readyMessages: Cases.Select(entry =>
            $"{entry.Name} ({entry.Id}): cast {AbilityCosts.Cost(entry.Id)} IS, drain {AbilityAuras.DrainOf(entry.Id)} IS/{AbilityAuras.IntervalOf(entry.Id):g}s, child {AbilityAuras.EffectOf(entry.Id)}. {entry.Effect}. Toggle off before switching cases.")
            .Append("Use Actions and record each stock cast. Use the separate Mobius positive/negative modes for case 7.")
            .ToArray());

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The Hacker callbacks profile fits memory");
        var probe = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
        context.Helpers.Add("QAFriend", probe);
        await probe.EnterWorldAsync(); // stock 05 / 0c 01; verify target availability without casting from the probe
        await probe.WaitForViewNamedAsync("WinterFrost");

        // Stock proxy_024, lines 13876–13877 and 12961–12962. The casts are self-centered;
        // the recorded selection and unused ground/slot bytes can be replayed intact.
        const string combatAura = "80 b9 e5 01 0d 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 20 00 00 00 78 f2 12 00 08 6b 00 00 00 00 00";
        probe.SendRpc(combatAura);
        await probe.WaitForRpcAsync("80 bd 05 e5 01 00 00 00 00 00 01");
        await probe.WaitForRpcAsync("80 bc 15 00 ?? ?? 00 b8 02 00 00 9f 00 ?? ?? ?? ?? 00 00 00 a0 40");
        probe.SendRpc("80 bb e5 01"); // decoded cancel-ability producer, body is a single little-endian ability ID
        await probe.WaitForRpcAsync("80 bd 05 e5 01 00 00 00 00 00 00");

        probe.SendRpc(StockDeflectVirus);
        await probe.WaitForRpcAsync("80 bd 05 7a 01 00 00 00 00 00 01");
        byte[] activated = (await probe.WaitForRpcAsync("80 bc 09 00 ?? ?? 00 7a 01 00 00 7a 01 20 08 00 28 00 cd cc 4c 3e")).Bytes;
        string record = WireFormat.ToHex(activated.AsSpan(4, 2).ToArray());
        var expired = await probe.WaitForRpcAsync($"80 bc 09 00 {record} 00 7a 01 00 00 7a 01 20 08 00 28 01 cd cc 4c 3e");
        probe.SendRpc(StockDismissDeflect);
        await probe.WaitForRpcAsync("80 bd 05 7a 01 00 00 00 00 00 00");
        await File.WriteAllLinesAsync(Path.Combine(context.OutputDirectory, "hacker-callbacks-proof.txt"),
            new[] { combatAura, StockDeflectVirus, StockDismissDeflect, WireFormat.ToHex(activated), WireFormat.ToHex(expired.Bytes) });
        return Array.Empty<byte>();
    }
}
