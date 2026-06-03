# Packet Dump Corpus

This directory contains text packet dumps used for MxO protocol research.

## mxopackets1

- Source repository: `https://github.com/Vibrantic/mxo-resurgence`
- Source commit inspected: `862d4ad470f96f8265908d0db970d13ddd57335c`
- Source path: `packets_and_assets/mxopackets1-master/mxopackets1-master`
- Imported files: `.txt` and text-like `.log*` packet dumps
- Files imported: 83
- Approximate size: 6.1 MB

The source README credits packets from HD_Neo, entilsar, medanon, sonyblack, and luzio, and credits Rajkosto for the sniffer. Binary images, game assets, and client/key material were intentionally not imported. The `.log*` files include encrypted margin handshake sections and decrypted game packet sections; the packet research tool filters for hex dump lines and protocol markers when summarizing them.

Run `scripts/packet-research.sh` to summarize this corpus and compare protocol 04 headers against the local RPC map.
