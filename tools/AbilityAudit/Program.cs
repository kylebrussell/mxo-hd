using System.Text.Json;
using hds;

// Inventory the actual executable support, rather than matching source comments or CSV flags.
string output = args.Length > 0 ? args[0] : "research/abilities/server_coverage.json";
static string Consumer(ushort stat) => stat switch
{
    204 => "stealth detection",
    209 => "stealth level",
    106 or 109 or 1056 or 1057 => "maximum vital",
    112 or 114 or 1058 or 1059 => "regeneration",
    163 or 522 or 524 or 996 or 997 => "combat accuracy",
    959 or 1009 or 1010 or 1027 or 1143 => "combat defence",
    1011 or 183 or 1029 or 1030 => "typed combat-special defence in interlock",
    144 or 180 or 181 or 1140 or 1144 => "combat resistance",
    159 or 171 or 172 or 1031 or 1034 or 1028 or 1139 or 1142 => "damage",
    174 or 1141 => "healing",
    575 => "Determination duration mitigation (known target totals)",
    995 or 1039 or 1040 or 1041 or 1042 or 1043 => "control duration",
    530 or 652 or 653 or 654 or 655 or 656 or 706 or 707 => "crafting",
    _ => "total only"
};
static string UtilityCoverage(ushort id) => id switch
{
    CompileSession.Ability => "tool-bound exclusive Compile UI session, flag1 activation/flag0 close, linked968 and toggle records, canonical60/812e authority and lifecycle cleanup; stored bit/fragment subset and recipe executor; equipment/ability-code economy unresolved; [docs/COMPILE-ITEMS.md](COMPILE-ITEMS.md)",
    UtilityAbilities.DownloadMissionMap => "retail mission-accept roll and cumulative flags; visible 80 c5 objective/enemy/known-lock minimap pings; bit interpretation ours, floorplan/team reveal absent; [docs/DATA-MINING.md](DATA-MINING.md)",
    UtilityAbilities.DisarmTraps => "Disarm Traps on a followed TrappedState object (DisarmTraps_Test contest, lines 0x7600024e/f/0x76000250, Disarmed/Triggered state); trap damage and Detect Traps absent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.OpenLocks => "Pick Lock (rule 4000 DoorPickLock): Open Locks loaded + Lock_Pick/Digital_Camera tool, retail lines 0x7a000141/142/145; door or container moves to the script state; mission `locked=\"1\"` objects; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.HackConstructKey => "Hardline Hack Construct (rule 10024) ability/faction gate lines only; construct hacking itself absent; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.DisableSecurityDevices => "Disable Security Devices use (rule 1012 SecurityPBXDisable): ability + DisableDeviceTool, Security Systems vs Difficulty contest, ACCESS GRANTED/ALARM chat, Disabled/Alarming state; no XP, alarm has no effect; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.RecallTeamMember => "Recall Team Member: team member, same district, not fighting/dead; caster/target lines 0x7a00015b/15a, phase-out FX, teleport to caster; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.RecallMissionTeam => "Recall Mission Team: every online team member as Recall Team Member; caster line 0x7a000164; [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)",
    UtilityAbilities.SeeLuggables => "goggles search flag highlights same-floor light luggables/carriers within 5 m through shipped luggable-aura FX; exact native white-glow path unverified; [docs/DATA-MINING.md](DATA-MINING.md)",
    UtilityAbilities.DetectLuggables => "handheld search pings nearest same-floor ground luggable through 80 c5 for its sourced10s activation; native waypoint record and distances beyond i8 ping range absent; [docs/DATA-MINING.md](DATA-MINING.md)",
    UtilityAbilities.FindDataNode => "distinct215/base and621/mastery locator searches, real loaded tool prerequisites,10s handheld expiry, native 80 cd/cb/ce FX and own-ID 80 bd activation; tool/pickup/death/unload/zone cleanup; no sourced ambient coordinates or tapping economy; [docs/DATA-MINING.md](DATA-MINING.md)",
    UtilityAbilities.LocateDataNode => "base215 search, separate cap5/cost5 and native activation ID; shared sourced search with mastery621; actual loaded tool prerequisites and handheld10s expiry; [docs/DATA-MINING.md](DATA-MINING.md)",
    AbilityEmpBlast.Ability => "script neutral0/5000 gated by explicit NPC1026 grant; caster-radius10, cast1s, reuse30s, NPC HP/death; production grant owners, item consumption, exact eligibility/TestFunction2 and cost default unresolved; [evidence](ABILITY-EMP-BLAST.md)",
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
        id = e.Id, cost = AbilityCosts.Cost(e.Id), cost_source = e.Id == CompileSession.Ability ? "stored tool/UI session" : AbilityCosts.Retail.ContainsKey(e.Id) ? "capture" :
            e.Has("Ability/AbilityInnerStrengthCost") ? "stored" : "fallback",
        cooldown = cooldown ? new { cd.Seconds, cd.Group, cd.GroupSeconds } : null,
        buff_buffer = e.Modifiers.Any(m => m.Text("StatModInitFunc") == "BufferOverflowInit"),
        health_callback = AbilityHealthCallbacks.Describe(e.Id),
        tactical_toggle = e.Id == 9 || e.Id == 15,
        buff_source = !buff ? "none" : AbilityBuffs.Retail.ContainsKey(e.Id) ? "capture" :
            AbilityHealthCallbacks.Template(e.Id) || AbilityBuffs.Scripted.ContainsKey(e.Id) || AbilityScaledBuffs.TryScriptedTimed(e.Id,50,out _) ? "stored scripted" : AbilityScaledBuffs.TryToggle(e.Id,50,out _) ? "stored learned level" : AbilityScaledBuffs.TryGet(e.Id,50,out _) ? "stored character level" : "stored constants",
        buff_drain = buff && b.DrainAmount > 0 ? new { amount=b.DrainAmount, interval=b.DrainSeconds } : null,
        buff_term = buff ? new { refund_is=b.RefundIS, heal=b.TermHeal, bits=b.DissemblingType } : null,
        buff_stats = buff ? b.Modifiers.Select(m => new { stat = m.Stat, value = m.Value, consumer = Consumer(m.Stat) }).ToArray() : null,
        timed = timed ? t.Mods.Select(m => new { state = m.Proc == TempMods.Proc.State ? m.Target : 0,
            stat = m.Proc == TempMods.Proc.Stat ? m.Target : 0, proc = m.Proc.ToString(), seconds = m.Seconds,
            chance = m.Chance, breaks = m.Break.ToString(), break_chance = m.BreakChance, protect_first_health_change = m.ProtectFirstHealthChange }).ToArray() : null,
        damage = hit ? new { min = damage!.MinDamage, max = damage.MaxDamage } : null,
        resurrection = AbilityResurrection.Handles(e.Id), utility = UtilityCoverage(e.Id), stealth = Stealth.Handles(e.Id),
        healing = e.Healing, direct_damage = e.DirectDamage, dot = e.DamageOverTime,
        player_healing = AbilityHealing.PlayerTarget(e),
        heal_base = e.Modifiers.Where(m => m.Number("WhenEnabled", -1) == 2 &&
            (m.Target == 108 && m.Number("DamageType", -1) == 3 || m.Text("StatModUpdateFunc") == "HOT"))
            .Select(m => AbilityValues.TryValue(m, 50, 50, out int value) ? (int?)value : null).ToArray(),
        passives = AbilityPassives.Modifiers(e.Id).Select(m => new { stat = m.Target,
            consumer = Consumer(m.Target),
            function = (int)m.Number("ValueFunction"),
            level50 = AbilityValues.TryValue(m, Math.Min(50, (int)e.Number("Ability/AbilityLevelCap", 50)), 50, out int value) ? (int?)value : null }).ToArray(),
        grants = AbilityProgression.GrantsOf(e.Id),
        aura = AbilityAuras.Handles(e.Id) ? new { effect = AbilityAuras.EffectOf(e.Id), friendly = AbilityAuras.Friendly(e.Id), drain = AbilityAuras.DrainOf(e.Id), interval = AbilityAuras.IntervalOf(e.Id) } : null,
        sweep = AbilityHostile.IsSweep(e.Id),
        consumable_sweep = ConsumableItems.Items.Any(p => p.Value.AbilityId == e.Id && ConsumableItems.IsUsable(p.Value) &&
            (p.Value.Effect == ConsumableItems.Effect.Cure || p.Value.Effect == ConsumableItems.Effect.ConditionSweep)),
        instant_hostile = AbilityHostile.Lands(e.Id),
        catalog_debuff = AbilityHostile.Catalog(e.Id) != null,
        // Combat styles: the active style's while-enabled modifiers at level 50 (CombatStyles)
        style = CombatStyles.Modifiers(e.Id).Select(m => new { stat = m.Target, consumer = Consumer(m.Target),
            level50 = AbilityValues.TryValue(m, 50, 50, out int value) ? (int?)value : null }).ToArray(),
        special_move_style = CombatStyles.IsSpecialMove(e.Id) ? CombatStyles.RequiredStyle(e.Id) : 0,
        sneak_bonus = SneakAttacks.Bonus(e.Id,true),
        special_animation = CombatAnimations.Special(e.Id,true,CombatTactic.Speed)?.Id,
        stealth_shield = StealthShieldAbilities.Describe(e.Id),
        special = CombatSpecials.Describe(e.Id)
    };
});
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
// Optional execution inventory fields are emitted only for their actual producers.
File.WriteAllText(output, "[\n" + string.Join(",\n", rows.Select(row => "  " + JsonSerializer.Serialize(
    JsonSerializer.SerializeToElement(row).EnumerateObject().Where(p => p.Name != "consumable_sweep" || p.Value.GetBoolean())
        .ToDictionary(p => p.Name, p => p.Value)))) + "\n]\n");
Console.WriteLine("Wrote " + output);
