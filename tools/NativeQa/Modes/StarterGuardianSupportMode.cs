using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterGuardianSupportMode : NativeQaMode
{
    private const ushort Hacker = 1006, FastHealing = 46, Firewall = 68, QuickRecovery = 70, CombatEnhancement = 173;
    private const int Level = 16, X = 17000, Y = 495, Z = 2300, FriendX = 17200, EnemyX = 17350, FarX = 17350, FarZ = 3450;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, FastHealing, Firewall, QuickRecovery, CombatEnhancement).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] Resistance = { (157, 40), (180, 10), (181, 10), (1033, 10) };
    private static readonly (ushort Stat, int Value)[] Accuracy = { (522, 20), (524, 20), (1032, 20) };

    private static readonly (string Name, int Id, int X, int Z, int Alignment)[] Helpers =
        { ("QAFriend", 6111, FriendX, Z, 1), ("QAEnemy", 6114, EnemyX, Z, 2), ("QAFar", 6112, FarX, FarZ, 1) };

    public override string Name => "ability-parity-starter-guardian-support";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={Level} WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,FriendX,Z)},alignment=1,pvpflag=0,healthC=350,healthM=500,innerStrC=500,innerStrM=500 WHERE charId=110",
        }.Concat(Helpers.Select(helper =>
            $"UPDATE characters SET x={helper.X},y={Y},z={helper.Z},alignment={helper.Alignment},pvpflag=0," +
            $"level=50,exp={PlayerProgression.ExperienceForLevel(50)},healthC=100,healthM=500 WHERE charId={helper.Id}"))
            .Append("DELETE FROM char_abilities WHERE char_id IN (6111,6112,6114)").ToArray(),
        DevCommands: true, hotbarAbilities: new[] { Hacker, FastHealing, CombatEnhancement, Firewall },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "46,68,70,173", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAFriend ({FriendX},{Y},{Z}), 2 m east on outdoor NAV. QAEnemy ({EnemyX},{Y},{Z}) is 3.5 m east and hostile, QAFar ({FarX},{Y},{FarZ}) is a friendly 12 m southeast. Both are visible. Select Hacker Style in page 2 slot 1.",
            "Before any valid cast, /target QAEnemy and press page 2 slots 2, 3, then 4. Repeat with /target QAFar. Expect stock local refusal or a target-class 80 ba reason 24 if sent. No IS debit, cast/reuse, [MODIFIER], [VITALS] or [PVP] round.",
            "At login Quick Recovery is already loaded. [MODIFIER] char=110 source=70 stat=1052 +3 and stat=1057 +7, both indefinite. Leave it loaded because it qualifies the two casts below. These are the startup passive records.",
            "Select QAFriend and press page 2 slot 2 for Fast Healing 1.0. Expect a 2 s cast, 35 IS, 13 s own reuse, one [MODIFIER] char=6111 source=46 stat=112 +40 for 600 s, and no matching modifier on CSR. Use helper chat ?dismissbuff 46 to observe its removal.",
            "With QAFriend still selected, press page 2 slot 3 for Combat Enhancement 1.0. Expect a 2 s cast, 40 IS, 13 s own reuse, and [MODIFIER] char=6111 source=173 stats 522/524/1032 each +20 for 300 s. Use helper chat ?dismissbuff 173 to check all three removes. Neither cast changes maxima or enters [PVP].",
            "With QAFriend still selected, press slot 4 Personal Firewall. Expect 2 s, 40 IS, 13 s reuse and [MODIFIER] 68/157 +40, 68/180,181,1033 +10 on 6111 for 300 s only. Dismiss it on QAFriend to observe the four removes and the 75 HP heal. Save all first request frames."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (var (x, z) in Helpers.Select(helper => (helper.X, helper.Z)).Prepend((X, Z)))
            Check.That(NavMesh.Locate(1, x, Y, z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1 &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, z), $"Outdoor NAV with a clear floor path to ({x},{Y},{z})");
        foreach (var helper in Helpers)
        {
            var player = await HeadlessPlayer.LoginAsync(context.Address, helper.Name, helper.Name, stockCharacterLoad: true);
            context.Helpers.Add(player.Handle, player);
            await PvpOpponentsFixture.EnterAsync(player);
            NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, player);
        }
        var friend = context.Helpers["QAFriend"];
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Full ranked prerequisite profile fits");
        await PvpOpponentsFixture.EnterAsync(actor);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        var far = await actor.WaitForViewNamedAsync("QAFar");
        var enemy = await actor.WaitForViewNamedAsync("QAEnemy");
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)))}");
        await BonusAsync(actor, QuickRecovery, 1052, 3, 0, false, 0);
        await BonusAsync(actor, QuickRecovery, 1057, 7, 0, false, 0);
        byte staleSpawn = unchecked((byte)(target.SpawnId + 1));
        foreach (ViewCreation candidate in new[] { enemy, far, target with { SpawnId = staleSpawn } })
            foreach (ushort id in new[] { FastHealing, CombatEnhancement, Firewall })
                await RejectAsync(id, candidate);

        await BuffAsync(FastHealing, 600, new[] { ((ushort)112, 40) });
        await BuffAsync(CombatEnhancement, 300, Accuracy);
        ushort oldHealth = await AbilityEvidenceDirectHealsMode.ReadHealthAsync(friend);
        await BuffAsync(Firewall, 300, Resistance);
        Check.Equal((ushort)(oldHealth + 75), await AbilityEvidenceDirectHealsMode.ReadHealthAsync(friend), "Firewall term heal");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(helper => helper.World.Problems.Count), "Guardian support wire framing");
        Console.WriteLine("GUARDIAN SUPPORT PASS 46/68/70/173");
        return Array.Empty<byte>();

        ushort ReadIs() => actor.World.ViewSnapshot().SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates)
            .LastOrDefault(BitConverter.ToUInt16(actor.World.CreationSnapshot().First(view => view.ViewId == 2)
                .Attributes["InnerStrengthAvailable"]));

        async Task RejectAsync(ushort id, ViewCreation candidate)
        {
            ushort before = ReadIs();
            int cursor = actor.World.RpcSnapshot().Count;
            actor.SkipReceived();
            EvidenceRetestWire.Cast(actor, id, candidate);
            await StockAbilityProfile.ObserveAsync(actor, cursor, rpc =>
                AbilityRefusals.TryDecode(rpc.Bytes, out var refusal) && refusal.AbilityID == id &&
                refusal.ErrorID == AbilityRefusals.InvalidTarget, $"Refuse selected {candidate.ViewId}/{candidate.SpawnId} for {id}");
            actor.SendRpc("81 48");
            await actor.WaitForRpcAsync("81 49");
            Check.That(!actor.World.RpcSnapshot().Skip(cursor).Any(rpc => rpc.Opcode is 0xac or 0xbd),
                "Target refusal does not start a cast or reuse");
            Check.Equal(before, ReadIs(), "Target refusal spends no IS");
        }

        async Task BuffAsync(ushort id, float duration, (ushort Stat, int Value)[] modifiers)
        {
            ushort before = ReadIs();
            int castCursor = actor.World.RpcSnapshot().Count;
            int recipientCursor = friend.World.RpcSnapshot().Count;
            actor.SkipReceived();
            EvidenceRetestWire.Cast(actor, id, target);
            await EvidenceRetestWire.CastBarAsync(actor, id, 2);
            await EvidenceRetestWire.TimerAsync(actor, castCursor, id, AbilityCooldowns.OwnTimer, 13);
            await actor.WaitUntilAsync(() => ReadIs() == before - (id == FastHealing ? 35 : 40),
                $"Actual IS debit for {id}", HeadlessPlayer.DefaultTimeout);
            foreach (var (stat, value) in modifiers)
                await BonusAsync(friend, id, stat, value, duration, false, recipientCursor);
            int removalCursor = friend.World.RpcSnapshot().Count;
            friend.SendChat($"?dismissbuff {id}");
            foreach (var (stat, value) in modifiers)
            {
                await BonusAsync(friend, id, stat, value, duration, true, removalCursor);
                Check.That(!actor.World.RpcSnapshot().Skip(castCursor).Any(packet =>
                    StockAbilityProfile.Bonus(packet, id, stat, value, duration, false)), $"{id}/{stat} belongs only to QAFriend");
            }
        }
    }

    private static Task<RpcRecord> BonusAsync(HeadlessPlayer viewer, ushort source, ushort stat, int value, float duration,
        bool remove, int cursor) => StockAbilityProfile.ObserveAsync(viewer, cursor,
            packet => StockAbilityProfile.Bonus(packet, source, stat, value, duration, remove),
            $"Bonus {source}/{stat} {value} for {viewer.Handle}, remove={remove}");
}

