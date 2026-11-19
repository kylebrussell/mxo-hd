# Ability audit: current evidence and historical snapshots

[ABILITY-PARITY.md](ABILITY-PARITY.md) is the preserved historical Markdown snapshot. Its table remains readable at the established path; feature PRs leave that snapshot unchanged. The tracked `research/abilities/server_coverage.json` is also a historical snapshot; feature PRs preserve it exactly from current master. The current executable audit is generated and uploaded by every CI run as `ability-audit-<commit SHA>` in the run's Artifacts section. The bundle contains current Markdown, filterable CSV and executable server coverage for every canonical module. Open the [CI runs](https://github.com/kylebrussell/mxo-hd/actions/workflows/ci.yml) for the PR's exact head or merge commit.

The tracked [CSV](ABILITY-PARITY.csv) is a checkpoint refreshed after groups of lane PRs merge. During an active group, use the exact-head CI bundle for current CSV and runtime inventory. Lane PRs commit their own evidence JSON under `research/abilities/evidence/` and include the audit delta in the PR and handoff. The generated Markdown explains the candidate scope, status meanings and source limits.

## Reproduce current evidence

```powershell
dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py --output-dir artifacts/ability-audit
```

The executable inventory, current Markdown and filterable CSV go under ignored `artifacts/ability-audit/`. The generator uses the fresh inventory and reports an error if it is absent. Use this artifact-only command in lane PRs.

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

To compare the freshly built audit with the reviewed base, pass the base commit's
CSV from its CI artifact. The tracked checkpoint is also suitable when it matches
the reviewed base.

```powershell
dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py --baseline "<base-artifact-csv>" --output-dir artifacts/ability-audit
```

The CLI and generated Markdown report status totals, status transitions and
remaining-item totals and deltas. Include the status transition counts in the PR
and the handoff. A baseline made before AB0 has no remaining-item counts.

The [ranked retest fixtures](ABILITY-EVIDENCE-RETESTS.md) document which stock observations can close current rows and how to run the matching headless checks.

## Port pending feature branches

1. Merge updated master into the feature branch, retaining all feature behavior, source evidence and individual manuals.
2. Take the updated master's copy of the tracked CSV and both historical snapshots when resolving conflicts. Regenerate the artifact-only bundle from the combined implementation, then review the per-package evidence and delta.
3. Commit the implementation, tests, manuals and package evidence files. Link the current exact-head audit bundle for the CSV and Markdown. Each lane PR stays independent of the next checkpoint update.
4. Port NativeQa mode configuration using [the fixture mode guide](../tools/NativeQa/Modes/README.md). Pending Writer and Zen Master branches add their own mode files after merging this structure; do not reintroduce shared Program mode switches. Compile-items and its SeedDisguiseTool/archive metadata from PR135, and Desperation from merged PR130, are already preserved here.

## Refresh the tracked CSV

After a group of lane merges, start an audit refresh from current master. Generate the audit with the merged evidence and commit just the tracked CSV in a small audit PR.

```powershell
dotnet run --project tools/AbilityAudit
python research/abilities/build_audit.py
git add docs/ABILITY-PARITY.csv
```

Use the previous checkpoint as the baseline for the refresh PR's cumulative delta. The historical Markdown and runtime inventory use the separate snapshot-only process.
