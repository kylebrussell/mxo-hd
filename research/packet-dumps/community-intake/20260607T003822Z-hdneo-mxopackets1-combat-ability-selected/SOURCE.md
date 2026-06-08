# Packet Capture Source

- Imported at UTC: `20260607T003822Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hdneo-combat-ability`
- Scanned files from: `/tmp/mxo-hdneo-combat-ability`
- Sender: `hdneo/mxopackets1`
- Provenance: `public GitHub Matrix Online packet archive`
- Context: `public combat, ability load/unload, hyperspeed, and individual ability-use logs selected from hdneo/mxopackets1 for combat/object-state packet research`
- Max imported file bytes: `5242880`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `abilities/useoverclocked.log` | `logs/abilities_useoverclocked.log.txt` | 1497 | `f3f6c534673c4813b26c3969ec7b7dff0aaed3c1fc9f67415be986e65a848230` |
| `abilities/cast_repairRSI1.0_tomyself.log` | `logs/abilities_cast_repairRSI1.0_tomyself.log.txt` | 3795 | `88ad2d956cc32f92badd981ffdc626d609dcbb55539c8ee12369944f8da43a6b` |
| `abilities/use_plague_zone_2_0_at_corrupted.log` | `logs/abilities_use_plague_zone_2_0_at_corrupted.log.txt` | 11508 | `e8c70a9cf854be5be45edf3bd80297195fb17ff038d756b0c185c2a6b65467b6` |
| `abilities/usecombathacking.log` | `logs/abilities_usecombathacking.log.txt` | 2924 | `18c871406f7d78458d85213d9fa6fd75d4c2fda812b70a41d506541f1ab00533` |
| `abilities/use_code_nuke_30_at_corrupted.log` | `logs/abilities_use_code_nuke_30_at_corrupted.log.txt` | 15498 | `bbb789306c3f1fa93c12401ad3bebd9016c625cad3a8d4210005e7d5475dc8e9` |
| `abilities/use_destroyer_at_corrupted.log` | `logs/abilities_use_destroyer_at_corrupted.log.txt` | 20444 | `1bd5e80c873da8a75364c3243bf6c276670b1a514426ec71e0ec94100d11b675` |
| `actions/Vector_loadandunloadhyperspeed.log` | `logs/actions_Vector_loadandunloadhyperspeed.log.txt` | 18983 | `c9819019f609667c081b945450f0c949dbb1eb2a66a2cccd308df9867dac1eb4` |
| `actions/combat.log` | `logs/actions_combat.log.txt` | 1198907 | `a572741026b1e02fce23fd7cf91c101b2f5693c15abb54d390348c2de32e3cd8` |
| `actions/combat_marketplace_abilitychangecoder.log` | `logs/actions_combat_marketplace_abilitychangecoder.log.txt` | 4066224 | `74529c7fcfe7cacf4b5435c93c32e2cf9b8aeb2a733729e50b600b26d28d1e73` |
| `actions/Vector_unload and load ability.log` | `logs/actions_Vector_unload_and_load_ability.log.txt` | 2660 | `4c4568bfaaa60e7cd493f7acf38680e96fd83b6d721f0ede5acdbb5cced23826` |
| `actions/Vector_unloadhyperspeed.log` | `logs/actions_Vector_unloadhyperspeed.log.txt` | 1766 | `9bce4e3f633bdf652b835d5b34b743a672c98020381f3d2069c96d59e7352035` |
| `actions/Vector_loadhyperspeed.log` | `logs/actions_Vector_loadhyperspeed.log.txt` | 485 | `277a1b952cf8724383f6ee61d052dd4172d7a9679b6c6adf4948f93389350355` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only text logs or text-like dumps that contain no account secrets, client binaries, private keys, or unrelated game assets.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
