using System.Buffers.Binary;
using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class StarterCoderPetCastRangeMode : NativeQaMode
{
    private const int Level = 15;
    private const int NearX = CombatFixtureLoadout.CasterX, FarX = NearX + 2200;
    private const int Y = CombatFixtureLoadout.GroundY, Z = CombatFixtureLoadout.CasterZ;
    private const ushort CoderStyle = 1008, RemoteProxy = 27;
    // PR318 stock Fortify request, ec5525399ac6:4387–4388. Drain Code uses the same selected-pet layout.
    private const string PetCast = "80b91400090008000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000";
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, CoderStyle, RemoteProxy, CoderPets.FortifySimulacra, CoderPets.DrainCode }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-coder-pet-range";
    public override NativeQaModePlan Plan { get; } = new(Loadout, NearX, Z,
        Array.Empty<string>(), Array.Empty<string>(), new[]
        {
            $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},innerStrC=150,innerStrM=200 WHERE charId=110",
            $"UPDATE char_abilities SET level=LEAST(level,{Level}) WHERE char_id=110"
        }, DevCommands: true, environmentOverrides: new() { ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0" },
        readyMessages: new[]
        {
            "Activate Coder Style, summon Remote Proxy 1.0 and use ?pet stay. Wait for 'Staying.' This fixture uses the dev order to avoid the misleading stock button tooltip.",
            $"Before casting, move to ?tp {FarX} {Y} {Z} 0 0 0. Select the parked Proxy and try Fortify Simulacra (20) and Drain Code (19). If an ability sends a request, expect an out-of-range refusal with no cast or reuse. The Proxy stays at 224 Max HP and alive.",
            $"Use ?tp {NearX} {Y} {Z} 0 0 0 to return. Fortify should work immediately and raise Max HP to 449. The disposable replay also verifies both mid-cast range failures."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        if (context.Server == null) return Array.Empty<byte>();
        using var caster = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester", stockCharacterLoad: true);
        var loaded = AbilityMemoryUiFixture.Decode(caster.CharacterData[6]).Select(row => AbilityCatalog.IdOf(row.Descriptor)).ToHashSet();
        Check.That(Loadout.All(loaded.Contains), "The stock archive contains the two tested abilities and their prerequisites");
        await caster.EnterWorldAsync();
        var server = context.ActualServer!;
        NativeQaReadiness.RequireNoAmbientAggro(server, caster);
        var self = caster.World.CreationSnapshot().Last(view => view.ViewId == 2);
        await StarterFixedDamageMode.SelectStyle(caster, self, CoderStyle, 172);
        caster.SendChat("?pet summon 27");
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

        foreach (ushort id in samples)
        {
            int cursor = caster.World.RpcSnapshot().Count;
            Cast(caster, pet, id);
            await EvidenceRetestWire.CastBarAsync(caster, id, 2);
            await Move(caster, FarX);
            string fizzle = $"[PET] {id} fizzles: original pet, owner, range or combat requirement changed";
            await caster.WaitUntilAsync(() => server.DebugLog().Contains(fizzle) ||
                server.DebugLog().Contains("[PET] Fortify Simulacra (20) pet=") ||
                caster.World.RpcSnapshot().Skip(cursor).Any(rpc => rpc.Bytes.Length == 3 &&
                    rpc.Bytes[0] == 0x81 && rpc.Bytes[1] == 0x77 && rpc.Bytes[2] == registration.Bytes[3]),
                $"pet ability {id} finishes", HeadlessPlayer.DefaultTimeout);
            Check.That(server.DebugLog().Contains(fizzle), $"Pet ability {id} must fizzle after the pet leaves selection range");
            Check.That(!caster.World.RpcSnapshot().Skip(cursor).Any(rpc => BytePattern.Parse("81 77").IsMatch(rpc.Bytes)),
                "The original pet remains alive and registered");
            await Move(caster, NearX);
        }

        await EvidenceRetestWire.TimerAsync(caster, 0, CoderPets.FortifySimulacra, AbilityCooldowns.OwnTimer, 8, ended: true);
        caster.SkipReceived();
        Cast(caster, pet, CoderPets.FortifySimulacra);
        await caster.WaitForViewUpdateAsync(pet.ViewId, WireFormat.ToHex(CoderPets.BuildHealthUpdate(hp, (ushort)(maxHp + Level * 15))));
        Check.That(server.DebugLog().Contains("maxHP=449 resistance=30 duration=600"),
            "In-range Fortify still installs the caster-level health and resistance modifiers");
        Check.Equal(0, caster.World.Problems.Count, "Starter pet cast request framing");
        Console.WriteLine("  Moving past both stored ranges fizzles both casts, then in-range Fortify adds 225 max HP.");
        return Array.Empty<byte>();
    }

    private static void Cast(HeadlessPlayer caster, ViewCreation pet, ushort id)
    {
        byte[] request = Convert.FromHexString(PetCast);
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(2), id);
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
