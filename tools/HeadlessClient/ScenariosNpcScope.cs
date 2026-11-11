namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AmbientNpcScope(ScenarioContext context)
    {
        HeadlessPlayer encounter = (await context.AddPlayersInWorldAsync(new CharacterSeed("EncounterScope")
        { DistrictId = 3, District = "it", X = 42662, Y = -1105, Z = -6400 }))[0];
        HeadlessPlayer interior = (await context.AddPlayersInWorldAsync(new CharacterSeed("PublicScope")
        { DistrictId = 2, District = "dt", X = -29092, Y = -17705, Z = -6015 }))[0];
        await interior.WaitForViewNamedAsync("Succubus", TimeSpan.FromSeconds(20));
        // A real visibility pass has run in both districts; historical encounter arrivals are not ambient.
        await Task.Delay(2000);
        Check.That(encounter.World.CreationSnapshot().All(v => v.Name != "Special Agent" && v.Name != "Corrupted"),
            "The captured Special Agent arrival does not become a permanent street NPC");
        Check.That(interior.World.CreationSnapshot().Any(v => v.Name == "Succubus"),
            "Public interior hosts still load from the scoped catalog");
    }
}
