# Discord Backup Channel Scan

This note records the public MxO Discord backup channels checked for packet-format research leads. Full message bodies are intentionally not copied here; use the generated report anchors to review source context in the public HTML exports.

- Source index: `https://files.mxoemu.info/MxoDiscordBackup/`
- Scan command for a channel: `scripts/mine-discord-resource-leads.sh --source <channel.json> --json-url <channel-json-url> --html-url <channel-html-url> --out <report.md>`

## Tracked high-signal reports

| Channel | Messages | Report | Packet-log terms | Other useful terms | Reason retained |
| --- | ---: | --- | ---: | --- | --- |
| `emulation` | 34,471 | `research/community-data/discord-emulation-packet-leads.md` | 17 `packet log`, 1 `packet capture`, 3 `sniffer` | 45 `collector`, 1 each for `8113`/`8114`/`8115`, 7 `code storage`, 1 `vendor_items`, 5 `mission logs` | Best targeted evidence for collector RPCs, vendor provenance, code-storage gaps, Neo packet logs, combat material, old sniffer code, and replay/source leads. |
| `mara-central` | 97,015 | `research/community-data/discord-mara-central-packet-leads.md` | 26 `packet log`, 6 `packet capture`, 3 `captured packet`, 1 `sniffer` | 53 `collector`, 20 `mission logs`, 15 `torrent`, 20 `MxOSource`, 1 `code storage` | Broadest Matrix Online community channel; adds curated public-corpus, sniffer-provenance, NPC limitation, final-shutdown-window, combat, server-resource, tutorial-parser, packet-capture, mission-log, source, and archive leads not present in the emulation-only report. |

## Checked but not retained as separate reports

| Channel | Messages | Packet-log terms | Other terms observed | Reason not retained |
| --- | ---: | ---: | --- | --- |
| `mxo-faq` | 3 | 0 | 1 `combat` | FAQ content has no packet-log, capture, sniffer, collector, vendor, source, or archive leads. |
| `cmnty-mish-chat` | 295 | 0 | 8 `combat`, 4 `Morpheus` | Community mission chat has broad gameplay terms only, with no packet-log-specific or source/archive signal. |
| `matrix-games` | 5,063 | 0 `packet log`, 0 `packet capture` | 6 `packet`, 39 `combat`, 2 `Hardline Dreams`, 27 `Morpheus`, 5 `source code`, 2 `collector` | Mostly broad Matrix game discussion. Its source/HLD chatter is weaker than the retained `emulation` and `mara-central` leads, so tracking a full report would add noise. |

## Resulting acquisition priorities

1. Use `discord-emulation-packet-leads.md` for collector RPC `8113`/`8114`/`8115`, `vendor_items.csv`, code-storage-to-inventory, Neo packet-log archive, and `.codejunky` combat-log follow-ups.
2. Use `discord-mara-central-packet-leads.md` for public GitHub packet corpus provenance, Rajkosto sniffer provenance, older packet-log holders, NPC spawn/log limitations, combat packet notes, server-resource priority, tutorial packet-log parser leads, packet-capture, mission-log torrent, `MxOSource`, and broad archive-holder follow-ups.
3. Treat both reports as acquisition maps only. They do not replace raw packet captures, and every received file still needs triage and import review before it can become packet evidence.
