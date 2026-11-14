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
