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
    public void WeaponStylesUseOnlyTheSelectedLearnedLevelNumbers()
    {
        foreach (ushort weapon in new ushort[] { 129, 147, 158 })
        {
            Assert.True(CombatStyles.IsStyle(weapon));
            Assert.Equal(7,CombatStyles.Modifiers(weapon).Count());
            var client=Client();
            AbilityPassives.SetLoaded(client,new[]{(weapon,(ushort)20)},true);
            Assert.Equal(0,AbilityStats.Player(client,524));
            CombatStyles.Select(client,weapon);
            Assert.Equal(25,AbilityStats.Player(client,524));
            Assert.Equal(6,AbilityStats.Player(client,171));
            Assert.Equal(weapon==129?27:weapon==147?21:23,AbilityStats.Player(client,1009));
            CombatStyles.Select(client,17);
            Assert.Equal(0,AbilityStats.Player(client,524));
            Assert.Equal(0,AbilityStats.Player(client,171));
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

    private static List<string> Rpcs(WorldClient client)
    {
        var list = new List<string>();
        foreach (SequencedMessage m in client.messageQueue.RPCMessagesQueue)
            list.Add(BitConverter.ToString(m.content).Replace("-", "").ToLowerInvariant().Substring(2)); // length byte first
        client.messageQueue.RPCMessagesQueue.Clear();
        client.messageQueue.ObjectMessagesQueue.Clear();
        return list;
    }

    [Fact]
    public void UsingALoadedStyleFromTheActionsWindowSwitchesLikeRetail()
    {
        var client = Client();
        client.playerInstance.CharacterName.setValue("Stylist");
        client.playerData.setOnWorld(true);
        AbilityPassives.SetLoaded(client, new (ushort, ushort)[] { (2, 50), (CombatStyles.KungFu, 50), (CombatStyles.HackerStyle, 50) });
        Rpcs(client);

        // 80 b9 85 00: Kung-Fu used like an ability
        Assert.True(CombatStyles.UseStyle(client, CombatStyles.KungFu));
        Assert.Equal(CombatStyles.KungFu, CombatStyles.Active(client));
        Assert.Equal(133u, client.playerData.combatSelection);
        var rpcs = Rpcs(client);
        // Retail's order (Hacker Style replacing Self Defense, abilitychangecoder 10:36:05): Self Defense's kind-15 records
        // off, its 80 bd 05 toggle off, the new style's kind-15 records on, its toggle on, then the activation line
        int sdToggle = rpcs.IndexOf("80bd051100000000000000");
        int kfToggle = rpcs.IndexOf("80bd058500000000000001");
        Assert.True(sdToggle > 0, string.Join(" | ", rpcs));
        Assert.True(kfToggle > sdToggle);
        Assert.All(rpcs.Take(sdToggle), r => Assert.StartsWith("80bc1500", r));
        Assert.All(rpcs.Take(sdToggle), r => Assert.Contains("0011000000", r));
        Assert.Contains(rpcs.Skip(sdToggle + 1).Take(kfToggle - sdToggle - 1), r => r.StartsWith("80bc1500") && r.Contains("0085000000" + "0a02"));
        // "You are now using the %1z martial arts style." with MeleeAcc's value: 10 + trunc(50 x 1.3f) = 74
        Assert.StartsWith("81671700" + "2100" + "1c2200c6" + "01" + "85000000" + "00000000", rpcs[kfToggle + 1]);
        Assert.EndsWith("02004a00000000000000", rpcs[kfToggle + 1]);

        // The same style again changes nothing but the selection echo; an unloaded style is refused, not switched
        Assert.True(CombatStyles.UseStyle(client, CombatStyles.KungFu));
        Assert.Empty(Rpcs(client));
        Assert.True(CombatStyles.UseStyle(client, CombatStyles.Karate));
        Assert.Equal(CombatStyles.KungFu, CombatStyles.Active(client));
        Assert.StartsWith("80ba8400", Assert.Single(Rpcs(client)));
        Assert.False(CombatStyles.UseStyle(client, CombatSpecials.BerserkerAttack));
    }

    [Fact]
    public void TheWrongStyleRefusalIsTheClientsOwnString()
    {
        // client.dll 621e52cd: 80 ba code 13 -> table index 11 -> ID_ABILITY_NOT_IN_CORRECT_STYLE (0x7a0002ba)
        Assert.Equal((byte)0x0d, CombatStyles.DeniedWrongStyle);
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
