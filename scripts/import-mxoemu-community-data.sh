#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work_dir="${MXOEMU_DATA_WORKDIR:-/tmp/mxo-research/mxoemu-files}"
community_dir="$repo_root/research/community-data"
goprops_url="${MXOEMU_GOPROPS_URL:-https://files.mxoemu.info/goprops.7z}"
abilities_url="${MXOEMU_ABILITIES_URL:-https://files.mxoemu.info/abilities.txt}"
fxlisthex_url="${MXOEMU_FXLISTHEX_URL:-https://files.mxoemu.info/fxlisthex.txt}"
fxlistreal_url="${MXOEMU_FXLISTREAL_URL:-https://files.mxoemu.info/fxlistreal.txt}"
gameobjects_url="${MXOEMU_GAMEOBJECTS_URL:-https://files.mxoemu.info/gameobjects.csv}"
invitems_url="${MXOEMU_INVITEMS_URL:-https://files.mxoemu.info/invitems.txt}"
invitems_command_url="${MXOEMU_INVITEMS_COMMAND_URL:-https://files.mxoemu.info/invitems_command.txt}"

mkdir -p "$work_dir"
mkdir -p "$community_dir"

manifest_rows="$work_dir/community-data-manifest.rows"
: > "$manifest_rows"

sha256_file() {
  shasum -a 256 "$1" | awk '{print $1}'
}

line_count() {
  wc -l < "$1" | tr -d ' '
}

byte_count() {
  wc -c < "$1" | tr -d ' '
}

header_value() {
  local headers="$1"
  local key="$2"
  awk -v key="$key" '
    BEGIN { IGNORECASE = 1 }
    index($0, key ":") == 1 {
      sub(/\r$/, "", $0)
      sub(/^[^:]+:[[:space:]]*/, "", $0)
      print
      exit
    }
  ' "$headers"
}

escape_md() {
  local value="$1"
  value="${value//|/\\|}"
  printf '%s' "$value"
}

relative_path() {
  local path="$1"
  printf '%s' "${path#"$repo_root/"}"
}

download_resource() {
  local name="$1"
  local url="$2"
  local destination="$3"
  local headers="$work_dir/$name.headers"

  curl -fsSL -D "$headers" -o "$destination" "$url"
}

append_manifest_row() {
  local resource="$1"
  local url="$2"
  local imported_file="$3"
  local headers="$4"
  local note="$5"
  local last_modified
  local etag

  last_modified="$(header_value "$headers" "last-modified")"
  etag="$(header_value "$headers" "etag")"
  printf '| %s | %s | `%s` | %s | %s | `%s` | %s | `%s` | %s |\n' \
    "$(escape_md "$resource")" \
    "$(escape_md "$url")" \
    "$(relative_path "$imported_file")" \
    "$(byte_count "$imported_file")" \
    "$(line_count "$imported_file")" \
    "$(sha256_file "$imported_file")" \
    "$(escape_md "${last_modified:-unknown}")" \
    "$(escape_md "${etag:-unknown}")" \
    "$(escape_md "$note")" >> "$manifest_rows"
}

download_resource "abilities" "$abilities_url" "$community_dir/abilities.txt"
append_manifest_row "abilities.txt" "$abilities_url" "$community_dir/abilities.txt" "$work_dir/abilities.headers" "Compact ability GoID, codename, and display-name map."

download_resource "fxlisthex" "$fxlisthex_url" "$community_dir/fxlisthex.txt"
append_manifest_row "fxlisthex.txt" "$fxlisthex_url" "$community_dir/fxlisthex.txt" "$work_dir/fxlisthex.headers" "Little/big-endian FX id names used for packet payload matching."

download_resource "fxlistreal" "$fxlistreal_url" "$community_dir/fxlistreal.txt"
append_manifest_row "fxlistreal.txt" "$fxlistreal_url" "$community_dir/fxlistreal.txt" "$work_dir/fxlistreal.headers" "Single-endian FX file/name map; currently matches data/fxlistreal.txt."

download_resource "invitems" "$invitems_url" "$community_dir/invitems.txt"
append_manifest_row "invitems.txt" "$invitems_url" "$community_dir/invitems.txt" "$work_dir/invitems.headers" "Compact item GoID, codename, and display-name map."

download_resource "invitems_command" "$invitems_command_url" "$community_dir/invitems_command.txt"
append_manifest_row "invitems_command.txt" "$invitems_command_url" "$community_dir/invitems_command.txt" "$work_dir/invitems_command.headers" "MxOEmu &gib command list used for item display-name correlation."

download_resource "gameobjects" "$gameobjects_url" "$work_dir/gameobjects.csv"
perl -pe 's/\r$//; s/:\s*,/,/; s/$/,/ unless /,\s*$/;' "$work_dir/gameobjects.csv" > "$community_dir/gameobjects-public.csv"
append_manifest_row "gameobjects.csv" "$gameobjects_url" "$community_dir/gameobjects-public.csv" "$work_dir/gameobjects.headers" "Normalized from public name-colon CSV into parser-compatible name,id, rows; source SHA-256 $(sha256_file "$work_dir/gameobjects.csv")."

download_resource "goprops" "$goprops_url" "$work_dir/goprops.7z"
rm -rf "$work_dir/goprops"
mkdir -p "$work_dir/goprops"
bsdtar -xf "$work_dir/goprops.7z" -C "$work_dir/goprops"

perl -ne '
sub flush {
  return unless defined $id && defined $price;
  my $out = $id < 0 ? $id + 4294967296 : $id;
  my $sell = defined $non ? $non : "False";
  printf "%.0f,%s,%s,%s\n", $out, $code, $price, $sell;
}
BEGIN { print "go_id,codename,vendor_price,non_sellable\n"; }
if (/^TypeInfo:/) {
  flush();
  undef $id; undef $code; undef $price; undef $non;
  $id = $1 if /\047id\047:\s*(-?\d+)/;
  $code = $1 if /\047codename\047:\s*\047([^\047]+)\047/;
} elsif (/\|(?:InvItem\/)?VendorPrice\|\s*:\s*\|([^|]+)\|/) {
  $price = $1 unless defined $price;
} elsif (/\|(?:InvItem\/)?NonSellable\|\s*:\s*\|([^|]+)\|/) {
  $non = $1;
}
END { flush(); }
' "$work_dir/goprops/goprops.txt" > "$repo_root/data/vendor_prices.csv"
append_manifest_row "goprops.7z derived vendor prices" "$goprops_url" "$repo_root/data/vendor_prices.csv" "$work_dir/goprops.headers" "Derived compact VendorPrice and NonSellable table; raw goprops archive is not tracked."

{
  printf '# MxOEmu Community Data Manifest\n\n'
  printf 'Generated by `scripts/import-mxoemu-community-data.sh` from public `https://files.mxoemu.info/` resources.\n\n'
  printf '| Resource | Source URL | Imported file | Bytes | Lines | SHA-256 | Last-Modified | ETag | Notes |\n'
  printf '| --- | --- | --- | ---: | ---: | --- | --- | --- | --- |\n'
  cat "$manifest_rows"
} > "$community_dir/MANIFEST.md"

wc -l \
  "$repo_root/data/vendor_prices.csv" \
  "$community_dir/abilities.txt" \
  "$community_dir/fxlisthex.txt" \
  "$community_dir/fxlistreal.txt" \
  "$community_dir/gameobjects-public.csv" \
  "$community_dir/invitems.txt" \
  "$community_dir/invitems_command.txt" \
  "$community_dir/MANIFEST.md"
