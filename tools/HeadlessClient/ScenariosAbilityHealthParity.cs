using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task AbilityHealthParity(ScenarioContext context)
    {
        var players = await context.AddPlayersInWorldAsync(
            new CharacterSeed("HealthCaster")
            {
                Level = 50, Experience = (int)hds.PlayerProgression.ExperienceForLevel(50), Organization = 1,
                OwnedLoadedAbilities = new ushort[] { 2, 17, 38, 40, 45, 52, 57, 58, 60, 67, 75, 77, 95, 97,
                    98, 169, 327, 398, 399, 401, 404, 461, 466, 1006 }.Select(id => (id, (ushort)50)).ToArray()
            },
            new CharacterSeed("HealthAlly") { X = 17120, Level = 50, Health = 100, MaxHealth = 1000, Organization = 1 },
            new CharacterSeed("DotTarget") { X = 17220, Level = 50, Health = 5000, MaxHealth = 5000, Organization = 2 });
        var caster = players[0]; var ally = players[1]; var enemy = players[2];
        var allyView = await caster.WaitForViewNamedAsync(ally.Handle);
        var enemyView = await caster.WaitForViewNamedAsync(enemy.Handle);

        // Luizoe old sniffer 115308 and native CSR 455f0446826c:27053. Only the current view/spawn pair is substituted.
        caster.SendRpc(HealthCast("80 b9 4d 00 06 00 3b 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 32 00 00 00 00 00 00 00 08 6b 00 00 00 00 00", allyView));
        var castBar = await caster.WaitForRpcAsync("80 ac 4d 00");
        Check.Equal(2f, BitConverter.ToSingle(castBar.Bytes, castBar.Bytes.Length - 4), "Repair RSI cast duration");
        await caster.WaitForRpcAsync("80 bd 01 4d 00 04 00 00 80 40 01");
        await caster.WaitForRpcAsync("80 bd 01 ae 02 00 00 00 80 40 01");
        await ally.WaitUntilAsync(() => context.Server.DebugLog().Contains("Heal 77 -> HealthAlly for 9"),
            "Repair RSI lands on the wounded ally for 9", TimeSpan.FromSeconds(10));
        var healed = Regex.Match(context.Server.DebugLog(), @"\[ABILITY\] Healed (\d+) -> (\d+)");
        Check.That(healed.Success && int.Parse(healed.Groups[2].Value) - int.Parse(healed.Groups[1].Value) == 9,
            "Repair RSI changes the ally's actual HP by 9 with no healing influence");
        await ally.WaitForViewUpdateAsync(2, "02 80 80 80 40 " + Le(ushort.Parse(healed.Groups[2].Value)));

        caster.SendRpc(HealthCast("80 b9 a9 00 11 00 02 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 32 00 00 00 00 00 00 00 08 6b 00 00 00 00 00", allyView));
        await ally.WaitForRpcAsync(ModPattern(169, 1157) + " 00 00 70 41");
        Check.Equal(0, Regex.Matches(context.Server.DebugLog(), @"169 heals HealthAlly").Count,
            "Emergency Repairs has no initial healing tick");

        // Enemy and caster opt in to the normal PvP admission gate. The fixture controls the landing contest only.
        foreach (var player in new[] { caster, enemy })
        {
            player.SendChat("?pvpflag on");
            await player.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
        }
        // Retained ordinary Hacker Style action from the starter viral sight fixture.
        caster.SendRpc("80b9ee030000000000000000000000000000000000000000000000000000000090c97941086b0000000000");
        // Retail actions_combat line 16215, cast on a selected target.
        caster.SendRpc(HealthCast("80 b9 d2 01 0a 00 a1 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 08 6b 00 00 00 00 00", enemyView));
        var applied = await enemy.WaitForRpcAsync(ModPattern(466, 1157) + " 00 00 70 41", TimeSpan.FromSeconds(10));
        int total = BinaryPrimitives.ReadInt32LittleEndian(applied.Bytes.AsSpan(13));
        // Level-50 Hacker Style adds 27 and loaded Transmit Virus (95) adds 15.
        Check.That(total is >= 602 and <= 630, $"Plague Zone captured total scaled by +42% viral influence, actual {total}");
        await ally.WaitForRpcAsync(ModPattern(169, 1157, true), TimeSpan.FromSeconds(20));
        await enemy.WaitForRpcAsync(ModPattern(466, 1157, true), TimeSpan.FromSeconds(20));
        MatchCollection hot = Regex.Matches(context.Server.DebugLog(), @"169 heals HealthAlly for (\d+)");
        Check.Equal(5, hot.Count, "Emergency Repairs ticks five times before expiry");
        Check.That(hot.Cast<Match>().All(tick => tick.Groups[1].Value == "2"), "Emergency Repairs requests 2 HP per tick");
        var dot = enemy.Received("81 67 ?? ?? ?? ?? f9 00 00 7c 00 00 10 00 00 00 48 07 80");
        Check.Equal(5, dot.Count, "Plague Zone ticks five times before expiry");
        int raw = (int)(total / 15.0 * 2.5 + 0.5);
        foreach (var tick in dot)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(tick.Bytes.AsSpan(19));
            Check.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(tick.Bytes.AsSpan(offset)), "DOT line carries damage and resistance");
            int damage = BinaryPrimitives.ReadInt32LittleEndian(tick.Bytes.AsSpan(offset + 2));
            int resisted = BinaryPrimitives.ReadInt32LittleEndian(tick.Bytes.AsSpan(offset + 6));
            Check.Equal(raw, damage + resisted, "Plague Zone tick recomputes from the applied total and stored interval");
        }
        Check.Equal(0, players.Sum(player => player.World.Problems.Count), "Health parity wire parsing");
        Console.WriteLine($"Repair RSI +9 HP; Emergency Repairs five +2 HP ticks; Plague Zone five raw {raw}-damage ticks (applied total {total}).");
    }

    // PR290 native CSR 54d32748b0ba:8305-8306, including the uninitialised byte the client happened to send.
    public static byte[] NativeRejuvenateConsent() => WireFormat.FromHex(
        "81 88 a0 01 00 00 00 00 00 00 00 ca 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 " +
        "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00");

    private static byte[] HealthCast(string capture, ViewCreation target)
    {
        byte[] request = WireFormat.FromHex(capture);
        Check.Equal(43, request.Length, "Captured 80 b9 request length");
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(4), target.ViewId);
        request[6] = target.SpawnId;
        return request;
    }
}
