using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ProxyCoderSightMode : NativeQaMode
{
    private const ushort Derez = 26;
    private const int Level = 18, Y = 1095, Z = 16150;
    private const int Pick = 21550, Blocked = 22350, Owner = 21600, Pet = 21750, OwnerPark = 21350;
    private static readonly ushort[] Loadout = StockAbilityProfile.Profile(2, 17, 1008, Derez).Order().ToArray();
    private static readonly ushort[] HelperLoadout = StockAbilityProfile.Profile(2, 17, 1008, 22).Order().ToArray();

    public override string Name => "ability-parity-proxy-coder-sight";

    public override NativeQaModePlan Plan { get; } = new(Loadout, Pick, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={Level} WHERE char_id=110" }, new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},z={Z},pvpflag=0,innerStrC=200,innerStrM=200 WHERE charId IN (110,6114)",
            $"UPDATE characters SET x={Pick},rotation=64,alignment=1 WHERE charId=110",
            $"UPDATE characters SET x={Owner},alignment=2 WHERE charId=6114",
            "DELETE FROM char_abilities WHERE char_id=6114",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded) VALUES " + string.Join(',',
                HelperLoadout.Select((ability, slot) => $"(6114,{slot},{AbilityMessages.AbilityGameObject(ability)},{Level},1)"))
        }, DevCommands: true, hotbarAbilities: new ushort[] { 1008, Derez }, testMobFixture: "martial-player-controls.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "26", ["NativeCloseableRows"] = 1 }, readyMessages: new[]
        {
            $"Level {Level}, {Loadout.Sum(AbilityMemory.Cost)}/{AbilityMemory.ForLevel(Level)} memory. CSR starts at ({Pick},{Y},{Z}) facing the isolated Nuker ({Pet},{Y},{Z}) 2 m east. QAEnemy steps west to ({OwnerPark},{Y},{Z}) after summoning. Switch to Coder Style with page 2 slot 1, then single-click the Nuker. Keep it selected.",
            $"Use the printed blocked-stand teleport command to ({Blocked},{Y},{Z}). The same Nuker is now 6 m west across a mapped sight gap. Try page 2 slot 2. Expected sight refusal, reason {AbilityRefusals.TargetNotInSight} if a request is sent, no cast/cost/reuse and the Nuker remains.",
            "Use the printed clear-stand command, pause briefly, reselect the Nuker if needed, and press slot 2 again. Expect 40 IS, two seconds, 13.5-second reuse, 13000 damage and the same impact then death sequence previously witnessed in PR753."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var nav = new Dictionary<int, NavLocation>();
        foreach (int x in new[] { Pick, Blocked, Owner, Pet, OwnerPark })
        {
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Proxy Coder fixture stand ({x},{Y},{Z}) has verified NAV");
            nav.Add(x, stand);
        }
        Check.That(NavMesh.HasSightPath(1, Pick, Y, Z, Pet, Y, Z) && !NavMesh.HasSightPath(1, Blocked, Y, Z, Pet, Y, Z),
            "Same spawn has a clear picking stand and an in-range blocked casting stand");

        var helper = await HeadlessPlayer.LoginAsync(context.Address, "QAEnemy", "QAEnemy", stockCharacterLoad: true);
        context.Helpers.Add(helper.Handle, helper);
        await PvpOpponentsFixture.EnterAsync(helper);
        NativeQaReadiness.RequireNoAmbientAggro(context.ActualServer!, helper);
        helper.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await helper.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        helper.SendRpc(ProxyCoderDerezMode.StockDaemon);
        await EvidenceRetestWire.CastBarAsync(helper, 22, 8);
        await helper.WaitForRpcAsync("81 76");
        var pet = await ProxyCoderDerezMode.SeePetAsync(helper, helper.CharId);
        Check.That(EngageFoesFixture.Position(pet).SequenceEqual(new double[] { Pet, Y, Z }), "The Nuker has the verified stand");
        helper.SendChat("?pet passive");
        await helper.WaitForTextAsync("Passive.");
        helper.SendChat("?pet stay");
        await helper.WaitForTextAsync("Staying.");
        await MoveAsync(helper, OwnerPark, "C0");
        Console.WriteLine($"Proxy Coder blocked stand: {Teleport(Blocked, "C0")}; clear stand: {Teleport(Pick, "40")}");
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();

        StarterHackerPassiveFixture.VerifyProfile(caster, Loadout);
        await PvpOpponentsFixture.EnterAsync(caster);
        caster.SendRpc(StarterCoderPetBuffsAndDetonateMode.CoderStock);
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var target = await ProxyCoderDerezMode.SeePetAsync(caster, helper.CharId);
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        caster.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        byte[] request = ProxyCoderDerezMode.Use(handle);
        await MoveAsync(caster, Blocked, "C0");
        int start = caster.World.RpcSnapshot().Count;
        await StarterHackerControlTargetMode.RefusedAsync(caster, Derez, request, AbilityRefusals.TargetNotInSight);
        Check.That(!context.ActualServer!.DebugLog().Contains("[ABILITY-COST] ability=26", StringComparison.Ordinal),
            "The blocked cast spends no Inner Strength");
        await MoveAsync(caster, Pick, "40");
        await Task.Delay(600); // The stock duplicate-press guard also applies after a refusal.
        caster.SendRpc(request);
        await EvidenceRetestWire.CastBarAsync(caster, Derez, 2);
        await EvidenceRetestWire.TimerAsync(caster, start, Derez, AbilityCooldowns.OwnTimer, 13.5f);
        await helper.WaitForRpcAsync("81 77");
        Console.WriteLine("PROXY CODER SIGHT PASS 26");
        return Array.Empty<byte>();

        string Teleport(int x, string yaw) => $"?tp {x} {Y} {Z} {nav[x].sector:x} {nav[x].region:x} {nav[x].polygon:x} - {yaw}";

        async Task MoveAsync(HeadlessPlayer player, int x, string yaw)
        {
            player.SendChat(Teleport(x, yaw));
            await player.WaitForTextAsync($"Teleported to {nav[x].sector:x}/{nav[x].region:x}");
        }
    }
}
