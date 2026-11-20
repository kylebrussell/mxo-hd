using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterInnerStrengthCycleMode : NativeQaMode
{
    private const ushort ExecuteProgram = 45, Sprint = 184, Evade = 217;
    private const ushort BaseMaximum = 200, LoadedMaximum = 220, InitialStrength = 150;
    // Stock activation bodies captured in the Sprint run 11701b9dd84e and native PR524.
    private const string SprintUse = "80b9b800000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private const string EvadeUse = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, ExecuteProgram, Sprint, Evade)
        .Order().ToArray();

    public override string Name => "ability-parity-starter-inner-strength-cycle";
    public override NativeQaModePlan Plan { get; } = new(Loadout, 24800, -1498,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET y=495,level=50,exp={PlayerProgression.ExperienceForLevel(50)}," +
                $"innerStrC={InitialStrength},innerStrM={BaseMaximum} WHERE charId=110",
            "UPDATE char_abilities SET level=50 WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Sprint, Evade },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "45,184,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Stand still out of combat and watch Inner Strength. Execute Program (45) is loaded at rank 50. It raises the cap to 220 and some regen ticks to +9 instead of +8. Wait for 220 before the comparison.",
            "Use ?qaunload 45. The client should show 200/200 immediately. Press page 2 slot 2 (Evade Combat) twice to turn the shield on and off. Capture the 40 IS debit, the 30 s reuse and the +8 regen to 200.",
            "At full IS, use ?qaload 45. Capture restored cap and regen to 220. Press page 2 slot 1 (Hyper-Sprint), wait for its first 25 IS drain, then switch it off. Capture the initial 25 IS debit, drain beside +8/+9 regeneration and five-second reuse. Enter submits each chat command."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var server = context.ActualServer!;
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "Legal loaded memory");
        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[6])
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var self = actor.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(LoadedMaximum, BitConverter.ToUInt16(self.Attributes["InnerStrengthMax"]), "Execute Program publishes the increased login cap");
        await WaitForStrengthAsync(0, LoadedMaximum, "Regeneration reaches the increased cap");
        var loginStrengths = Strengths(0);
        var loginTicks = loginStrengths.Prepend(InitialStrength).Zip(loginStrengths, (before, after) => after - before);
        Check.That(loginTicks.Contains(9), "Execute Program's percentage regeneration increases a tick above eight");

        int unloadViews = actor.World.ViewSnapshot().Count;
        int unloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { ExecuteProgram }, load: false);
        await StockAbilityProfile.ObserveAsync(actor, unloaded,
            frame => StockAbilityProfile.Bonus(frame, ExecuteProgram, 1056, 10, 0, true), "Unloaded max IS modifier");
        await ExpectOwnAsync(unloadViews, "02 00 02 80 80 80 14 c8 00 c8 00", "Unloading publishes the base cap and clamps current IS");

        int evadeViews = actor.World.ViewSnapshot().Count;
        int evadeRpcs = actor.World.RpcSnapshot().Count;
        actor.SendRpc(EvadeUse);
        await StockAbilityProfile.ObserveAsync(actor, evadeRpcs, frame => Shield(frame, false), "Evade shield starts");
        await WaitForStrengthAsync(evadeViews, 160, "Evade Combat spends forty IS");
        int shieldOff = actor.World.RpcSnapshot().Count;
        actor.SendRpc(EvadeUse);
        await StockAbilityProfile.ObserveAsync(actor, shieldOff, frame => Shield(frame, true), "Evade shield stops");
        await EvidenceRetestWire.TimerAsync(actor, shieldOff, Evade, AbilityCooldowns.OwnTimer, 30);
        await WaitForStrengthAsync(evadeViews, BaseMaximum, "Unloaded regeneration returns to the 200 cap");
        ushort[] normalRegen = Strengths(evadeViews).Where(value => value >= 160).ToArray();
        Check.That(new ushort[] { 160, 168, 176, 184, 192, 200 }.All(normalRegen.Contains),
            "Unloaded regeneration ticks regain eight IS");

        int reloadViews = actor.World.ViewSnapshot().Count;
        int reloaded = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { ExecuteProgram }, load: true);
        await StockAbilityProfile.ObserveAsync(actor, reloaded,
            frame => StockAbilityProfile.Bonus(frame, ExecuteProgram, 1056, 10, 0, false), "Reloaded max IS modifier");
        await ExpectOwnAsync(reloadViews, "02 00 02 80 80 80 04 dc 00", "Reloading publishes the increased cap again");
        await WaitForStrengthAsync(reloadViews, LoadedMaximum, "Reloaded regeneration reaches 220");

        actor.SkipReceived();
        int sprintViews = actor.World.ViewSnapshot().Count;
        int logStart = server.DebugLog().Length;
        actor.SendRpc(SprintUse);
        await actor.WaitForRpcAsync("80 bd 05 b8 00 00 00 00 00 00 01");
        await ExpectOwnAsync(sprintViews, "80 90 c3 00 b0 19 31 46 12 ?? 02 00 04", "Hyper-Sprint spends twenty-five IS from 220");
        await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
            "[BUFF-DRAIN] char110 ability184", StringComparison.Ordinal) &&
            server.DebugLog()[logStart..].Contains(" amount25 ", StringComparison.Ordinal),
            "Hyper-Sprint first debit stays twenty-five with Execute Program loaded", TimeSpan.FromSeconds(8));
        int sprintOff = actor.World.RpcSnapshot().Count;
        actor.SendRpc(SprintUse);
        await actor.WaitForRpcAsync("80 bd 05 b8 00 00 00 00 00 00 00");
        await EvidenceRetestWire.TimerAsync(actor, sprintOff, Sprint, AbilityCooldowns.OwnTimer, 5);
        Check.Equal(0, actor.World.Problems.Count, "Inner Strength cycle framing");
        return Array.Empty<byte>();

        List<ushort> Strengths(int start) => AwakenedPowerShotMode.InnerStrengthUpdates(actor.World.ViewSnapshot().Skip(start));

        Task WaitForStrengthAsync(int start, ushort value, string description) =>
            actor.WaitUntilAsync(() => Strengths(start).Contains(value), description, TimeSpan.FromSeconds(24));

        Task ExpectOwnAsync(int start, string hex, string description)
        {
            var pattern = BytePattern.Parse(hex);
            return actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(start).Any(pattern.Occurs),
                description, HeadlessPlayer.DefaultTimeout);
        }
    }

    private static bool Shield(RpcRecord frame, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Evade &&
        record.TargetId == EvasionShield.StateAbility && record.AddRemoveType == (remove ? 1 : 0);
}
