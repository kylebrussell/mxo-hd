#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

tmp_root="$(mktemp -d "${TMPDIR:-/tmp}/mxo-packet-tools.XXXXXX")"
cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

assert_file() {
  local path="$1"
  if [[ ! -f "$path" ]]; then
    echo "Expected file was not created: $path" >&2
    exit 1
  fi
}

assert_contains() {
  local path="$1"
  local expected="$2"
  if ! LC_ALL=C rg --fixed-strings --quiet -- "$expected" "$path"; then
    echo "Expected to find '$expected' in $path" >&2
    echo "--- $path ---" >&2
    sed -n '1,220p' "$path" >&2
    exit 1
  fi
}

assert_not_contains() {
  local path="$1"
  local unexpected="$2"
  if LC_ALL=C rg --fixed-strings --quiet -- "$unexpected" "$path"; then
    echo "Did not expect to find '$unexpected' in $path" >&2
    echo "--- $path ---" >&2
    sed -n '1,220p' "$path" >&2
    exit 1
  fi
}

sha256_file() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file" | awk '{ print $1 }'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$file" | awk '{ print $1 }'
  else
    echo "sha256sum or shasum is required for packet capture tooling tests" >&2
    exit 2
  fi
}

capture_import_dir() {
  local output_file="$1"
  awk '
    /^Wrote / { sub(/^Wrote /, ""); print; exit }
    /^Preview wrote / { sub(/^Preview wrote /, ""); print; exit }
  ' "$output_file"
}

capture_archive_path() {
  local output_file="$1"
  awk '/^Wrote / { sub(/^Wrote /, ""); print; exit }' "$output_file"
}

source_dir="$tmp_root/source"
mkdir -p "$source_dir/sub"

printf 'compact vendor/object leads: 810e 01000850\n' > "$source_dir/PacketLog.txt"
printf 'spaced selector lead: 01 00 0c 76\n' >> "$source_dir/PacketLog.txt"
printf 'selector ff compact vendor family: 014300000000000000e02d810d228920\n' >> "$source_dir/PacketLog.txt"
printf 'selector ff continuation shapes: 00 41 00 ff 00 00 00 00 b0 74 and 00 00 00 ff 00 00 00 00 1b ee\n' >> "$source_dir/PacketLog.txt"
printf 'interaction lead: 80 c8\n' > "$source_dir/sub/old.log"
printf 'pipe path object lead: 01 00 08 67\n' > "$source_dir/odd|name.txt"
printf 'password = hunter2\n' > "$source_dir/secret.txt"
printf '\000\377\000\377' > "$source_dir/binary.bin"
packet_log_size="$(wc -c < "$source_dir/PacketLog.txt" | tr -d '[:space:]')"
old_log_size="$(wc -c < "$source_dir/sub/old.log" | tr -d '[:space:]')"
odd_file_size="$(wc -c < "$source_dir/odd|name.txt" | tr -d '[:space:]')"
packet_log_hash="$(sha256_file "$source_dir/PacketLog.txt")"
old_log_hash="$(sha256_file "$source_dir/sub/old.log")"
odd_file_hash="$(sha256_file "$source_dir/odd|name.txt")"

triage_out="$tmp_root/triage.md"
"$repo_root/scripts/triage-packet-capture.sh" \
  --source "$source_dir" \
  --out "$triage_out" \
  --max-matches 3 >/dev/null

assert_contains "$triage_out" 'PacketLog.txt:1:compact vendor/object leads: 810e 01000850'
assert_contains "$triage_out" 'PacketLog.txt:2:spaced selector lead: 01 00 0c 76'
assert_contains "$triage_out" 'PacketLog.txt:3:selector ff compact vendor family: 014300000000000000e02d810d228920'
assert_contains "$triage_out" 'PacketLog.txt:4:selector ff continuation shapes: 00 41 00 ff 00 00 00 00 b0 74 and 00 00 00 ff 00 00 00 00 1b ee'
assert_contains "$triage_out" 'sub/old.log:1:interaction lead: 80 c8'

collect_source="$tmp_root/collect-source"
mkdir -p "$collect_source"
printf 'collect packet lead: 810e 01000850\n' > "$collect_source/PacketLog.txt"
printf 'password = hunter2\n' > "$collect_source/UnknownRPC.txt"
collect_packet_hash="$(sha256_file "$collect_source/PacketLog.txt")"

collect_out="$tmp_root/collect.out"
"$repo_root/scripts/collect-packet-capture.sh" \
  --source "$collect_source" \
  --out "$tmp_root/collected" \
  --context "collector smoke test" \
  --character "Smoke|Char" \
  --protocol "CR2" \
  --location "Richland / Mara C / 1,2,3" \
  --actions "vendor open then buy" \
  --objects "vendor static 888143880 | object 123" \
  --items "GoID 123 slot 4 price 100" \
  --cash "1000 -> 900" \
  --inventory "slot 4 empty -> item" \
  --state "health unchanged" \
  --missing "no failures" > "$collect_out"

collect_archive="$(capture_archive_path "$collect_out")"
assert_file "$collect_archive"

collect_extract="$tmp_root/collect-extract"
mkdir -p "$collect_extract"
tar -xzf "$collect_archive" -C "$collect_extract"
collect_bundle="$(find "$collect_extract" -mindepth 1 -maxdepth 1 -type d | head -n 1)"

assert_file "$collect_bundle/capture-context.txt"
assert_file "$collect_bundle/capture-file-manifest.md"
assert_file "$collect_bundle/skipped-files.md"
assert_file "$collect_bundle/TRIAGE.md"
assert_contains "$collect_bundle/capture-context.txt" 'Context: collector smoke test'
assert_contains "$collect_bundle/capture-context.txt" 'Character handle: Smoke|Char'
assert_contains "$collect_bundle/capture-context.txt" 'CR1 or CR2: CR2'
assert_contains "$collect_bundle/capture-context.txt" 'Action sequence: vendor open then buy'
assert_contains "$collect_bundle/capture-context.txt" 'Cash before/after: 1000 -> 900'
assert_contains "$collect_bundle/capture-file-manifest.md" '| Log name | Source path | Bundled path | Bytes | SHA-256 |'
assert_contains "$collect_bundle/capture-file-manifest.md" '| Character handle | `Smoke\|Char` |'
assert_contains "$collect_bundle/capture-file-manifest.md" '| Protocol | `CR2` |'
assert_contains "$collect_bundle/capture-file-manifest.md" '| Actions | `vendor open then buy` |'
assert_contains "$collect_bundle/capture-file-manifest.md" '| Objects | `vendor static 888143880 \| object 123` |'
assert_contains "$collect_bundle/capture-file-manifest.md" "\`PacketLog.txt\`"
assert_contains "$collect_bundle/capture-file-manifest.md" "\`$collect_packet_hash\`"
assert_contains "$collect_bundle/skipped-files.md" 'possible secret marker; review manually before bundling'
assert_contains "$collect_bundle/TRIAGE.md" 'CLIENT_VENDOR_BUY candidate'

dir_import_out="$tmp_root/import-dir.out"
"$repo_root/scripts/import-packet-capture.sh" \
  --source "$source_dir" \
  --label smoke-dir \
  --sender smoke \
  --provenance test \
  --context "directory smoke test" \
  --out-root "$tmp_root/out" > "$dir_import_out"

dir_import="$(capture_import_dir "$dir_import_out")"
assert_file "$dir_import/SOURCE.md"
assert_file "$dir_import/SKIPPED.md"
assert_file "$dir_import/TRIAGE.md"
assert_file "$dir_import/logs/PacketLog.txt"
assert_file "$dir_import/logs/sub_old.log.txt"
assert_file "$dir_import/logs/odd_name.txt"
assert_contains "$dir_import/SOURCE.md" 'Source kind: `directory`'
assert_contains "$dir_import/SOURCE.md" '| Original path | Imported path | Bytes | SHA-256 |'
assert_contains "$dir_import/SOURCE.md" "| \`PacketLog.txt\` | \`logs/PacketLog.txt\` | $packet_log_size | \`$packet_log_hash\` |"
assert_contains "$dir_import/SOURCE.md" "| \`sub/old.log\` | \`logs/sub_old.log.txt\` | $old_log_size | \`$old_log_hash\` |"
assert_contains "$dir_import/SOURCE.md" "| \`odd\\|name.txt\` | \`logs/odd_name.txt\` | $odd_file_size | \`$odd_file_hash\` |"
assert_contains "$dir_import/SKIPPED.md" '| `secret.txt` | possible secret marker; review manually before import |'
assert_contains "$dir_import/SKIPPED.md" '| `binary.bin` | binary or non-text content |'
assert_contains "$dir_import/TRIAGE.md" 'Header-like selector 50'

dry_run_root="$tmp_root/dry-run-out"
dry_import_out="$tmp_root/import-dry-run.out"
"$repo_root/scripts/import-packet-capture.sh" \
  --source "$source_dir" \
  --label smoke-preview \
  --sender smoke \
  --provenance test \
  --context 'preview | context `tick`' \
  --out-root "$dry_run_root" \
  --dry-run > "$dry_import_out"

dry_import="$(capture_import_dir "$dry_import_out")"
assert_file "$dry_import/SOURCE.md"
assert_file "$dry_import/SKIPPED.md"
assert_file "$dry_import/TRIAGE.md"
assert_file "$dry_import/logs/odd_name.txt"
assert_contains "$dry_import/SOURCE.md" '# Packet Capture Source Preview'
assert_contains "$dry_import/SOURCE.md" '- Dry-run: `1`'
assert_contains "$dry_import/SOURCE.md" '- Context: <code>preview \| context `tick`</code>'
assert_contains "$dry_import_out" "Dry-run only; no files were staged under $dry_run_root."
if [[ -e "$dry_run_root" ]]; then
  echo "Dry-run unexpectedly staged files under $dry_run_root" >&2
  exit 1
fi

zip_path="$tmp_root/capture.zip"
(
  cd "$source_dir"
  zip -qr "$zip_path" .
)
zip_hash="$(sha256_file "$zip_path")"

zip_triage_out="$tmp_root/triage-zip.md"
"$repo_root/scripts/triage-packet-capture.sh" \
  --source "$zip_path" \
  --out "$zip_triage_out" \
  --max-matches 3 >/dev/null

assert_contains "$zip_triage_out" 'Source kind: `archive`'
assert_contains "$zip_triage_out" 'PacketLog.txt:1:compact vendor/object leads: 810e 01000850'
assert_contains "$zip_triage_out" 'PacketLog.txt:3:selector ff compact vendor family: 014300000000000000e02d810d228920'
assert_contains "$zip_triage_out" 'sub/old.log:1:interaction lead: 80 c8'

zip_import_out="$tmp_root/import-zip.out"
"$repo_root/scripts/import-packet-capture.sh" \
  --source "$zip_path" \
  --label smoke-zip \
  --sender smoke \
  --provenance test \
  --context "zip archive smoke test" \
  --out-root "$tmp_root/out" > "$zip_import_out"

zip_import="$(capture_import_dir "$zip_import_out")"
assert_file "$zip_import/SOURCE.md"
assert_file "$zip_import/SKIPPED.md"
assert_file "$zip_import/TRIAGE.md"
assert_file "$zip_import/logs/PacketLog.txt"
assert_file "$zip_import/logs/sub_old.log.txt"
assert_contains "$zip_import/SOURCE.md" 'Source kind: `archive`'
assert_contains "$zip_import/SOURCE.md" "Source archive SHA-256: \`$zip_hash\`"
assert_contains "$zip_import/SOURCE.md" 'Scanned files from: `temporary extraction of'
assert_contains "$zip_import/SOURCE.md" "| \`PacketLog.txt\` | \`logs/PacketLog.txt\` | $packet_log_size | \`$packet_log_hash\` |"
assert_contains "$zip_import/SOURCE.md" '## Duplicate Hash Leads'
assert_contains "$zip_import/SOURCE.md" "| imported file | \`PacketLog.txt\` | \`logs/PacketLog.txt\` | \`$packet_log_hash\` |"
assert_contains "$zip_import/SOURCE.md" "| imported file | \`sub/old.log\` | \`logs/sub_old.log.txt\` | \`$old_log_hash\` |"
assert_contains "$zip_import/TRIAGE.md" 'Header-like selector 76'

tar_path="$tmp_root/capture.tar.gz"
tar -C "$source_dir" -czf "$tar_path" .
tar_hash="$(sha256_file "$tar_path")"

tar_import_out="$tmp_root/import-tar.out"
"$repo_root/scripts/import-packet-capture.sh" \
  --source "$tar_path" \
  --label smoke-tar \
  --sender smoke \
  --provenance test \
  --context "tar archive smoke test" \
  --out-root "$tmp_root/out" > "$tar_import_out"

tar_import="$(capture_import_dir "$tar_import_out")"
assert_file "$tar_import/SOURCE.md"
assert_file "$tar_import/TRIAGE.md"
assert_file "$tar_import/logs/PacketLog.txt"
assert_contains "$tar_import/SOURCE.md" 'Source kind: `archive`'
assert_contains "$tar_import/SOURCE.md" "Source archive SHA-256: \`$tar_hash\`"
assert_contains "$tar_import/TRIAGE.md" 'CLIENT_VENDOR_BUY candidate'

discord_json="$tmp_root/discord.json"
cat > "$discord_json" <<'JSON'
{
  "guild": {"name": "The Matrix Community"},
  "channel": {"id": "137010908307259393", "name": "emulation"},
  "exportedAt": "2025-11-14T02:44:31.7472279+01:00",
  "messageCount": 2,
  "messages": [
    {
      "id": "615313596406169619",
      "timestamp": "2019-08-26T00:36:33.357+02:00",
      "author": {"name": "rajkosto"},
      "content": "collector RPC packet log targets 8113 8114 8115"
    },
    {
      "id": "623784469685600257",
      "timestamp": "2019-09-18T09:36:46.928+02:00",
      "author": {"name": "neowhoru"},
      "content": "vendor_items.csv coverage came from opened vendor packet logs"
    }
  ]
}
JSON

discord_report="$tmp_root/discord-leads.md"
"$repo_root/scripts/mine-discord-resource-leads.sh" \
  --source "$discord_json" \
  --json-url "https://example.invalid/export.json" \
  --html-url "https://example.invalid/export.html" \
  --out "$discord_report" >/dev/null

assert_file "$discord_report"
assert_contains "$discord_report" '| Public JSON | <https://example.invalid/export.json> |'
assert_contains "$discord_report" '[615313596406169619](https://example.invalid/export.html#chatlog__message-container-615313596406169619)'
assert_contains "$discord_report" '| `8113` | 1 |'
assert_contains "$discord_report" 'Collector RPC packets 8113, 8114, and 8115 were explicitly requested.'
assert_contains "$discord_report" 'vendor_items.csv was described as parsed from logs and incomplete because only opened vendors could be parsed.'
assert_contains "$discord_report" 'The candidate tables intentionally omit message excerpts'

discord_uncurated_json="$tmp_root/discord-uncurated.json"
cat > "$discord_uncurated_json" <<'JSON'
{
  "guild": {"name": "The Matrix Community"},
  "channel": {"id": "137006126100250624", "name": "mara-central"},
  "exportedAt": "2025-11-14T01:59:46.2760909+01:00",
  "messageCount": 1,
  "messages": [
    {
      "id": "1",
      "timestamp": "2021-01-01T00:00:00+01:00",
      "author": {"name": "tester"},
      "content": "old packet log and source code archive"
    }
  ]
}
JSON

discord_uncurated_report="$tmp_root/discord-uncurated-leads.md"
"$repo_root/scripts/mine-discord-resource-leads.sh" \
  --source "$discord_uncurated_json" \
  --json-url "https://example.invalid/mara.json" \
  --html-url "https://example.invalid/mara.html" \
  --out "$discord_uncurated_report" >/dev/null

assert_file "$discord_uncurated_report"
assert_contains "$discord_uncurated_report" '[1](https://example.invalid/mara.html#chatlog__message-container-1)'
assert_contains "$discord_uncurated_report" '| `packet log` | 1 |'
assert_not_contains "$discord_uncurated_report" 'Curated Packet-Resource Probes'
assert_not_contains "$discord_uncurated_report" '| missing |'

discord_mara_curated_json="$tmp_root/discord-mara-curated.json"
cat > "$discord_mara_curated_json" <<'JSON'
{
  "guild": {"name": "The Matrix Community"},
  "channel": {"id": "137006126100250624", "name": "mara-central"},
  "exportedAt": "2025-11-14T01:59:46.2760909+01:00",
  "messageCount": 1,
  "messages": [
    {
      "id": "1009119905561452674",
      "timestamp": "2022-08-16T17:22:14.027+02:00",
      "author": {"name": "pahefu"},
      "content": "Packet logs are public on github already tho"
    }
  ]
}
JSON

discord_mara_curated_report="$tmp_root/discord-mara-curated-leads.md"
"$repo_root/scripts/mine-discord-resource-leads.sh" \
  --source "$discord_mara_curated_json" \
  --json-url "https://example.invalid/mara-curated.json" \
  --html-url "https://example.invalid/mara-curated.html" \
  --out "$discord_mara_curated_report" >/dev/null

assert_file "$discord_mara_curated_report"
assert_contains "$discord_mara_curated_report" 'A community member said packet logs were already public on GitHub.'
assert_contains "$discord_mara_curated_report" 'Track Vibrantic/mxo-resurgence mxopackets1 and mxopackets2 as acquired public corpus sources.'
assert_contains "$discord_mara_curated_report" '[1009119905561452674](https://example.invalid/mara-curated.html#chatlog__message-container-1009119905561452674)'

echo "Packet capture tooling smoke tests passed."
