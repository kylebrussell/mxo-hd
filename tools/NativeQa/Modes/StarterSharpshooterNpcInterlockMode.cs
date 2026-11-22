using System.Text.RegularExpressions;
using hds;
using HeadlessClient;
using static NativeQa.StockAbilityProfile;

namespace NativeQa.Modes;

public sealed class StarterSharpshooterNpcInterlockMode : NativeQaMode
{
    private const int Level = 19, X = 17000, Y = 495, Z = 2300, DummyX = 17200;
    private const ushort Sharpshooter = 148;
    // Full stock Self Defense selection before the close 40 from the gun-special admission batch.
    private const string StockOpen = "80b9110005000300000000000000000000000000000000000000000060070d1e03000000086b0000000000";
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
            $"Level {Level}, CSR ({X},{Y},{Z}) faces the unarmed 30,000 HP Melee Dummy ({DummyX},{Y},{Z}) on verified outdoor NAV. Sharpshooter 148 starts in Archive. Type ?cd w rifle, keep Self Defense and the same tactic, then right-click Close Combat on the dummy. Double-click starts free fire.",
            "Wait for a regular opposed round after the opener. Expect [SHARPSHOOTER NPC INTERLOCK] loaded148=False modifier=-15. The attack and defence input deltas should both be -15. Type ?qaload 148 in the same fight and expect both deltas 0. Type ?qaunload 148 and expect -15/-15 again.",
            "Type ?cd stop and wait for the combat HUD to clear. Use ?cd w smg, then right-click Close Combat on the same dummy and expect -10/-10 when 148 is archived. Repeat after ?cd stop with ?cd w handgun and expect -5/-5. Ordinary close opposed rounds only, with no queued moves, withdrawals, or PvP rounds."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        foreach (int x in new[] { X, DummyX })
            Check.That(NavMesh.Locate(1, x, Y, Z, out var stand) && !stand.indoors && Math.Abs(stand.floorY + 95 - Y) < 1,
                $"Verified outdoor NPC interlock stand ({x},{Y},{Z})");
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
        int start = await OpenAsync();
        await ExpectAsync(start, CombatFixtureWeapons.Rifle, false, -15);
        start = server.DebugLog().Length;
        await ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: true);
        await ExpectAsync(start, CombatFixtureWeapons.Rifle, true, 0);
        start = server.DebugLog().Length;
        await ChangeLoadoutAsync(actor, archive, new[] { Sharpshooter }, load: false);
        await ExpectAsync(start, CombatFixtureWeapons.Rifle, false, -15);

        foreach (var (name, weapon, penalty) in new[] { ("smg", CombatFixtureWeapons.Smg, -10), ("handgun", CombatFixtureWeapons.Handgun, -5) })
        {
            await StopAsync();
            await CombatFixtureLoadout.VerifyWeaponSwapAsync(actor, context.OwnedDatabase!, name, weapon);
            start = await OpenAsync();
            await ExpectAsync(start, weapon, false, penalty);
        }
        await StopAsync();
        Check.Equal(0, actor.World.Problems.Count, "NPC interlock and archive framing");
        return Array.Empty<byte>();

        async Task<int> OpenAsync()
        {
            actor.SkipReceived();
            string targetHex = WireFormat.ToHex(BitConverter.GetBytes(handle));
            int cursor = server.DebugLog().Length;
            actor.World.SendRpcs(StarterHackerControlTargetMode.WithHandle(StockOpen, handle),
                WireFormat.FromHex($"40 {targetHex} 01 6b"));
            await actor.WaitForRpcAsync($"40 {targetHex} 00 6b");
            await actor.WaitForRpcAsync("47");
            return cursor;
        }

        async Task StopAsync()
        {
            actor.SkipReceived();
            actor.SendChat("?cd stop");
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
