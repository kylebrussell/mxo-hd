using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class StarterEvadeEntryMode : NativeQaMode
{
    private const ushort SelfDefense = 17, Insight = 113, Evade = 217;
    private const string StockEvade = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string StockSelfDefense = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";

    public override string Name => "ability-parity-starter-evade-entry";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, SelfDefense, Evade },
        PvpOpponentsFixture.X, PvpOpponentsFixture.Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            "DELETE FROM inventory WHERE charId IN (110,6114) AND slot=107",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                "(6114,400,2147485696,50,1),(6114,401,2147501056,50,1),(6114,402,2147599360,50,1)"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { SelfDefense, Evade },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "17,113,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Keep Speed on CSR. QAEnemy starts in Power. Use page 2 slot 1 Self Defense if needed. Clear selection and press slot 2 Evade Combat at rest. Capture its shield on CSR and the helper view.",
            """Request {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. With Insight unloaded, entry refuses at force 100 versus evade 110. Use helper chat ?qaload 113 and repeat the same attempt. Force 110 admits at equality. Withdraw normally and send helper stop as well. Enter submits chat without Escape.""",
            "Once both players are out, unload 113 via helper chat and press CSR slot 2 to end Evade Combat. Capture shield removal and 30 s reuse. The same helper entry now admits without Insight. These scores describe the current server entry rule."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var actor = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(actor.Handle, actor);
        var owned = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        await PvpOpponentsFixture.EnterAsync(actor);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Insight }, load: false);
        actor.SendRpc("42 01 00 00 00 00 00");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == 1017 && record.TargetId == 1012,
            "Helper selected Power");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        if (context.StockProbe is not { } defender) return Array.Empty<byte>();

        var server = context.ActualServer!;
        await PvpOpponentsFixture.EnterAsync(defender);
        var target = await actor.WaitForViewNamedAsync(defender.Handle);
        var enemy = await defender.WaitForViewNamedAsync(actor.Handle);
        string targetHandle = Hex(target), enemyHandle = Hex(enemy);
        defender.SendRpc(StarterHackerControlTargetMode.WithHandle(StockSelfDefense, 0));
        int on = defender.World.RpcSnapshot().Count;
        defender.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(defender, on, frame => Shield(frame, false), "Live Evade shield");
        await EnterAsync(100, false);

        int load = await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Insight }, load: true);
        await StockAbilityProfile.ObserveAsync(actor, load, frame => StockAbilityProfile.Bonus(frame, Insight, 1048, 10, 0, false),
            "Insight adds ten percent Force Combat");
        await EnterAsync(110, true);
        await StockAbilityProfile.ChangeLoadoutAsync(actor, owned, new[] { Insight }, load: false);
        int off = defender.World.RpcSnapshot().Count;
        defender.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(defender, off, frame => Shield(frame, true), "Shield is removed on toggle-off");
        await EvidenceRetestWire.TimerAsync(defender, off, Evade, AbilityCooldowns.OwnTimer, 30);
        await EnterAsync(null, true);
        Check.Equal(0, actor.World.Problems.Count + defender.World.Problems.Count, "Evade Combat entry replay framing");
        return Array.Empty<byte>();

        async Task EnterAsync(int? force, bool allowed)
        {
            int start = server.DebugLog().Length;
            actor.SkipReceived();
            defender.SkipReceived();
            actor.SendRpc($"40 {targetHandle} 01 6b");
            if (!allowed)
                await actor.WaitForRpcAsync($"4d 0e 00 01 00 {targetHandle}");
            else
            {
                await actor.WaitForRpcAsync($"46 {targetHandle} 01");
                await actor.WaitForRpcAsync("45");
            }
            string log = server.DebugLog()[start..];
            if (force.HasValue)
                Check.That(log.Contains($"QAEnemy -> CSR_Tester force{force} evade110 allowed{allowed}", StringComparison.Ordinal),
                    "The current shield and modifier drive entry");
            else
                Check.That(!log.Contains("[COMBAT-INSIGHT OURS]", StringComparison.Ordinal), "Entry after shield removal bypasses that rule");
            if (allowed)
            {
                actor.SendRpc("44 00");
                defender.SendRpc("44 00");
                await actor.WaitForRpcAsync($"46 {targetHandle} 00", TimeSpan.FromSeconds(20));
                await defender.WaitForRpcAsync($"46 {enemyHandle} 00", TimeSpan.FromSeconds(20));
            }
        }
    }

    private static bool Shield(RpcRecord frame, bool remove) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Evade &&
        record.TargetId == EvasionShield.StateAbility && record.AddRemoveType == (remove ? 1 : 0);

    private static string Hex(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
}
