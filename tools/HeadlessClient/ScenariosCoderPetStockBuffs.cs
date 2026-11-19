namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CoderPetStockBuffs(ScenarioContext context)
    {
        // Stock 7.6005 Coder native sweep. Source lines and direction are recorded in CODER-PETS.md.
        const string summon = "80 b9 1b 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00";
        const string fortify = "80 b9 14 00 0e 00 0d 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00";
        const string power = "80 b9 19 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00";
        const string charge = "80 b9 5b 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 ff ff 7f 7f 00 00 00 00 08 6b 00 00 00 00 00";
        var owner = await context.AddPlayerAsync(NearNpc("StockPetOwner") with
        {
            Level = 50,
            InnerStrength = 1000,
            MaxInnerStrength = 1000,
            OwnedLoadedAbilities = new[] { ((ushort)27, (ushort)50), ((ushort)20, (ushort)50),
                ((ushort)25, (ushort)50), ((ushort)603, (ushort)50) }
        });
        await owner.EnterWorldAsync();
        owner.SendRpc(summon);
        await owner.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(18));
        await owner.WaitUntilAsync(() => owner.World.CreationSnapshot().Any(v => v.Goid == 40376),
            "current stock-summoned Proxy view", TimeSpan.FromSeconds(8));
        var pet = owner.World.CreationSnapshot().Single(v => v.Goid == 40376);
        Check.Equal((ushort)224, BitConverter.ToUInt16(pet.Attributes["MaxHealth"]), "Stock Remote Proxy 1.0 level-four starting HP");

        // Only replace the two shorts of the captured target handle with the receiving player's live view/spawn.
        byte[] selected = WireFormat.FromHex(fortify);
        BitConverter.GetBytes(pet.ViewId).CopyTo(selected, 4);
        BitConverter.GetBytes(checked((ushort)pet.SpawnId)).CopyTo(selected, 6);
        owner.SendRpc(selected);
        await owner.WaitForRpcAsync("81 78 01 00 e0 00 ce 03 03 00 04 00 00", TimeSpan.FromSeconds(8));
        await owner.WaitForRpcAsync("80 bd 01 14 00 04 00 00 00 41 01");

        owner.SendRpc(power);
        await owner.WaitForRpcAsync("80 bd 01 19 00 04 00 00 00 41 01", TimeSpan.FromSeconds(10));
        owner.SendRpc(charge);
        await owner.WaitForRpcAsync("80 bd 01 5b 02 04 00 00 f0 41 01", TimeSpan.FromSeconds(10));
        string debug = context.Server.DebugLog();
        Check.That(debug.Contains("[PET] Fortify Simulacra (20) pet=1 maxHP=974 resistance=100 duration=600"), "Fortify lands on the explicit stock handle");
        Check.That(debug.Contains("[PET] Power Boost (25) pet=1 melee=25 ballistic=25 duration=300"), "Power Boost lands on the implicit own pet");
        Check.That(debug.Contains("[PET] Charge Simulacra (603) pet=1 costReduction=20% regenIncrease=50% duration=600"), "Charge lands on the same pet");
    }
}
