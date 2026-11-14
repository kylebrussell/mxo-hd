# Native QA startup regression checks

Run the actual pre-client hooks without starting the native client or taking its lock:

```powershell
dotnet tools/NativeQa/bin/Release/net8.0/NativeQa.dll --bootstrap-checks C:/path/to/mxo-hd C:/path/to/proof
```

With no trailing mode names, discovery includes every `NativeQaMode`. Trailing exact mode names select a smaller check. Build the NativeQa project explicitly in the requested configuration; the solution's Release mapping can still build NativeQa as Debug.

Each custom `AfterStartAsync` runs with its actual shared seed sequence, mode environment, disposable database and real server. Hooks use the server's allocated ports. The unchanged native path retains its ordinary fixed ports. The inherited empty hook is invoked for every remaining mode on one live baseline server, after a real helper login and world entry. This shared group checks those hooks and metadata serialization; it does not claim to execute every inactive mode's SQL profile or native UI checklist.

The normal native executor and this check both use `NativeQaFixtureSeeder`. It copies only CSR110 from the main database through the disposable harness, then writes the owned `mxotest_` schema. The check does not run native cleanup or write the main database. No TheNeo session is opened.

Every mode serializes its metadata against the executor's actual `NativeQaFixtureState` type. `state-schema-check.json` records this collision check; `state.json` describes the profile actually seeded, including the shared baseline. Mode results distinguish declared abilities from seeded abilities. Custom hooks retain helper RPCs, view lists, errors and startup artifacts. Mobius additionally requires a real inventory result: QAEnemy has exactly one rifle692 in slot105 and no item in bag slot1.

`BootstrapPassed` records hook and serialization success. The overall summary succeeds only after server/database disposal returns. Verify the recorded process IDs and exact schema absence separately before reporting READY. Keep the source and driver frozen during execution, retain failures, and do not automatically retry. This checks startup and wire behavior; native rendering and interaction remain Claude's separate gate.

On frozen tooling6f403ebb, all112 discovered hooks passed:13 custom hooks used13 separate servers, and99 inherited hooks shared one authenticated baseline. All14 exact schemas were absent and all14 owned server PIDs exited. Proof: `Temp/mxo-pr229-all-native-bootstrap-6f403ebb-20261114`; summary SHA256 `7FF7AC67E606EF2016BD58126AA76966FE815C7E43EE395CC0752C8ED6642153`. This does not claim Mobius coverage on that branch; its two modes belong to PR231's separate run.

The initial CI after extraction retained5446 passes and one failure: the character-clone source regression still looked for SQL in Program.cs. It now checks the shared seeder's complete explicit column list and verifies that Program delegates to it. The SQL statements and order are unchanged. Earlier native startup failures remain recorded in the handoff.

## PR231 Mobius integration: attributed all-mode check

Frozen integration `11f39c9a3ebd78be439a2440f6ae72399f527ead` ran the CLI once with no trailing mode names: **113 hooks passed on16 serial disposable servers**. Fifteen custom hooks each used their own actual profile/server;98 inherited empty hooks shared one authenticated baseline. This extends the historical112-mode result above with PR231's separate Mobius coverage; it does not relabel that earlier run.

Both `mobius-free-attack-negative` and `mobius-free-attack-positive` sent the normal `63 01 69` equip request, received full success `65 01 00 69 00 00`, and observed a fresh own-view `EquippedItemID692` update. The independent inventory query returned `1:0` for rifle692/count1 in slot105 and all items in slot1, for each profile. Both helper session parsers were clean. All16 exact owned schemas returned absent and all16 owned PIDs were absent after teardown.

Every server actually selected Release `hds.dll` SHA256 `08A9DB140504CC72CDD2C9A46A996CC9B7C6797593F1355EEC020FA885DA5602`. The frozen Release NativeQa CLI was `D16CDF9F60BDCAF1C35DF99C9D95515E1133D13FB077770BD8384616F4EE72AE`; Release HeadlessClient was `9442EDC56F9CEF8C229DA2C07185B672D86A0328A9AF16485D6FE84D10F1A3C1`. External proof: `Temp/mxo-pr231-shared-bootstrap-20261114`, including raw helper records, packet/server logs, preflight, stdout, exit, `matrix-proof.json` and exact cleanup verification. Summary SHA256 `3A80C99BB94CD1A2E5065521C481FE0F171F5270A83A0F640CBF2FEC9AAFB3E6`; cleanup verification SHA256 `DFC610D709499C957FF833D0A3CF38088638AF5C4194AE060151D7B5D0C165BF`.

The later character-clone assertion and documentation update change only tests/docs; runtime hds/tools/data sources and their frozen DLLs remain attributed to11f. This is pre-client hook/equipment evidence, not a new Mobius proc/freefire run or native Actions/UI PASS. Root review and final green CI remain required before READY.
