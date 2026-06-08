#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/collect-packet-capture.sh [options]

Bundles Hardline Dreams packet research logs into a timestamped archive with
file hashes, skipped-file evidence, and packet-signature triage.

Options:
  --context TEXT    Human-readable action notes to include in capture-context.txt.
  --character TEXT  Character handle used during the capture.
  --protocol TEXT   CR1, CR2, or unknown.
  --location TEXT   District, hardline, coordinates, and nearby NPC/static object.
  --actions TEXT    Exact action sequence, including failed attempts.
  --objects TEXT    Known object ids, static ids, vendor ids, or NPC names.
  --items TEXT      Known item GoIDs, item names, slots, stack counts, or prices.
  --cash TEXT       Cash before/after.
  --inventory TEXT  Inventory slot/count before/after.
  --state TEXT      Health, ability, buff, FX, equipment, team, crew, or faction state.
  --missing TEXT    Known missing context or failed actions.
  --source DIR      Directory to search first. Defaults to the repository root.
  --out DIR         Output directory. Defaults to data/experiment_artifacts/packet-captures.
  -h, --help        Show this help text.
USAGE
}

context=""
character_handle=""
protocol=""
location=""
actions=""
objects=""
items=""
cash_state=""
inventory_state=""
state_notes=""
missing_notes=""
source_dir=""
out_dir=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --context)
      [[ $# -ge 2 ]] || { echo "--context requires a value" >&2; exit 2; }
      context="$2"
      shift 2
      ;;
    --character)
      [[ $# -ge 2 ]] || { echo "--character requires a value" >&2; exit 2; }
      character_handle="$2"
      shift 2
      ;;
    --protocol)
      [[ $# -ge 2 ]] || { echo "--protocol requires a value" >&2; exit 2; }
      protocol="$2"
      shift 2
      ;;
    --location)
      [[ $# -ge 2 ]] || { echo "--location requires a value" >&2; exit 2; }
      location="$2"
      shift 2
      ;;
    --actions)
      [[ $# -ge 2 ]] || { echo "--actions requires a value" >&2; exit 2; }
      actions="$2"
      shift 2
      ;;
    --objects)
      [[ $# -ge 2 ]] || { echo "--objects requires a value" >&2; exit 2; }
      objects="$2"
      shift 2
      ;;
    --items)
      [[ $# -ge 2 ]] || { echo "--items requires a value" >&2; exit 2; }
      items="$2"
      shift 2
      ;;
    --cash)
      [[ $# -ge 2 ]] || { echo "--cash requires a value" >&2; exit 2; }
      cash_state="$2"
      shift 2
      ;;
    --inventory)
      [[ $# -ge 2 ]] || { echo "--inventory requires a value" >&2; exit 2; }
      inventory_state="$2"
      shift 2
      ;;
    --state)
      [[ $# -ge 2 ]] || { echo "--state requires a value" >&2; exit 2; }
      state_notes="$2"
      shift 2
      ;;
    --missing)
      [[ $# -ge 2 ]] || { echo "--missing requires a value" >&2; exit 2; }
      missing_notes="$2"
      shift 2
      ;;
    --source)
      [[ $# -ge 2 ]] || { echo "--source requires a value" >&2; exit 2; }
      source_dir="$2"
      shift 2
      ;;
    --out)
      [[ $# -ge 2 ]] || { echo "--out requires a value" >&2; exit 2; }
      out_dir="$2"
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

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
source_dir="${source_dir:-$repo_root}"
source_dir="$(cd "$source_dir" && pwd)"
out_dir="${out_dir:-$repo_root/data/experiment_artifacts/packet-captures}"

if ! command -v rg >/dev/null 2>&1; then
  echo "ripgrep (rg) is required to collect packet captures" >&2
  exit 2
fi

timestamp="$(date -u +"%Y%m%dT%H%M%SZ")"
bundle_dir="$out_dir/$timestamp"
archive_path="$out_dir/$timestamp.tar.gz"
mkdir -p "$bundle_dir"

sha256_file() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file" | awk '{ print $1 }'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$file" | awk '{ print $1 }'
  else
    echo "sha256sum or shasum is required to collect packet captures" >&2
    exit 2
  fi
}

has_sensitive_marker() {
  local file="$1"
  LC_ALL=C rg --text --ignore-case --quiet \
    -- 'password|session[[:space:]_-]*token|private[[:space:]_-]*key|-----BEGIN [A-Z ]*PRIVATE KEY-----' "$file"
}

context_value() {
  local value="${1:-}"
  value="${value//$'\r'/ }"
  value="${value//$'\n'/ }"
  printf '%s' "${value:-unspecified}"
}

markdown_table_text() {
  local value="$1"
  value="$(context_value "$value")"
  value="${value//|/\\|}"
  printf '%s' "$value"
}

markdown_html_text() {
  local value="$1"
  value="${value//&/&amp;}"
  value="${value//</&lt;}"
  value="${value//>/&gt;}"
  printf '%s' "$value"
}

markdown_code() {
  local value=""
  value="$(markdown_table_text "$1")"
  if [[ "$value" == *'`'* ]]; then
    printf '<code>%s</code>' "$(markdown_html_text "$value")"
  else
    printf '`%s`' "$value"
  fi
}

log_names=(
  "PacketLog.txt"
  "UnknownRPC.txt"
  "UnknownClient03Request.txt"
  "DebugLog.txt"
  "ServerLog.txt"
)

search_dirs=("$source_dir")
add_search_dir() {
  local candidate="$1"
  local existing=""
  for existing in "${search_dirs[@]}"; do
    [[ "$existing" == "$candidate" ]] && return
  done
  search_dirs+=("$candidate")
}

add_search_dir "$repo_root"
add_search_dir "$repo_root/hds/bin/Debug/net8.0"
add_search_dir "$repo_root/hds/bin/Release/net8.0"

copied=0
skipped=0
manifest_path="$bundle_dir/capture-file-manifest.md"
skipped_path="$bundle_dir/skipped-files.md"

{
  printf '# Capture File Manifest\n\n'
  printf -- '- Captured at UTC: %s\n' "$(markdown_code "$timestamp")"
  printf -- '- Repository: %s\n' "$(markdown_code "$repo_root")"
  printf -- '- Primary source directory: %s\n' "$(markdown_code "$source_dir")"
  printf -- '- Context: %s\n\n' "$(markdown_code "$context")"
  printf '## Capture Metadata\n\n'
  printf '| Field | Value |\n'
  printf '| --- | --- |\n'
  printf '| Character handle | %s |\n' "$(markdown_code "$character_handle")"
  printf '| Protocol | %s |\n' "$(markdown_code "$protocol")"
  printf '| Location | %s |\n' "$(markdown_code "$location")"
  printf '| Actions | %s |\n' "$(markdown_code "$actions")"
  printf '| Objects | %s |\n' "$(markdown_code "$objects")"
  printf '| Items | %s |\n' "$(markdown_code "$items")"
  printf '| Cash | %s |\n' "$(markdown_code "$cash_state")"
  printf '| Inventory | %s |\n' "$(markdown_code "$inventory_state")"
  printf '| State | %s |\n' "$(markdown_code "$state_notes")"
  printf '| Missing context | %s |\n\n' "$(markdown_code "$missing_notes")"
  printf '## Bundled Files\n\n'
  printf '| Log name | Source path | Bundled path | Bytes | SHA-256 |\n'
  printf '| --- | --- | --- | ---: | --- |\n'
} > "$manifest_path"

{
  printf '# Skipped Files\n\n'
  printf '| Source path | Reason |\n'
  printf '| --- | --- |\n'
} > "$skipped_path"

for log_name in "${log_names[@]}"; do
  for dir in "${search_dirs[@]}"; do
    path="$dir/$log_name"
    if [[ -f "$path" ]]; then
      if has_sensitive_marker "$path"; then
        printf '| %s | possible secret marker; review manually before bundling |\n' "$(markdown_code "$path")" >> "$skipped_path"
        skipped=$((skipped + 1))
        continue
      fi

      if [[ "$dir" == "$repo_root" ]]; then
        target="repo-root"
      elif [[ "$dir" == "$repo_root/"* ]]; then
        target="${dir#"$repo_root/"}"
        target="${target//\//__}"
      else
        target="${dir//\//__}"
      fi
      mkdir -p "$bundle_dir/logs"
      bundled_path="$bundle_dir/logs/${target}__${log_name}"
      bundled_rel="${bundled_path#"$bundle_dir"/}"
      cp "$path" "$bundled_path"
      size="$(wc -c < "$bundled_path" | tr -d '[:space:]')"
      hash="$(sha256_file "$bundled_path")"
      printf '| %s | %s | %s | %s | %s |\n' \
        "$(markdown_code "$log_name")" \
        "$(markdown_code "$path")" \
        "$(markdown_code "$bundled_rel")" \
        "$size" \
        "$(markdown_code "$hash")" >> "$manifest_path"
      copied=$((copied + 1))
    fi
  done
done

cat > "$bundle_dir/capture-context.txt" <<EOF
Captured at UTC: $timestamp
Repository: $repo_root
Primary source directory: $source_dir
Context: ${context:-unspecified}

Capture metadata:
- Character handle: $(context_value "$character_handle")
- CR1 or CR2: $(context_value "$protocol")
- District/hardline/coordinates: $(context_value "$location")
- Action sequence: $(context_value "$actions")
- Object ids / item GoIDs: $(context_value "$objects")
- Item details: $(context_value "$items")
- Cash before/after: $(context_value "$cash_state")
- Inventory slot/count before/after: $(context_value "$inventory_state")
- Health/ability/FX/equipment/team/crew/faction state: $(context_value "$state_notes")
- Known missing or failed actions: $(context_value "$missing_notes")

Recommended runtime flags:
MXO_PACKET_LOG=1
MXO_RPC_LOG=1
MXO_CLIENT_VIEW_LOG=1
MXO_DEBUG_LOG=1

Requested action notes:
- Character handle:
- CR1 or CR2:
- District/hardline/coordinates:
- Action sequence:
- Object ids / item GoIDs:
- Cash before/after:
- Inventory slot/count before/after:
- Health/ability/FX/equipment before/after:
- Known missing or failed actions:
EOF

if [[ "$copied" -eq 0 ]]; then
  cat > "$bundle_dir/NO_LOGS_FOUND.txt" <<EOF
No packet log files were found.

Searched:
$(printf '%s\n' "${search_dirs[@]}")

Expected names:
$(printf '%s\n' "${log_names[@]}")
EOF
else
  "$repo_root/scripts/triage-packet-capture.sh" \
    --source "$bundle_dir/logs" \
    --out "$bundle_dir/TRIAGE.md" \
    --max-matches 10 >/dev/null
fi

tar -C "$out_dir" -czf "$archive_path" "$timestamp"
echo "Wrote $archive_path"
if [[ "$copied" -eq 0 ]]; then
  echo "Warning: no log files were found; archive contains only the context template." >&2
else
  echo "Included $copied log file(s)."
fi
if [[ "$skipped" -gt 0 ]]; then
  echo "Skipped $skipped file(s) with possible secret markers; see skipped-files.md in the archive." >&2
fi
