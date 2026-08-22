# Encrypted plan file format, version 1

A CashFlow Planner plan can be saved encrypted. This document specifies the format
completely enough to write an independent decryptor, because a personal finance
file you may need in ten years should never depend on one program still existing.

Reference implementations, deliberately duplicated so neither is a single point of
failure:

- `src/CashFlowPlanner.BlazorWasm/wwwroot/js/plan-crypto.js` — used by the app
- `tools/decrypt-plan.html` — standalone, offline, no dependencies, no network
- `tools/crypto-selftest.html` — the assertions both must satisfy

This document covers the **file**, the copy that leaves the machine. The browser
working copy in `localStorage` is a different problem with a different answer — a
device key, no passphrase, no KDF, and a much smaller envelope. See
[`WORKING-COPY-ENCRYPTION.md`](WORKING-COPY-ENCRYPTION.md). The two share no key
material and neither can read the other's output.

## Why not `age`

The original plan was the [`age`](https://age-encryption.org) format, so files
would be readable by the standard `age` CLI. That needs the `typage` JavaScript
library bundled with `esbuild`, and there is no Node toolchain on the machine this
was built on. The alternative — hand-vendoring `typage` plus its `@noble`
dependencies and rewriting their import specifiers — meant unaudited third-party
cryptography wired up by hand in the one code path where a mistake makes financial
data permanently unreadable.

This format uses only the browser's own Web Crypto instead. The cost is that it is
bespoke; `tools/decrypt-plan.html` and this document are how that cost is repaid.
Moving to `age` later is a format-version bump, and v1 files stay readable.

## Why the crypto lives in JavaScript

.NET 10 in WebAssembly has **no symmetric cipher**. Verified against the .NET
10.0.11 reference assemblies: `AesGcm`, `AesCcm`, `ChaCha20Poly1305`, `Aes.Create`,
`RSA.Create` and `ECDsa.Create` all carry `[UnsupportedOSPlatform("browser")]`.
`Rfc2898DeriveBytes`, `HKDF`, SHA-2, HMAC and `RandomNumberGenerator` do work — so
a key can be derived in C# but nothing can be encrypted with it. Managed crypto in
WASM is also 20–40× slower than native.

## Structure

A UTF-8 JSON document:

```json
{
  "format": "cashflowplanner-encrypted",
  "version": 1,
  "kdf": {
    "algorithm": "PBKDF2",
    "hash": "SHA-256",
    "iterations": 600000,
    "salt": "<base64, 16 bytes>"
  },
  "wrappedKey": {
    "algorithm": "AES-256-GCM",
    "iv": "<base64, 12 bytes>",
    "ciphertext": "<base64: the 32-byte data key, encrypted>"
  },
  "payload": {
    "algorithm": "AES-256-GCM",
    "iv": "<base64, 12 bytes>",
    "ciphertext": "<base64: the plan JSON, encrypted>"
  }
}
```

All binary values are standard base64 with padding. AES-GCM ciphertexts include
the 16-byte authentication tag appended, which is what the Web Crypto API produces
and consumes.

## Key hierarchy

Two levels, and the reason matters:

```
passphrase ──PBKDF2-HMAC-SHA256, 600 000 iterations──▶  KEK   (key-encryption key)
KEK        ──AES-256-GCM──▶ wraps a fresh random DEK   on every single save
DEK        ──AES-256-GCM──▶ encrypts the plan JSON
```

- **A fresh DEK per save** means an AES-GCM `(key, nonce)` pair is never reused.
  Nonce reuse is the standard way GCM is broken, and reusing one long-lived key
  across thousands of autosaves is exactly the situation where it happens.
- **The KEK is derived once per session** and cached in memory. Running PBKDF2 at
  600 000 iterations on every autosave debounce would cost ~190 ms each time and
  make the app unusable.
- The KEK is imported as **non-extractable**, so it cannot be read back out of the
  browser's key store by script.

The salt is stored in the file and stays stable for that file, so the cached KEK
remains valid across saves. A brand-new encrypted plan gets a fresh salt.

## Authenticated header

Both AES-GCM operations bind the header as **additional authenticated data**. The
AAD is the UTF-8 bytes of these six fields joined with `|`, in exactly this order:

```
format | version | kdf.algorithm | kdf.hash | kdf.iterations | kdf.salt
```

For the example above:

```
cashflowplanner-encrypted|1|PBKDF2|SHA-256|600000|<salt-base64>
```

Any edit to the header — lowering `iterations` to weaken the KDF, swapping the
salt — makes decryption fail rather than silently succeed with weaker parameters.
An independent decryptor **must** reproduce this string byte for byte.

## Decrypting, step by step

1. Parse the JSON. Reject unless `format` is `cashflowplanner-encrypted`.
   Reject if `version` is greater than you understand.
2. Build the AAD string above and encode it as UTF-8.
3. Import the passphrase as PBKDF2 key material and derive a 256-bit AES-GCM key
   using `kdf.salt`, `kdf.iterations` and `kdf.hash`.
4. AES-GCM-decrypt `wrappedKey.ciphertext` with that key, `wrappedKey.iv` and the
   AAD. Failure here means a wrong passphrase or an altered header. The result is
   the raw 32-byte DEK.
5. Import the DEK as an AES-GCM key.
6. AES-GCM-decrypt `payload.ciphertext` with the DEK, `payload.iv` and the same
   AAD. The result is the plan JSON as UTF-8.

## What this does and does not protect

**Protects** the plan file at rest. Someone who obtains the file — from a synced
folder, a backup, a lost laptop, a cloud provider — cannot read it or alter it
undetectably without the passphrase.

**Does not protect** against anything with access to the running browser session
while the plan is unlocked, malware on the machine, or a forgotten passphrase.
There is no recovery mechanism and no backdoor: lose the passphrase and the file
is gone. That is the point, and the app says so before encrypting anything.

**Does not hide** metadata. The file's size, timestamp, and the fact that it is a
CashFlow Planner plan are all visible.

## Changing the format

Bump `version` and keep the ability to read older versions. `iterations` is read
from the file rather than assumed, so the work factor can be raised for new files
without breaking existing ones.
