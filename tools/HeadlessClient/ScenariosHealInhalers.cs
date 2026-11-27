using hds;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task HealInhalersUse(ScenarioContext context)
    {
        var doses = new[] { (5, 39757u, 2), (6, 39758u, 1), (7, 39759u, 1), (8, 39760u, 1), (9, 39761u, 1), (10, 2399u, 1) };
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("InhalerOwner") { Level = 50, Health = 200, MaxHealth = 500, Items = doses },
            new CharacterSeed("InhalerControl") { Level = 5, Health = 499, MaxHealth = 500, X = 17120, Items = doses });
        var actor = players[0];
        var control = players[1];

        control.SendRpc("80 c6 06 00");
        await control.WaitForTextAsync("character level is too low");
        Check.Equal(1L, await Count(control, 6), "The level gate retains its dose");
        control.SendRpc("80 c6 05 00");
        await control.WaitForRpcAsync(Modifier(716, 15, off: false));
        await control.WaitForViewUpdateAsync(2, "02 80 80 80 40 f4 01");

        Cast(actor, 716);
        await Refused(actor, 716, AbilityRefusals.RequiredItemCategory);
        Check.Equal(0, actor.Received("80 ac cc 02").Count, "Bare activation starts no cast");

        var tiers = new[] { (15, 4), (30, 8), (45, 11), (60, 15), (90, 23) };
        int health = 200;
        for (int index = 0; index < tiers.Length; index++)
        {
            ushort ability = (ushort)(716 + index);
            ushort item = (ushort)(39757 + index);
            byte slot = (byte)(5 + index);
            var (total, tick) = tiers[index];
            if (index % 2 == 0) actor.SendRpc($"80 c6 {slot:x2} 00");
            else CastSweepItem(actor, ability, slot);

            var cast = await actor.WaitForRpcAsync("80 ac " + Le(ability));
            Check.Equal(1f, BitConverter.ToSingle(cast.Bytes, 14), "Every inhaler casts in one second");
            await actor.WaitForRpcAsync(index == 0 ? "69 01 05 00 00 00 00 00 00 00" : $"5f {Le((ushort)slot)} 01");
            await actor.WaitForRpcAsync(Modifier(ability, total, off: false));
            await actor.WaitForRpcAsync(Timer(711, start: true));
            Check.Equal(1, actor.Received(Timer(item, start: true)).Count, "The used item receives its own 10-second timer");

            if (index == 0)
            {
                actor.SendRpc("80 c6 06 00");
                await Refused(actor, 717, AbilityRefusals.ReuseWait);
                actor.SendRpc("80 c6 0a 00");
                await Refused(actor, 190, AbilityRefusals.ReuseWait);
                Check.Equal(1L, await Count(actor, 6), "Shared reuse retains the next inhaler");
                Check.Equal(1L, await Count(actor, 10), "The same timer protects the ordinary Health Pill");
            }

            for (int pulse = 0; pulse < 3; pulse++)
            {
                health += tick;
                await actor.WaitForViewUpdateAsync(2, "02 80 80 80 40 " + Le((ushort)health));
            }
            await actor.WaitForRpcAsync(Modifier(ability, total, off: true), TimeSpan.FromSeconds(14));
            await actor.WaitUntilAsync(() => actor.Received(Timer(711, start: false)).Count == index + 1,
                "The shared Health Pill timer ends", TimeSpan.FromSeconds(5));
            Check.Equal(3, Pulses(actor, ability), "Exactly three heals happen before expiry");
        }

        Check.Equal(383, health, "All five tiers heal for a total of 183 HP in this fixture");
        Check.Equal(1L, await Count(actor, 5), "One of the two first-tier doses remains");
        Check.Equal(1, Pulses(control, 716), "The cap permits only the one-point heal from 499 to 500");
        Check.Equal(0, actor.World.Problems.Count + control.World.Problems.Count, "Inhaler frames stay readable");

        Task<long> Count(HeadlessPlayer player, int slot) => context.Database.LongAsync(
            $"SELECT COALESCE(SUM(count),0) FROM inventory WHERE charId={player.CharId} AND slot={slot}");

        int Pulses(HeadlessPlayer player, ushort ability) => context.Server.DebugLog().Split('\n').Count(line =>
            line.Contains($"[VITALS] char={player.CharId} ability={ability} HOT raw="));

        static string Modifier(ushort ability, int total, bool off) =>
            $"80 bc 1d 00 ?? ?? 00 {Le((uint)ability)} 6c 00 {Le((uint)total)} {(off ? "01" : "00")} 00 00 20 41";

        static string Timer(ushort id, bool start) =>
            WireFormat.ToHex(AbilityCooldowns.BuildTimer(id, AbilityCooldowns.GroupTimer, 10, start));

        static async Task Refused(HeadlessPlayer player, ushort ability, uint reason)
        {
            var row = await player.WaitForRpcAsync("80 ba " + Le(ability));
            Check.That(AbilityRefusals.TryDecode(row.Bytes, out var denial) && denial.ErrorID == reason,
                $"Ability {ability} refusal reason is {reason}");
        }
    }
}
