namespace HeadlessClient;

/// <summary>
/// Spy stealth (docs/STEALTH.md): the DissemblingType and ConditionStateFlags each side gets, and an enemy losing the
/// stealthed player's view until the stealth ends.
/// </summary>
public static partial class Scenarios
{
    private const ushort SneakAbility = 1111, InvisibilityAbility = 1116;

    private static int CreationsNamed(HeadlessPlayer player, string name) => player.World.CreationSnapshot().Count(c => c.Name == name);

    /// <summary>
    /// A friend keeps seeing the sneaker (with kSneak and ST_STEALTHED on their view of them); once the sneaker can be
    /// attacked by the onlooker, the onlooker loses the view; switching Sneak off brings it back without the bits.
    /// </summary>
    private static async Task StealthSneakObserver(ScenarioContext context)
    {
        (HeadlessPlayer onlooker, HeadlessPlayer sneaker, ViewCreation sneakerView, _) = await TwoNearby(context,
            new CharacterSeed("Onlooker") { Level = 50, Organization = 1 },
            new CharacterSeed("Sneaker") { X = 17120, Level = 50, Organization = 2 });

        // Sneak (2 s cast): own view DissemblingType kSneak + ST_STEALTHED, the StealthState record and the toggle on
        Cast(sneaker, SneakAbility);
        await sneaker.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 82 02 00 01 00 00 00 02", TimeSpan.FromSeconds(10));
        await sneaker.WaitForRpcAsync("80 bd 05 57 04 00 00 00 00 00 01");
        await sneaker.WaitForRpcAsync(ModPattern(SneakAbility, 881));
        Check.Equal(1, sneaker.Received(ModPattern(SneakAbility, 115)).Count, "Movement Speed record");
        Check.Equal(1, sneaker.Received(ModPattern(SneakAbility, 509)).Count, "Noise record");
        // The onlooker isn't an enemy: their view of the sneaker gets the same bits (the client draws invis_self_loop)
        await onlooker.WaitForViewUpdateAsync(sneakerView.ViewId, "02 80 80 80 c0 02 00 04 00 00 00 02");
        await Task.Delay(TimeSpan.FromSeconds(3));
        Check.Equal(0, CountViewUpdate(onlooker, 1, $"010100{Le(sneakerView.ViewId).Replace(" ", "")}"), "Friendly onlooker keeps the view");

        // Flagged for PvP, the sneaker is the onlooker's enemy: the view goes (the onlooker faces +z, the sneaker is
        // to the side, so the five-second detection can't see them)
        int spawns = CreationsNamed(onlooker, "Sneaker");
        sneaker.SendChat("?pvpflag on");
        await sneaker.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 80 01 00 00 00 03");
        await onlooker.WaitForViewUpdateAsync(1, $"01 01 00 {Le(sneakerView.ViewId)}", TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(6));
        Check.Equal(spawns, CreationsNamed(onlooker, "Sneaker"), "The enemy sneaker stays hidden through a detection pass");

        // Sneak again switches it off: records and toggle off, bits cleared, and the onlooker sees them again
        sneaker.SkipReceived();
        Cast(sneaker, SneakAbility);
        await sneaker.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 82 00 00 01 00 00 00 01");
        await sneaker.WaitForRpcAsync(ModPattern(SneakAbility, 881, off: true));
        await sneaker.WaitForRpcAsync("80 bd 05 57 04 00 00 00 00 00 00");
        await onlooker.WaitUntilAsync(() => CreationsNamed(onlooker, "Sneaker") > spawns, "the sneaker spawning again", TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Damage ends Sneak (OnHealthReduceDeactivateAbility); Invisibility has no health callback and survives it, and the
    /// enemy only sees the invisible player again when it is switched off. Using another ability ends it too.
    /// </summary>
    private static async Task StealthBreaks(ScenarioContext context)
    {
        (HeadlessPlayer onlooker, HeadlessPlayer spy, ViewCreation spyView, _) = await TwoNearby(context,
            new CharacterSeed("Watcher") { Level = 50, Organization = 1 },
            new CharacterSeed("Spy") { X = 17120, Level = 50, Organization = 2 });

        Cast(spy, SneakAbility);
        await onlooker.WaitForViewUpdateAsync(spyView.ViewId, "02 80 80 80 c0 02 00 04 00 00 00 02", TimeSpan.FromSeconds(10));
        spy.SendChat("?health 300");
        await onlooker.WaitForViewUpdateAsync(spyView.ViewId, "02 80 80 80 c0 00 00 04 00 00 00 00");
        await spy.WaitForRpcAsync("80 bd 05 57 04 00 00 00 00 00 00");

        // Invisibility (3 s cast): kInvisible; damage doesn't end it
        Cast(spy, InvisibilityAbility);
        await onlooker.WaitForViewUpdateAsync(spyView.ViewId, "02 80 80 80 c0 04 00 04 00 00 00 02", TimeSpan.FromSeconds(10));
        await spy.WaitForRpcAsync(ModPattern(InvisibilityAbility, 115));
        int offs = CountViewUpdate(onlooker, spyView.ViewId, "02808080c00000");
        spy.SendChat("?health 200");
        await Task.Delay(TimeSpan.FromSeconds(2));
        Check.Equal(offs, CountViewUpdate(onlooker, spyView.ViewId, "02808080c00000"), "Damage leaves Invisibility on");

        // Flagged, the invisible spy disappears for the watcher; another ability ends Invisibility and brings them back
        int spawns = CreationsNamed(onlooker, "Spy");
        spy.SendChat("?pvpflag on");
        await onlooker.WaitForViewUpdateAsync(1, $"01 01 00 {Le(spyView.ViewId)}", TimeSpan.FromSeconds(10));
        Cast(spy, 12); // Hyper-Speed
        await spy.WaitForRpcAsync("80 bd 05 5c 04 00 00 00 00 00 00");
        await onlooker.WaitUntilAsync(() => CreationsNamed(onlooker, "Spy") > spawns, "the spy spawning again", TimeSpan.FromSeconds(10));
    }
}
