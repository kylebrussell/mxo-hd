using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AwakenedHyperDefenses(ScenarioContext context)
    {
        // Captured full Hyper-Block use packet from ABILITY-STATUS.md. Only the ability ID varies.
        const string cast = "80 b9 4b 04 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 40 30 37 26 00 00 00 00 08 6b 00 00 00 00 00";
        var actor = await context.AddPlayerAsync(NearNpc("HyperDefense") with
        {
            InnerStrength = 2000,
            MaxInnerStrength = 2000,
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 50), (10, 50), (1099, 50), (1100, 50), (1101, 50) }
        });
        await actor.EnterWorldAsync();

        Task<RpcRecord> Modifier(ushort ability, ushort stat, int value, bool remove) =>
            actor.WaitForRpcAsync(r => ManageBonusRecord.TryDecode(r.Bytes, out var modifier) &&
                modifier!.SourceId == ability && modifier.TargetId == stat && modifier.SignedValue == value &&
                modifier.AddRemoveType == (remove ? 1 : 0),
                $"Hyper defense {ability} stat {stat} {(remove ? "off" : "on")}");

        (ushort Ability, ushort Stat, int Value)[] moves =
        {
            (1101, 1011, 47),
            (1100, 183, 77),
            (1099, 1009, 52),
            (10, 1010, 51)
        };
        for (int index = 0; index < moves.Length; index++)
        {
            var current = moves[index];
            byte[] request = WireFormat.FromHex(cast);
            BitConverter.GetBytes(current.Ability).CopyTo(request, 2);
            actor.SendRpc(request);
            if (index > 0)
            {
                var previous = moves[index - 1];
                await Modifier(previous.Ability, previous.Stat, previous.Value, true);
            }
            await Modifier(current.Ability, current.Stat, current.Value, false);
        }
        Check.Equal(0, actor.World.Problems.Count, "Hyper defenses use valid stock RPC records");
    }
    private static async Task AwakenedCheapShot(ScenarioContext context)
    {
        var actor = await context.AddPlayerAsync(NearNpc("AwakenedSpecial") with
        {
            InnerStrength = 500,
            MaxInnerStrength = 500,
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 50), (17, 50), (198, 50) }
        });
        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("Trainman");
        actor.SendRpc($"40 {HandleOf(target)} 01 6b");
        ushort combatView = await WaitForCombatView(actor);
        await WaitForEntry(actor, combatView);
        Cast(actor, 198, target.ViewId);
        await AssertRuleMoveAsync(actor, combatView, 285, "Cheap Shot lands its shipped Self Defense move");
        Check.Equal(0, actor.World.Problems.Count, "Cheap Shot uses valid combat records");
    }
}
