#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/triage-packet-capture.sh [--source PATH] [--out FILE] [--max-matches N]

Scans received packet-log text for current Hardline Dreams packet-research targets
and writes a Markdown triage report. Hex signatures match both spaced and compact
forms, such as "01 00 08 50" and "01000850".

Options:
  --source PATH    Directory or supported archive containing packet logs.
                   Supported archives: .zip, .tar, .tar.gz, .tgz,
                   .tar.bz2, .tbz2, .tar.xz, .txz.
                   Defaults to research/packet-dumps.
  --out FILE       Write the report to FILE instead of stdout.
  --max-matches N  Maximum sample lines per pattern. Defaults to 20.
  -h, --help       Show this help text.
USAGE
}

source_input=""
out_file=""
max_matches=20

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      [[ $# -ge 2 ]] || { echo "--source requires a value" >&2; exit 2; }
      source_input="$2"
      shift 2
      ;;
    --out)
      [[ $# -ge 2 ]] || { echo "--out requires a value" >&2; exit 2; }
      out_file="$2"
      shift 2
      ;;
    --max-matches)
      [[ $# -ge 2 ]] || { echo "--max-matches requires a value" >&2; exit 2; }
      max_matches="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if ! [[ "$max_matches" =~ ^[0-9]+$ ]] || [[ "$max_matches" -lt 1 ]]; then
  echo "--max-matches must be a positive integer" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
source_input="${source_input:-$repo_root/research/packet-dumps}"

tmp_dirs=()
cleanup() {
  local dir=""
  [[ "${#tmp_dirs[@]}" -eq 0 ]] && return
  for dir in "${tmp_dirs[@]}"; do
    [[ -n "$dir" && -d "$dir" ]] && rm -rf "$dir"
  done
}
trap cleanup EXIT

source_input_path() {
  local input="$1"
  local input_dir=""
  local input_base=""
  input_dir="$(cd "$(dirname "$input")" && pwd)"
  input_base="$(basename "$input")"
  printf '%s/%s' "$input_dir" "$input_base"
}

is_safe_archive_entry() {
  local entry="$1"
  local normalized=""
  local part=""
  local -a parts=()

  normalized="${entry//\\//}"
  [[ -z "$normalized" ]] && return 0
  [[ "$normalized" == /* ]] && return 1

  IFS='/' read -r -a parts <<< "$normalized"
  for part in "${parts[@]}"; do
    [[ "$part" == ".." ]] && return 1
  done

  return 0
}

validate_archive_entries() {
  local archive="$1"
  local entry=""

  while IFS= read -r entry; do
    if ! is_safe_archive_entry "$entry"; then
      echo "Archive contains unsafe path: $entry" >&2
      echo "Refusing to extract $archive" >&2
      return 2
    fi
  done
}

extract_archive() {
  local archive="$1"
  local extract_dir="$2"
  local lower_name=""

  lower_name="$(basename "$archive" | tr '[:upper:]' '[:lower:]')"
  mkdir -p "$extract_dir"

  case "$lower_name" in
    *.zip)
      if ! command -v unzip >/dev/null 2>&1; then
        echo "unzip is required to triage .zip packet captures" >&2
        exit 2
      fi
      unzip -Z1 "$archive" | validate_archive_entries "$archive"
      unzip -q "$archive" -d "$extract_dir"
      ;;
    *.tar|*.tar.gz|*.tgz|*.tar.bz2|*.tbz2|*.tar.xz|*.txz)
      if ! command -v tar >/dev/null 2>&1; then
        echo "tar is required to triage tar packet captures" >&2
        exit 2
      fi
      tar -tf "$archive" | validate_archive_entries "$archive"
      tar -xf "$archive" -C "$extract_dir"
      ;;
    *)
      echo "Unsupported --source archive type: $archive" >&2
      echo "Use a directory, .zip, .tar, .tar.gz, .tgz, .tar.bz2, .tbz2, .tar.xz, or .txz." >&2
      exit 2
      ;;
  esac
}

if [[ ! -e "$source_input" ]]; then
  echo "--source does not exist: $source_input" >&2
  exit 2
fi

source_kind="directory"
source_display=""
source_dir=""

if [[ -d "$source_input" ]]; then
  source_dir="$(cd "$source_input" && pwd)"
  source_display="$source_dir"
elif [[ -f "$source_input" ]]; then
  source_kind="archive"
  source_file="$(source_input_path "$source_input")"
  source_display="$source_file"
  extract_root="$(mktemp -d "${TMPDIR:-/tmp}/mxo-packet-triage.XXXXXX")"
  tmp_dirs+=("$extract_root")
  source_dir="$extract_root/extracted"
  extract_archive "$source_file" "$source_dir"
else
  echo "--source must be a directory or supported archive: $source_input" >&2
  exit 2
fi

if [[ -n "$out_file" ]]; then
  mkdir -p "$(dirname "$out_file")"
fi

if ! command -v rg >/dev/null 2>&1; then
  echo "ripgrep (rg) is required for packet capture triage" >&2
  exit 2
fi

rg_common=(
  --text
  --ignore-case
  --glob '!**/.git/**'
  --glob '!**/bin/**'
  --glob '!**/obj/**'
  --glob '!**/node_modules/**'
)

hex_regex() {
  local pattern="$1"
  local -a bytes=()
  read -r -a bytes <<< "$pattern"
  local regex="${bytes[0]}"
  local byte=""
  for byte in "${bytes[@]:1}"; do
    regex+="[[:space:]]*$byte"
  done
  printf '%s' "$regex"
}

count_matches() {
  local pattern="$1"
  local regex=""
  regex="$(hex_regex "$pattern")"
  (rg "${rg_common[@]}" --count-matches -- "$regex" "$source_dir" || true) |
    awk -F: '{ total += $NF } END { print total + 0 }'
}

sample_matches() {
  local pattern="$1"
  local regex=""
  regex="$(hex_regex "$pattern")"
  (rg "${rg_common[@]}" --line-number --with-filename -- "$regex" "$source_dir" || true) |
    head -n "$max_matches" |
    sed "s#${source_dir}/##"
}

write_pattern() {
  local label="$1"
  local pattern="$2"
  local count=""
  local samples=""

  count="$(count_matches "$pattern")"
  printf '### %s\n\n' "$label"
  printf 'Signature: `%s` (spaced or compact hex)\n\n' "$pattern"
  printf 'Matches: %s\n\n' "$count"

  if [[ "$count" -gt 0 ]]; then
    samples="$(sample_matches "$pattern")"
    printf '```text\n%s\n```\n\n' "$samples"
  fi
}

generate_report() {
  printf '# Packet Capture Triage Report\n\n'
  printf -- '- Source kind: `%s`\n' "$source_kind"
  printf -- '- Source input: `%s`\n' "$source_display"
  printf -- '- Scanned files from: `%s`\n' "$source_dir"
  printf -- '- Generated UTC: `%s`\n' "$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
  printf -- '- Max sample lines per pattern: `%s`\n\n' "$max_matches"
  printf 'Use this report to decide whether received logs are worth importing into `research/packet-dumps/`. Matches are byte-string leads, not decoded packet claims.\n\n'

  printf '## Vendor And Interaction Leads\n\n'
  write_pattern 'SERVER_VENDOR_OPEN candidate' '81 0d'
  write_pattern 'CLIENT_VENDOR_BUY candidate' '81 0e'
  write_pattern 'CLIENT_VENDOR_SELL candidate' '81 11'
  write_pattern 'CR2 static object interaction' '80 c8'
  write_pattern 'CR1 static object interaction' '80 c3'
  write_pattern 'SERVER object/state bundle' '80 bc'

  printf '## Selector ff Vendor Body Leads\n\n'
  write_pattern 'Selector ff vendor field +9 family' 'e0 2d 81 0d'
  write_pattern 'Selector ff vendor body prefix' '01 43 00 00 00 00 00 00 00 e0 2d 81 0d 22 89 20'
  write_pattern 'Selector ff continuation prefix 00 41 00 ff' '00 41 00 ff'
  write_pattern 'Selector ff continuation prefix 00 01 00 ff' '00 01 00 ff'
  write_pattern 'Selector ff continuation prefix 00 01 04 ff' '00 01 04 ff'
  write_pattern 'Selector ff continuation prefix 00 01 10 ff' '00 01 10 ff'
  write_pattern 'Selector ff continuation prefix 00 00 00 ff' '00 00 00 ff'
  write_pattern 'Vendor-open continuation resource shape' '00 41 00 ff 00 00 00 00 b0 74'
  write_pattern 'Vendor-sell continuation resource shape' '00 00 00 ff 00 00 00 00 1b ee'

  printf '## Object599 / NPC_BASE Spawn-Profile Leads\n\n'
  write_pattern 'Object599 creation prefix' '0c 57 02'
  write_pattern 'Adjacent Object599 boundary prefix' '00 0c 57 02'
  write_pattern 'Header-like selector 50' '01 00 08 50'
  write_pattern 'Header-like selector 67' '01 00 08 67'
  write_pattern 'Header-like selector 68' '01 00 08 68'
  write_pattern 'Header-like selector 69' '01 00 08 69'
  write_pattern 'Header-like selector 76' '01 00 0c 76'
  write_pattern 'Header-like selector 90' '01 00 08 90'
  write_pattern 'Header-like selector a3' '01 00 08 a3'
  write_pattern 'Header-like selector d0' '01 00 08 d0'

  printf '## Movement And Object-State Leads\n\n'
  write_pattern 'Mode 06 movement selector 08' '00 06 08'
  write_pattern 'Mode 06 movement selector 0a' '00 06 0a'
  write_pattern 'Mode 06 movement selector 0c' '00 06 0c'
  write_pattern 'Mode 06 movement selector 0e' '00 06 0e'
  write_pattern 'Repeated mode 06 suffix lead' 'ff 00 00 00 10 00 00 00 00 00 01 ff 00 00 00 00'
  write_pattern 'Movement-state ff 01 64 01 tail' 'ff 01 64 01'
  write_pattern 'Movement-state ff 01 4e 01 tail' 'ff 01 4e 01'

  printf '## Ability, Effect, And Combat-State Leads\n\n'
  write_pattern 'SERVER_PLAYER_ATTRIBUTE / ability load' '80 b2'
  write_pattern 'SERVER_ABILITY_UNLOAD' '80 b3'
  write_pattern 'Object599 adrenaline booster effect id' '4c 0a 00 28'
  write_pattern 'Object599 fast healing effect id' 'b5 07 00 28'
  write_pattern 'Object599 accelerated spawn effect id' '66 00 00 04'

  printf '## Intake Next Steps\n\n'
  printf -- '- Preserve the original files separately before editing or importing anything.\n'
  printf -- '- Inspect matches with surrounding lines; embedded hits inside encrypted or unrelated bodies are leads only.\n'
  printf -- '- Record sender, provenance, CR1/CR2 status, action context, character handle, coordinates, known object ids, and known item GoIDs.\n'
  printf -- '- Import only text logs or text-like dumps that contain no passwords, session tokens, private keys, client binaries, or unrelated game assets.\n'
}

if [[ -n "$out_file" ]]; then
  generate_report > "$out_file"
  echo "Wrote $out_file"
else
  generate_report
fi
