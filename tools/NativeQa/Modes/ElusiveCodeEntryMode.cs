using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

/// <summary>Elusive Code 940 passive: +50 Combat Evasion into the live Evade entry gate and +25 Ballistic Damage
/// Resistance, both present only while the passive is loaded.</summary>
public sealed class ElusiveCodeEntryMode : NativeQaMode
{
    private const ushort SelfDefense = 17, Insight = 113, Evade = 217, ElusiveCode = 940;
    private const string StockEvade = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private const string StockSelfDefense = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";

    public override string Name => "ability-parity-elusive-code";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, SelfDefense, Evade, ElusiveCode },
        PvpOpponentsFixture.X, PvpOpponentsFixture.Z, Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE char_abilities SET level=50 WHERE char_id=110 AND ability_id={0x80000000u | ((uint)ElusiveCode << 10)}",
            "DELETE FROM inventory WHERE charId IN (110,6114) AND slot=107",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                "(6114,400,2147485696,50,1),(6114,401,2147501056,50,1),(6114,402,2147599360,50,1)"
        }, DevCommands: true, loadAbilityAncestors: true, hotbarAbilities: new[] { SelfDefense, Evade },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "940", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            "Elusive Code 940 starts seeded-loaded at level 50 (its stored ReqCharLevel 99 blocks ordinary loading at the retail cap). In Status, capture its source records: +50 Combat Evasion (520) and +25 Ballistic Damage Resistance (181).",
            "Keep Speed on CSR. QAEnemy starts in Power with Insight 113 loaded (force 110). Use page 2 slot 1 Self Defense if needed. Clear selection and press slot 2 Evade Combat at rest. Capture its shield.",
            """Request {"actor":"QAEnemy","action":"melee","target":"CSR_Tester"}. Expect refusal and the log line 'QAEnemy -> CSR_Tester force110 evade167 allowedFalse' (940's +50 direct Combat Evasion, scaled by Execute Program's +5%).""",
            "While the shield is still active, use ?qaunload 940 on CSR. Capture removal of both 940 records. Repeat the same attack: the logged evade drops to 115, still refused at force 110.",
            "Use ?qaload 940 on CSR. 940 stores ReqCharLevel 99, so expect the modal 'Your character level is too low to load that ability.' and no 940 record. Do not attack again: each refused entry costs the 255-point shield about 97, and a third ends it.",
            "Press slot 2 to end Evade Combat. Capture shield removal and the 30 s reuse. The same attack now admits without the entry rule, although 940 stays loaded."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var actor = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(actor.Handle, actor);
        await PvpOpponentsFixture.EnterAsync(actor);
        actor.SendRpc("42 01 00 00 00 00 00");
        await StockAbilityProfile.ObserveAsync(actor, 0, frame =>
            ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == 1017 && record.TargetId == 1012,
            "Helper selected Power");
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        if (context.StockProbe is not { } defender) return Array.Empty<byte>();

        var server = context.ActualServer!;
        var defenderAbilities = AbilityMemoryUiFixture.Decode(defender.CharacterData[6])
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(defenderAbilities.TryGetValue(ElusiveCode, out var owned) && owned.Level == 50, "Elusive Code is owned at level 50");
        await PvpOpponentsFixture.EnterAsync(defender);
        var target = await actor.WaitForViewNamedAsync(defender.Handle);
        var enemy = await defender.WaitForViewNamedAsync(actor.Handle);
        string targetHandle = Hex(target), enemyHandle = Hex(enemy);
        defender.SendRpc(StarterHackerControlTargetMode.WithHandle(StockSelfDefense, 0));
        int on = defender.World.RpcSnapshot().Count;
        defender.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(defender, on, frame => Shield(frame, false), "Live Evade shield");
        int loaded = await ScoreAsync(110);
        Check.That(loaded > 110, "Loaded Elusive Code keeps the evade score above force 110");

        int unload = await StockAbilityProfile.ChangeLoadoutAsync(defender, defenderAbilities, new[] { ElusiveCode }, load: false);
        await StockAbilityProfile.ObserveAsync(defender, unload,
            frame => StockAbilityProfile.Bonus(frame, ElusiveCode, 520, 50, 0, true), "Elusive Code Combat Evasion removed");
        await StockAbilityProfile.ObserveAsync(defender, unload,
            frame => StockAbilityProfile.Bonus(frame, ElusiveCode, 181, 25, 0, true), "Elusive Code Ballistic Damage Resistance removed");
        int unloaded = await ScoreAsync(110);
        // floor((100 + 520) * (100 + 1049) / 100) + 10: the ancestor Execute Program 45 supplies 1049 +5, so 940's
        // direct +50 is worth 52 points (headless 167 -> 115).
        Check.Equal(115, unloaded, "Without 940 the shielded evade is Execute Program's 115");
        Check.Equal(167, loaded, "Elusive Code's +50 direct evade scales to 167 under the +5% bonus");

        // GOB 940 stores CharStatsPrerequisite/ReqCharLevel 99: the ordinary loadout refuses to reload it at the
        // retail cap, so the seeded row is the only loaded state. The refusal must not restore either record.
        int reload = defender.World.RpcSnapshot().Count;
        var row = defenderAbilities[ElusiveCode];
        defender.SendRpc($"80 ae 00 00 00 00 00 00 0a 00 01 00 00 {StockAbilityProfile.LittleEndianHex(row.Slot)} " +
            $"{WireFormat.ToHex(BitConverter.GetBytes((uint)ElusiveCode))} {StockAbilityProfile.LittleEndianHex(row.Level)}");
        await defender.WaitForTextAsync("Your character level is too low to load that ability.");
        Check.Equal(0, defender.World.RpcSnapshot().Skip(reload).Count(frame =>
            StockAbilityProfile.Bonus(frame, ElusiveCode, 520, 50, 0, false) ||
            StockAbilityProfile.Bonus(frame, ElusiveCode, 181, 25, 0, false)), "A refused reload restores no 940 record");
        // Each refused entry costs the shield about 97 of its 255 points; a third probe would end it before toggle-off.

        int off = defender.World.RpcSnapshot().Count;
        defender.SendRpc(StockEvade);
        await StockAbilityProfile.ObserveAsync(defender, off, frame => Shield(frame, true), "Shield is removed on toggle-off");
        await EvidenceRetestWire.TimerAsync(defender, off, Evade, AbilityCooldowns.OwnTimer, 30);
        await EnterAsync(null, null, true);
        Check.Equal(0, actor.World.Problems.Count + defender.World.Problems.Count, "Elusive Code entry replay framing");
        Console.WriteLine($"ELUSIVE CODE PASS 940 evade{loaded}->{unloaded} ballistic+25 reload refused ReqCharLevel99");
        return Array.Empty<byte>();

        async Task<int> ScoreAsync(int force)
        {
            int start = server.DebugLog().Length;
            string prefix = $"QAEnemy -> CSR_Tester force{force} evade";
            actor.SkipReceived();
            defender.SkipReceived();
            actor.SendRpc($"40 {targetHandle} 01 6b");
            string log = "";
            for (int i = 0; i < 100 && !log.Contains(prefix, StringComparison.Ordinal); i++)
            {
                await Task.Delay(100);
                log = server.DebugLog()[start..];
            }
            int at = log.IndexOf(prefix, StringComparison.Ordinal);
            Check.That(at >= 0, "The live shield and the loaded passive drive entry");
            string rest = log[(at + prefix.Length)..];
            int evade = int.Parse(new string(rest.TakeWhile(char.IsDigit).ToArray()));
            bool allowed = rest[rest.TakeWhile(char.IsDigit).Count()..].StartsWith(" allowedTrue", StringComparison.Ordinal);
            Check.Equal(force >= evade, allowed, "Entry follows force against the current evade score");
            await EnterAsync(force, evade, allowed, start);
            return evade;
        }

        async Task EnterAsync(int? force, int? evade, bool allowed, int? sent = null)
        {
            int start = sent ?? server.DebugLog().Length;
            if (sent == null)
            {
                actor.SkipReceived();
                defender.SkipReceived();
                actor.SendRpc($"40 {targetHandle} 01 6b");
            }
            if (!allowed)
                await actor.WaitForRpcAsync($"4d 0e 00 01 00 {targetHandle}");
            else
            {
                await actor.WaitForRpcAsync($"46 {targetHandle} 01");
                await actor.WaitForRpcAsync("45");
            }
            string log = server.DebugLog()[start..];
            if (force.HasValue)
                Check.That(log.Contains($"QAEnemy -> CSR_Tester force{force} evade{evade} allowed{allowed}", StringComparison.Ordinal),
                    "The live shield and the loaded passive drive entry");
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
