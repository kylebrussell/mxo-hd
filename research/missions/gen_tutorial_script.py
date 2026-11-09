"""
Turns the client's decompiled tutorial script (Mission986-decompiled.py) into C#, so its timers, lines and goal order
stay exactly as retail wrote them. The engine it calls is hds/world/Structures/TutorialMission.cs.

    python gen_tutorial_script.py <all_string_tables.tsv> ../../hds/world/Structures/TutorialScript.g.cs

The string table is extracted from the client; it isn't in the repo, but the generated file is.
"""
import csv
import json
import re
import sys
from pathlib import Path

here = Path(__file__).parent
source = (here / 'Mission986-decompiled.py').read_text(encoding='utf-8').splitlines()
strings = {}
with open(sys.argv[1], encoding='utf-8', newline='') as f:
    for row in csv.reader(f, delimiter='\t', quoting=csv.QUOTE_NONE):
        if len(row) >= 3 and row[0] == 'stringtable_client':
            strings[row[2]] = int(row[1], 16)

panels = {k: int(v, 16) for k, v in json.loads((here / 'tutorial-goals.json').read_text(encoding='utf-8'))['meta']['panels'].items()} \
    if 'meta' in json.loads((here / 'tutorial-goals.json').read_text(encoding='utf-8')) else None
if panels is None:
    data = json.loads((here / 'tutorial-goals.json').read_text(encoding='utf-8'))
    for value in data.values():
        if isinstance(value, dict) and 'panels' in value:
            panels = {k: int(v, 16) for k, v in value['panels'].items()}

goal_texts = {}
with open(here / 'mission986_goals.tsv', encoding='utf-8', newline='') as f:
    for row in csv.DictReader(f, delimiter='\t', quoting=csv.QUOTE_NONE):
        goal_texts[int(row['goal'])] = row['captured_goal_text']

FX = {
    'FX_OBJECT_INTERFACE_FADE_TO_BLACK': '0x28000792u',
    'FX_OBJECT_INTERFACE_WHITE_TRANSITION_IN': '0x2200001du',
    'FX_OBJECT_INTERFACE_WHITE_TRANSITION_OUT': '0x2200001eu',
}

METHODS = {'OnGoalComplete': ('OnGoalComplete', 'long goalnumber'),
           'OnToggledViewActivated': ('OnToggledViewActivated', 'long viewid'),
           'OnModalDialogueAccepted': ('OnModalDialogueAccepted', 'long messageid, bool leftchoice'),
           'OnMissionEvent': ('OnMissionEvent', 'long eventid'),
           'OnStartOfTutorialMission': ('OnStartOfTutorialMission', '')}
CALLS = {'playNarration': 'PlayNarration', 'broadcastMessage': 'BroadcastMessage', 'scheduleEvent': 'ScheduleEvent',
         'sendModalMessage': 'SendModalMessage', 'completeGoal': 'CompleteGoal', 'isGoalComplete': 'IsGoalComplete',
         'toggleView': 'ToggleView', 'sendMissionEvent': 'SendMissionEvent', 'playLocalFX': 'PlayLocalFX',
         'teleportToMissionElement': 'TeleportToMissionElement', 'bootPlayer': 'BootPlayer', 'respawnNPC': 'RespawnNPC',
         'unkillNPC': 'UnkillNPC', 'grantExperience': 'GrantExperience'}


def expr(text):
    text = re.sub(r'StringTable\.(\w+)', lambda m: hex(strings[m.group(1)]) + 'u', text)
    text = re.sub(r'constants\.PANEL\.(\w+)', lambda m: hex(panels[m.group(1)]), text)
    for name, value in FX.items():
        text = text.replace(name, value)
    text = re.sub(r'mission\.(\w+)\((?:teamleader|sender)(?:, )?', lambda m: CALLS[m.group(1)] + '(', text)
    text = re.sub(r'\bnot\s+', '!', text).replace(' and ', ' && ').replace(' or ', ' || ')
    text = text.replace("'", '"').replace('True', 'true').replace('False', 'false')
    return text


out = []
i = 0
while i < len(source):
    m = re.match(r'    def (\w+)\(self(?:, ([^)]*))?\):', source[i])
    if not m or m.group(1) not in METHODS:
        i += 1
        continue
    name, params = METHODS[m.group(1)]
    out.append(f'        private void {name}({params})')
    out.append('        {')
    i += 1
    stack = [8]
    while i < len(source):
        line = source[i]
        if line.strip() == '':
            i += 1
            continue
        indent = len(line) - len(line.lstrip())
        if indent <= 4:
            break
        text = line.strip()
        while indent < stack[-1]:
            stack.pop()
            out.append(' ' * (stack[-1] + 0) + '}')
        i += 1
        # The script's developer warp and its team/phase plumbing don't apply here
        if text.startswith('if phasenumber') or text.startswith('if self.MissionWarping'):
            i += 1
            continue
        if text.startswith('teamleader ='):
            continue
        if 'consolevar' in text:
            base = indent
            while i < len(source) and (source[i].strip() == '' or len(source[i]) - len(source[i].lstrip()) > base):
                i += 1
            continue
        if text == 'pass':
            out.append(' ' * indent + ';')
            continue
        branch = re.match(r'(if|elif) (.*):$', text)
        if branch:
            keyword = 'if' if branch.group(1) == 'if' else 'else if'
            out.append(' ' * indent + f'{keyword} ({expr(branch.group(2))})')
            out.append(' ' * indent + '{')
            stack.append(indent + 4)
            continue
        if text == 'else:':
            out.append(' ' * indent + 'else')
            out.append(' ' * indent + '{')
            stack.append(indent + 4)
            continue
        if text == 'return':
            out.append(' ' * indent + 'return;')
            continue
        out.append(' ' * indent + expr(text) + ';')
    while len(stack) > 1:
        stack.pop()
        out.append(' ' * stack[-1] + '}')
    out.append('        }')
    out.append('')

constants = []
seen = set()
for line in source:
    m = re.match(r'^([A-Z][A-Z0-9_]*) = (.+)$', line)
    if not m or m.group(1) in seen:
        continue
    seen.add(m.group(1))
    value = m.group(2).rstrip('L')
    if value.startswith("'"):
        continue
    kind = 'long'
    constants.append(f'        private const {kind} {m.group(1)} = {value};')

texts = ',\n'.join(f'            "{goal_texts[g]}"' for g in sorted(goal_texts))
result = f"""// Generated by research/missions/gen_tutorial_script.py from the client's decompiled Mission986 script. Don't edit by hand.
namespace hds
{{
    public partial class TutorialMission
    {{
        /// <summary>Goal text as retail sent it (80 a0), by goal id.</summary>
        public static readonly string[] GoalTexts =
        {{
{texts}
        }};

""" + '\n'.join(constants) + '\n\n' + '\n'.join(out).rstrip() + """
    }
}
"""
Path(sys.argv[2]).write_text(result.replace('\n', '\r\n'), encoding='utf-8', newline='')
print('generated', len(out), 'lines,', len(constants), 'constants')
