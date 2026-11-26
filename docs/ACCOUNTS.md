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

| Result | `AS_AuthReply` status |
| --- | --- |
| Unknown user or wrong password (one answer for both; unknown users run a dummy hash so timing matches) | `0x11000002` `LTAS_USERNAMEPASSWORDMISMATCH` |
| Locked out | `0x1100000D` `LTAS_RADIUSMAXATTEMPTS` |
| `account_status = 1` (banned) | `0x11000003` `LTAS_ACCOUNTISBANNED` |
| `account_status = 2` (disabled) or any other non-zero value | `0x11000007` `LTAS_ACCOUNTINACTIVE` |
| Database down | `0x1100000F` `LTAS_DATABASECONNDOWN` |

Status is checked only after the password, so a stranger can't learn that an account is banned. Error replies are now framed with
the auth TCP length prefix. Before, they went out raw, so the client never saw the code.

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

**Still open (follow-up):** margin and world use one fixed Twofish session key (`MarginEncryption.TF_Key`), so per-account keys
protect nothing on the wire yet. A random per-session key needs the world's UDP decryption to look it up per client.

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
