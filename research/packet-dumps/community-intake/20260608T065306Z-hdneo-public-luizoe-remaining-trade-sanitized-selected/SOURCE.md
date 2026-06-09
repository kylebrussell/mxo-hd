# Packet Capture Source

- Imported at UTC: `20260608T065306Z`
- Dry-run: `0`
- Source kind: `directory`
- Source input: `/tmp/mxo-hdneo-luizoe-remaining-trade-sanitized`
- Scanned files from: `/tmp/mxo-hdneo-luizoe-remaining-trade-sanitized`
- Sender: `hdneo public GitHub`
- Provenance: `public hdneo/mxopackets1 archive; sanitized derivative`
- Context: `Selected missing Luizoe recursion/Syntax trade-adjacent logs after archive comparison; NUL bytes stripped and connection-handshake text removed before import; recursion1 derivative and delete-character source omitted.`
- Max imported file bytes: `5242880`

## Imported Files

| Original path | Imported path | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| `mxopackets1/luizoe/any things.log` | `logs/mxopackets1_luizoe_any_things.log.txt` | 301457 | `0781ad1bd5225ce6c217ce54db4c7beaca2e3029387313b4e2851a1b4c4c6733` |
| `mxopackets1/luizoe/margins/Syntax.log` | `logs/mxopackets1_luizoe_margins_Syntax.log.txt` | 1269672 | `718c50198855ee871ba2ac4f8912a8e66fe31d32971d2ee208faf448bcb6857d` |
| `mxopackets1/luizoe/recursion2.log` | `logs/mxopackets1_luizoe_recursion2.log.txt` | 481271 | `fc6d22518ff69bd02e78bbf49c87591c6d2ff5b60db59b05e03e6c1fda83ea7d` |

## Intake Next Steps

- Review `SOURCE.md`, `SKIPPED.md`, and `TRIAGE.md` before committing imported logs.
- Preserve the original received archive outside the repository and use the SHA-256 hashes in `SOURCE.md` for duplicate/provenance checks.
- Import only reviewed text logs or text-like dumps that clear repository safety review; leave binaries and unrelated game assets out of Git.
- After review, run `scripts/packet-research.sh` to regenerate packet summaries.
