#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

rajko_root="${RAJKO_MXOEMU_ROOT:-/tmp/mxo-research/rajkosto-mxoemu}"
tasteewheat_root="${TASTEEWHEAT_MXOEMU_ROOT:-/tmp/mxo-research/tasteewheat-mxoemu1}"
args=(
  --repo "$repo_root"
  --packet-dumps "$repo_root/research/packet-dumps"
  --json "$repo_root/docs/generated/packet-research-summary.json"
  --markdown "$repo_root/docs/generated/packet-research-summary.md"
)

if [[ -d "$rajko_root" ]]; then
  args+=(--rajko "$rajko_root")
fi

if [[ -d "$tasteewheat_root" ]]; then
  args+=(--external-source "$tasteewheat_root")
fi

dotnet run --project "$repo_root/tools/PacketResearch/PacketResearch.csproj" -- "${args[@]}"
