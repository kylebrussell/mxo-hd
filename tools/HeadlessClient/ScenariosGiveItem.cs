namespace HeadlessClient;

public static partial class Scenarios
{
    /// <summary>A mission item dragged onto the NPC that wants it (client 66) completes the Give goal like the menu's
    /// Give; dragged onto anyone else it stays in the bag.</summary>
    private static async Task GiveMissionItemByDragging(ScenarioContext context)
    {
        HeadlessPlayer player = (await context.AddPlayersInWorldAsync(new CharacterSeed("Giver") { Level = 50 }))[0];
        player.SendRpc("80 94 05 00 03");
        await player.WaitForTextAsync("Give Item Fixture");
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9c");
        await player.WaitForRpcAsync("80 9e 00");
        await EnterMissionArea(context, player);

        ViewCreation courier = await player.WaitForViewNamedAsync("Courier");
        ViewCreation receiver = await player.WaitForViewNamedAsync("Receiver");
        player.SendRpc($"80 c7 {MissionNpcId(courier)} 0d 00");
        await WaitForDatabaseAsync(context, $"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6831", "1", "The disk in the bag");
        long slot = await context.Database.LongAsync($"SELECT slot FROM inventory WHERE charId={player.CharId} AND goid=6831");

        // Onto the wrong NPC: refused, the disk stays
        player.SendRpc($"66 {slot:x2} {Le(courier.ViewId)} {courier.SpawnId:x2} 00");
        await player.WaitForTextAsync("They don't want that.");

        // Onto the receiver: the goal is done and the disk goes
        player.SendRpc($"66 {slot:x2} {Le(receiver.ViewId)} {receiver.SpawnId:x2} 00");
        await WaitForDatabaseAsync(context, $"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId} AND goid=6831", "0", "The disk handed over");
        await player.WaitForTextAsync("Give item fixture completed.", TimeSpan.FromSeconds(15));
    }
}
