// Encryption for CashFlow Planner plan files.
//
// WHY THIS IS HAND-ROLLED
// .NET 10 in WebAssembly has no symmetric cipher at all - AesGcm, AesCcm,
// ChaCha20Poly1305 and even Aes.Create are [UnsupportedOSPlatform("browser")].
// A key can be derived in C# but nothing can be encrypted with it, so the crypto
// has to live in JavaScript. This uses the browser's own Web Crypto and pulls in
// no third-party code.
//
// THE FORMAT  (see docs/ENCRYPTED-FILE-FORMAT.md, and tools/decrypt-plan.html
// which implements the same thing standalone)
//
//   { format, version, kdf{...}, wrappedKey{...}, payload{...} }
//
// Two levels, on purpose:
//
//   passphrase --PBKDF2--> KEK   (slow, once per session)
//   KEK        --AES-KW-ish-->  wraps a fresh random DEK on every save
//   DEK        --AES-GCM-->     encrypts the plan
//
// A fresh DEK per save means the (key, nonce) pair is never reused, which is the
// classic way AES-GCM is broken. It also keeps the expensive key derivation off
// the autosave path: deriving PBKDF2 at 600k iterations on every keystroke debounce
// would make the app unusable.
//
// The header is passed as AES-GCM additional authenticated data, so editing the
// iteration count or salt in the file makes decryption fail rather than silently
// weakening it.

const FORMAT = "cashflowplanner-encrypted";
const VERSION = 1;

// OWASP's 2026 floor for PBKDF2-HMAC-SHA256. Recorded in the file so a future
// build can raise it without being unable to read today's files.
const PBKDF2_ITERATIONS = 600000;
const SALT_BYTES = 16;
const IV_BYTES = 12;   // 96-bit, the only size AES-GCM should be used with

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

// The exact bytes bound into the ciphertext as AAD. Built from the header fields
// only, in a fixed order, so it can be reproduced independently by any decryptor.
function headerAad(header) {
    return new TextEncoder().encode(
        [
            header.format,
            header.version,
            header.kdf.algorithm,
            header.kdf.hash,
            header.kdf.iterations,
            header.kdf.salt
        ].join("|"));
}

async function deriveKek(passphrase, saltBytes, iterations) {
    const material = await crypto.subtle.importKey(
        "raw",
        new TextEncoder().encode(passphrase),
        "PBKDF2",
        false,
        ["deriveKey"]);

    return crypto.subtle.deriveKey(
        { name: "PBKDF2", salt: saltBytes, iterations, hash: "SHA-256" },
        material,
        { name: "AES-GCM", length: 256 },
        false,               // non-extractable: the KEK can never be read back out
        ["encrypt", "decrypt"]);
}

// Session state. Deliberately module-scoped and never persisted: the passphrase
// and derived key live only as long as the tab does.
let session = null;   // { kek, saltB64, iterations }

export function isUnlocked() {
    return session !== null;
}

export function lock() {
    session = null;
}

/**
 * Derive and cache the key-encryption key. Call once per session.
 * saltB64 is null for a brand-new file (a fresh salt is generated) or the salt
 * from an existing file's header when unlocking it.
 */
export async function unlock(passphrase, saltB64) {
    if (typeof passphrase !== "string" || passphrase.length === 0) {
        throw new Error("A passphrase is required.");
    }

    const salt = saltB64 ? fromBase64(saltB64) : crypto.getRandomValues(new Uint8Array(SALT_BYTES));
    const iterations = PBKDF2_ITERATIONS;
    const kek = await deriveKek(passphrase, salt, iterations);

    session = { kek, saltB64: toBase64(salt), iterations };

    return session.saltB64;
}

/** Encrypt plan JSON. Requires unlock() first. Returns the envelope as a JSON string. */
export async function encrypt(planJson) {
    if (!session) {
        throw new Error("Locked: unlock with a passphrase before encrypting.");
    }

    const header = {
        format: FORMAT,
        version: VERSION,
        kdf: {
            algorithm: "PBKDF2",
            hash: "SHA-256",
            iterations: session.iterations,
            salt: session.saltB64
        }
    };

    const aad = headerAad(header);

    // Fresh data-encryption key for every single save.
    const dek = await crypto.subtle.generateKey(
        { name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);

    const payloadIv = crypto.getRandomValues(new Uint8Array(IV_BYTES));
    const payloadCipher = await crypto.subtle.encrypt(
        { name: "AES-GCM", iv: payloadIv, additionalData: aad },
        dek,
        new TextEncoder().encode(planJson));

    const rawDek = new Uint8Array(await crypto.subtle.exportKey("raw", dek));
    const wrapIv = crypto.getRandomValues(new Uint8Array(IV_BYTES));
    const wrappedDek = await crypto.subtle.encrypt(
        { name: "AES-GCM", iv: wrapIv, additionalData: aad },
        session.kek,
        rawDek);

    rawDek.fill(0);   // best effort; JS gives no real guarantee

    return JSON.stringify({
        ...header,
        wrappedKey: {
            algorithm: "AES-256-GCM",
            iv: toBase64(wrapIv),
            ciphertext: toBase64(new Uint8Array(wrappedDek))
        },
        payload: {
            algorithm: "AES-256-GCM",
            iv: toBase64(payloadIv),
            ciphertext: toBase64(new Uint8Array(payloadCipher))
        }
    }, null, 2);
}

/** True if the text looks like one of our envelopes, without attempting to decrypt. */
export function isEncrypted(text) {
    try {
        const parsed = JSON.parse(text);
        return parsed && parsed.format === FORMAT;
    } catch {
        return false;
    }
}

/** Read the salt from an envelope so unlock() can derive the right key. */
export function readSalt(envelopeJson) {
    const env = JSON.parse(envelopeJson);
    if (env.format !== FORMAT) {
        throw new Error("This file is not an encrypted CashFlow Planner plan.");
    }
    if (env.version > VERSION) {
        throw new Error(
            `This file was written by a newer version of CashFlow Planner `
            + `(format ${env.version}, this build understands ${VERSION}). Update the app and try again.`);
    }
    return env.kdf.salt;
}

/** Decrypt an envelope. Requires unlock() with the salt from readSalt(). */
export async function decrypt(envelopeJson) {
    if (!session) {
        throw new Error("Locked: unlock with a passphrase before decrypting.");
    }

    const env = JSON.parse(envelopeJson);

    const aad = headerAad({
        format: env.format,
        version: env.version,
        kdf: env.kdf
    });

    let rawDek;
    try {
        rawDek = await crypto.subtle.decrypt(
            { name: "AES-GCM", iv: fromBase64(env.wrappedKey.iv), additionalData: aad },
            session.kek,
            fromBase64(env.wrappedKey.ciphertext));
    } catch {
        // AES-GCM authentication failed. Either the passphrase is wrong or the
        // header was edited. Both are indistinguishable by design, and both are
        // "we cannot read this", so say the likely thing.
        throw new Error("Wrong passphrase, or the file has been altered.");
    }

    const dek = await crypto.subtle.importKey(
        "raw", rawDek, { name: "AES-GCM" }, false, ["decrypt"]);

    let plain;
    try {
        plain = await crypto.subtle.decrypt(
            { name: "AES-GCM", iv: fromBase64(env.payload.iv), additionalData: aad },
            dek,
            fromBase64(env.payload.ciphertext));
    } catch {
        throw new Error("The encrypted plan is damaged and cannot be read.");
    }

    return new TextDecoder().decode(plain);
}
