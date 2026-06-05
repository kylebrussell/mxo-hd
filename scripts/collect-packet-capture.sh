#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/collect-packet-capture.sh [--context "short action notes"] [--source DIR] [--out DIR]

Bundles Hardline Dreams packet research logs into a timestamped archive.

Options:
  --context TEXT  Human-readable action notes to include in capture-context.txt.
  --source DIR    Directory to search first. Defaults to the repository root.
  --out DIR       Output directory. Defaults to data/experiment_artifacts/packet-captures.
  -h, --help      Show this help text.
USAGE
}

context=""
source_dir=""
out_dir=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --context)
      [[ $# -ge 2 ]] || { echo "--context requires a value" >&2; exit 2; }
      context="$2"
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

timestamp="$(date -u +"%Y%m%dT%H%M%SZ")"
bundle_dir="$out_dir/$timestamp"
archive_path="$out_dir/$timestamp.tar.gz"
mkdir -p "$bundle_dir"

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
for log_name in "${log_names[@]}"; do
  for dir in "${search_dirs[@]}"; do
    path="$dir/$log_name"
    if [[ -f "$path" ]]; then
      if [[ "$dir" == "$repo_root" ]]; then
        target="repo-root"
      elif [[ "$dir" == "$repo_root/"* ]]; then
        target="${dir#"$repo_root/"}"
        target="${target//\//__}"
      else
        target="${dir//\//__}"
      fi
      mkdir -p "$bundle_dir/logs"
      cp "$path" "$bundle_dir/logs/${target}__${log_name}"
      copied=$((copied + 1))
    fi
  done
done

cat > "$bundle_dir/capture-context.txt" <<EOF
Captured at UTC: $timestamp
Repository: $repo_root
Primary source directory: $source_dir
Context: ${context:-unspecified}

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
fi

tar -C "$out_dir" -czf "$archive_path" "$timestamp"
echo "Wrote $archive_path"
if [[ "$copied" -eq 0 ]]; then
  echo "Warning: no log files were found; archive contains only the context template." >&2
else
  echo "Included $copied log file(s)."
fi
