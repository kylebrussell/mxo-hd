#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_path=""
json_url=""
html_url=""
out_path=""
max_leads=120

usage() {
  cat <<'EOF'
Usage: scripts/mine-discord-resource-leads.sh --source PATH [options]

Mines a DiscordChatExporter JSON archive for packet-format research leads.

Options:
  --source PATH    Discord JSON export to inspect.
  --json-url URL   Optional public JSON export URL used for provenance.
  --html-url URL   Optional public HTML export URL used to build message anchors.
  --out FILE       Markdown report path. Defaults to stdout.
  --max-leads N    Maximum candidate lead rows. Defaults to 120.
  -h, --help       Show this help text.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      source_path="${2:-}"
      shift 2
      ;;
    --json-url)
      json_url="${2:-}"
      shift 2
      ;;
    --html-url)
      html_url="${2:-}"
      shift 2
      ;;
    --out)
      out_path="${2:-}"
      shift 2
      ;;
    --max-leads)
      max_leads="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      if [[ -z "$source_path" ]]; then
        source_path="$1"
        shift
      else
        echo "Unknown argument: $1" >&2
        usage >&2
        exit 1
      fi
      ;;
  esac
done

if [[ -z "$source_path" ]]; then
  echo "--source is required" >&2
  usage >&2
  exit 1
fi

if [[ ! -f "$source_path" ]]; then
  echo "Source JSON not found: $source_path" >&2
  exit 1
fi

if ! [[ "$max_leads" =~ ^[0-9]+$ ]]; then
  echo "--max-leads must be a positive integer" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "jq is required to mine Discord resource leads" >&2
  exit 1
fi

if ! command -v shasum >/dev/null 2>&1; then
  echo "shasum is required to hash Discord resource lead sources" >&2
  exit 1
fi

tmp_dir="$(mktemp -d "${TMPDIR:-/tmp}/mxo-discord-leads.XXXXXX")"
trap 'rm -rf "$tmp_dir"' EXIT

sha256_file() {
  shasum -a 256 "$1" | awk '{print $1}'
}

byte_count() {
  wc -c < "$1" | tr -d ' '
}

relative_path() {
  local path="$1"
  case "$path" in
    "$repo_root"/*) printf '%s' "${path#"$repo_root/"}" ;;
    *) printf '%s' "$path" ;;
  esac
}

escape_md() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//|/\\|}"
  value="${value//$'\n'/ }"
  value="${value//$'\r'/ }"
  printf '%s' "$value"
}

message_link() {
  local id="$1"
  if [[ -n "$html_url" ]]; then
    printf '[%s](%s#chatlog__message-container-%s)' "$id" "$html_url" "$id"
  else
    printf '`%s`' "$id"
  fi
}

guild="$(jq -r '.guild.name // .guild // "unknown"' "$source_path")"
channel_name="$(jq -r '.channel.name // .channel // "unknown"' "$source_path")"
channel_id="$(jq -r '.channel.id // "unknown"' "$source_path")"
exported_at="$(jq -r '.exportedAt // "unknown"' "$source_path")"
date_range="$(jq -r 'if .dateRange then ((.dateRange.after // "unknown") + " to " + (.dateRange.before // "unknown")) else "unknown" end' "$source_path")"
message_count="$(jq -r '.messageCount // (.messages | length)' "$source_path")"
source_sha="$(sha256_file "$source_path")"
source_bytes="$(byte_count "$source_path")"
source_display="$(relative_path "$source_path")"

term_counts="$tmp_dir/term-counts.tsv"
jq -r '
  def terms: [
    ["packet log", "(^|\\W)packet logs?(\\W|$)"],
    ["packet", "(^|\\W)packets?(\\W|$)"],
    ["captured packet", "captured packets?"],
    ["packet capture", "packet capture"],
    ["sniffer", "sniffer"],
    ["combat", "combat"],
    ["Hardline Dreams", "hardline dreams"],
    ["Morpheus", "morpheus"],
    ["MxOSource", "mxosource"],
    ["reality.exe", "reality\\.exe"],
    ["source code", "source code"],
    ["mission logs", "mission logs?"],
    ["torrent", "torrent"],
    ["vendor_items", "vendor_items"],
    ["collector", "collector"],
    ["8113", "(^|\\W)8113(\\W|$)"],
    ["8114", "(^|\\W)8114(\\W|$)"],
    ["8115", "(^|\\W)8115(\\W|$)"],
    ["code storage", "code storage"],
    ["inventory", "inventory"]
  ];
  terms[] as $term
  | [
      $term[0],
      ([.messages[] | select((.content // "") | test($term[1]; "i"))] | length)
    ]
  | @tsv
' "$source_path" > "$term_counts"

candidate_rows="$tmp_dir/candidate-rows.tsv"
jq -r --argjson max "$max_leads" --arg html "$html_url" '
  def lead_terms: [
    {label:"packet log", re:"(^|\\W)packet logs?(\\W|$)"},
    {label:"captured packet", re:"captured packets?"},
    {label:"packet capture", re:"packet capture"},
    {label:"sniffer", re:"sniffer"},
    {label:"combat packet", re:"combat.{0,80}packet|packet.{0,80}combat"},
    {label:"Hardline Dreams", re:"hardline dreams"},
    {label:"Morpheus", re:"morpheus"},
    {label:"MxOSource", re:"mxosource"},
    {label:"reality.exe", re:"reality\\.exe"},
    {label:"source code", re:"source code"},
    {label:"mission logs", re:"mission logs?"},
    {label:"torrent", re:"torrent"},
    {label:"vendor_items", re:"vendor_items"},
    {label:"collector", re:"collector"},
    {label:"8113", re:"(^|\\W)8113(\\W|$)"},
    {label:"8114", re:"(^|\\W)8114(\\W|$)"},
    {label:"8115", re:"(^|\\W)8115(\\W|$)"},
    {label:"code storage", re:"code storage"},
    {label:"inventory", re:"inventory"}
  ];
  def matched_terms($text):
    [lead_terms[] as $term | select($text | test($term.re; "i")) | $term.label] | unique;
  def value_summary($text):
    if ($text | test("8113|8114|8115|collector"; "i")) then
      "Collector/code-storage RPC capture target"
    elif ($text | test("vendor_items"; "i")) then
      "Vendor inventory provenance and coverage warning"
    elif ($text | test("code storage|inventory"; "i")) then
      "Inventory or code-storage missing-capture target"
    elif ($text | test("combat"; "i")) then
      "Combat packet-log acquisition lead"
    elif ($text | test("morpheus|mxosource|reality\\.exe|mission logs?|torrent|source code"; "i")) then
      "Replay/source/archive acquisition lead"
    else
      "Packet-log holder or provenance lead"
    end;
  def relevance($text):
    [
      (if ($text | test("8113|8114|8115"; "i")) then 100 else 0 end),
      (if ($text | test("vendor_items"; "i")) then 95 else 0 end),
      (if ($text | test("(^|\\W)packet logs?(\\W|$)"; "i")) then 90 else 0 end),
      (if ($text | test("code storage"; "i")) then 88 else 0 end),
      (if ($text | test("captured packets?|packet capture|sniffer"; "i")) then 86 else 0 end),
      (if ($text | test("combat.{0,80}packet|packet.{0,80}combat"; "i")) then 84 else 0 end),
      (if ($text | test("mission logs?|torrent|reality\\.exe|mxosource"; "i")) then 70 else 0 end),
      (if ($text | test("source code"; "i")) then 40 else 0 end),
      (if ($text | test("morpheus|hardline dreams"; "i")) then 30 else 0 end),
      (if ($text | test("inventory|collector"; "i")) then 20 else 0 end)
    ] | max;
  def md:
    tostring
    | gsub("\\\\"; "\\\\\\\\")
    | gsub("\\|"; "\\\\|")
    | gsub("[\\r\\n]+"; " ");
  [
    .messages[]
    | . as $message
    | ($message.content // "") as $content
    | (matched_terms($content)) as $matched
    | select($matched | length > 0)
    | {
        id: ($message.id // "unknown"),
        timestamp: ($message.timestamp // "unknown"),
        author: ($message.author.name // $message.author.nickname // $message.author.id // "unknown"),
        matched: ($matched | join(", ")),
        value: value_summary($content),
        score: relevance($content)
      }
  ]
  | sort_by(-.score, .timestamp, .id)
  | .[:$max]
  | .[]
  | [.id, .timestamp, (.author | md), (.matched | md), (.value | md)]
  | @tsv
' "$source_path" > "$candidate_rows"

curated_ids="$tmp_dir/curated-ids.tsv"
cat > "$curated_ids" <<'EOF'
605338229201305601	Packet logs were described as necessary for making protocol assumptions.	Packet-log corpus provenance	Keep searching for raw logs before inferring missing packet semantics from client code alone.
605338299527069697	Packet logs were described as necessary for server-only data such as mission scripts.	Server-only data acquisition lead	Ask old log holders whether mission/script captures survived.
615313596406169619	Collector RPC packets 8113, 8114, and 8115 were explicitly requested.	Collector RPC target	Ask for collector open/exchange captures and search archives for 8113, 8114, and 8115.
618981033995665418	A code-storage-to-inventory action was called out as lacking packet logs.	Code storage missing-capture target	Capture transfer from code storage back to inventory with before/after inventory state.
623784469685600257	vendor_items.csv was described as parsed from logs and incomplete because only opened vendors could be parsed.	Vendor provenance and incompleteness	Do not treat vendor_items.csv as complete; prioritize vendor-open captures.
1124158409806188655	Packet logs from the final official-server period were described as rare but foundational.	Old live-log provenance	Ask whether any final-month logger output or derived parser artifacts still exist.
1153447633780736063	Gang data was described as limited to gangs encountered in packet logs.	Packet-log coverage limitation	Treat packet-log-derived tables as observed coverage, not complete game data.
1153769933629562950	Packet logs were described as useful for NPC creation data and world-change detection.	NPC creation packet-log lead	Prioritize Object599/NPC_BASE spawn captures and world-change boundaries.
1165107274931511437	A community member offered to run a packet sniffer for a specific action.	Current capture volunteer lead	Use structured capture requests for targeted one-action sessions.
1349909406602494032	Packet logs were described as historically important but incomplete because the client only sees relevant world state.	Coverage limitation	Design capture asks around exact before/after actions and nearby objects.
1377338108479144016	Remaining useful data was described as gameobject behavior and custom GO update tables rather than captures alone.	Semantic-reference lead	Pair packet logs with object-update table/source references.
1377793849321062480	A community member described experimenting with packet-log extraction scripts.	Current packet-log processing lead	Ask for extracted JSON/text plus raw source logs for validation.
1378453713752162404	A maintainer directed someone to search Neo packet logs for useful evidence.	Neo packet-log archive lead	Ask for Neo packet-log search results or raw archive access.
1378547672394498119	Combat-related packet-log material was reported as found.	Combat capture lead	Request raw combat packet logs with ability, FX, equipment, and object-state context.
1382239816430911488	Packet logs were suggested as evidence for combat click timing.	Combat timing lead	Capture click/hit timing with nearby object-state updates.
448200240282206218	A torrent containing many mission logs was mentioned.	Mission-log archive lead	Ask whether the torrent or extracted mission logs survived.
416622180110172180	MxOSource combat was described as replaying captured packets and being unstable.	Captured-packet replay lead	Search for replay inputs, packet text, or notes behind that implementation.
421058521191153664	reality.exe server compilation was suggested.	Replay tooling lead	Track reality.exe and old Python replay-server artifacts as possible packet-corpus routes.
868252093260115968	Old packet sniffer code was mentioned.	Sniffer tooling lead	Ask whether the old sniffer source or output format survived.
880023644829544499	Live-server packet logs were tied to Rajkosto's sniffer.	Sniffer corpus provenance	Cross-reference the public mxopackets1 and mxopackets2 archives before asking for duplicate copies.
1009119905561452674	A community member said packet logs were already public on GitHub.	Public GitHub packet-corpus lead	Track Vibrantic/mxo-resurgence mxopackets1 and mxopackets2 as acquired public corpus sources.
1092493692788543679	NPC packet-log evidence was described as observed spawn packets with no ability loadout data.	NPC spawn coverage limitation	Use Object599 spawn packets for observed state only; keep full NPC templates tied to server-resource evidence.
1092493912498774016	Population work was described as statistical inference over spawn packets.	NPC archetype inference warning	Keep packet-derived NPC archetypes marked inferred until source or resource evidence confirms them.
1135346643449942096	A community member said last-day personal packet logs still existed.	Last-day packet-log holder lead	Ask for raw final-day logs with route/action notes and source hashes.
1155361001143803965	Available packet logs were described as concentrated near the final two weeks before shutdown.	Time-window coverage limitation	Label old-live packet-derived findings as final-shutdown-window evidence.
1338093915689062411	Combat packets were described as decoded by emulator teams, while implementation remained large server work.	Combat packet-format lead	Ask for raw combat packet logs and decoded notes, then compare with local ability/effect/object-state parser evidence.
1338889818494472213	Full server-side resources were described as stronger evidence than packet logs for combat, mission scripts, loot, and NPC templates.	Server-resource acquisition lead	Prioritize server-resource archives when packet logs cannot expose server-only templates.
1339313432989536286	NPC ability loadouts were described as not distributed in packet logs.	NPC ability-loadout limitation	Treat missing NPC ability loadouts as a server-resource gap, not only a decoder gap.
1414377037329727591	Live tutorial packet logs were described as parsed into a large internal document.	Tutorial packet-log parser lead	Ask for the parser, generated document, and raw tutorial logs.
EOF

curated_rows="$tmp_dir/curated-rows.tsv"
curated_message_rows="$tmp_dir/curated-message-rows.tsv"
: > "$curated_rows"
curated_id_csv="$(cut -f 1 "$curated_ids" | paste -sd, -)"
jq -r --arg ids "$curated_id_csv" '
  ($ids | split(",")) as $ids
  | .messages[]
  | (.id // "") as $id
  | select($ids | index($id))
  | [
      $id,
      (.timestamp // "unknown"),
      (.author.name // .author.nickname // .author.id // "unknown")
    ]
  | @tsv
' "$source_path" > "$curated_message_rows"

while IFS=$'\t' read -r id finding value next_action; do
  metadata="$(awk -F '\t' -v id="$id" '$1 == id { print $2 "\t" $3; exit }' "$curated_message_rows")"

  if [[ -n "$metadata" ]]; then
    timestamp="${metadata%%$'\t'*}"
    author="${metadata#*$'\t'}"
    printf '%s\t%s\t%s\t%s\t%s\t%s\n' \
      "$id" \
      "$timestamp" \
      "$author" \
      "$finding" \
      "$value" \
      "$next_action" >> "$curated_rows"
  fi
done < "$curated_ids"

report_path="$tmp_dir/report.md"
{
  printf '# Discord Packet Resource Leads\n\n'
  printf 'Generated by `scripts/mine-discord-resource-leads.sh` from a DiscordChatExporter JSON archive. The candidate tables intentionally omit message excerpts; follow the message links to inspect source context.\n\n'
  printf '## Source\n\n'
  printf '| Field | Value |\n'
  printf '| --- | --- |\n'
  if [[ -n "$json_url" ]]; then
    printf '| Public JSON | <%s> |\n' "$json_url"
  else
    printf '| Source JSON | `%s` |\n' "$(escape_md "$source_display")"
  fi
  printf '| SHA-256 | `%s` |\n' "$source_sha"
  printf '| Bytes | %s |\n' "$source_bytes"
  printf '| Guild | `%s` |\n' "$(escape_md "$guild")"
  printf '| Channel | `%s` (`%s`) |\n' "$(escape_md "$channel_name")" "$(escape_md "$channel_id")"
  printf '| Exported at | `%s` |\n' "$(escape_md "$exported_at")"
  printf '| Date range | `%s` |\n' "$(escape_md "$date_range")"
  printf '| Message count | %s |\n' "$message_count"
  if [[ -n "$html_url" ]]; then
    printf '| Public HTML | <%s> |\n' "$html_url"
  fi
  printf '\n'

  printf '## Term Counts\n\n'
  printf '| Term | Matching messages |\n'
  printf '| --- | ---: |\n'
  while IFS=$'\t' read -r term count; do
    printf '| `%s` | %s |\n' "$(escape_md "$term")" "$count"
  done < "$term_counts"
  printf '\n'

  if [[ -s "$curated_rows" ]]; then
    printf '## Curated Packet-Resource Probes\n\n'
    printf '| Message | Timestamp | Author | Packet-resource finding | Value | Next action |\n'
    printf '| --- | --- | --- | --- | --- | --- |\n'
    while IFS=$'\t' read -r id timestamp author finding value next_action; do
      printf '| %s | `%s` | `%s` | %s | %s | %s |\n' \
        "$(message_link "$id")" \
        "$(escape_md "$timestamp")" \
        "$(escape_md "$author")" \
        "$(escape_md "$finding")" \
        "$(escape_md "$value")" \
        "$(escape_md "$next_action")"
    done < "$curated_rows"
    printf '\n'
  fi

  printf '## Candidate Lead Messages\n\n'
  printf '| Message | Timestamp | Author | Matched terms | Packet-resource value |\n'
  printf '| --- | --- | --- | --- | --- |\n'
  while IFS=$'\t' read -r id timestamp author matched value; do
    printf '| %s | `%s` | `%s` | `%s` | %s |\n' \
      "$(message_link "$id")" \
      "$(escape_md "$timestamp")" \
      "$(escape_md "$author")" \
      "$(escape_md "$matched")" \
      "$(escape_md "$value")"
  done < "$candidate_rows"
  printf '\n'

  printf '## Follow-Up Buckets\n\n'
  printf '1. Ask packet-log holders for collector RPC `8113`, `8114`, and `8115` captures, plus code-storage-to-inventory transfers with before/after inventory state.\n'
  printf '2. Treat `vendor_items.csv` as observed-log coverage rather than complete vendor truth; prioritize vendor-open captures for static objects that already correlate with vendor rows.\n'
  printf '3. Follow the Neo packet-log and combat-log leads for raw logs before accepting extracted JSON/text summaries as evidence.\n'
  printf '4. Keep replay/source leads (`MxOSource`, `reality.exe`, old Python replay server, old sniffer code, mission-log torrent) as acquisition routes for historical packet corpora.\n'
  printf '5. Pair every acquired packet log with exact action context because the Discord evidence repeatedly says old logs are partial and client-visible only.\n'
} > "$report_path"

if [[ -n "$out_path" ]]; then
  mkdir -p "$(dirname "$out_path")"
  cp "$report_path" "$out_path"
  printf 'Wrote %s\n' "$out_path"
else
  cat "$report_path"
fi
