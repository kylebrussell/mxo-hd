using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterInitiativeBatchMode : NativeQaMode
{
    private const ushort Gunman = 128, Soldier = 154, Desperation = 1149;
    private const ushort InitiativeBonus = 1046, Initiative = 1044;
    private static readonly ushort[] Loadout = Profile(2, 17, Gunman, Soldier, Desperation).Order().ToArray();
    // Full stock Desperation with no target, a91de5605bf5:738–739.
    private const string StockCast = "80b97d040000000000000000000000000000000000000000000000e05bc50001000000086b0000000000";

    public override string Name => "ability-parity-starter-initiative-and-desperation";
    public override NativeQaModePlan Plan { get; } = new(Loadout, CombatFixtureLoadout.CasterX, CombatFixtureLoadout.CasterZ,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level=50,exp={PlayerProgression.ExperienceForLevel(50)},y={CombatFixtureLoadout.GroundY},healthC=100,healthM=500,innerStrC=500,innerStrM=500 WHERE charId=110"
        }, DevCommands: true, loadAbilityAncestors: true, testMobFixture: CombatFixtureLoadout.MeleeCsv,
        hotbarAbilities: new ushort[] { Desperation }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "128,154,1149", ["NativeCloseableRows"] = 0 }, readyMessages: new[]
        {
            "Inspect the separate Initiative Bonus adds from Gunman (+1) and Soldier (+2). Use ?qaunload 128, then ?qaload 128. Repeat for 154. Confirm the respective grant 1070 or 1061 disappears and returns in Actions.",
            "With both passives loaded and no target, set ?health 100. Press page 2 slot 1 Desperation. Expect +84 Initiative for 20s and 90s reuse (+5% total includes the required 137 prerequisite). During the effect use ?qaunload 154. The existing +84 remains until natural expiry.",
            "Run ?combatdepth initiative while the effect is on and after it expires to capture the current interlock bonus as 84 then 0. This is the current interlock application policy; capture the native bonus records and expiry."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(50), "The two starting disciplines share legal memory");
        var rows = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        foreach (var (ability, grant, value) in new (ushort, ushort, int)[] { (Gunman, 1070, 1), (Soldier, 1061, 2) })
        {
            var passives = new[] { (ability, grant, new[] { (InitiativeBonus, value) }) };
            var proof = new List<object>();
            await CheckPassivesAsync(actor, proof, passives, 0, remove: false);
            int off = await ChangeLoadoutAsync(actor, rows, new[] { ability }, load: false);
            await CheckPassivesAsync(actor, proof, passives, off, remove: true);
            int on = await ChangeLoadoutAsync(actor, rows, new[] { ability }, load: true);
            await CheckPassivesAsync(actor, proof, passives, on, remove: false);
        }

        int cursor = actor.World.RpcSnapshot().Count;
        actor.SendRpc(StockCast);
        await ObserveAsync(actor, cursor, packet => Bonus(packet, Desperation, Initiative, 84, 20, remove: false),
            "The two sampled sources plus the +2 prerequisite scale the snapshot from 80 to 84");
        await EvidenceRetestWire.TimerAsync(actor, cursor, Desperation, AbilityCooldowns.OwnTimer, 90);
        await ChangeLoadoutAsync(actor, rows, new[] { Soldier }, load: false);
        actor.SendChat("?combatdepth initiative");
        await actor.WaitForTextAsync("bonus=84");
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor)
            .Any(packet => Bonus(packet, Desperation, Initiative, 84, 20, remove: true)),
            "Natural expiry removes the original snapshot", TimeSpan.FromSeconds(24));
        actor.SendChat("?combatdepth initiative");
        await actor.WaitForTextAsync("bonus=0");
        Check.Equal(0, actor.World.Problems.Count, "Initiative passive, grant and scripted buff framing");
        return Array.Empty<byte>();
    }
}
