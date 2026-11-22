using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterGuardianSightMode;
using static NativeQa.Modes.StarterHackerPassiveFixture;

namespace NativeQa.Modes;

public sealed class StarterGuardianSupportLoadoutMode : NativeQaMode
{
    private const ushort Firewall = 68;
    private const float Reuse = 13;
    private static readonly NativeQaModePlan BasePlan = new StarterGuardianSightMode().Plan;
    private static readonly ushort[] LoadOrder = { 46, 68, 173 };
    private static readonly ushort[] UnloadOrder = { 173, 68, 46 };

    public override string Name => "ability-parity-starter-guardian-support-loadout";

    public override NativeQaModePlan Plan { get; } = new(BasePlan.Abilities.ToArray(), BasePlan.X, BasePlan.Z,
        BasePlan.BeforeLoadout.ToArray(), BasePlan.AfterLoadout.ToArray(), BasePlan.AfterHelpers.ToArray(),
        DevCommands: true, hotbarAbilities: BasePlan.HotbarAbilities.ToArray(),
        environmentOverrides: new(BasePlan.EnvironmentOverrides),
        extraQaMetadata: new() { ["ScopedStockRows"] = "46,68,173", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {BasePlan.Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({CasterX},{FloorY},{StandZ}) faces QAFriend ({ClearX},{FloorY},{StandZ}) 2 m away on verified clear outdoor NAV, away from the ice. Hacker Style is page 2 slot 1. Use /target QAFriend and leave the friendly recipient selected.",
            "Enter ?qaunload 173, ?qaunload 68, then ?qaunload 46. Try page 2 slots 2–4. Expect a clear or gated shortcut, or a single 80 ba reason 7 if a request is sent. No cast bar, [ABILITY-COST], reuse or recipient [MODIFIER].",
            "Enter ?qaload 46, ?qaload 68 and ?qaload 173. If stock cleared a shortcut, use the restored Actions entry. Use all slots on the same QAFriend. Each cast is 2 s with 13 s reuse. [ABILITY-COST] 35/40/40 IS. [MODIFIER] char=6111 source=46 stat=112 +40/600 s, source=68 stats 157 +40 and 180/181/1033 +10/300 s, source=173 stats 522/524/1032 +24/300 s. No maximum [VITALS] or PvP round.",
            "Optional timing check before the loaded casts: start Firewall, then submit ?qaunload 68 while its 2 s bar is open. Keep QAFriend selected on the clear stand. Expect [ABILITY] 68 fizzles: the starter support ability was unloaded before landing, 40 IS, 13 s reuse and no new modifier. Reload 68 before the positive casts. Final IS is 545 with this fizzle, 585 without it. The disposable replay verifies the timing check."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { CasterX, ClearX })
            Check.That(NavMesh.Locate(1, x, FloorY, StandZ, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - FloorY) <= 100, $"Verified outdoor NAV stand ({x},{FloorY},{StandZ})");
        Check.That(NavMesh.HasSightPath(1, CasterX, FloorY, StandZ, ClearX, FloorY, StandZ), "The selected friendly stand has mapped sight");

        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await PvpOpponentsFixture.EnterAsync(friend);
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var archive = VerifyProfile(caster, BasePlan.Abilities.ToArray());
        await StartAsync(caster, friend, server, pvp: true);
        var view = await caster.WaitForViewNamedAsync(friend.Handle);
        int friendStart = friend.World.RpcSnapshot().Count;
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, UnloadOrder, load: false);
        foreach (var sample in Samples)
            await StarterHackerControlTargetMode.RefusedAsync(caster, sample.Id,
                EvidenceRetestWire.CastRequest(sample.Id, view), AbilityRefusals.NotLoaded);
        foreach (var sample in Samples)
            Check.That(!SawAnyBonus(friendStart, sample.Id, sample.Duration, sample.Bonuses), $"Unloaded {sample.Id} cannot change QAFriend");
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, LoadOrder, load: true);

        var firewall = Samples.Single(sample => sample.Id == Firewall);
        int logStart = server.DebugLog().Length;
        friendStart = friend.World.RpcSnapshot().Count;
        int castStart = await BeginAsync(Firewall);
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Firewall }, load: false);
        await caster.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
            "[ABILITY] 68 fizzles: the starter support ability was unloaded before landing"),
            "The committed unload prevents a pending friendly Firewall", HeadlessPlayer.DefaultTimeout);
        await EvidenceRetestWire.TimerAsync(caster, castStart, Firewall, AbilityCooldowns.OwnTimer, Reuse);
        await AssertIsAsync(660);
        friend.SendRpc("81 48");
        await StockAbilityProfile.ObserveAsync(friend, friendStart, frame => frame.Opcode == 0x149, "The recipient stream advances beyond the fizzle");
        Check.That(!SawAnyBonus(friendStart, Firewall, firewall.Duration, firewall.Bonuses), "The fizzled Firewall applies no bonus");
        await StockAbilityProfile.ChangeLoadoutAsync(caster, archive, new[] { Firewall }, load: true);
        byte[] firewallEnd = AbilityCooldowns.BuildTimer(Firewall, AbilityCooldowns.OwnTimer, Reuse, start: false);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(castStart).Any(frame => frame.Bytes.AsSpan().SequenceEqual(firewallEnd)),
            "Firewall's ordinary 13 s timer clears", TimeSpan.FromSeconds(15));

        int expectedIs = 660;
        foreach (var sample in Samples)
        {
            friendStart = friend.World.RpcSnapshot().Count;
            castStart = await BeginAsync(sample.Id);
            await EvidenceRetestWire.TimerAsync(caster, castStart, sample.Id, AbilityCooldowns.OwnTimer, Reuse);
            await AssertIsAsync(expectedIs -= sample.Cost);
            foreach (var (stat, value) in sample.Bonuses)
            {
                await StockAbilityProfile.ObserveAsync(friend, friendStart, frame =>
                    StockAbilityProfile.Bonus(frame, sample.Id, stat, value, sample.Duration, false),
                    $"The reloaded {sample.Id}/{stat} reaches QAFriend");
                Check.That(!caster.World.RpcSnapshot().Skip(castStart).Any(frame =>
                    StockAbilityProfile.Bonus(frame, sample.Id, stat, value, sample.Duration, false)),
                    $"Direct support {sample.Id}/{stat} applies to the selected friend");
            }
        }
        Check.Equal(0, caster.World.Problems.Count + friend.World.Problems.Count, "Guardian support loadout wire framing");
        Console.WriteLine("GUARDIAN SUPPORT LOADOUT PASS 46/68/173");
        return Array.Empty<byte>();

        async Task<int> BeginAsync(ushort ability)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            caster.SkipReceived();
            EvidenceRetestWire.Cast(caster, ability, view);
            await EvidenceRetestWire.CastBarAsync(caster, ability, 2);
            return cursor;
        }

        Task AssertIsAsync(int expected) => caster.WaitUntilAsync(() =>
            caster.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)expected),
            $"Expected Inner Strength {expected}", HeadlessPlayer.DefaultTimeout);

        bool SawAnyBonus(int cursor, ushort ability, float duration, (ushort Stat, int Value)[] bonuses) =>
            friend.World.RpcSnapshot().Skip(cursor).Any(frame => bonuses.Any(bonus =>
                StockAbilityProfile.Bonus(frame, ability, bonus.Stat, bonus.Value, duration, false)));
    }
}
