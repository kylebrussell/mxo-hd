using System.Text.Json;
using hds;

// Inventory the actual executable support, rather than matching source comments or CSV flags.
string output = args.Length > 0 ? args[0] : "research/abilities/server_coverage.json";
static string Consumer(ushort stat) => stat switch
{
    106 or 109 or 1056 or 1057 => "maximum vital",
    112 or 114 or 1058 or 1059 => "regeneration",
    163 or 522 or 524 or 996 or 997 => "combat accuracy",
    959 or 1009 or 1010 or 1027 or 1143 => "combat defence",
    144 or 180 or 181 or 1140 or 1144 => "combat resistance",
    159 or 171 or 172 or 1031 or 1139 or 1142 => "damage",
    174 or 1141 => "healing",
    995 or 1039 or 1040 or 1041 or 1042 or 1043 => "control duration",
    530 or 652 or 653 or 654 or 655 or 656 or 706 or 707 => "crafting",
    _ => "total only"
};
static string UtilityCoverage(ushort id) => id switch
{
    UtilityAbilities.DownloadMissionMap => "rshooks.py roll on mission accept, access flags stored, success FX; the client record that shows the map is not identified; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.DisarmTraps => "Disarm Traps on a followed TrappedState object (DisarmTraps_Test contest, lines 0x7600024e/f/0x76000250, Disarmed/Triggered state); trap damage and Detect Traps absent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.OpenLocks => "Pick Lock (rule 4000 DoorPickLock): Open Locks loaded + Lock_Pick/Digital_Camera tool, retail lines 0x7a000141/142/145; door or container moves to the script state; mission `locked=\"1\"` objects; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.HackConstructKey => "Hardline Hack Construct (rule 10024) ability/faction gate lines only; construct hacking itself absent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.DisableSecurityDevices => "Disable Security Devices use (rule 1012 SecurityPBXDisable): ability + DisableDeviceTool, Security Systems vs Difficulty contest, ACCESS GRANTED/ALARM chat, Disabled/Alarming state; no XP, alarm has no effect; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.RecallTeamMember => "Recall Team Member: team member, same district, not fighting/dead; caster/target lines 0x7a00015b/15a, phase-out FX, teleport to caster; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.RecallMissionTeam => "Recall Mission Team: every online team member as Recall Team Member; caster line 0x7a000164; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.FindDataNode => "Find Data Node toggle: locator tool gate (ID_ABILITY_TOOL_FAIL), start/stop lines; 80 ca-80 cf locate records not sent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    _ => ""
};
var rows = AbilityCatalog.All.Values.OrderBy(e => e.Id).Select(e =>
{
    bool cooldown = AbilityCooldowns.TryGet(e.Id, out var cd);
    bool buff = AbilityBuffs.TryGet(e.Id, out var b);
    bool timed = TempMods.TryGet(e.Id, out var t);
    bool hit = AbilityTargets.Retail.TryGetValue(e.Id, out var damage);
    return new
    {
        id = e.Id, cost = AbilityCosts.Cost(e.Id), cost_source = AbilityCosts.Retail.ContainsKey(e.Id) ? "capture" :
            e.Has("Ability/AbilityInnerStrengthCost") ? "stored" : "fallback",
        cooldown = cooldown ? new { cd.Seconds, cd.Group, cd.GroupSeconds } : null,
        buff_source = !buff ? "none" : AbilityBuffs.Retail.ContainsKey(e.Id) ? "capture" : AbilityScaledBuffs.TryGet(e.Id,50,out _) ? "stored character level" : "stored constants",
        buff_stats = buff ? b.Modifiers.Select(m => new { stat = m.Stat, value = m.Value, consumer = Consumer(m.Stat) }).ToArray() : null,
        timed = timed ? t.Mods.Select(m => new { state = m.Proc == TempMods.Proc.State ? m.Target : 0,
            stat = m.Proc == TempMods.Proc.Stat ? m.Target : 0, proc = m.Proc.ToString(), seconds = m.Seconds,
            chance = m.Chance, breaks = m.Break.ToString(), break_chance = m.BreakChance, protect_first_health_change = m.ProtectFirstHealthChange }).ToArray() : null,
        damage = hit ? new { min = damage!.MinDamage, max = damage.MaxDamage } : null,
        resurrection = AbilityResurrection.Handles(e.Id), utility = UtilityCoverage(e.Id),
        healing = e.Healing, direct_damage = e.DirectDamage, dot = e.DamageOverTime,
        player_healing = AbilityHealing.PlayerTarget(e),
        heal_base = e.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2 &&
            (m.Target == 108 && m.Number("DamageType", -1) == 3 || m.Text("StatModUpdateFunc") == "HOT"))
            .Select(m => AbilityValues.TryValue(m, 50, 50, out int value) ? (int?)value : null).ToArray(),
        passives = AbilityPassives.Modifiers(e.Id).Select(m => new { stat = m.Target,
            consumer = Consumer(m.Target),
            function = (int)m.Number("ValueFunction"),
            level50 = AbilityValues.TryValue(m, Math.Min(50, (int)e.Number("Ability/AbilityLevelCap", 50)), 50, out int value) ? (int?)value : null }).ToArray(),
        grants = AbilityProgression.GrantsOf(e.Id)
    };
});
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, "[\n" + string.Join(",\n", rows.Select(row => "  " + JsonSerializer.Serialize(row))) + "\n]\n");
Console.WriteLine("Wrote " + output);
