# Native QA for Coder pets and PvP targeting

Claude runs the real client against the disposable `coder-pets-pvp` mode. Use the exact reviewed head and select only copied CSR_Tester 110 on CSR_AbilityQA. This fixture uses the ordinary level-50 hostile-server entry with a safe level of 15. QAFriend (organization 1, west 1.2 m) and QAEnemy (organization 2, east 1.2 m) enter as headless players. Both are flagged by world entry. `pvp-opponents-helpers.json` records the helper settings, self flags and mutual creations. The helpers send no pet orders or attacks during startup.

```powershell
$root = 'C:/Users/russe/Developer/mxo-wt/ability-ab5'
$head = (git -C $root rev-parse HEAD)
$qa = Join-Path $env:TEMP ('mxo-native-ab8-' + (Get-Date -Format yyyyMMdd-HHmmss))
dotnet build "$root/tools/NativeQa/NativeQa.csproj" -c Release
dotnet "$root/tools/NativeQa/bin/Release/net8.0/NativeQa.dll" $root $qa $head coder-pets-pvp
```

1. Use Actions to cast Remote Proxy 5.0 (594). Confirm a level-50 Proxy appears with its own Simulacra window. Click Stay and walk roughly 8 m away on level ground. It stays in place. Click Follow and wait a few seconds. It walks back within about 2.5 m. The order log should contain `81 7a` with ID `fe`, commands 0 and 3.
2. Use `?pet dummy` to place a non-aggro Simulacra Dummy 8 m away. In Defensive and Follow, enter ordinary interlock with the dummy. Check that the Proxy assists without an Attack order. The server logs `[PET] <sheet> Simulacrum assists against Simulacra Dummy`. Click Passive. `?pet` should report Passive and omit the automatic target. Then manually Attack the dummy and confirm a Passive pet can still attack on command.
3. End owner combat, use Deactivate and cast Logic Daemon 4.0 (616) from Actions. Repeat Stay, Follow and Defensive assist on a fresh dummy. Expect a level-50 Nuker and the same follow spacing, with ranged attacks from up to 15 m. For both summons, record the observed sheet, follow/stand movement, window command changes and assist log. Pet damage varies.
4. With a dummy nearby, set Passive and Follow before testing any player selection in the Simulacra window. If a player selection produces an Attack RPC, expect a server refusal and no switch to the dummy. If the stock client suppresses this order, record the suppression. The runtime currently supports NPC targets for pet attacks.
5. Before the player combat check, cast Remote Proxy 1.0 (27) from Actions. Fortify (20) on the selected Proxy should leave current HP 224 and raise MaxHP to 974. Out of combat and within 10 m, clear selection and cast Power Boost (25), then Charge Simulacra (603) after the cast bar completes. Require both landing logs and recast records. Power Boost logs melee/ballistic +25 for 300 s. Charge logs cost reduction 20%, regeneration increase 50% for 600 s. `?pet` should report cost 8 and power 25/25 on that Proxy. The client window does not expose this pet IS pool.
6. Return to the fixture spawn. Check that flagged QAFriend has no legal ordinary interlock action. If an attack request goes out, require a refusal and no damage. QAEnemy should allow ordinary interlock and take opening damage without a duel. If needed, press Withdraw and send the existing helper request `{"actor":"QAEnemy","action":"stop"}`. Record the native menu behavior and HP result.
7. Close the client, create the empty `$qa/stop` file from a second terminal and allow normal fixture exit. Verify `stopped.json` and lock release using the standard NativeQa checklist.

The stock Simulacra window Attack action at client.dll `620efaf0` resolves a selected object first and gates the send through `625f0790`. On success `620efbe2` writes the current-simulacrum ID, `620efbec` writes the command and `620efc40` writes the selected object's network field. This establishes the intent to attack a particular object. Server follow and assist remain approximate, and native presentation is pending. Player-target and player-versus-pet combat semantics require separate evidence.

## Headless checks

The isolated bootstrap passed with all four helpers on a newly built Release server. The independent `pvp-opponents-entry` regression passed with same-organization refusal, an opposing-organization opening hit of 50 HP (5000 to 4950), and ordinary withdrawal. The replay uses the stock `40 <view u16> <spawn u8> 00 01 6b` request with the current observer's handle. The `lastday2` capture has the direction-confirmed example `40 07 00 3a 00 01 6b` at line 13907. The normal attack opcode and handle layout apply here. Same-organization and opposing-organization behavior was exercised against the disposable server. Focused CoderPets and PvP tests passed (74).
