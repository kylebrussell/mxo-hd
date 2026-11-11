using System.Text.Json;
using hds;

// Inventory the actual executable support, rather than matching source comments or CSV flags.
string output = args.Length > 0 ? args[0] : "research/abilities/server_coverage.json";
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
        buff_source = !buff ? "none" : AbilityBuffs.Retail.ContainsKey(e.Id) ? "capture" : "stored constants",
        buff_stats = buff ? b.Modifiers.Select(m => new { stat = m.Stat, value = m.Value }).ToArray() : null,
        timed = timed ? t.Mods.Select(m => new { state = m.Proc == TempMods.Proc.State ? m.Target : 0,
            stat = m.Proc == TempMods.Proc.Stat ? m.Target : 0, proc = m.Proc.ToString(), seconds = m.Seconds,
            chance = m.Chance, breaks = m.Break.ToString(), break_chance = m.BreakChance, ignore_first_damage = m.IgnoreFirstDamage }).ToArray() : null,
        damage = hit ? new { min = damage.MinDamage, max = damage.MaxDamage } : null,
        healing = e.Healing, direct_damage = e.DirectDamage, dot = e.DamageOverTime,
        grants = AbilityProgression.GrantsOf(e.Id)
    };
});
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine("Wrote " + output);
