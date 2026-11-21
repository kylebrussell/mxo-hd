using hds;
using HeadlessClient;
using static NativeQa.Modes.StarterCoderPetBuffsAndDetonateMode;

namespace NativeQa.Modes;

public sealed class StarterDetonatePlayerDefeatMode : NativeQaMode
{
    private const int Level = 18, PetLevel = 17, X = 24800, Y = 495, Z = -1498, TargetZ = -1848;
    private const ushort Coder = 1008, Proxy = 29, Fortify = 20, Detonate = 34, EmptyMind = 8;
    private const int BaseHp = 712, FortifiedHp = 982, Blast = 245, ResistedBlast = 206;
    internal const int Resistance = 25;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, Coder, Proxy, Fortify, Detonate).Order().ToArray();
    private static readonly ushort[] DefenderLoadout = StockAbilityProfile.Profile(2, 17, EmptyMind).Order().ToArray();
    // First stock Remote Proxy 3.0 request from the PR596 tiers run, reused without changing its selector.
    private const string ProxyUse = "80b91d0000000000000000000000000000000000000000000000000000000090767241086b0000000000";

    public override string Name => "ability-parity-starter-detonate-player-defeat";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), Array.Empty<string>(),
        new[]
        {
            PvpOpponentsFixture.NativeSql,
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},x={X},y={Y},z={Z}," +
                $"rotation={MobAggro.YawTowards(X,Z,X+150,Z)},innerStrC=700,innerStrM=1000 WHERE charId=110",
            $"UPDATE characters SET level={PetLevel},exp={PlayerProgression.ExperienceForLevel(PetLevel)},y={Y},z={TargetZ},alignment=2," +
                $"x=CASE WHEN charId=6114 THEN {X} ELSE {X + 300} END WHERE charId IN (6112,6114)",
            "UPDATE characters SET healthC=200,healthM=200 WHERE charId=6114",
            $"UPDATE characters SET x={X - 400},y={Y},z={TargetZ},alignment=1 WHERE charId=6111",
            $"UPDATE characters SET x={X + 1250},y={Y},z={TargetZ},alignment=2 WHERE charId=6113",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112,6113,6114)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " +
                string.Join(",", DefenderLoadout.Select((id, i) => $"(6112,{i},{0x80000000u | ((uint)id << 10)},17,1)")),
            $"UPDATE char_abilities SET level={Level} WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new[] { Coder, Proxy, Fortify, Detonate },
        testMobFixture: "martial-player-controls.csv", environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15",
            ["MXO_SIMULACRUM_QA_HOLD_IS"] = "1", [AreaTargetDiagnostics.Variable] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "20,29,34", ["NativeCloseableRows"] = 1, ["MemoryUsed"] = Loadout.Sum(AbilityMemory.Cost) },
        readyMessages: new[]
        {
            "Level 18 with the full Coder prerequisite profile. The pet spawns on the open stand 1.5 m straight ahead; no ambient mobs. QAEnemy starts at 200 HP south of CSR. QAFar is hostile inside the pet radius; QAFriend is allied inside; QAOther is hostile outside.",
            "Use Coder Style (page 2 slot 1), leave selection clear and summon Proxy 3.0 (slot 2). Expect L17, 712/712 HP, 8 s cast, 50 IS and 8 s reuse. Enter ?pet passive. Select the own Proxy, then use Fortify (slot 3). Expect [VITALS] owner=110 maxHP 712 -> 982, current HP 712, 30 IS and four +36 resistances for 600 s.",
            """With the Proxy still selected, send {"actor":"QAFar","action":"aura-resist"}. Once OK and [MODIFIER] char=6112 source=8 stat=157 +25 for 20 s, press slot 4. Expect 2 s cast, 30 IS, 60 s reuse, 245 on QAEnemy (killed by CSR_Tester), and 206 on QAFar. Area diagnostics reject QAFriend and QAOther. The original Proxy alone disappears after about half a second. CSR IS 700 → 650 → 620 → 590."""
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer!;
        foreach (string name in new[] { "QAEnemy", "QAFar", "QAFriend", "QAOther" })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name, stockCharacterLoad: true);
            context.Helpers.Add(name, helper);
            await PvpOpponentsFixture.EnterAsync(helper);
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);
        }
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(actor.CharacterData[6]).Where(row => AbilityMemory.IsAbility(row.Descriptor))
            .ToDictionary(row => AbilityCatalog.IdOf(row.Descriptor), row => row.Level);
        Check.That(Loadout.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level) && loaded.Keys.ToHashSet().SetEquals(Loadout) &&
            Loadout.All(id => AbilityPrerequisites.Current.Closure(id).All(need =>
                loaded.TryGetValue(need.Key, out ushort rank) && rank >= need.Value)), "The ranked Proxy and Detonate profile fits memory");
        await PvpOpponentsFixture.EnterAsync(actor);
        foreach (string name in context.Helpers.Keys) await actor.WaitForViewNamedAsync(name);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        actor.SendRpc(CoderStock);
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        int summonAt = await CastAsync(Proxy, Convert.FromHexString(ProxyUse), 8, 8, 650);
        var registered = await StockAbilityProfile.ObserveAsync(actor, summonAt, frame => frame.Bytes is [0x81, 0x76, ..], "Proxy window");
        byte sim = registered.Bytes[3];
        ViewCreation? pet = null;
        await actor.WaitUntilAsync(() => (pet = actor.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == sim)) != null,
            "The L17 Proxy arrives in view", HeadlessPlayer.DefaultTimeout);
        Check.Equal((byte)PetLevel, pet!.Attributes["Level"][0], "Proxy level");
        Check.Equal((ushort)BaseHp, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Proxy maximum");
        actor.SendChat("?pet passive");
        await actor.WaitForTextAsync("Passive.");
        uint handle = DuelPackets.Handle(pet.ViewId, pet.SpawnId);
        int views = actor.World.ViewSnapshot().Count;
        await CastAsync(Fortify, StarterHackerControlTargetMode.WithHandle(FortifyStock, handle), 2, 8, 620);
        await EvidenceRetestWire.ViewSinceAsync(actor, views, pet.ViewId, WireFormat.ToHex(CoderPets.BuildHealthUpdate(BaseHp, FortifiedHp)));

        var resisted = context.Helpers["QAFar"];
        var lethal = context.Helpers["QAEnemy"];
        await StarterPathogenistAuraResistanceMode.ApplyResistanceAsync(resisted, Resistance);
        var cursor = context.Helpers.ToDictionary(pair => pair.Key, pair => pair.Value.World.RpcSnapshot().Count);
        int lethalViews = lethal.World.ViewSnapshot().Count;
        int before = server.DebugLog().Length;
        int castAt = await CastAsync(Detonate, StarterHackerControlTargetMode.WithHandle(DetonateStock, handle), 2, 60, 590);
        await HitAsync(lethal, Blast, 0);
        await HitAsync(resisted, ResistedBlast, Blast - ResistedBlast);
        await lethal.WaitUntilAsync(() => PvpOpponentsFixture.HealthHistory(lethal.World.ViewSnapshot(), lethalViews, 2, self: true).Contains((ushort)0),
            "The lethal player reaches zero HP", HeadlessPlayer.DefaultTimeout);
        await StockAbilityProfile.ObserveAsync(actor, castAt, frame => frame.Bytes is [0x81, 0x77, var id] && id == sim,
            "The detonating pet's window is removed");
        string log = server.DebugLog()[before..];
        Check.That(log.Contains("[PVP] QAEnemy killed by CSR_Tester") &&
            log.Contains($"1109 type2 total{Resistance} raw{Blast} final{ResistedBlast}") &&
            !log.Contains("Detonate Proxy player source=CSR_Tester target=QAFriend") &&
            !log.Contains("Detonate Proxy player source=CSR_Tester target=QAOther"),
            "Player defeat, pet-level resistance, ally and radius exclusion all follow the same blast");
        Check.Equal(0, actor.World.Problems.Count + context.Helpers.Values.Sum(p => p.World.Problems.Count), "Detonate defeat framing");
        return Array.Empty<byte>();

        async Task HitAsync(HeadlessPlayer player, int damage, int absorbed)
        {
            byte[] expected = AbilityMessages.Damage(false, Detonate, actor.Handle, player.Handle, damage, absorbed);
            await StockAbilityProfile.ObserveAsync(player, cursor[player.Handle], frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"Detonate hits {player.Handle} for {damage} and absorbs {absorbed}");
        }

        async Task<int> CastAsync(ushort id, byte[] request, int cast, int reuse, int isAfter)
        {
            int rpcAt = actor.World.RpcSnapshot().Count;
            int isAt = actor.World.ViewSnapshot().Count;
            actor.SendRpc(request);
            await EvidenceRetestWire.CastBarAsync(actor, id, cast);
            await EvidenceRetestWire.TimerAsync(actor, rpcAt, id, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => actor.World.ViewSnapshot().Skip(isAt)
                .SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).Any(isNow => isNow == isAfter),
                $"{id} leaves {isAfter} IS", HeadlessPlayer.DefaultTimeout);
            return rpcAt;
        }
    }
}
