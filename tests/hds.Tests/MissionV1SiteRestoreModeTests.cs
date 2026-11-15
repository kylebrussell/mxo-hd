using System.Text.RegularExpressions;
using System.Xml.Linq;
using NativeQa;
using NativeQa.Modes;

namespace hds.Tests;

public class MissionV1SiteRestoreModeTests
{
    static string Repo {get{string p=AppContext.BaseDirectory;while(!File.Exists(Path.Combine(p,"hds.sln")))p=Path.GetDirectoryName(p)!;return p;}}
    [Fact] public void DiscoveredPlanSeedsOnlyEarlierReceiptsAndNeverAnActiveMissionOrItems()
    {
        var mode=NativeQaModes.RequireKnown("mission-v1-site-restore");Assert.IsType<MissionV1SiteRestoreMode>(mode);
        var plan=mode.Plan;Assert.True(plan.DevCommands);Assert.False(plan.GmCharacter110);Assert.False(plan.FixedCallbackRolls);
        string sql=string.Join("\n",plan.BeforeLoadout.Concat(plan.AfterLoadout).Concat(plan.AfterHelpers));
        var receipts=Regex.Matches(sql,@"\(110,(\d+)\)").Select(m=>int.Parse(m.Groups[1].Value)).ToArray();
        Assert.Equal(MissionV1SiteRestoreMode.EarlierMissions,receipts);Assert.Equal(29,receipts.Distinct().Count());
        Assert.Contains(101208,receipts);Assert.Contains(101209,receipts);Assert.DoesNotContain(101210,receipts);Assert.DoesNotContain(101211,receipts);
        Assert.DoesNotContain("char_active_missions",sql);Assert.DoesNotContain("inventory",sql,StringComparison.OrdinalIgnoreCase);
        Assert.Equal("101208",plan.EnvironmentOverrides["MXO_MISSION_SITE_QA_SEED"]);
        var definitions=Directory.GetFiles(Path.Combine(Repo,"data/missions"),"zion_1_2_*.xml").Select(XElement.Load).OrderBy(m=>(int)m.Attribute("id")!).ToArray();
        Assert.Equal(101210,(int)definitions.First(m=>!receipts.Contains((int)m.Attribute("id")!)).Attribute("id")!);
        var selected=definitions.Single(m=>(int)m.Attribute("id")! ==101210);
        Assert.Equal(new[]{"Talk","Escort"},selected.Elements("phase").First().Elements("objective").Select(o=>(string)o.Attribute("type")!));
        Assert.DoesNotContain(selected.Descendants("objective"),o=>(string?)o.Attribute("type")=="Get");
    }
}
