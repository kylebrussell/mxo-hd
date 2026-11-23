using hds;
using MxoHd.PacketResearch;

namespace HeadlessClient;

public static partial class Scenarios
{
    private static async Task CoderUpperViral(ScenarioContext context)
    {
        const ushort enrage = 597, barrage = 598, level = 50;
        const int x = 17000, y = 495, casterZ = 6400, centreZ = 6600, edgeZ = 7100, outsideZ = 7101;
        var stands = new[] { (x, casterZ), (x, centreZ), (x, edgeZ), (x, outsideZ), (x + 100, centreZ) };
        SectorProperties.Load(Path.Combine(context.RepositoryRoot, "data", "sector_properties.csv"));
        NavMesh.UseDirectory(Path.Combine(context.RepositoryRoot, "data", "navmesh"));
        foreach (var (standX, standZ) in stands)
        {
            Check.That(NavMesh.Locate(1, standX, y, standZ, out var stand) && !stand.indoors &&
                Math.Abs(stand.floorY + 95 - y) < 1 &&
                NavMesh.HasSightPath(1, x, y, casterZ, standX, y, standZ),
                $"Outdoor visible NAV ({standX},{y},{standZ})");
        }

        ushort[] tested = { 2, 17, CombatStyles.CoderStyle, 599, enrage, barrage };
        var profile = tested.SelectMany(id => AbilityPrerequisites.Current.Closure(id).Keys.Append(id))
            .Distinct().Order().Select(id => (id, level)).ToArray();
        Check.That(profile.Sum(row => AbilityMemory.Cost(row.id)) <= AbilityMemory.ForLevel(level),
            "The full Proxy Master profile fits the archive");

        CharacterSeed Seed(string name, int standX, int standZ, int org) => new(name)
        {
            X = standX, Y = y, Z = standZ, Level = level, Experience = checked((int)PlayerProgression.ExperienceForLevel(level)),
            Health = 5000, MaxHealth = 5000, InnerStrength = 700, MaxInnerStrength = 900, Organization = org,
            OwnedLoadedAbilities = new (ushort, ushort)[] { (2, 1), (17, 1) }
        };
        var caster = await context.AddPlayerAsync(Seed("UpperCoder", x, casterZ, 1) with { OwnedLoadedAbilities = profile });
        var primary = await context.AddPlayerAsync(Seed("UpperCoderCentre", x, centreZ, 2));
        var edge = await context.AddPlayerAsync(Seed("UpperCoderEdge", x, edgeZ, 2));
        var outside = await context.AddPlayerAsync(Seed("UpperCoderOutside", x, outsideZ, 2));
        var ally = await context.AddPlayerAsync(Seed("UpperCoderAlly", x + 100, centreZ, 1));
        var actors = new[] { caster, primary, edge, outside, ally };
        foreach (var player in actors) await PvpOpponentsFixture.EnterAsync(player);
        var views = new Dictionary<HeadlessPlayer, uint>();
        foreach (var player in actors.Skip(1))
        {
            var view = await caster.WaitForViewNamedAsync(player.Handle);
            views.Add(player, DuelPackets.Handle(view.ViewId, view.SpawnId));
        }

        // Generated server inputs. The first complete stock 597/598 casts remain a native step.
        byte[] Cast(ushort ability, uint handle) =>
            new AbilityUseRecord(ability, handle, 0, 0, 0, 0, 0x6b, 0x6b, 0, 0).Encode();
        async Task RefusedAsync(ushort ability, uint handle, uint reason)
        {
            caster.SkipReceived();
            caster.SendRpc(Cast(ability, handle));
            byte[] expected = AbilityRefusals.Encode(new(ability, reason));
            await caster.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(expected),
                $"Refusal {reason} for {ability}");
        }

        int baseline = context.Server.DebugLog().Length;
        await RefusedAsync(barrage, views[primary], AbilityRefusals.WrongStyle);
        caster.SendRpc("3e 6b f0 03 00 00 00 00");
        await caster.WaitForViewUpdateAsync(2, "02 80 80 80 80 80 10 f0 03 00 00");
        foreach (ushort ability in new[] { enrage, barrage })
        {
            await RefusedAsync(ability, 0, AbilityRefusals.InvalidTarget);
            await RefusedAsync(ability, views[ally], AbilityRefusals.InvalidTarget);
            await RefusedAsync(ability, views[primary] ^ 0x10000, AbilityRefusals.InvalidTarget);
        }
        Check.That(!context.Server.DebugLog()[baseline..].Contains("[ABILITY-COST]"),
            "Invalid requests leave the original IS untouched");

        string State(HeadlessPlayer player, string operation) => $"char={player.CharId} source={enrage} stat=93 {operation}";
        caster.SkipReceived();
        caster.SendRpc(Cast(enrage, views[primary]));
        await CastBarAndReuseAsync(enrage, 4, 30);
        await caster.WaitUntilAsync(() => context.Server.DebugLog()[baseline..].Contains(State(primary, "add value=0 duration=10s")) &&
            context.Server.DebugLog()[baseline..].Contains(State(edge, "add value=0 duration=10s")),
            "Enrage holds exactly the selected enemy and the 5.00 m edge", HeadlessPlayer.DefaultTimeout);
        string first = context.Server.DebugLog()[baseline..];
        Check.That(first.Contains($"char={caster.CharId} ability=597 paid=43 IS=") && !first.Contains(State(ally, "add")) &&
            !first.Contains(State(outside, "add")), "The paid cast excludes the in-range ally and the 5.01 m enemy");
        foreach (var player in actors)
            Check.Equal((ushort)5000, RefusalOwnValue(player, "Health", "02 80 80 80 40"), "Enrage leaves HP unchanged");

        caster.SkipReceived();
        caster.SendRpc(Cast(barrage, views[primary]));
        await CastBarAndReuseAsync(barrage, 2, 6);
        await primary.WaitUntilAsync(() => RefusalOwnValue(primary, "Health", "02 80 80 80 40") == 4913,
            "Static Barrage applies 64 viral base with Coder +26 and Run Advanced Program +10 on the selected player", HeadlessPlayer.DefaultTimeout);
        foreach (var player in new[] { edge, outside, ally })
            Check.Equal((ushort)5000, RefusalOwnValue(player, "Health", "02 80 80 80 40"), "Static Barrage has no splash");

        await caster.WaitUntilAsync(() => context.Server.DebugLog()[baseline..].Contains(State(primary, "remove")) &&
            context.Server.DebugLog()[baseline..].Contains(State(edge, "remove")),
            "Both independent ten-second states expire", TimeSpan.FromSeconds(16));
        Check.That(context.Server.DebugLog()[baseline..].Contains($"char={caster.CharId} ability=598 paid=26 IS="),
            "The single live Barrage uses the cost reduced by the loaded Proxy Master profile");
        Check.Equal(0, actors.Sum(player => player.World.Problems.Count), "The two Coder cast paths decoded cleanly");
        Console.WriteLine("CODER UPPER VIRAL PASS: selected Enrage radius and expiry, plus fixed single-target Static Barrage damage.");

        async Task CastBarAndReuseAsync(ushort ability, float seconds, float ownReuse)
        {
            var cast = await caster.WaitForRpcAsync($"80 ac {WireFormat.ToHex(BitConverter.GetBytes(ability))}");
            Check.Equal(seconds, BitConverter.ToSingle(cast.Bytes.AsSpan(^4)), "Shipped cast time");
            byte[] timer = AbilityCooldowns.BuildTimer(ability, AbilityCooldowns.OwnTimer, ownReuse, start: true);
            await caster.WaitForRpcAsync(frame => frame.Bytes.AsSpan().SequenceEqual(timer), "Shipped own reuse");
        }
    }
}
