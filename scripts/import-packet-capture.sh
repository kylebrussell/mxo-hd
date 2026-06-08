#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/import-packet-capture.sh --source PATH [options]

Stages received text packet logs under research/packet-dumps/community-intake,
writes provenance metadata, and runs packet signature triage.

Options:
  --source PATH      Directory or supported archive containing logs to import.
                     Supported archives: .zip, .tar, .tar.gz, .tgz,
                     .tar.bz2, .tbz2, .tar.xz, .txz. Required.
  --label TEXT       Short source label used in the destination folder.
                     Defaults to the source directory name.
  --sender TEXT      Person, group, or channel that provided the logs.
  --provenance TEXT  Old live-server, MxOEmu, Hardline Dreams, unknown, etc.
  --context TEXT     Short action/context notes for SOURCE.md.
  --out-root DIR     Destination root. Defaults to research/packet-dumps/community-intake.
  --max-bytes N      Maximum file size to import. Defaults to 5242880.
                     Imported file SHA-256 hashes are recorded in SOURCE.md.
  --dry-run          Write a review preview under /tmp without staging files.
  --preview          Alias for --dry-run.
  -h, --help         Show this help text.
USAGE
}

source_input=""
label=""
sender="unspecified"
provenance="unspecified"
context="unspecified"
out_root=""
max_bytes=5242880
dry_run=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      [[ $# -ge 2 ]] || { echo "--source requires a value" >&2; exit 2; }
      source_input="$2"
      shift 2
      ;;
    --label)
      [[ $# -ge 2 ]] || { echo "--label requires a value" >&2; exit 2; }
      label="$2"
      shift 2
      ;;
    --sender)
      [[ $# -ge 2 ]] || { echo "--sender requires a value" >&2; exit 2; }
      sender="$2"
      shift 2
      ;;
    --provenance)
      [[ $# -ge 2 ]] || { echo "--provenance requires a value" >&2; exit 2; }
      provenance="$2"
      shift 2
      ;;
    --context)
      [[ $# -ge 2 ]] || { echo "--context requires a value" >&2; exit 2; }
      context="$2"
      shift 2
      ;;
    --out-root)
      [[ $# -ge 2 ]] || { echo "--out-root requires a value" >&2; exit 2; }
      out_root="$2"
      shift 2
      ;;
    --max-bytes)
      [[ $# -ge 2 ]] || { echo "--max-bytes requires a value" >&2; exit 2; }
      max_bytes="$2"
      shift 2
      ;;
    --dry-run|--preview)
      dry_run=1
      shift
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

if [[ -z "$source_input" ]]; then
  echo "--source is required" >&2
  usage >&2
  exit 2
fi

if ! [[ "$max_bytes" =~ ^[0-9]+$ ]] || [[ "$max_bytes" -lt 1 ]]; then
  echo "--max-bytes must be a positive integer" >&2
  exit 2
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
out_root="${out_root:-$repo_root/research/packet-dumps/community-intake}"

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
        echo "unzip is required to import .zip packet captures" >&2
        exit 2
      fi
      unzip -Z1 "$archive" | validate_archive_entries "$archive"
      unzip -q "$archive" -d "$extract_dir"
      ;;
    *.tar|*.tar.gz|*.tgz|*.tar.bz2|*.tbz2|*.tar.xz|*.txz)
      if ! command -v tar >/dev/null 2>&1; then
        echo "tar is required to import tar packet captures" >&2
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

slugify() {
  local value="$1"
  value="$(printf '%s' "$value" | tr '[:upper:]' '[:lower:]')"
  value="$(printf '%s' "$value" | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//')"
  printf '%s' "${value:-capture}"
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

sanitize_path() {
  local value="$1"
  value="$(printf '%s' "$value" | sed -E 's#^\./##; s#[/[:space:]]+#_#g; s/[^A-Za-z0-9._-]+/_/g; s/^_+//; s/_+$//')"
  printf '%s' "${value:-log}"
}

is_text_file() {
  local file="$1"
  LC_ALL=C grep -Iq . "$file"
}

has_sensitive_marker() {
  local file="$1"
  LC_ALL=C rg --text --ignore-case --quiet \
    -- 'password|session[[:space:]_-]*token|private[[:space:]_-]*key|-----BEGIN [A-Z ]*PRIVATE KEY-----' "$file"
}

sha256_file() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file" | awk '{ print $1 }'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$file" | awk '{ print $1 }'
  else
    echo "sha256sum or shasum is required to import packet captures" >&2
    exit 2
  fi
}

if [[ ! -e "$source_input" ]]; then
  echo "--source does not exist: $source_input" >&2
  exit 2
fi

source_kind="directory"
source_display=""
scanned_display=""
source_archive_hash=""

if [[ -d "$source_input" ]]; then
  source_dir="$(cd "$source_input" && pwd)"
  source_display="$source_dir"
  scanned_display="$source_dir"
elif [[ -f "$source_input" ]]; then
  source_kind="archive"
  source_file="$(source_input_path "$source_input")"
  source_display="$source_file"
  source_archive_hash="$(sha256_file "$source_file")"
  extract_root="$(mktemp -d "${TMPDIR:-/tmp}/mxo-packet-import.XXXXXX")"
  tmp_dirs+=("$extract_root")
  source_dir="$extract_root/extracted"
  extract_archive "$source_file" "$source_dir"
  scanned_display="temporary extraction of $source_file"
else
  echo "--source must be a directory or supported archive: $source_input" >&2
  exit 2
fi

label="${label:-$(basename "$source_display")}"
timestamp="$(date -u +"%Y%m%dT%H%M%SZ")"
if [[ "$dry_run" -eq 1 ]]; then
  dest_dir="$(mktemp -d "${TMPDIR:-/tmp}/mxo-packet-import-preview.XXXXXX")"
else
  dest_base="$out_root/${timestamp}-$(slugify "$label")"
  dest_dir="$dest_base"
  dest_suffix=1
  while [[ -e "$dest_dir" ]]; do
    dest_dir="${dest_base}-${dest_suffix}"
    dest_suffix=$((dest_suffix + 1))
  done
fi
logs_dir="$dest_dir/logs"
mkdir -p "$logs_dir"

imported=0
skipped=0
duplicate_count=0
duplicate_tmp="$dest_dir/DUPLICATE_HASH_LEADS.tmp"

{
  if [[ "$dry_run" -eq 1 ]]; then
    printf '# Packet Capture Source Preview\n\n'
  else
    printf '# Packet Capture Source\n\n'
  fi
  printf -- '- Imported at UTC: %s\n' "$(markdown_code "$timestamp")"
  printf -- '- Dry-run: %s\n' "$(markdown_code "$dry_run")"
  printf -- '- Source kind: %s\n' "$(markdown_code "$source_kind")"
  printf -- '- Source input: %s\n' "$(markdown_code "$source_display")"
  if [[ -n "$source_archive_hash" ]]; then
    printf -- '- Source archive SHA-256: %s\n' "$(markdown_code "$source_archive_hash")"
  fi
  printf -- '- Scanned files from: %s\n' "$(markdown_code "$scanned_display")"
  printf -- '- Sender: %s\n' "$(markdown_code "$sender")"
  printf -- '- Provenance: %s\n' "$(markdown_code "$provenance")"
  printf -- '- Context: %s\n' "$(markdown_code "$context")"
  printf -- '- Max imported file bytes: %s\n\n' "$(markdown_code "$max_bytes")"
  printf '## Imported Files\n\n'
  printf '| Original path | Imported path | Bytes | SHA-256 |\n'
  printf '| --- | --- | ---: | --- |\n'
} > "$dest_dir/SOURCE.md"

: > "$duplicate_tmp"

record_duplicate_hash_leads() {
  local kind="$1"
  local original_path="$2"
  local imported_path="$3"
  local hash="$4"
  local source_md=""
  local manifest_path=""
  local match=""
  local line_no=""

  [[ -z "$hash" ]] && return 0
  [[ -d "$out_root" ]] || return 0

  while IFS= read -r -d '' source_md; do
    [[ "$source_md" == "$dest_dir/SOURCE.md" ]] && continue

    match="$(LC_ALL=C rg --line-number --fixed-strings -- "\`$hash\`" "$source_md" | head -n 1 || true)"
    if [[ -n "$match" ]]; then
      manifest_path="${source_md#"$repo_root/"}"
      line_no="${match%%:*}"
      printf '| %s | %s | %s | %s | %s |\n' \
        "$kind" \
        "$(markdown_code "$original_path")" \
        "$(markdown_code "$imported_path")" \
        "$(markdown_code "$hash")" \
        "$(markdown_code "$manifest_path:$line_no")" >> "$duplicate_tmp"
      duplicate_count=$((duplicate_count + 1))
    fi
  done < <(find "$out_root" -type f -name SOURCE.md -print0 2>/dev/null || true)
}

if [[ -n "$source_archive_hash" ]]; then
  record_duplicate_hash_leads "source archive" "$source_display" "-" "$source_archive_hash"
fi

: > "$dest_dir/SKIPPED.md"
{
  printf '# Skipped Files\n\n'
  printf '| Original path | Reason |\n'
  printf '| --- | --- |\n'
} > "$dest_dir/SKIPPED.md"

while IFS= read -r -d '' file; do
  rel="${file#"$source_dir"/}"
  size="$(wc -c < "$file" | tr -d '[:space:]')"

  if [[ "$size" -eq 0 ]]; then
    printf '| %s | empty file |\n' "$(markdown_code "$rel")" >> "$dest_dir/SKIPPED.md"
    skipped=$((skipped + 1))
    continue
  fi

  if [[ "$size" -gt "$max_bytes" ]]; then
    printf '| %s | too large: %s bytes |\n' "$(markdown_code "$rel")" "$size" >> "$dest_dir/SKIPPED.md"
    skipped=$((skipped + 1))
    continue
  fi

  if ! is_text_file "$file"; then
    printf '| %s | binary or non-text content |\n' "$(markdown_code "$rel")" >> "$dest_dir/SKIPPED.md"
    skipped=$((skipped + 1))
    continue
  fi

  if has_sensitive_marker "$file"; then
    printf '| %s | possible secret marker; review manually before import |\n' "$(markdown_code "$rel")" >> "$dest_dir/SKIPPED.md"
    skipped=$((skipped + 1))
    continue
  fi

  target_name="$(sanitize_path "$rel")"
  target_base="${target_name%.txt}"
  target_path="$logs_dir/${target_base}.txt"
  suffix=1
  while [[ -e "$target_path" ]]; do
    target_path="$logs_dir/${target_base}_${suffix}.txt"
    suffix=$((suffix + 1))
  done

  cp "$file" "$target_path"
  hash="$(sha256_file "$target_path")"
  imported_rel="${target_path#"$dest_dir"/}"
  record_duplicate_hash_leads "imported file" "$rel" "$imported_rel" "$hash"
  printf '| %s | %s | %s | %s |\n' \
    "$(markdown_code "$rel")" \
    "$(markdown_code "$imported_rel")" \
    "$size" \
    "$(markdown_code "$hash")" >> "$dest_dir/SOURCE.md"
  imported=$((imported + 1))
done < <(find "$source_dir" -type f -print0)

if [[ "$imported" -eq 0 ]]; then
  printf '\nNo files were imported. Review `SKIPPED.md` before trying again.\n' >> "$dest_dir/SOURCE.md"
else
  "$repo_root/scripts/triage-packet-capture.sh" \
    --source "$logs_dir" \
    --out "$dest_dir/TRIAGE.md" \
    --max-matches 10 >/dev/null
fi

if [[ "$duplicate_count" -gt 0 ]]; then
  {
    printf '\n## Duplicate Hash Leads\n\n'
    printf 'These rows matched SHA-256 hashes already present in prior intake manifests. They are review leads, not automatic rejection: a repeated hash can mean an intentional resend, a partial repost, or a renamed/repacked archive.\n\n'
    printf '| Kind | Original path | Imported path | SHA-256 | Previous manifest |\n'
    printf '| --- | --- | --- | --- | --- |\n'
    cat "$duplicate_tmp"
  } >> "$dest_dir/SOURCE.md"
fi
rm -f "$duplicate_tmp"

{
  printf '\n## Intake Next Steps\n\n'
  if [[ "$dry_run" -eq 1 ]]; then
    printf -- '- This is a dry-run preview outside the repository. Re-run without `--dry-run` to stage reviewed logs.\n'
  fi
  printf -- '- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.\n'
  printf -- '- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.\n'
  printf -- '- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.\n'
  printf -- '- After review, run `scripts/packet-research.sh` to regenerate packet summaries.\n'
} >> "$dest_dir/SOURCE.md"

if [[ "$dry_run" -eq 1 ]]; then
  echo "Preview wrote $dest_dir"
  echo "Dry-run only; no files were staged under $out_root."
else
  echo "Wrote $dest_dir"
fi
echo "Imported $imported file(s); skipped $skipped file(s)."
if [[ "$imported" -eq 0 ]]; then
  exit 1
fi
