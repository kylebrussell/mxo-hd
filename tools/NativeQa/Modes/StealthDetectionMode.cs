using HeadlessClient;
using hds;
using System.Text.Json;

namespace NativeQa.Modes;

public sealed class StealthDetectionMode : NativeQaMode
{
    private const string Low = "QAFriend", High = "QAFar";
    private const int X = 17000, Y = 495, Z = 2300, LowX = 18400, HighX = 18900;
    private const ushort SneakLevel = 28;

    public override string Name => "stealth-detection";

    public override NativeQaModePlan Plan { get; } = new(
        SneakScaledGrantFixture.Parents, X, Z, Array.Empty<string>(),
        new[] { $"UPDATE char_abilities SET level={SneakLevel} WHERE char_id=110 AND ability_id=2147638272" },
        new[]
        {
            $"UPDATE characters SET y={Y},alignment=1 WHERE charId=110",
            $"UPDATE characters SET x=CASE handle WHEN '{Low}' THEN {LowX} ELSE {HighX} END,y={Y},z={Z},rotation=192,alignment=2 WHERE handle IN ('{Low}','{High}')",
            "DELETE FROM char_abilities WHERE char_id IN (6111,6112)",
            "INSERT INTO char_abilities(char_id,slot,ability_id,level,is_loaded,ability_name) VALUES(6112,0,2147485696,50,1,'AwakenedAbility'),(6112,1,2147788800,50,1,'DetectionAbility')",
        },
        environmentOverrides: new() { ["MXO_PVP_SERVER"] = "1", ["MXO_PVP_MAX_SAFE_LEVEL"] = "15", ["MXO_AGGRO"] = "0" },
        readyMessages: new[]
        {
            "Use ordinary Sneak at 17000/495/2300. Both hostile helpers initially lose CSR's view. Walk east on z=2300 to about x=17300 and wait for a QAFriend missed roll (SA 1, SL 28); QAFar is outside contest range.",
            "Then walk east to about x=17800. Wait for QAFar (SA 28) to reveal CSR and the Sneak toggle to turn off. This is a random contest; allow up to 10 five-second checks. Both helpers regain CSR after the global reveal. See docs/STEALTH.md for pass criteria."
        });

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Requires a fixture server.");
        double range = Stealth.DetectionRangeUnits(SneakLevel);
        Check.That(LowX - X > range && LowX - 17300 <= range && HighX - 17300 > range &&
            HighX - 17800 <= range, "The fixture has distinct activation, low and high stages");
        Check.That(!Stealth.SneakTest(1, SneakLevel, 0, LowX - 17300, 0) &&
            Stealth.SneakTest(28, SneakLevel, 0, HighX - 17800, 0), "The two fixture builds have distinct contests");

        foreach (var (name, expectedX) in new[] { (Low, LowX), (High, HighX) })
        {
            var helper = await HeadlessPlayer.LoginAsync(context.Address, name, name);
            context.Helpers.Add(name, helper);
            // 05 and 0c 01 are captured client world-entry records. No gameplay action is sent here.
            await DebuffRadiusFixture.EnterFlaggedAsync(helper);
            var own = helper.World.CreationSnapshot().Last(v => v.ViewId == 2);
            Check.That(EngageFoesFixture.Position(own).SequenceEqual(new double[] { expectedX, Y, Z }),
                $"{name} begins at the intended position");
            NativeQaReadiness.RequireNoAmbientAggro(server, helper);

            var loaded = GhostStrikeInvisibilityTool.AbilityIds(helper.CharacterData.GetValueOrDefault((byte)6) ?? Array.Empty<byte>());
            Check.Equal(name == High ? "2,298" : "", string.Join(",", loaded.Order()), $"{name} loadout");
        }

        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "stealth-detection-bootstrap.json"),
            JsonSerializer.Serialize(new { SneakLevel, RangeUnits = range, LowAwareness = 1, HighAwareness = 28,
                Activation = X, LowStage = 17300, HighStage = 17800, Y, Z }));
        return Array.Empty<byte>();
    }
    internal static async Task RecordResultAsync(string directory, ServerInstance server,
        IReadOnlyDictionary<string, HeadlessPlayer> helpers)
    {
        string output = Path.Combine(directory, "stealth-detection-result.json");
        if (File.Exists(output)) return;

        const string csr = "CSR_Tester";
        var lines = server.DebugLog().Split('\n');
        int activation = Array.FindLastIndex(lines, line => line.Contains("[STEALTH] " + csr + " 1111 on"));
        if (activation < 0) return;
        int lowMiss = Array.FindIndex(lines, activation, line =>
            line.Contains("[STEALTH] detect " + Low + " vs " + csr) && line.Contains("missed (SA 1 SL 28)"));
        int highReveal = Array.FindIndex(lines, activation, line =>
            line.Contains("[STEALTH] " + High + " sees " + csr + " (SA 28 SL 28"));
        int ended = Array.FindIndex(lines, activation, line => line.Contains("[STEALTH] " + csr + " 1111 off"));
        if (lowMiss < 0 || highReveal <= lowMiss || ended <= highReveal ||
            !lines[ended].Contains("(revealed)")) return;

        var views = new Dictionary<string, int>();
        foreach (var name in new[] { Low, High })
        {
            var helper = helpers[name];
            var lifetimes = helper.World.CreationSnapshot().Where(view => view.Name == csr).ToArray();
            if (lifetimes.Length < 2 || helper.World.Problems.Count != 0) return;
            byte[] deletion = { 1, 1, 0, (byte)lifetimes[0].ViewId, (byte)(lifetimes[0].ViewId >> 8) };
            if (!helper.World.ViewSnapshot().Any(list => BytePattern.IndexOf(list, deletion, 0) >= 0)) return;
            views.Add(name, lifetimes.Length);
        }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            HelperProof = "PASS", LowMiss = lines[lowMiss].Trim(), HighReveal = lines[highReveal].Trim(),
            Ended = lines[ended].Trim(), CsrViewCreations = views
        }));
        Console.WriteLine("Helper proof PASS: low miss, high detection, and CSR's view restored to both observers.");
    }
}
