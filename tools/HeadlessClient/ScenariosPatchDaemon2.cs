using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    // Server-only behavior check. NativeQa's separate bootstrap uses captured 602 and Deactivate
    // inputs. Use the server's ordinary cast handler through its dev command for 612 until a stock frame is recorded.
    private static async Task PatchDaemon2Server(ScenarioContext context)
    {
        const ushort level = 23;
        var profile = new ushort[] { 2, 17, 1008, 602, 612 }
            .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order()
            .Select(id => (id, level)).ToArray();
        var seed = new CharacterSeed("PatchDaemonOwner")
        {
            X = 17000, Y = 495, Z = 6400, Level = level, Experience = (int)PlayerProgression.ExperienceForLevel(level),
            Health = 500, MaxHealth = 500, InnerStrength = 500, MaxInnerStrength = 1000, Organization = 1,
            OwnedLoadedAbilities = profile
        };
        var owner = await context.AddPlayerAsync(seed);
        var observer = await context.AddPlayerAsync(seed with { Handle = "PatchDaemonFriend", X = 17400, Health = 200 });
        await observer.EnterWorldAsync();
        await owner.EnterWorldAsync();
        owner.SendRpc("80b9f0030000000000000000000000000000000000000000000000000000000090767241086b0000000000");
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        owner.SendRpc("80b95a02000000000000000000000000000000000000000000000000585ca80001000000086b0000000000");
        var first = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        byte previousId = first.Bytes[3];
        var prior = await OwnerPetAsync(previousId);
        Check.Equal((byte)22, prior.Attributes["Level"][0], "602 clamps the original Healer at level 22");

        int from = owner.World.RpcSnapshot().Count;
        owner.SkipReceived();
        owner.SendChat("?pet cast 612");
        var cast = await owner.WaitForRpcAsync("80 ac 64 02");
        Check.Equal(8f, BitConverter.ToSingle(cast.Bytes, cast.Bytes.Length - 4), "612 has an eight-second cast bar");
        Check.That(!owner.World.RpcSnapshot().Skip(from).Any(frame => frame.Opcode is 0x176 or 0x177),
            "The old Healer stays through cast startup");
        var fresh = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
        byte newId = fresh.Bytes[3];
        var healer = await OwnerPetAsync(newId);
        await owner.WaitForRpcAsync("80 bd 01 64 02 04 00 00 20 41 01");
        Check.That(context.Server.DebugLog().Contains($"[ABILITY-COST] char={owner.CharId} ability=612 paid=120"),
            "612 debits the stored 120 IS");
        var windows = owner.World.RpcSnapshot().Skip(from).Where(frame => frame.Opcode is 0x176 or 0x177)
            .Select(frame => (frame.Opcode, Id: frame.Bytes[frame.Opcode == 0x176 ? 3 : 2])).ToArray();
        Check.That(windows.SequenceEqual(new[] { (0x177, previousId), (0x176, newId) }) && previousId != newId,
            "The cast replaces only the previous simulacrum");
        Check.Equal("Healer Simulacrum", healer.Name, "612 chooses the Healer sheet");
        Check.Equal((byte)23, healer.Attributes["Level"][0], "612 uses the correct level-23 Healer");
        Check.Equal((ushort)937, BitConverter.ToUInt16(healer.Attributes["MaxHealth"]), "Modeled level-23 pet maximum HP");

        int healthAt = owner.World.ViewSnapshot().Count, friendAt = observer.World.ViewSnapshot().Count;
        owner.SendChat("?health 100");
        await owner.WaitUntilAsync(() => Heals().Length >= 3, "612 heals its injured owner twice", TimeSpan.FromSeconds(10));
        var healed = Heals().Take(3).ToArray();
        Check.That(healed.Zip(healed.Skip(1), (before, after) => after - before).All(delta => delta is >= 8 and <= 11),
            "Healing matches the current same-level NPC roll model");
        Check.That(!PvpOpponentsFixture.HealthHistory(observer.World.ViewSnapshot(), friendAt, 2, self: true).Any(),
            "The nearby wounded friend gets no pet heal");

        owner.SendRpc("81 7a fe 01");
        await owner.WaitForRpcAsync($"81 77 {newId:x2}");
        int afterDismissal = owner.World.ViewSnapshot().Count;
        await Task.Delay(TimeSpan.FromSeconds(CoderPets.AttackPeriodSeconds + 0.5));
        Check.That(!PvpOpponentsFixture.HealthHistory(owner.World.ViewSnapshot(), afterDismissal, 2, self: true).Any(),
            "Heals stop after dismissal");

        ushort[] Heals() => PvpOpponentsFixture.HealthHistory(owner.World.ViewSnapshot(), healthAt, 2, self: true)
            .SkipWhile(hp => hp != 100).Distinct().ToArray();

        async Task<ViewCreation> OwnerPetAsync(byte id)
        {
            ViewCreation? result = null;
            await owner.WaitUntilAsync(() => (result = owner.World.CreationSnapshot().FirstOrDefault(view =>
                view.Goid == CoderPets.SimulacraGoId && view.Attributes["SimulacraID"][0] == id &&
                BitConverter.ToUInt32(view.Attributes["OwnerCharacterID"]) == owner.CharId)) != null,
                $"Owned Healer {id} reaches the world", HeadlessPlayer.DefaultTimeout);
            return result!;
        }
    }
}
