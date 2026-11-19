namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task FreeAttackBootstrapReplay(ScenarioContext context)
    {
        var shooter = await context.AddPlayerAsync(NearNpc("RefreshShooter") with
        {
            Items = new[] { (11, 692u, 1) }
        });
        var selected = await context.AddPlayerAsync(NearNpc("RefreshSelected") with
        {
            Items = new[] { (11, 698u, 1) }
        });
        var held = await context.AddPlayerAsync(NearNpc("RefreshHeld") with
        {
            Items = new[] { (1, 2399u, 1), (105, 2565u, 1) }
        });
        string rows = $"SELECT invId,charId,slot,goid,count,purity FROM inventory WHERE charId IN ({shooter.CharId},{selected.CharId},{held.CharId}) ORDER BY invId";
        string before = await context.Database.ScalarAsync(rows);
        var slotData = WireFormat.FromHex("0b b4 02 00 00 00 00 00 0c");
        Check.That(BytePattern.IndexOf(shooter.CharacterData[5], slotData, 0) >= 0, "Margin includes the original owned rifle");

        // Stock initialization can report fists before world entry. The UI refresh waits for 0c 01.
        shooter.SendRpc("3f 6b 00 00 00 00 00");
        await shooter.EnterWorldAsync(loadingComplete: false);
        Check.Equal(0, shooter.Received("5f 0b 00 01").Count, "No refresh before loading-complete");
        shooter.SendRpc("0c 01");
        await shooter.WaitForRpcAsync("5f 0b 00 01");
        await shooter.WaitForRpcAsync("5e 0b 00 b4 02 00 00 00 00 00 10 01");

        // A genuine selection is client-owned. Supply the stock reply to exercise the next server stage.
        shooter.SendRpc("3f 0b 00 10 00 00 00");
        var dummy = await shooter.WaitForViewNamedAsync("WinterFrost");
        string handle = HandleOf(dummy);
        shooter.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {handle}"),
            WireFormat.FromHex($"40 {handle} 00 0b"));
        await shooter.WaitForRpcAsync("81 68");
        await shooter.WaitUntilAsync(() => context.Server.DebugLog().Contains("Free Fire weapon: slot 11, kind 16, goid 692") &&
            context.Server.DebugLog().Contains("Close combat request ignored: firing at this target"),
            "The selected rifle fires and its companion 40 stays ranged", HeadlessPlayer.DefaultTimeout);

        // Repeated initialization or an explicit clear cannot reissue the refresh.
        shooter.SendRpc("3d 00");
        shooter.SendRpc("3f 6b 00 00 00 00 00");
        shooter.SendRpc("0c 01");
        shooter.SendRpc("3d 01");
        await shooter.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 02 21");
        Check.Equal(1, shooter.Received("5e 0b 00 b4 02 00 00 00 00 00 10 01").Count, "Only one refresh per client session");

        selected.SendRpc("3f 0b 00 10 00 00 00");
        selected.SendRpc("3f 6b 00 00 00 00 00");
        await selected.EnterWorldAsync();
        selected.SendRpc("3d 01");
        await selected.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 02 21");
        Check.Equal(0, selected.Received("5e").Count + selected.Received("5f").Count, "An existing choice prevents the refresh");

        await held.EnterWorldAsync();
        var own = held.World.CreationSnapshot().Last(view => view.ViewId == 2);
        Check.Equal(2565u, BitConverter.ToUInt32(own.Attributes["EquippedItemID"]), "The revolver begins in the hand slot");
        held.SendRpc("3f 6b 00 00 00 00 00");
        await held.WaitForRpcAsync("5f 69 00 01");
        await held.WaitForRpcAsync("5e 02 00 05 0a 00 00 00 00 00 10 01");
        await held.WaitForRpcAsync("65 02 00 69 00 00");
        held.SendRpc("3f 02 00 10 00 00 00");
        held.SendRpc("3f 69 00 10 00 00 00");
        var heldTarget = await held.WaitForViewNamedAsync("WinterFrost");
        string heldHandle = HandleOf(heldTarget);
        held.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {heldHandle}"),
            WireFormat.FromHex($"40 {heldHandle} 00 69"));
        await held.WaitForRpcAsync("81 68");
        await held.WaitUntilAsync(() => context.Server.DebugLog().Contains("Free Fire weapon: slot 105, kind 16, goid 2565"),
            "The held weapon selection resolves the persisted revolver", HeadlessPlayer.DefaultTimeout);
        Check.Equal(before, await context.Database.ScalarAsync(rows), "Inventory row identity, owner, slot, GOID and metadata persist");
        Check.Equal(0, shooter.World.Problems.Count + selected.World.Problems.Count + held.World.Problems.Count,
            "No protocol errors in the three client sessions");
    }

    private static async Task FreeAttackEquipReplay(ScenarioContext context)
    {
        var player = await context.AddPlayerAsync(NearNpc("RefreshEquip") with
        {
            Items = new[] { (11, 692u, 1), (105, 2565u, 1) }
        });
        string rifle = $"FROM inventory WHERE charId={player.CharId} AND goid=692";
        long id = await context.Database.LongAsync($"SELECT invId {rifle} AND slot=11");
        player.SendRpc("3f 6b 00 00 00 00 00");
        await player.EnterWorldAsync();
        player.SendRpc("3d 01");
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 02 21");
        Check.Equal(0, player.Received("5e").Count + player.Received("5f").Count, "Equip experiment leaves the login inventory alone");
        player.SendRpc("3d 00");
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 02 22");
        player.SendRpc("63 0b 69");
        await player.WaitForRpcAsync("5f 0b 00 01");
        await player.WaitForRpcAsync("5e 0b 00 b4 02 00 00 00 00 00 10 01");
        await player.WaitForRpcAsync("65 69 00 01 00 00");
        await player.WaitForRpcAsync("65 0b 00 69 00 00");
        await player.WaitForViewUpdateAsync(2, "02 80 80 80 80 08 b4 02 00 00");
        Check.Equal(id, await context.Database.LongAsync($"SELECT invId {rifle} AND slot=105"),
            "The same persisted rifle moves from the bag into the hand");
        Check.Equal(2L, await context.Database.LongAsync($"SELECT COUNT(*) FROM inventory WHERE charId={player.CharId}"),
            "Equip introduces no inventory rows");

        player.SendRpc("3f 69 00 10 00 00 00");
        var target = await player.WaitForViewNamedAsync("WinterFrost");
        string handle = HandleOf(target);
        player.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {handle}"),
            WireFormat.FromHex($"40 {handle} 00 69"));
        await player.WaitForRpcAsync("81 68");
        await player.WaitUntilAsync(() => context.Server.DebugLog().Contains("Free Fire weapon: slot 105, kind 16, goid 692"),
            "The move-follow selection resolves the same rifle", HeadlessPlayer.DefaultTimeout);
    }
}
