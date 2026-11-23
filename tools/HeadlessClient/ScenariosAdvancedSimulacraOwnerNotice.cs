using hds;
using static HeadlessClient.AdvancedSimulacraNoticeFixture;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AdvancedSimulacraOwnerNotice(ScenarioContext context)
    {
        var seed = new CharacterSeed("AdvancedSummonOwner")
        {
            X = X, Y = Y, Z = Z, Level = Level, Experience = (int)PlayerProgression.ExperienceForLevel(Level),
            Health = 500, MaxHealth = 500, InnerStrength = 1000, MaxInnerStrength = 1000, Organization = 1,
            OwnedLoadedAbilities = Abilities.Select(ability => (ability, Level)).ToArray()
        };
        Check.That(Abilities.Sum(AbilityMemory.Cost) <= AbilityMemory.ForLevel(Level), "The full advanced profile fits memory");
        var owner = await context.AddPlayerAsync(seed);
        var observer = await context.AddPlayerAsync(seed with
        {
            Handle = "AdvancedSummonObserver", X = ObserverX, OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1) }
        });
        await observer.EnterWorldAsync();
        await owner.EnterWorldAsync();
        owner.SendRpc(CoderStyle);
        await owner.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");

        byte old = await SummonAsync(27, Proxy1, 4, null, 0);
        old = await SummonAsync(22, Nuker1, 7, "Nuker Simulacrum", old);
        old = await SummonAsync(602, Healer1, 22, "Healer Simulacrum", old);
        old = await SummonAsync(Proxy, Proxy5, Level, null, old);

        // Use the real cast and ordinary Archive unload, with a generated dev cast
        // input here until this rank's complete stock activation is retained.
        int failedStart = owner.World.RpcSnapshot().Count;
        owner.SkipReceived();
        owner.SendChat("?pet cast 616");
        await owner.WaitForRpcAsync("80 ac 68 02");
        owner.SendChat("?qaunload 616");
        await owner.WaitForRpcAsync("80 b3 68 02");
        await Task.Delay(TimeSpan.FromSeconds(8.5));
        Check.That(!owner.World.RpcSnapshot().Skip(failedStart).Any(frame => frame.Opcode is 0x176 or 0x177 || IsNotice(frame)),
            "An unloaded replacement neither publishes a notice/window nor removes the previous proxy");

        owner.SendChat("?qaload 616");
        await owner.WaitForRpcAsync("80 b2 68 02");
        // The original failed operation may have started the normal 10 s reuse after its cast bar.
        await Task.Delay(TimeSpan.FromSeconds(10));
        byte nuker = await SummonAsync(Nuker, null, Level, "Nuker Simulacrum", old);
        byte healer = await SummonAsync(Healer, null, Level, "Healer Simulacrum", nuker);
        byte[] healerReuse = AbilityCooldowns.BuildTimer(Healer, AbilityCooldowns.OwnTimer, 10, start: true);
        await owner.WaitUntilAsync(() => owner.World.RpcSnapshot().Any(frame => frame.Bytes.AsSpan().SequenceEqual(healerReuse)),
            "Patch Daemon 3.0 has 10 s own reuse", HeadlessPlayer.DefaultTimeout);
        Check.That(context.Server.DebugLog().Contains($"[ABILITY-COST] char={owner.CharId} ability=617 paid=170"),
            "Patch Daemon 3.0 uses its stored 170 IS");
        int injuredAt = context.Server.DebugLog().Length;
        owner.SendChat("?health 100");
        observer.SendChat("?health 100");
        await observer.WaitUntilAsync(() => RefusalOwnValue(observer, "Health", "02 80 80 80 40") == 100,
            "The observer is wounded before validating the recipient restriction", HeadlessPlayer.DefaultTimeout);
        string ownerPulse = $"[VITALS] char={owner.CharId} pet={healer} ability=617 HP ";
        string observerPulse = $"[VITALS] char={observer.CharId} pet={healer} ability=617 HP ";
        await owner.WaitUntilAsync(() => context.Server.DebugLog()[injuredAt..].Split(ownerPulse).Length >= 3,
            "Patch Daemon 3.0 heals its wounded owner twice", TimeSpan.FromSeconds(10));
        Check.That(!context.Server.DebugLog()[injuredAt..].Contains(observerPulse) &&
            RefusalOwnValue(observer, "Health", "02 80 80 80 40") == 100, "The equally wounded observer is excluded");

        owner.SendRpc(Deactivate);
        await owner.WaitForRpcAsync($"81 77 {healer:x2}");
        int dismissedAt = context.Server.DebugLog().Length;
        await Task.Delay(TimeSpan.FromSeconds(CoderPets.AttackPeriodSeconds + 0.5));
        Check.That(!context.Server.DebugLog()[dismissedAt..].Contains(ownerPulse), "The dismissed Healer stops producing health ticks");

        observer.SkipReceived();
        observer.SendRpc("81 48");
        await observer.WaitForRpcAsync("81 49");
        Check.Equal(3, owner.World.RpcSnapshot().Count(IsNotice), "Exactly the three successful advanced casts notify the owner");
        Check.That(!observer.World.RpcSnapshot().Any(frame => IsNotice(frame) || frame.Opcode == 0x176),
            "An observer sees pet views but receives neither a private notice nor a Simulacra window");
        Check.Equal(0, owner.World.Problems.Count + observer.World.Problems.Count, "Summon and notice packets decode cleanly");
        Console.WriteLine("ADVANCED SUMMON OWNER NOTICE SERVER PASS: three lower roles, three private notices, failed replacement and the Healer lifetime.");

        async Task<byte> SummonAsync(ushort ability, string? exactRequest, ushort expectedLevel, string? expectedName, byte previous)
        {
            int initial = owner.World.RpcSnapshot().Count;
            owner.SkipReceived();
            if (exactRequest == null) owner.SendChat($"?pet cast {ability}");
            else owner.SendRpc(exactRequest);
            string id = WireFormat.ToHex(BitConverter.GetBytes(ability));
            var cast = await owner.WaitForRpcAsync($"80 ac {id}");
            Check.Equal(8f, BitConverter.ToSingle(cast.Bytes.AsSpan(^4)), "Eight-second summon cast");
            var window = await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(14));
            byte key = window.Bytes[3];
            if (previous != 0)
            {
                var activity = owner.World.RpcSnapshot().Skip(initial).Where(frame => frame.Opcode is 0x176 or 0x177).ToArray();
                Check.That(activity.Length == 2 && activity[0].Opcode == 0x177 && activity[0].Bytes[2] == previous &&
                    activity[1].Opcode == 0x176 && key != previous, "Replacement removes the exact prior window and publishes once");
            }
            var pet = await PetViewAsync(owner, owner.CharId, key);
            var observedPet = await PetViewAsync(observer, owner.CharId, key);
            Check.Equal(pet.Name, observedPet.Name, "Owner and observer see the same summoned name");
            Check.Equal((byte)expectedLevel, pet.Attributes["Level"][0], "Shipped summon level cap");
            if (ability == Healer)
                Check.Equal((ushort)1725, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Modeled level-44 Healer maximum health");
            Check.That(expectedName == null ? CoderPets.ProxySheets.Any(sheet => pet.Name == $"{sheet} Simulacrum")
                : pet.Name == expectedName, "The shipped summon sheet is reflected in the view");
            if (ability is Proxy or Nuker or Healer)
            {
                string text = $"You summoned a level {expectedLevel} {pet.Name}.";
                await owner.WaitForTextAsync(text);
                Check.Equal(1, owner.World.RpcSnapshot().Skip(initial).Count(IsNotice), "Exactly one owner notice per advanced summon");
            }
            else
                Check.That(!owner.World.RpcSnapshot().Skip(initial).Any(IsNotice), "The three lower-role controls keep their existing output");
            return key;
        }
    }
}
