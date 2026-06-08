#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/run-warboy-packet-research.sh [options]

Downloads or reuses the public mxopackets2 Warboy/Rajko archive, verifies it,
extracts it safely outside Git, runs packet-signature triage, and runs the
packet research report with scratch outputs.

Options:
  --archive PATH     Use a local warboy.zip instead of downloading it.
  --source-dir DIR   Use an already extracted Warboy dump directory.
  --work-dir DIR     Scratch workspace. Defaults to
                     data/experiment_artifacts/packet-corpora/warboy.
  --out-dir DIR      Report directory. Defaults to WORK_DIR/reports.
  --max-matches N    Triage sample lines per pattern. Defaults to 5.
  -h, --help         Show this help text.

Environment:
  WARBOY_ARCHIVE_URL     Override the public archive URL.
  WARBOY_PACKET_WORKDIR  Override the default scratch workspace.
USAGE
}

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

default_archive_url="https://raw.githubusercontent.com/Vibrantic/mxo-resurgence/master/packets_and_assets/mxopackets2-master/mxopackets2-master/warboy.zip"
archive_url="${WARBOY_ARCHIVE_URL:-$default_archive_url}"
expected_sha256="3dde9509d4f1b97e09938315dc591acfde2e32c5f4f1fb3153ce738f816f6341"

archive_input=""
source_dir_input=""
work_dir="${WARBOY_PACKET_WORKDIR:-$repo_root/data/experiment_artifacts/packet-corpora/warboy}"
out_dir=""
max_matches=5

while [[ $# -gt 0 ]]; do
  case "$1" in
    --archive)
      [[ $# -ge 2 ]] || { echo "--archive requires a value" >&2; exit 2; }
      archive_input="$2"
      shift 2
      ;;
    --source-dir)
      [[ $# -ge 2 ]] || { echo "--source-dir requires a value" >&2; exit 2; }
      source_dir_input="$2"
      shift 2
      ;;
    --work-dir)
      [[ $# -ge 2 ]] || { echo "--work-dir requires a value" >&2; exit 2; }
      work_dir="$2"
      shift 2
      ;;
    --out-dir)
      [[ $# -ge 2 ]] || { echo "--out-dir requires a value" >&2; exit 2; }
      out_dir="$2"
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

if [[ -n "$archive_input" && -n "$source_dir_input" ]]; then
  echo "Use either --archive or --source-dir, not both." >&2
  exit 2
fi

if ! [[ "$max_matches" =~ ^[0-9]+$ ]] || [[ "$max_matches" -lt 1 ]]; then
  echo "--max-matches must be a positive integer" >&2
  exit 2
fi

out_dir="${out_dir:-$work_dir/reports}"
mkdir -p "$work_dir" "$out_dir"
work_dir="$(cd "$work_dir" && pwd)"
out_dir="$(cd "$out_dir" && pwd)"

absolute_path() {
  local input="$1"
  local input_dir=""
  local input_base=""
  if [[ -d "$input" ]]; then
    (cd "$input" && pwd)
  else
    input_dir="$(cd "$(dirname "$input")" && pwd)"
    input_base="$(basename "$input")"
    printf '%s/%s\n' "$input_dir" "$input_base"
  fi
}

file_sha256() {
  local path="$1"
  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$path" | awk '{ print $1 }'
  elif command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$path" | awk '{ print $1 }'
  else
    echo "shasum or sha256sum is required to verify the Warboy archive" >&2
    exit 2
  fi
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

has_packet_logs() {
  local dir="$1"
  [[ -d "$dir" ]] || return 1
  find "$dir" -type f \( -iname '*.log' -o -iname '*.log.*' -o -iname '*.txt' \) -print -quit | grep -q .
}

find_warboy_dump_root() {
  local root="$1"
  local base_name=""
  local candidate=""

  if [[ -d "$root/warboy" ]] && has_packet_logs "$root/warboy"; then
    absolute_path "$root/warboy"
    return 0
  fi

  base_name="$(basename "$root" | tr '[:upper:]' '[:lower:]')"
  if [[ "$base_name" == "warboy" ]] && has_packet_logs "$root"; then
    absolute_path "$root"
    return 0
  fi

  while IFS= read -r candidate; do
    if has_packet_logs "$candidate"; then
      absolute_path "$candidate"
      return 0
    fi
  done < <(find "$root" -type d -iname warboy -print)

  if has_packet_logs "$root"; then
    absolute_path "$root"
    return 0
  fi

  echo "Could not find packet log files under $root" >&2
  return 2
}

extract_archive() {
  local archive="$1"
  local extract_dir="$2"

  if ! command -v unzip >/dev/null 2>&1; then
    echo "unzip is required to extract the Warboy archive" >&2
    exit 2
  fi

  mkdir -p "$extract_dir"
  unzip -Z1 "$archive" | validate_archive_entries "$archive"

  if ! has_packet_logs "$extract_dir"; then
    unzip -q "$archive" -d "$extract_dir"
  fi
}

archive_path=""
source_mode="download"
actual_sha256=""

if [[ -n "$source_dir_input" ]]; then
  if [[ ! -d "$source_dir_input" ]]; then
    echo "--source-dir is not a directory: $source_dir_input" >&2
    exit 2
  fi
  dump_root="$(find_warboy_dump_root "$(absolute_path "$source_dir_input")")"
  source_mode="source-dir"
elif [[ -n "$archive_input" ]]; then
  if [[ ! -f "$archive_input" ]]; then
    echo "--archive is not a file: $archive_input" >&2
    exit 2
  fi
  archive_path="$(absolute_path "$archive_input")"
else
  archive_path="$work_dir/warboy.zip"
  if [[ ! -f "$archive_path" ]]; then
    if ! command -v curl >/dev/null 2>&1; then
      echo "curl is required to download the Warboy archive" >&2
      exit 2
    fi
    echo "Downloading $archive_url"
    curl -fSLo "$archive_path" "$archive_url"
  fi
fi

if [[ -n "$archive_path" ]]; then
  actual_sha256="$(file_sha256 "$archive_path")"
  if [[ "$actual_sha256" != "$expected_sha256" ]]; then
    echo "Warboy archive SHA-256 mismatch" >&2
    echo "Expected: $expected_sha256" >&2
    echo "Actual:   $actual_sha256" >&2
    exit 2
  fi

  extract_dir="$work_dir/extracted-${actual_sha256:0:12}"
  extract_archive "$archive_path" "$extract_dir"
  dump_root="$(find_warboy_dump_root "$extract_dir")"
  [[ -n "$archive_input" ]] && source_mode="archive"
fi

triage_out="$out_dir/warboy-triage.md"
json_out="$out_dir/packet-research-with-warboy.json"
markdown_out="$out_dir/packet-research-with-warboy.md"
metadata_out="$out_dir/README.md"
generated_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"

"$script_dir/triage-packet-capture.sh" \
  --source "$dump_root" \
  --out "$triage_out" \
  --max-matches "$max_matches"

PACKET_RESEARCH_EXTRA_DUMPS="$dump_root" \
PACKET_RESEARCH_JSON="$json_out" \
PACKET_RESEARCH_MARKDOWN="$markdown_out" \
"$script_dir/packet-research.sh"

cat > "$metadata_out" <<EOF
# Warboy Packet Research Scratch Artifacts

- Generated UTC: \`$generated_at\`
- Source mode: \`$source_mode\`
- Source URL: \`$archive_url\`
- Expected archive SHA-256: \`$expected_sha256\`
- Actual archive SHA-256: \`${actual_sha256:-not checked in source-dir mode}\`
- Archive path: \`${archive_path:-not used in source-dir mode}\`
- Dump root: \`$dump_root\`
- Triage report: \`$triage_out\`
- Packet research JSON: \`$json_out\`
- Packet research Markdown: \`$markdown_out\`

The raw archive and extracted logs are intentionally kept outside tracked packet
dump paths. Commit only curated reports or small semantic resources unless a
large-file storage policy is chosen.
EOF

echo "Warboy dump root: $dump_root"
echo "Triage report: $triage_out"
echo "Packet research JSON: $json_out"
echo "Packet research Markdown: $markdown_out"
echo "Metadata: $metadata_out"
