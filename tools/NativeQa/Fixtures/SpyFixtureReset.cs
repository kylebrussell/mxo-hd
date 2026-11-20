using HeadlessClient;
using hds;

namespace NativeQa;

internal static class SpyFixtureReset
{
    internal const string Reselect = "After each fixture reset, wait for the dummy to reappear and select its body again before casting.";

    internal static async Task<byte[]> VerifyAsync(NativeQaBootstrapContext context)
    {
        if (context.StockProbe is not { } actor) return Array.Empty<byte>();
        await actor.EnterWorldAsync();
        var old = await actor.WaitForViewNamedAsync(CombatFixtureLoadout.DummyName);
        CombatFixtureLoadout.AssertDummy(actor, old);

        int creationAt = actor.World.CreationSnapshot().Count;
        int viewAt = actor.World.ViewSnapshot().Count;
        actor.SendChat("?combatdepth fixture-reset");
        await actor.WaitForTextAsync("Fixture reset: Melee Dummy restored.");
        await actor.WaitUntilAsync(() => actor.World.CreationSnapshot().Skip(creationAt)
            .Any(view => view.Name == CombatFixtureLoadout.DummyName), "Reset publishes a new dummy", HeadlessPlayer.DefaultTimeout);
        var current = actor.World.CreationSnapshot().Skip(creationAt).Single(view => view.Name == CombatFixtureLoadout.DummyName);
        byte[] delete = WireFormat.FromHex($"01 00 01 01 00 {StockAbilityProfile.LittleEndianHex(old.ViewId)}");
        Check.That(actor.World.ViewSnapshot().Skip(viewAt).Any(list => list.AsSpan().IndexOf(delete) >= 0),
            "The old dummy view is deleted");
        Check.That(current.ViewId != old.ViewId && current.SpawnId != old.SpawnId &&
            current.Attributes["IsEnemy"][0] == 1 && current.Attributes["IsDead"][0] == 0 &&
            BitConverter.ToUInt16(current.Attributes["Health"]) == 30000, "The new dummy has a fresh live hostile view");

        string target = WireFormat.ToHex(BitConverter.GetBytes(DuelPackets.Handle(current.ViewId, current.SpawnId)));
        actor.SkipReceived();
        actor.SendRpc($"81 51 {target}");
        actor.SendRpc($"40 {target} 01 6b");
        await actor.WaitForRpcAsync("47", TimeSpan.FromSeconds(12));
        Check.Equal(0, actor.World.Problems.Count, "Stock targeting and Close Combat accept the new view");
        return Array.Empty<byte>();
    }
}
