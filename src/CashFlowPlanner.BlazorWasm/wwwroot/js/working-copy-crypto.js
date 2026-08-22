// Encryption for the *browser working copy* of the plan - the autosaved scratch copy
// that lives in localStorage so a reload does not lose the last few edits.
//
// This is NOT the file format. See plan-crypto.js and docs/ENCRYPTED-FILE-FORMAT.md
// for that. The two solve different problems and deliberately share no key material.
//
// WHAT THIS PROTECTS, EXACTLY
//
//   Protects: the bytes at rest in the browser profile directory. localStorage is a
//   plain SQLite/LevelDB file on disk; before this change a lost laptop, a Time
//   Machine or OneDrive backup, a forensic image, or another account on a shared
//   machine handed over salaries, balances and debts to anything that can run
//   `strings` over the profile.
//
//   Does NOT protect against script running on this origin. An XSS payload, a
//   malicious extension with host access, or a devtools console can simply call this
//   module and get the plaintext back - the key is reachable by definition, because
//   the page has to be able to read its own working copy without asking the user for
//   anything. Nothing here should ever be described as XSS mitigation. If script runs
//   on the origin, it has already won.
//
//   Does NOT hide metadata: the storage key names, the value length, and the fact
//   that this is a CashFlow Planner working copy are all in the clear.
//
// WHY A DEVICE KEY AND NOT THE FILE PASSPHRASE
//
//   The working copy is a scratch copy scoped to this browser. It never travels: it
//   is not exported, not synced, not part of any backup the user makes deliberately.
//   Its entire job is to be restorable on the next page load, automatically, before
//   any UI exists to prompt with. Hanging that restore off the file passphrase would
//   mean a user who reloads the tab and cannot remember (or cannot be bothered to
//   retype) the passphrase loses their unexported edits - trading a real data-loss
//   protection for a weaker privacy one. The passphrase protects the file, which is
//   the artefact that actually leaves the machine.
//
// WHY THE KEY IS A NON-EXTRACTABLE CryptoKey IN IndexedDB
//
//   IndexedDB can structured-clone a CryptoKey directly. Storing the key that way
//   means the raw bytes are never materialised in JavaScript and never written to a
//   readable file: the profile contains a key handle the browser's key store knows
//   how to use, not 32 bytes someone can lift out with a hex editor and use to
//   decrypt the localStorage blob offline. Exporting the key to base64 and stashing
//   it in localStorage next to the ciphertext would be security theatre - it would
//   put the lock and the key in the same drawer, which is precisely the threat this
//   is meant to close.

const DB_NAME = "cashflowplanner-keys";
const DB_VERSION = 1;
const STORE_NAME = "device-keys";
const KEY_ID = "working-copy-v1";

// Storage envelope: "cfpwc1:" <base64 iv> ":" <base64 ciphertext+tag>
//
// Deliberately not JSON. The file format can afford a self-describing header because
// it has to survive a decade and an independent decryptor; this blob has to survive
// until the next save and competes for a ~5 MB localStorage quota, where every
// wrapper byte is paid for twice (once for the plan, once for the .prev copy).
const PREFIX = "cfpwc1:";
const IV_BYTES = 12;   // 96-bit, the only nonce size AES-GCM should be used with

// Bound into every ciphertext as additional authenticated data. It pins the blob to
// this format and version, so a value written by a future v2 cannot be silently
// decrypted under v1 rules. It deliberately does NOT include the storage key name:
// the .prev slot is filled by moving the current ciphertext across verbatim, and
// binding the slot name would force a re-encrypt on every rotation for no real gain
// (an attacker who can write to the profile can do far worse than swap two of the
// user's own plan copies).
const AAD = new TextEncoder().encode("cashflowplanner-working-copy-v1");

let keyPromise = null;
let warned = false;

function warnOnce(message, error) {
    if (warned) {
        return;
    }
    warned = true;
    console.warn(`[CashFlow Planner] ${message}`, error || "");
}

function toBase64(bytes) {
    let binary = "";
    const chunk = 0x8000;
    for (let i = 0; i < bytes.length; i += chunk) {
        binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
    }
    return btoa(binary);
}

function fromBase64(text) {
    const binary = atob(text);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes;
}

/** True if this browser can do the job at all. Private mode and locked-down profiles say no. */
export function isSupported() {
    return typeof indexedDB !== "undefined"
        && typeof crypto !== "undefined"
        && !!crypto.subtle;
}

function openDatabase() {
    return new Promise((resolve, reject) => {
        if (typeof indexedDB === "undefined") {
            reject(new Error("IndexedDB is not available."));
            return;
        }

        let request;
        try {
            request = indexedDB.open(DB_NAME, DB_VERSION);
        } catch (error) {
            // Firefox in permanent private browsing throws here rather than erroring async.
            reject(error);
            return;
        }

        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME);
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error || new Error("IndexedDB open failed."));
        request.onblocked = () => reject(new Error("IndexedDB is blocked by another tab."));
    });
}

function runRequest(store, makeRequest) {
    return new Promise((resolve, reject) => {
        const request = makeRequest(store);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error || new Error("IndexedDB request failed."));
    });
}

async function readStoredKey() {
    const db = await openDatabase();
    try {
        const tx = db.transaction(STORE_NAME, "readonly");
        return await runRequest(tx.objectStore(STORE_NAME), s => s.get(KEY_ID));
    } finally {
        db.close();
    }
}

async function createKey() {
    // extractable: false. The whole point - see the header comment. crypto.subtle
    // will refuse exportKey on this handle for the rest of its life, including for us.
    const key = await crypto.subtle.generateKey(
        { name: "AES-GCM", length: 256 },
        false,
        ["encrypt", "decrypt"]);

    const db = await openDatabase();
    try {
        const tx = db.transaction(STORE_NAME, "readwrite");
        const store = tx.objectStore(STORE_NAME);

        // Re-read inside the write transaction. Two tabs opening for the first time can
        // both find no key and both generate one; whoever commits first wins, and the
        // loser adopts that key instead of overwriting it - otherwise the first tab's
        // already-written ciphertext would become undecryptable.
        //
        // The key had to be generated *before* the transaction opened: an IndexedDB
        // transaction auto-commits as soon as the microtask queue drains with no
        // pending request, so awaiting generateKey inside it would close it under us.
        const existing = await runRequest(store, s => s.get(KEY_ID));
        if (existing) {
            return existing;
        }

        await runRequest(store, s => s.put(key, KEY_ID));
        return key;
    } finally {
        db.close();
    }
}

async function getKey() {
    const existing = await readStoredKey();
    if (existing) {
        return existing;
    }
    return createKey();
}

/**
 * The device key, or null if this browser will not give us one.
 * Cached per module instance; a failure is not cached, so a transient IndexedDB
 * hiccup does not condemn the tab to plaintext for the rest of the session.
 */
async function tryGetKey() {
    if (!isSupported()) {
        warnOnce(
            "This browser has no IndexedDB or Web Crypto, so the plan working copy in "
            + "localStorage will be stored unencrypted. Your data is safe; it is just readable "
            + "by anything that can read this browser profile.");
        return null;
    }

    if (!keyPromise) {
        keyPromise = getKey();
    }

    try {
        return await keyPromise;
    } catch (error) {
        keyPromise = null;
        warnOnce(
            "Could not open the device key store (private mode or blocked site data), so the "
            + "plan working copy in localStorage will be stored unencrypted. Your data is safe; "
            + "it is just readable by anything that can read this browser profile.",
            error);
        return null;
    }
}

/** True if this text is one of our storage envelopes. Cheap; does not decrypt. */
export function isEncrypted(text) {
    return typeof text === "string" && text.startsWith(PREFIX);
}

/**
 * Encrypt a working copy for storage.
 * Returns the plaintext unchanged if no device key can be obtained - see the comment
 * on the C# side: writing nothing would be silent data loss, which is strictly worse.
 */
export async function protect(text) {
    const key = await tryGetKey();
    if (!key) {
        return text;
    }

    // Fresh IV per write. The device key is long-lived by design, so this is the only
    // thing standing between us and GCM nonce reuse.
    const iv = crypto.getRandomValues(new Uint8Array(IV_BYTES));

    const cipher = await crypto.subtle.encrypt(
        { name: "AES-GCM", iv, additionalData: AAD },
        key,
        new TextEncoder().encode(text));

    return PREFIX + toBase64(iv) + ":" + toBase64(new Uint8Array(cipher));
}

/**
 * Decrypt a stored working copy.
 *
 * Plaintext that predates this feature is returned unchanged: a returning user has a
 * readable plan sitting in localStorage right now and it must survive the upgrade.
 *
 * Returns null when an envelope cannot be opened - the device key was cleared with
 * site data, or the value is damaged. That is "there is no usable working copy",
 * not an error: the plan file on disk is the source of truth.
 */
export async function unprotect(stored) {
    if (typeof stored !== "string" || stored.length === 0) {
        return null;
    }

    if (!isEncrypted(stored)) {
        return stored;
    }

    const body = stored.slice(PREFIX.length);
    const separator = body.indexOf(":");
    if (separator <= 0) {
        return null;
    }

    const key = await tryGetKey();
    if (!key) {
        return null;
    }

    try {
        const plain = await crypto.subtle.decrypt(
            {
                name: "AES-GCM",
                iv: fromBase64(body.slice(0, separator)),
                additionalData: AAD
            },
            key,
            fromBase64(body.slice(separator + 1)));

        return new TextDecoder().decode(plain);
    } catch (error) {
        console.warn(
            "[CashFlow Planner] The browser working copy could not be decrypted and was ignored. "
            + "Load your plan file to continue.",
            error);
        return null;
    }
}

/**
 * Throw the device key away, making every existing working-copy envelope permanently
 * unreadable. Used by tools/working-copy-crypto-selftest.html, and available for a
 * "forget this device" action. Clearing the working copy itself is a separate step.
 */
export async function deleteDeviceKey() {
    keyPromise = null;

    const db = await openDatabase();
    try {
        const tx = db.transaction(STORE_NAME, "readwrite");
        await runRequest(tx.objectStore(STORE_NAME), s => s.delete(KEY_ID));
    } finally {
        db.close();
    }
}
