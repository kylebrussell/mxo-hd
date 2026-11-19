using HeadlessClient;
using MxoHd.PacketResearch;

namespace NativeQa;

/// <summary>Exercise the existing disposable chat bridge and require ordinary loadout wire evidence.</summary>
internal static class OperativePassiveLoadoutQa
{
    public const string Sweep3 = "ability-parity-operative-sweep-3";
    public static bool Supports(string mode) => mode is "ability-parity-operative-sweep-2" or Sweep3;

    public static async Task VerifyAsync(HeadlessPlayer actor, string mode)
    {
        ushort ability = mode == "ability-parity-operative-sweep-2" ? (ushort)363 : (ushort)116;
        ushort[] stats = ability == 363 ? new ushort[] { 1143, 1027, 575 } : new ushort[] { 1140, 1144 };
        int value = ability == 363 ? 10 : 7;
        string abilityHex = WireFormat.ToHex(BitConverter.GetBytes(ability));

        foreach (bool unload in new[] { true, false })
        {
            int start = actor.World.RpcSnapshot().Count;
            var ack = BytePattern.Parse($"80 {(unload ? "b3" : "b2")} {abilityHex}");
            actor.SendChat($"?qa{(unload ? "unload" : "load")} {ability}");
            await actor.WaitUntilAsync(() =>
            {
                var packets = actor.World.RpcSnapshot().Skip(start).ToArray();
                return packets.Any(rpc => ack.IsMatch(rpc.Bytes)) && stats.All(stat => packets.Any(rpc =>
                    ManageBonusRecord.TryDecode(rpc.Bytes, out var bonus) && bonus!.Family == 0x45 &&
                    bonus.SourceId == ability && bonus.TargetId == stat && bonus.SignedValue == value &&
                    bonus.DurationBits == 0 && bonus.AddRemoveType == (unload ? 1 : 0)));
            }, $"Operative {ability} ordinary acknowledgement and all modifier changes (unload={unload})",
                HeadlessPlayer.DefaultTimeout);
        }
        Console.WriteLine($"  Operative {ability} ordinary loadout acknowledgement and {stats.Length} scoped off/on records verified");
    }
}
