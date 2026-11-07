# Real-Client Setup Plan

Goal: connect the real Matrix Online client to this server so we can test against it and record captures we control. Recent packet research keeps ending with "needs a new capture." Requests from the client have the same format whatever server it talks to, and we control the server state. That means our own experiments can answer questions the public logs can't. One example is whether the `81 11` sell request addresses a DB inventory slot or a vendor-list index.

What our own server can't give us: the format of packets the original servers sent. Those still come from old retail captures. But whether the client accepts what we send, ignores it or crashes on it is useful evidence too.

## What the server expects (verified in code, 2026-11-06)

| Piece | Actual value | Where |
| --- | --- | --- |
| Auth server | TCP **11000** | `hds/auth/AuthServer.cs:13` (hardcoded) |
| Margin server | TCP **10000** | `hds/margin/MarginServer.cs:32` (hardcoded) |
| World server | UDP **10000** | `hds/world/WorldServer.cs:134` (hardcoded) |
| World name sent to the client | `Recursion` | `worlds` table in `SQL/reality_hd.sql` |
| Test accounts | `loluser`, `loluser2`, `loluser10`, … | `users` table in `SQL/reality_hd.sql` |
| Auth signing key | Hardcoded RSA key: `privKeyXML` (1024-bit, e=17) is used to decrypt and sign. An unused `privKeyXML2` (2048-bit, e=17) is also present. | `hds/utils/encryption/MxoRSA.cs` |
| Packet logging | Set `MXO_PACKET_LOG=1` to write decrypted traffic to `PacketLog.txt` | `hds/utils/output/Output.cs:15` |

Note: the ports in `Config.xml.dist`, the README and `SETUP-macOS.md` (10000/10001/10002) do not match the code. The code ignores those config values.

## How the client finds a server

These details come from the [rajkosto/mxoemu Environment Setup wiki](https://github.com/rajkosto/mxoemu/wiki/Environment-Setup):

- The client only uses hostnames, never raw IPs. Put these in `useropts.cfg` in the client folder:
  ```
  UseLaunchPad = 0
  AuthServerDNSName = "localauth.mxoemu.info"
  MarginServerDNSSuffix = ".local.mxoemu.info"
  ```
- The client adds the suffix to the world name from the world list. For our `Recursion` world that gives `Recursion.local.mxoemu.info`, and the client uses that name for both margin and world traffic. This fits our server, where margin and world share port 10000 (TCP and UDP).
- **Public DNS already resolves all of these to `127.0.0.1`** (checked with `dig`: `localauth.mxoemu.info`, `Recursion.local.mxoemu.info`). If the client runs on the same Mac, no hosts-file edits are needed. A VM needs hosts entries inside the guest that point these names at the Mac's IP.
- **Key check:** the client compares the auth server's public key with one built into the executable. To use your own key, you copy `Matrix.exe` to a new name, write your public key into the copy at the offsets where the stock key sits, and launch it with `-nopatch -clone`. The wiki gives offsets `0x0B6080` and `0x0B6B38` for a 256-byte `signpub.dat`. Those offsets are for an old client build, so look them up again for 7.6005. The [hsmasteryoda launcher](https://github.com/hsmasteryoda/matrix-online-launcher) does the same thing today by producing a `MatrixPrivate.exe`.

**Open question (first thing to settle once the client is downloaded):** is the key in our `MxoRSA.cs` the stock MxOEmu key the 7.6005 client already trusts, or does the client need patching? To check, search `matrix.exe` / `launcher.exe` for the modulus bytes of both keys in each byte order. If either is found, the stock client works as is. If not, export our public key in the client's `signpub.dat` format and patch a copy. Note that the 2010 wiki talks about a 256-byte (2048-bit) key, but our auth handler signs with the 1024-bit key.

### Key check result (2026-11-06, client `MxO_7.6005.7z`)

Ran `scripts/client-key-check.py` on the extracted binaries. The client and DLLs are kept outside the repo, in `~/Developer/mxo-client/`.

- This build has no `matrix.exe`. It ships `launcher.exe` (2008 build), `client.dll` (patched by the community in 2025), and `mxowrap.dll` (a 2025 MxOEmu "PatchMaker" DLL that hooks functions at runtime with Microsoft Detours).
- **Neither of our keys appears in any binary.** I checked raw bytes in both byte orders, DER encodings, and hex/decimal/base64 text.
- **The client's trusted key is in `launcher.exe` at the wiki's offsets:** two identical 256-byte (2048-bit) copies at `0x0B6080` and `0x0B6B38`. The string `pubkey.dat` follows right after, and the default `AuthServerDNSName` is `auth.lith.thematrixonline.net`. The key starts `c7 48 18 fd 48 dc …` and ends `… 67 9c 71 73`. Both ends are odd, so the byte order can't be told from the bytes.
- **So the stock client will reject our auth server.** We'd need a patched copy of `launcher.exe` that carries our public key.
- **Still unresolved:** the launcher holds a 2048-bit key, but `AuthHandler` signs with the 1024-bit `privKeyXML`. `privKeyXML2` (2048-bit) is defined but never used. Our `AS_GetPublicKeyReply` sends only key id 4 and no key. The client also mentions a `pubkey.dat` file, which may be where it caches the server key. Which key goes in the slot, in which byte order, and whether hds should sign with `privKeyXML2` instead are best answered by reading the launcher's verify code.
- **Resolved by decompiling `launcher.exe` in Ghidra 12.2. See below.**
- **Good news for decompiling:** none of the binaries are packed (`.text` entropy is about 6.6). `client.dll` keeps its C++ class names (RTTI) and uses Crypto++ RSA (`RSASSA_PKCS1v15_MD5_Verifier`, `RSAES_OAEP_SHA_*`).

### How the launcher trusts an auth server (decompiled, 2026-11-06)

Addresses are virtual addresses in `launcher.exe` (image base `0x400000`).

1. **Embedded root key.** `FUN_00447260` builds a verifier from `DAT_004b6b38`: `Integer(bytes, 0x100, UNSIGNED)`, i.e. a 256-byte **big-endian** modulus, with exponent `Integer(0x11)` = 17. The copy at `0x4b6080` has no code references. Patch both copies anyway.
2. **Cached server key.** On startup `FUN_00447c10` reads `pubkey.dat` (key id, E, N, and a 256-byte signature) and checks it with the same routine as step 4. `FUN_00447eb0` then sends `AS_GetPublicKeyRequest` (opcode `06`, carrying LauncherVersion and CurPublicKeyId).
3. **`AS_GetPublicKeyReply` (opcode `07`).** The field layout comes from the logger `FUN_00444e20` and handler `FUN_00447f50`, with offsets counted from the opcode byte:
   ```
   +0x00 u8   opcode 07
   +0x01 u32  Status        (must be <= 0)
   +0x05 u32  CurrentTime
   +0x09 u32  PublicKeyId   (if equal to the cached id, the key check is skipped)
   +0x0d u16  offset of N          -> [u16 len][N bytes]
   +0x0f u8   E
   +0x10 u16  offset of Signature  -> [u16 len][sig bytes]
   ```
   The fixed part is 18 bytes (`0x12`). Variable arrays use the same offset → `[u16 length][bytes]` scheme as hds's auth reply, where offsets are counted from the opcode byte.
4. **The check itself.** `FUN_00447780` rejects the reply unless `N` is exactly **128 bytes (1024-bit)**. `FUN_00468f80` then builds the message `N as 128 big-endian bytes ‖ E as 1 byte` (129 bytes) and checks a **256-byte** signature against the root key. A failure returns `0x19000004`. On success the launcher keeps `(N, E)` as the server key: `FUN_00447120` makes an OAEP encryptor for the login blob and `FUN_00447020` makes a verifier for the auth reply. `FUN_00447dd0` then writes `pubkey.dat`.
5. **Signature scheme.** RSASSA-PKCS1-v1.5 with **MD5**. The MD5 DigestInfo prefix (`30 20 30 0c 06 08 2a 86 48 86 f7 0d 02 05 05 00 04 10`) is the only one in the binary. No SHA-1 or other hash prefixes are present.

**How this maps onto hds:**
- `privKeyXML2` (2048-bit, e=17) has exactly the shape of the root key. It's the root key the HD-era patched client must have carried.
- `privKeyXML` (1024-bit, e=17) is the server key. hds already uses it for OAEP decryption of the login blob and for MD5 signing of the auth reply, matching steps 4–5.
- **Why a fresh client fails today:** hds's `ProcessGetPublicKey_Request` sends only the 18-byte fixed part, with key id 4, both offsets 0 and E 0. A client without a matching `pubkey.dat` then tries to check an empty `N`. That fails the 128-byte rule, so login aborts.

**The patch (implemented 2026-11-06):**
1. **Server.** `AuthHandler.BuildPublicKeyReply` sends a complete `AS_GetPublicKeyReply`: key id 4, E = 0x11, `N` = the `privKeyXML` modulus (128 bytes BE), and Signature = the PKCS1-v1.5-MD5 signature by `privKeyXML2` over `N ‖ 0x11` (256 bytes). The 406-byte body uses the 2-byte `0x8000 | len` TCP length prefix. `tests/hds.Tests/AuthPublicKeyReplyTests.cs` checks the reply the way the launcher reads it.
2. **Client.** Run `scripts/patch-client-launcher.py <launcher.exe>`. It writes `launcher_hd.exe` next to the input, with the `privKeyXML2` modulus at file offsets `0x0B6080` and `0x0B6B38`, and never changes the original. Launch it with `launcher_hd.exe -nopatch -clone`.
3. **Checked end to end without the client.** I ran the server and requested the key over TCP 11000 the way the launcher does. The reply passes the launcher's gates, and its signature checks out as valid against the key read from `launcher_hd.exe`, using raw RSA and the launcher's PKCS#1-MD5 encoding. Against the stock `launcher.exe` it's invalid.
4. **Server crash on macOS/Linux fixed along the way.** `WorldServer.ListenForAllClients` called the Windows-only `Socket.IOControl(SIO_UDP_CONNRESET)`. It threw right after the ready line and killed the whole process, so nothing could connect. It's now Windows-only, and `scripts/smoke-local.sh` now also checks that the process survives and port 11000 accepts connections.
5. **Risk to check during the first live login:** `MxoRSA.signWithMD5` passes an already-computed MD5 digest to `SignData`, which hashes again. If the launcher's auth-reply check hashes the raw signed data, that signature won't verify. Check this in the auth-reply handler next time the launcher is open in Ghidra, or watch for the error on the first login.

The Ghidra project is kept at `~/Developer/mxo-client/ghidra/` (project `mxo`). The headless scripts used are in the session scratchpad and can be added to `tools/` if we keep doing this.

## Windows setup (verified 2026-11-06)

**First milestone reached:** the real client logged in, passed the margin handshake and entered the world as `TheNeo` on a Windows 11 PC, with no human at the keyboard once the dev login switch below was on. Several of the notes above turned out to be wrong for client 7.6005:

1. **Get the client.** Download `https://files.mxoemu.info/MxO_7.6005.7z` (1,327,331,186 bytes, SHA-256 `619F01A3339D6B0C99866DE7EC4F0671D211D0375E8A416F7B0D8C75E58E18B2`) and extract it outside the repo, for example `~/Developer/mxo-client/MxO/`.
2. **Patch the launcher, then put it in place as `launcher.exe`.** Run `scripts/patch-client-launcher.py <client>/launcher.exe`, keep the original as `launcher_stock.exe`, and copy `launcher_hd.exe` over `launcher.exe`. The launcher copies itself to `%TEMP%\MatrixOnline.0\matrix.exe` and starts that copy with `-nopatch -clone`. If the file isn't named `launcher.exe`, or you pass `-clone` yourself, it shows "Please run the game by using launcher.exe…" and quits.
3. **Start it with `launcher.exe -nopatch -noeula`, and never with `-clone`.** `-nopatch` stops the patcher from restoring a stock launcher. `-noeula` skips the 2006 user-agreement screen, which otherwise appears on every start. Accept the agreement once yourself first.
4. **Point the client at the local server in `autoexec.cfg`, not `useropts.cfg`.** `autoexec.cfg` pulls in `useropts.cfg` by a relative path, which isn't found from the temp copy. So add these lines straight to `autoexec.cfg` (the "do not edit" warning only means the patcher overwrites it):
   ```
   UseLaunchPad = 0
   AuthServerDNSName = "localauth.mxoemu.info"
   MarginServerDNSSuffix = ".local.mxoemu.info"
   ```
   **Without this the launcher logs in to the public MxOEmu server.** The built-in `auth.lith.thematrixonline.net` now resolves to `host.rajko.info` (37.48.122.61). The launcher also loads its patch-notes page over plain HTTP from `patch.lith.thematrixonline.net`, even with `-nopatch`.
5. **Windows Defender flags the patched launcher** as `Trojan:Win32/Bearfoos.A!ml`. The stock `launcher.exe`, `client.dll` and `mxowrap.dll` scan clean, and the patched file differs from stock only in the two 256-byte key slots. The `!ml` suffix marks a machine-learning guess, not a signature match. Defender kills `matrix.exe` about a second after the margin connect, and the launcher then reports "clienthasfailed". To get past it, allow the detection and exclude the client folder and `%TEMP%\MatrixOnline.0`. That's a decision for whoever owns the machine.
6. **Run the server with `MXO_PACKET_LOG=1`.** To drive the client without typing a real password, also set `MXO_DEV_ANY_PASSWORD=1` for local use only. With it set, existing accounts log in with any password. The launcher still refuses an empty password ("Login request not sent - bad parameters?"), so type any placeholder.
7. **Flow:** log in, then CONTINUE, select a character, then SELECT. The first run shows a detail auto-detect dialog with a Continue button, then the intro videos and "Loading Character", then the world. Old Direct3D games drop out of fullscreen when they lose focus. Restore the window rather than launching `matrix.exe` again, which only shows the `-clone` warning.

Known open problem: **about half of world logins stall at "Negotiating with Servers..."**. The server gets the client's `CLIENT_SPAWN_READY` (RPC 5) and logs `[SPAWN] Queued self-spawn ...`, but the client never answers with its `02` view, and keeps sending only `05` keepalives. Across 8 logins on 2026-11-06, the first login after a server start usually worked (3 of 4), and later logins in the same run never did (0 of 3). Resending unacked object messages (see below) didn't change that. Things to look at next: per-session state the world server keeps after a client is dropped (ObjectManager entries, entity/view ids, `gameServerEntities`), and what the client expects between RPC 5 and the self-spawn. Workaround for now: restart the server, which takes about 3 minutes to load, and log in once.

Driving the client without a human: run `launcher.exe -nopatch -noeula`, focus the launcher window (it doesn't come to the front by itself), type the username, then any placeholder password with `MXO_DEV_ANY_PASSWORD=1`, then CONTINUE, select the character, and SELECT. In game, `?gotopos x y z` teleports. It multiplies by 100, so pass the database/static-object coordinates divided by 100. The chat box ignores pasted text, so type commands key by key. `Windowed = 1` stalls the client at "Registering with Servers..." and isn't usable.

Server bugs found on the way:
- **Margin: "Loading Character" never finished.** Only the MOTD reply was flagged as the last character-data reply, so with an empty `<Motd>` the client waited forever. `MarginClient.loadCharacter` now ends with the retail empty `0f` reply flagged as last (`… 10 27 0d 01 0f 10 00 00 00`).
- **World: the server crashed on the UDP session start.** `MyWorldDbAccess.SetPlayerValues` read integer columns with `GetDecimal`, which MySqlConnector rejects (`InvalidCastException`). All `GetDecimal` reads now go through `Convert.ToDecimal(dr.GetValue(n))`.
- **Auth: the plaintext password was logged.** The decrypted login blob, which contains the plaintext password, went to the console and `ServerLog.txt`. Only the MD5 part is logged now.

## Platform options on Apple Silicon

For protocol work, broken graphics are fine. We only need the client to log in, enter the world and send and receive packets.

1. **CrossOver (try first).** Paid, with a free trial. Runs on this Mac directly, so `127.0.0.1` works with no network setup. A 2021 report in the [MxOEmu "Mac via wine" thread](https://mxoemu.info/forum/showthread.php?tid=2276) says it ran on an Intel Mac: create a bottle, copy the client to C:, install DirectX 9 and DirectMusic through "Install Windows Application," then run `Launcher.exe`. rajkosto says graphics are wrong (fullbright, odd models) without DXVK. Apple Silicon is untested.
2. **Parallels with Windows 11 on ARM (fallback).** Paid. Windows on ARM emulates x86, and Parallels supports DirectX 9-class 3D. This needs hosts entries in the VM pointing at the Mac's IP on the VM network, plus macOS firewall rules for TCP 11000/10000 and UDP 10000. UTM is free but has no 3D acceleration for Windows guests, so it's likely too slow to be useful.
3. **A spare Windows PC on the LAN, if one is available.** Probably the least friction. Networking is the same as option 2.

## Steps

1. ~~**Get the client.**~~ Done. `MxO_7.6005.7z` is in `~/Developer/mxo-client/download/`, and the binaries are extracted to `~/Developer/mxo-client/bin-extract/`. All of it stays outside Git (see the intake rules in `PACKET-RESOURCE-LEADS.md`).
2. ~~**Settle the key question.**~~ Done. See the decompile notes and patch above (`scripts/client-key-check.py`, `scripts/patch-client-launcher.py`). `launcher_hd.exe` is already generated in `bin-extract/`.
3. **Set up the platform.** Start with the CrossOver bottle. Add `useropts.cfg` with the three lines above. If the patcher still runs, launch with `-nopatch -clone`.
4. **Run the server with logging:** `MXO_PACKET_LOG=1`, Docker MySQL (`scripts/smoke-local.sh` path), then `dotnet run`.
5. **First milestone: log in → world list → character select → enter world.** Record where it fails. Auth, margin and world each fail differently, and the server console shows which stage was reached.
6. **First experiments once in the world.** Run each one with `scripts/collect-packet-capture.sh`, recording cash and inventory before and after:
   - **Vendor sell addressing:** sell the same item from two different inventory slots, then the same slot at two different vendor-list positions. This settles slot vs. list index for `81 11`.
   - **View spawn/despawn:** this is the unchecked item in TODO §4. Walk away from and back to NPCs, static objects and a second client.
   - **Combat:** check the phase 1–5 combat flow against what the client actually displays.
   - **Buy/sell round trip:** check that the client shows the cash and inventory updates we already send.

## Done when

- The client reaches the world on our server, and the steps are written into `SETUP-macOS.md`.
- At least one controlled vendor-sell capture is imported through `scripts/import-packet-capture.sh`, and the slot-vs-index question is answered in `docs/TODO.md`.
