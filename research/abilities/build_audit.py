"""Build the per-ability audit from the shipped catalog and executable server coverage inventory.

dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py
"""
from audit_links import rebase_links
from parity_progress import Evidence, summarize
import argparse
import collections
import csv
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--output-dir', type=Path, help='Write the current Markdown, CSV and coverage review bundle here')
parser.add_argument('--source-base-url', help='Pinned HTTPS repository blob URL for portable artifact evidence links')
parser.add_argument('--coverage', type=Path, help='Executable AbilityAudit JSON inventory (default: ignored artifacts/ability-audit/server_coverage.json)')
parser.add_argument('--baseline', type=Path, help='Prior generated CSV for status transitions and remaining-item changes')
parser.add_argument('--update-markdown-snapshot', action='store_true', help='Explicit docs-only historical snapshot refresh')
args=parser.parse_args()
baseline = args.baseline.read_text(encoding='utf-8-sig') if args.baseline else None
if args.output_dir and args.update_markdown_snapshot:
    parser.error('--output-dir and --update-markdown-snapshot are separate workflows')
artifact_dir=args.output_dir or ROOT / 'artifacts/ability-audit'
coverage_path = args.coverage or ROOT / 'artifacts/ability-audit/server_coverage.json'
if not coverage_path.is_file():
    parser.error(f'Executable coverage not found: {coverage_path}. Run dotnet run --project tools/AbilityAudit first, or pass --coverage with a freshly generated inventory. The tracked historical inventory is not a current fallback.')
artifact_dir.mkdir(parents=True,exist_ok=True)

catalog = json.loads((ROOT / 'data/ability_catalog.json').read_text())
abilities = {r['id']: r for r in catalog['abilities']}
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


LEGACY_SOLDIER_MODULES = 'research/client-scripts/ability/soldier/old_sm_'


def callbacks(i, *, include_legacy=False):
    explicit = {v for m in mods(i) for k, v in m.items() if k.endswith('Func') and isinstance(v, str) and v}
    stem = abilities[i]['name'].removesuffix('Ability')
    # abilities.GetImportObjects omits old_sm_* from the normal server startup.
    explicit.update(f for f, matches in functions.items() if f.startswith(stem + '_')
                    and (include_legacy or any(not path.startswith(LEGACY_SOLDIER_MODULES) for path, _ in matches)))
    if i == 85:
        explicit.add('MezzingInit')
    return sorted(explicit)


def source_links(i):
    links = []
    active = set(callbacks(i))
    for f in callbacks(i, include_legacy=True):
        matches = functions.get(f)
        if matches:
            path, line = next((match for match in matches if not match[0].startswith(LEGACY_SOLDIER_MODULES)), matches[0])
            link = f'[{f}](../{path}#L{line})'
            links.append(link if f in active else f'{link} (legacy module omitted at startup)')
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
    sources = source_links(i)
    if sources:
        result.append(sources)
    return '; '.join(dict.fromkeys(result)) or 'Native action / passive metadata; no decoded stored modifier'


UTILITY_NOTES = {538: 'presentation only; no effect executor; prerequisite tier only (no script references it); see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)', 539: 'presentation only; no effect executor; prerequisite tier only (no script references it); see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)', 540: 'presentation only; no effect executor; flag read by Find Data Node goggles (luggable glow); needs the visual locate record; see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)', 541: 'presentation only; no effect executor; flag read by Find Data Node handheld (nearest luggable); needs requestLuggableLocation; see [docs/UTILITY-ABILITIES.md](UTILITY-ABILITIES.md)'}

# Data-mining overrides; all other executors retain their runtime inventory and sourced notes.
DATA_MINING = {
    345: ('direct stat177 existing captured+21/30s UDB0 target-centred NPC/PvP radius; native owner/UI formula, server landing policy follows74; linked75/1055 unresolved; [evidence](ABILITY-DEBUFF-RADIUS.md)', 'partial'),
    215: ('base215 search, separate cap5/cost5 and native activation ID; shared sourced search with mastery621; actual loaded tool prerequisites and handheld10s expiry; [docs/DATA-MINING.md](DATA-MINING.md)', 'partial'),
    44: ('shipped mission-accept roll and six cumulative flags; stock 80 c5 marker policy for objects, hostiles, locked doors, friendly NPCs, entrances and exits; same-floor pings and leader objective/hostile sharing; unload ends the reveal, with no restart on reload in the same mission; [docs/DATA-MINING.md](DATA-MINING.md)', 'partial'),
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
    26: 'loaded, outside combat; live opposing player pet within16m through its owner; fixed13000 type5 and ordinary pet death; exact retail opponent rules and lethal activation FX unresolved',
    **{a: _summon.format(lo, hi) + '; random proxy sheet' for a, lo, hi in ((27, 1, 4), (29, 8, 17), (586, 18, 34), (594, 35, 50))},
    28: _summon.format(4, 7) + '; random proxy sheet; prepares the replacement before retiring the previous summon and publishes one replacement; stock Deactivate removes the current proxy',
    **{a: _summon.format(lo, hi) + '; Nuker sheet' for a, lo, hi in ((22, 4, 7), (601, 8, 17), (611, 18, 34), (616, 35, 50))},
    **{a: _summon.format(lo, hi) + '; Healer sheet heals owner (amount a choice)' for a, lo, hi in ((602, 12, 22), (612, 23, 43), (617, 44, 50))},
    20: 'own simulacrum only; Max Health +level x15 and four resistance ratings +level x2 for 600s; NPC melee retaliation consumes the Fortify rating using the shipped armor curve normalized to ordinary pet damage (OURS pet caller)',
    603: 'own simulacrum only; sourced 50m range scaled by selection-range debuff at admission and landing; stock brackets the 10m reduced range; 600s IS cost -20%/regen +50%; base pool/cost/regen OURS',
    34: 'own simulacrum only; pet-centred 8m blast on NPCs and eligible hostile players; DetonateProxyProc internal viral child 1109 uses pet-level typed resistance; owner receives normal credit; pet takes 13000 after 0.5s',
    19: 'own simulacrum only; stored 8000 type-5 hit and +50 caster IS verified on a surviving fortified Proxy; DecoyDuration 60s unresolved',
    609: 'toggle; 5 IS/5s drain with deactivation; 699 typed Viral Damage172/Healing174 influence +level x0.5 within20m; Nuker hit/Healer heal percentage scaling; no Proxy damage',
    619: 'toggle; 5 IS/5s drain with deactivation; 700 accuracy +level x1.3 consumed by pet attack roll (OURS hit curve)',
    699: 'Daemon Boost 1.0 pulse: Viral Damage172/Healing174 influence +level x0.5 for5s; percentage Nuker damage/Healer heal only',
    700: 'Daemon Boost 2.0 pulse: sourced +level x1.3 accuracy, actual attack roll consumer (OURS base/curve)',
}
PET_REPAIRS = {30, 31, 587, 596, 615}

PASSIVE_LIMITS = {
    18: 'discipline metadata; no stored stat or decoded gameplay callback',
    35: 'ApplyAuraEffect pulses 697 on friendly simulacra; incoming viral casts on player pets are unsupported',
    52: 'shipped ComputerHack can check the loaded Hacker ability; the numeric Computer Hacking contest follows an unconditional return',
    75: 'the direct stat177 radius consumer has no recovered link to bonus1055; [evidence](ABILITY-DEBUFF-RADIUS.md)',
    161: 'shipped BaseSneakAttackTestFn returns success before any detection contest; the declared dependency alone does not read the stat',
    165: 'TapDataNode_Subject delegates to native activateTapDataNode; no recovered tapping executor; [evidence](DATA-MINING.md)',
    297: 'shipped Encrypt/Decrypt reads the learned Cryptography ability; executable support for the separate Decryption Skill total is unresolved',
    397: 'discipline metadata; no stored stat or decoded gameplay callback',
    512: 'Repair Skill is stored but no shipped Python reader or item repair executor is established',
    515: 'WriteCodeTest discount is applied to the supported ordinary Writer path; wider category/signal-boost behavior unresolved',
    537: 'discipline prerequisite with no stored stat or decoded gameplay callback; [evidence](DATA-MINING.md)',
    622: 'tap skill total and locator prerequisite are available; native tapping and yield are unresolved; [evidence](DATA-MINING.md)',
    697: 'stored selector19 targets friendly simulacra; stat183 has no supported incoming viral cast path for player pets',
    1199: 'organization discipline metadata; no stored stat or decoded gameplay callback',
}


def server(i):
    if i == 1149:
        if any(m['consumer'] != 'total only' for m in coverage[i].get('buff_stats', [])):
            return "OURS: capped Initiative1044 adds attack-base points once in NPC/PvP interlock, excluding escape/freefire/freecast; sourced20s health snapshot, IS10/reuse90/buffer10; retail gameplay caller unresolved; [policy](DESPERATION-INITIATIVE.md)"
        return "COSMETIC:20s health/InitiativeBonus snapshot1044; IS10/reuse90/buffer10; expiry/death/unload cleanup; no recovered combat consumer; [source boundary](DESPERATION.md)"
    if i == 7:
        return 'stored character level 99 prerequisite enforced on load; inaccessible at retail levels 1–50; purchase/upload remain possible; no vulnerability effect claimed; [evidence](DETECT-VULNERABILITY.md)'
    if i == 13:
        return 'stored level 99 selected-load prerequisite with no parent or grant path in the shipped ability catalog; seeded damage, periodic FX and IS drain verified; [evidence](ABILITY-AWAKENED-COMBAT-ACTIVES.md#hyper-strength-access)'
    if i in DATA_MINING:
        return DATA_MINING[i][0]
    r, f = coverage[i], fields(i)
    parts = []
    if i == 1124:
        parts.append('exact native tool prerequisite enforced before cost/cast; none of869 shipped component10 tools names1124 (470 names1125); effect remains unresolved, no alias to384; [evidence](ABILITY-1124.md)')
    if r.get('stealth'):
        parts.append('stealth state, visibility and detection executor; script tests/drains; [details and limits](STEALTH.md)')
    if r.get('utility'):
        parts.append(r['utility'])
    if i in UTILITY_NOTES:
        parts.append(UTILITY_NOTES[i])
    if r.get('multifighting'):
        parts.append(r['multifighting'])
    if r['cost_source'] != 'fallback':
        parts.append(f"IS {r['cost']} ({r['cost_source']})")
    if r['cooldown']:
        cd = r['cooldown']
        parts.append(f"reuse {cd['Seconds']:g}s" + (f" / group {cd['Group']} {cd['GroupSeconds']:g}s" if cd['Group'] else ''))
    starter_viral_bases = {53: 12, 57: 8, 58: 11, 59: 17, 60: 7}
    if i in starter_viral_bases and not r['damage']:
        parts.append(f'shipped fixed viral base {starter_viral_bases[i]}; live selected enemy required before cast or reuse; normal viral hit/deflect contest, caster influence scaling and typed resistance; stock base and influenced hits observed')
        if i == 60:
            parts.append('same typed damage path for each legal splash recipient')
    elif i in (105, 176) and not r['damage']:
        base, hit = (8, 8) if i == 105 else (17, 18)
        admission = 'current live interlock opponent only; wider damage scaling unresolved' if i == 105 else 'wider damage and admission unresolved'
        parts.append(f'stored fixed ballistic {base} base with incomplete legacy endpoints; stock interlock landed {hit}; {admission}')
    elif r['damage']:
        parts.append(f"captured hit {r['damage']['min']}..{r['damage']['max']}")
    elif i == 504:
        parts.append('stored fixed ballistic base 22 with incomplete legacy endpoints; armor piercing and complete damage parity unresolved')
    elif i in (36, 514):
        base = 4 if i == 36 else 6
        parts.append(f'stored fixed viral {base} base with incomplete legacy endpoints; 16 m live hostile selection before cost and 2 m target-centred splash verified in stock client; retail viral scaling unresolved')
    elif i in (197, 198) and not r['damage']:
        base = {197: 16, 198: 7}[i]
        parts.append(f'stored fixed physical base {base} before ordinary influence and resistance')
        if i == 197:
            parts.append('requires Dazed on the live interlock opponent before charging')
    elif i in (254, 255):
        base = {254: 18, 255: 16}[i]
        parts.append(f'stored fixed ballistic base {base} before influence and resistance; requires a live hostile selection at rest; retail scaling unresolved')
    elif i == 130:
        parts.append('stored fixed ballistic 9 base when legacy endpoints are incomplete; requires a live opposing selection within 20m before charging the cast; stock reason34 verified; wider damage and targeting unresolved')
    elif i == 134:
        parts.append('stored fixed thrown base 13 before typed influence and resistance; stock Spy interlock landed 14; wider damage and targeting unresolved')
    elif i == 152:
        parts.append('stored fixed thrown base 28 when legacy endpoints are incomplete; stealth opener trigger still unresolved')
    elif i == 145:
        parts.append('stored fixed physical base 7 before typed influence and resistance; current live interlock opponent only; retail scaling unresolved')
    elif i == 605:
        parts.append('stored fixed physical base 17 before typed influence and resistance; stock Coder interlock landed 19; wider targeting and outcome variants unresolved')
    elif i == 33:
        parts.append('stored fixed viral base 22 with incomplete legacy endpoints; 16 m live enemy selection before cost verified in stock client; full retail scaling unresolved')
    elif i == 279:
        parts.append('stored function-3 thrown base with live enemy selection required; stock level-20 shot landed 37; wider damage and conditional ordering unresolved')
    elif i == 280:
        parts.append('stored fixed thrown base 1 with live enemy selection required; stock landed 1 and rooted for 15.75s with Wicked Aim; wider targeting and breaks unresolved')
    elif i == 1135:
        parts.append('stored fixed thrown base 30 before typed influence and resistance; stock landed 46 at +54% influence; wider damage scaling unresolved')
    elif i == 504:
        parts.append('stored fixed ballistic base 22 with incomplete legacy endpoints; stock landed 23 after fixture-assisted Concealment; armor piercing unresolved')
    elif i == 694:
        parts.append('Static Field pulse: caster-centred 6 m hostile selector; each eligible recipient rolls the stored viral test before one-point damage and typed resistance')
    elif r['direct_damage'] and not r.get('health_callback') and i not in PETS:
        starter_base = {126: 'ballistic 11', 138: 'physical 36', 139: 'physical 12'}.get(i)
        if starter_base is not None:
            parts.append(f'stored fixed {starter_base} base on a landed interlock move with incomplete legacy endpoints; complete damage parity unresolved')
        else:
            parts.append('generic hit 50..80 / CSV value; scaling unresolved')
    if r['dot']:
        parts.append(('captured payload 264 / stored interval 2.5s' if i == 141 else
                      'captured' if not r.get('catalog_debuff') else 'stored total/interval') + ' DOT executor'
                     if r['timed'] and any(m['proc'] == 'Dot' for m in r['timed']) else 'DOT missing')
    if r.get('resurrection'):
        motion = 'Revive RSI 375' if i == 438 else str(i)
        parts.append('dead friendly player + native approval; ' + ('1 HP / 1 IS' if i == 325 else 'full HP / IS')
                     + f'; Action_Resurrect 63 with {motion} motion context')
    if r.get('registered_grants'):
        parts.append('stored zero-valued 620→1108 live grant registration/load after avatar readiness; stock Actions visibility pending'
                     if i == 620 else 'stored zero-valued 155→1113 live grant after avatar readiness; stock Actions visibility pending'
                     if i == 155 else 'stored zero-valued 483→1080 live grant registration/load after avatar readiness; stock Actions visibility pending'
                     if i == 483 else 'captured zero-valued loaded grant membership after live-avatar readiness; idempotent removal/reload; [evidence](LOADED-GRANT-MEMBERSHIP.md)')
    if r.get('conditional_thrown_damage'):
        parts.append('Off Balance only: one stored on-use thrown initializer, value45/24 at50 before existing influence/resistance; base/whole native ordering unresolved; [evidence](ABILITY-CONDITIONAL-THROW.md)')
    if i == 120:
        parts.append('stored function-2 grant level feeds Disguise duration; native Actions registration pending')
    if i == 280:
        parts.append('stored sibling -100 callback protects first health change; later damage breaks the root and clears its parent')
    if r.get('conditional_powerless'):
        parts.append('landed initialization checks current Staggered once; stored6s Powerless through existing state policy, no new immunity timer; [evidence](ABILITY-CONDITIONAL-POWERLESS.md)')
    if r.get('conditional_stun'):
        parts.append('Dazed checked at .01s marker term; script stun/immunity ' + ('4s' if i == 347 else '8s') + '; preserves sibling effects; [source](CONDITIONAL-STUN.md)')
    if i == 429 and any(m['proc'] == 'Terminal' for m in r['timed'] or []):
        parts.append('10% landed proc expires after 0.1s; ApplyStunWithTimer gives 4s Stun and separate 4s immunity')
    elif any(m.get('StatModTermFunc') == 'ApplyStunWithTimer' for m in mods(i)) and any(m['proc'] == 'Terminal' for m in r['timed'] or []):
        marker = next(m for m in r['timed'] if m['proc'] == 'Terminal')
        seconds = f"{marker['seconds']:g}".lstrip('0')
        parts.append(f'landed proc {seconds}s marker calls ApplyStunWithTimer; 4s Stun and separate 4s immunity')
    if i in (268, 406):
        parts.append('caster-centred hostile area via shipped selector 4 at cast completion')
    if i in (38, 47, 97):
        parts.append('stock level-19 caster-centred radius gains 10% from active Pathogenist Upgrade 1085')
    if i in (126, 130, 176):
        parts.append('stock blocked sight denies the selected shot before cast, IS cost or reuse')
    if i in (126, 130):
        parts.append('stock timed shots fizzle if the original target leaves sight before landing')
    if i in (176, 254):
        parts.append('chance-root publishes movement scale -1 while held and restores the surviving speed effects on removal; stock-client movement observation remains open')
    if i in (86, 130):
        if r.get('movement_debuff_scale'):
            parts.append('signed stat 115 projects to stock run speed and the remaining scale returns at expiry')
        else:
            parts.append('PR775 stock run found base run speed unchanged with the three -50 movement records active')
    if i in (22, 27, 28):
        parts.append('stock summons keep the open street stand or use a verified stand on the same room floor')
    if i in (22, 27, 28, 29, 602):
        parts.append('requires loaded memory from admission through cast completion; an Archive fizzle keeps the prior summon')
    if i in (33, 36, 53, 57, 58, 59, 60, 514):
        parts.append('cast completion rechecks the original target spawn and range before damage or splash')
    if i in (21, 33, 36, 514):
        parts.append('checks loaded membership before charging and before applying the cast')
    if i in (53, 57, 58, 59, 60):
        parts.append('checks loaded membership before charging and applying the timed attack')
    if i in (40, 86):
        parts.append('cast completion requires the original hostile spawn in range before applying its timed effect')
    if i in (40, 66, 86):
        parts.append('checks loaded membership before charging and landing; landed effects and reuse survive unload')
    if i == 66:
        parts.append('requires a selected hostile within 16 m at admission and again at landing')
    if i in (40, 86, 130):
        parts.append('the active Force Combat penalty lowers the current engagement score until expiry')
    if i in (43, 48, 69):
        parts.append('cast completion rechecks the original hostile spawn and range before debuff or damage over time')
        parts.append('checks loaded membership at admission and preserves the duration of landed effects on unload')
    if r.get('sharpshooter'):
        parts.append(r['sharpshooter'])
    if r.get('health_callback'):
        parts.append(r['health_callback'])
    if r['healing'] and not r.get('health_callback'):
        if i in PET_REPAIRS:
            parts.append('heals own simulacrum: stored base; caster healing influence; player cast refused')
        elif not r.get('player_healing'):
            parts.append('pet/special healing target executor missing; player cast refused')
        else:
            amount = '10% target max HP' if i == 967 else 'stored base ' + '+'.join(str(v) if v is not None else '?' for v in r.get('heal_base', [])) + '; caster healing influence'
            heal = 'self-only heal' if i == 55 else 'target/team heal'
            parts.append(heal + (' over time' if any(m.get('StatModUpdateFunc') == 'HOT' for m in mods(i)) else '') + '; ' + amount)
    passive = [m for m in r.get('passives', []) if m['level50'] is not None]
    if passive:
        parts.append('loaded passive totals + records (@50: ' + ', '.join(f"{name(m['stat'])} {m['level50']:+d} [{m['consumer']}]" for m in passive) + ')')
    if i in PASSIVE_LIMITS:
        parts.append(PASSIVE_LIMITS[i])
    if r['buff_source'] != 'none':
        if r.get('tactical_toggle'):
            parts.append('script tactical toggle; precise/energized combat attack and damage multipliers; mutual refusal')
        elif i == 49:
            entry_consumer = any(m['stat'] == 520 and m['consumer'] != 'total only' for m in (r.get('buff_stats') or []))
            parts.append('Gaussian Blur kBlur0x40 self/watchers/late views; ' + ('OURS direct520+25 raises deterministic new PvP entry resistance only under current217; no retail contest/shield HP' if entry_consumer else 'stored Combat Evasion+25 has no engagement consumer'))
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
    if r.get('proxy_technician'):
        parts.append(r['proxy_technician'])
    if mobius := r.get('mobius'):
        parts.append(f"script {mobius['chance']}% / {mobius['seconds']:g}s: incoming weapon free fire and new forced interlock gates; branch healing FX; susceptibility and positive native proof unresolved; [evidence](ABILITY-MOBIUS-FREE-ATTACK.md)")
    if r.get('aura') and not r.get('proxy_technician'):
        a = r['aura']
        recipients = 'friendly team players' if a.get('friendly') else 'hostile NPC/PvP targets'
        if i == 945:
            recipients = 'hostile PvP players'
        parts.append(f"toggled aura: IS drain {a['drain']}/{a['interval']:g}s, pulses {a['effect']} on {recipients} in its effect radius")
        if i in (42, 61, 89, 378, 380, 485):
            parts.append('source world/life/transport gates cancel old pulse work; active recipients retain their own expiry; [evidence](ABILITY-HACKER-CALLBACKS.md)')
        if i in (378, 380):
            parts.append('admitted recipient receives 0.2s Deflect FX callback and linked on/off record')
        if i in (378, 485):
            parts.append('captured parent modifier tuples and native 80 bb cancellation')
        if i == 378:
            parts.append('captured own 10s reuse starts on activation')
    if r.get('sweep'):
        parts.append('instant25% cleanse of debuff parents (UDB0, not exempt)' if i == 868 else 'pulse sweep removes cleansable debuff parents (UDB0, not exempt)')
    if r.get('buff_buffer'):
        parts.append('native script buffer reservation min(Value,free); baseline100+4level is OURS; refuses new upgrades until removal/expiry')
    if r.get('instant_hostile'):
        parts.append('Trojan Horse pulse: hostile players lose 20 current IS, clamped at zero' if i == 946
                     else 'instant script: removable buffs (UBF0) / current IS on landing')
    if r.get('buff_dispel'):
        parts.append('buff dispel on landing: ' + ', '.join(f"{m['callback']} {m['chance'] * 100:g}% proc" for m in r['buff_dispel']) + ' (existing server RNG policy; native sequence unverified)')
    if r['grants']:
        parts.append('load/unload/startup grants ' + ','.join(map(str, r['grants'])))
    style = [m for m in r.get('style', []) if m['level50'] is not None]
    if style:
        parts.append('active-style modifiers via 3e/80 b9 (@50: ' + ', '.join(f"{name(m['stat'])} {m['level50']:+d}" for m in style) + '); ' + ('stock weapon exchanges and opening transitions with a matching equipped gun' if r.get('weapon_style_exchange') else 'style interlock exchanges'))
    if r.get('special_move_style'):
        parts.append(f"special move: needs {name(r['special_move_style'])} (auto-switch when loaded)")
    if r.get('sneak_bonus'):
        parts.append(f"stealth-started opener +{r['sneak_bonus']} scripted bonus (trigger/addition policy; COMBAT-DEPTH.md)")
    if r.get('special_animation'):
        source = 'canonical ability symbol in stock ILDB rule' if r.get('special_animation_rule') else 'name mapping policy'
        parts.append(f"named ILDB special choreography {r['special_animation']} ({source})")
    if r.get('stealth_shield'):
        parts.append(r['stealth_shield'])
    if r.get('special'):
        parts.append(r['special'])
    if i == 11:
        parts.append('flag-1 Hyper-Jump takes 20 IS on accepted start; ordinary jumps and cancel preserve IS; stock rank 10/11 forward range, short aim and 31.76 m distance-timed aim landing verified; stock keeps 20 IS with Efficiency 117 +15; full-peak C4 returns from an estimated airborne origin with its original deadline and no additional debit')
    if not r.get('stealth') and ('Sneak' in abilities[i]['name'] or 'Conceal' in abilities[i]['name']):
        parts.append('stealth detection/AI incomplete')
    if i in PETS:
        parts.append(PETS[i])
    if i in (19, 25):
        parts.append('loaded membership required at admission and pet effect landing')
    if i == 173:
        parts.append('stock level-19 +24 on the chosen allies feeds melee, ballistic and thrown special attack inputs')
    if i in (46, 68, 173):
        parts.append('requires clear sight to the selected ally before charging and at cast completion')
        parts.append('requires loaded membership at admission and completion')
    if i == 113:
        parts.append('stock equal-level shield damage 35 to 36 when 1048 +3 is loaded')
    if i in (22, 27, 28):
        parts.append('stock Simulacra Attack and Stay verified for this summon')
    if i == 27:
        parts.append('stock Attack carries the selected view and spawn; Stay ends attacks')
    if i in (20, 30, 603):
        parts.append('requires continuous loaded membership through the pet support cast')
    if i in (20, 603):
        parts.append('unloading preserves the lifetime of the landed pet buff')
    if i == 504:
        parts.append('requires active Concealment; stock range and sight refusals retain it, and an admitted cast consumes it at start and hits for 23 after six seconds')
    if i in (503, 1137):
        parts.append('unloading the parent clears the active Concealment grant, with separate Sneak preserved')
    if i == 1137:
        parts.append('Archive unload cancels a pending cast and republishes the normal owner stealth-off state and stance')
    if i == 21:
        parts.append('requires a live enemy selection before charging the cast')
    return '; '.join(parts) or 'presentation only; no effect executor'


def status(i):
    if i in (7, 13, 871):
        return 'inaccessible'
    if i in DATA_MINING:
        return DATA_MINING[i][1]
    r = coverage[i]
    if i in PETS or r.get('proxy_technician') or r.get('sharpshooter') or r.get('multifighting'):
        return 'partial'
    # Conservative: metadata, a raw constant or a packet helper alone is not retail gameplay parity.
    if i == 49 and not any(m['stat'] == 520 and m['consumer'] != 'total only' for m in (r.get('buff_stats') or [])):
        return 'cosmetic'
    if (i == 1149 and not any(m['consumer'] != 'total only' for m in (r.get('buff_stats') or []))) or (r['buff_source'] in ('stored character level', 'stored learned level') and not any(m['consumer'] != 'total only' for m in r['buff_stats'])):
        return 'cosmetic'
    if r.get('stealth_shield') or r.get('health_callback') or r.get('stealth') or r.get('utility') or r.get('resurrection') or r['healing'] or r['direct_damage'] or r['buff_source'] != 'none' or r['timed'] or r['grants'] or r.get('aura') or r.get('sweep') or r.get('instant_hostile') or any(m['level50'] is not None and m['consumer'] != 'total only' for m in r.get('passives', [])) or r.get('style') or r.get('special') or 'HyperJump' in abilities[i]['name']:
        return 'partial'
    if fields(i).get('Ability/IsDiscipline') or any(m.get('WhenEnabled') == 0 for m in mods(i)):
        return 'cosmetic'
    return 'missing'


def remaining_items(i, current_status, runtime_summary):
    if current_status == 'inaccessible':
        if i == 13:
            return {'access': 'Recover any ordinary retail unlock independent of the level 99 selected-load gate'}
        return {'access': 'Resolve the stored character level 99 prerequisite against the retail level cap'}

    r, f = coverage[i], fields(i)
    modifiers = mods(i)
    targets = {goid(m.get('TargetAttribute', 0)) for m in modifiers} - {0}
    states = {goid(m.get('TargetState', 0)) for m in modifiers} - {0}
    hooks = callbacks(i)
    open_items = {}

    def labels(ids):
        return ', '.join(name(key) for key in sorted(ids))

    if current_status == 'missing':
        open_items['executor'] = ('Identify Disrupt Connection’s stock tool or caster and recover its effect'
                                  if i == 1124 else 'Recover and execute the intended native action')
    if r['direct_damage'] or r['dot']:
        open_items['damage'] = 'Verify sourced damage scaling and target resistance in combat'
    if r['healing']:
        open_items['healing'] = 'Verify the exact healing total for a known caster and target'
    periodic = sorted({m.get('StatModUpdateFunc') for m in modifiers
                       if str(m.get('StatModUpdateFunc')).endswith(('DOT', 'HOT'))})
    if periodic or r['dot']:
        open_items['periodic'] = f"Verify tick interval, count and total for {', '.join(periodic) or 'the stored DOT'}"
    if r.get('resurrection'):
        open_items['resurrection'] = 'Verify consent, restored vitals and this ability’s resurrection motion'
    terminal_stun = any(m.get('StatModTermFunc') == 'ApplyStunWithTimer' for m in modifiers) and any(
        m['proc'] == 'Terminal' for m in r['timed'] or [])
    if states or r.get('conditional_stun') or r.get('conditional_powerless') or terminal_stun:
        open_items['controls'] = ('Verify duration, breaks, immunity and presentation for ' +
                                  (labels(states) if states else 'Stun' if terminal_stun else 'the conditional control'))
    if f.get('Ability/RadiusOfEffect') or f.get('Ability/AngleOfEffect'):
        radius = f.get('Ability/RadiusOfEffect', 'native default')
        angle = f.get('Ability/AngleOfEffect', 'native default')
        open_items['area'] = f'Verify target shape and admission with stored radius {radius} and angle {angle}'
    if f.get('Ability/TestFunction') or f.get('Ability/HasDeflectionInterlockMove') or r['direct_damage']:
        open_items['targeting'] = 'Verify applicable line of sight, cast target, hit and deflection rules'
    # Health and the periodic payload carrier are executable effects, not stats with a separate base/link.
    stats = targets - {108, 1157}
    if stats:
        open_items['stats'] = 'Verify base, links or runtime consumption for ' + labels(stats)
    consumers = [m for key in ('passives', 'buff_stats', 'style') for m in (r.get(key) or [])]
    unused = {m['stat'] for m in consumers if m['consumer'] == 'total only'}
    if unused:
        open_items['consumer'] = 'Wire or verify the gameplay consumer for ' + labels(unused)
    if r.get('stealth') or targets.intersection({204, 209, 646}) or f.get('Ability/DetectionDifficultyChange'):
        open_items['stealth-detection'] = 'Verify the stored visibility or detection contest and reveal rules'
    if r.get('aura'):
        open_items['aura'] = 'Verify aura membership, pulse, drain and cancellation'
        if i in (42, 61, 89, 378, 380, 485):
            open_items['aura'] = 'Verify native toggle and pulse records, same-child overlap between different casters, and recipient expiry'
    if i == 967 or any(m.get('ProcTriggerChance') not in (None, 0, 1) for m in modifiers):
        open_items['proc'] = ('Verify the 1% chance of 10s immunity' if i == 967 else
                             'Verify stored proc probability, eligibility and resulting effect')
    if r.get('weapon_style_exchange'):
        open_items['choreography'] = 'Verify equipped-weapon exchanges and opening transitions in the stock client'
    elif r.get('special_animation_rule'):
        open_items['choreography'] = f"Verify native playback of ILDB move {r['special_animation']} and recover any other cast and outcome variants"
    elif r.get('special_move_style') or r.get('special_animation'):
        open_items['choreography'] = 'Verify style-specific special-move exchange and presentation'
    if i in PETS or r.get('proxy_technician'):
        open_items['pets'] = 'Verify the sourced owner, target, lifecycle or combat effect'
    if hooks:
        open_items['script'] = 'Verify callback coverage for ' + ', '.join(hooks)
    if 'OURS' in runtime_summary or 'unresolved' in runtime_summary:
        open_items['source-parity'] = 'Resolve the inferred or unresolved behavior identified in the server column'
    if i == 86:
        open_items['source-parity'] = 'Verify that the signed movement adjustment slows the stock recipient and restores its prior speed on expiry'
    if i == 485:
        open_items['source-parity'] = 'Reconcile the captured shortfall timer order and child damage values with the shared aura scheduler'
    if i == 693:
        open_items['source-parity'] = 'Reconcile the archived +37 Melee and +99 Viral Defense records and child recast presentation'
    if i == 696:
        open_items['source-parity'] = 'Reconcile the archived +37 Ballistic and +39 Melee Damage records with the equal caster-level snapshots'
    if i == 872:
        open_items['source-parity'] = 'Verify whether stock retail suppresses the zero-amount restore chat when IS is already capped'
    if not open_items and current_status == 'cosmetic':
        open_items['consumer'] = 'Identify the gameplay effect or establish the discipline is metadata only'
    open_items['behavior-tests'] = 'Account for every listed shipped behavior with executable coverage and a test'
    return open_items


seeds = {i for i in abilities if fields(i).get('Ability/AbilityClass', 0) == 0 and
         (fields(i).get('Ability/InventoryMemCost', 0) > 0 or fields(i).get('Ability/IsDiscipline')) and not abilities[i]['name'].startswith('NPC')}
player = set(seeds)
while True:
    grants = {goid(m.get('TargetAbilityGrant', 0)) for i in player for m in mods(i)} - {0}
    new = grants.intersection(abilities) - player
    if not new:
        break
    player.update(new)
evidence = Evidence.load(ROOT / 'research/abilities/evidence', player)
rows = []
for i in sorted(player):
    access = 'memory' if fields(i).get('Ability/InventoryMemCost', 0) > 0 else 'discipline' if i in seeds else 'grant'
    if any(i in coverage[p]['grants'] for p in player if p != i):
        access += ', grant'
    if i == 872:
        access = 'internal, level-99 parent only'
    current_status, runtime_summary = status(i), server(i)
    remaining = remaining_items(i, current_status, runtime_summary)
    final_status, remaining = evidence.evaluate(i, current_status, remaining)
    rows.append([str(i), name(i), access, role(i), tree(i), expected(i), runtime_summary, final_status,
                 '; '.join(f'{key}: {detail}' for key, detail in remaining.items())])
counts = collections.Counter(row[7] for row in rows)
progress = summarize(rows, baseline)
header = f'''# Player ability parity audit

This is a conservative audit of the shipped 7.6005 templates against the executable server on this branch. **No ability is declared retail-complete merely because it sends an animation or a modifier record.** Counts: {len(player)} player-loadable candidates and their granted abilities ({len(seeds)} memory/discipline candidates, {len(player - seeds)} additional grants); {', '.join(f'{v} {k}' for k, v in sorted(counts.items()))}.

## Inventory and evidence

`data/abilityIDs.csv` contains 9,972 data rows, including unnamed engine IDs and repeated aliases at +4096/+8192. Its named rows resolve to **1,404 canonical shipped templates**. `data/ability_catalog.json` retains those templates, their component names, stored attributes and original stored AbilityID (171 differ from the template index). It omits absent engine defaults rather than inventing zeroes. GOB SHA-256: `{catalog['gob_sha256']}`.

The main table includes every non-NPC class-0 template with a positive inventory memory cost or a discipline flag, plus the transitive closure of its grants. This intentionally retains legacy, GM and ambiguous discipline candidates; it does not claim the retail UI exposed all of them. Access is tagged below. The canonical appendix accounts for every remaining template, including NPC, item, state and stat records. CSV aliases normalize to the canonical ID for execution. Friendly names come from the community name table; GOIDs, not suggestive prerequisite component names, determine dependency links.

Tree is the stored discipline parent, or the nearest prerequisite discipline where none is stored. Archetype follows prerequisite GOIDs, with category fallback. A shared ability can be usable by several builds. Each modifier's raw stored numbers are **inputs to native value/duration functions**, not asserted final retail damage or healing. Actual captured values take precedence in the server.

Read alongside [`abilities.py`](../research/client-scripts/abilities.py), [`tempmods.py`](../research/client-scripts/tempmods.py), [`combat.py`](../research/client-scripts/combat.py), [`combat_calculations.py`](../research/client-scripts/combat_calculations.py) and the linked `ability/` callbacks. `augments.py` and `ability/soldier/old_sm_*` are legacy code not imported by `abilities.GetImportObjects`; they are not silently treated as the active rules. `discovery`, `abilitylib`, `combatlib`, constants and console-variable defaults remain native dependencies. Callback links identify the decompiled definitions, not proof that the server implements them.

## Status meanings and shared gaps

- **done**: every identified behavior executes and has test evidence, the family has a native-verified representative, and the remaining column is empty.
- **partial**: some real effects execute; a listed behavior, scaling, target rule, condition or native presentation remains incomplete.
- **missing**: intended effect lacks an executor; an animation, cost or generic damage fallback does not count.
- **inaccessible**: primary client load prerequisite exceeds the retail character cap; no effect parity is claimed.
- **cosmetic**: discipline/passive loads or displays, but its native passive/stat behavior is not fully applied.

The new catalog fallback supplies explicit costs, cast times, recast/family timers and timed controls, preserving retail overrides. Insufficient IS and incapacitated/powerless casts are rejected. Root blocks movement and jumps; stun/mez blocks actions; powerless blocks abilities; disarm blocks weapon fire; enrage blocks withdrawal. Pacify and scripted roots break as their health callbacks specify. A negative chance-root proc protects its first health change (including healing), then flips positive for later damage checks, as in the script. A control state cannot stack through its immunity timer.

Hacker area damage and controls reach visible NPCs on the same floor. DOTs on secondary targets keep ticking without replacing the current fight; kills share death claiming, XP, mission and loot handling. The merged PvP paths also use action gates, combat-stat bonuses and health-triggered control breaks. LOS, full hit/deflection tests, cone shapes, multi-NPC combat AI, resistance-derived ability damage and some PvP target rules remain incomplete.

Loaded passives now evaluate the deterministic native value functions: learned ability level (2), character level (3), and fixed ranges. Scale uses the stored binary32 value, x87 multiplication/truncation, min offset and positive max cap. Owned loadouts, upgrades, new-world entry and Death Effect updates rebuild totals and retail-style `80 bc 45 00` records; obsolete unconditional Hyper-Jump/Write Code records are removed. Shared grants contribute once. Vitals, regeneration, combat accuracy/defence/resistance bonuses, damage influence, control durations and crafting use these totals. Control duration bonuses follow the stored class-4 state links (stun, root, pacify, blind, powerless, confuse) and keep fractional seconds. Immunity timers and break rules retain their separate definitions. Percent bonuses apply to the server's existing base formulas; those bases, character-attribute links, several stat families, complete stealth/detection attributes, complete target Determination/attribute bases and special callbacks are still incomplete. UDB0 on-use debuffs now use target stat575 for script duration mitigation; self effects and immunity timers are excluded (COMBAT-DEPTH.md). Stealth state/visibility/detection now has an executor; see [STEALTH.md](STEALTH.md) for sourced behavior and choices. A passive record is not proof that every consuming system is implemented. See [native modifier evidence and remaining limits](ABILITY-MODIFIERS.md).

Friendly direct/group heals and HOTs use each shipped template's deterministic base and the caster's healing influence, replacing the target-quarter-max-health placeholder. HOT uses its stored total and the script tick formula. Simulacra repairs heal only the caster's own simulacrum; player targets are refused. Mobius Code retains its explicit 10% target-max heal; its 1% immunity proc is still missing. These are sourced base values, with native influence multiplication/truncation; exact final retail amounts still require native comparison with a known caster build and complete attribute contributions. Fixed stored self/friendly buffs gain records/family expiry; captured values win. Deterministic numeric timed buffs using character-level function3 resolve from the caster at application, with active snapshots shared by stat/vital consumers and exact removal records. Script/aura/state/random effects and indefinite style scaling stay excluded; [scaled-buff evidence](ABILITY-SCALED-BUFFS.md) lists native comparison points. Restore RSI325, Revitalize326, Revive375 and Rejuvenate416 now use dead-friendly targets, the native81 88 consent exchange and their script-defined resource restoration. Refusal, timeout, wrong-ability replies, replies without a live offer and reconstruction are rejected; the native short reply carries no per-offer nonce. Exact script-specific resurrection motion and native dialog acceptance remain unverified; see [resurrection evidence](ABILITY-RESURRECTION.md). Coder simulacra (Remote Proxy, Logic/Patch Daemon) spawn as owned NPC_BASE_SIMULACRA views with follow/assist AI, the client's orders, death and lifetime, plus the own-simulacrum abilities and Daemon Boost; see [CODER-PETS.md](CODER-PETS.md) for what is retail evidence and what is chosen.

Combat styles (Self Defense, Aikido, Karate, Kung-Fu, Hacker/Coder/Spy Style) become active through `3e`. The active style's while-enabled modifiers join the passive totals, and its ILDB exchange set drives the interlock moves all viewers see. `CombatSpecialMove` abilities need their stored stance: a loaded style is switched to, an unloaded one refuses the move. Thrown DOTs, Berserker/Side Roll caster buffs, Taunt aggro and the Exile weapons have executors. Their inferences and gaps (RandomCombatStateChance, weakness by NPC name, unmapped special choreography) are in [COMBAT-STYLES.md](COMBAT-STYLES.md). `43` MartialArt is unreachable in 7.6005.

Concentration564 now consumes loaded modifiers for damage cast-through on explicit stored interruptible templates only; native integer-division/typed-argument evidence, absent-flag boundary and server input policy are in [ABILITY-CONCENTRATION.md](ABILITY-CONCENTRATION.md).

`AbilityEffects`, `Virii`, `TempMods` and other modules contain script-derived pure helpers for corrosion, disables and immunity, but helpers without live handler wiring remain missing/partial in this audit.

## Remaining work and status delta

```text
{progress}
```

## Per-ability table

Access `memory` means the stored memory cost is positive; `discipline` means its discipline flag is set; `grant` means reachable through a player candidate. A grant can also carry memory metadata. Values are raw unless explicitly described as captured.

| ID / ability | Access | Archetype / tree | Shipped behavior / callbacks | Server behavior | Status | Remaining |
|---|---|---|---|---|---|---|
'''
table = '\n'.join('| ' + ' | '.join([f'{r[0]} {r[1]}', r[2], f'{r[3]} / {r[4]}', r[5], r[6], r[7], r[8]]) + ' |' for r in rows)
appendix = '''\n\n## Canonical inventory outside the player table

These records are not silently dropped. Scope is the stored class or an explicit NPC name; other class-0 records require evidence that they are loadable rather than item/event/internal actions. Their full decoded fields remain in the JSON catalog.

| ID | Template | Scope / exclusion |
|---:|---|---|
'''
classes = {1: 'core attribute', 2: 'engine stat', 4: 'engine state', 5: 'personality archetype'}
for i, ability in abilities.items():
    if i not in player:
        scope = 'NPC' if ability['name'].startswith('NPC') else classes.get(fields(i).get('Ability/AbilityClass'), 'item / event / internal / unproven loadability')
        if coverage[i].get('consumable_sweep'):
            scope = 'item-triggered ability; cast/stack/shared timer/level checks; ' + ('initial + periodic' if i != 659 else 'periodic') + ' negative-condition sweep; [source and native checklist](ABILITY-CONSUMABLE-SWEEPS.md)'
        if coverage[i].get('mobius'):
            scope += '; ' + server(i)
        appendix += f"| {i} | {ability['name']} | {scope} |\n"
footer = '''
## Reproduction and checks

1. `python research/abilities/extract_catalog.py --client <read-only MxO folder>`
2. `dotnet run --project tools/AbilityAudit`
3. `python research/abilities/build_audit.py` (current Markdown in `artifacts/ability-audit`; see [audit policy](ABILITY-AUDIT.md))

The runtime inventory calls the real `AbilityCosts`, `AbilityCooldowns`, `AbilityBuffs`, `TempMods`, `AbilityTargets` and grant lookup, rather than searching comments. The [runtime inventory](../research/abilities/server_coverage.json) records the executable coverage. The generated [CSV](ABILITY-PARITY.csv) is filterable by archetype, tree and status. Ability changes have unit coverage in `AbilityParityTests.cs` and four fresh-server `ability-parity-*` scenarios for support, controls, area effects and IS denial. Native ability checks are in [ABILITY-PARITY-QA.md](ABILITY-PARITY-QA.md) and the handoff file; this ability task does not acquire the client lock. The earlier PR94 native validation is separately recorded in `PVP.md`.
'''
markdown_path=ROOT / 'docs/ABILITY-PARITY.md' if args.update_markdown_snapshot else artifact_dir / 'ABILITY-PARITY.md'
markdown=rebase_links(header + table + appendix + footer,ROOT,markdown_path.parent,
    args.source_base_url,bundled=not args.update_markdown_snapshot)
markdown_path.write_text(markdown, encoding='utf-8', newline='\r\n')
csv_path=artifact_dir / 'ABILITY-PARITY.csv' if args.output_dir else ROOT / 'docs/ABILITY-PARITY.csv'
with csv_path.open('w', newline='', encoding='utf-8') as stream:
    writer = csv.writer(stream, lineterminator='\r\n')
    writer.writerow(['id', 'ability', 'access', 'archetype', 'tree', 'shipped_behavior', 'server_behavior', 'status', 'remaining'])
    writer.writerows(rows)
if not args.update_markdown_snapshot:
    if csv_path.resolve() != (artifact_dir / 'ABILITY-PARITY.csv').resolve():
        (artifact_dir / 'ABILITY-PARITY.csv').write_text(csv_path.read_text(encoding='utf-8'),encoding='utf-8',newline='\r\n')
    (artifact_dir / 'server_coverage.json').write_text(coverage_path.read_text(encoding='utf-8'),encoding='utf-8',newline='\r\n')
print(f'{len(player)} player candidates/grants, {len(abilities) - len(player)} other templates')
print(progress)
