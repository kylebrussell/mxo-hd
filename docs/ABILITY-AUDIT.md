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

## Track remaining work and compare a package

The generator lists open checks by type and detail in each row's `remaining` field.
A row can count toward multiple types. A reported change counts affected rows, and
a status transition counts each row once. The baseline for AB0 has 561 rows, with
544 partial, 15 cosmetic, one missing, one inaccessible and zero done.

**Done** requires every identified behavior to execute and have a test, a passing
stock-client representative in the row's family, and an empty `remaining` field.
Runtime inventory or a headless fixture alone cannot qualify a native family.
Review the evidence against the ability's shipped modifiers, callbacks and
target rules before recording an item as closed.

Use one reviewed JSON file per package in `research/abilities/evidence/` when a
check closes. Its `abilities` object is keyed by numeric ability ID. Each row may
set a `family` and provide `checks` indexed by the remaining-item type. Each
check needs specific `source`, `runtime`, and `test` references. The
`behavior-tests` entry must account for every identified behavior. The separate
`families` object is keyed by family name. Each family record gives the
`representative` ability ID, `result` of `pass`, the tested `head`, and the
published `native` evidence URL. Assign that representative to the same family
in its ability record. When recording a native pass, describe the observed scope
so a presentation-only pass cannot close unverified gameplay checks.

To compare the freshly built audit with the reviewed base, save the base CSV
before regenerating:

```powershell
$baseline = Join-Path $env:TEMP 'mxo-ability-audit-base.csv'
git show origin/master:docs/ABILITY-PARITY.csv | Set-Content -LiteralPath $baseline -Encoding utf8
dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py --baseline $baseline
```

The CLI and generated Markdown report status totals, status transitions and
remaining-item totals and deltas. Include the status transition counts in the PR
and the handoff. A baseline made before AB0 has no remaining-item counts.

## Port pending feature branches

1. Merge updated master into the feature branch, retaining all feature behavior, source evidence and individual manuals.
2. Restore both historical snapshots from updated master: `git restore --source=origin/master -- docs/ABILITY-PARITY.md research/abilities/server_coverage.json`. Regenerate the current CSV and ignored executable inventory from the combined implementation. Review the current bundle rather than editing the historical JSON to resolve a feature merge.
3. Run the executable audit and new generator. Review the fresh executable inventory and commit the current CSV plus source/manual changes. Do not commit `research/abilities/server_coverage.json` in a feature PR. Preserve honest partial/missing statuses. Upload or link the exact-head current CI audit artifact instead of committing the generated Markdown table or runtime JSON.
4. Port NativeQa mode configuration using [the fixture mode guide](../tools/NativeQa/Modes/README.md). Pending Writer and Zen Master branches add their own mode files after merging this structure; do not reintroduce shared Program mode switches. Compile-items and its SeedDisguiseTool/archive metadata from PR135, and Desperation from merged PR130, are already preserved here.

Existing pending PRs may need this one-time port. This policy does not retroactively remove their source evidence or substitute an old table for the exact-head current artifact.
