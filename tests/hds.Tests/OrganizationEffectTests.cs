using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using hds;
using hds.shared;
using NativeQa;
using Xunit;

namespace hds.Tests;

[Collection("AbilityParityWorld")]
public class OrganizationEffectTests
{
    private const ushort SystemControl = AbilityHealthCallbacks.SystemControl, Retribution = AbilityHealthCallbacks.Retribution,
        Zion = AbilityHealthCallbacks.ChampionOfZion;
    private const ushort SystemControlEffect = AbilityHealthCallbacks.SystemControlEffect,
        RetributionEffect = AbilityHealthCallbacks.RetributionEffect, ZionEffect = AbilityHealthCallbacks.ChampionOfZionEffect;
    private const int X = 17000, Y = 495, Z = 6400;

    private static WorldClient Client(uint charId, double x, double z, int alignment = 1, ushort health = 5000, ushort inner = 900)
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setCharID(charId);
        client.playerData.setEntityId(charId);
        client.playerData.setDistrictId(1);
        client.playerData.setOnWorld(true);
        client.playerData.pvpFlag = true;
        client.playerData.isBot = true;
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.CharacterName.setValue(StringUtils.stringToBytes("QA" + charId + "\0"));
        client.playerInstance.OrganizationID.setValue((byte)alignment);
        client.playerInstance.Position.setValue(Pos(x, Y, z));
        client.playerInstance.Health.setValue(health);
        client.playerInstance.MaxHealth.setValue((ushort)5000);
        client.playerInstance.InnerStrengthMax.setValue((ushort)1000);
        client.playerInstance.InnerStrengthAvailable.setValue(inner);
        client.messageQueue = new MessageQueue();
        client.viewMan = new ViewManager();
        typeof(WorldClient).GetField("lastFlushTick", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private static byte[] Pos(double x, double y, double z)
    {
        byte[] bytes = new byte[24];
        BitConverter.GetBytes(x).CopyTo(bytes, 0);
        BitConverter.GetBytes(y).CopyTo(bytes, 8);
        BitConverter.GetBytes(z).CopyTo(bytes, 16);
        return bytes;
    }

    private static void PrepareWorld(params WorldClient[] clients)
    {
        string root = FindRepoRoot();
        SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        Store.world = (WorldServer)RuntimeHelpers.GetUninitializedObject(typeof(WorldServer));
        WorldServer.Clients ??= new Dictionary<string, WorldClient>();
        WorldServer.Clients.Clear();
        for (int i = 0; i < clients.Length; i++) WorldServer.Clients[$"organization-{i}"] = clients[i];
        var config = (WorldConfig)RuntimeHelpers.GetUninitializedObject(typeof(WorldConfig));
        config.IsPvpServer = true;
        config.PvpMaxSafeLevel = 15;
        Store.worldConfig = config;
    }

    private static void Track(WorldClient observer, WorldClient target, ushort viewId)
    {
        observer.viewMan.views.Add(new ClientView(12, viewId, target.playerData.getEntityId()) { spawnId = viewId, viewCreated = true });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Func<int> roll100 = AbilityHealthCallbacks.Random100, roll15 = AbilityHealthCallbacks.Random15;
        private readonly System.Collections.ArrayList mobs = WorldServer.mobs;
        public readonly WorldClient Owner, Enemy, Outside, Friend, FarFriend;
        public readonly List<int> Rolls = new();
        public int Roll;

        public Fixture()
        {
            Owner = Client(110, X, Z, health: 5000, inner: 500);
            Enemy = Client(6114, X - 300, Z, alignment: 2);            // 3.00 m
            Outside = Client(6113, X + 501, Z, alignment: 2);          // 5.01 m: outside radius 5
            Friend = Client(6111, X + 150, Z, health: 4000, inner: 500); // 1.50 m
            FarFriend = Client(6112, X, Z - 600, health: 4000, inner: 500); // 6.00 m
            PrepareWorld(Owner, Enemy, Outside, Friend, FarFriend);
            Track(Owner, Enemy, 4); Track(Owner, Outside, 5); Track(Owner, Friend, 6); Track(Owner, FarFriend, 7);
            WorldServer.mobs = System.Collections.ArrayList.Synchronized(new System.Collections.ArrayList());
            AbilityHealthCallbacks.Random100 = () => { Rolls.Add(Roll); return Roll; };
            AbilityHealthCallbacks.Random15 = () => 0;
        }

        public void Dispose()
        {
            AbilityHealthCallbacks.Random100 = roll100; AbilityHealthCallbacks.Random15 = roll15;
            foreach (var c in new[] { Owner, Enemy, Outside, Friend, FarFriend })
            {
                AbilityBuffs.RemoveAll(c);
                TimedEffects.RemoveParent(TimedEffects.ForPlayer(c), SystemControlEffect);
            }
            WorldServer.mobs = mobs;
            WorldServer.Clients.Clear();
        }
    }

    [Fact]
    public void OrganizationParentsAreSourcedTogglesWithFivePercentHealthCallbacks()
    {
        foreach (var (parent, proc, effect) in new[]
        {
            (SystemControl, "ApplyMachineEffectHealthChance", SystemControlEffect),
            (Retribution, "ApplyMervEffectHealthChance", RetributionEffect),
            (Zion, "ApplyZionEffectHealthChance", ZionEffect)
        })
        {
            var entry = AbilityCatalog.Find(parent);
            Assert.Equal(1d, entry.Number("Ability/IsAToggleAbility"));
            Assert.Equal(proc, entry.Modifiers.Single(m => m.Has("StatModHealthChangedFunc")).Text("StatModHealthChangedFunc"));
            Assert.Equal(effect, AbilityHealthCallbacks.OrganizationEffect(proc));
            Assert.Equal(parent, AbilityCatalog.IdOf((uint)AbilityCatalog.Find(effect).Number("AbilityPrerequisite/ReqAbilityID")));
            Assert.True(AbilityHealthCallbacks.Template(parent));
            Assert.True(AbilityHealthCallbacks.TryGet(parent, 50, 0, out var buff));
            Assert.True(buff.Toggle);
            Assert.Equal(0f, buff.Seconds);
            Assert.Equal(proc, buff.HealthChangedProc);
            Assert.Equal(AbilityHealthCallbacks.OrganizationEffectChance, buff.HealthChangedValue);
            Assert.Equal(25, buff.DrainAmount);
            Assert.Equal(5d, buff.DrainSeconds);
            Assert.Equal((ushort)25, AbilityCosts.Cost(parent));
            Assert.True(AbilityCooldowns.TryGet(parent, out var cooldown));
            Assert.Equal(240f, cooldown.Seconds);
            Assert.Equal((ushort)1178, cooldown.Group);
            Assert.Equal(240f, cooldown.GroupSeconds);
            Assert.True(AbilityHealthCallbacks.InternalEffect(effect));
            Assert.False(AbilityHealthCallbacks.InternalEffect(parent));
            Assert.Equal(5d, AbilityCatalog.Find(effect).Number("Ability/RadiusOfEffect"));
            Assert.NotEqual("", AbilityHealthCallbacks.Describe(effect));
            Assert.DoesNotContain("OURS", AbilityHealthCallbacks.Describe(effect));
            Assert.DoesNotContain("unresolved", AbilityHealthCallbacks.Describe(effect));
        }
        // tempmods.py: randrange(0, 100) < 5. The stored 240 belongs to AddRecastTimer on the same modifier.
        Assert.Equal(5, AbilityHealthCallbacks.OrganizationEffectChance);
        Assert.Equal(240d, AbilityCatalog.Find(SystemControl).Modifiers.Single(m => m.Text("StatModTermFunc") == "AddRecastTimer").Number("MinValue"));
    }

    [Fact]
    public void OrganizationCallbacksRollOnlyForDamageThatLeavesTheOwnerBelowMaximum()
    {
        using var f = new Fixture();
        Assert.True(AbilityBuffs.Apply(f.Owner, Retribution));
        f.Roll = 0;
        AbilityHealthCallbacks.HealthChanged(f.Owner, -100, true);  // healing
        AbilityHealthCallbacks.HealthChanged(f.Owner, 100, false);  // still at maximum
        Assert.Empty(f.Rolls);
        Assert.Equal((ushort)5000, PvpCombat.HealthOf(f.Enemy));

        f.Roll = 5;
        AbilityHealthCallbacks.HealthChanged(f.Owner, 100, true);
        Assert.Equal(new[] { 5 }, f.Rolls);
        Assert.Equal((ushort)5000, PvpCombat.HealthOf(f.Enemy));

        f.Roll = 4;
        AbilityHealthCallbacks.HealthChanged(f.Owner, 100, true);
        Assert.Equal(new[] { 5, 4 }, f.Rolls);
        Assert.Equal((ushort)4850, PvpCombat.HealthOf(f.Enemy));

        AbilityBuffs.Remove(f.Owner, Retribution);
        f.Roll = 0;
        AbilityHealthCallbacks.HealthChanged(f.Owner, 100, true);
        Assert.Equal(2, f.Rolls.Count);
        Assert.Equal((ushort)4850, PvpCombat.HealthOf(f.Enemy));
    }

    [Fact]
    public void RetributionDealsFixedNeutralDamageOnlyToHostilesInsideFiveMetres()
    {
        using var f = new Fixture();
        Assert.Equal(150, AbilityHealthCallbacks.Sample(RetributionEffect, 0));
        Assert.Equal(150, AbilityHealthCallbacks.Sample(RetributionEffect, 32767));
        Assert.Equal(5d, AbilityCatalog.Find(RetributionEffect).Number("StatModifier/DamageType"));
        Assert.Equal(4d, AbilityCatalog.Find(RetributionEffect).Number("Ability/CastDirectObjects"));

        AbilityHealthCallbacks.Reapply(f.Owner, RetributionEffect);
        Assert.Equal((ushort)4850, PvpCombat.HealthOf(f.Enemy));
        Assert.Equal((ushort)5000, PvpCombat.HealthOf(f.Outside));
        Assert.Equal((ushort)4000, PvpCombat.HealthOf(f.Friend));
        Assert.Equal((ushort)5000, PvpCombat.HealthOf(f.Owner));
    }

    [Fact]
    public void SystemControlStunsHostilesForFiveSecondsWithOneTenSecondImmunityWindow()
    {
        using var f = new Fixture();
        Assert.True(TempMods.TryGet(SystemControlEffect, out var effect));
        var stun = Assert.Single(effect.Mods);
        Assert.Equal(TempMods.StunAbility, stun.Target);
        Assert.Equal(5f, stun.Seconds);
        Assert.Equal(10f, effect.ImmunitySeconds);
        Assert.Equal("AddControlImmunityTimerVal", AbilityCatalog.Find(SystemControlEffect).Modifiers
            .Single(m => m.Has("StatModTermFunc")).Text("StatModTermFunc"));

        AbilityHealthCallbacks.Reapply(f.Owner, SystemControlEffect);
        var landed = TimedEffects.ForPlayer(f.Enemy).Effects.Snapshot();
        Assert.Contains(landed, m => m.Target == TempMods.StunAbility && m.Proc == TempMods.Proc.State && m.Seconds == 5f);
        Assert.Contains(landed, m => m.Target == TempMods.ISDrainTargetAbility && m.Seconds == 10f);
        Assert.Empty(TimedEffects.ForPlayer(f.Outside).Effects.Snapshot());
        Assert.Empty(TimedEffects.ForPlayer(f.Friend).Effects.Snapshot());
        Assert.Empty(TimedEffects.ForPlayer(f.Owner).Effects.Snapshot());

        // Inside the immunity window a second reapplication adds no second stun.
        AbilityHealthCallbacks.Reapply(f.Owner, SystemControlEffect);
        Assert.Single(TimedEffects.ForPlayer(f.Enemy).Effects.Snapshot(), m => m.Target == TempMods.StunAbility);
    }

    [Fact]
    public void ChampionOfZionHealsAndRestoresInnerStrengthForOwnerAndFriendsInsideFiveMetres()
    {
        using var f = new Fixture();
        Assert.Equal(250, AbilityHealthCallbacks.Sample(ZionEffect, 0));
        Assert.Equal(6d, AbilityCatalog.Find(ZionEffect).Number("StatModifier/DamageType"));
        Assert.Equal(7d, AbilityCatalog.Find(ZionEffect).Number("Ability/CastDirectObjects"));
        f.Owner.playerInstance.Health.setValue((ushort)4900);
        f.Friend.playerInstance.InnerStrengthAvailable.setValue((ushort)990);

        AbilityHealthCallbacks.Reapply(f.Owner, ZionEffect);
        Assert.Equal((ushort)5000, PvpCombat.HealthOf(f.Owner));  // capped at maximum
        Assert.Equal((ushort)525, AbilityHandler.CurrentInnerStrength(f.Owner));
        Assert.Equal((ushort)4250, PvpCombat.HealthOf(f.Friend));
        Assert.Equal((ushort)1000, AbilityHandler.CurrentInnerStrength(f.Friend)); // capped at maximum
        Assert.Equal((ushort)4000, PvpCombat.HealthOf(f.FarFriend));
        Assert.Equal((ushort)500, AbilityHandler.CurrentInnerStrength(f.FarFriend));
        Assert.Equal((ushort)5000, PvpCombat.HealthOf(f.Enemy));
        Assert.Equal((ushort)900, AbilityHandler.CurrentInnerStrength(f.Enemy));
    }

    [Fact]
    public void ZionToggleHealsTheOwnerThroughItsOwnDamageCallbackWithoutRecursion()
    {
        using var f = new Fixture();
        Assert.True(AbilityBuffs.Apply(f.Owner, Zion));
        f.Roll = 0;
        Assert.False(PvpCombat.Damage(f.Owner, 1000));
        Assert.Single(f.Rolls);
        Assert.Equal((ushort)4250, PvpCombat.HealthOf(f.Owner));
        Assert.Equal((ushort)4250, PvpCombat.HealthOf(f.Friend));
    }

    [Fact]
    public void OrganizationEffectsDoNotAffectNpcsOutsideTheRadius()
    {
        using var f = new Fixture();
        var near = new Mob(); near.setEntityId(9001); near.setMobId(1); near.setDistrict(1); near.setName("Near"); near.setLevel(50);
        near.setHealthC(1000); near.setHealthM(1000); near.setXPos(X); near.setYPos(Y); near.setZPos(Z + 300);
        var far = new Mob(); far.setEntityId(9002); far.setMobId(2); far.setDistrict(1); far.setName("Far"); far.setLevel(50);
        far.setHealthC(1000); far.setHealthM(1000); far.setXPos(X); far.setYPos(Y); far.setZPos(Z + 501);
        WorldServer.mobs.Add(near); WorldServer.mobs.Add(far);
        f.Owner.viewMan.addView(9001, 1).viewCreated = true;
        f.Owner.viewMan.addView(9002, 1).viewCreated = true;

        AbilityHealthCallbacks.Reapply(f.Owner, SystemControlEffect);
        Assert.Contains(TimedEffects.ForMob(near).Effects.Snapshot(), m => m.Target == TempMods.StunAbility && m.Seconds == 5f);
        Assert.Empty(TimedEffects.ForMob(far).Effects.Snapshot());
        TimedEffects.RemoveParent(TimedEffects.ForMob(near), SystemControlEffect);
    }

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "data", "sector_properties.csv")))
            dir = Directory.GetParent(dir)!.FullName;
        return dir;
    }
}
