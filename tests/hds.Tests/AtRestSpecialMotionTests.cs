using System;
using hds;
using Xunit;

namespace hds.Tests;

public class AtRestSpecialMotionTests
{
    private static readonly string ActionTable = Load();

    private static string Load()
    {
        string root = AppContext.BaseDirectory;
        while (!System.IO.File.Exists(System.IO.Path.Combine(root, "hds.sln"))) root = System.IO.Path.GetDirectoryName(root)!;
        return System.IO.File.ReadAllText(System.IO.Path.Combine(root, "hds", "world", "Structures", "ClientDB.cs"));
    }

    [Theory]
    [InlineData((ushort)279, (byte)0x40, "FreeAttackWeaponSMKnifeThrow")]
    [InlineData((ushort)280, (byte)0x40, "FreeAttackWeaponSMKnifeThrow")]
    [InlineData((ushort)130, (byte)0x5d, "FreeAttackWeaponSMPistolHinderingShot")]
    [InlineData((ushort)454, (byte)0x60, "FreeAttackWeaponSMRifleDeadlyShot")]
    public void EachSpecialUsesItsNamedFreeAttackActionInTheTargetedMotionRecord(ushort ability, byte action, string name)
    {
        Assert.Equal(action, AtRestSpecialMotion.Actions[ability]);
        // The action byte is the client action list entry (hds/world/Structures/ClientDB.cs action table).
        Assert.Contains($"{action:x2} \"{name}\"", ActionTable);
        uint target = 0x000b0004;
        byte[] own = StealthShieldAbilities.BuildThrow(true, 7, ability, target, action);
        Assert.Equal(new byte[] { 2, 0, 1, 0x20, 7, 1, StealthShieldAbilities.Aggro, action }, own[..8]);
        Assert.Equal(BitConverter.GetBytes(ability), own[8..10]);
        Assert.Equal(BitConverter.GetBytes(target), own[10..14]);
        Assert.Equal(own[2..], StealthShieldAbilities.BuildThrow(false, 7, ability, target, action));
    }

    [Fact]
    public void TheKnifeThrowDefaultIsUnchanged()
    {
        Assert.Equal(StealthShieldAbilities.KnifeThrow, AtRestSpecialMotion.KnifeThrow);
        Assert.Equal(StealthShieldAbilities.BuildThrow(true, 7, 1110, 0x00440022),
            StealthShieldAbilities.BuildThrow(true, 7, 1110, 0x00440022, StealthShieldAbilities.KnifeThrow));
        Assert.Contains("40 \"FreeAttackWeaponSMKnifeThrow\"", ActionTable);
    }
}
