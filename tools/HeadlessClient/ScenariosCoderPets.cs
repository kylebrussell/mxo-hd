namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>
    /// Coder simulacra (docs/CODER-PETS.md): Logic Daemon 4.0 summons an owned NPC_BASE_SIMULACRA that a second player
    /// sees too; the client's 81 7c / 81 7a orders change its mode and dismiss it; Drain Code destroys a resummoned one.
    /// </summary>
    private static async Task CoderPetLifecycle(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(NearNpc("PetOwner") with { Level = 50 }, NearNpc("PetWatcher", 100));
        var owner = players[0]; var watcher = players[1];

        Cast(owner, 616); // Logic Daemon 4.0: 8 s cast, then the summon
        var pet = await owner.WaitForViewNamedAsync("Nuker Simulacrum", TimeSpan.FromSeconds(20));
        Check.Equal((ushort)40376, pet.Goid, "The simulacrum is an NPC_BASE_SIMULACRA view");
        Check.Equal(owner.CharId, BitConverter.ToUInt32(pet.Attributes["OwnerCharacterID"]), "Owned by the caster");
        Check.Equal(owner.CharId, BitConverter.ToUInt32(pet.Attributes["MasterCharacterID"]), "Master is the caster");
        Check.Equal((byte)50, pet.Attributes["Level"][0], "Logic Daemon 4.0 clamps level 50 to 35-50");
        Check.Equal((ushort)1950, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Retail level 50 simulacrum health");
        var seen = await watcher.WaitForViewNamedAsync("Nuker Simulacrum", TimeSpan.FromSeconds(10));
        Check.Equal(owner.CharId, BitConverter.ToUInt32(seen.Attributes["OwnerCharacterID"]), "Others see whose simulacrum it is");

        owner.SendRpc("81 7c fe 05 02"); // Simulacra window: Passive
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("Passive");
        owner.SendRpc("81 7a fe 00"); // Stay
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("Stay");
        owner.SendRpc("81 7a fe 01"); // Deactivate
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("No simulacrum");

        owner.SendChat("?pet summon 27");
        await owner.WaitForTextAsync("is next to you");
        Cast(owner, 19); // Drain Code: the simulacrum is consumed
        await owner.WaitForTextAsync("Your simulacrum has been destroyed", TimeSpan.FromSeconds(15));
        owner.SendChat("?pet");
        await owner.WaitForTextAsync("No simulacrum");
        Cast(owner, 34); // Detonate Proxy without a simulacrum is refused
        await owner.WaitForTextAsync("You need an active simulacrum");
        Check.Equal(0, owner.World.Problems.Count, "Owner protocol problems");
        Check.Equal(0, watcher.World.Problems.Count, "Watcher protocol problems");
    }
}
