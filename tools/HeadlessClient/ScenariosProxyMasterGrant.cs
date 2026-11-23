using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task ProxyMasterPetGrant(ScenarioContext context)
    {
        const ushort level = 50, discipline = 599, grant = 1107;
        const ushort proxy = 594, casterSlot = 490;
        const int x = 17000, y = 495, z = 6400;
        var profile = new[] { (ushort)2, (ushort)17, CombatStyles.CoderStyle, discipline, proxy }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().Select(id => (id, level)).ToArray();
        Check.That(profile.Sum(row => AbilityMemory.Cost(row.id)) <= AbilityMemory.ForLevel(level),
            "The complete Proxy Master profile fits Archive memory");
        CharacterSeed Seed(string name, int position) => new(name)
        {
            X = position, Y = y, Z = z, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
            Health = 1000, MaxHealth = 1000, InnerStrength = 1000, MaxInnerStrength = 1000, Organization = 1
        };
        var owner = await context.AddPlayerAsync(Seed("ProxyMasterOwner", x) with { OwnedLoadedAbilities = profile });
        var other = await context.AddPlayerAsync(Seed("ProxyMasterObserver", x + 400) with
        {
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1), (27, 1) }
        });
        await context.Database.ExecuteAsync($"UPDATE char_abilities SET slot={casterSlot} WHERE char_id={owner.CharId} AND ability_id={AbilityMessages.AbilityGameObject(discipline)}");
        await owner.EnterWorldAsync();
        await other.EnterWorldAsync();
        byte[] added = Convert.FromHexString("80bc4503530400570200005304000000000000000000");
        byte[] removed = added.ToArray();
        removed[17] = 1;
        await ObserveDisciplineAsync(true, 0);
        Check.Equal(0L, await context.Database.LongAsync($"SELECT COUNT(*) FROM char_abilities WHERE char_id={owner.CharId} AND ability_id={AbilityMessages.AbilityGameObject(grant)}"),
            "Transient grant has no owned Archive row");

        // Retained stock Remote Proxy 5.0 and Deactivate. The 1107 envelopes below only exercise the server.
        owner.SendRpc("80b95202000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        byte ownPet = (await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14))).Bytes[3];
        other.SendChat("?pet summon 27");
        byte foreignPet = (await other.WaitForRpcAsync("81 76")).Bytes[3];

        uint own = await PetHandleAsync(owner.CharId, ownPet);
        uint foreign = await PetHandleAsync(other.CharId, foreignPet);
        int baseline = context.Server.DebugLog().Length;
        await RefusalAsync(0, AbilityRefusals.InvalidTarget);
        await RefusalAsync(foreign, AbilityRefusals.InvalidTarget);
        await RefusalAsync(own ^ 0x10000, AbilityRefusals.InvalidTarget);
        Check.That(!context.Server.DebugLog()[baseline..].Contains("ability=1107 paid="), "Refusals leave the owner's IS alone");

        await BeginCastAsync(own);
        await ChangeDisciplineAsync(false);
        await ChangeDisciplineAsync(true);
        await owner.WaitUntilAsync(() => context.Server.DebugLog()[baseline..].Contains("[PET] 1107 fizzles: the ability was unloaded during the cast"),
            "Unloading and reloading the grant during a cast cannot restore that cast", HeadlessPlayer.DefaultTimeout);
        Check.That(!context.Server.DebugLog()[baseline..].Contains("Proxy Master (1107)"), "The stale cast applies no pet bonus");
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("cost 10");
        await Task.Delay(TimeSpan.FromSeconds(15.25));

        await BeginCastAsync(own);
        await owner.WaitUntilAsync(() => context.Server.DebugLog()[baseline..].Contains(
                $"[PET] Proxy Master (1107) owner={owner.CharId} pet={ownPet} costReduction=25% regenIncrease=50% duration=600"),
            "The selected original pet receives the stored IS modifiers", HeadlessPlayer.DefaultTimeout);
        byte[] timer = AbilityCooldowns.BuildTimer(grant, AbilityCooldowns.OwnTimer, 15, start: true);
        await owner.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(timer), "Fifteen-second own timer");
        Check.Equal(2, context.Server.DebugLog()[baseline..].Split("ability=1107 paid=190").Length - 1,
            "Both admitted casts pay the stored 200 less the loaded +5 percent, exactly once each");

        await ChangeDisciplineAsync(false);
        await RefusalAsync(own, AbilityRefusals.NotLoaded);
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("cost 7.5");
        await ProxyMasterResourceFixture.CheckRateAsync(owner, ownPet, 3, 7.5);
        other.SendChat("?pet");
        await other.WaitForTextAsync("cost 10");

        owner.SendRpc("81 7a fe 01");
        await owner.WaitForRpcAsync($"81 77 {ownPet:x2}");
        owner.SendChat("?pet summon 27");
        byte replacement = (await owner.WaitForRpcAsync("81 76")).Bytes[3];
        Check.That(replacement != ownPet, "Replacement has a distinct pet generation");
        await ProxyMasterResourceFixture.CheckRateAsync(owner, replacement, 2, 10);
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("cost 10");
        Check.Equal(0, owner.World.Problems.Count + other.World.Problems.Count, "Grant, views and casts decode");
        Console.WriteLine("PROXY MASTER RESOURCE PASS: real Archive grant lifecycle, selected owned-pet cast, stale cast fencing, and per-pet IS cost.");

        byte[] Request(uint handle) =>
            new AbilityUseRecord(grant, handle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();

        async Task BeginCastAsync(uint handle)
        {
            owner.SkipReceived();
            owner.SendRpc(Request(handle));
            var cast = await owner.WaitForRpcAsync("80 ac 53 04");
            Check.Equal(2f, BitConverter.ToSingle(cast.Bytes.AsSpan(^4)), "Proxy Master uses its two-second cast");
        }

        async Task RefusalAsync(uint handle, uint reason)
        {
            owner.SkipReceived();
            owner.SendRpc(Request(handle));
            byte[] refusal = AbilityRefusals.Encode(new(grant, reason));
            await owner.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(refusal), $"1107 refusal {reason}");
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.1));
        }

        async Task ChangeDisciplineAsync(bool load)
        {
            int cursor = owner.World.RpcSnapshot().Count;
            SetAbilityLoaded(owner, casterSlot, discipline, level, load);
            await ObserveDisciplineAsync(load, cursor);
        }

        async Task ObserveDisciplineAsync(bool load, int cursor)
        {
            byte[] grantRecord = load ? added : removed;
            var modifier = BytePattern.Parse(PassiveRecord(discipline, 1053, 5, off: !load));
            await owner.WaitUntilAsync(() =>
            {
                var frames = owner.World.RpcSnapshot().Skip(cursor).ToArray();
                return frames.Any(frame => frame.Bytes.AsSpan().SequenceEqual(grantRecord)) &&
                    frames.Any(frame => modifier.IsMatch(frame.Bytes));
            }, $"Grant and cost modifier after parent loaded={load}", HeadlessPlayer.DefaultTimeout);
        }

        async Task<uint> PetHandleAsync(uint character, byte id)
        {
            ViewCreation? pet = null;
            await owner.WaitUntilAsync(() => (pet = owner.World.CreationSnapshot().LastOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
                BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == character)) != null,
                $"Pet {id} owned by {character} is visible", HeadlessPlayer.DefaultTimeout);
            return DuelPackets.Handle(pet!.ViewId, pet.SpawnId);
        }
    }
}
