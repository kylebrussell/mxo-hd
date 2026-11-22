using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterHackerAuraRadiusMode : NativeQaMode
{
    private const int X = PvpOpponentsFixture.X, Z = PvpOpponentsFixture.Z;
    private const ushort Miasma = 61, StaticField = 89, TrojanHorse = 945;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Miasma, StaticField, TrojanHorse)
        .Order().ToArray();
    // Stock pr564 PacketLog 2248/3568 and the PR545 Trojan Horse request.
    private static readonly (ushort Parent, ushort Child, int Radius, string Request)[] Samples =
    {
        (Miasma, 701, 6, "80b93d00000000000000000000000000000000000000000000000000585cbe0001000000086b0000000000"),
        (StaticField, 694, 6, "80b95900000000000000000000000000000000000000000000000000585cbe0001000000086b0000000000"),
        (TrojanHorse, 946, 3, "80b9b103000000000000000000000000000000000000000000000000585cbc0001000000086b0000000000")
    };
    private static readonly (string Name, uint Id, int Dx, int Dz, int Organisation)[] Stands =
    {
        ("QAEnemy", 6114, 300, 0, 2), ("QAOther", 6113, 600, 0, 2),
        ("QAFar", 6112, 0, 601, 2), ("QAFriend", 6111, -250, 0, 1)
    };

    public override string Name => "ability-parity-starter-hacker-aura-radius";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[] { PvpOpponentsFixture.NativeSql, "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)" }
        .Concat(Stands.Select(stand =>
            $"UPDATE characters SET x={X + stand.Dx},z={Z + stand.Dz},alignment={stand.Organisation},innerStrC=500,innerStrM=500 WHERE charId={stand.Id}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { Miasma, StaticField, TrojanHorse },
        environmentOverrides: new()
        {
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "61,89,694,701,945,946", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Stay at spawn with empty selection. Hostile QAEnemy is exactly 3 m away, QAOther exactly 6 m, QAFar 6.01 m, and friendly QAFriend 2.5 m away.",
            "On page 2 toggle Miasma (slot 1). Check the 701 −14/−14 defense records on QAEnemy and QAOther only. Let it run at least three seconds and see a pulse before toggling it off. Wait at least three seconds before slot 2.",
            "Toggle Static Field (slot 2). Its child 694 hits for 1 on QAEnemy and QAOther only. Let it run at least three seconds and see a pulse before toggling it off, then wait at least three seconds.",
            "Toggle Trojan Horse (slot 3) and wait for one 946 pulse. QAEnemy alone loses 20 IS. Toggle it off. Keep the AREA-QA inclusion/exclusion lines and first outgoing ability requests."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }

        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var witness in context.Helpers.Values)
            await caster.WaitForViewNamedAsync(witness.Handle);
        foreach (var sample in Samples)
        {
            Check.Equal((double)sample.Radius, AbilityCatalog.Find(sample.Child).Number("Ability/RadiusOfEffect"),
                "The fixture uses the shipped child radius");
            int logStart = server.DebugLog().Length;
            var witnesses = context.Helpers.Values.Select(helper =>
                (Helper: helper, Rpc: helper.World.RpcSnapshot().Count, View: helper.World.ViewSnapshot().Count)).ToArray();
            await ToggleAsync(sample.Parent, sample.Request, true);
            var expected = sample.Radius == 3 ? new[] { "QAEnemy" } : new[] { "QAEnemy", "QAOther" };
            foreach (var entry in witnesses.Where(entry => expected.Contains(entry.Helper.Handle)))
                await entry.Helper.WaitUntilAsync(() => SawPulse(entry.Helper, entry.Rpc, entry.View, sample.Child),
                    $"{sample.Child} reaches {entry.Helper.Handle}", HeadlessPlayer.DefaultTimeout);
            if (sample.Parent != TrojanHorse)
                await Task.Delay(TimeSpan.FromSeconds(2.3)); // Allow the stock active-toggle guard to clear.
            await ToggleAsync(sample.Parent, sample.Request, false);
            await Task.Delay(TimeSpan.FromSeconds(2.3));
            foreach (var entry in witnesses.Where(entry => !expected.Contains(entry.Helper.Handle)))
                Check.That(!SawPulse(entry.Helper, entry.Rpc, entry.View, sample.Child),
                    $"{sample.Child} excludes {entry.Helper.Handle}");
            Check.That(server.DebugLog()[logStart..].Contains(
                $"[AURA] {sample.Parent} pulse {sample.Child} reaches {expected.Length} targets", StringComparison.Ordinal),
                "The pulse counted exactly the observed hostile recipients");
        }
        Check.Equal(0, caster.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Aura radius fixture packet framing");
        return Array.Empty<byte>();

        async Task ToggleAsync(ushort parent, string request, bool on)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SendRpc(request);
            await StockAbilityProfile.ObserveAsync(caster, cursor,
                packet => packet.Bytes.AsSpan().SequenceEqual(AbilityBuffs.BuildExclusive(parent, on)), $"Aura {parent} on={on}");
        }

        bool SawPulse(HeadlessPlayer helper, int rpcStart, int viewStart, ushort child) => child switch
        {
            701 => new ushort[] { 1009, 1010 }.All(stat => helper.World.RpcSnapshot().Skip(rpcStart)
                .Any(packet => StockAbilityProfile.Bonus(packet, child, stat, -14, 5, false))),
            694 => helper.World.RpcSnapshot().Skip(rpcStart).Any(packet => packet.Bytes.AsSpan().SequenceEqual(
                AbilityMessages.Damage(false, child, caster.Handle, helper.Handle, 1, 0))),
            946 => helper.World.ViewSnapshot().Skip(viewStart).Any(view =>
                BuffDrainLifetimeFixture.TryInnerStrength(view, out var value) && value == 480),
            _ => false
        };
    }
}
