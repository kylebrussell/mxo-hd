using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderPetRangeReturnMode : NativeQaMode
{
    private const int Level = 15;
    private const int NearX = CombatFixtureLoadout.CasterX, FarX = NearX + 2200;
    private const int Y = CombatFixtureLoadout.GroundY, Z = CombatFixtureLoadout.CasterZ;
    private const ushort CoderStyle = 1008, RemoteProxy = 27;
    // Complete PR510 stock requests; only the live target view/spawn changes.
    private const string Summon = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string Fortify = "80b914000d000c000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private const string Drain = "80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, CoderStyle, RemoteProxy, CoderPets.FortifySimulacra, CoderPets.DrainCode }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-coder-pet-range-return";
    public override NativeQaModePlan Plan { get; } = new(Loadout, NearX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},innerStrC=350,innerStrM=1000 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        }, DevCommands: true, hotbarAbilities: new ushort[] { CoderStyle, RemoteProxy, 20, 19 },
        extraQaMetadata: new() { ["ScopedStockRows"] = "27,20,19", ["NativeCloseableRows"] = 0 }, environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Use page 2 slot 1 Coder Style, slot 2 Remote Proxy 1.0 and ?pet stay. Submit chat with Enter. Wait for 'Staying.' This fixture uses the dev order to avoid the misleading stock button tooltip.",
            $"Before casting, move to ?tp {FarX} {Y} {Z} 0 0 0. Select the parked Proxy and try Fortify Simulacra (20) and Drain Code (19). If an ability sends a request, expect an out-of-range refusal with no cast or reuse. The Proxy stays at 224 Max HP and alive.",
            $"Use ?tp {NearX} {Y} {Z} 0 0 0 to return. Fortify should work immediately and raise Max HP to 449. Then use slot 4 Drain Code. It removes that same pet, restores 50 IS with room below maximum, and starts 58 s reuse. Keep the body selected without Escape."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } caster) return Array.Empty<byte>();
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(Loadout.All(loaded.Contains), "The stock archive contains the two tested abilities and their prerequisites");
        await caster.EnterWorldAsync();
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        caster.SendRpc("80b9f003000000000000000000000000000000000000000000000000585cc70001000000086b0000000000");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        caster.SendRpc(Summon);
        var registration = await caster.WaitForRpcAsync("81 76");
        ViewCreation? proxy = null;
        await caster.WaitUntilAsync(() => (proxy = caster.World.CreationSnapshot().LastOrDefault(view =>
            view.Goid == 40376 && view.Attributes.TryGetValue("OwnerCharacterID", out var owner) &&
            BitConverter.ToUInt32(owner) == caster.CharId)) != null, "owned Remote Proxy view", HeadlessPlayer.DefaultTimeout);
        var pet = proxy!;
        ushort hp = BitConverter.ToUInt16(pet.Attributes["Health"]);
        ushort maxHp = BitConverter.ToUInt16(pet.Attributes["MaxHealth"]);
        Check.Equal((ushort)224, maxHp, "Level-4 Remote Proxy maximum health");
        caster.SendRpc("81 7a fe 00"); // Simulacra window: Stay.
        await caster.WaitForRpcAsync("81 78");
        caster.SendChat("?pet");
        await caster.WaitForTextAsync("Stay");

        ushort[] samples = { CoderPets.FortifySimulacra, CoderPets.DrainCode };
        await Move(caster, FarX);
        foreach (ushort id in samples)
        {
            caster.SkipReceived();
            int cursor = caster.World.RpcSnapshot().Count;
            Cast(caster, pet, id);
            await caster.WaitForTextAsync("Your simulacrum is out of range");
            var cast = BytePattern.Parse($"80 ac {StockAbilityProfile.LittleEndianHex(id)}");
            var reuse = BytePattern.Parse($"80 bd 01 {StockAbilityProfile.LittleEndianHex(id)}");
            await StockAbilityProfile.ObserveAsync(caster, cursor,
                packet => AbilityRefusals.TryDecode(packet.Bytes, out var refusal) && refusal.AbilityID == id,
                $"Out-of-range refusal for {id}");
            Check.That(!caster.World.RpcSnapshot().Skip(cursor).Any(packet =>
                cast.IsMatch(packet.Bytes) || reuse.IsMatch(packet.Bytes)), $"Out-of-range cast {id} starts no cast or reuse");
        }
        await Move(caster, NearX);

        caster.SkipReceived();
        int fortifyAt = caster.World.RpcSnapshot().Count;
        Cast(caster, pet, CoderPets.FortifySimulacra);
        await EvidenceRetestWire.CastBarAsync(caster, CoderPets.FortifySimulacra, 2);
        await EvidenceRetestWire.TimerAsync(caster, fortifyAt, CoderPets.FortifySimulacra, AbilityCooldowns.OwnTimer, 8);
        await caster.WaitForViewUpdateAsync(pet.ViewId, WireFormat.ToHex(CoderPets.BuildHealthUpdate(hp, (ushort)(maxHp + Level * 15))));
        Check.That(server.DebugLog().Contains("maxHP=449 resistance=30 duration=600"), "Returning to range allows Fortify");
        caster.SkipReceived();
        int drainAt = caster.World.RpcSnapshot().Count;
        int isAt = caster.World.ViewSnapshot().Count;
        Cast(caster, pet, CoderPets.DrainCode);
        await caster.WaitForRpcAsync($"81 77 {registration.Bytes[3]:x2}");
        await EvidenceRetestWire.TimerAsync(caster, drainAt, CoderPets.DrainCode, AbilityCooldowns.OwnTimer, 58);
        await caster.WaitUntilAsync(() =>
        {
            var frames = caster.World.ViewSnapshot();
            var before = frames.Take(isAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates).TakeLast(1);
            var after = frames.Skip(isAt).SelectMany(AbilityParityCoderSweepMode.InnerStrengthUpdates);
            var values = before.Concat(after).ToArray();
            return values.Zip(values.Skip(1), (previous, current) => current - previous).Contains(50);
        }, "Returning to range lets Drain remove the registered pet and restore 50 IS", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, caster.World.Problems.Count, "Starter pet cast request framing");
        return Array.Empty<byte>();
    }

    private static void Cast(HeadlessPlayer caster, ViewCreation pet, ushort id)
    {
        byte[] request = Convert.FromHexString(id == CoderPets.FortifySimulacra ? Fortify : Drain);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(pet.ViewId, pet.SpawnId));
        caster.SendRpc(request);
    }

    private static async Task Move(HeadlessPlayer caster, int x)
    {
        int cursor = caster.World.RpcSnapshot().Count;
        caster.SendChat($"?tp {x} {Y} {Z} 0 0 0");
        await StockAbilityProfile.ObserveAsync(caster, cursor, rpc => rpc.Hex.Contains("54656c65706f7274656420746f20302f30"),
            "fixture teleport acknowledged");
    }
}
