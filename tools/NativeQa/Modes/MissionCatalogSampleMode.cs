using System.Xml.Linq;
using System.Collections.Concurrent;
using System.Text.Json;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MissionCatalogSampleMode : NativeQaMode
{
    public override string Name => "mission-catalog-sample";
    private readonly ConcurrentDictionary<(string Missions, string? Phase, string? Samples), NativeQaModePlan> plans = new();

    public override NativeQaModePlan Plan => plans.GetOrAdd(
        (Environment.GetEnvironmentVariable("MXO_QA_MISSION_ID") ?? "1205",
            Environment.GetEnvironmentVariable("MXO_QA_MISSION_PHASE"),
            Environment.GetEnvironmentVariable("MXO_QA_MISSION_SAMPLES")),
        key => BuildPlan(key.Missions, key.Phase, key.Samples));

    private static (int Mission, int Area)[] ParseSamples(string value)
    {
        var samples = value.Split(',', StringSplitOptions.TrimEntries).Select(sample =>
        {
            var parts = sample.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out int mission) || !int.TryParse(parts[1], out int area)
                || mission < 1 || area < 1)
                throw new ArgumentException("MXO_QA_MISSION_SAMPLES must contain mission:area pairs.");
            return (Mission: mission, Area: area);
        }).ToArray();
        if (samples.Distinct().Count() != samples.Length)
            throw new ArgumentException("Choose distinct mission:area pairs.");
        return samples;
    }

    internal static NativeQaModePlan BuildPlan(string value, string? phaseValue = null, string? sampleValue = null)
    {
            string? root=AppContext.BaseDirectory;
            while(root!=null&&!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root);
            if(root==null)throw new DirectoryNotFoundException("Mission catalog repository");
            var catalog=Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml").Select(XElement.Load).ToArray();
            var samples = string.IsNullOrEmpty(sampleValue) ? Array.Empty<(int Mission, int Area)>() : ParseSamples(sampleValue);
            if (samples.Length != 0 && !string.IsNullOrEmpty(phaseValue))
                throw new ArgumentException("Choose either MXO_QA_MISSION_SAMPLES or MXO_QA_MISSION_PHASE.");
            var ids = samples.Length > 0 ? samples.Select(s => s.Mission).Distinct().ToArray()
                : value.Split(',', StringSplitOptions.TrimEntries)
                    .Select(v => int.TryParse(v, out int n) ? n : throw new ArgumentException("MXO_QA_MISSION_ID must contain catalog mission IDs"))
                    .ToArray();
            if (samples.Length == 0 && (ids.Length > 8 || ids.Distinct().Count() != ids.Length))
                throw new ArgumentException("Choose up to eight distinct catalog missions");
            var selected=ids.Select(candidate=>catalog.SingleOrDefault(m=>(int)m.Attribute("id")! == candidate)??throw new ArgumentException("Unknown catalog mission "+candidate)).ToArray();
            int id=ids[0];var mission=selected[0];
            foreach (var sample in samples)
            {
                var definition = selected.Single(m => (int)m.Attribute("id")! == sample.Mission);
                if (sample.Area > definition.Elements("phase").Count() || ((uint?)definition.Attribute("entryItem") ?? 0) != 0)
                    throw new ArgumentException($"Invalid area or ticket mission in sample {sample.Mission}:{sample.Area}.");
            }
            int? startArea = null;
            if (!string.IsNullOrEmpty(phaseValue))
            {
                int phaseCount = mission.Elements("phase").Count();
                if (ids.Length != 1 || !int.TryParse(phaseValue, out int phase) || phase < 1 || phase > phaseCount)
                    throw new ArgumentException($"MXO_QA_MISSION_PHASE requires one mission and an area from 1 to {phaseCount}.");
                startArea = phase;
            }
            int org=(int?)mission.Attribute("org")??1,district=(int?)mission.Attribute("district")??1;
            string contact=(string?)mission.Attribute("contact")??"Tyndall",districtName=(string?)mission.Attribute("districtName")??"slums";
            uint ticket=(uint?)mission.Attribute("entryItem")??0;
            if(selected.Any(m=>((int?)m.Attribute("district")??1)!=district)||ids.Length>1&&selected.Any(m=>((uint?)m.Attribute("entryItem")??0)!=0))
                throw new ArgumentException("A shared sample session requires one district and contact-issued missions");
            var hardline=File.ReadLines(Path.Combine(root,"data","hardline_network.csv")).Skip(1).Select(l=>l.Split(','))
                .First(r=>r.Length>=15&&r[0]==district.ToString()&&r[4].Length>0);
            double Coordinate(int index)=>double.Parse(hardline[index],System.Globalization.CultureInfo.InvariantCulture);
            double x=district==1?23828:Coordinate(11),y=district==1?495:Coordinate(12),z=district==1?13984:Coordinate(13);
            var setup=new List<string>{"DELETE FROM char_missions WHERE charId=110 AND missionId<>986",
                "DELETE FROM inventory WHERE charId=110",
                "INSERT IGNORE INTO char_missions(charId,missionId) VALUES "+string.Join(",",catalog.Where(m=>!ids.Contains((int)m.Attribute("id")!)).Select(m=>$"(110,{(int)m.Attribute("id")!})")),
                FormattableString.Invariant($"UPDATE characters SET level=50,exp={hds.PlayerProgression.ExperienceForLevel(50)},x={x},y={y},z={z},rotation=126,alignment={Math.Max(1,org)},repMero={selected.Max(m=>(int?)m.Attribute("minReputation")??0)},districtId={district},district='{districtName}',healthC=5000,healthM=5000,innerStrC=2000,innerStrM=2000,pvpflag=0 WHERE charId=110")};
            if(ticket!=0)setup.Add($"INSERT INTO inventory(charId,goid,slot,count,purity) VALUES(110,{ticket},1,0,0)");
            return new(new ushort[]{2,17,45,1006,77},(int)x,(int)z,Array.Empty<string>(),Array.Empty<string>(),setup.ToArray(),
                DevCommands:true,environmentOverrides:new()
                {
                    ["MXO_MISSION_SITE_QA_SEED"] = samples.Length > 0 || ids.Length > 1 ? "catalog" : id.ToString(),
                    ["MXO_QA_MISSION_PHASE_COMMAND"] = "1", ["MXO_AGGRO"] = "0", ["MXO_MOB_WANDER"] = "0"
                },
                extraQaMetadata:startArea.HasValue ? new()
                {
                    ["CheckpointMission"] = id, ["CheckpointArea"] = startArea.Value,
                    ["EarlierAreasPlayedHeadlessly"] = startArea.Value - 1
                } : samples.Length > 0 ? new() { ["DirectMissionSamples"] = sampleValue } : null,
                readyMessages:samples.Length > 0
                    ? samples.Select(s => $"Verified start: ?missionphase {s.Mission} {s.Area}")
                        .Append("Finish or abort the active mission before the next command. Direct starts bypass contacts and stage authored carry-over for QA.").ToArray()
                    : startArea.HasValue
                    ? new[] { $"Mission {id} is already accepted at area {startArea}. "
                        + (startArea == 1 ? "The fixture accepted the offer headlessly. "
                            : $"The fixture played {startArea.Value - 1} earlier areas headlessly with movement and combat assistance. ")
                        + "Follow the measured approach below and play this area in the native client." }
                    : selected.Select(m=>$"Catalog mission {(int)m.Attribute("id")!}: {(string?)m.Attribute("title")}; contact {(string?)m.Attribute("contact")??"Tyndall"}. Use ?org {Math.Max(1,(int?)m.Attribute("org")??1)} before calling that contact. This mission is uncompleted.")
                        .Append("Each accepted phase prints measured street/lobby/floor positions. Other receipts are fixture setup; use ordinary mission interactions and rides. Movement/weaken are assistance.").ToArray());
    }

    private static async Task CheckDirectSamplesAsync(NativeQaBootstrapContext context,
        (int Mission, int Area)[] samples)
    {
        var server = context.ActualServer ?? throw new InvalidOperationException("Missing catalog fixture server.");
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Missing catalog fixture database.");
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Missing catalog path.");
        Check.That(database.Name.StartsWith(ThrowawayDatabase.Prefix, StringComparison.Ordinal) &&
            server.DatabaseName == database.Name, "Direct samples require the matching disposable server");
        const string payload = "SELECT COALESCE((SELECT HEX(payload) FROM char_active_missions WHERE charId=110),'')";
        Check.Equal("", await database.ScalarAsync(payload), "Direct samples start with no active mission");
        var proof = new List<object>();
        using (var player = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester"))
        {
            Check.Equal(110u, player.CharId, "Disposable native character");
            await player.EnterWorldAsync();
            foreach (var (mission, area) in samples)
            {
                string request = $"?missionphase {mission} {area}";
                Console.WriteLine($"Checking {request} before Ready.");
                player.SkipReceived();
                player.SendChat(request);
                var reply = await player.WaitForRpcAsync(r => r.Bytes.Length > 1 && r.Bytes[0] == 0x2e &&
                    (System.Text.Encoding.UTF8.GetString(r.Bytes).Contains("QA READY mission ", StringComparison.Ordinal) ||
                     System.Text.Encoding.UTF8.GetString(r.Bytes).Contains("QA phase start refused", StringComparison.Ordinal)),
                    $"direct start response for {mission}:{area}", TimeSpan.FromSeconds(45));
                var message = System.Text.Encoding.UTF8.GetString(reply.Bytes);
                Check.That(message.Contains($"QA READY mission {mission} area {area}.", StringComparison.Ordinal),
                    $"{request} did not become ready: {message[(message.IndexOf("QA ", StringComparison.Ordinal))..].TrimEnd('\0')}");
                var staged = MervFollowupCheckpointFixture.ParseHex(await database.ScalarAsync(payload));
                Check.That(staged?.Mission == mission && staged.Phase == area - 1 && staged.Done.Length == 0,
                    "Direct start committed the requested area with fresh goals");
                Check.Equal(0L, await database.LongAsync(
                    $"SELECT COUNT(*) FROM char_missions WHERE charId=110 AND missionId={mission}"),
                    "Probe has no completion receipt for the selected mission");
                NativeQaReadiness.RequireNoAmbientAggro(server, player);
                string[] approaches = MissionCatalogObjectApproaches.Find(staged!, root).Select(obj => obj.Describe()).ToArray();
                foreach (string approach in approaches)
                    Console.WriteLine($"READY {mission}:{area} bootstrap {approach}");
                proof.Add(new { Mission = mission, Area = area, staged!.Following, staged.Items,
                    Actors = staged.Mobs.Select(m => new { m.Npc, m.Name, m.Health, m.Dead }),
                    Props = staged.Objects.Select(o => o.Id), Entrance = MissionCatalogEntrances.Describe(staged, root),
                    ObjectApproaches = approaches });

                player.SkipReceived();
                player.SendRpc("80 a6");
                await player.WaitForRpcAsync("80 a2 08");
                Check.Equal("", await database.ScalarAsync(payload), "Ordinary abort clears the probe mission");
            }
            player.SkipReceived();
            player.SendRpc("80 fe 00");
            await player.WaitForRpcAsync("80 fd");
            Check.Equal(0, player.World.Problems.Count, "Direct sample probes and quit have no wire errors");
        }

        // The same character is reserved for native login after the server reaps the probe endpoint.
        await Task.Delay(TimeSpan.FromSeconds(33));
        Check.Equal(0L, await database.LongAsync("SELECT is_online FROM characters WHERE charId=110"),
            "Disposable native character has logged out");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "mission-samples-bootstrap.json"),
            JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Phase start requires the catalog path.");
        hds.SectorProperties.Load(Path.Combine(root, "data", "sector_properties.csv"));
        hds.NavMesh.UseDirectory(Path.Combine(root, "data", "navmesh"));
        var metadata = Plan.ExtraQaMetadata;
        if (metadata.TryGetValue("DirectMissionSamples", out var sampleValue))
        {
            await CheckDirectSamplesAsync(context, ParseSamples(sampleValue.GetString()!));
            return Array.Empty<byte>();
        }
        if (!metadata.TryGetValue("CheckpointArea", out var areaValue))
        {
            // A normal offer fixture still needs an authenticated world to open.
            using var canary = await HeadlessPlayer.LoginAsync(context.Address, "QAFriend", "QAFriend");
            await canary.EnterWorldAsync();
            Check.That(canary.World.CurrentOwnViewLife().OwnViewReady && canary.World.Problems.Count == 0,
                "Catalog fixture authenticated canary");
            return Array.Empty<byte>();
        }

        var server = context.ActualServer ?? throw new InvalidOperationException("Phase start requires a fixture server.");
        var database = context.OwnedDatabase ?? throw new InvalidOperationException("Phase start requires a disposable database.");
        int mission = metadata["CheckpointMission"].GetInt32(), area = areaValue.GetInt32();
        await using var scenario = ScenarioContext.UseOwnedServer(root, database, server);
        string PayloadQuery = "SELECT IFNULL(HEX(payload),'') FROM char_active_missions WHERE charId=110";
        Check.Equal("", await database.ScalarAsync(PayloadQuery), "Native fixture starts without an active mission");
        Console.WriteLine($"Preparing mission {mission} at area {area}; wait for Native QA ready.");
        hds.MissionProgress.SavedState earned;
        using (var player = await HeadlessPlayer.LoginAsync(context.Address, "CSR_AbilityQA", "CSR_Tester"))
        {
            Check.Equal(110u, player.CharId, "Disposable native character");
            earned = await Scenarios.StartCatalogPhaseAsync(scenario, player, mission, area);
            player.SkipReceived();
            player.SendRpc("80 fe 00");
            await player.WaitForRpcAsync("80 fd");
            Check.Equal(0, player.World.Problems.Count, "Phase preparation and quit have no wire errors");
        }

        // The ordinary quit leaves the UDP endpoint reserved until the world reaps it.
        await Task.Delay(TimeSpan.FromSeconds(33));
        var persisted = MervFollowupCheckpointFixture.ParseHex(await database.ScalarAsync(PayloadQuery));
        Check.That(persisted?.Mission == mission && persisted.Phase == area - 1 &&
            persisted.Identity == earned.Identity && persisted.Done.SequenceEqual(earned.Done) &&
            persisted.Following.Order().SequenceEqual(earned.Following.Order()) &&
            persisted.Items.OrderBy(p => p.Key).SequenceEqual(earned.Items.OrderBy(p => p.Key)),
            "The earned checkpoint, goals, followers and items survive quit");
        Check.Equal(0L, await database.LongAsync("SELECT is_online FROM characters WHERE charId=110"),
            "Native character has logged out");
        Check.Equal(0L, await database.LongAsync(
            $"SELECT COUNT(*) FROM char_missions WHERE charId=110 AND missionId={mission}"),
            "No receipt for the selected mission");
        await File.WriteAllTextAsync(Path.Combine(context.OutputDirectory, "mission-phase-start.json"),
            JsonSerializer.Serialize(new
            {
                Mission = mission, Area = area, EarlierAreasPlayedHeadlessly = area - 1,
                earned.Identity, earned.Done, earned.Following, earned.Items,
                Props = earned.Objects.Select(o => o.Id),
                Actors = earned.Mobs.Where(m => m.Npc != 0).Select(m => new { m.Npc, m.Name, m.Health, m.Dead })
            }, new JsonSerializerOptions { WriteIndented = true }));
        return Array.Empty<byte>();
    }
}
