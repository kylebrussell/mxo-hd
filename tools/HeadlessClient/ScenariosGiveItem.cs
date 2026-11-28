namespace HeadlessClient;

public static partial class Scenarios
{
    public const uint GiveItemFixtureMission = 399903, GiveItemFixtureDisk = 6831;
    public const int GiveStandX = 17000, GiveStandY = 495, GiveStandZ = 6400;
    private const ushort MovedDiskSlot = 6;

    private static async Task GiveMissionItemByDragging(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("Giver") { Level = 50, Organization = 3, X = GiveStandX, Y = GiveStandY, Z = GiveStandZ },
            new CharacterSeed("MenuGiver") { Level = 50, Organization = 3, X = GiveStandX, Y = GiveStandY, Z = GiveStandZ });
        await ProveGiveItemAsync(players[0], context.Database);
        await ProveGiveItemAsync(players[1], context.Database, menu: true);
    }

    /// <summary>Take the disk from Courier and deliver the actual owned bag slot to Receiver.</summary>
    public static async Task ProveGiveItemAsync(HeadlessPlayer player, ThrowawayDatabase db, bool menu = false)
    {
        string disk = $"FROM inventory WHERE charId={player.CharId} AND goid={GiveItemFixtureDisk}";
        Task<long> DiskCount() => db.LongAsync($"SELECT COUNT(*) {disk}");
        Task<long> DiskSlot() => db.LongAsync($"SELECT slot {disk}");
        Task<long> Receipts() => db.LongAsync($"SELECT COUNT(*) FROM char_missions WHERE charId={player.CharId} AND missionId={GiveItemFixtureMission}");

        player.SendRpc("80 94 05 00 03");
        await player.WaitForTextAsync("Give Item Fixture");
        await player.WaitForRpcAsync("80 97 05 00");
        player.SendRpc("80 98 06 00 00 00 01 02 00");
        await player.WaitForRpcAsync("80 99");
        int beforeAccept = player.World.CreationSnapshot().Count;
        player.SendRpc("80 9b 07 00 01");
        await player.WaitForRpcAsync("80 9c");

        ViewCreation? courier = null, receiver = null;
        await player.WaitUntilAsync(() =>
        {
            var newViews = player.World.CreationSnapshot().Skip(beforeAccept).ToArray();
            courier = newViews.LastOrDefault(view => view.Name == "Courier");
            receiver = newViews.LastOrDefault(view => view.Name == "Receiver");
            return courier != null && receiver != null;
        }, "the newly accepted mission's Courier and Receiver", TimeSpan.FromSeconds(15));
        var courierId = MissionNpcId(courier!);
        var receiverId = MissionNpcId(receiver!);
        player.SendRpc($"80 c7 {courierId} 0d 00");
        await player.WaitForRpcAsync("80 a0 01 06 00 01");
        Check.Equal(1L, await DiskCount(), "Get grants exactly one tracked disk");
        ushort source = checked((ushort)await DiskSlot());

        async Task Refused(ushort slot, string target, ushort expectedSlot)
        {
            player.SendRpc($"66 {slot:x2} {target}");
            await player.WaitForTextAsync("They don't want that.");
            Check.Equal((long)expectedSlot, await DiskSlot(), "Refusal keeps the tracked disk in its current slot");
            Check.Equal(0L, await Receipts(), "Refusal does not complete the mission");
        }

        if (menu)
            player.SendRpc($"80 c7 {receiverId} 0e 00");
        else
        {
            Check.That(source != MovedDiskSlot, "The disk arrives before the reserved empty destination");
            await Refused(source, courierId, source);
            await Refused(MovedDiskSlot, receiverId, source);
            string staleReceiver = $"{Le(receiver!.ViewId)} {Le((ushort)(receiver.SpawnId ^ 1))}";
            await Refused(source, staleReceiver, source);
            player.SendRpc($"65 {Le(source)} {Le(MovedDiskSlot)} 00");
            await player.WaitForRpcAsync($"65 {Le(source)} {Le(MovedDiskSlot)} 00");
            await Refused(source, receiverId, MovedDiskSlot);
            player.SendRpc($"66 {MovedDiskSlot:x2} {receiverId}");
            source = MovedDiskSlot;
        }
        await player.WaitForRpcAsync($"5f {Le(source)} 01");
        await player.WaitForTextAsync("Give item fixture completed.", TimeSpan.FromSeconds(15));
        Check.Equal(0L, await DiskCount(), "Give consumes the requested disk");
        Check.Equal(1L, await Receipts(), "Give awards exactly one receipt");
    }
}
