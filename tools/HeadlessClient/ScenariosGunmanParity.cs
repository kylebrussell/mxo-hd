using MxoHd.PacketResearch;
using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task GunmanUpgradesAndEvasive(ScenarioContext context)
    {
        ushort[] ids = { 2, 17, 129, 242, 1070, 1074, 1075, 1076, 1060 };
        var actor = await context.AddPlayerAsync(NearNpc("GunmanUpgrade") with
        {
            Items = new[] { (105, 2565u, 1) },
            OwnedLoadedAbilities = ids.Select(id => (id, (ushort)50)).ToArray(),
            InnerStrength = 2000,
            MaxInnerStrength = 2000
        });
        await actor.EnterWorldAsync();
        var target = await actor.WaitForViewNamedAsync("Trainman");

        Task<RpcRecord> Modifier(ushort id, ushort stat, int value, bool remove = false) =>
            actor.WaitForRpcAsync(r => ManageBonusRecord.TryDecode(r.Bytes, out var m) &&
                m!.SourceId == id && m.TargetId == stat && m.SignedValue == value &&
                m.AddRemoveType == (remove ? 1 : 0), $"ability {id}: stat {stat}, value {value}, removal {remove}",
                remove ? TimeSpan.FromSeconds(18) : HeadlessPlayer.DefaultTimeout);

        var upgrades = new (ushort Ability, int Accuracy)[] { (1070, 4), (1074, 9), (1075, 4), (1076, 3), (1060, 4) };
        foreach (var (current, index) in upgrades.Select((value, index) => (value, index)))
        {
            Cast(actor, current.Ability, target.ViewId);
            if (index > 0)
            {
                var previous = upgrades[index - 1];
                await Modifier(previous.Ability, 524, previous.Accuracy, true);
            }
            await Modifier(current.Ability, 524, current.Accuracy);
        }

        actor.SendRpc("80 b9 81 00 00 00");
        await actor.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 81 00 00 00");
        actor.SendRpc($"40 {HandleOf(target)} 01 6b");
        var combatView = await WaitForCombatView(actor);
        await WaitForEntry(actor, combatView);
        Cast(actor, 242, target.ViewId);
        await Modifier(242, 1011, 20);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Count(r =>
            ManageBonusRecord.TryDecode(r.Bytes, out var m) && m is { SourceId: 242, AddRemoveType: 0, SignedValue: 20, Duration: 12 }) == 3,
            "The landed Pistol Evasive exchange publishes all three caster defenses", HeadlessPlayer.DefaultTimeout);
        await actor.WaitUntilAsync(() => context.Server.DebugLog().Contains($"242 lands on view {target.ViewId} for "),
            "Pistol Evasive keeps the ordinary targeted hit", HeadlessPlayer.DefaultTimeout);
        await Modifier(242, 1011, 20, remove: true);
        await actor.WaitUntilAsync(() => actor.World.RpcSnapshot().Count(r =>
            ManageBonusRecord.TryDecode(r.Bytes, out var m) && m is { SourceId: 242, AddRemoveType: 1, SignedValue: 20 }) == 3,
            "All three caster defense records expire", HeadlessPlayer.DefaultTimeout);
        Check.Equal(0, actor.World.Problems.Count, "Gunman upgrade and Evasive protocol");
    }
}
