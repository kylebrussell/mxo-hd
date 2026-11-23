using hds;

namespace hds.Tests;

public class NpcEvasionTests
{
    [Fact]
    public void ShieldProfileRequiresTheCapturedPositionModeAndEffect()
    {
        var mob = new Mob { xBase = 97619, yBase = 695, zBase = 10761 };
        mob.setDistrict(1);
        mob.setName("Crossbones Tar");
        mob.setLevel(7);
        mob.setRsiHex("59030008");
        string[] row = "1;slums;Crossbones Tar;7;337;337;59030008;97619;695;10761;229;false;false;18;100000;b51c00c6;;2;9;f9070000;;1;671091229".Split(';');

        Assert.NotNull(NpcEvasion.FromSpawnRow(row, mob));
        row[13] = "34";
        Assert.Null(NpcEvasion.FromSpawnRow(row, mob));
        row[13] = "18";
        row[22] = "0";
        Assert.Null(NpcEvasion.FromSpawnRow(row, mob));
        row[22] = "671091229";
        mob.xBase = 105371;
        mob.zBase = 6654;
        Assert.Null(NpcEvasion.FromSpawnRow(row, mob));
        Assert.Null(NpcEvasion.FromSpawnRow(row[..22], mob));
    }

    [Fact]
    public void MeterReleaseRestoresModeOnTheNpcAttributePart()
    {
        Assert.Equal(Convert.FromHexString("048080808010a6"), NpcEvasion.BuildUpdate(166, CombatInterlock.InCombatFlag));
        Assert.Equal(Convert.FromHexString("0480808080900080060000100022"), NpcEvasion.BuildUpdate(0, CombatInterlock.InCombatFlag));
    }
}
