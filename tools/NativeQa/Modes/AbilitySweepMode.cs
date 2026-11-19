using HeadlessClient;
using System.Text.Json;

namespace NativeQa.Modes;

public abstract class AbilitySweepMode : NativeQaMode
{
    private const int CasterX = 24800;
    private const int CasterZ = -1498;
    private const int GroundY = 495;
    private const int FriendZ = -1298;
    private const string MeleeDummy = "Melee Dummy";

    protected sealed record Sample(ushort Id, string Ability, string Target, string Health, string State,
        string Duration, ushort Cost)
    {
        public string Describe(int index) =>
            $"Slot {index + 1} | {Ability} ({Id}) | {Target} | HP {Health} | {State} | {Duration} | {Cost} IS";
    }

    // Slot order is the manual native hotbar checklist, independent of the margin archive slot numbers.
    protected AbilitySweepMode(Sample[] samples, ushort[] support, bool enemyHelper = false,
        bool firearms = false, bool hackerTargets = false, bool isolatedMelee = false)
    {
        if (samples.Length != 8 || samples.Select(sample => sample.Id).Distinct().Count() != samples.Length)
            throw new ArgumentException("A sweep requires eight distinct representative actions.", nameof(samples));

        Evidence = samples.Select((sample, index) => sample.Describe(index)).ToArray();
        EnemyHelper = enemyHelper;
        Firearms = firearms;
        IsolatedMelee = isolatedMelee;
        HoldHelperWound = hackerTargets;
        EnemyX = CasterX + (hackerTargets ? 200 : 800);
        var environment = new Dictionary<string, string>();
        if (enemyHelper)
        {
            environment["MXO_PVP_SERVER"] = "1";
            environment["MXO_PVP_MAX_SAFE_LEVEL"] = "15";
        }
        if (hackerTargets)
            environment["MXO_DESPERATION_QA_HOLD_HEALTH"] = "1";
        var setup = new List<string>
        {
            $"UPDATE characters SET y={GroundY},healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000 WHERE charId=110",
            $"UPDATE characters SET x={CasterX},y={GroundY},z={FriendZ},healthC=500,healthM=5000 WHERE charId=6111",
            $"UPDATE characters SET x={EnemyX},y={GroundY},z={CasterZ},healthC=5000,healthM=5000 WHERE charId=6114"
        };
        if (firearms)
        {
            setup.Add("DELETE FROM inventory WHERE charId=110 AND slot IN (10,11,12,105)");
            setup.Add("INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,537,10,1,0),(110,692,11,1,0),(110,698,12,1,0)");
        }

        var instructions = new List<string>
        {
            "Drag the eight visible Actions to an empty hotbar page in the order below. Use normal activation and record one result per slot.",
            "Save the complete outgoing stock activation RPC and actual HP/IS, target, state and expiry per slot. See docs/ABILITY-SWEEPS.md."
        };
        instructions.Add(isolatedMelee
            ? "The isolated 30,000 HP Melee Dummy is 3 m west. Wait for each queued special to resolve before pressing the next one."
            : "Confirm the target's name in the HUD. WinterFrost is west, Trainman northwest and Blackwoods southwest of the documented start.");
        if (hackerTargets)
        {
            instructions.Add("For Repair RSI, target QAFriend at 500/5000 HP. The disposable Hacker fixture holds natural HP regeneration so the wound survives hotbar setup.");
            instructions.Add("For UI Lag, stand at the fixture start 2 m west of flagged QAEnemy. UI Lag checks 3 m around you.");
            instructions.Add("For Stun 2.0, select WinterFrost. Check Trainman at 3 m and Blackwoods at 6 m from that anchor.");
        }
        if (firearms)
            instructions.Add("Equip the matching weapon before each style or special. Revolver bag 10, Automatic Rifle bag 11, SMG bag 12.");
        Plan = new NativeQaModePlan(samples.Select(sample => sample.Id).Concat(support).Distinct().ToArray(),
            CasterX, CasterZ, Array.Empty<string>(), Array.Empty<string>(), setup.ToArray(), DevCommands: hackerTargets,
            environmentOverrides: environment,
            extraQaMetadata: new() { ["AbilitySweepNativeStatus"] = "Pending", ["AbilitySweepSlots"] = samples.Length },
            readyMessages: instructions.Concat(Evidence).ToArray(),
            testMobFixture: isolatedMelee ? "ability-sweep-melee.csv" : "ability-sweep-mobs.csv");
    }

    public override NativeQaModePlan Plan { get; }
    private string[] Evidence { get; }
    private bool EnemyHelper { get; }
    private bool Firearms { get; }
    private bool IsolatedMelee { get; }
    private bool HoldHelperWound { get; }
    private int EnemyX { get; }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("An owned fixture database is required.");
        var server = context.ActualServer ?? throw new InvalidOperationException("An owned fixture server is required.");
        var friend = await EnterHelperAsync(context, "QAFriend");
        var friendView = AssertPosition(friend, CasterX, FriendZ);
        var friendHp = BitConverter.ToUInt16(friendView.Attributes["Health"]);
        var friendMaxHp = BitConverter.ToUInt16(friendView.Attributes["MaxHealth"]);
        if (HoldHelperWound)
        {
            Check.Equal((ushort)500, friendHp, "QAFriend entered wounded");
            Check.Equal((ushort)5000, friendMaxHp, "QAFriend remains eligible for Repair RSI");
        }
        double[]? enemyPosition = null;
        if (EnemyHelper)
        {
            var enemy = await EnterHelperAsync(context, "QAEnemy");
            enemyPosition = EngageFoesFixture.Position(AssertPosition(enemy, EnemyX, CasterZ));
            await enemy.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 01 00 00 00 01");
            await friend.WaitForViewNamedAsync("QAEnemy");
        }

        var loaded = AbilityMemoryUiFixture.Decode(friend.CharacterData[6]);
        var csr = (await database.ExecuteAsync("SELECT slot,ability_id FROM char_abilities WHERE char_id=110 AND is_loaded=1 ORDER BY slot"))
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var (id, index) in Plan.Abilities.Select((id, index) => (id, index)))
        {
            uint descriptor = 0x80000000u | ((uint)id << 10);
            Check.That(loaded.Any(row => row.Slot == index && row.Descriptor == descriptor && row.Level > 0),
                $"Ordinary margin loadout includes action {id} in archive slot {index}");
            Check.That(csr.Contains($"{index}\t{descriptor}", StringComparer.Ordinal),
                $"CSR has the same seeded action {id} in archive slot {index}");
        }

        if (Firearms)
        {
            Check.Equal(3L, await database.LongAsync(
                "SELECT COUNT(*) FROM inventory WHERE charId=110 AND (slot,goid) IN ((10,537),(11,692),(12,698)) AND count=1"),
                "CSR has the three sourced weapons in their documented bag slots");
        }

        var targets = new List<object>();
        var expectedTargets = IsolatedMelee
            ? new[] { (MeleeDummy, CasterZ) }
            : new[] { ("WinterFrost", CasterZ), ("Trainman", -1198), ("Blackwoods", -2098) };
        foreach (var (name, z) in expectedTargets)
        {
            var npc = await friend.WaitForViewNamedAsync(name);
            var position = EngageFoesFixture.Position(npc);
            Check.That(npc.Goid == 599 && npc.Attributes.TryGetValue("Health", out var hp) &&
                hp.Length == 2 && BitConverter.ToUInt16(hp) > 0, $"{name} is a living NPC");
            Check.That(position.SequenceEqual(new double[] { 24499, GroundY, z }),
                $"{name} is at its documented, distinct cast target position");
            ushort maxHp = BitConverter.ToUInt16(npc.Attributes["MaxHealth"]);
            if (IsolatedMelee)
                Check.Equal((ushort)30000, maxHp, "The melee dummy is durable across repeated specials");
            targets.Add(new { Name = name, npc.ViewId, npc.SpawnId, Position = position, MaxHp = maxHp });
        }
        if (IsolatedMelee)
            Check.That(friend.World.CreationSnapshot().Where(view => view.Goid == 599).All(view => view.Name == MeleeDummy),
                "The melee dummy has no nearby NPCs to intercept picks");
        foreach (var helper in context.Helpers.Values)
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "ability-sweep.json"),
            JsonSerializer.Serialize(new
            {
                Mode = Name, NativeStatus = "Pending", Slots = Evidence, Loaded = loaded,
                NpcTargets = targets, EnemyPosition = enemyPosition,
                Friend = new { Health = friendHp, MaxHp = friendMaxHp, NaturalHealthRegenHeld = HoldHelperWound },
                Helpers = context.Helpers.Keys, BootstrapGameplayRequests = new[] { "05", "0c 01" }
            }, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }

    private static ViewCreation AssertPosition(HeadlessPlayer player, int x, int z)
    {
        var view = player.World.CreationSnapshot().Last(creation => creation.ViewId == 2);
        var position = EngageFoesFixture.Position(view);
        Check.That(position.SequenceEqual(new double[] { x, GroundY, z }),
            $"{player.Handle} entered at the intended target position");
        return view;
    }

    private static async Task<HeadlessPlayer> EnterHelperAsync(NativeQaBootstrapContext context, string name)
    {
        var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
        context.Helpers.Add(name, helper);
        // Both records are documented stock world entry requests in docs/CLIENT-ACTIONS.md.
        await helper.EnterWorldAsync();
        return helper;
    }
}

public sealed class AbilitySweepAwakenedMode : AbilitySweepMode
{
    public override string Name => "ability-sweep-awakened";
    public AbilitySweepAwakenedMode() : base(new Sample[]
    {
        new(1, "Adrenaline Booster", "self", "0", "ballistic resistance +75 at level 50", "20 s", 20),
        new(5, "Calm Mind, Calm Body", "self", "0", "thrown resistance +75 at level 50", "20 s", 20),
        new(6, "Consistent Technique", "self", "0", "melee resistance +235 (capture)", "20 s", 20),
        new(10, "Hyper-Dodge", "self", "0", "ballistic defense +51 (capture)", "while enabled", 20),
        new(184, "Hyper-Sprint", "self", "0", "movement scale 0.15 (capture)", "while enabled; drain policy pending", 25),
        new(11, "Hyper-Jump", "nearby outdoor ground", "0", "aim and jump; look for one clock echo", "one jump", 20),
        new(12, "Hyper-Speed", "self", "0", "movement scale 0.45 (capture)", "600 s", 40),
        new(198, "Cheap Shot", "WinterFrost in Self Defense interlock", "21–67 captured base damage",
            "Cheap Shot choreography 285", "one round", 20)
    }, new ushort[] { 2, 17, 1099, 1101 }) { }
}

public sealed class AbilitySweepHackerMode : AbilitySweepMode
{
    public override string Name => "ability-sweep-hacker";
    public AbilitySweepHackerMode() : base(new Sample[]
    {
        new(38, "Area Disruption", "WinterFrost near caster", "0", "Stun 102 on hit; immunity input 8 s", "3 s input", 14),
        new(57, "Logic Barrage 1.0", "WinterFrost", "8 stored viral base; final range unresolved", "viral hit or deflect", "instant", 5),
        new(77, "Repair RSI 1.0", "QAFriend", "9 base healing; final amount depends on healing influence", "direct heal", "instant", 10),
        new(97, "UI Lag 1.0", "hostiles within 3 m of caster", "0 (capture)", "viral/thrown offense and defense debuff", "27 s", 12),
        new(314, "Upgrade Attacks", "self", "0", "viral accuracy +108 (capture)", "20 s", 35),
        new(333, "Stun 2.0", "hostiles within 4 m of selected WinterFrost", "0 (capture)", "Stun on hit", "5 s capture", 24),
        new(383, "Force Enhancer", "self", "0", "ballistic damage +6, max IS -317, ballistic defense +21 and accuracy +46 (capture)",
            "30 s", 0),
        new(72, "Resist Combat", "self", "0", "combat evasion stat 520 +20 (capture)", "600 s", 40)
    }, new ushort[] { 2, 17, 1006, 40, 43, 45, 50, 52, 55, 56, 58, 59, 62, 71, 74, 75, 76, 80, 83, 92, 95, 98, 170, 309, 310, 311, 312, 327, 328, 330, 376, 378, 379 }, enemyHelper: true, hackerTargets: true) { }
}

public sealed class AbilitySweepCoderMode : AbilitySweepMode
{
    public override string Name => "ability-sweep-coder";
    public AbilitySweepCoderMode() : base(new Sample[]
    {
        new(27, "Remote Proxy 1.0", "self", "0 immediate; attack model damage unresolved",
            "own level 4 Proxy appears in Simulacra window; test follow and assist", "until dismissal, replacement or expiry", 50),
        new(20, "Fortify Simulacra 1.0", "own Proxy", "+750 maximum HP at level 50",
            "four resistance scores +100 each", "600 s", 30),
        new(25, "Power Boost 1.0", "own Proxy out of combat within 10 m", "0 immediate",
            "Proxy melee and ballistic damage influence +25 at level 50", "300 s", 50),
        new(603, "Charge Simulacra", "own Proxy", "0", "IS cost -20%, regeneration +50%", "600 s", 35),
        new(589, "Proxy Technician", "self with live Proxy", "0", "aura 698 on pet; 5 IS per 5 s", "5 s per pulse", 15),
        new(22, "Logic Daemon 1.0", "self", "0 immediate; attack model damage unresolved",
            "own level 7 Logic Daemon replaces Proxy; test follow and assist", "until dismissal, replacement or expiry", 50),
        new(33, "Static Blast 1.0", "WinterFrost", "22 stored viral base; final range unresolved", "viral hit or deflect", "instant", 15),
        new(34, "Detonate Proxy", "own simulacrum near WinterFrost", "NPC blast clamps pet HP to max HP / 10 through max HP / 4",
            "NPC blast within 8 m; simulacrum receives 13000 damage", "instant blast / 0.5 s pet damage", 30)
    }, new ushort[] { 2, 17, 18, 23, 26, 28, 29, 31, 32, 35, 36, 37, 585, 586, 588, 600, 602, 1008 }) { }
}

public sealed class AbilitySweepOperativeMode : AbilitySweepMode
{
    public override string Name => "ability-sweep-operative";
    public AbilitySweepOperativeMode() : base(new Sample[]
    {
        new(135, "Martial Arts Prowess", "self", "0", "melee damage stat +40 (capture)", "20 s", 20),
        new(123, "Find Weakness", "self", "0", "thrown damage influence +25 at level 50", "20 s", 20),
        new(149, "Sixth Sense", "self; QAEnemy 8 m east", "0", "hostile count and map blips; verify discovery and scan visual", "10 s display", 10),
        new(146, "Poison Knife", "WinterFrost before interlock", "17 stored total DOT input", "ThrownDOT", "25 s input", 15),
        new(341, "Sever Artery", "WinterFrost before interlock", "68 stored total DOT input",
            "slow and Force Combat debuff; conditional combat-state callbacks where enabled", "30 s input", 35),
        new(647, "Taunt", "WinterFrost nearby", "0", "NPC turns on caster; capture aggro attribution", "instant", 10),
        new(281, "Wounding Throw", "WinterFrost in Spy Style", "61 stored total DOT input", "ThrownDOT", "50 s input", 20),
        new(282, "Blinding Throw", "WinterFrost in Spy Style", "24 stored total DOT input; +30 on qualifying Off-Balance",
            "Blind, plus ThrownDOT", "10 s Blind / 15 s DOT inputs", 25)
    }, new ushort[] { 2, 17, 113, 116, 118, 120, 121, 130, 131, 141, 142, 143, 151, 152, 153, 155, 164, 175, 185, 278, 279, 280, 283, 284, 288, 291, 292, 339, 1112, 1125 }, enemyHelper: true) { }
}

public sealed class AbilitySweepMartialArtistMode : AbilitySweepMode
{
    public override string Name => "ability-sweep-martial-artist";
    public AbilitySweepMartialArtistMode() : base(new Sample[]
    {
        new(132, "Karate", "self", "0", "Karate style; melee accuracy +64 at level 50", "while active style", 0),
        new(267, "Sky-High Sidekick", "Melee Dummy in Karate interlock", "21 stored physical base",
            "choreography 5937, ballistic/melee resistance -40", "12 s debuff input", 30),
        new(133, "Kung-Fu", "self", "0", "Kung-Fu style; melee accuracy +74 at level 50", "while active style", 0),
        new(260, "Misdirect Punch", "Melee Dummy in Kung-Fu interlock", "24 stored physical base",
            "choreography 8082, Blind", "4 s input", 30),
        new(262, "Piston Kicks", "Melee Dummy in Kung-Fu interlock", "108 stored physical base",
            "choreography 8085; Stun on proc (stored 40%)", "4 s input", 25),
        new(428, "Suicidal Butterfly", "Melee Dummy in Kung-Fu interlock", "25 stored physical base",
            "choreography 8084; Confuse on proc (stored 30%)", "4 s input", 35),
        new(430, "Wooden Dummy Drill", "Melee Dummy in Kung-Fu interlock", "107 stored physical base",
            "choreography 8080; Powerless", "6 s input", 30),
        new(412, "Tomo Nage", "Melee Dummy in Aikido interlock", "113 stored physical base",
            "choreography 742; Enraged and ballistic/melee resistance -100", "6 s Enraged / 12 s debuff inputs", 45)
    }, new ushort[] { 2, 17, 101, 105, 113, 116, 124, 130, 135, 136, 137, 138, 139, 143, 145, 154, 185, 257, 258, 259, 264, 265, 269, 270, 271, 272, 405, 407, 429, 431 }, isolatedMelee: true) { }
}

public sealed class AbilitySweepGunmanMode : AbilitySweepMode
{
    public override string Name => "ability-sweep-gunman";
    public AbilitySweepGunmanMode() : base(new Sample[]
    {
        new(129, "Handguns", "self with Revolver", "0", "Handguns style; ballistic accuracy +64 at level 50", "while active style", 0),
        new(126, "Disarming Shot", "Melee Dummy in Handguns interlock", "11 stored ballistic base",
            "choreography 9522; ballistic accuracy -15", "10 s debuff input", 20),
        new(147, "Rifles", "self with rifle", "0", "Rifles style; ballistic accuracy +64 at level 50", "while active style", 0),
        new(254, "Immobilizing Shot", "Melee Dummy in Rifles interlock", "18 stored ballistic base",
            "choreography 11361, Rooted with break chance, Force Combat -15", "8 s input", 30),
        new(158, "Submachine Guns", "self with SMG", "0", "Submachine Guns style; ballistic accuracy +64 at level 50", "while active style", 0),
        new(176, "Covering Fire", "Melee Dummy in SMG interlock", "17 stored ballistic base",
            "choreography 21083; Rooted with break chance", "8 s input", 25),
        new(243, "Pistol Aerial", "Melee Dummy in Handguns interlock", "16 stored ballistic base", "choreography 9518; Stun", "4 s input", 20),
        new(255, "Disabling Shot", "Melee Dummy in Rifles interlock", "16 stored ballistic base",
            "choreography 11360; Powerless on proc (stored 50%)", "4 s input", 20)
    }, new ushort[] { 2, 17, 105, 113, 116, 124, 125, 128, 130, 135, 143, 145, 154, 185, 239, 252, 253 }, firearms: true, isolatedMelee: true) { }
}
