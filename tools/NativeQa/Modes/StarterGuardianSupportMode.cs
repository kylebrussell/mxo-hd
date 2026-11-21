using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterGuardianSupportMode : NativeQaMode
{
    private const ushort Hacker = 1006, FastHealing = 46, QuickRecovery = 70, CombatEnhancement = 173;
    private const int Level = 15, X = 17000, Y = 495, Z = 2300, FriendX = 17200;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Hacker, FastHealing, QuickRecovery, CombatEnhancement).Order().ToArray();
    private static readonly (ushort Stat, int Value)[] Accuracy = { (522, 19), (524, 19), (1032, 19) };

    public override string Name => "ability-parity-starter-guardian-support";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={Level} WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,FriendX,Z)},alignment=1,pvpflag=0,healthC=350,healthM=500,innerStrC=500,innerStrM=500 WHERE charId=110",
            $"UPDATE characters SET x={FriendX},y={Y},z={Z},alignment=1,pvpflag=0,healthC=100,healthM=500 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111"
        }, DevCommands: true, hotbarAbilities: new[] { Hacker, FastHealing, CombatEnhancement },
        environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1" },
        extraQaMetadata: new() { ["ScopedStockRows"] = "46,70,173", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR ({X},{Y},{Z}) faces QAFriend ({FriendX},{Y},{Z}), 2 m east on outdoor NAV. Enter as usual, then select Hacker Style in page 2 slot 1.",
            "At login Quick Recovery is already loaded. [MODIFIER] char=110 source=70 stat=1052 +3 and stat=1057 +7, both indefinite. Leave it loaded because it qualifies the two casts below. This sample verifies the live passive record, not the sweep formula.",
            "Select QAFriend and press page 2 slot 2 for Fast Healing 1.0. Expect a 2 s cast, 35 IS, 13 s own reuse, one [MODIFIER] char=6111 source=46 stat=112 +40 for 600 s, and no matching modifier on CSR. Use helper chat ?dismissbuff 46 to observe its removal.",
            "With QAFriend still selected, press page 2 slot 3 for Combat Enhancement 1.0. Expect a 2 s cast, 40 IS, 13 s own reuse, and [MODIFIER] char=6111 source=173 stats 522/524/1032 each +19 for 300 s. Use helper chat ?dismissbuff 173 to check all three removes. Neither cast changes maxima or enters [PVP]. Save both first request frames."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, FriendX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var nav) && !nav.indoors && Math.Abs(nav.floorY + 95 - Y) < 1,
                $"Outdoor NAV stand ({x},{Y},{Z})");
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, friend);
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();

        var archive = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && Loadout.All(id => archive.ContainsKey(id) &&
            AbilityPrerequisites.Current.Closure(id).All(need => archive.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)),
            "Full ranked prerequisite profile fits");
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, actor);
        var target = await actor.WaitForViewNamedAsync(friend.Handle);
        actor.SendRpc(StarterHackerViralSightMode.StockStyle);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 ee 03 00 00");
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(target.ViewId, target.SpawnId)))}");
        await BonusAsync(actor, QuickRecovery, 1052, 3, 0, false, 0);
        await BonusAsync(actor, QuickRecovery, 1057, 7, 0, false, 0);
        await BuffAsync(FastHealing, 600, new[] { ((ushort)112, 40) });
        await BuffAsync(CombatEnhancement, 300, Accuracy);
        Check.Equal(0, actor.World.Problems.Count + friend.World.Problems.Count, "Guardian support wire framing");
        Console.WriteLine("GUARDIAN SUPPORT PASS 46/70/173");
        return Array.Empty<byte>();

        async Task BuffAsync(ushort id, float duration, (ushort Stat, int Value)[] modifiers)
        {
            int castCursor = actor.World.RpcSnapshot().Count;
            int recipientCursor = friend.World.RpcSnapshot().Count;
            actor.SkipReceived();
            EvidenceRetestWire.Cast(actor, id, target);
            await EvidenceRetestWire.CastBarAsync(actor, id, 2);
            await EvidenceRetestWire.TimerAsync(actor, castCursor, id, AbilityCooldowns.OwnTimer, 13);
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

