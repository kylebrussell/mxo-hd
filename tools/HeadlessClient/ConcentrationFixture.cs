using System.Buffers.Binary;
using hds;

namespace HeadlessClient;
public static class ConcentrationFixture
{
    public static ushort[] Abilities(bool loaded) => loaded
        ? new ushort[] { 2, 17, 1006, 360, 133, 261, 628 }
        : new ushort[] { 2, 17, 1006, 360, 133, 261 };
    // Vector_abilityloadoutchange lines 31127–31128. Only the selected view and spawn are rebound.
    private const string StockInsidious = "80 b9 68 01 13 00 27 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 15 00 00 00 49 23 07 6b 00 00 00 00 00";

    public static byte[] InsidiousRequest(ViewCreation target)
    {
        var request = WireFormat.FromHex(StockInsidious);
        BinaryPrimitives.WriteUInt32LittleEndian(request.AsSpan(4), DuelPackets.Handle(target.ViewId, target.SpawnId));
        return request;
    }

    public static async Task PrepareEnemyAsync(HeadlessPlayer enemy)
    {
        enemy.SendChat("?org 2");enemy.SendChat("?pvpflag off");enemy.SendChat("?pvpflag on");
        await enemy.WaitForViewUpdateAsync(2,"02 80 80 80 80 80 80 01 00 00 00 01");
    }
}
