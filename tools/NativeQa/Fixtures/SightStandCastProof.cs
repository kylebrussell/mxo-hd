using hds;
using HeadlessClient;

namespace NativeQa;

internal static class SightStandCastProof
{
    internal static async Task<ViewCreation> PickAsync(HeadlessPlayer actor, ushort style, string stockStyle)
    {
        await actor.EnterWorldAsync();
        actor.SendRpc(stockStyle);
        await actor.WaitForViewUpdateAsync(2, $"02 80 80 80 80 80 10 {StockAbilityProfile.LittleEndianHex(style)} 00 00");
        actor.SendChat("?combatdepth foundations-sight-pick");
        await actor.WaitForTextAsync("Select QA Sight Target 2 m ahead");
        var target = await actor.WaitForViewNamedAsync("QA Sight Target");
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendRpc($"81 51 {WireFormat.ToHex(BitConverter.GetBytes(handle))}");
        return target;
    }

    internal static async Task CheckBlockedAsync(NativeQaBootstrapContext context, ViewCreation target,
        (ushort Ability, float Reuse, string Capture)[] samples)
    {
        var actor = context.StockProbe!;
        var server = context.ActualServer!;
        uint handle = DuelPackets.Handle(target.ViewId, target.SpawnId);
        actor.SendChat("?combatdepth foundations-sight-blocked");
        await actor.WaitForTextAsync("QA Sight Target is 6 m west");
        NativeQaReadiness.RequireNoAmbientAggro(server, actor);
        foreach (var (ability, reuse, capture) in samples)
        {
            int cursor = actor.World.RpcSnapshot().Count;
            int logStart = server.DebugLog().Length;
            byte[] request = Modes.StarterHackerControlTargetMode.WithHandle(capture, handle);
            Check.Equal(43, request.Length, $"Full stock request for {ability}");
            actor.SendRpc(request);
            await Modes.EvidenceRetestWire.TimerAsync(actor, cursor, ability, AbilityCooldowns.OwnTimer, reuse);
            await actor.WaitUntilAsync(() => server.DebugLog()[logStart..].Contains(
                $"[ABILITY] Sight blocked ability {ability} on view {target.ViewId}", StringComparison.Ordinal),
                $"The same selected handle reaches the sight gate for {ability}", HeadlessPlayer.DefaultTimeout);
            string result = server.DebugLog()[logStart..];
            Check.That(!result.Contains($"[FREECAST-VIRAL] ability={ability}") &&
                !result.Contains($"[EFFECTS] {ability} on ") && !result.Contains($"[ABILITY] {ability} lands on "),
                $"Obstructed {ability} reaches neither the contest nor any primary or splash effect");
            if (AbilityCooldowns.TryGet(ability, out var cooldown) && cooldown.Group != 0 && cooldown.GroupSeconds > 0)
            {
                byte[] groupOff = AbilityCooldowns.BuildTimer(cooldown.Group, AbilityCooldowns.GroupTimer, cooldown.GroupSeconds, false);
                await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Skip(cursor)
                    .Any(frame => frame.Bytes.AsSpan().SequenceEqual(groupOff)),
                    $"Shared reuse expires for {ability}", TimeSpan.FromSeconds(cooldown.GroupSeconds + 4));
            }
        }
        Check.Equal(0, actor.World.Problems.Count, "Sight stand protocol framing");
    }
}
