using hds;

namespace hds.Tests;

public class ViriiEffectsAugmentsTests
{
    [Fact]
    public void VirusTimesAndSizes()
    {
        // virii_defines.py 9-21, virii.py 10-21
        Assert.Equal(new[] { 10f, 15f, 25f }, new[] { Virii.DurationForLevel(1), Virii.DurationForLevel(2), Virii.DurationForLevel(3) });
        Assert.Equal(25f, Virii.MultiDuration);
        Assert.Equal(new[] { -10, -20, -30 }, new[] { Virii.DebuffForLevel(1), Virii.DebuffForLevel(2), Virii.DebuffForLevel(3) });
        Assert.Equal(new[] { 10, 20, 30 }, new[] { Virii.BuffForLevel(1), Virii.BuffForLevel(2), Virii.BuffForLevel(3) });
        Assert.Equal(5000f, Virii.Level3Range);
        Assert.Equal(15f, Virii.MovementDisrupt3Time); // level 2's time, as the script has it
        Assert.Equal(75, Virii.MovementDisrupt3Effect);
    }

    [Fact]
    public void VirusRoll()
    {
        // Difficulty: 50 + 4 x (Awakened level + Resist Viruses); success: 4 x (Transmit Virus + Execute Program) + d100
        Assert.Equal(50 + 4 * (10 + 3), Virii.Difficulty(10, 3));
        Assert.Equal(4 * (5 + 2), Virii.SuccessRating(5, 2));
        Assert.Equal(20, Virii.SuccessRating(5, 0));
        Assert.Equal(Virii.Result.Success, Virii.Check(91, 90, out int margin));
        Assert.Equal(1, margin);
        Assert.Equal(Virii.Result.Deflected, Virii.Check(90, 90, out _));

        var random = new Random(7);
        for (int i = 0; i < 100; i++)
        {
            Assert.InRange(Virii.SingleSuccess(5, 2, random), 28, 127);
        }
        Assert.Equal(Virii.Result.Failure, Virii.SuccessCheck(false, false, 50, 50, 1, 0, random, out _));
        Assert.Equal(Virii.Result.Deflected, Virii.SuccessCheck(true, true, 50, 50, 1, 0, random, out _));
        // 4 x 100 + d100 against 50 + 4 x 1 always hits
        Assert.Equal(Virii.Result.Success, Virii.SuccessCheck(true, false, 50, 50, 1, 0, random, out _));

        Assert.Equal(Virii.Result.Reflected, Virii.Reflection(true, 40, 40));
        Assert.Equal(Virii.Result.Success, Virii.Reflection(true, 40, 41));
        Assert.Equal(Virii.Result.Success, Virii.Reflection(false, 40, 0));
    }

    [Fact]
    public void Corruption()
    {
        Assert.True(Virii.CorruptionLands(30, 30));
        Assert.False(Virii.CorruptionLands(31, 30));
        var random = new Random(1);
        Assert.Equal(Virii.WeaponSlot, Virii.CorruptionSlot(true, true, false, random));
        Assert.Null(Virii.CorruptionSlot(true, false, true, random));
        Assert.Null(Virii.CorruptionSlot(false, false, false, random));
        Assert.Equal(Virii.HackerToolSlot, Virii.CorruptionSlot(false, false, true, random));
        var seen = new HashSet<int?>();
        for (int i = 0; i < 50; i++)
        {
            seen.Add(Virii.CorruptionSlot(false, true, true, random));
        }
        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public void DeflectVirusEffectMatchesRetail()
    {
        // crashover2k proxy_024 13034: 80 bc 09 00 d8 00 00 7a 01 00 00 7a 01 20 08 00 28 00 cd cc 4c 3e
        Assert.Equal(0x28000820u, Virii.DeflectVirus1Fx);
        Assert.Equal(0.2f, Virii.DeflectEffectSeconds);
        Assert.Equal((ushort)378, Virii.DeflectVirus1Ability);
    }

    [Fact]
    public void CorrodeAndDestroy()
    {
        Assert.Equal(AbilityEffects.Corrosion.NoItem, AbilityEffects.CorrodeItem(false, 5, 2, out _));
        Assert.Equal(AbilityEffects.Corrosion.Indestructible, AbilityEffects.CorrodeItem(true, 0, 2, out int same));
        Assert.Equal(0, same);
        Assert.Equal(AbilityEffects.Corrosion.Corroded, AbilityEffects.CorrodeItem(true, 5, 2, out int three));
        Assert.Equal(3, three);
        Assert.Equal(AbilityEffects.Corrosion.NoStabilityLeft, AbilityEffects.CorrodeItem(true, 2, 2, out _));
        // A negative amount raises stability (tempmods.py SnowballProc's CorrodeItem(item, -1)); above 7 counts as none left
        Assert.Equal(AbilityEffects.Corrosion.NoStabilityLeft, AbilityEffects.CorrodeItem(true, 7, -1, out int eight));
        Assert.Equal(8, eight);

        Assert.Equal(AbilityEffects.Destruction.PlayerItemRemoved, AbilityEffects.DestroyItem(true, 4, Virii.HackerToolSlot));
        Assert.Equal(AbilityEffects.Destruction.Indestructible, AbilityEffects.DestroyItem(true, 0, Virii.WeaponSlot));
        Assert.Equal(AbilityEffects.Destruction.NpcWeaponRemoved, AbilityEffects.DestroyItem(false, 0, Virii.WeaponSlot));
        Assert.Equal(AbilityEffects.Destruction.NpcSlotEmpty, AbilityEffects.DestroyItem(false, 0, Virii.HackerToolSlot));

        // The Crash virus corrodes every equipped item by 2 (the 100% chance always passes); indestructible ones stay
        Assert.Equal(new[] { 3, 0, -1 }, AbilityEffects.CrashCorrosion(new[] { 5, 0, 1 }, new Random(2)));
    }

    [Fact]
    public void DisableTakesDependentsFirst()
    {
        var has = new HashSet<ushort> { 10, 12 };
        Assert.Equal(new ushort[] { 10, 12, 5 }, AbilityEffects.AbilitiesToDisable(5, new ushort[] { 10, 11, 12 }, has.Contains));
        Assert.Equal(new ushort[] { 5 }, AbilityEffects.AbilitiesToDisable(5, null, has.Contains));
        Assert.Equal(0x28000437u, AbilityEffects.DisableFx);
    }

    [Fact]
    public void AugmentTablesAreEmpty()
    {
        // augments.py: ten empty tables in MasterAugmentTable's eleven places, nothing past the end
        Assert.Equal(11, Augments.MasterAugmentTable.Length);
        for (int c = -1; c <= 12; c++)
        {
            Assert.Empty(Augments.GetAugmentTable(c));
        }
    }
}
