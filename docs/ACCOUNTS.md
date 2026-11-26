# Accounts: sign-up, password storage, per-account keys

What hds does with accounts so the server can be hosted publicly. Code lives in `hds/accounts/`, `hds/auth/AuthHandler.cs`,
`hds/auth/AuthTicketCodec.cs` and the margin and world entry points.

## What the stock client sends

Traced through `AuthHandler`, `tools/HeadlessClient/AuthLogin.cs` and the MxOEmu captures under `research/packet-dumps`:

1. `AS_GetPublicKeyRequest` / `AS_GetPublicKeyReply`: the launcher gets the 1024-bit auth server key, signed by the root key (see `docs/CLIENT-SETUP-PLAN.md`).
2. `AS_AuthRequest`: an OAEP blob under the server key with the client's random 16-byte Twofish key and the username.
3. `AS_AuthChallenge`: 16 bytes under that Twofish key. They used to be all zeros; now they're random.
4. `AS_AuthChallengeResponse`: Twofish-encrypted, holding MD5(challenge), then a u16 length and the **plaintext password** (ASCII, NUL-terminated).
5. `AS_AuthReply`: the character and world lists, the margin ticket, and the account's RSA private exponent, Twofish-encrypted with the encrypted challenge as IV.

Because the server receives the plaintext, it hashes the password itself. There's no need for `Argon2(MD5(pw))`.

## Password storage

`users.password_digest` holds `$pbkdf2-sha256$v=1$i=600000$<salt b64>$<hash b64>` (`PasswordHasher`). It's PBKDF2-HMAC-SHA256 with
600,000 iterations (the OWASP figure) and a 16-byte random salt per user. We chose it over Argon2id because it's built into .NET:
no new package and nothing native. The scheme, version and iteration count are stored with every hash. A future Argon2id `v=2` can
sit next to it: rows report `needsRehash` and are rewritten on their next login.

- **Migration.** A row with only `passwordmd5` (every row in the stock dump) is checked against MD5 on the next successful real
  login. Then `password_digest` is written and `passwordmd5`, `passwordHash` and `passwordSalt` are cleared. A failed login changes
  nothing.
- **Schema.** `AccountStore.EnsureSchema()` runs at startup, before the listeners, in the same style as the other `Ensure*`
  migrations. It adds the column when it's missing. `SQL/reality_hd.sql` has the column too, so fresh databases start with it. The
  existing unique `username` key (latin1 case-insensitive) stays.
- **Never logged.** No password, MD5 or digest is logged. The Twofish session key isn't either, since it decrypts the password.
  The log line that printed the stored MD5 on a failed login is gone.

## Login decisions (`AccountLogin`)

The steps run in this order:

1. Lockout check.
2. Case-insensitive lookup.
3. Verification: the digest, or the legacy MD5 followed by migration.
4. `account_status`.

| Result | `AS_AuthReply` status | Stock launcher dialog (string ID) |
| --- | --- | --- |
| Unknown user or wrong password (one answer for both; unknown users run a dummy hash so timing matches) | `0x11000002` `LTAS_USERNAMEPASSWORDMISMATCH` | 114 "Your username or password is incorrect. Please try again..." (225, the *CAPS LOCK* variant, when Caps Lock is on) |
| `account_status = 1` (banned), `2` (disabled) or any other non-zero value | `0x11000003` `LTAS_ACCOUNTISBANNED` | 274 "Your account has been suspended by a Game Master..." |
| Locked out | `0x1100000F` `LTAS_DATABASECONNDOWN` | 116 "The auth servers are unavailable. Please try again later." |
| Database down | `0x1100000F` `LTAS_DATABASECONNDOWN` | 116 (same) |

Status is checked only after the password, so a stranger can't learn that an account is banned.

### How the stock launcher shows a refusal

There's no failed-login capture in `research/packet-dumps`: all 11 `AS_AuthReply` messages there are successes. The format below
comes from disassembling the 7.6005 `launcher.exe` (image base `0x400000`):

- **Parsing.** The opcode-`0x0b` case of `0x448140` runs the generic `AS_AuthReply` parser (`0x443470`) before it looks at
  Status. The parser reads nine u16 array offsets at body `+0x05`, `+0x0b`, `+0x0d`, `+0x0f`, `+0x11`, `+0x13`, `+0x19`, `+0x1b` and
  `+0x1d`. It follows each non-zero offset to a `[u16 length][bytes]` array, and NUL-terminates two of the arrays in place.
- **Error path.** If Status (body `+1`) is non-zero, `0x448140` returns 3, before any signature check. The login state
  (`0x43f300`) then stores Status as the mediator's error, and the UI's error handler (`0x409305`) picks a string.
- **Status to dialog.** The UI handler maps each status to a string-table entry:

  | Status | String | Note |
  | --- | --- | --- |
  | `0x11000002` | 114, or 225 if `GetAsyncKeyState(VK_CAPITAL)` | |
  | `0x11000003` | 274 | |
  | `0x11000005` | 115 "already in use" | |
  | `0x11000007` | 128 "disabled" | Also `ShellExecute("open", InactiveAccountURL)`; the default is `http://expired.lith.thematrixonline.net/` |
  | `0x11000008`..`0x1100000D` | 213-218 | Admin and SecurID texts; `0x1100000D` `RADIUSMAXATTEMPTS` is 216, "SecurID authentication failed due to networking error" |
  | `0x1100000F` | 116 | |
  | `0x11000011` | 117 | |
  | Anything else | 222 "(%s)" with the code's name | |

hds's old error reply was 11 bytes, so the offsets at `+0x0b` onwards came from past the end of the message. Most likely the
launcher faulted while reading through them: native QA saw the window close about 3 s after submit, with no dialog and no crash event.

`AuthErrorReply` now sends the full fixed part: 33 bytes, the same length as the success header, with every offset zero. That
leaves all the arrays empty, so the launcher should reach its own dialog. This comes from static analysis and still needs native QA.

- **Disabled accounts** get the "suspended" text rather than `0x11000007`, which would open a browser at a domain we don't control.
- **Lockout** uses 116 ("try again later"). The launcher has no player-facing lockout text, and the SecurID one is misleading.

**Lockout** (`LoginThrottle`, in memory):

| Scope | Locks after | First lock | Each further failure | Cap |
| --- | --- | --- | --- | --- |
| Per account | 5 failures in 15 min | 30 s | doubles the lock | 15 min |
| Per client address | 20 failures in 15 min | 1 min | doubles the lock | 1 h |

- A success clears the account's counter but not the address's.
- Loopback is exempt from the address limit, though not the account one. Everyone behind mxo-web's proxy shares its address.
  `MXO_AUTH_THROTTLE_LOOPBACK=1` turns that exemption off.
- PBKDF2 runs behind a concurrency gate, so a login flood can't take every core.

`MXO_DEV_ANY_PASSWORD=1` still skips the password for local testing. It never migrates a row, never counts failures, and doesn't
get past a ban.

## Per-account RSA keys

The ticket's signed data carries the account's modulus at bytes 82..178 (96 bytes, after `00 11`). Margin OAEP-encrypts the session
Twofish key to that modulus with e = 17, and the client decrypts it with the private exponent d from the auth reply. So:

- **Size.** Keys are 768-bit with e = 17, because that's all the ticket layout and margin accept. The MxOEmu captures show exactly
  that, with a unique key per account, stable across sessions.
- **d reaches its owner.** The account's owner receives d on every login; that's how the protocol works. It can't be
  "server-only". It has to stay secret from everyone else. The stock dump gives every row the same key, so anyone with any account
  held everyone's d.
- **New accounts.** Sign-up generates a key with `AccountKeys.Generate()`: two 384-bit primes from `RandomNumberGenerator`,
  40-round Miller-Rabin, and a raw RSA round-trip check.
- **Existing accounts.** `dotnet hds.dll --rekey-accounts` gives every account whose key is shared or invalid a fresh one. `--all`
  rekeys every account. It's idempotent and prints counts only. It's safe on a live server: a session already past auth keeps the
  key in its ticket, and the next login gets the new one. Margin reads the key from the ticket and the world never sees it, so
  nothing else changes.

## Margin and world trust

Before this change, any client could connect to margin and claim any user id, because the ticket signature was never checked
(`TODO` in `certConnectRequest`). It could then load or delete any character by id. The world would also open a UDP session for
any character id. Now:

- Margin accepts only a 128 + 182-byte ticket whose PKCS#1-MD5 signature checks out against the auth server key, and whose expiry
  plus a grace period hasn't passed. `MXO_TICKET_GRACE_SECONDS` sets the grace; the default is 24 h. Clients and mxo-web's proxy
  keep the ticket for later margin connects, such as back to character select. The signature is what stops forgery.
- After the ticket check, margin also requires `account_status = 0`.
- Character load and delete require the character to belong to the ticket's account.
- The world's UDP init is honoured only for a character that a live, verified margin connection has loaded.

**Per-login session key.** Margin and world used to share one fixed Twofish key (now `MarginEncryption.LegacyKey`), so anyone
could read or forge any session. Now:

- **Margin.** Each margin connection generates a random 16-byte key (`MarginEncryption.ForNewSession`). It sends the key in
  `CertConnectReply`, OAEP-encrypted to the account's modulus from the verified ticket, which is what the protocol carries the
  key for. Margin encrypts its own traffic with it from then on.
- **World.** A new UDP endpoint starts under a random key nobody has. The unencrypted UDP init names the character; once a live,
  verified margin connection holds that character, the world switches the session to that connection's key
  (`MarginServer.SessionKeyForVerifiedCharacter`, then `WorldClient.UseSessionKey`). Datagrams under any other key fail the CRC
  and are dropped.
- **Relog.** A relog is a new margin connection, so it gets a new key.
- **HeadlessClient.** `MarginSession` takes the key from the cert reply and refuses the legacy key. `HeadlessPlayer` passes it to
  `WorldSession`. A `WorldSession` built without a key looks up the key of the margin session that last loaded that character
  (`MarginSession.SessionKeyFor`), so callers such as mxo-web's proxy keep working unchanged once they run a current
  HeadlessClient build. A HeadlessClient vendored before this change throws "Margin sent a session key other than the server's
  fixed one" against a current server and has to be re-vendored.

## Sign-up API

The API is hosted inside hds and is off unless `MXO_SIGNUP_HTTP=1`.

| Setting | Default | Meaning |
| --- | --- | --- |
| `MXO_SIGNUP_URL` | `http://127.0.0.1:11080` | Listen address (loopback, for a Cloudflare Tunnel). `:0` picks a free port; the server prints the real one. |
| `MXO_SIGNUP_TRUST_CF` | off | Believe `CF-Connecting-IP`, and only from a loopback peer. |
| `MXO_TURNSTILE_SECRET` | unset (no bot check) | Cloudflare Turnstile secret. When set, requests need `turnstileToken` (or a `CF-Turnstile-Response` header). Failures fail closed. |
| `MXO_SIGNUP_PER_IP_LIMIT`, `MXO_SIGNUP_GLOBAL_LIMIT`, `MXO_SIGNUP_WINDOW_SECONDS` | 10, 100, 600 | Fixed-window limits on `POST /api/signup`. |
| `MXO_SIGNUP_CORS_ORIGIN` | unset | One allowed browser origin for a sign-up page on another host. |

- `POST /api/signup` takes JSON `{"username","password","email"?,"turnstileToken"?}`. Replies:
  - `201 {"ok":true,"username"}`
  - `400 invalid_username | invalid_password | invalid_email | bad_request`
  - `409 username_taken`
  - `403 verification_failed`
  - `429 rate_limited`
  - `503 unavailable`

  `username_taken` is the only reply that says anything about existing accounts. The body is capped at 4 KB. The password is
  hashed and dropped; it's never stored or logged.
- `GET /api/health` returns `{"ok":true}`.
- **Rules** (`AccountRules`), a subset of what the launcher can type and send byte-for-byte:
  - Usernames are 3-20 letters, digits or `_`, starting with a letter. Uniqueness is case-insensitive.
  - Passwords are 10-64 printable ASCII characters (space to `~`) and can't equal the username.
  - Email is optional.

## Test fixtures and the user-28 contract

HeadlessClient scenarios, NativeQa modes and mxo-web's proxy `DevServer` create accounts by copying user 28's `publicModulus`,
`privateExponent` and `passwordmd5`, and they run with `MXO_DEV_ANY_PASSWORD=1`. **That keeps working unchanged:**

- Legacy MD5 rows stay valid.
- The shared key stays a valid key.
- Under the dev flag nothing is migrated, so user 28 keeps its `passwordmd5` in every throwaway database.

If user 28 is ever migrated (a real-password login as `loluser` in that database), copies made afterwards get `passwordmd5 = NULL`.
They still log in under `MXO_DEV_ANY_PASSWORD=1`, but not with a real password unless the copy also takes `password_digest`.

## Tests

- `tests/hds.Tests/AccountSecurityTests.cs` covers:
  - hashing, versioning and tamper checks
  - MD5 migration and status
  - lockout and backoff
  - key generation, checked against margin's .NET encryptor and HeadlessClient's decryptor
  - ticket signing and verification, forgery and expiry
  - sign-up validation
- HeadlessClient scenario `account-signup` (`dotnet run --project tools/HeadlessClient -- --filter account-signup`), with password
  checks on. It covers:
  - signing up over HTTP
  - auth, margin and world, then a relog
  - a legacy row migrating
  - margin refusing a forged ticket and the world refusing a stray UDP session
  - lockout, ban and disable
- NativeQa `account-signup`: `NativeQa <repo> <fixture-dir> <commit> account-signup`. It starts a disposable server on the real
  client's ports with password checks on and the API at `http://127.0.0.1:11080`. It signs up a new account and seeds an old-style
  MD5 account, writes both throwaway passwords to `<fixture-dir>/credentials.json`, and prints the steps. Create `<fixture-dir>/stop`
  to end it.
