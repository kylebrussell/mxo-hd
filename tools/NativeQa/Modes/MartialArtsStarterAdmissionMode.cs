using System.Text.RegularExpressions;
using HeadlessClient;
using hds;

namespace NativeQa.Modes;

public sealed class MartialArtsStarterAdmissionMode : NativeQaMode
{
    private const ushort Aikido = 101, KungFu = 133, GuardBreaker = 138, IronGuard = 139;
    private const byte Level = 12, Rank = 12;
    private static readonly ushort[] Roots = { 2, 17, Aikido, KungFu, GuardBreaker, IronGuard };
    private static readonly ushort[] Loadout = Roots.Concat(Roots.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys))
        .Distinct().ToArray();

    // Complete stock PR436 requests, plus the same ordinary opening used in PR517.
    internal const string StockOpen = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
    internal const string StockAikido = "80b965000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    internal const string StockKungFu = "80b985000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    internal const string StockGuard = "80b98a000400020000000000000000000000000000000000000000008a000000006b0000086b0000000000";
    internal const string StockIron = "80b98b000400020000000000000000000000000000000000000000008b000000006b0000086b0000000000";

    public override string Name => "ability-parity-ma-starter-admission";

    public override NativeQaModePlan Plan { get; } = new(
        Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y=495," +
                "healthC=2000,healthM=2000,innerStrC=1000,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level={Rank} WHERE char_id=110",
            $"DELETE FROM inventory WHERE charId=110 AND slot={CombatFixtureWeapons.HandSlot}"
        }, DevCommands: true, testMobFixture: CombatFixtureLoadout.MeleeCsv, hotbarAbilities: new[] { Aikido, KungFu, GuardBreaker, IronGuard },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.ResetMode] = "1", ["MXO_CONDITIONAL_THROW_QA"] = "1" },
        readyMessages: new[]
        {
            "Level 12, rank 12, unarmed, with Melee Dummy 3 m west (Aikido 101 + Kung-Fu 133 + Guard Breaker 138 + Iron Guard 139 + MA Expertise 136 = +10% melee damage).",
            "Enter ordinary unarmed Close Combat on Melee Dummy, reselect Aikido (101), and use Iron Guard (page 2 slot 4): expect 25 IS, 10.0 s own reuse, landed physical damage 13 (base 12 + 10%), and caster +10 MeleeDef (1009) & +10 BallisticDef (1010) for 12 s.",
            "Reset with ?combatdepth fixture-reset, strafe to expose the fresh dummy, reselect and open Close Combat again. Enter submits each command. Do not use Escape. Reselect Kung-Fu (slot 2). Try Guard Breaker (slot 3) before Off Balance, then run ?combatdepth offbalance 30 on that same current dummy and retry: expect 20 IS, 10.0 s own reuse, landed physical damage 39 (base 36 + 10%) and target '[EFFECTS] 138 on Melee Dummy: Stat 1009 -10 15 s'."
        },
        extraQaMetadata: new()
        {
            ["ScopedStockRows"] = "101,133,138,139", ["NativeCloseableRows"] = 0,
            ["LoadedMemory"] = Loadout.Sum(AbilityMemory.Cost), ["MemoryCapacity"] = AbilityMemory.ForLevel(Level)
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "Legal level-12 memory");
        var dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(caster, dummy);
        await RefuseAsync(IronGuard, StockIron, Handle(dummy));
        await RefuseAsync(GuardBreaker, StockGuard, Handle(dummy));

        await OpenAsync(StockAikido, Aikido);
        await RefuseAsync(IronGuard, StockIron, Handle(dummy) ^ 0x10000);
        int before = caster.World.RpcSnapshot().Count;
        await LandAsync(IronGuard, StockIron, 13);
        foreach (ushort stat in new ushort[] { 1009, 1010 })
            await StockAbilityProfile.ObserveAsync(caster, before,
                frame => StockAbilityProfile.Bonus(frame, IronGuard, stat, 10, 12, false), "Iron Guard caster-only defence");

        int previous = caster.World.CreationSnapshot().Count;
        caster.SendChat("?combatdepth fixture-reset");
        await caster.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
        await caster.WaitUntilAsync(() => caster.World.CreationSnapshot().Skip(previous).Any(view => view.Name == dummy.Name),
            "A fresh hostile dummy is selectable after reset", HeadlessPlayer.DefaultTimeout);
        var old = dummy;
        dummy = await caster.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        Check.That(Handle(old) != Handle(dummy), "Reset retires the old opponent handle");

        await OpenAsync(StockKungFu, KungFu);
        await RefuseAsync(GuardBreaker, StockGuard, Handle(old));
        var noState = await RefuseAsync(GuardBreaker, StockGuard, Handle(dummy), AbilityRefusals.TargetStateRequired);
        Check.That(AbilityRefusals.TryDecode(noState.Bytes, out var refusal) && refusal.StatesNotMet == 4,
            "The live target must have the shipped Off Balance state before the move is queued");
        caster.SendChat("?combatdepth offbalance 30");
        await caster.WaitForTextAsync("OffBalance fixture applied");
        await LandAsync(GuardBreaker, StockGuard, 39);
        Check.That(server.DebugLog().Contains("[EFFECTS] 138 on Melee Dummy: Stat 1009 -10 15 s", StringComparison.Ordinal),
            "Guard Breaker still debuffs its target's defence");
        return Array.Empty<byte>();

        async Task OpenAsync(string style, ushort id)
        {
            uint handle = Handle(dummy);
            string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            caster.SkipReceived();
            caster.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockOpen, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
            await caster.WaitForRpcAsync($"40 {hex} 00 6b");
            await caster.WaitForRpcAsync("47");
            caster.SendRpc($"81 51 {hex}");
            caster.SendRpc(StarterHackerControlTargetMode.WithHandle(style, handle));
            await caster.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(id)} 00 00");
        }

        Task<RpcRecord> RefuseAsync(ushort id, string request, uint handle, uint reason = AbilityRefusals.InvalidTarget) =>
            StarterHackerControlTargetMode.RefusedAsync(caster, id, StarterHackerControlTargetMode.WithHandle(request, handle), reason);

        async Task LandAsync(ushort id, string request, int amount)
        {
            var result = new Regex($@"\[ABILITY\] {id} lands on view {dummy.ViewId} for (\d+)\b");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (id == GuardBreaker && attempt != 0)
                {
                    caster.SendChat("?combatdepth offbalance 30");
                    await caster.WaitForTextAsync("OffBalance fixture applied");
                }
                int cursor = caster.World.RpcSnapshot().Count;
                int log = server.DebugLog().Length;
                caster.SendRpc(StarterHackerControlTargetMode.WithHandle(request, Handle(dummy)));
                await EvidenceRetestWire.TimerAsync(caster, cursor, id, AbilityCooldowns.OwnTimer, 10);
                string failed = $"{AbilityCatalog.Find(id)!.Name} fails";
                await caster.WaitUntilAsync(() => result.IsMatch(server.DebugLog()[log..]) ||
                    server.DebugLog()[log..].Contains(failed, StringComparison.Ordinal),
                    $"Resolved starter special {id}", HeadlessPlayer.DefaultTimeout);
                var landed = result.Match(server.DebugLog()[log..]);
                if (landed.Success)
                {
                    Check.Equal(amount, int.Parse(landed.Groups[1].Value), $"Landed starter special {id}");
                    return;
                }
                await EvidenceRetestWire.TimerAsync(caster, cursor, id, AbilityCooldowns.OwnTimer, 10, ended: true);
            }
            throw new InvalidOperationException($"The ordinary rounds did not land {id}.");
        }
    }

    private static uint Handle(ViewCreation view) => DuelPackets.Handle(view.ViewId, view.SpawnId);
}
