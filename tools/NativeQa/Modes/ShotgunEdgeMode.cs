using hds;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class ShotgunEdgeMode : NativeQaMode
{
    private const int X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    private const uint Shotgun = 696, Rifle = 692;

    public override string Name => "shotgun-gun-vs-fists";
    public override NativeQaModePlan Plan { get; } = new(new ushort[] { 2, 17 }, X, Z, Array.Empty<string>(), new[] { """
        UPDATE characters SET level=19,y=495,rotation=64,healthC=20000,healthM=20000 WHERE charId=110;
        DELETE FROM inventory WHERE charId=110 AND slot IN (3,4,105);
        INSERT INTO inventory(charId,goid,slot,count,purity) VALUES (110,692,3,1,0),(110,696,4,1,0);
        UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id=2147635200;
        """ }, Array.Empty<string>(),
        DevCommands: true, testMobFixture: "starter-soldier-resistance.csv",
        environmentOverrides: new() {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0", [CombatFixtureWeapons.DisengageMode] = "1"
        },
        readyMessages: new[] {
            "CSR (17000,495,2300) faces the unarmed Melee Dummy (17200,495,2300). Both stands are outdoor NAV. Sharpshooter 148 is unloaded. Bag 3 holds the rifle and bag 4 the shotgun. No teleport or stat overrides.",
            "Double-click bag 3 and pick the rifle in Free Attack. Select Melee Dummy directly ahead. Use the Free Fire button. Expect [SHARPSHOOTER OURS] class55 defenderGunFalse, metres <=3, opponents1, loaded148False, modifier-15. Hit/miss is ordinary.",
            "Type ?cd stop and wait for the combat HUD to clear. Double-click bag 4 and select the shotgun explicitly in Free Attack. Fire at the same dummy from within 3 m. Expect class55, the same one-opponent inputs, modifier0. Use ?cd stop when finished."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string data = Path.Combine(context.RepositoryRoot!, "data");
        SectorProperties.Load(Path.Combine(data, "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(data, "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && stand.floorY + 95 == Y,
                $"Outdoor NPC stand at ({x},{Y},{Z})");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "NPC is visible from the printed stand");
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        await actor.EnterWorldAsync();
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        ViewCreation dummy = await actor.WaitForViewNamedAsync("Melee Dummy");
        Check.Equal((ushort)30000, BitConverter.ToUInt16(dummy.Attributes["MaxHealth"]), "The live Melee Dummy is in view");
        string handle = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(dummy.ViewId, dummy.SpawnId)));
        await Fire(3, Rifle, -15);
        await Fire(4, Shotgun, 0);
        Check.Equal(0, actor.World.Problems.Count, "Both stock shots decode");
        return Array.Empty<byte>();

        async Task Fire(int bag, uint weapon, int expected)
        {
            actor.SkipReceived();
            actor.SendRpc($"63 {bag:x2} 69");
            await actor.WaitForRpcAsync($"65 {bag:x2} 00 69 00 00");
            actor.SendRpc("3f 69 00 10 00 00 00");
            int cursor = server.DebugLog().Length;
            actor.World.SendRpcs(WireFormat.FromHex("3d 01"), WireFormat.FromHex($"41 {handle}"),
                WireFormat.FromHex($"40 {handle} 00 69"));
            await actor.WaitForRpcAsync("4e 00");
            var shotLine = await actor.WaitForRpcAsync("81 68");
            Check.Equal(weapon, BitConverter.ToUInt32(shotLine.Bytes, 23), "The selected gun fired");
            actor.SendChat("?cd stop");
            await actor.WaitForTextAsync("Fixture combat ended.");
            var inputs = server.DebugLog()[cursor..].Split('\n')
                .Select(line => SharpshooterFixture.ReadInput(line.TrimEnd('\r'), actor.Handle))
                .OfType<SharpshooterFixture.Input>().ToArray();
            Check.That(inputs.Length > 0 && inputs.All(input =>
                input.Class == 55 && !input.DefenderGun && input.Opponents == 1 && !input.Loaded && input.Modifier == expected),
                $"Actual gun {weapon} has the expected {expected} modifier at the single unarmed NPC");
        }
    }
}
