using System.Buffers.Binary;
using HeadlessClient;

namespace NativeQa;

internal static class CoderSweepRetest
{
    internal static async Task<object> VerifyAsync(ServerInstance server, HeadlessPlayer csr)
    {
        await csr.EnterWorldAsync();
        var friend = await csr.WaitForViewNamedAsync("QAFriend");
        var dummy = await csr.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);

        // Stock PR312 Coder capture 88a1d93e477a:3167, 5455 and 9302. Only the live Attack target changes.
        csr.SendRpc("80b95202000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        await csr.WaitForRpcAsync("81 76", TimeSpan.FromSeconds(20));
        csr.SendRpc("81 7c fe 05 02");
        csr.SendRpc($"81 7b fe 02 {Handle(friend)}");
        await csr.WaitUntilAsync(() => server.DebugLog().Contains("[PET] Order refused: Choose a hostile NPC", StringComparison.Ordinal),
            "The Coder pet refuses the player target while out of combat", HeadlessPlayer.DefaultTimeout);
        Check.That(!server.DebugLog().Contains("Simulacrum hits Melee Dummy", StringComparison.Ordinal),
            "The denied Attack did not redirect the pet to Melee Dummy");

        // Stock Detonate and targeted Static Blast from PR312, b0d0eb7ff285:10220 and :6342.
        csr.SendRpc("80b92200000000000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        await csr.WaitUntilAsync(() => server.DebugLog().Contains("to 1 NPC(s) within 8 m", StringComparison.Ordinal),
            "Out-of-combat Detonate damages the isolated NPC", HeadlessPlayer.DefaultTimeout);
        await csr.WaitForRpcAsync("81 77");

        csr.SendRpc($"40 {Handle(dummy)} 01 6b");
        await csr.WaitForRpcAsync("47");
        csr.SendChat("?combatdepth fixture-disengage");
        await csr.WaitForTextAsync("Fixture combat ended");
        byte[] cast = Convert.FromHexString("80b92100050003000000000000000000000000000000000000000000ffff7f7f00000000086b0000000000");
        BinaryPrimitives.WriteUInt32LittleEndian(cast.AsSpan(4), PackedHandle(dummy));
        csr.SendRpc(cast);
        await csr.WaitUntilAsync(() => server.DebugLog().Contains(
            "[FREECAST-VIRAL] ability=33 source=CSR_Tester target=Melee Dummy", StringComparison.Ordinal),
            "Static Blast reaches its ordinary free-cast contest after fixture disengage", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, csr.World.Problems.Count, "Stock Coder requests and combat teardown parse cleanly");
        return new { PlayerAttackRefused = true, Detonate = 34, StaticBlast = 33, InterlockCleared = true };
    }

    private static uint PackedHandle(ViewCreation view) => (uint)view.ViewId | ((uint)view.SpawnId << 16);
    private static string Handle(ViewCreation view) => WireFormat.ToHex(BitConverter.GetBytes(PackedHandle(view)));
}
