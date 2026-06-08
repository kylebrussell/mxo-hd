#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

rajko_root="${RAJKO_MXOEMU_ROOT:-/tmp/mxo-research/rajkosto-mxoemu}"
tasteewheat_root="${TASTEEWHEAT_MXOEMU_ROOT:-/tmp/mxo-research/tasteewheat-mxoemu1}"
json_out="${PACKET_RESEARCH_JSON:-$repo_root/docs/generated/packet-research-summary.json}"
markdown_out="${PACKET_RESEARCH_MARKDOWN:-$repo_root/docs/generated/packet-research-summary.md}"
args=(
  --repo "$repo_root"
  --packet-dumps "$repo_root/research/packet-dumps"
  --json "$json_out"
  --markdown "$markdown_out"
)

if [[ -n "${PACKET_RESEARCH_EXTRA_DUMPS:-}" ]]; then
  IFS=':' read -r -a extra_dump_roots <<< "$PACKET_RESEARCH_EXTRA_DUMPS"
  for dump_root in "${extra_dump_roots[@]}"; do
    if [[ -z "$dump_root" ]]; then
      continue
    fi

    if [[ ! -d "$dump_root" ]]; then
      echo "PACKET_RESEARCH_EXTRA_DUMPS entry is not a directory: $dump_root" >&2
      exit 2
    fi

    args+=(--packet-dumps "$dump_root")
  done
fi

if [[ -d "$rajko_root" ]]; then
  args+=(--rajko "$rajko_root")
fi

if [[ -d "$tasteewheat_root" ]]; then
  args+=(--external-source "$tasteewheat_root")
fi

dotnet run --project "$repo_root/tools/PacketResearch/PacketResearch.csproj" -- "${args[@]}"
