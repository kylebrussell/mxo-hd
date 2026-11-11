using System.Runtime.CompilerServices;
using hds;

namespace hds.Tests;

public class AbilityCastTests
{
    private static ClientData Data() => (ClientData)RuntimeHelpers.GetUninitializedObject(typeof(ClientData));

    [Fact]
    public void NoCastAtFirst()
    {
        ClientData data = Data();
        data.castEndsAt = -1;
        Assert.False(AbilityCooldowns.IsCasting(data, 100));
    }

    [Fact]
    public void ACastBlocksUntilItEnds()
    {
        ClientData data = Data();
        AbilityCooldowns.StartCast(data, 100, 2f);
        Assert.True(AbilityCooldowns.IsCasting(data, 101.2));
        Assert.False(AbilityCooldowns.IsCasting(data, 102));
    }

    [Fact]
    public void BusyDenialHasRetailShape()
    {
        byte[] record = AbilityCooldowns.BuildDenied(0x1ae, AbilityCooldowns.DeniedBusy);
        Assert.Equal(new byte[] { 0x80, 0xba, 0xae, 0x01, 0x00, 0x00, 0x6b, 0x06 }, record.Take(8).ToArray());
        Assert.Equal(19, record.Length);
    }

    [Fact]
    public void PlayerEffectUsesTheRecipientsOwnAndOtherAttributeIndexes()
    {
        Assert.Equal(Convert.FromHexString("02000280808080300409002807"),
            ServerPackets.BuildPlayerAbilityEffect(0x28000904, 7, true));
        Assert.Equal(Convert.FromHexString("028080800c0409002807"),
            ServerPackets.BuildPlayerAbilityEffect(0x28000904, 7, false));
    }
}
