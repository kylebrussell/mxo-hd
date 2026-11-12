using hds;
using hds.shared;

namespace hds.Tests;

public class CoderPetTests
{
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

    [Fact]
    public void SummonLevelsFollowSimulacraScript()
    {
        // simulacra.py *_Subject: sim_clamp(min, max, subject.Level)
        Assert.Equal((1, 4), (CoderPets.Summons[27].MinLevel, CoderPets.Summons[27].MaxLevel));
        Assert.Equal((35, 50), (CoderPets.Summons[594].MinLevel, CoderPets.Summons[594].MaxLevel));
        Assert.Equal((4, 7), (CoderPets.Summons[22].MinLevel, CoderPets.Summons[22].MaxLevel));
        Assert.Equal((44, 50), (CoderPets.Summons[617].MinLevel, CoderPets.Summons[617].MaxLevel));
        Assert.Equal(CoderPets.Kind.Healer, CoderPets.Summons[602].Kind);
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

    [Fact]
    public void DetonateValueFollowsTempmods()
    {
        // DetonateProxyProc: min(health, max/4), at least max/10
        Assert.Equal(487, CoderPets.DetonateValue(1950, 1950));
        Assert.Equal(300, CoderPets.DetonateValue(300, 1950));
        Assert.Equal(195, CoderPets.DetonateValue(20, 1950));
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
        mob.setRsiHex("d2040058");
        mob.setWeapon(0x0a06);
        mob.setXPos(17057.809936523438);
        mob.setYPos(495.0);
        mob.setZPos(3475.392822265625);
        mob.pet = new CoderPets.Pet { Owner = Owner(0xef02), Mob = mob, SimulacraId = 2, Kind = CoderPets.Kind.Nuker };
        byte[] creation = mob.getCreationData().GetCreationAttributes();
        Assert.Equal(retail, Hex(creation));
        Assert.Equal(new byte[] { 0xb8, 0x9d }, mob.getCreationData().GetGoid());
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
}
