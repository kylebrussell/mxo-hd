# Ability audit: current evidence and historical snapshot

[ABILITY-PARITY.md](ABILITY-PARITY.md) is the preserved historical Markdown snapshot. Its table remains readable at the established path; feature PRs leave that snapshot unchanged. The current executable audit is generated and uploaded by every CI run as `ability-audit-<commit SHA>` in the run's Artifacts section. The bundle contains current Markdown, filterable CSV and executable server coverage for every canonical module. Open the [CI runs](https://github.com/kylebrussell/mxo-hd/actions/workflows/ci.yml) for the PR's exact head or merge commit.

The tracked [CSV](ABILITY-PARITY.csv), [runtime inventory](../research/abilities/server_coverage.json), catalog, source evidence and individual ability manuals remain part of feature review. The methodology, candidate scope, partial/missing meanings and source limitations are included in each generated Markdown report. A successful generator or fixture check does not establish native gameplay parity.

## Reproduce current evidence

```powershell
dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py
```

This updates the tracked CSV from the executable inventory and writes current Markdown plus local CSV/coverage companions to ignored `artifacts/ability-audit/`; it leaves the tracked Markdown snapshot unchanged. For a self-contained review bundle without changing tracked evidence:

```powershell
New-Item -ItemType Directory -Force artifacts/ability-audit
dotnet run --project tools/AbilityAudit -- artifacts/ability-audit/server_coverage.json
python research/abilities/build_audit.py --coverage artifacts/ability-audit/server_coverage.json --output-dir artifacts/ability-audit
```

Local report links resolve from the actual output directory. CI adds `--source-base-url "https://github.com/kylebrussell/mxo-hd/blob/$GITHUB_SHA/"`, so source and manual links are pinned to the reviewed commit even after downloading the bundle; CSV/coverage links stay local. External URLs and line anchors are preserved. CI uploads all three outputs with read-only repository access. It does not push generated commits. The optional `--update-markdown-snapshot` flag explicitly refreshes the tracked snapshot; submit that file alone in a separate docs-only PR based on current master. CI rejects a snapshot change mixed with any other path, including gameplay or manual changes.

## Port pending feature branches

1. Merge updated master into the feature branch, retaining all feature behavior, source evidence and individual manuals.
2. Restore only `docs/ABILITY-PARITY.md` from updated master: `git restore --source=origin/master -- docs/ABILITY-PARITY.md`. Resolve CSV and executable inventory by regenerating from the combined implementation; never discard their evidence to make a merge clean.
3. Run the executable audit and new generator. Review and commit current CSV/source coverage and source/manual changes. Preserve honest partial/missing statuses. Upload or link the exact-head current CI audit artifact instead of committing the generated Markdown table.
4. Port NativeQa mode configuration using [the fixture mode guide](../tools/NativeQa/Modes/README.md). Writer, Zen Master and Desperation branches add their own mode files after merging this structure; do not reintroduce shared Program mode switches. Compile-items and its SeedDisguiseTool/archive metadata from PR135 are already preserved here.

Existing pending PRs may need this one-time port. This policy does not retroactively remove their source evidence or substitute an old table for the exact-head current artifact.
