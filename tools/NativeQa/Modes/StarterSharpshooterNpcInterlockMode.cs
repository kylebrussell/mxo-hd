using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterSharpshooterNpcInterlockMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    private const ushort Sharpshooter = 148;
    private const string ReturnToStand = "?tp 17000 495 2300 393 11 3 - 40";
    // Full stock Self Defense selection before the close 40 from the gun-special admission batch.
    private const string StockOpen = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
    private const string StockSpeed = "42 00 00 00 00 00 00";
    // PR797 native PacketLog 1546 and 1905, the SMG and first handgun interlocks' authoritative positions.
    private const float SmgX = 16930.091796875f;
    private const float DisplacedX = 16864.998046875f, DisplacedZ = 2302.291015625f;
    private static readonly ushort[] Loadout = new ushort[] { 2, 17, Sharpshooter }
        .SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id)).Distinct().Order().ToArray();

    public override string Name => "ability-parity-starter-sharpshooter-npc-interlock";
    public override NativeQaModePlan Plan { get; } = new(Loadout, X, Z, Array.Empty<string>(), new[]
    {
        StarterHackerPassiveFixture.CapRanks,
        $"UPDATE char_abilities SET is_loaded=0 WHERE char_id=110 AND ability_id={AbilityMessages.AbilityGameObject(Sharpshooter)}"
    }, new[]
    {
        CombatFixtureLoadout.SeedWeapons,
        $"UPDATE characters SET level={Level},exp={PlayerProgression.ExperienceForLevel(Level)},y={Y},rotation=64," +
            "healthC=20000,healthM=20000 WHERE charId=110"
    }, DevCommands: true, hotbarAbilities: new ushort[] { 17 }, testMobFixture: "starter-soldier-resistance.csv",
        environmentOverrides: new()
        {
            ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0",
            [CombatFixtureWeapons.WeaponMode] = "1", [CombatFixtureWeapons.DisengageMode] = "1"
        }, extraQaMetadata: new() { ["ScopedStockRows"] = "148", ["NativeCloseableRows"] = 0 },
        readyMessages: new[]
        {
            $"Handgun step only. Level {Level}, CSR ({X},{Y},{Z}) faces the unarmed 30,000 HP Melee Dummy ({DummyX},{Y},{Z}) on verified outdoor NAV, two metres apart. Sharpshooter 148 starts in Archive. Use ?cd w smg then ?cd w handgun and wait for the equipped handgun 2565 message.",
            $"Type {ReturnToStand} and wait for the camera to settle. If needed, briefly tap strafe and reselect Melee Dummy. Stay within 3 m. Choose Speed, keep Self Defense (page 2 slot 1), and right-click Close Combat on Melee Dummy. Double-click starts free fire.",
            "Wait for a regular opposed round after the opener. Expect [SHARPSHOOTER NPC INTERLOCK] class=56 loaded148=False modifier=-5, attack 19 to 14 and defence 18 to 13. Keep 148 archived and use no specials or Withdraw. Rifle and SMG already passed native QA."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach ((float x, float z) in new[] { ((float)X, (float)Z), ((float)DummyX, (float)Z), (SmgX, Z), (DisplacedX, DisplacedZ), ((float)X, Z + 10f) })
            Check.That(NavMesh.Locate(1, x, Y, z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Verified outdoor NPC interlock stand ({x},{Y},{z})");
        Check.That(NavMesh.Locate(1, X, Y, Z, out var home) && (home.sector, home.region, home.polygon) == (0x393, 0x11, 3),
            "The printed return command has the original street sector, region and polygon");
        Check.That(NavMesh.HasSightPath(1, X, Y, Z, DummyX, Y, Z), "The ordinary NPC target is in clear sight");

        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        var server = context.ActualServer!;
        var archive = StarterHackerPassiveFixture.VerifyProfile(actor, Loadout.Where(id => id != Sharpshooter).ToArray(), Sharpshooter);
        await CombatFixtureLoadout.VerifyWeaponAtLoginAsync(actor, CombatFixtureWeapons.Handgun);
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        var target = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        bool unarmed = !target.Attributes.TryGetValue("EquippedItemID", out var equipment) || BitConverter.ToUInt32(equipment) == 0;
        Check.That(target.Goid == 599 && unarmed &&
            BitConverter.ToUInt16(target.Attributes["MaxHealth"]) == 30000, "The live 30,000 HP NPC starts without a weapon");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);

        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, "rifle", CombatFixtureWeapons.Rifle);
        int start = await OpenAsync(X, Z);
        await ExpectAsync(start, CombatFixtureWeapons.Rifle, false, -15);
        start = server.DebugLog().Length;
        await ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: true);
        await ExpectAsync(start, CombatFixtureWeapons.Rifle, true, 0);
        start = server.DebugLog().Length;
        await ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: false);
        await ExpectAsync(start, CombatFixtureWeapons.Rifle, false, -15);

        await StopAsync();
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, "smg", CombatFixtureWeapons.Smg);
        MoveTo(SmgX, Z);
        start = await OpenAsync(SmgX, Z);
        await ExpectAsync(start, CombatFixtureWeapons.Smg, false, -10);
        await StopAsync();
        await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, "handgun", CombatFixtureWeapons.Handgun);

        MoveTo(DisplacedX, DisplacedZ);
        start = await OpenAsync(DisplacedX, DisplacedZ);
        await actor.WaitUntilAsync(() =>
        {
            string rounds = server.DebugLog()[start..];
            return Regex.Matches(rounds, @"\[COMBAT-NPC\] action Melee Dummy").Count >= 2 &&
                Regex.IsMatch(rounds, @"\[COMBAT\] Round [2-9]: Speed");
        }, "An ordinary Speed round and NPC counter at the native displaced stand", TimeSpan.FromSeconds(18));
        Check.That(!server.DebugLog()[start..].Contains("[SHARPSHOOTER NPC INTERLOCK]", StringComparison.Ordinal),
            "The equipped handgun round at 3.35 m stays outside the short-range rule");
        Console.WriteLine("Native displacement reproduced: ordinary handgun exchange 3.35 m from the dummy, outside the 3 m rule.");
        await StopAsync();

        actor.SendChat(ReturnToStand);
        await actor.WaitForTextAsync("Teleported to 393/11");
        MoveTo(X, Z + 10);
        start = await OpenAsync(X, Z + 10);
        await ExpectAsync(start, CombatFixtureWeapons.Handgun, false, -5);
        await StopAsync();
        Check.Equal(0, actor.World.Problems.Count, "NPC interlock and archive framing");
        return Array.Empty<byte>();

        void MoveTo(float x, float z)
        {
            string position = WireFormat.ToHex(new[] { x, Y, z }.SelectMany(BitConverter.GetBytes).ToArray());
            actor.World.SendViewList(WireFormat.FromHex($"02 00 01 08 {position} 00 00"));
        }

        async Task<int> OpenAsync(float x, float z)
        {
            actor.SkipReceived();
            string targetHex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            string centre = WireFormat.ToHex(new[] { (double)x, Y, z }.SelectMany(BitConverter.GetBytes).ToArray());
            int cursor = server.DebugLog().Length;
            int views = actor.World.ViewSnapshot().Count;
            actor.World.SendRpcs(WireFormat.FromHex(StockSpeed), StarterHackerControlTargetMode.WithHandle(StockOpen, handle),
                WireFormat.FromHex($"40 {targetHex} 01 6b"));
            await actor.WaitForRpcAsync("4e 01");
            await actor.WaitForRpcAsync($"40 {targetHex} 00 6b");
            await actor.WaitForRpcAsync("47");
            await EvidenceRetestWire.ViewSinceAsync(actor, views, 1,
                $"02 2e 00 ?? ?? 01 {centre} 01 00 00 00 03 00 00 00 02 00 ?? ?? {targetHex} 00");
            return cursor;
        }

        async Task StopAsync()
        {
            actor.SkipReceived();
            actor.SendChat("?cd stop");
            await actor.WaitForRpcAsync($"46 {WireFormat.ToHex(BitConverter.GetBytes(handle))} 00");
            await actor.WaitForTextAsync("Fixture combat ended. Wait for the stock combat HUD to clear.");
        }

        async Task ExpectAsync(int cursor, uint weapon, bool loaded, int penalty)
        {
            byte weaponClass = RangedWeapons.AttackClass(weapon);
            var pattern = new Regex($@"\[SHARPSHOOTER NPC INTERLOCK\] CSR_Tester class={weaponClass} loaded148={loaded} modifier={penalty} " +
                @"attack=(-?\d+)->(-?\d+) defence=(-?\d+)->(-?\d+) opponents=1", RegexOptions.CultureInvariant);
            Match? witness = null;
            await actor.WaitUntilAsync(() => (witness = pattern.Match(server.DebugLog()[cursor..])).Success,
                $"ordinary NPC exchange with weapon class {weaponClass}, 148 loaded={loaded} and modifier {penalty}", TimeSpan.FromSeconds(18));
            int[] inputs = witness!.Groups.Cast<Group>().Skip(1).Select(group => int.Parse(group.Value)).ToArray();
            Check.Equal(penalty, inputs[1] - inputs[0], "The player attack contest receives the modifier");
            Check.Equal(penalty, inputs[3] - inputs[2], "The NPC attack contest receives the player's modified defence");
            Console.WriteLine(witness.Value);
        }
    }
}
