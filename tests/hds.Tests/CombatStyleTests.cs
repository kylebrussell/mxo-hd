using System.Runtime.CompilerServices;
using hds;
using hds.shared;

namespace hds.Tests;

[Collection("DelayedActions")]
public class CombatStyleTests
{
    private static WorldClient Client()
    {
        var client = (WorldClient)RuntimeHelpers.GetUninitializedObject(typeof(WorldClient));
        client.playerData = new ClientData();
        client.playerData.setDistrictId(1);
        client.playerInstance = new PlayerCharacter();
        client.playerInstance.Level.setValue((byte)50);
        client.playerInstance.Health.setValue((ushort)100);
        client.playerInstance.MaxHealth.setValue((ushort)500);
        client.playerInstance.InnerStrengthMax.setValue((ushort)500);
        client.playerInstance.InnerStrengthAvailable.setValue((ushort)100);
        client.messageQueue = new MessageQueue();
        typeof(WorldClient).GetField("lastFlushTick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(client, long.MaxValue);
        return client;
    }

    private const ushort MeleeTactics = 522, MeleeDamage = 159, MeleeDefense = 1009, ViralTactics = 342, Healing = 174;

    [Fact]
    public void OnlyTheActiveStyleContributesItsWhileEnabledModifiers()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50), (CombatStyles.KungFu, 50), (CombatStyles.Karate, 50) });
        // Self Defense (granted by Awakened) is the default style
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(client));
        int selfDefenseDamage = AbilityPassives.Total(client, MeleeDamage);
        Assert.True(selfDefenseDamage >= 4);

        CombatStyles.Select(client, CombatStyles.KungFu);
        Assert.Equal(CombatStyles.KungFu, CombatStyles.Active(client));
        Assert.Equal(10 + 64, AbilityPassives.Total(client, MeleeTactics)); // MeleeAcc 10 + trunc(50 x binary32 1.3 = 64.99999)
        Assert.Equal(4 + 23, AbilityPassives.Total(client, MeleeDamage));   // MeleeDam 4 + trunc(50 x .47)
        Assert.Equal(2 + 47, AbilityPassives.Total(client, MeleeDefense));  // MeleeDef 2 + trunc(50 x .96f = 47.99999)

        CombatStyles.Select(client, CombatStyles.Karate);
        Assert.Equal(0 + 64, AbilityPassives.Total(client, MeleeTactics));
        Assert.Equal(4 + 25, AbilityPassives.Total(client, MeleeDamage));
        Assert.Equal(-2 + 47, AbilityPassives.Total(client, MeleeDefense));
    }

    [Fact]
    public void AnUnloadedStyleFallsBackToSelfDefense()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50), (CombatStyles.KungFu, 50) });
        CombatStyles.Select(client, CombatStyles.KungFu);
        int kungFu = AbilityPassives.Total(client, MeleeTactics);
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50) });
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(client));
        Assert.NotEqual(kungFu, AbilityPassives.Total(client, MeleeTactics));
        CombatStyles.Select(client, CombatStyles.SelfDefense);
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(client));
    }

    [Theory]
    [InlineData(CombatStyles.Aikido)]
    [InlineData(CombatStyles.Karate)]
    [InlineData(CombatStyles.KungFu)]
    [InlineData(CombatStyles.HackerStyle)]
    [InlineData(CombatStyles.CoderStyle)]
    [InlineData(CombatStyles.SpyStyle)]
    [InlineData(CombatStyles.SelfDefense)]
    public void EveryMissingStyleIsASelectableStyleWithStoredModifiers(ushort style)
    {
        Assert.True(CombatStyles.IsStyle(style));
        Assert.True(CombatSelection.IsStyle(style));
        Assert.NotEmpty(CombatStyles.Modifiers(style));
        Assert.All(CombatStyles.Modifiers(style), m => Assert.Equal(2, m.Number("ValueFunction")));
    }

    [Fact]
    public void HackerAndCoderStylesBoostViralAndHealing()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50), (CombatStyles.HackerStyle, 50), (CombatStyles.CoderStyle, 50) });
        CombatStyles.Select(client, CombatStyles.HackerStyle);
        Assert.Equal(6 + 64, AbilityPassives.Total(client, ViralTactics)); // ViralAcc 6 + trunc(50 x 1.3f)
        Assert.Equal(4 + 23, AbilityPassives.Total(client, Healing)); // HealDam
        CombatStyles.Select(client, CombatStyles.CoderStyle);
        Assert.Equal(0 + 64, AbilityPassives.Total(client, ViralTactics));
        Assert.Equal(0 + 64, AbilityPassives.Total(client, MeleeTactics));
        Assert.Equal(0, AbilityPassives.Total(client, Healing));
    }

    [Fact]
    public void SpecialMovesNeedTheirStoredStance()
    {
        Assert.Equal(CombatStyles.Karate, CombatStyles.RequiredStyle(CombatSpecials.BerserkerAttack));
        Assert.Equal(CombatStyles.Karate, CombatStyles.RequiredStyle(267)); // Sky High Sidekick
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(CombatSpecials.PoisonKnife));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.RequiredStyle(CombatSpecials.SideRollEscape));
        Assert.Equal(0, CombatStyles.RequiredStyle(CombatSpecials.Taunt));
        Assert.Equal(0, CombatStyles.RequiredStyle(CombatSpecials.WoodenStake));
        Assert.Equal(0, CombatStyles.RequiredStyle(15)); // Precise Blow is a toggle, not a style special

        Assert.True(CombatStyles.IsSpecialMove(CombatSpecials.BerserkerAttack));
        Assert.True(CombatStyles.IsSpecialMove(CombatSpecials.WoundingThrow));
        Assert.False(CombatStyles.IsSpecialMove(CombatSpecials.PoisonKnife));

        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50), (CombatStyles.Karate, 50), (CombatStyles.SpyStyle, 50) });
        Assert.False(CombatStyles.CanUse(client, CombatSpecials.BerserkerAttack));
        Assert.True(CombatStyles.CanUse(client, CombatSpecials.Taunt));
        Assert.True(CombatStyles.CanUse(client, CombatSpecials.PoisonKnife)); // a stance but not a special move: not gated
        CombatStyles.Select(client, CombatStyles.Karate);
        Assert.True(CombatStyles.CanUse(client, CombatSpecials.BerserkerAttack));
        Assert.False(CombatStyles.CanUse(client, CombatSpecials.WoundingThrow));
        // The client's special-move picker switches to a loaded style; so does the server
        Assert.True(CombatStyles.EnsureStyleFor(client, CombatSpecials.WoundingThrow));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.Active(client));
        Assert.Equal(CombatStyles.SpyStyle, (ushort)client.playerData.combatSelection);
        // Tomo Nage needs Aikido, which isn't loaded
        Assert.False(CombatStyles.EnsureStyleFor(client, 412));
        Assert.Equal(CombatStyles.SpyStyle, CombatStyles.Active(client));
        // Cheap Shot needs Self Defense, which Awakened grants
        Assert.True(CombatStyles.EnsureStyleFor(client, 198));
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(client));
    }

    [Fact]
    public void WeaponStylesAreSelectableButTheirNumbersBelongElsewhere()
    {
        foreach (ushort weapon in new ushort[] { 129, 147, 158 })
        {
            Assert.True(CombatStyles.IsStyle(weapon));
            Assert.Empty(CombatStyles.Modifiers(weapon));
        }
    }

    [Fact]
    public void InterlockExchangesComeFromTheActiveStylesIldbSet()
    {
        Assert.Equal('W', CombatStyles.Letter(CombatStyles.KungFu));
        Assert.Equal('K', CombatStyles.Letter(CombatStyles.Karate));
        Assert.Equal('A', CombatStyles.Letter(CombatStyles.Aikido));
        Assert.Equal('S', CombatStyles.Letter(CombatStyles.SpyStyle));
        Assert.Equal(21012u, CombatAnimations.Transition(CombatTactic.Speed));
        Assert.Equal(21005u, CombatAnimations.Transition(CombatTactic.Power, 'W')); // Trans Stand-WP
        Assert.Equal(21028u, CombatAnimations.Transition(CombatTactic.Speed, 'A')); // Trans Stand-AS
        var move = CombatAnimations.Choose(CombatTactic.Speed, CombatTactic.Power, CombatRules.AttackerHits, 0, 'W', 'K');
        Assert.StartsWith("WS vs KP:", move.Group);
        var selfDefense = CombatAnimations.Choose(CombatTactic.Speed, CombatTactic.Power, CombatRules.AttackerHits, 0);
        Assert.StartsWith("SS vs SP:", selfDefense.Group);
        Assert.Equal(748u, CombatAnimations.All.First(m => m.Id == 748).Id); // the captured Aikido special stays exported
    }

    [Fact]
    public void ThrownDotsUseTheirStoredValuesAndTheScriptTick()
    {
        Assert.True(TempMods.TryGet(CombatSpecials.PoisonKnife, out var poison));
        var dot = Assert.Single(poison.Mods);
        Assert.Equal(TempMods.Proc.Dot, dot.Proc);
        Assert.Equal((17, 17, 25f, 2.5), (dot.MinValue, dot.MaxValue, dot.Seconds, dot.Interval));
        Assert.Equal(2, TempMods.DotTick(17, 25, 2.5));

        Assert.True(TempMods.TryGet(CombatSpecials.WoundingThrow, out var wounding));
        Assert.Equal((61, 50f), (wounding.Mods.Single().MinValue, wounding.Mods.Single().Seconds));

        Assert.True(TempMods.TryGet(CombatSpecials.SeverArtery, out var sever));
        Assert.Equal(68, sever.Mods.Single(m => m.Proc == TempMods.Proc.Dot).MinValue);
        foreach (ushort stat in new ushort[] { 115, 877, 878 })
            Assert.Equal((-50, 30f), (sever.Mods.Single(m => m.Target == stat).MinValue, sever.Mods.Single(m => m.Target == stat).Seconds));
        Assert.Equal(-10, sever.Mods.Single(m => m.Target == 519).MinValue);
        Assert.DoesNotContain(sever.Mods, m => m.Target == 710); // RandomCombatStateChance is not modelled
    }

    [Fact]
    public void BerserkerAndSideRollBuffTheCasterNotAFriendlyTarget()
    {
        Assert.True(AbilityBuffs.TryGet(CombatSpecials.BerserkerAttack, out var berserk));
        Assert.Equal(21f, berserk.Seconds);
        Assert.Equal(new (ushort, int)[] { (159, 11), (180, 50), (181, 50) },
            berserk.Modifiers.Select(m => (m.Stat, m.Value)).OrderBy(m => m.Item1).ToArray());
        Assert.True(AbilityBuffs.TryGet(CombatSpecials.SideRollEscape, out var roll));
        Assert.Equal((30f, (ushort)520, 20), (roll.Seconds, roll.Modifiers.Single().Stat, roll.Modifiers.Single().Value));
        Assert.False(AbilitySupport.Handles(CombatSpecials.BerserkerAttack));
        Assert.False(AbilitySupport.Handles(CombatSpecials.SideRollEscape));
        Assert.True(CombatSpecials.TargetsEnemy(CombatSpecials.BerserkerAttack));
        Assert.False(CombatSpecials.TargetsEnemy(CombatSpecials.Taunt));
        Assert.True(CombatSpecials.ActsOnCast(CombatSpecials.Taunt));

        var client = Client();
        Assert.True(AbilityBuffs.Apply(client, CombatSpecials.BerserkerAttack));
        Assert.Equal(11, AbilityBuffs.Total(client, 159));
        Assert.Equal(50, AbilityBuffs.Total(client, 180));
    }

    [Fact]
    public void ExileWeaponsFollowGeneralPy()
    {
        Assert.Equal(CombatSpecials.KilledOnlyByWood, CombatSpecials.Weakness("Vampire Thrall"));
        Assert.Equal(CombatSpecials.KilledOnlyBySilver, CombatSpecials.Weakness("Werewolf"));
        Assert.Equal(CombatSpecials.KilledOnlyByHolyWater, CombatSpecials.Weakness("Zombie Lurch"));
        Assert.Equal(0, CombatSpecials.Weakness("Blackwoods"));
        Assert.Equal(100, CombatSpecials.ExileDamage(CombatSpecials.WoodenStake, CombatSpecials.KilledOnlyByWood));
        Assert.Equal(750, CombatSpecials.ExileDamage(CombatSpecials.WoodenStakePlus, CombatSpecials.KilledOnlyByWood));
        Assert.Equal(10, CombatSpecials.ExileDamage(CombatSpecials.WoodenStake, 0));
        Assert.Equal(10, CombatSpecials.ExileDamage(CombatSpecials.SilverBullet, CombatSpecials.KilledOnlyByWood));
        Assert.Equal(100, CombatSpecials.ExileDamage(CombatSpecials.SilverBullet, CombatSpecials.KilledOnlyBySilver));
        Assert.Equal(750, CombatSpecials.ExileDamage(CombatSpecials.SilverBulletPlus, CombatSpecials.KilledOnlyBySilver));
        Assert.Equal(100, CombatSpecials.ExileDamage(CombatSpecials.HolyWater, CombatSpecials.KilledOnlyByHolyWater));
        Assert.Equal(750, CombatSpecials.ExileDamage(CombatSpecials.HolyWaterPlus, CombatSpecials.KilledOnlyByHolyWater));
        Assert.Equal((ushort)10, CombatSpecials.Damage(CombatSpecials.HolyWater, null));
        Assert.Equal((ushort)0, CombatSpecials.Damage(CombatSpecials.PoisonKnife, null)); // never the generic 50-81
        Assert.Null(CombatSpecials.Damage(198, null));
    }

    [Fact]
    public void MartialArtIsRetainedWithoutChangingTheStyle()
    {
        var client = Client();
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50), (CombatStyles.KungFu, 50) });
        CombatSelection.ProcessMartialArt(client, new byte[] { 0x85, 0x00 });
        Assert.Equal((ushort)0x85, client.playerData.martialArt);
        Assert.Equal(CombatStyles.SelfDefense, CombatStyles.Active(client));
    }
}
