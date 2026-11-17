using System.Xml.Linq;
using System.Collections.Concurrent;
using System.Text.Json;
using HeadlessClient;

namespace NativeQa.Modes;

public sealed class MissionCatalogSampleMode : NativeQaMode
{
    public override string Name => "mission-catalog-sample";
    private readonly ConcurrentDictionary<(string Missions, string? Phase), NativeQaModePlan> plans = new();

    public override NativeQaModePlan Plan => plans.GetOrAdd(
        (Environment.GetEnvironmentVariable("MXO_QA_MISSION_ID") ?? "1205",
            Environment.GetEnvironmentVariable("MXO_QA_MISSION_PHASE")),
        key => BuildPlan(key.Missions, key.Phase));

    internal static NativeQaModePlan BuildPlan(string value, string? phaseValue = null)
    {
            string? root=AppContext.BaseDirectory;
            while(root!=null&&!File.Exists(Path.Combine(root,"hds.sln")))root=Path.GetDirectoryName(root);
            if(root==null)throw new DirectoryNotFoundException("Mission catalog repository");
            var catalog=Directory.GetFiles(Path.Combine(root,"data","missions"),"*.xml").Select(XElement.Load).ToArray();
            var ids=value.Split(',',StringSplitOptions.TrimEntries).Select(v=>int.TryParse(v,out int n)?n:throw new ArgumentException("MXO_QA_MISSION_ID must contain catalog mission IDs")).ToArray();
            if(ids.Length>8||ids.Distinct().Count()!=ids.Length)throw new ArgumentException("Choose up to eight distinct catalog missions");
            var selected=ids.Select(candidate=>catalog.SingleOrDefault(m=>(int)m.Attribute("id")! == candidate)??throw new ArgumentException("Unknown catalog mission "+candidate)).ToArray();
            int id=ids[0];var mission=selected[0];
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
                DevCommands:true,environmentOverrides:new(){["MXO_MISSION_SITE_QA_SEED"]=ids.Length>1?"catalog":id.ToString(),["MXO_AGGRO"]="0",["MXO_MOB_WANDER"]="0"},
                extraQaMetadata:startArea.HasValue ? new()
                {
                    ["CheckpointMission"] = id, ["CheckpointArea"] = startArea.Value,
                    ["EarlierAreasPlayedHeadlessly"] = startArea.Value - 1
                } : null,
                readyMessages:startArea.HasValue
                    ? new[] { $"Mission {id} is already accepted at area {startArea}. "
                        + (startArea == 1 ? "The fixture accepted the offer headlessly. "
                            : $"The fixture played {startArea.Value - 1} earlier areas headlessly with movement and combat assistance. ")
                        + "Follow the measured approach below and play this area in the native client." }
                    : selected.Select(m=>$"Catalog mission {(int)m.Attribute("id")!}: {(string?)m.Attribute("title")}; contact {(string?)m.Attribute("contact")??"Tyndall"}. Use ?org {Math.Max(1,(int?)m.Attribute("org")??1)} before calling that contact. This mission is uncompleted.")
                        .Append("Each accepted phase prints measured street/lobby/floor positions. Other receipts are fixture setup; use ordinary mission interactions and rides. Movement/weaken are assistance.").ToArray());
    }

    public override async Task<byte[]> AfterStartAsync(NativeQaBootstrapContext context)
    {
        var metadata = Plan.ExtraQaMetadata;
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
        string root = context.RepositoryRoot ?? throw new InvalidOperationException("Phase start requires the catalog path.");
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
