using hds;
using hds.shared;

namespace hds.Tests;

// Expected bytes come from retail captures under research/packet-dumps (see the comments on each builder).
public class RangedWeaponTests
{
    private static string Hex(byte[] bytes) => StringUtils.bytesToString_NS(bytes).ToLowerInvariant();
    private static float F32(string hex) => BitConverter.ToSingle(StringUtils.hexStringToBytes(hex), 0);

    [Theory]
    [InlineData("Revolver0", RangedWeapons.ClassPistol)]            // Redpill Special: 0x38 in retail
    [InlineData("SemiAutoPistolSystem", RangedWeapons.ClassPistol)] // 0x38 in retail
    [InlineData("AutomaticRifle1", RangedWeapons.ClassRifle)]       // Chopper Jumper's rifle: 0x37
    [InlineData("e50autorifle1001", RangedWeapons.ClassRifle)]
    [InlineData("e50shotgun2006", RangedWeapons.ClassRifle)]        // shotgun NPCs: 0x37
    [InlineData("MesonRifle3B", RangedWeapons.ClassRifle)]
    [InlineData("e50submachinegun1001", RangedWeapons.ClassSubmachinegun)] // 0x39
    [InlineData("Submachinegun3", RangedWeapons.ClassSubmachinegun)]
    [InlineData("SleepSubgun", RangedWeapons.ClassSubmachinegun)]
    [InlineData("DualPistol6", RangedWeapons.ClassDualPistol)]
    [InlineData("DualSubmachinegun2", RangedWeapons.ClassDualSubmachinegun)]
    [InlineData("Mclothing_Shirt_A5_C8", 0)]
    [InlineData("", 0)]
    public void WeaponNamesMapToRetailAttackClasses(string name, byte expected)
    {
        Assert.Equal(expected, RangedWeapons.AttackClassForName(name));
    }

    [Fact]
    public void NpcPistolShotMatchesRetail()
    {
        // Tutorial capture (crashover2k ..._uriah.log.txt line 14258): Alex (view 6) hits mxoemu (02 00 ea 00).
        string retail = "02" + "2a" + "04" + "010c38" + "8100" + "0200ea00" + "00" + "a060e745" + "0060c744" + "76b436c6";
        Assert.Equal(retail, Hex(ServerPackets.BuildNpcRangedShot(0x04, RangedWeapons.ClassPistol, true, 0x00ea0002,
            F32("a060e745"), F32("0060c744"), F32("76b436c6"))));
    }

    [Fact]
    public void NpcRifleShotMatchesRetail()
    {
        // luizoe old-sniffer Unknown log line 32745: Chopper Jumper (AutomaticRifle1) hits bboysex (02 00 ec 00).
        // Retail sent it in a part-06 update (with attributes); the part-02 record is byte-identical.
        string retailPart = "2a" + "01" + "010c37" + "8100" + "0200ec00" + "00" + "e2d847c4" + "004030c4" + "98beb946";
        Assert.Equal("02" + retailPart, Hex(ServerPackets.BuildNpcRangedShot(0x01, RangedWeapons.ClassRifle, true,
            0x00ec0002, F32("e2d847c4"), F32("004030c4"), F32("98beb946"))));
    }

    [Fact]
    public void NpcMissCarriesTheMissMarker()
    {
        // Tutorial capture line 14387: Alex's fifth shot misses: 2a 05 01 0c 38 00 10 02 00 ea 00 00 <pos>.
        string retail = "02" + "2a" + "05" + "010c38" + "0010" + "0200ea00" + "00" + "a060e745" + "0060c744" + "76b436c6";
        Assert.Equal(retail, Hex(ServerPackets.BuildNpcRangedShot(0x05, RangedWeapons.ClassPistol, false, 0x00ea0002,
            F32("a060e745"), F32("0060c744"), F32("76b436c6"))));
    }

    [Fact]
    public void PlayerRifleShotUsesTheRifleClass()
    {
        string shot = Hex(ServerPackets.BuildRangedShot(0x02, 0x00a30004, 1f, 2f, 3f, 0x11223344, 0x4b, false, true,
            RangedWeapons.ClassRifle));
        Assert.StartsWith("020003" + "2802010c37" + "8100", shot);
    }

    [Fact]
    public void DistanceForFalloffIsInMetres()
    {
        // 600 world units apart = 6 m, a typical retail gunfight distance (median 626 units in the captures).
        byte[] player = NumericalUtils.doublesToLtVector3d(1000, 0, 2000);
        Assert.Equal(6f, CombatCalculator.CalculateDistanceMetres(player, 1600, 0, 2000), 3);
        Assert.Equal(600f, CombatCalculator.CalculateDistance(player, 1600, 0, 2000), 3);
    }

    [Theory]
    [InlineData("IWRf", RangedWeapons.ClassRifle)]
    [InlineData("IWHg", RangedWeapons.ClassPistol)]
    [InlineData("IWSm", RangedWeapons.ClassSubmachinegun)]
    [InlineData("IWDH", RangedWeapons.ClassDualPistol)]
    [InlineData("IWDS", RangedWeapons.ClassDualSubmachinegun)]
    [InlineData("ICTh", 0)]
    public void CategoriesMapToAttackClasses(string category, byte expected)
    {
        Assert.Equal(expected, RangedWeapons.AttackClassForCategory(category));
    }

    [Fact]
    public void WeaponDataFromTheClientParses()
    {
        // data/weapons.csv, decoded from the client's gameobjects.gob.
        string path = DataLoader.DataPath("weapons.csv");
        var stats = RangedWeapons.ParseStats(File.ReadAllLines(path));
        Assert.True(stats.Count > 600);

        var redpill = stats[2565];
        Assert.Equal("Revolver0", redpill.Name);
        Assert.Equal(RangedWeapons.ClassPistol, redpill.AttackClass);
        Assert.Equal(16f, redpill.RangeMetres);
        Assert.Equal(4.5f, redpill.ReuseSeconds);
        Assert.Equal(50, redpill.Noise);  // retail's first shot with it set NoiseLevel 0x32
        Assert.Equal(1, redpill.BaseDamage);

        var rifle = stats[692];           // AutomaticRifle1, Chopper Jumper's gun: class 0x37 in retail
        Assert.Equal(RangedWeapons.ClassRifle, rifle.AttackClass);
        Assert.Equal(22f, rifle.RangeMetres);
        Assert.Equal(3.5f, rifle.ReuseSeconds);

        Assert.Equal(RangedWeapons.ClassDualSubmachinegun, stats[2503].AttackClass); // DualSubmachinegun2
        Assert.Equal(2.5f, stats[2503].ReuseSeconds);
    }

    [Fact]
    public void ParseStatsSkipsCommentsHeaderAndNonGuns()
    {
        var stats = RangedWeapons.ParseStats(new[]
        {
            "# comment",
            "goid,name,category,baseDamage,rangeMetres,reuseSeconds,reqCharLevel,reqAbility,reqAbilityLevel,noise",
            "6892,Submachinegun3,IWSm,7,20,3.0,20,,,65",
            "1,NotAGun,ICTh,0,0,0,0,,,0",
        });
        Assert.Single(stats);
        Assert.Equal(3.0f, stats[6892].ReuseSeconds);
        Assert.Equal(RangedWeapons.ClassSubmachinegun, stats[6892].AttackClass);
    }

    [Fact]
    public void NoiseLevelFollowsRetailSequence()
    {
        // Retail tutorial fight with the Redpill Special (noise 50): 0x32, 0x4b, 0x57, 0x5d, 0x60.
        byte noise = 0;
        var seen = new List<byte>();
        for (int i = 0; i < 5; i++)
        {
            noise = RangedWeapons.NextNoiseLevel(noise, 50);
            seen.Add(noise);
        }
        Assert.Equal(new byte[] { 0x32, 0x4b, 0x57, 0x5d, 0x60 }, seen);
    }

    [Theory]
    [InlineData(RangedWeapons.ClassPistol, 4.5f, 16f)]
    [InlineData(RangedWeapons.ClassRifle, 3.5f, 22f)]
    [InlineData(RangedWeapons.ClassSubmachinegun, 3.0f, 20f)]
    [InlineData(RangedWeapons.ClassDualPistol, 4.0f, 16f)]
    [InlineData(RangedWeapons.ClassDualSubmachinegun, 2.5f, 20f)]
    public void ClassDefaultsMatchTheClientData(byte attackClass, float reuse, float range)
    {
        Assert.Equal(reuse, RangedWeapons.DefaultReuseSeconds(attackClass));
        Assert.Equal(range, RangedWeapons.DefaultRangeMetres(attackClass));
    }
}
