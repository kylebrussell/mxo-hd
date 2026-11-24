using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ProxyMasterRecipientMode : NativeQaMode
{
    private const ushort Level = 50, Parent = 599, Grant = 1107, Proxy = 594;
    private const int X = 17000, Y = 495, Z = 6400, PetX = X + 150, FriendX = 17400;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, CombatStyles.CoderStyle, Parent, Proxy)
        .Order().ToArray();
    private static readonly int Memory = Loadout.Sum(AbilityMemory.Cost);
    private static readonly (ushort Parent, ushort Child, (ushort Stat, int Value)[] Stats)[] Passives =
        { (Parent, Grant, new[] { ((ushort)1053, 5) }) };

    public override string Name => "ability-parity-proxy-master-recipient";

    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110" },
        new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,PetX,Z)},innerStrC=700,innerStrM=1000,alignment=1 WHERE charId=110",
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={FriendX},y={Y},z={Z},alignment=1,innerStrC=700,innerStrM=1000 WHERE charId=6111",
            "DELETE FROM char_abilities WHERE char_id=6111",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(",", Loadout.Select((id, slot) =>
                $"(6111,{slot},{AbilityMessages.AbilityGameObject(id)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new[] { CombatStyles.CoderStyle, Proxy, Grant },
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1",
            [CoderPets.ProxyMasterMeterQa] = "1", [CoderPets.ProxyMasterSweepQa] = "1",
            ["MXO_BUFF_DISPEL_QA_ROLL100"] = "0"
        },
        extraQaMetadata: new() { ["ScopedStockRows"] = "1107", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Memory },
        readyMessages: new[]
        {
            $"Level {Level}, {Memory}/{AbilityMemory.ForLevel(Level)} legal memory. Verified clear outdoor NAV CSR ({X},{Y},{Z}) faces the owned Proxy's spawn ({PetX},{Y},{Z}) at 1.5 m. All of 1107's ranked prerequisites are loaded and 1107 itself is a transient Actions grant.",
            "Choose Coder Style on page 2 slot 1 and summon Remote Proxy 5.0 from slot 2 from rest. Expect L50, 8 s, 114 IS (700->586), 8 s own reuse. Click the visible Proxy once and confirm its name. Before 1107, type ?pet exhaust, then ?pet meter twice about 5 s apart. Ignore reading intervals spanning the reset. Compare delta IS divided by delta tick in [PET METER]. Expect +2 IS/s and cost 10.",
            """With your selected pet type ?pet master-wound and send {"actor":"QAFriend","action":"chat","text":"?pet master-wound"}. Both existing pets now sit at half HP with real Slow 86 records. This disposable setup applies the condition without an aggro source. Use the meter twice, two seconds apart, to record the flat unbuffed HP.""",
            "With that same pet selected, double-click the rank-zero Proxy Master action in the separate Proxy Master Actions section (or use page 2 slot 3). Save the full first ordinary stock 80 b9 53 04 frame. Expect 2 s, [ABILITY-COST] ability=1107 paid=190 IS=586->396, 15 s own reuse, and [PET] Proxy Master (1107) with the current pet ID, costReduction=25% regenIncrease=50% duration=600 healthRegenIncrease=50%. Expect immediate [SWEEP] 1107 removes 86 only from your Proxy.",
            """Immediately type ?pet master-wound again to apply a fresh Slow to your existing mastered pet. Expect [MODIFIER] mob=... source=1107 stat=710 +10, a second [SWEEP] near 10 s after the cast, and rising HP in consecutive [PET METER] rows with unchanged max. The additive HP estimate out of combat is 0.5% max/s. Send {"actor":"QAFriend","action":"chat","text":"?pet meter"} for the unchanged unmastered HP, no 1107 effect and no sweep on that Proxy. The fixture fixes only the dispel roll.""",
            "Repeat ?pet exhaust and two ?pet meter samples on the same pet. Expect +3 IS/s and cost 7.5. Type ?qaunload 599. Its Actions grant and source 599 stat 1053 +5 disappear. The current pet still reports cost 7.5. Reload 599, then /ordersimulacra All Deactivate. Expect that pet's window removed. Summon again from slot 2. The new ID reports cost 10 with no inherited 1107 effect.",
            "The pet meter reads actual live resource ticks without advancing the clock. Focused tests exercise the 600 s expiry boundary. The pet HP bonus uses the common health rate as its baseline. The original retail pet base rate remains open. Projected DONE +1 if the stock grant confirms init and periodic sweep, owned recipient and both resource effects."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Proxy Master requires repository NAV.");
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, PetX, FriendX, FriendX + 150 })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y &&
                NavMesh.HasSightPath(1, X, Y, Z, x, Y, Z), $"Clear outdoor NAV at ({x},{Y},{Z})");
        var server = context.ActualServer!;
        var friend = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend", stockCharacterLoad: true);
        context.Helpers.Add(friend.Handle, friend);
        await friend.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, friend);
        friend.SendRpc(AdvancedSimulacraNoticeFixture.CoderStyle);
        await friend.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        friend.SendRpc(AdvancedSimulacraNoticeFixture.Proxy5);
        var friendRegistration = await StockAbilityProfile.ObserveAsync(friend, 0, frame => frame.Opcode == 0x176, "Unmastered control Proxy window");
        var friendPet = await AdvancedSimulacraNoticeFixture.PetViewAsync(friend, friend.CharId, friendRegistration.Bytes[3]);
        Check.That(EngageFoesFixture.Position(friendPet).SequenceEqual(new double[] { FriendX + 150, Y, Z }), "Unmastered pet stand is visible and mapped");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        var owned = AbilityMemoryUiFixture.Decode(caster.CharacterData[7]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor));
        Check.That(Memory <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.Append(Grant).All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out var row) && row.Level >= need.Value)),
            "Proxy Master and its Actions grant have every ranked prerequisite in the legal loadout");
        Check.That(!owned.ContainsKey(Grant), "The Actions grant has no owned Archive row");

        await caster.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        await StockAbilityProfile.CheckPassivesAsync(caster, new List<object>(), Passives, 0, remove: false);
        caster.SendRpc(AdvancedSimulacraNoticeFixture.CoderStyle);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        int cast = caster.World.RpcSnapshot().Count;
        caster.SendRpc(AdvancedSimulacraNoticeFixture.Proxy5);
        await EvidenceRetestWire.CastBarAsync(caster, Proxy, 8);
        await EvidenceRetestWire.TimerAsync(caster, cast, Proxy, AbilityCooldowns.OwnTimer, 8);
        var registered = await StockAbilityProfile.ObserveAsync(caster, cast, frame => frame.Opcode == 0x176, "Ordinary Proxy window");
        byte id = registered.Bytes[3];
        var pet = await AdvancedSimulacraNoticeFixture.PetViewAsync(caster, caster.CharId, id);
        Check.Equal((byte)Level, pet.Attributes["Level"][0], "The stock summon yields a level-50 Proxy");
        Check.That(EngageFoesFixture.Position(pet).SequenceEqual(new double[] { PetX, Y, Z }), "The Proxy is on its visible NAV stand");
        Check.That(server.DebugLog().Contains("[ABILITY-COST] char=110 ability=594 paid=114 IS=700->586"),
            "The captured summon pays the discounted amount once");
        await ProxyMasterResourceFixture.CheckRateAsync(caster, id, 2, 10);

        caster.SendChat("?pet master-wound");
        await caster.WaitForTextAsync("Proxy Master wound ready");
        friend.SendChat("?pet master-wound");
        await friend.WaitForTextAsync("Proxy Master wound ready");
        Check.That(server.DebugLog().Contains("source=86 stat=", StringComparison.Ordinal), "Both controls use the ordinary Slow timed records");

        int dismissed = caster.World.RpcSnapshot().Count;
        caster.SendRpc(AdvancedSimulacraNoticeFixture.Deactivate);
        await StockAbilityProfile.ObserveAsync(caster, dismissed,
            frame => frame.Bytes is [0x81, 0x77, var removed] && removed == id, "Captured Deactivate removes the original Proxy");
        Check.Equal(0, caster.World.Problems.Count, "Grant and ordinary Proxy wire parsing");
        Console.WriteLine("PROXY MASTER RECIPIENT BOOTSTRAP PASS: retained Coder Style, 594 and Deactivate; visible pet, grant and baseline resource ticks. The first complete 1107 frame is reserved for stock QA.");
        return Array.Empty<byte>();
    }
}
