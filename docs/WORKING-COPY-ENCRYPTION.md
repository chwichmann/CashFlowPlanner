# Working-copy encryption

The plan file can be encrypted with a passphrase — that is
[`ENCRYPTED-FILE-FORMAT.md`](ENCRYPTED-FILE-FORMAT.md). This document is about the
other copy: the **browser working copy**, the autosaved scratch copy in
`localStorage` that lets a reload keep the last few edits.

Until this change that copy was plaintext JSON. `localStorage` is a plain database
file in the browser profile, so account balances, salaries and debts were sitting in
something `strings` and `grep` can read.

Implementation:

- `src/CashFlowPlanner.BlazorWasm/wwwroot/js/working-copy-crypto.js` — the crypto
- `src/CashFlowPlanner.BlazorWasm/Services/WorkingCopyCipher.cs` — the .NET side
- `tools/working-copy-crypto-selftest.html` — the assertions, run in a browser

## What this protects, and what it does not

**Protects the bytes at rest in the browser profile.** A lost or stolen laptop, a
Time Machine or OneDrive backup that swept the profile up, a forensic image, another
account on a shared machine, a disk sold without wiping. This is a real threat for a
household finance app and it is the one this closes.

**Protects nothing against script running on the origin.** An XSS payload, a
malicious browser extension with host access, or someone typing into the devtools
console can `import` the same module and get the plaintext straight back — the
self-test page does exactly that. The page must be able to read its own working copy
unattended, so any key the page can reach, an attacker inside the page can reach too.
Nothing here is an XSS mitigation and it must never be described as one.

**Does not hide metadata.** The storage key names, the value length and the fact that
this is a CashFlow Planner working copy are all in the clear, as is the cache
timestamp (a wall-clock time that says nothing about the finances, and which the
navbar has to be able to show even when the key store is sick).

**Does not protect the exported file.** That is the passphrase's job.

## A device key, not the passphrase

The key is a random 256-bit AES-GCM key generated in the browser and kept in
IndexedDB as a **non-extractable `CryptoKey`**, structured-cloned into the store
rather than exported to bytes. The profile therefore holds a key handle the browser's
key store knows how to use, not 32 bytes anyone can lift out with a hex editor and
use to decrypt the `localStorage` blob offline. Exporting the key to base64 and
storing it next to the ciphertext would put the lock and the key in the same drawer.

The obvious alternative — reuse the file passphrase — was rejected deliberately. The
working copy is a scratch copy scoped to this browser that never travels: not
exported, not synced, not in any backup the user makes on purpose. Its entire job is
to be restorable on the next page load, automatically, before any UI exists to prompt
with. Hanging that restore off the passphrase would mean that a user who reloads the
tab and cannot remember (or cannot be bothered to retype) it loses their unexported
edits. That trades a real data-loss protection for a weaker privacy one. The
passphrase protects the file, which is the artefact that actually leaves the machine.

The consequence is the honest one: **the working copy is readable by whoever controls
this browser profile while it is running.** That is the same person who can open the
app and look at the dashboard.

## Format

```
cfpwc1:<base64 12-byte IV>:<base64 AES-256-GCM ciphertext+tag>
```

Deliberately not JSON. The file format can afford a self-describing header because it
has to survive a decade and an independent decryptor; this blob only has to survive
until the next save, and it competes for a ~5 MB quota where every wrapper byte is
paid for twice — once for the plan, once for the `.prev` recovery copy.

- A **fresh IV on every write**. The device key is long-lived by design, so this is
  the only thing standing between the app and GCM nonce reuse.
- The constant string `cashflowplanner-working-copy-v1` is bound in as **additional
  authenticated data**, pinning each blob to this format version.
- The AAD deliberately does *not* include the storage key name. The `.prev` slot is
  filled by moving the current ciphertext across verbatim, and binding the slot would
  force a re-encrypt on every rotation for no real gain — an attacker who can write
  to the profile can do far worse than swap two of the user's own plan copies.
- No KDF. There is no passphrase, so there is nothing to stretch.

The `.prev` recovery copy is encrypted exactly like the main copy. Because every
write gets a fresh IV, the "has anything actually changed?" test that guards the
rotation compares **plaintext**; comparing ciphertext would rotate on every save and
destroy the recovery point.

## Migration from plaintext

A returning user has readable JSON under `cashflowplanner.currentPlanJson` right now.
An envelope is recognised by its `cfpwc1:` prefix, and a plan document always starts
with `{`, so the two can never be confused. Anything without the prefix is returned
as-is and then rewritten encrypted — on the read path, not only on the next save, so
that someone who opens the app to look at last month's numbers and never types
anything does not keep a readable plan in their profile forever. The rewrite is best
effort: the plaintext stays exactly where it is until the encrypted write succeeds.

## When the crypto is not available

IndexedDB is blocked in some private-browsing modes, by some enterprise policies, and
by users who have switched site data off. In that case the working copy is written
**unencrypted**, with a one-time console warning.

That is a deliberate choice and not a lapse. The working copy is the only thing
standing between an unexported edit and a closed tab, and this project has already
paid for the lesson that a save which quietly does nothing is the worst available
outcome — it is finding P1b, the reason `PlanSaveResult` and `IBrowserPlanCache`
exist at all. Refusing to write when the key store is unavailable would reintroduce
exactly that failure: the user keeps typing, the navbar keeps looking fine, and a
reload silently discards the session. Degrading to plaintext loses no data and leaves
the user no worse off than before this feature existed. Privacy is a real gain; it is
not worth buying with someone's afternoon.

If the device key is *lost* rather than unavailable — site data cleared, which takes
IndexedDB with it while `localStorage` survives — the envelope is permanently
unreadable. That reads as "there is no working copy", which is a normal state: the
plan file on disk is the source of truth, and the next save replaces the dead value.

## Size

Base64 costs a third, and `localStorage` gives an origin about 5 MB. Measured by
`tools/working-copy-crypto-selftest.html` in Chrome, in characters, which is what a
quota counts:

| plan | plaintext | encrypted | overhead | with `.prev` | of a 5 MB quota |
| --- | --- | --- | --- | --- | --- |
| starter plan | 4.9 KiB | 6.6 KiB | +34.3% | 13.3 KiB | 0.26% |
| sample plan as shipped | 2.9 KiB | 3.9 KiB | +35.0% | 7.8 KiB | 0.15% |
| ~200 transactions, 8 accounts | 99.3 KiB | 132.4 KiB | +33.4% | 264.8 KiB | 5.17% |

The overhead is base64 and nothing else: `ceil((n + 16) / 3) * 4` characters for an
`n`-byte plan plus the 16-byte GCM tag, plus a fixed 24-character prefix and IV. It
converges on +33.3% as the plan grows.

**This is material and worth saying plainly.** A large plan — the ~288 KB figure that
`PlanCacheCoordinator` quotes for a realistic one — goes from about 576 KB of quota
with its `.prev` copy to about 768 KB, roughly 15% of the budget. It does not put a
household plan anywhere near the limit, and the quota path is still handled and still
reported (`PlanCacheWriteFailure.QuotaExceeded`, which drops `.prev` and retries), but
it does move the ceiling: with both copies stored, a plan whose JSON could previously
reach ~2.5 MB now runs out of quota at ~1.9 MB. Nothing a household produces gets
close, and the bank-import store shares the same origin quota, so the headroom is not
infinite either way.

Compressing before encrypting would more than pay this back — plan JSON gzips to
roughly a tenth — and `CompressionStream` is available in every browser this app
supports. It was left out on purpose: it is a second failure mode on the save path
for a problem nobody has yet, and it belongs in its own change with its own tests.

## Testing

- `tests/CashFlowPlanner.BlazorWasm.Tests/WorkingCopyEncryptionTests.cs` — the wiring:
  round-trip, no plan content in storage, plaintext migration on both the read and the
  write path, the plaintext fallback, a lost device key, `.prev` rotation, quota
  reporting. Runs against a fake browser and a stand-in cipher, since neither Web
  Crypto nor IndexedDB exists in the xUnit process.
- `tools/working-copy-crypto-selftest.html` — the cryptography itself, in a browser.
  Serve the repository root over HTTP (`python3 -m http.server 8080`) and open it.
  Serve it from the repository root rather than the app's own origin: it creates and
  deletes a device key in whatever origin it runs on.
