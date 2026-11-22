using System.Globalization;
using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using MxoHd.PacketResearch;
using static NativeQa.Modes.StarterHackerControlTargetMode;

namespace NativeQa.Modes;

public sealed class StarterGuardianAttackConsumersMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300;
    private const ushort Enhancement = 173;
    private const string Foe = "QAEnemy";
    // The rifle style and Body Shot are the full stock requests used in the PR756 replay.
    private const string RiflesFrame = "80b993000400020000000000000000000000000000000000000000000000000000000000086b0000000000";
    private const string BodyShotFrame = "80b9690004000200000000000000000000000000000000000000000069000000006b0000086b0000000000";
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 1006, 51, Enhancement).Order().ToArray();
    private static readonly ushort[] FriendLoadout = HelperLoadout(2, 17, 198, 131, 134, 1112);
    private static readonly ushort[] RifleLoadout = HelperLoadout(2, 17, 105, 147, 154);
    private static readonly (string Handle, int Id, int X, int Organisation, ushort[] Abilities)[] Helpers =
    {
        ("QAFriend", 6111, X + 100, 1, FriendLoadout),
        ("QAFar", 6112, X + 300, 1, RifleLoadout),
        (Foe, 6114, X + 200, 2, new ushort[] { 2, 17 })
    };

    private sealed record Sample(string Attacker, ushort Id, ushort Stat, ushort Style, string StyleFrame, string? Frame);
    private static readonly Sample[] Samples =
    {
        new("QAFriend", 198, 522, 17, AwakenedInterlockAdmissionMode.StockOpen,
            AwakenedInterlockAdmissionMode.Samples.Single(row => row.Ability == 198).Capture),
        new("QAFriend", 134, 1032, 1112, AwakenedStockStealthSpeedMode.SpyUse, null),
        new("QAFar", 105, 524, 147, RiflesFrame, BodyShotFrame)
    };

    public override string Name => "ability-parity-starter-guardian-attack-consumers";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={Level} WHERE char_id=110" },
        new[] { "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)",
            $"DELETE FROM inventory WHERE charId IN (110,6111,6112,6114) AND slot={CombatFixtureWeapons.HandSlot}",
            $"INSERT INTO inventory (charId,slot,goid,count,purity) VALUES (6112,{CombatFixtureWeapons.HandSlot},{CombatFixtureWeapons.Rifle},1,0)",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z},rotation=64,alignment=1,pvpflag=0,innerStrC=500,innerStrM=500 WHERE charId=110" }
            .Concat(Helpers.SelectMany(helper => new[]
            {
                $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={helper.X},y={Y},z={Z},alignment={helper.Organisation},pvpflag=0,healthC=20000,healthM=20000,innerStrC=1000,innerStrM=1000 WHERE charId={helper.Id}",
                "INSERT INTO char_abilities (char_id,slot,ability_id,level,is_loaded) VALUES " +
                    string.Join(",", helper.Abilities.Select((id, index) => $"({helper.Id},{400 + index},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
            })).ToArray(),
        DevCommands: true, hotbarAbilities: new ushort[] { 1006, Enhancement }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "173,198,134,105", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level 19 for all four characters. Outdoor NAV CSR ({X},{Y},{Z}) faces the helpers at ({X + 100},{Y},{Z}), ({X + 200},{Y},{Z}) and ({X + 300},{Y},{Z}). The three relevant memory loads are CSR {Loadout.Sum(AbilityMemory.Cost)}/49, QAFriend {FriendLoadout.Sum(AbilityMemory.Cost)}/49 and QAFar {RifleLoadout.Sum(AbilityMemory.Cost)}/49. Select page 2 slot 1 Hacker Style.",
            """Take baseline samples one at a time via the helper broker: {"actor":"QAFriend","action":"enhancement-sample","target":"QAEnemy","ability":198}, then the same with 134, then {"actor":"QAFar","action":"enhancement-sample","target":"QAEnemy","ability":105}. Wait for each response before the next. Each helper opens a real PvP interlock, resolves one queued special attempt, then both helpers use ordinary Withdraw.""",
            "Use /target QAFriend and cast Combat Enhancement from slot 2. Save the first 173 client request. Expect 2 s cast, 40 IS, 13 s own reuse and [MODIFIER] 173/522, 524, 1032 each +24 for 300 s on QAFriend. After reuse, repeat on QAFar to show 500 to 460 to 420 IS on CSR.",
            "Repeat the three samples. Each response returns the actual consumed [COMBAT-ATTACK OURS] number. Expect 19 to 43 on 198 stat 522, 26 to 50 on 134 stat 1032 and 24 to 48 on 105 stat 524. Read the associated [PVP] round even if the special loses the contest. No buff on CSR or QAEnemy and no maximum-vital changes."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in Helpers.Select(row => row.X).Prepend(X))
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, x, Y, Z, X + 200, Y, Z), $"Clear outdoor NAV for ({x},{Y},{Z})");

        var server = context.ActualServer!;
        foreach (var row in Helpers)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, row.Handle, row.Handle, stockCharacterLoad: true);
            context.Helpers.Add(helper.Handle, helper);
            VerifyLoadout(helper, row.Abilities);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        var rifle = context.Helpers["QAFar"];
        var rifleSelf = rifle.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.That(rifleSelf.Attributes.TryGetValue("EquippedItemID", out var item) &&
            BitConverter.ToUInt32(item) == CombatFixtureWeapons.Rifle, "The rifle helper actually starts with the rifle equipped");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        VerifyLoadout(actor, Loadout);
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(StarterHackerPassiveFixture.StyleFrame);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        var baseline = new Dictionary<ushort, int>();
        foreach (var sample in Samples)
            baseline.Add(sample.Id, (await MeasureAsync(context.Helpers[sample.Attacker], context.Helpers[Foe], sample.Id, server)).Attack);

        int strength = 500;
        foreach (string recipient in new[] { "QAFriend", "QAFar" })
        {
            var helper = context.Helpers[recipient];
            var view = await actor.WaitForViewNamedAsync(recipient);
            int receiveAt = helper.World.RpcSnapshot().Count;
            int sendAt = actor.World.RpcSnapshot().Count, viewsAt = actor.World.ViewSnapshot().Count;
            // PR699 preserves the stock 173 targeted request header; the opaque tail is from the existing support layout.
            EvidenceRetestWire.Cast(actor, Enhancement, view);
            await EvidenceRetestWire.CastBarAsync(actor, Enhancement, 2);
            strength -= 40;
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(viewsAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Contains((ushort)strength),
                $"Combat Enhancement spends 40 IS and leaves {strength}", HeadlessPlayer.DefaultTimeout);
            await EvidenceRetestWire.TimerAsync(actor, sendAt, Enhancement, AbilityCooldowns.OwnTimer, 13);
            foreach (ushort stat in new ushort[] { 522, 524, 1032 })
                await StockAbilityProfile.ObserveAsync(helper, receiveAt,
                    frame => StockAbilityProfile.Bonus(frame, Enhancement, stat, 24, 300, false), $"173/{stat} reaches {recipient}");
            if (recipient == "QAFriend")
                await actor.WaitForRpcAsync("80 bd 01 ad 00 04 00 00 50 41 00", TimeSpan.FromSeconds(16));
        }

        foreach (var sample in Samples)
        {
            int attack = (await MeasureAsync(context.Helpers[sample.Attacker], context.Helpers[Foe], sample.Id, server)).Attack;
            Check.Equal(baseline[sample.Id] + 24, attack, $"173's direct {sample.Stat} contributes exactly +24 to live special {sample.Id}");
            Console.WriteLine($"GUARDIAN ATTACK {sample.Id}/{sample.Stat}: {baseline[sample.Id]} -> {attack}");
        }
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count),
            "Guardian accuracy consumer framing");
        return Array.Empty<byte>();
    }

    private static ushort[] HelperLoadout(params ushort[] roots) => roots
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    private static void VerifyLoadout(HeadlessPlayer actor, ushort[] ids)
    {
        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(ids.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && archive.Keys.ToHashSet().SetEquals(ids) &&
            ids.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), $"Legal complete level-19 profile for {actor.Handle}");
    }

    internal sealed record Measurement(ushort Ability, ushort Stat, int Attack, string Round);

    internal static async Task<Measurement> MeasureAsync(HeadlessPlayer attacker, HeadlessPlayer defender, ushort id, ServerInstance server)
    {
        var sample = Samples.SingleOrDefault(row => row.Id == id && row.Attacker == attacker.Handle);
        string target = defender.Handle;
        if (sample == null || target != Foe)
            throw new InvalidOperationException("Use QAFriend 198/134 or QAFar 105 against QAEnemy in this fixture.");
        var opponent = await attacker.WaitForViewNamedAsync(target);
        uint handle = DuelPackets.Handle(opponent.ViewId, opponent.SpawnId);
        string hex = WireFormat.ToHex(BitConverter.GetBytes(handle));

        attacker.SkipReceived();
        attacker.World.SendRpcs(WithHandle(AwakenedInterlockAdmissionMode.StockOpen, handle), WireFormat.FromHex($"40 {hex} 01 6b"));
        await attacker.WaitForRpcAsync($"46 {hex} 01");
        try
        {
            // The stock Close Combat opening selects Self Defense. Reassert non-default styles after it.
            if (sample.Style != 17)
            {
                attacker.SendRpc(WithHandle(sample.StyleFrame, handle));
                await attacker.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {EvidenceRetestWire.Le(sample.Style)} 00 00");
            }
            int from = server.DebugLog().Length;
            // Leg Sweep has no full client frame retained in the repo. Reuse its existing 43-byte queue layout.
            byte[] request = sample.Frame != null ? WithHandle(sample.Frame, handle) :
                new AbilityUseRecord(id, handle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
            attacker.SendRpc(request);
            var result = new Regex($@"\[COMBAT-ATTACK OURS\] special {sample.Id} stat {sample.Stat} total (-?\d+)");
            Match match = Match.Empty;
            string? round = null;
            await attacker.WaitUntilAsync(() =>
            {
                var log = server.DebugLog()[from..];
                match = result.Match(log);
                if (!match.Success) return false;
                round = log[(match.Index + match.Length)..].Split('\n').FirstOrDefault(line =>
                    line.StartsWith("[PVP] Round ", StringComparison.Ordinal) &&
                    line.Contains(attacker.Handle, StringComparison.Ordinal) && line.Contains($"vs {target}", StringComparison.Ordinal));
                return round != null;
            }, $"The live PvP round consumes {sample.Id} stat {sample.Stat}", TimeSpan.FromSeconds(15));
            return new Measurement(id, sample.Stat, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), round!.Trim());
        }
        finally
        {
            attacker.SendRpc("44 01");
            defender.SendRpc("44 01");
            await attacker.WaitForRpcAsync($"46 {hex} 00", TimeSpan.FromSeconds(15));
        }
    }
}
