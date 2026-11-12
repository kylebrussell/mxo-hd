namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task PvpGroupInterlock(ScenarioContext context)
    {
        string[] names = { "GroupA", "GroupB", "GroupC", "GroupD", "GroupE", "GroupF", "GroupG", "Onlooker" };
        var players = await context.AddPlayersInWorldAsync(names.Select((name, i) => new CharacterSeed(name)
            { X = 17020 + 35 * i, Level = i == 1 ? 1 : 50, Organization = i == 1 ? 2 : 1, Health = 20000, MaxHealth = 20000 }).ToArray());
        var a = players[0]; var b = players[1]; var c = players[2]; var observer = players[7];
        foreach (var player in players.Take(7))
        {
            player.SendChat("?pvpflag on");
            await player.WaitForViewUpdateAsync(2, OwnPvpFlagOn);
            foreach (string name in names.Where(n => n != player.Handle)) await player.WaitForViewNamedAsync(name);
        }
        var bv = await a.WaitForViewNamedAsync("GroupB");
        a.SendRpc($"40 {HandleOf(bv)} 01 6b");
        await a.WaitForRpcAsync("45");
        ushort av = await WaitForCombatView(a), ov = await WaitForCombatView(observer);
        foreach (var player in players.Skip(2).Take(4))
        {
            var target = await player.WaitForViewNamedAsync("GroupB");
            player.SendRpc($"40 {HandleOf(target)} 01 6b");
            await player.WaitForRpcAsync("45");
        }
        // The observer and all six fighters now have the same six handles, using their local view ids.
        bool HasSix(HeadlessPlayer player, int after = 0) => player.World.ViewSnapshot().Skip(after).Any(section =>
        {
            byte[] marker = WireFormat.FromHex("01 00 02");
            for (int at = BytePattern.IndexOf(section, marker, 0); at >= 0; at = BytePattern.IndexOf(section, marker, at + 1))
                if (at + 65 <= section.Length && BitConverter.ToUInt32(section, at + 36) == 0x3f &&
                    BitConverter.ToUInt16(section, at + 3) == 62) return true;
            return false;
        });
        await observer.WaitUntilAsync(() => HasSix(observer), "six-slot observer init", TimeSpan.FromSeconds(10));
        foreach (var fighter in players.Take(6)) Check.That(HasSix(fighter), fighter.Handle + " received the six-slot mapping");
        await observer.WaitUntilAsync(() => CombatEntries(observer, ov).Any(e => e[0] == 6 || e[1] == 6),
            "sixth fighter's live round entry", TimeSpan.FromSeconds(10));
        Check.Equal(av, CombatViewOf(a), "Joining players reuse the original interlock");
        var gv = await players[6].WaitForViewNamedAsync("GroupB");
        players[6].SendRpc($"40 {HandleOf(gv)} 01 6b");
        await players[6].WaitForRpcAsync($"4d 06 00 00 00 {HandleOf(gv)}");

        // One withdrawal removes slot 3 only. Everyone else keeps the same shared view and live rounds.
        int entriesBefore = CombatEntries(observer, ov).Count;
        c.SendRpc("44");
        await c.WaitForRpcAsync($"46 {HandleOf(await c.WaitForViewNamedAsync("GroupB"))} 00", TimeSpan.FromSeconds(120));
        Check.That(observer.World.ViewSnapshot().Any(section =>
        {
            byte[] marker = WireFormat.FromHex($"{Le(ov)} 01");
            for (int at = BytePattern.IndexOf(section, marker, 0); at >= 0; at = BytePattern.IndexOf(section, marker, at + 1))
                if (at + 40 <= section.Length && BitConverter.ToUInt32(section, at + 31) == 0x80000000 &&
                    BitConverter.ToUInt32(section, at + 35) == 4) return true;
            return false;
        }), "Only the withdrawing player's slot is detached for survivors and the observer");
        await observer.WaitUntilAsync(() => CombatEntries(observer, ov).Count > entriesBefore + 3,
            "group rounds continue after withdrawal", TimeSpan.FromSeconds(15));
        Check.Equal(av, CombatViewOf(a), "A participant's departure preserves the shared view");
        int beforeRejoin = observer.World.ViewSnapshot().Count;
        c.SendRpc($"40 {HandleOf(await c.WaitForViewNamedAsync("GroupB"))} 01 6b");
        await c.WaitForRpcAsync("45");
        await observer.WaitUntilAsync(()=>HasSix(observer,beforeRejoin),"withdrawn fighter rejoins the same interlock",TimeSpan.FromSeconds(10));
        Check.Equal(av,CombatViewOf(a),"Rejoining retains the shared view");
    }
}
