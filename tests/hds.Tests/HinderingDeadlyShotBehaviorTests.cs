using System;
using System.Linq;
using hds;
using Xunit;

namespace hds.Tests;

/// <summary>Every listed shipped behavior of Hindering Shot (130) and Deadly Shot (454), with the other tests that run each.</summary>
public class HinderingDeadlyShotBehaviorTests
{
    [Fact]
    public void HinderingShotStoredTermsModifiersContestAndMotion()
    {
        var entry = AbilityCatalog.Find(CombatSpecials.HinderingShot)!;
        Assert.Equal(20, AbilityCosts.Cost(CombatSpecials.HinderingShot));
        Assert.True(AbilityCooldowns.TryGet(CombatSpecials.HinderingShot, out var cd));
        Assert.Equal(8f, cd.Seconds);
        Assert.Equal(690, cd.Group);
        Assert.Equal(5f, cd.GroupSeconds);
        Assert.Equal(2f, entry.CastSeconds);
        Assert.Equal(20, entry.Number("Ability/SelectionRange"));
        Assert.Equal(1, entry.Number("Ability/TestFunction"));
        Assert.Equal(CombatStyles.Handguns, CombatStyles.RequiredStyle(CombatSpecials.HinderingShot));
        Assert.Equal(1, TypedAbilityResistance.StoredType(CombatSpecials.HinderingShot));
        // Stored 8 s records: Movement Speed, Sneak Movement and Invisibility Movement -50, Force Combat -10.
        var timed = entry.Modifiers.Where(m => m.Number("Duration") == 8 && m.Number("WhenEnabled", -1) == 2).ToArray();
        foreach (var (stat, value) in new (ushort, int)[] { (115, -50), (877, -50), (878, -50), (519, -10) })
            Assert.Contains(timed, m => m.Target == stat && m.Number("MinValue") == value);
        var item = new AbilityItem();
        item.setAbilityID(CombatSpecials.HinderingShot);
        Assert.Equal(9, AbilityHandler.AbilityDamage(item, new Random(1)));
        Assert.True(PoisonKnifeContest.Applies(CombatSpecials.HinderingShot, AbilityCastOrigin.NormalFreeCast));
        Assert.Equal(AtRestSpecialMotion.PistolHinderingShot, AtRestSpecialMotion.Actions[CombatSpecials.HinderingShot]);
    }

    [Fact]
    public void DeadlyShotContestMotionAndStunCallback()
    {
        var entry = AbilityCatalog.Find(CombatSpecials.DeadlyShot)!;
        Assert.Contains(entry.Modifiers, m => m.Text("StatModTermFunc") == "ApplyStunWithTimer" && m.Number("MaxValue") == 4 &&
            m.Number("ProcTriggerChance") == 0.1);
        Assert.True(PoisonKnifeContest.Applies(CombatSpecials.DeadlyShot, AbilityCastOrigin.NormalFreeCast));
        Assert.Equal(AtRestSpecialMotion.RifleDeadlyShot, AtRestSpecialMotion.Actions[CombatSpecials.DeadlyShot]);
    }
}
