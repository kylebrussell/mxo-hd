using System.Text.Json;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class HarmfulCodeMode : NativeQaMode
{
    private const int Level = 19;
    private const ushort HarmfulCode = 53;
    // Native PR406 PacketLog fc98739c17a5:3155.
    private const string StockViralUse = "80b93500040002000000000000000000000000000000000000000000585cc70001000000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, HarmfulCode, 95).Order().ToArray();

    public override string Name => "ability-parity-harmful-code";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495,innerStrC=200,innerStrM=200,alignment=CASE WHEN charId=6114 THEN 2 ELSE 1 END WHERE charId IN (110,6114)",
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "UPDATE characters SET x=25200,z=-1498 WHERE charId=6114"
        },
        DevCommands: true, loadAbilityAncestors: true,
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "10",
            ["MXO_FREECAST_VIRAL_QA"] = "1", ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766",
            ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0"
        },
        readyMessages: new[]
        {
            "Level 19, 49/49 memory. Use Hacker Style outside interlock. Try 53 with no selection and check for one Target is not valid line, no IS, cast bar or reuse.",
            "Select QAEnemy at 4 m and retry 53 immediately. Expect 14 damage, 4s cast, 8s own reuse and 4s shared reuse."
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "53", ["NativeCloseableRows"] = 0 });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var enemy = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(enemy.Handle, enemy);
        await enemy.EnterWorldAsync();
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        Check.Equal(AbilityMemory.ForLevel(Level), Loadout.Sum(AbilityMemory.Cost), "The early Hacker profile fits 49 memory");
        await actor.EnterWorldAsync();
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        var target = await actor.WaitForViewNamedAsync(enemy.Handle);
        await enemy.WaitForViewNamedAsync(actor.Handle);
        await StarterFixedDamageMode.SelectStyle(actor, self, CombatStyles.HackerStyle, 172);
        await StockAbilityProfile.ObserveAsync(actor, 0, packet => StockAbilityProfile.Bonus(packet, 95, 1031, 5, 0, false),
            "Transmit Virus grants +5 linked viral damage at the fixture rank");
        var invalid = await StarterHackerViralDamageMode.VerifyInvalidTargets(actor, HarmfulCode, StockViralUse, target);
        int cursor = actor.World.RpcSnapshot().Count;
        byte[] request = await StarterFixedDamageMode.Land(actor, enemy, target, HarmfulCode, 14, 4, 8, StockViralUse);
        byte[] shared = AbilityCooldowns.BuildTimer(682, AbilityCooldowns.GroupTimer, 4, true);
        var group = await StockAbilityProfile.ObserveAsync(actor, cursor, packet => packet.Bytes.AsSpan().SequenceEqual(shared),
            "Harmful Code starts the four-second shared timer");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "harmful-code.json"),
            JsonSerializer.Serialize(new { Request = Convert.ToHexString(request), InvalidTargets = invalid, Base = 12, Landed = 14, OwnReuse = 8, GroupReuse = group.Hex }));
        return Array.Empty<byte>();
    }
}
