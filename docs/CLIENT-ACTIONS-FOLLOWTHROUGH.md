# C1 ignored-action primary follow-through

This is a research and diagnostic fixture change. No ignored request gains a gameplay handler; action counts and retail-parity classifications do not change. Baseline master7c2a9b8991ce717deaea3e4127eb34facff95d3b includes the existing P7 work. PR144 Concentration2de4ce17 was separately read: its normal timed-cast generation/cancel path must not be replaced by client UserData completion.

## Newly identified commands

The reviewed client.dll SHA256 is `cb5ffcc6bb046fc01e9b10cd8e0c5aa86a82ce8a6a50549c0af6b5c55f79cf3a`. Addresses are image VAs. Archive resource `resource/string_tables/stringtable_client.sta` SHA256 is `9640747067a1f0b05c81e2a966ae2897822b5684d03905fb2ea3eb7312770d24`.

- **3b is /targetdamage.** Slash dispatcher62258267 compares stringID600000d3 and jumps62259838; that leaf calls no-field constructor62133600 and send6214c700. String600000d3 is exactly `/targetdamage`. Another sender is6216de40. Naming the request does not establish whether it toggles reporting, what damage it reports or any response layout; no combat effect is inferred.
- **8155 is /stuck.** Dispatcher62258116 compares3a000052 and short-jumps6225818e, which calls62246b40. String3a000052 is exactly `/stuck`. That helper requires the current actor and calls local predicate62605da0. If false it displays3a000055, `You do not appear to be stuck, please contact customer care.` If true it constructs8155 via622420c0 and sends6214c700. The local predicate calls actor virtual slot+d8 and62605af0; this is not a recovered server stuck test or safe destination. No teleport, cooldown, privilege or terrain-recovery policy is invented.
- **81aa is an interrupted-completion report.** Routine620ae840 allocates7 bytes, writes UserData from its second argument at raw2 and the byte at global629e3b38 at raw6, then sends. It does not itself establish a server ability identity, callback registration, effect authority or cancellation ownership. The distinct8191 registered callback path and PR144's server-owned normal cast guards remain untouched.

Reproduce the byte slices and archived string identities, without launching/loading the client:

```powershell
python research/client-actions/ignored_followthrough.py C:/Users/russe/Developer/mxo-client/MxO/client.dll --client-tools C:/Users/russe/Developer/mxo-research/tools/client-data
python -m unittest discover -s research/client-actions -p 'test_*.py'
```

The hash-checked extractor uses standard-library PE address mapping. The optional archive reader is the existing read-only client-data tool; the client is never executed. Generated evidence belongs in ignored artifacts or an owned Temp file. The strict decoder takes a **standalone RPC**, rejects all extra/truncated bytes and unknown records, and returns only raw proven fields. Range values are raw32-bit words, not a claim about signed semantics or units. Interrupted remains the raw byte. No field grants authority.

## Remaining gates

8156 still has thirteen no-field view-manager send sites, with no established purpose or answer. Time8137 still carries imported ticks from the common UI timer, not a proven `/time` contract; no clock answer is guessed. Range8146 still parses two integer words without established units, controlled subsystem or server authorization. Hovercraft8089 transaction meanings/owned backend/economy, construct80f2/f3 ownership registry,80f6 instance/key/cost lifecycle, and80ef defence consumer remain the existing P7 gates. See IGNORED-CLIENT-ACTIONS.md for the original bounded layouts; this PR does not repeat or claim to close those backend investigations.

A bounded retail corpus probe covered280files/packet headers and counted raw two-byte opcode candidates by direction. Candidate substrings are not attributed RPCs or request/answer links; bare3b is excluded due to ambiguity. Full local counts/examples are in Temp/mxo-c1-capture-candidates-20261112.json (376143 headers). The lone C2S8155 candidate is movement data, not an RPC (ProxyM1_m1_packets.log.txt:2992). Two8156 candidates are exact9-byte no-field request frames; AgentsAndIntPortingSyncHardline:6421 has `02 04 01 00 20 01 02 81 56`, followed by effect/state updates, and ZombiesPortingEtc:33069 supplies the second. They do not establish a dedicated reply or mutation contract.

## Diagnostic fixture and Claude-only native one-pass

Discovered mode `client-actions-research` starts at Mara23828/13984 with abilities2/17. It has no SQL loadout mutations, dev/GM authority, forced rolls, health hold or environment override. This fixture is for recording current UI and packet behavior, not claiming newly implemented parity.

1. Verify exact PR head, read-only build-info and fixture-plan. Start the diagnostic mode through Claude's normal exclusive native workflow.
2. Open Chat and type `/targetdamage` once. Record whether native sends3b and any visible feedback; this server currently ignores it. Do not describe absent feedback as successful target-damage support.
3. While standing safely at Mara, type `/stuck` once. Expected conditional local path is the not-stuck/contact-care message and possibly no8155 request. Record actual behavior. Do not deliberately trap the character or expect a teleport; no server recovery handler was added.
4. Type an ordinary local chat line and confirm the session remains usable. Do not invoke guessed hovercraft/construct requests through undocumented UI. `/time` and `/range` are historical guesses, not asserted native commands here.

Fresh protocol canary sends ten standalone native-shaped ignored records, checks a known814d echo after each, and verifies ordinary area chat reaches a second player. It checks stored coordinates/cash/item totals, no world-transition packets and no client parser problems. Unchanged stored coordinates do not prove all in-memory state; no gameplay/no-op compatibility conclusion is expanded beyond those checks. An initial fixture wrongly expected area chat to echo to its sender; that failure is retained and corrected using a real observer. Native behavior remains pending Claude.

Validation: eight strict decoder tests and the diagnostic-mode authority test pass. Corrected fresh canary PASS19.3s/exit0/parallel1; initial28.8s self-chat assertion failure retained. Both NativeQa configurations build. No production source file changed.
