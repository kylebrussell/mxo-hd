"""Build the per-ability audit from the shipped catalog and executable server coverage inventory.

dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py
"""
import collections
import csv
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
catalog = json.loads((ROOT / 'data/ability_catalog.json').read_text())
abilities = {r['id']: r for r in catalog['abilities']}
coverage_path = ROOT / 'research/abilities/server_coverage.json'
coverage = {r['id']: r for r in json.loads(coverage_path.read_text())}
assert set(coverage) == set(abilities)


def goid(value):
    return (int(value) & 0x7fffffff) >> 10 if int(value) & 0x80000000 else 0


names = {}
with (ROOT / 'research/community-data/abilities.txt').open() as stream:
    for row in csv.reader(stream):
        if len(row) >= 3:
            names[goid(row[0])] = row[2]


def name(i):
    return names.get(i, abilities.get(i, {}).get('name', str(i)).removesuffix('Ability'))


def fields(i):
    return abilities[i]['fields']


def mods(i):
    return [{k.split('/', 1)[1]: v for k, v in fields(i).items() if k.startswith(c['name'] + '/')}
            for c in abilities[i]['components'] if c['type'] == 41]


def dependencies(i):
    return {goid(v) for k, v in fields(i).items() if k == 'Ability/DisciplineBelongsTo' or k.endswith('/ReqAbilityID')} - {0, i}


def ancestors(i):
    seen, queue = {}, [(i, 0)]
    while queue:
        current, distance = queue.pop(0)
        if current in seen or current not in abilities:
            continue
        seen[current] = distance
        queue.extend((p, distance + 1) for p in sorted(dependencies(current)))
    return seen


def category(i):
    return int(fields(i).get('InvItem/GOCategoryID', 0)).to_bytes(4, 'little')[:3].decode('ascii', 'replace')


def role(i):
    chain = ancestors(i)
    roles = {137: 'Martial Artist', 128: 'Gunman', 155: 'Operative / Spy', 52: 'Hacker', 18: 'Coder', 397: 'Game Master'}
    matches = [(chain[p], text) for p, text in roles.items() if p in chain]
    if matches:
        return min(matches)[1]
    return {'C': 'Coder', 'H': 'Hacker', 'O': 'Operative', 'A': 'Awakened', 'W': 'Legacy / special weapon', 'G': 'Generic combat'}.get(category(i)[1:2], 'Shared / unassigned')


def tree(i):
    parent = goid(fields(i).get('Ability/DisciplineBelongsTo', 0))
    if parent:
        return name(parent)
    if fields(i).get('Ability/IsDiscipline'):
        return name(i)
    candidates = [(d, p) for p, d in ancestors(i).items() if p != i and fields(p).get('Ability/IsDiscipline')]
    return name(min(candidates)[1]) if candidates else 'Shared / no stored parent'


functions = collections.defaultdict(list)
for path in sorted((ROOT / 'research/client-scripts').rglob('*.py')):
    for line, text in enumerate(path.read_text(errors='replace').splitlines(), 1):
        match = re.match(r'^def (\w+)\(', text)
        if match:
            functions[match[1]].append((path.relative_to(ROOT).as_posix(), line))


def callbacks(i):
    explicit = {v for m in mods(i) for k, v in m.items() if k.endswith('Func') and isinstance(v, str) and v}
    stem = abilities[i]['name'].removesuffix('Ability')
    explicit.update(f for f in functions if f.startswith(stem + '_'))
    if i == 85:
        explicit.add('MezzingInit')
    return sorted(explicit)


def source_links(i):
    links = []
    for f in callbacks(i):
        matches = functions.get(f)
        if matches:
            path, line = matches[0]
            links.append(f'[{f}](../{path}#L{line})')
        else:
            links.append(f'`{f}` (native/unresolved)')
    return ', '.join(links)


damage_types = {0: 'physical', 1: 'ballistic', 2: 'viral', 3: 'heal', 7: 'thrown'}


def expected(i):
    result = []
    for m in mods(i):
        target, state = goid(m.get('TargetAttribute', 0)), goid(m.get('TargetState', 0))
        value = str(m.get('MinValue', '?'))
        if m.get('MaxValue') != m.get('MinValue'):
            value += '..' + str(m.get('MaxValue', '?'))
        duration = f"{m.get('Duration', '?'):}s" if m.get('DurationType') == 0 else 'instant' if m.get('DurationType') == 1 else 'while enabled' if m.get('DurationType') == 2 else 'native duration'
        proc = m.get('StatModUpdateFunc', '')
        if target == 108:
            result.append(f"{damage_types.get(m.get('DamageType'), 'health effect')} raw {value} ({duration})")
        elif proc == 'HOT' or str(proc).endswith('DOT'):
            result.append(f'{proc} raw total {value} ({duration})')
        elif state:
            result.append(f'{name(state)} ({duration})' + (f", {m['ProcTriggerChance'] * 100:g}% proc" if 'ProcTriggerChance' in m else ''))
        elif target:
            scaling = f" × native function {m['ValueFunction']}" if m.get('ValueFunction', 0) else ''
            if 'ValueScale' in m:
                scaling += f" / scale {m['ValueScale']}"
            result.append(f'{name(target)} raw {value}{scaling} ({duration})')
        grant = goid(m.get('TargetAbilityGrant', 0))
        if grant:
            result.append(f'grant {name(grant)} [{grant}]')
    if i == 967:
        result.append('heal 10% max health; 1% chance of 10s ability immunity')
    if 'HyperJump' in abilities[i]['name']:
        result.append('hyperjump movement / rank-dependent range')
    if callbacks(i):
        result.append(source_links(i))
    return '; '.join(dict.fromkeys(result)) or 'Native action / passive metadata; no decoded stored modifier'


UTILITY_NOTES = {538: 'presentation only; no effect executor; prerequisite tier only (no script references it); see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)', 539: 'presentation only; no effect executor; prerequisite tier only (no script references it); see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)', 540: 'presentation only; no effect executor; flag read by Find Data Node goggles (luggable glow); needs the visual locate record; see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)', 541: 'presentation only; no effect executor; flag read by Find Data Node handheld (nearest luggable); needs requestLuggableLocation; see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)'}

# Data-mining overrides; all other executors retain their runtime inventory and sourced notes.
DATA_MINING = {
    215: ('base215 search, separate cap5/cost5 and native activation ID; shared sourced search with mastery621; actual loaded tool prerequisites and handheld10s expiry; [docs/DATA-MINING.md](DATA-MINING.md)', 'partial'),
    44: ('retail mission-accept roll and cumulative flags; visible 80 c5 objective/enemy/known-lock minimap pings; bit interpretation ours, floorplan/team reveal absent; [docs/DATA-MINING.md](DATA-MINING.md)', 'partial'),
    538: ('GOB prerequisite tier; retains loadout/dependency metadata, no separate Python callback or stored stat to execute; [docs/DATA-MINING.md](DATA-MINING.md)', 'cosmetic'),
    539: ('GOB prerequisite tier; retains loadout/dependency metadata, no separate Python callback or stored stat to execute; tapping economy absent; [docs/DATA-MINING.md](DATA-MINING.md)', 'cosmetic'),
    540: ('goggles search flag highlights same-floor light luggables/carriers within 5 m through shipped luggable-aura FX; exact native white-glow path unverified; [docs/DATA-MINING.md](DATA-MINING.md)', 'partial'),
    541: ('handheld search pings nearest same-floor ground luggable through 80 c5 for its sourced10s activation; native waypoint record and distances beyond i8 ping range absent; [docs/DATA-MINING.md](DATA-MINING.md)', 'partial'),
    621: ('distinct215/base and621/mastery locator searches, real loaded tool prerequisites,10s handheld expiry, native 80 cd/cb/ce FX and own-ID 80 bd activation; tool/pickup/death/unload/zone cleanup; no sourced ambient coordinates or tapping economy; [docs/DATA-MINING.md](DATA-MINING.md)', 'partial'),
}

# Coder simulacra executors (hds/world/Structures/CoderPets.cs; evidence and choices in docs/CODER-PETS.md)
_summon = 'NPC_BASE_SIMULACRA spawn (retail creation layout), owned; native8176/77/78 window registration/removal/status; follow/assist/orders 81 7a-7c; dies; dismiss/resummon/logout/zone removal; level sim_clamp {}-{}; HP retail 337@7/1950@50, interpolated; damage = NPC round model (choice)'
PETS = {
    25: 'own live pet within10m, noncombat,50IS/4s;300s caster-level snapshot x0.5 melee159/ballistic171 percentage influence on Proxy hits only; ForceCombat519=40 has no consumer (partial)',
    **{a: _summon.format(lo, hi) + '; random proxy sheet' for a, lo, hi in ((27, 1, 4), (28, 4, 7), (29, 8, 17), (586, 18, 34), (594, 35, 50))},
    **{a: _summon.format(lo, hi) + '; Nuker sheet' for a, lo, hi in ((22, 4, 7), (601, 8, 17), (611, 18, 34), (616, 35, 50))},
    **{a: _summon.format(lo, hi) + '; Healer sheet heals owner (amount a choice)' for a, lo, hi in ((602, 12, 22), (612, 23, 43), (617, 44, 50))},
    20: 'own simulacrum only; Max Health +level x15 for 600s; four resistances +level x2 consumed by retaliation (OURS rating curve)',
    603: 'own simulacrum only; 600s IS cost -20%/regen +50% consumed by bounded pet IS; base pool/cost/regen OURS',
    34: 'own simulacrum only; DetonateProxyProc blast clamp(health, max/10, max/4) to NPCs within 8m, then 13000 to the simulacrum',
    19: 'own simulacrum only; 8000 to the simulacrum, +50 caster IS; DecoyDuration 60s unexplained',
    609: 'toggle; 5 IS/5s drain with deactivation; 699 typed Viral Damage172/Healing174 influence +level x0.5 within20m; Nuker hit/Healer heal percentage scaling; no Proxy damage',
    619: 'toggle; 5 IS/5s drain with deactivation; 700 accuracy +level x1.3 consumed by pet attack roll (OURS hit curve)',
    699: 'Daemon Boost 1.0 pulse: Viral Damage172/Healing174 influence +level x0.5 for5s; percentage Nuker damage/Healer heal only',
    700: 'Daemon Boost 2.0 pulse: sourced +level x1.3 accuracy, actual attack roll consumer (OURS base/curve)',
}
PET_REPAIRS = {30, 31, 587, 596, 615}


def server(i):
    if i == 7:
        return 'stored character level 99 prerequisite enforced on load; inaccessible at retail levels 1–50; purchase/upload remain possible; no vulnerability effect claimed; [evidence](DETECT-VULNERABILITY.md)'
    if i in DATA_MINING:
        return DATA_MINING[i][0]
    r, f = coverage[i], fields(i)
    parts = []
    if r.get('stealth'):
        parts.append('stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md)')
    if r.get('utility'):
        parts.append(r['utility'])
    if i in UTILITY_NOTES:
        parts.append(UTILITY_NOTES[i])
    if r['cost_source'] != 'fallback':
        parts.append(f"IS {r['cost']} ({r['cost_source']})")
    if r['cooldown']:
        cd = r['cooldown']
        parts.append(f"reuse {cd['Seconds']:g}s" + (f" / group {cd['Group']} {cd['GroupSeconds']:g}s" if cd['Group'] else ''))
    if r['damage']:
        parts.append(f"captured hit {r['damage']['min']}..{r['damage']['max']}")
    elif r['direct_damage'] and not r.get('health_callback'):
        parts.append('generic hit 50..80 / CSV value; scaling unresolved')
    if r['dot']:
        parts.append(('captured' if not r.get('catalog_debuff') else 'stored total/interval') + ' DOT executor'
                     if r['timed'] and any(m['proc'] == 'Dot' for m in r['timed']) else 'DOT missing')
    if r.get('resurrection'):
        parts.append('dead friendly player + native approval; ' + ('1 HP / 1 IS' if i == 325 else 'full HP / IS') + '; exact resurrect motion unverified')
    if r.get('health_callback'):
        parts.append(r['health_callback'])
    if r['healing'] and not r.get('health_callback'):
        if i in PET_REPAIRS:
            parts.append('heals own simulacrum: stored base; caster healing influence; player cast refused')
        elif not r.get('player_healing'):
            parts.append('pet/special healing target executor missing; player cast refused')
        else:
            amount = '10% target max HP' if i == 967 else 'stored base ' + '+'.join(str(v) if v is not None else '?' for v in r.get('heal_base', [])) + '; caster healing influence'
            parts.append('target/team heal' + (' over time' if any(m.get('StatModUpdateFunc') == 'HOT' for m in mods(i)) else '') + '; ' + amount)
    passive = [m for m in r.get('passives', []) if m['level50'] is not None]
    if passive:
        parts.append('loaded passive totals + records (@50: ' + ', '.join(f"{name(m['stat'])} {m['level50']:+d} [{m['consumer']}]" for m in passive) + ')')
    if r['buff_source'] != 'none':
        if r.get('tactical_toggle'):
            parts.append('script tactical toggle; precise/energized combat attack and damage multipliers; mutual refusal')
        elif i == 49:
            parts.append('Gaussian Blur kBlur0x40 self/watchers/late views; stored Combat Evasion+25 has no engagement consumer')
        elif r['buff_source'] in ('stored character level', 'stored learned level'):
            consumers = sorted({m['consumer'] for m in r['buff_stats'] if m['consumer'] != 'total only'})
            parts.append('buff records + ' + ('learned-level' if r['buff_source'] == 'stored learned level' else 'caster-level') + ' stat snapshot; ' + (', '.join(consumers) if consumers else 'numeric totals only; gameplay consumer missing'))
        else:
            parts.append(f"buff records + selected combat/vital stats ({r['buff_source']})")
    if r.get('buff_drain'):
        parts.append(f"IS drain {r['buff_drain']['amount']}/{r['buff_drain']['interval']:g}s; shortfall deactivates")
    if r.get('buff_term'):
        if r['buff_term']['refund_is']:
            parts.append(f"term refunds {r['buff_term']['refund_is']} IS to recipient")
        if r['buff_term']['heal']:
            parts.append(f"armor bit0x100; term heals {r['buff_term']['heal']} (death cleanup guarded)")
    if r['timed']:
        states = [f"{name(m['state'])} {m['seconds']:g}s" + (f" / break {m['breaks']}" if m['breaks'] != 'None' else '') for m in r['timed'] if m['state']]
        if states:
            parts.append(', '.join(states))
        if any(m['stat'] for m in r['timed']):
            parts.append(('stored' if r.get('catalog_debuff') else 'captured') + ' debuff records; accuracy/defence/resistance, IS cost, regen and max vitals consumed; other stats client-side/record only')
    if r.get('aura'):
        a = r['aura']
        parts.append(f"toggled aura: IS drain {a['drain']}/{a['interval']:g}s, pulses {a['effect']} on {'friendly team players' if a.get('friendly') else 'hostile NPC/PvP targets'} in its effect radius")
    if r.get('sweep'):
        parts.append('instant25% cleanse of debuff parents (UDB0, not exempt)' if i == 868 else 'pulse sweep removes cleansable debuff parents (UDB0, not exempt)')
    if r.get('buff_buffer'):
        parts.append('native script buffer reservation min(Value,free); baseline100+4level is OURS; refuses new upgrades until removal/expiry')
    if r.get('instant_hostile'):
        parts.append('instant script: removable buffs (UBF0) / current IS on landing')
    if r['grants']:
        parts.append('load/unload/startup grants ' + ','.join(map(str, r['grants'])))
    style = [m for m in r.get('style', []) if m['level50'] is not None]
    if style:
        parts.append('active-style modifiers via 3e/80 b9 (@50: ' + ', '.join(f"{name(m['stat'])} {m['level50']:+d}" for m in style) + '); ' + ('weapon-specific interlock mapping unverified' if i in (129,147,158) else 'style interlock exchanges'))
    if r.get('special_move_style'):
        parts.append(f"special move: needs {name(r['special_move_style'])} (auto-switch when loaded)")
    if r.get('sneak_bonus'):
        parts.append(f"stealth-started opener +{r['sneak_bonus']} scripted bonus (trigger/addition policy; COMBAT-DEPTH.md)")
    if r.get('special_animation'):
        parts.append(f"named ILDB special choreography {r['special_animation']} (name mapping policy)")
    if r.get('special'):
        parts.append(r['special'])
    if 'HyperJump' in abilities[i]['name']:
        parts.append('jump trajectory + control gate; rank/limits incomplete')
    if not r.get('stealth') and ('Sneak' in abilities[i]['name'] or 'Conceal' in abilities[i]['name']):
        parts.append('stealth detection/AI incomplete')
    if i in PETS:
        parts.append(PETS[i])
    return '; '.join(parts) or 'presentation only; no effect executor'


def status(i):
    if i == 7:
        return 'inaccessible'
    if i in DATA_MINING:
        return DATA_MINING[i][1]
    r = coverage[i]
    if i in PETS:
        return 'partial'
    # Conservative: metadata, a raw constant or a packet helper alone is not retail gameplay parity.
    if i == 49 or r['buff_source'] in ('stored character level', 'stored learned level') and not any(m['consumer'] != 'total only' for m in r['buff_stats']):
        return 'cosmetic'
    if r.get('health_callback') or r.get('stealth') or r.get('utility') or r.get('resurrection') or r['healing'] or r['direct_damage'] or r['buff_source'] != 'none' or r['timed'] or r['grants'] or r.get('aura') or r.get('sweep') or r.get('instant_hostile') or any(m['level50'] is not None and m['consumer'] != 'total only' for m in r.get('passives', [])) or r.get('style') or r.get('special') or 'HyperJump' in abilities[i]['name']:
        return 'partial'
    if fields(i).get('Ability/IsDiscipline') or any(m.get('WhenEnabled') == 0 for m in mods(i)):
        return 'cosmetic'
    return 'missing'


seeds = {i for i in abilities if fields(i).get('Ability/AbilityClass', 0) == 0 and
         (fields(i).get('Ability/InventoryMemCost', 0) > 0 or fields(i).get('Ability/IsDiscipline')) and not abilities[i]['name'].startswith('NPC')}
player = set(seeds)
while True:
    grants = {goid(m.get('TargetAbilityGrant', 0)) for i in player for m in mods(i)} - {0}
    new = grants.intersection(abilities) - player
    if not new:
        break
    player.update(new)
counts = collections.Counter(status(i) for i in player)
header = f'''# Player ability parity audit

This is a conservative audit of the shipped 7.6005 templates against the executable server on this branch. **No ability is declared retail-complete merely because it sends an animation or a modifier record.** Counts: {len(player)} player-loadable candidates and their granted abilities ({len(seeds)} memory/discipline candidates, {len(player - seeds)} additional grants); {', '.join(f'{v} {k}' for k, v in sorted(counts.items()))}. No full-parity `done` claims yet.

## Inventory and evidence

`data/abilityIDs.csv` contains 9,972 data rows, including unnamed engine IDs and repeated aliases at +4096/+8192. Its named rows resolve to **1,404 canonical shipped templates**. `data/ability_catalog.json` retains those templates, their component names, stored attributes and original stored AbilityID (171 differ from the template index). It omits absent engine defaults rather than inventing zeroes. GOB SHA-256: `{catalog['gob_sha256']}`.

The main table includes every non-NPC class-0 template with a positive inventory memory cost or a discipline flag, plus the transitive closure of its grants. This intentionally retains legacy, GM and ambiguous discipline candidates; it does not claim the retail UI exposed all of them. Access is tagged below. The canonical appendix accounts for every remaining template, including NPC, item, state and stat records. CSV aliases normalize to the canonical ID for execution. Friendly names come from the community name table; GOIDs, not suggestive prerequisite component names, determine dependency links.

Tree is the stored discipline parent, or the nearest prerequisite discipline where none is stored. Archetype follows prerequisite GOIDs, with category fallback. A shared ability can be usable by several builds. Each modifier's raw stored numbers are **inputs to native value/duration functions**, not asserted final retail damage or healing. Actual captured values take precedence in the server.

Read alongside [`abilities.py`](../research/client-scripts/abilities.py), [`tempmods.py`](../research/client-scripts/tempmods.py), [`combat.py`](../research/client-scripts/combat.py), [`combat_calculations.py`](../research/client-scripts/combat_calculations.py) and the linked `ability/` callbacks. `augments.py` and `ability/soldier/old_sm_*` are legacy code not imported by `abilities.GetImportObjects`; they are not silently treated as the active rules. `discovery`, `abilitylib`, `combatlib`, constants and console-variable defaults remain native dependencies. Callback links identify the decompiled definitions, not proof that the server implements them.

## Status meanings and shared gaps

- **done**: all identified behavior and relevant gameplay paths validated. None assigned yet.
- **partial**: some real effects execute; a listed behavior, scaling, target rule, condition or native presentation remains incomplete.
- **missing**: intended effect lacks an executor; an animation, cost or generic damage fallback does not count.
- **inaccessible**: primary client load prerequisite exceeds the retail character cap; no effect parity is claimed.
- **cosmetic**: discipline/passive loads or displays, but its native passive/stat behavior is not fully applied.

The new catalog fallback supplies explicit costs, cast times, recast/family timers and timed controls, preserving retail overrides. Insufficient IS and incapacitated/powerless casts are rejected. Root blocks movement and jumps; stun/mez blocks actions; powerless blocks abilities; disarm blocks weapon fire; enrage blocks withdrawal. Pacify and scripted roots break as their health callbacks specify. A negative chance-root proc protects its first health change (including healing), then flips positive for later damage checks, as in the script. A control state cannot stack through its immunity timer.

Hacker area damage and controls reach visible NPCs on the same floor. DOTs on secondary targets keep ticking without replacing the current fight; kills share death claiming, XP, mission and loot handling. The merged PvP paths also use action gates, combat-stat bonuses and health-triggered control breaks. LOS, full hit/deflection tests, cone shapes, multi-NPC combat AI, resistance-derived ability damage and some PvP target rules remain incomplete.

Loaded passives now evaluate the deterministic native value functions: learned ability level (2), character level (3), and fixed ranges. Scale uses the stored binary32 value, x87 multiplication/truncation, min offset and positive max cap. Owned loadouts, upgrades, new-world entry and Death Effect updates rebuild totals and retail-style `80 bc 45 00` records; obsolete unconditional Hyper-Jump/Write Code records are removed. Shared grants contribute once. Vitals, regeneration, combat accuracy/defence/resistance bonuses, damage influence, control durations and crafting use these totals. Control duration bonuses follow the stored class-4 state links (stun, root, pacify, blind, powerless, confuse) and keep fractional seconds. Immunity timers and break rules retain their separate definitions. Percent bonuses apply to the server's existing base formulas; those bases, character-attribute links, several stat families, complete stealth/detection attributes, complete target Determination/attribute bases and special callbacks are still incomplete. UDB0 on-use debuffs now use target stat575 for script duration mitigation; self effects and immunity timers are excluded (COMBAT-DEPTH.md). Stealth state/visibility/detection now has an executor; see [STEALTH.md](STEALTH.md) for sourced behavior and choices. A passive record is not proof that every consuming system is implemented. See [native modifier evidence and remaining limits](ABILITY-MODIFIERS.md).

Friendly direct/group heals and HOTs use each shipped template's deterministic base and the caster's healing influence, replacing the target-quarter-max-health placeholder. HOT uses its stored total and the script tick formula. Simulacra repairs heal only the caster's own simulacrum; player targets are refused. Mobius Code retains its explicit 10% target-max heal; its 1% immunity proc is still missing. These are sourced base values, with native influence multiplication/truncation; exact final retail amounts still require native comparison with a known caster build and complete attribute contributions. Fixed stored self/friendly buffs gain records/family expiry; captured values win. Deterministic numeric timed buffs using character-level function3 resolve from the caster at application, with active snapshots shared by stat/vital consumers and exact removal records. Script/aura/state/random effects and indefinite style scaling stay excluded; [scaled-buff evidence](ABILITY-SCALED-BUFFS.md) lists native comparison points. Restore RSI325, Revitalize326, Revive375 and Rejuvenate416 now use dead-friendly targets, the native81 88 consent exchange and their script-defined resource restoration. Refusal, timeout, wrong-ability replies, replies without a live offer and reconstruction are rejected; the native short reply carries no per-offer nonce. Exact script-specific resurrection motion and native dialog acceptance remain unverified; see [resurrection evidence](ABILITY-RESURRECTION.md). Coder simulacra (Remote Proxy, Logic/Patch Daemon) spawn as owned NPC_BASE_SIMULACRA views with follow/assist AI, the client's orders, death and lifetime, plus the own-simulacrum abilities and Daemon Boost; see [CODER-PETS.md](CODER-PETS.md) for what is retail evidence and what is chosen.

Combat styles (Self Defense, Aikido, Karate, Kung-Fu, Hacker/Coder/Spy Style) become active through `3e`. The active style's while-enabled modifiers join the passive totals, and its ILDB exchange set drives the interlock moves all viewers see. `CombatSpecialMove` abilities need their stored stance: a loaded style is switched to, an unloaded one refuses the move. Thrown DOTs, Berserker/Side Roll caster buffs, Taunt aggro and the Exile weapons have executors. Their inferences and gaps (RandomCombatStateChance, weakness by NPC name, unmapped special choreography) are in [COMBAT-STYLES.md](COMBAT-STYLES.md). `43` MartialArt is unreachable in 7.6005.

`AbilityEffects`, `Virii`, `TempMods` and other modules contain script-derived pure helpers for corrosion, disables and immunity, but helpers without live handler wiring remain missing/partial in this audit.

## Per-ability table

Access `memory` means the stored memory cost is positive; `discipline` means its discipline flag is set; `grant` means reachable through a player candidate. A grant can also carry memory metadata. Values are raw unless explicitly described as captured.

| ID / ability | Access | Archetype / tree | Shipped behavior / callbacks | Server behavior | Status |
|---|---|---|---|---|---|
'''
rows = []
for i in sorted(player):
    access = 'memory' if fields(i).get('Ability/InventoryMemCost', 0) > 0 else 'discipline' if i in seeds else 'grant'
    if any(i in coverage[p]['grants'] for p in player if p != i):
        access += ', grant'
    rows.append([str(i), name(i), access, role(i), tree(i), expected(i), server(i), status(i)])
table = '\n'.join('| ' + ' | '.join([f'{r[0]} {r[1]}', r[2], f'{r[3]} / {r[4]}', r[5], r[6], r[7]]) + ' |' for r in rows)
appendix = '''\n\n## Canonical inventory outside the player table

These records are not silently dropped. Scope is the stored class or an explicit NPC name; other class-0 records require evidence that they are loadable rather than item/event/internal actions. Their full decoded fields remain in the JSON catalog.

| ID | Template | Scope / exclusion |
|---:|---|---|
'''
classes = {1: 'core attribute', 2: 'engine stat', 4: 'engine state', 5: 'personality archetype'}
for i, ability in abilities.items():
    if i not in player:
        scope = 'NPC' if ability['name'].startswith('NPC') else classes.get(fields(i).get('Ability/AbilityClass'), 'item / event / internal / unproven loadability')
        appendix += f"| {i} | {ability['name']} | {scope} |\n"
footer = '''
## Reproduction and checks

1. `python research/abilities/extract_catalog.py --client <read-only MxO folder>`
2. `dotnet run --project tools/AbilityAudit`
3. `python research/abilities/build_audit.py`

The runtime inventory calls the real `AbilityCosts`, `AbilityCooldowns`, `AbilityBuffs`, `TempMods`, `AbilityTargets` and grant lookup, rather than searching comments. The generated [CSV](ABILITY-PARITY.csv) is filterable by archetype, tree and status. Ability changes have unit coverage in `AbilityParityTests.cs` and four fresh-server `ability-parity-*` scenarios for support, controls, area effects and IS denial. Native ability checks are in [ABILITY-PARITY-QA.md](ABILITY-PARITY-QA.md) and the handoff file; this ability task does not acquire the client lock. The earlier PR94 native validation is separately recorded in `PVP.md`.
'''
(ROOT / 'docs/ABILITY-PARITY.md').write_text(header + table + appendix + footer, encoding='utf-8')
with (ROOT / 'docs/ABILITY-PARITY.csv').open('w', newline='', encoding='utf-8') as stream:
    writer = csv.writer(stream, lineterminator='\n')
    writer.writerow(['id', 'ability', 'access', 'archetype', 'tree', 'shipped_behavior', 'server_behavior', 'status'])
    writer.writerows(rows)
print(f'{len(player)} player candidates/grants, {len(abilities) - len(player)} other templates; {dict(counts)}')
