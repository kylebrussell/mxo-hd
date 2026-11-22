using System.Buffers.Binary;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AbsorbSimulacraServer(ScenarioContext context)
    {
        const ushort level = 27, absorb = CoderPets.AbsorbSimulacra;
        const string durable = "80b94a02000000000000000000000000000000000000000000000000585cb70001000000086b0000000000";
        const string fragile = "80b91b00000000000000000000000000000000000000000000000000585cb40001000000086b0000000000";
        const string nuker = "80b916000000000000000000000000000000000000000000000000000090767241086b0000000000";
        var abilities = new ushort[] { 2, 17, 1008, 27, 586, absorb }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().Select(id => (id, level)).ToArray();
        var seed = new CharacterSeed("AbsorbOwner")
        {
            X = 17000, Y = 495, Z = 6400, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
            Health = 100, MaxHealth = 500, InnerStrength = 700, MaxInnerStrength = 1000, Organization = 1,
            OwnedLoadedAbilities = abilities
        };
        var caster = await context.AddPlayerAsync(seed);
        var friend = await context.AddPlayerAsync(seed with { Handle = "AbsorbFriend", X = 17400 });
        await friend.EnterWorldAsync();
        await caster.EnterWorldAsync();
        friend.SendRpc(nuker);
        await friend.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        caster.SendRpc("80b9f003000000000000000000000000000000000000000000000000000090767241086b0000000000");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        var strong = await SummonAsync(durable, 1087);
        var foreign = await caster.WaitForViewNamedAsync("Nuker Simulacrum");

        int rejected = context.Server.DebugLog().Length;
        Check.Equal(friend.CharId, BitConverter.ToUInt32(foreign.Attributes["OwnerCharacterID"]), "Foreign pet belongs to the friendly witness");
        await RefusedAsync(0);
        await RefusedAsync(DuelPackets.Handle(foreign.ViewId, foreign.SpawnId));
        await RefusedAsync(DuelPackets.Handle(strong.ViewId, (ushort)(strong.SpawnId ^ 1)));
        Check.That(!context.Server.DebugLog()[rejected..].Contains($"ability={absorb} paid="),
            "Empty, foreign and stale handles spend no IS");

        int healthAt = caster.World.ViewSnapshot().Count;
        int friendAt = friend.World.ViewSnapshot().Count;
        var first = await CastAsync(strong);
        await caster.WaitUntilAsync(() => caster.World.RpcSnapshot().Skip(first.Rpc).Any(frame =>
            frame.Opcode == 0x178 && frame.Bytes[2] == strong.Attributes["SimulacraID"][0] &&
            BitConverter.ToUInt16(frame.Bytes, 4) == 362 && BitConverter.ToUInt16(frame.Bytes, 6) == 1087),
            "The same Proxy window shows HP 362/1087", HeadlessPlayer.DefaultTimeout);
        await RequireEffectsAsync(first.Log, 1087, 362, 100, 334);
        Check.That(PvpOpponentsFixture.HealthHistory(caster.World.ViewSnapshot(), healthAt, 2, self: true).Contains((ushort)334),
            "The caster receives the separate source heal");
        Check.That(!PvpOpponentsFixture.HealthHistory(friend.World.ViewSnapshot(), friendAt, 2, self: true).Any(),
            "The source heal does not reach the friendly observer");

        var weak = await SummonAsync(fragile, 224);
        caster.SendChat("?health 400");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 40 90 01");
        await caster.WaitForRpcAsync("80 bd 01 5f 02 04 00 00 f0 41 00", TimeSpan.FromSeconds(35));
        var second = await CastAsync(weak);
        byte weakId = weak.Attributes["SimulacraID"][0];
        await RequireEffectsAsync(second.Log, 224, 0, 400, 500);
        Check.That(caster.World.RpcSnapshot().Skip(second.Rpc).Count(frame => frame.Bytes is [0x81, 0x77, var id] && id == weakId) == 1,
            "Lethal neutral damage terminates the own Proxy once");
        friend.SendChat("?pet");
        await friend.WaitForTextAsync("Nuker Simulacrum L7");

        async Task<(int Log, int Rpc)> CastAsync(ViewCreation pet)
        {
            uint target = DuelPackets.Handle(pet.ViewId, pet.SpawnId);
            caster.SendRpc("81 51 " + WireFormat.ToHex(BitConverter.GetBytes(target)));
            caster.SkipReceived();
            int from = context.Server.DebugLog().Length, cursor = caster.World.RpcSnapshot().Count;
            caster.SendRpc(Request(target));
            var cast = await caster.WaitForRpcAsync("80 ac 5f 02");
            Check.Equal(2f, BitConverter.ToSingle(cast.Bytes, cast.Bytes.Length - 4), "Two-second 607 cast");
            await caster.WaitForRpcAsync("80 bd 01 5f 02 04 00 00 f0 41 01");
            return (from, cursor);
        }

        async Task RefusedAsync(uint target)
        {
            caster.SkipReceived();
            caster.SendRpc(Request(target));
            var expected = AbilityRefusals.Encode(new AbilityRefusals.Record(absorb, AbilityRefusals.InvalidTarget));
            await caster.WaitForRpcAsync(reply => reply.Bytes.AsSpan().SequenceEqual(expected), "Stock invalid target reason 24");
            await Task.Delay(TimeSpan.FromSeconds(AbilityHandler.DuplicateAbilityWindowSeconds + 0.2));
        }

        Task RequireEffectsAsync(int from, int petBefore, int petAfter, int healthBefore, int healthAfter) =>
            caster.WaitUntilAsync(() =>
            {
                string changes = context.Server.DebugLog()[from..];
                return changes.Contains($"[ABILITY-COST] char={caster.CharId} ability=607 paid=25 IS=") &&
                    changes.Contains($"type=5 raw=725 HP {petBefore}->{petAfter}") &&
                    changes.Contains($"[VITALS] char={caster.CharId} ability=607 type=6 raw=234 HP {healthBefore} -> {healthAfter}");
            }, "The exact 25 IS cost and both neutral Health effects publish", HeadlessPlayer.DefaultTimeout);

        async Task<ViewCreation> SummonAsync(string captured, ushort maxHp)
        {
            caster.SkipReceived();
            int firstCreation = caster.World.CreationSnapshot().Count;
            caster.SendRpc(captured);
            var register = await caster.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
            byte id = register.Bytes[3];
            ViewCreation? pet = null;
            await caster.WaitUntilAsync(() => (pet = caster.World.CreationSnapshot().Skip(firstCreation).FirstOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
                BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == caster.CharId)) != null,
                $"Owned Proxy {id} is visible", HeadlessPlayer.DefaultTimeout);
            Check.Equal(maxHp, BitConverter.ToUInt16(pet!.Attributes["MaxHealth"]), "Ordinary pet maximum HP");
            return pet;
        }

        static byte[] Request(uint target)
        {
            // Server-only test: own-pet Drain Code's captured shape with 607 and the live target.
            // The NativeQa bootstrap never sends this. Native QA captures the first stock 607 activation.
            byte[] frame = Convert.FromHexString("80b913000e000f000000000000000000000000000000000000000000585cb40001000000086b0000000000");
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), CoderPets.AbsorbSimulacra);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), target);
            return frame;
        }
    }
}
