using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterOperativeHinderingEntryMode : NativeQaMode
{
    private const ushort ExecuteProgram = 45, Insight = 113, Handguns = 129, Hindering = 130, Compel = 150, Evade = 217;
    // PR554b stock Handguns and Hindering Shot (5530/5281). Stock Compel from PR412.
    private const string HandgunsCast = "80b981000400020000000000000000000000000000000000000000000000000090767241086b0000000000";
    private const string HinderingCast = "80b98200040002000000000000000000000000000000000000000000585cba0001000000086b0000000000";
    private const string CompelCast = "80b99600000000000000000000000000000000000000000000000000d557377c20000000086b0000000000";
    // Stock Evade Combat request also used in StarterEvadeEntryMode.
    private const string EvadeCast = "80b9d9000000000000000000000000000000000000000000000000002000000078f21200086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Handguns, Hindering).Order().ToArray();

    public override string Name => "ability-parity-starter-operative-hindering-entry";
    public override NativeQaModePlan Plan { get; } = new(Loadout, PvpOpponentsFixture.X, PvpOpponentsFixture.Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET x=17160,level=20,exp={PlayerProgression.ExperienceForLevel(20)} WHERE charId=6114",
            "UPDATE characters SET x=17280 WHERE charId=6111",
            "DELETE FROM inventory WHERE charId IN (110,6111,6114) AND slot=107",
            $"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,{CombatFixtureWeapons.Handgun},107,1,0)",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6114)",
            Seed(6114, 20, 2, 17, Compel),
            Seed(6111, 50, 2, 17, Evade, ExecuteProgram)
        }, DevCommands: true, hotbarAbilities: new[] { Handguns, Hindering }, environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "45,113,130,150,217", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            "Stay on the starting street and stay out of interlock. QAEnemy stands 1.6 m east in Power with Compel (+20 Force Combat) and level-20 Combat Insight (+4%). QAFriend stands 2.8 m east in Speed with Evade and Execute Program (+5% Evade).",
            "Select QAEnemy. On page 2 press slot 1 Handguns, then slot 2 Hindering Shot once. Capture the cast and wait for the automatic helper comparison. No extra input is needed until both helpers finish.",
            "QAEnemy cannot interlock QAFriend at force 114 versus evade 115 while Hindering is active. After the eight-second debuff expires, the same helper attempt admits at force 124 versus evade 115. The helpers then withdraw. These scores use the implemented server entry rule."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        var attacker = await EnterAsync("QAEnemy");
        var defender = await EnterAsync("QAFriend");
        var defenderView = await attacker.WaitForViewNamedAsync(defender.Handle);
        var attackerView = await defender.WaitForViewNamedAsync(attacker.Handle);

        int defenderStart = defender.World.RpcSnapshot().Count;
        defender.SendRpc(EvadeCast);
        await StockAbilityProfile.ObserveAsync(defender, defenderStart,
            packet => StockAbilityProfile.Bonus(packet, Evade, EvasionShield.StateAbility, 0, 0, false),
            "QAFriend has an active Evade Combat shield");
        await StockAbilityProfile.ObserveAsync(defender, 0,
            packet => StockAbilityProfile.Bonus(packet, ExecuteProgram, 1049, 5, 0, false),
            "QAFriend has the five percent Execute Program evade bonus");

        int attackerStart = attacker.World.RpcSnapshot().Count;
        attacker.SendRpc("42 01 00 00 00 00 00");
        attacker.SendRpc(CompelCast);
        await StockAbilityProfile.ObserveAsync(attacker, attackerStart,
            packet => StockAbilityProfile.Bonus(packet, Compel, 519, 20, 300, false),
            "QAEnemy has the live Compel Close Combat force bonus");
        await StockAbilityProfile.ObserveAsync(attacker, 0,
            packet => StockAbilityProfile.Bonus(packet, Insight, 1048, 4, 0, false),
            "The required Combat Insight ancestor adds four percent at level 20");
        int hinderingStart = attacker.World.RpcSnapshot().Count;
        if (context.StockProbe is not { } shooter)
        {
            _ = RunNativeChallengeAsync();
            return Array.Empty<byte>();
        }

        await PvpOpponentsFixture.EnterAsync(shooter);
        NativeQaReadiness.RequireNoAmbientAggro(server, shooter);
        var target = await shooter.WaitForViewNamedAsync(attacker.Handle);
        await shooter.WaitForViewNamedAsync(defender.Handle);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        shooter.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        shooter.SendRpc(StarterHackerControlTargetMode.WithHandle(HandgunsCast, handle));
        await shooter.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 81 00 00 00");
        shooter.SendRpc(StarterHackerControlTargetMode.WithHandle(HinderingCast, handle));
        await EvidenceRetestWire.CastBarAsync(shooter, Hindering, 2);
        await CompareAsync();
        Check.Equal(0, shooter.World.Problems.Count + attacker.World.Problems.Count + defender.World.Problems.Count,
            "Force Combat comparison framing");
        return Array.Empty<byte>();

        async Task<HeadlessPlayer> EnterAsync(string name)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
            return helper;
        }

        async Task RunNativeChallengeAsync()
        {
            try
            {
                await attacker.WaitUntilAsync(() => attacker.World.RpcSnapshot().Skip(hinderingStart).Any(
                    frame => StockAbilityProfile.Bonus(frame, Hindering, 519, -10, 8, false)),
                    "Waiting for the stock Hindering Shot", TimeSpan.FromMinutes(5));
                await CompareAsync();
                Console.WriteLine("Hindering comparison passed. Interlock refused at 114/115 and admitted after expiry at 124/115.");
            }
            catch (Exception error)
            {
                Console.WriteLine("Hindering comparison stopped: " + error.Message);
            }
        }

        async Task CompareAsync()
        {
            string defenderHandle = Hex(defenderView);
            string attackerHandle = Hex(attackerView);
            await StockAbilityProfile.ObserveAsync(attacker, hinderingStart,
                packet => StockAbilityProfile.Bonus(packet, Hindering, 519, -10, 8, false),
                "Live Hindering penalty");
            int debugAt = server.DebugLog().Length;
            attacker.SkipReceived();
            attacker.SendRpc($"40 {defenderHandle} 01 6b");
            await attacker.WaitForRpcAsync($"4d 0e 00 01 00 {defenderHandle}");
            Check.That(server.DebugLog()[debugAt..].Contains("QAEnemy -> QAFriend force114 evade115 allowedFalse", StringComparison.Ordinal),
                "Live Hindering penalty makes entry fail while the shield is active");
            await StockAbilityProfile.ObserveAsync(attacker, hinderingStart,
                packet => StockAbilityProfile.Bonus(packet, Hindering, 519, -10, 8, true),
                "Hindering expires without removing Compel");
            debugAt = server.DebugLog().Length;
            attacker.SkipReceived();
            defender.SkipReceived();
            attacker.SendRpc($"40 {defenderHandle} 01 6b");
            await attacker.WaitForRpcAsync($"46 {defenderHandle} 01");
            await attacker.WaitForRpcAsync("45");
            Check.That(server.DebugLog()[debugAt..].Contains("QAEnemy -> QAFriend force124 evade115 allowedTrue", StringComparison.Ordinal),
                "Same entry succeeds after the eight-second penalty expires");
            attacker.SendRpc("44 00");
            defender.SendRpc("44 00");
            await attacker.WaitForRpcAsync($"46 {defenderHandle} 00", TimeSpan.FromSeconds(20));
            await defender.WaitForRpcAsync($"46 {attackerHandle} 00", TimeSpan.FromSeconds(20));
        }
    }

    private static string Seed(uint owner, int level, params ushort[] roots) =>
        "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
        string.Join(",", StockAbilityProfile.Profile(roots).Order().Select((id, index) =>
            $"({owner},{400 + index},{AbilityMessages.AbilityGameObject(id)},{level},1)"));

    private static string Hex(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(view.ViewId, view.SpawnId)));
}
