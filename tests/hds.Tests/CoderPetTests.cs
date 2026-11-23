using hds;
using hds.shared;

namespace hds.Tests;

public class CoderPetTests
{
    [Fact]
    public void SummonKeepsTheStreetOffsetAndUsesTheSameRoomWhenTheOffsetIsBlocked()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv")))
            root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        Assert.Equal((17150d, 495d, 2300d), CoderPets.SummonStand(1, 17000, 495, 2300));
        Assert.Equal((17150d, 497d, 2300d), CoderPets.SummonStand(ushort.MaxValue, 17000, 497, 2300));

        Assert.True(NavMesh.Locate(1, 41010, 1695, 12280, out var owner));
        Assert.True(owner.indoors);
        Assert.False(NavMesh.Locate(1, 41160, 1695, 12280, out _));
        var pet = CoderPets.SummonStand(1, 41010, 1695, 12280);
        Assert.Equal(41116.07, pet.X, precision: 2);
        Assert.Equal(1695, pet.Y);
        Assert.Equal(12386.07, pet.Z, precision: 2);
        Assert.True(NavMesh.WalkableLine(1, owner, 41010, 12280, pet.X, pet.Z, out var reached));
        Assert.Equal((owner.sector, owner.region, owner.floorY), (reached.sector, reached.region, reached.floorY));
    }

    [Fact]
    public void ChargeSelectionUsesTheScriptedRangeAndPercentDecrease()
    {
        var owner = Owner(43);
        owner.playerInstance.Position.setValue(NumericalUtils.doublesToLtVector3d(100, 495, 300));
        var mob = new Mob();
        mob.setYPos(495);
        mob.setZPos(300);
        var pet = new CoderPets.Pet { Owner = owner, Mob = mob };
        mob.setXPos(5100);
        Assert.True(CoderPets.InAbilityRange(pet, CoderPets.ChargeSimulacra));
        mob.setXPos(5101);
        Assert.False(CoderPets.InAbilityRange(pet, CoderPets.ChargeSimulacra));

        var effects = TimedEffects.ForPlayer(owner).Effects;
        effects.Apply(214, new[] { new TimedEffects.Active
        {
            Proc = TempMods.Proc.Stat, Target = 214, Value = 80, Seconds = 60
        } }, TimeUtils.GetSimTimeSeconds());
        mob.setXPos(1100);
        Assert.True(CoderPets.InAbilityRange(pet, CoderPets.ChargeSimulacra));
        mob.setXPos(1101);
        Assert.False(CoderPets.InAbilityRange(pet, CoderPets.ChargeSimulacra));
        mob.setXPos(1700);
        Assert.True(CoderPets.InAbilityRange(pet, CoderPets.FortifySimulacra));
        mob.setXPos(1701);
        Assert.False(CoderPets.InAbilityRange(pet, CoderPets.FortifySimulacra));
    }

    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();

    private static WorldClient Owner(uint charId, byte level = 50)
    {
        var client = (WorldClient)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue(level);
        return client;
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("Rifle", null)]
    [InlineData("Simulacra.Healer", null)]
    [InlineData("Simulacra.Nuker", null)]
    [InlineData("Simulacra.Aikido", "Aikido")]
    [InlineData("Simulacra.Karate", "Karate")]
    [InlineData("Simulacra.KungFu", "KungFu")]
    [InlineData("Simulacra.Pistol", "Pistol")]
    [InlineData(" simulacra.rifle ", "Rifle")]
    [InlineData("Simulacra.SMG", "SMG")]
    public void ConfiguredProxySheetUsesOnlyAuthoredProxyRoles(string? configured, string? expected) =>
        Assert.Equal(expected, CoderPets.NormalizeProxySheetOverride(configured));

    [Fact]
    public void SummonLevelsFollowSimulacraScript()
    {
        // simulacra.py *_Subject: sim_clamp(min, max, subject.Level)
        Assert.Equal((1, 4), (CoderPets.Summons[27].MinLevel, CoderPets.Summons[27].MaxLevel));
        Assert.Equal((35, 50), (CoderPets.Summons[594].MinLevel, CoderPets.Summons[594].MaxLevel));
        Assert.Equal((4, 7), (CoderPets.Summons[22].MinLevel, CoderPets.Summons[22].MaxLevel));
        Assert.Equal((44, 50), (CoderPets.Summons[617].MinLevel, CoderPets.Summons[617].MaxLevel));
        Assert.Equal(CoderPets.Kind.Healer, CoderPets.Summons[602].Kind);
        Assert.Equal(CoderPets.Kind.Healer, CoderPets.Summons[612].Kind);
        Assert.Equal(23, CoderPets.SimLevel(CoderPets.Summons[612], 22));
        Assert.Equal(29, CoderPets.SimLevel(CoderPets.Summons[612], 29));
        Assert.Equal(43, CoderPets.SimLevel(CoderPets.Summons[612], 50));
        Assert.Equal(CoderPets.Kind.Nuker, CoderPets.Summons[616].Kind);
        Assert.Equal(7, CoderPets.SimLevel(CoderPets.Summons[22], 50));
        Assert.Equal(4, CoderPets.SimLevel(CoderPets.Summons[22], 1));
        Assert.Equal(12, CoderPets.SimLevel(CoderPets.Summons[601], 12));
        Assert.Equal(12, CoderPets.Summons.Count);
    }

    [Fact]
    public void HealthMatchesRetailSpawns()
    {
        // Retail NPC_BASE_SIMULACRA spawns: L7 Nuker 337, L50 Nuker/Aikido 1950
        Assert.Equal(337, CoderPets.MaxHealthAt(7));
        Assert.Equal(1950, CoderPets.MaxHealthAt(50));
        Assert.InRange(CoderPets.MaxHealthAt(25), 337, 1950);
    }

    [Theory]
    [InlineData(27, 725, 234)]
    [InlineData(50, 1300, 418)]
    public void AbsorbUsesTheIndependentStoredOwnerAndPetHealthEffects(int level, int hit, ushort heal)
    {
        Assert.Equal((hit, heal), CoderPets.AbsorbValues(level));
        var ability = AbilityCatalog.Find(CoderPets.AbsorbSimulacra);
        Assert.Equal(11, ability.Number("Ability/CastValidTarget"));
        Assert.Equal(15, ability.Number("Ability/SelectionRange"));
        Assert.Equal(2, ability.CastSeconds);
        Assert.Equal(25, AbilityCosts.Cost(CoderPets.AbsorbSimulacra));
        Assert.True(AbilityCooldowns.TryGet(CoderPets.AbsorbSimulacra, out var reuse));
        Assert.Equal(30, reuse.Seconds);
        Assert.Contains(CoderPets.AbsorbSimulacra, CoderPets.ContinuousLoadAbilities);
    }

    [Fact]
    public void DetonateValueFollowsTempmods()
    {
        // DetonateProxyProc: min(health, max/4), at least max/10
        Assert.Equal(487, CoderPets.DetonateValue(1950, 1950));
        Assert.Equal(300, CoderPets.DetonateValue(300, 1950));
        Assert.Equal(195, CoderPets.DetonateValue(20, 1950));
    }

    [Fact]
    public void DetonateReadsViralResistanceAtThePetsLevel()
    {
        var pet = new CoderPets.Pet { Owner = Owner(43), Mob = new Mob() };
        pet.Mob.setLevel(7);
        var target = new Mob();
        target.setName("Resistance target");
        target.setLevel(7);
        target.setHealthM(5000);
        target.setHealthC(5000);
        var recipient = TimedEffects.ForMob(target);
        try
        {
            int raw = CoderPets.DetonateValue(337, 337);
            Assert.Equal((ushort)84, CoderPets.DetonateDamage(pet, target, raw));
            TimedEffects.Apply(recipient, new TempMods.Effect { Ability = 6, Mods = new[] {
                new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = 180,
                    MinValue = 75, MaxValue = 75, Seconds = 30 } } }, TimeUtils.GetSimTimeSeconds());
            Assert.Equal((ushort)84, CoderPets.DetonateDamage(pet, target, raw));
            TimedEffects.Apply(recipient, new TempMods.Effect { Ability = 8, Mods = new[] {
                new TempMods.Mod { Kind = TempMods.KindStat, Proc = TempMods.Proc.Stat, Target = 157,
                    MinValue = 75, MaxValue = 75, Seconds = 30 } } }, TimeUtils.GetSimTimeSeconds());
            ushort reduced = CoderPets.DetonateDamage(pet, target, raw);
            Assert.Equal(TypedAbilityResistance.Amount(raw, 7, 7, 75), reduced);
            Assert.NotEqual(TypedAbilityResistance.Amount(raw, 50, 7, 75), reduced);
            Assert.InRange(reduced, (ushort)1, (ushort)83);
        }
        finally { TimedEffects.Clear(recipient); }
    }

    [Fact]
    public void OrdersParseAsClientBuildsThem()
    {
        // client.dll 620ef730 (81 7a, 4 bytes), 620ef832 (81 7b, 8 bytes), 620ef952 (81 7c, 5 bytes); window ID 0xfe
        Assert.True(CoderPets.TryParseOrder(0x17a, new byte[] { 0xfe, 0x03 }, out byte id, out CoderPets.Command cmd, out _, out _));
        Assert.Equal((0xfe, CoderPets.Command.Follow), (id, cmd));
        Assert.True(CoderPets.TryParseOrder(0x17b, new byte[] { 0xfe, 0x02, 0x2f, 0x00, 0x22, 0x00 }, out _, out cmd, out uint target, out _));
        Assert.Equal(CoderPets.Command.Attack, cmd);
        Assert.Equal(0x0022002fu, target);
        Assert.True(CoderPets.TryParseOrder(0x17c, new byte[] { 0xfe, 0x05, 0x02 }, out _, out cmd, out _, out byte value));
        Assert.Equal((CoderPets.Command.Mode, (byte)CoderPets.Stance.Passive), (cmd, value));
        Assert.False(CoderPets.TryParseOrder(0x17b, new byte[] { 0xfe, 0x02 }, out _, out _, out _, out _));
        Assert.Equal(0, (int)CoderPets.Command.Stay);
        Assert.Equal(1, (int)CoderPets.Command.Deactivate);
    }

    [Fact]
    public void AttackOrdersRequireTheExplicitVisibleHostileNpc()
    {
        var owner = Owner(43);
        owner.viewMan = new ViewManager();
        var pet = new CoderPets.Pet { Owner = owner, Mob = new Mob() };
        pet.Mob.setDistrict(1);
        var dummy = new Mob();
        dummy.setEntityId(0xABCDEF);
        dummy.setDistrict(1);
        var npcView = owner.viewMan.addView(dummy.getEntityId(), 599);
        npcView.spawnId = 0x22;
        npcView.viewCreated = true;
        var playerView = owner.viewMan.addView(0xABCDE0, 0);
        playerView.viewCreated = true;
        lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Add(dummy);
        try
        {
            Assert.Same(dummy, CoderPets.ResolveAttackTarget(pet, owner, npcView.ViewID, fromClient: true));
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, playerView.ViewID, fromClient: true));
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, 0, fromClient: true));
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, 2, fromClient: true));
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, 999, fromClient: true));
            npcView.viewCreated = false;
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, npcView.ViewID, fromClient: true));
            npcView.viewCreated = true;
            uint fullNetworkField = DuelPackets.Handle(npcView.ViewID, npcView.spawnId);
            Assert.Same(dummy, CoderPets.ResolveAttackTarget(pet, owner, fullNetworkField, fromClient: true));
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, DuelPackets.Handle(npcView.ViewID, 0x21), fromClient: true));
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, fullNetworkField | 0x01000000u, fromClient: true));
            npcView.viewNeedsToBeDeleted = true;
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, npcView.ViewID, fromClient: true));
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, fullNetworkField, fromClient: true));
            npcView.viewNeedsToBeDeleted = false;
            dummy.setZPos((CoderPets.LeashMetres + 1) * 100);
            Assert.Null(CoderPets.ResolveAttackTarget(pet, owner, npcView.ViewID, fromClient: true));
        }
        finally
        {
            lock (WorldServer.mobs.SyncRoot) WorldServer.mobs.Remove(dummy);
        }
    }

    [Theory]
    [InlineData(CoderPets.Command.Follow, false)]
    [InlineData(CoderPets.Command.Attack, true)]
    public void PassiveDropsAssistAndKeepsExplicitAttack(CoderPets.Command order, bool keepsTarget)
    {
        var pet = new CoderPets.Pet { Order = order, Target = new Mob() };
        CoderPets.SetStance(pet, CoderPets.Stance.Passive);
        Assert.Equal(CoderPets.Stance.Passive, pet.Stance);
        Assert.Equal(keepsTarget, pet.Target != null);
        Assert.Equal(order, pet.Order);
    }

    [Fact]
    public void OwnPetSelectionRequiresMatchingVisibleViewAndStockSpawn()
    {
        var owner = Owner(43);
        owner.viewMan = new ViewManager();
        var pet = new CoderPets.Pet { Owner = owner, Mob = new Mob() };
        pet.Mob.setEntityId(0xABCDEF);
        var selected = owner.viewMan.addView(pet.Mob.getEntityId(), 40376);
        selected.spawnId = 13;
        selected.viewCreated = true;
        var foreign = owner.viewMan.addView(0x123456, 40376);
        foreign.spawnId = 13;
        foreign.viewCreated = true;

        Assert.True(CoderPets.MatchesSelection(owner, pet, 0, null));
        Assert.True(CoderPets.MatchesSelection(owner, pet, selected.ViewID, 13));
        Assert.False(CoderPets.MatchesSelection(owner, pet, selected.ViewID, 12));
        Assert.False(CoderPets.MatchesSelection(owner, pet, foreign.ViewID, 13));
        selected.viewNeedsToBeDeleted = true;
        Assert.False(CoderPets.MatchesSelection(owner, pet, selected.ViewID, 13));
        selected.viewNeedsToBeDeleted = false;
        owner.viewMan.removeViewByViewId(selected.ViewID);
        Assert.False(CoderPets.MatchesSelection(owner, pet, selected.ViewID, 13));
    }

    [Fact]
    public void CreationMatchesRetailFreshSummon()
    {
        // MaraC capture line 78483: 0c b8 9d <spawn> cd ab, then 18 attributes of a level 7 Nuker Simulacrum
        const string retail =
            "128e4e756b65722053696d756c616372756d000000000000000000000000000000000010000022890102ef0000808bd20400580c060028" +
            "02c1000000d673a8d0400000000000f07e4000000020c926ab40038402d0075101e051010d080000a2060a000002ef00000401";
        var mob = new Mob();
        mob.setName("Nuker Simulacrum");
        mob.setLevel(7);
        mob.setHealthM(337);
        mob.setHealthC(337);
        mob.setRsiHex(CoderPets.WireHex(CoderPets.DescriptionOf(CoderPets.Kind.Nuker, 7, false)));
        mob.setWeapon(0x0a06);
        mob.setXPos(17057.809936523438);
        mob.setYPos(495.0);
        mob.setZPos(3475.392822265625);
        var owner = Owner(0xef02);
        owner.playerInstance.OrganizationID.setValue((byte)3);
        mob.pet = new CoderPets.Pet { Owner = owner, Mob = mob, SimulacraId = 2, Kind = CoderPets.Kind.Nuker };
        byte[] creation = mob.getCreationData().GetCreationAttributes();
        Assert.Equal(retail, Hex(creation));
        Assert.Equal(new byte[] { 0xb8, 0x9d }, mob.getCreationData().GetGoid());
    }

    [Fact]
    public void PresentationUpdateUsesSimulacrumOrganizationAndConditionIndices()
    {
        Assert.Equal("0480a002808080800200000001", Hex(CoderPets.BuildPresentationUpdate(2, PvpFlag.PvpBit)));
    }

    [Fact]
    public void DescriptionGoesOutInRetailByteOrder()
    {
        // Retail L50 Nuker spawn (Syntax capture line 515): Description bytes 13 05 00 58. The first native test sent
        // 58 00 05 13 and the client rendered nothing.
        Assert.Equal("13050058", CoderPets.WireHex(CoderPets.DescriptionOf(CoderPets.Kind.Nuker, 50, false)));
        Assert.Equal("b90100b4", CoderPets.WireHex(CoderPets.DescriptionOf(CoderPets.Kind.Proxy, 50, false)));
        var mob = new Mob();
        mob.setName("Nuker Simulacrum");
        mob.setLevel(35);
        mob.setRsiHex(CoderPets.WireHex(CoderPets.DescriptionOf(CoderPets.Kind.Nuker, 35, false)));
        mob.pet = new CoderPets.Pet { Owner = Owner(110), Mob = mob, SimulacraId = 1, Kind = CoderPets.Kind.Nuker };
        var go = (AttributeClass3828)mob.getCreationData();
        Assert.Equal("13050058", Hex(go.Description.getValue()));
    }

    [Fact]
    public void HealthUpdateSelectsMaxHealthAndHealth()
    {
        // NPC view update attributes 16 (MaxHealth) and 27 (Health)
        Assert.Equal("04808084" + "1e09" + "40" + "0a00", Hex(CoderPets.BuildHealthUpdate(10, 0x091e)));
    }

    [Fact]
    public void PetAbilitiesAreClaimed()
    {
        foreach (ushort id in new ushort[] { 27, 28, 29, 586, 594, 22, 601, 611, 616, 602, 612, 617, 20, 603, 34, 19, 609, 619, 30, 31, 587, 596 })
            Assert.True(CoderPets.Handles(id), id.ToString());
        Assert.False(CoderPets.Handles(26));
        Assert.True(CoderPets.TargetsOwnPet(34));
        Assert.False(CoderPets.TargetsOwnPet(27));
    }
    private static CoderPets.Pet ResourcePet()
    {
        var mob = new Mob(); mob.setName("Nuker Simulacrum"); mob.setLevel(50);
        mob.setHealthC(337); mob.setHealthM(1950);
        return new CoderPets.Pet { Mob = mob, SimulacraId = 2, Order = CoderPets.Command.Attack, Stance = CoderPets.Stance.Passive };
    }

    [Fact]
    public void WindowLayoutMatchesNativeConstructorAndConsumer()
    {
        var pet = ResourcePet();
        var registration = CoderPets.BuildWindow(pet, true);
        Assert.Equal(46, registration.Length);
        Assert.Equal("81760002", Hex(registration[..4]));
        Assert.Equal("Nuker Simulacrum", System.Text.Encoding.ASCII.GetString(registration,4,16));
        Assert.Equal("0051019e070202320000", Hex(registration[36..]));
        var update = CoderPets.BuildWindow(pet, false);
        Assert.Equal("8178020051019e070202320000", Hex(update));
    }

    [Fact]
    public void ChargeChangesActualBudgetAndExpiresWithoutRetroactiveRegeneration()
    {
        var pet = ResourcePet(); pet.InnerStrength = 8; pet.LastResourceTick = 10;
        Assert.False(CoderPets.SpendAction(pet, 10));
        pet.ChargeUntil = 12;
        Assert.True(CoderPets.SpendAction(pet, 10));
        Assert.Equal(0, pet.InnerStrength);
        CoderPets.Regenerate(pet, 13); // 2 charged seconds at3/s, one ordinary second at2/s
        Assert.Equal(8, pet.InnerStrength);
        Assert.False(CoderPets.SpendAction(pet, 13));
        CoderPets.Regenerate(pet, 1000);
        Assert.Equal(100, pet.InnerStrength);
    }

    [Fact]
    public void FortifyAndBoostChangeGameplayOnlyDuringTheirLifetime()
    {
        var pet = ResourcePet(); pet.FortifyResistance = 100; pet.FortifyUntil = 20;
        Assert.Equal(76, CoderPets.MitigateRetaliation(pet, 50, 100, 19));
        Assert.Equal(100, CoderPets.MitigateRetaliation(pet, 50, 100, 20));
        Assert.Equal(75, CoderPets.HitChance(pet, 50, 10));
        pet.BoostUntil = 15; pet.BoostAccuracy = 65;
        Assert.Equal(95, CoderPets.HitChance(pet, 50, 14));
        Assert.Equal(75, CoderPets.HitChance(pet, 50, 15));
    }

    [Theory]
    [InlineData(3, 12)]
    [InlineData(4, 13)]
    [InlineData(5, 14)]
    public void FortifyUsesTheArmorChangeAtTheRetaliationLevels(int attackerLevel, int expected)
    {
        var pet = ResourcePet();
        pet.Mob.setLevel(4);
        pet.FortifyResistance = 36;
        pet.FortifyUntil = 20;
        Assert.Equal(expected, CoderPets.MitigateRetaliation(pet, attackerLevel, 20, 19));
        Assert.Equal(1, CoderPets.MitigateRetaliation(pet, attackerLevel, 1, 19));
        Assert.Equal(20, CoderPets.MitigateRetaliation(pet, attackerLevel, 20, 20));
    }
    [Fact]
    public void PowerBoostUsesCasterLevelTypedInfluenceAndReplacesRatherThanStacks()
    {
        var pet = ResourcePet(); pet.Kind = CoderPets.Kind.Proxy; pet.Sheet = "Aikido";
        CoderPets.ApplyPowerBoost(pet, 50, 10);
        Assert.Equal(100, CoderPets.PowerBoostDamage(pet, 80, 309));
        Assert.Equal(80, CoderPets.PowerBoostDamage(pet, 80, 310));
        pet.Sheet = "SMG";
        Assert.Equal(100, CoderPets.PowerBoostDamage(pet, 80, 309));
        CoderPets.ApplyPowerBoost(pet, 20, 20);
        Assert.Equal(88, CoderPets.PowerBoostDamage(pet, 80, 319));
        Assert.Equal(80, CoderPets.PowerBoostDamage(pet, 80, 320));
        pet.Kind = CoderPets.Kind.Nuker;
        Assert.Equal(80, CoderPets.PowerBoostDamage(pet, 80, 21));
        pet.Kind = CoderPets.Kind.Healer;
        Assert.Equal(80, CoderPets.PowerBoostDamage(pet, 80, 21));
        Assert.Equal(80, CoderPets.DaemonBoostHealing(pet, 80, 21));
    }

    [Fact]
    public void Daemon700AndPower25UseIndependentTimers()
    {
        var pet = ResourcePet();
        CoderPets.ApplyPowerBoost(pet, 50, 10);
        pet.Kind = CoderPets.Kind.Proxy; pet.Sheet = "Aikido";
        Assert.Equal(100, CoderPets.PowerBoostDamage(pet, 80, 25));
        CoderPets.ApplyDaemonBoostAccuracy(pet, 50, 309);
        Assert.Equal(64,pet.BoostAccuracy); //binary32 scale1.3 *50 truncates64.999997 to64
        Assert.Equal(100, CoderPets.PowerBoostDamage(pet, 80, 309));
        Assert.Equal(80, CoderPets.PowerBoostDamage(pet, 80, 310));
        Assert.Equal(95, CoderPets.HitChance(pet, 50, 310));
        Assert.Equal(75, CoderPets.HitChance(pet, 50, 314));
        Assert.Equal(80, CoderPets.PowerBoostDamage(ResourcePet(), 80, 21)); //fresh replacement carries no buff
    }

    [Fact]
    public void PowerBoostAppliesFixedForceCombatToTheActualPetUntilExpiry()
    {
        var entry = AbilityCatalog.Find(25);
        Assert.Equal(50, AbilityCosts.Cost(25));
        Assert.Equal(4, entry.CastSeconds);
        Assert.Equal(10, entry.Number("Ability/SelectionRange"));
        Assert.Equal(11, entry.Number("Ability/CastValidTarget"));
        Assert.Equal(1, entry.Number("Ability/NonCombatOnlyAbility"));
        Assert.Contains(entry.Modifiers, m => m.Target == 519 && m.Number("MinValue") == 40);
        Assert.True(CoderPets.TargetsOwnPet(25));

        var pet = ResourcePet();
        Assert.False(Scores(10).Allows);
        CoderPets.ApplyPowerBoost(pet, 18, 10);
        Assert.Equal(140, Scores(309.99).Force);
        Assert.Equal(110, Scores(309.99).Evade);
        Assert.True(Scores(309.99).Allows);
        Assert.False(Scores(310).Allows);
        Assert.Equal(0, CoderPets.ActivePowerForce(ResourcePet(), 30));

        CombatInsightEngagement.Scores Scores(double now) =>
            CombatInsightEngagement.Calculate(0, CoderPets.ActivePowerForce(pet, now), 0, 0, 0);
    }
    [Fact]
    public void PowerCastSnapshotsLevelAndRejectsOwnerLifeWorldAndPetReplacement()
    {
        var caster=Owner(110,50); var pet=ResourcePet(); pet.Owner=caster;
        var cast=new CoderPets.OwnPetCast(caster,pet);
        caster.playerInstance.Level.setValue((byte)20);
        CoderPets.ApplyPowerBoost(pet,cast.CasterLevel,10);
        Assert.Equal(25,pet.PowerMeleeDamage);
        Assert.True(cast.Matches(caster,pet));
        Assert.False(cast.Matches(caster,ResourcePet()));
        Assert.False(cast.Matches(Owner(110,50),pet));
        caster.playerData.lifeGeneration++;
        Assert.False(cast.Matches(caster,pet));
        caster.playerData.lifeGeneration--;
        caster.playerData.worldGeneration++;
        Assert.False(cast.Matches(caster,pet));
        caster.playerData.worldGeneration--;
        pet.Mob.setIsDead(true);
        Assert.False(cast.Matches(caster,pet));
        pet.Mob.setIsDead(false); pet.Gone=true;
        Assert.False(cast.Matches(caster,pet));
    }

    [Fact]
    public void PursuitReusesTheCornerRouteAndThrottlesAnUnreachableDestination()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "data", "sector_properties.csv")))
            root = Path.GetDirectoryName(root)!;
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));

        var mob = new Mob();
        mob.setDistrict(1);
        var pet = new CoderPets.Pet { Mob = mob };
        var original = (19074d, 95d, -8852d);
        var far = (20218d, 95d, -9510d);
        Assert.False(NavMesh.HasSightPath(1, original.Item1, original.Item2, original.Item3, far.Item1, far.Item2, far.Item3));
        Assert.True(CoderPets.PetWaypoint(pet, original, far, 0, out var first));
        Assert.True(pet.Route.Length > 1);
        NavPoint[] route = pet.Route;
        Assert.True(CoderPets.PetWaypoint(pet, (first.x, first.y, first.z), far, 1, out var second));
        Assert.Same(route, pet.Route);
        Assert.Equal(route[1].Position, second.Position);

        var differentFloor = (18533d, 1695d, 3170d);
        Assert.False(CoderPets.PetWaypoint(pet, original, differentFloor, 2, out _));
        Assert.Equal(5, pet.RetryRouteAt);
        Assert.False(CoderPets.PetWaypoint(pet, original, differentFloor, 3, out _));
        Assert.Equal(5, pet.RetryRouteAt);
        Assert.False(CoderPets.PetWaypoint(pet, original, differentFloor, 5, out _));
        Assert.Equal(8, pet.RetryRouteAt);
    }
}
