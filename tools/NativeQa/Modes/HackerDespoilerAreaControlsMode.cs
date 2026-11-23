using hds;
using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa.Modes;

public sealed class HackerDespoilerAreaControlsMode : NativeQaMode
{
    private const int Level = 30, X = 17000, Y = 495, Z = 6400, CentreZ = 6600;
    private const int PlagueEdgeZ = 7100, PlagueOutsideZ = 7101, PowerlessEdgeZ = 7400, PowerlessOutsideZ = 7401;
    private const int ControlEdgeZ = 7000, ControlOutsideZ = 7001;
    private const ushort Powerlessness = 399, Plague = 400, Nuke = 60, EmptyMind = 8;
    private const float PowerlessSeconds = 6.54f;
    private static readonly ushort[] Abilities = StockAbilityProfile.Profile(2, 17, CombatStyles.HackerStyle, Powerlessness,
        Plague, Nuke).Order().ToArray();
    private static readonly ushort[] ProbeProfile = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();
    private static readonly (string Name, int Character, int X, int Z, int Organization)[] Stands =
    {
        ("QAEnemy", 6114, X, CentreZ, 2),
        ("QAOther", 6113, X, PowerlessEdgeZ, 2),
        ("QAFar", 6112, X, PowerlessOutsideZ, 2),
        ("QAFriend", 6111, X + 100, CentreZ, 1)
    };

    public override string Name => "ability-parity-hacker-despoiler-area-controls";

    public override NativeQaModePlan Plan { get; } = new(Abilities, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X,CentreZ)},innerStrC=700,innerStrM=900,alignment=1,pvpflag=0 WHERE charId=110",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Stands.SelectMany(stand =>
                ProbeProfile.Select((id, slot) => $"({stand.Character},{slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)")))
        }.Concat(Stands.Select(stand =>
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={stand.X},y={Y},z={stand.Z}," +
            $"alignment={stand.Organization},pvpflag=0,healthC=1000,healthM=1000,innerStrC=500,innerStrM=500 WHERE charId={stand.Character}"))
        .ToArray(), DevCommands: true, hotbarAbilities: new[] { CombatStyles.HackerStyle, Powerlessness, Plague, Nuke },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", ["MXO_FREECAST_VIRAL_QA"] = "1",
            ["MXO_FREECAST_VIRAL_QA_ATTACK15"] = "32766", ["MXO_FREECAST_VIRAL_QA_DEFENSE15"] = "0",
            [AreaTargetDiagnostics.Variable] = "1"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "399,400", ["NativeCloseableRows"] = 1 },
        readyMessages: new[]
        {
            $"Level {Level}, {Abilities.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. Verified outdoor NAV CSR ({X},{Y},{Z}) faces QAEnemy ({X},{Y},{CentreZ}) at 2 m. QAOther ({X},{Y},{PowerlessEdgeZ}) is 8.00 m from QAEnemy, QAFar ({X},{Y},{PowerlessOutsideZ}) is 8.01 m, and QAFriend ({X + 100},{Y},{CentreZ}) is the in-radius ally. All are clear of WinterFrost's ice.",
            """Powerless recipient retest: select Hacker Style (page 2 slot 1), /target QAEnemy, then submit {"actor":"QAEnemy","action":"zone-probe"}. Do not wait for the request response before casting: press slot 2 once when the console prints ZONE PROBE ARMED. The four helpers send their normal Empty Mind requests automatically. Save the full stock 399 request and the zone-probe response.""",
            "On QAEnemy and QAOther, Empty Mind must be refused as powerless with no cast, reuse or IS debit. Expect their [MODIFIER] 399/96 and linked 96/0 to expire together at 6.54 s, no source 734, and the same Empty Mind request to receive source 8 stat 157 +45 for 20 s after expiry. QAFar and QAFriend get +45 on their first request while the opponents are powerless. They never receive a 399 record. No [PVP] rounds or max [VITALS] changes.",
            "Select Hacker Style (page 2 slot 1). Test slots 2 and 3 with no target and then with /target QAFriend. Expect local refusal or reason 24 with no IS, cast bar, reuse, or effects. Then /target QAEnemy and leave the selection there.",
            "Press slot 2 for Zone of Powerlessness 399 at rest and save the complete stock request. Expect a 2 s cast, 30 IS (700 to 670), 30 s reuse, and [MODIFIER] source=399 stat=96 on QAEnemy and QAOther. The shipped six-second base becomes 6.54 s with this profile's +9% from Repel Combat. Neither player should receive a new source=734 stat=681 timer. No HP damage, ally or out-of-radius effect. The inputs behind the older 9.9 s and 10.14 s live records remain unresolved.",
            $$"""For Plague Zone 1.0, move QAOther to the 5.00 m edge and QAFar to 5.01 m using helper requests {"actor":"QAOther","action":"move","x":{{X}},"y":{{Y}},"z":{{PlagueEdgeZ}}} and {"actor":"QAFar","action":"move","x":{{X}},"y":{{Y}},"z":{{PlagueOutsideZ}}}. Keep QAEnemy selected and press slot 3. Save the first complete stock request. Expect 2 s, 15 IS (670 to 655), 12 s reuse, and [MODIFIER] source=400 stat=1157 +74 for 15 s on QAEnemy and QAOther only, followed by five [EFFECTS] ticks of 12 per recipient and no sixth. Slot 4 is the exact stock Code Nuke bootstrap control."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Stands.Select(stand => (stand.X, stand.Z))
            .Concat(new[] { (X, Z), (X, PlagueEdgeZ), (X, PlagueOutsideZ), (X, ControlEdgeZ), (X, ControlOutsideZ) }))
        {
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - Y) <= 100, $"Verified outdoor NAV ({x},{Y},{z})");
            Check.That(NavMesh.HasSightPath(1, X, Y, CentreZ, x, Y, z) &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Open sight to ({x},{Y},{z}) from caster and centre");
        }

        var server = context.ActualServer!;
        foreach (var stand in Stands)
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, stand.Name, stand.Name, stockCharacterLoad: true);
            context.Helpers.Add(stand.Name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(loaded.Keys.ToHashSet().SetEquals(Abilities) && Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) &&
            Abilities.All(id => AbilityPrerequisites.Current.Closure(id).All(need => loaded.TryGetValue(need.Key, out ushort rank) &&
                rank >= need.Value)), "The complete ranked Despoiler loadout fits in memory");
        await PvpOpponentsFixture.EnterAsync(caster);
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        foreach (var helper in context.Helpers.Values) await caster.WaitForViewNamedAsync(helper.Handle);
        caster.SendRpc(StarterHackerViralSightMode.StockStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");

        await MoveControlAsync("QAOther", ControlEdgeZ);
        await MoveControlAsync("QAFar", ControlOutsideZ);
        var primary = await caster.WaitForViewNamedAsync("QAEnemy");
        uint handle = DuelPackets.Handle(primary.ViewId, primary.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");

        var witnesses = context.Helpers.Values.Select(player => (Player: player, Cursor: player.World.RpcSnapshot().Count)).ToArray();
        int cast = caster.World.RpcSnapshot().Count;
        string stock = StarterHackerViralSightMode.Samples.Single(sample => sample.Ability == Nuke).Capture;
        caster.SendRpc(StarterHackerControlTargetMode.WithHandle(stock, handle));
        await EvidenceRetestWire.CastBarAsync(caster, Nuke, 4);
        await EvidenceRetestWire.TimerAsync(caster, cast, Nuke, AbilityCooldowns.OwnTimer, 2);
        var damage = BytePattern.Parse("81 67 ?? ?? ?? ?? " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.DamageTarget)) +
            " 00 00 10 00 00 " + WireFormat.ToHex(BitConverter.GetBytes(AbilityMessages.AbilityGameObject(Nuke))));
        foreach (var (player, cursor) in witnesses)
        {
            if (player.Handle is "QAEnemy" or "QAOther")
                await StockAbilityProfile.ObserveAsync(player, cursor, rpc => damage.IsMatch(rpc.Bytes), $"Code Nuke hits {player.Handle}");
            else
            {
                player.SendRpc("81 48");
                await StockAbilityProfile.ObserveAsync(player, cursor, rpc => rpc.Opcode == 0x149, "Excluded helper stays connected");
                Check.That(!player.World.RpcSnapshot().Skip(cursor).Any(rpc => damage.IsMatch(rpc.Bytes)), $"{player.Handle} is excluded");
            }
        }
        Check.Equal(0, caster.World.Problems.Count + witnesses.Sum(witness => witness.Player.World.Problems.Count), "Selected splash framing");
        await Task.WhenAll(witnesses.Select(witness => VerifyEmptyMindAsync(witness.Player)));
        Console.WriteLine("DESPOILER AREA BOOTSTRAP PASS: retained stock Style, Code Nuke and each helper's Empty Mind.");
        return Array.Empty<byte>();

        async Task MoveControlAsync(string name, int z)
        {
            var helper = context.Helpers[name];
            var view = await caster.WaitForViewNamedAsync(name);
            byte[] position = new[] { (float)X, (float)Y, (float)z }.SelectMany(BitConverter.GetBytes).ToArray();
            int cursor = caster.World.ViewSnapshot().Count;
            helper.World.SendViewList(Convert.FromHexString("02000108").Concat(position).Concat(new byte[] { 0, 0 }).ToArray());
            await EvidenceRetestWire.ViewSinceAsync(caster, cursor, view.ViewId, $"01 08 {Convert.ToHexString(position)}");
        }
    }

    internal static async Task<object> ProbeZoneAsync(HeadlessPlayer primary, IReadOnlyDictionary<string, HeadlessPlayer> helpers)
    {
        Check.Equal(6114u, primary.CharId, "The Powerless recipient comparison is armed by QAEnemy");
        var edge = helpers["QAOther"];
        var excluded = new[] { helpers["QAFar"], helpers["QAFriend"] };
        var cursors = helpers.ToDictionary(pair => pair.Key, pair => pair.Value.World.RpcSnapshot().Count);
        Console.WriteLine("ZONE PROBE ARMED: cast 399 on QAEnemy now.");
        await Task.WhenAll(new[] { primary, edge }.Select(async actor => await actor.WaitUntilAsync(() =>
            actor.World.RpcSnapshot().Skip(cursors[actor.Handle]).Any(frame => PowerlessRecord(frame, false, null)),
            $"Fresh 399/96 on {actor.Handle}", TimeSpan.FromSeconds(40))));

        var affectedTasks = new[] { primary, edge }.Select(VerifyBlockedThenRecoveredAsync).ToArray();
        await Task.WhenAll(excluded.Select(VerifyEmptyMindAsync));
        var affected = await Task.WhenAll(affectedTasks);
        foreach (var actor in excluded)
            Check.That(!actor.World.RpcSnapshot().Skip(cursors[actor.Handle]).Any(frame => PowerlessRecord(frame, false, null)),
                $"{actor.Handle} accepted Empty Mind outside the 399 recipient set");
        Console.WriteLine("ZONE RECIPIENT PASS: both affected helpers refused then recovered; both excluded helpers cast immediately.");
        return new { Affected = affected, Excluded = excluded.Select(actor => actor.Handle).ToArray() };

        async Task<object> VerifyBlockedThenRecoveredAsync(HeadlessPlayer actor)
        {
            int stage = cursors[actor.Handle];
            var added = actor.World.RpcSnapshot().Skip(stage).First(frame => PowerlessRecord(frame, false, null));
            Check.That(ManageBonusRecord.TryDecode(added.Bytes, out var record), "Read the paired 399 record ID");
            ushort recordId = record!.RecordId;
            int attempt = actor.World.RpcSnapshot().Count;
            int views = actor.World.ViewSnapshot().Count;
            actor.SkipReceived();
            SendEmptyMind(actor);
            var refusal = await StockAbilityProfile.ObserveAsync(actor, attempt, frame =>
                AbilityRefusals.TryDecode(frame.Bytes, out var response) && response.AbilityID == EmptyMind,
                $"399 blocks {actor.Handle}'s normal Empty Mind request");
            await actor.WaitForTextAsync("You cannot use abilities while powerless.", HeadlessPlayer.DefaultTimeout);
            var removed = await StockAbilityProfile.ObserveAsync(actor, stage, frame => PowerlessRecord(frame, true, recordId),
                $"399 removal matches {actor.Handle}'s record {recordId}");
            var linked = await StockAbilityProfile.ObserveAsync(actor, stage, frame =>
                StockAbilityProfile.Bonus(frame, TempMods.PowerlessAbility, 0, 0, 0, false), "Powerless linked add");
            Check.That(ManageBonusRecord.TryDecode(linked.Bytes, out var link), "Read the linked record ID");
            var linkedOff = await StockAbilityProfile.ObserveAsync(actor, stage, frame =>
                StockAbilityProfile.Bonus(frame, TempMods.PowerlessAbility, 0, 0, 0, true) &&
                ManageBonusRecord.TryDecode(frame.Bytes, out var off) && off!.RecordId == link!.RecordId,
                "Powerless linked removal matches the add");
            Check.That(!actor.World.RpcSnapshot().Skip(attempt).Any(frame =>
                frame.Bytes is [0x80, 0xac, 0x08, 0x00, ..] or [0x80, 0xbd, 0x01, 0x08, 0x00, ..] ||
                StockAbilityProfile.Bonus(frame, EmptyMind, 157, 45, 20, false)),
                "The refused Empty Mind has no cast, reuse or defense modifier");
            Check.That(AwakenedPowerShotMode.InnerStrengthUpdates(actor.World.ViewSnapshot().Skip(views)).All(value => value >= 500),
                "The refused Empty Mind leaves seeded 500 IS unchanged");
            Check.That(!actor.World.RpcSnapshot().Skip(stage).Any(frame => ManageBonusRecord.TryDecode(frame.Bytes, out var modifier) &&
                modifier!.SourceId == TempMods.ControlImmunityTimerAbility), "399 grants no control immunity");
            await VerifyEmptyMindAsync(actor);
            return new { actor.Handle, Added = added.Hex, Linked = linked.Hex, Refusal = refusal.Hex,
                Removed = removed.Hex, LinkedOff = linkedOff.Hex };
        }
    }

    private static bool PowerlessRecord(RpcRecord frame, bool remove, ushort? recordId) =>
        ManageBonusRecord.TryDecode(frame.Bytes, out var record) && record!.SourceId == Powerlessness &&
        record.TargetId == TempMods.PowerlessAbility && Math.Abs(record.Duration - PowerlessSeconds) < 0.0001f &&
        record.AddRemoveType == (remove ? 1 : 0) && (recordId == null || recordId == record.RecordId);

    private static async Task VerifyEmptyMindAsync(HeadlessPlayer actor)
    {
        int from = actor.World.RpcSnapshot().Count;
        SendEmptyMind(actor);
        await StockAbilityProfile.ObserveAsync(actor, from, frame => StockAbilityProfile.Bonus(frame, EmptyMind, 157, 45, 20, false),
            $"{actor.Handle} uses the retained normal Empty Mind request");
    }

    private static void SendEmptyMind(HeadlessPlayer actor) =>
        actor.SendRpc(StarterHackerControlTargetMode.WithHandle(AwakenedResistanceThresholdMode.EmptyCast, 0));
}
