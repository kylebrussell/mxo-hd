# Ability audit: current evidence and historical snapshots

[ABILITY-PARITY.md](ABILITY-PARITY.md) is the preserved historical Markdown snapshot. Its table remains readable at the established path; feature PRs leave that snapshot unchanged. The tracked `research/abilities/server_coverage.json` is also a historical snapshot; feature PRs preserve it exactly from current master. The current executable audit is generated and uploaded by every CI run as `ability-audit-<commit SHA>` in the run's Artifacts section. The bundle contains current Markdown, filterable CSV and executable server coverage for every canonical module. Open the [CI runs](https://github.com/kylebrussell/mxo-hd/actions/workflows/ci.yml) for the PR's exact head or merge commit.

The tracked [CSV](ABILITY-PARITY.csv), catalog, executable inventory generator, source evidence and individual ability manuals remain part of feature review. The current runtime inventory is the `server_coverage.json` companion inside the exact-head CI bundle. The [tracked runtime snapshot](../research/abilities/server_coverage.json) is retained for historical reference and does not represent current feature behavior. The methodology, candidate scope, partial/missing meanings and source limitations are included in each generated Markdown report. A successful generator or fixture check does not establish native gameplay parity.

## Reproduce current evidence

```powershell
dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py
```

Both commands now default to the fresh inventory under ignored `artifacts/ability-audit/`. This updates the tracked CSV and writes the current Markdown plus local CSV/coverage companions there; it leaves both historical snapshots unchanged. If the executable inventory is absent, the Python generator fails with an instruction to run the executable audit; it never silently uses the historical inventory. For a self-contained review bundle without changing tracked evidence:

```powershell
New-Item -ItemType Directory -Force artifacts/ability-audit
dotnet run --project tools/AbilityAudit -- artifacts/ability-audit/server_coverage.json
python research/abilities/build_audit.py --coverage artifacts/ability-audit/server_coverage.json --output-dir artifacts/ability-audit
```

Local report links resolve from the actual output directory. CI adds `--source-base-url "https://github.com/kylebrussell/mxo-hd/blob/$GITHUB_SHA/"`, so source and manual links are pinned to the reviewed commit even after downloading the bundle; CSV/coverage links stay local. External URLs and line anchors are preserved. CI uploads all three outputs with read-only repository access. It does not push generated commits. The optional `--update-markdown-snapshot` flag explicitly refreshes the tracked snapshot; submit historical snapshot changes in a separate snapshot-only PR based on current master. Such a PR may change either or both historical files. CI rejects a historical snapshot change mixed with any other path, including gameplay, CSV or manual changes, and rejects deletion or renaming of a snapshot.

## Port pending feature branches

1. Merge updated master into the feature branch, retaining all feature behavior, source evidence and individual manuals.
2. Restore both historical snapshots from updated master: `git restore --source=origin/master -- docs/ABILITY-PARITY.md research/abilities/server_coverage.json`. Regenerate the current CSV and ignored executable inventory from the combined implementation. Review the current bundle rather than editing the historical JSON to resolve a feature merge.
3. Run the executable audit and new generator. Review the fresh executable inventory and commit the current CSV plus source/manual changes. Do not commit `research/abilities/server_coverage.json` in a feature PR. Preserve honest partial/missing statuses. Upload or link the exact-head current CI audit artifact instead of committing the generated Markdown table or runtime JSON.
4. Port NativeQa mode configuration using [the fixture mode guide](../tools/NativeQa/Modes/README.md). Pending Writer and Zen Master branches add their own mode files after merging this structure; do not reintroduce shared Program mode switches. Compile-items and its SeedDisguiseTool/archive metadata from PR135, and Desperation from merged PR130, are already preserved here.

Existing pending PRs may need this one-time port. This policy does not retroactively remove their source evidence or substitute an old table for the exact-head current artifact.
