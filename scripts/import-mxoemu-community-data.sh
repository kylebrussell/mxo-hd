#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work_dir="${MXOEMU_DATA_WORKDIR:-/tmp/mxo-research/mxoemu-files}"
source_url="${MXOEMU_GOPROPS_URL:-https://files.mxoemu.info/goprops.7z}"

mkdir -p "$work_dir"
curl -fSLo "$work_dir/goprops.7z" "$source_url"
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

wc -l "$repo_root/data/vendor_prices.csv"
