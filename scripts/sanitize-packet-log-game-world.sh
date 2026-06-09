#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/sanitize-packet-log-game-world.sh --source PATH --out DIR

Copies packet-log text into DIR after stripping NUL bytes and retaining only
GAME/WORLD packet blocks. AUTH and MARGIN blocks are dropped so public logs with
handshake material can be reviewed and imported as sanitized derivatives.

Options:
  --source PATH  Packet log file or directory to sanitize. Required.
  --out DIR      Destination directory for sanitized logs. Required.
  -h, --help     Show this help text.
USAGE
}

source_input=""
out_dir=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      [[ $# -ge 2 ]] || { echo "--source requires a value" >&2; exit 2; }
      source_input="$2"
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

if [[ -z "$source_input" || -z "$out_dir" ]]; then
  usage >&2
  exit 2
fi

if [[ ! -e "$source_input" ]]; then
  echo "--source does not exist: $source_input" >&2
  exit 2
fi

source_input_path() {
  local input="$1"
  local input_dir=""
  local input_base=""
  input_dir="$(cd "$(dirname "$input")" && pwd)"
  input_base="$(basename "$input")"
  printf '%s/%s' "$input_dir" "$input_base"
}

sanitize_file() {
  local source_file="$1"
  local dest_file="$2"
  mkdir -p "$(dirname "$dest_file")"
  LC_ALL=C tr -d '\000' < "$source_file" | awk '
    function packet_header(line) {
      return line ~ /^(Client|AUTH|GAME|WORLD|MARGIN)->(AUTH|Client|GAME|WORLD|MARGIN) \[/
    }
    function retained_header(line) {
      return line ~ /^(Client->GAME|GAME->Client|Client->WORLD|WORLD->Client) \[/
    }
    function sensitive_line(line, lower) {
      lower = tolower(line)
      return lower ~ /(auth tf key|twofish key|cert[_ ]?challenge|ms_establishudpsession|as_auth|sessionkey|password|passwd|pwd|token|client->auth|auth->client|client->margin|margin->client)/
    }
    {
      sub(/\r$/, "")
      if (packet_header($0)) {
        keep = retained_header($0)
      }
      if (keep && !sensitive_line($0)) {
        print
      }
    }
  ' > "$dest_file"
}

source_input="$(source_input_path "$source_input")"
mkdir -p "$out_dir"

files_sanitized=0
files_with_content=0

if [[ -f "$source_input" ]]; then
  dest="$out_dir/$(basename "$source_input")"
  sanitize_file "$source_input" "$dest"
  files_sanitized=1
  [[ -s "$dest" ]] && files_with_content=1
elif [[ -d "$source_input" ]]; then
  while IFS= read -r -d '' source_file; do
    rel="${source_file#"$source_input"/}"
    dest="$out_dir/$rel"
    sanitize_file "$source_file" "$dest"
    files_sanitized=$((files_sanitized + 1))
    [[ -s "$dest" ]] && files_with_content=$((files_with_content + 1))
  done < <(find "$source_input" -type f \( -iname '*.log' -o -iname '*.txt' \) -print0)
else
  echo "--source must be a file or directory: $source_input" >&2
  exit 2
fi

printf 'Sanitized %d file(s); %d retained GAME/WORLD content. Wrote %s\n' \
  "$files_sanitized" \
  "$files_with_content" \
  "$out_dir"
