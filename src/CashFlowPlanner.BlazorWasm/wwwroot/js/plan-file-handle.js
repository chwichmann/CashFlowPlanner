// Writing the plan straight to a file the user picked, and remembering which file
// that was between sessions.
//
// THE SHAPE OF THE API, because it decides the UX
//
// showSaveFilePicker() hands back a FileSystemFileHandle. Handles are
// structured-cloneable, so they survive in IndexedDB across sessions - but a
// restored handle comes back with permission "prompt", and requestPermission()
// needs a user gesture. So the honest flow is: pick once, then one click per
// session to reconnect.
//
// Two things remove even that click. Chrome 122+ offers "Allow on every visit"
// on the reconnect prompt, and an INSTALLED PWA persists the grant outright.
// That is why the manifest and service worker landed before this did.
//
// Critically: only the picker and requestPermission need a gesture. Writing to an
// already-granted handle does not, which is what makes unattended autosave
// possible at all.
//
// WRITE SAFETY
//
// createWritable() writes to a temporary swap file; the original is replaced only
// on close(). If close() never happens - crash, tab killed, power cut - the
// original file is untouched. That is a stronger guarantee than most desktop
// applications manage, and it is the reason this is safe to run on a debounce.
//
// SUPPORT, as of 2026
//
// Chrome/Edge/Opera desktop and Chrome Android 132+. Firefox and Safari have
// formal standards positions against it and are not expected to implement it, so
// everything here feature-detects and the app falls back to manual export.

const DB_NAME = "cashflowplanner";
const DB_VERSION = 1;
const STORE = "file-handles";
const HANDLE_KEY = "plan-file";

export function isSupported() {
    return typeof window !== "undefined" && "showSaveFilePicker" in window;
}

// --- IndexedDB: the handle must never leave the JS heap as anything but an opaque
// --- reference, so .NET only ever sees status strings and file names.

function openDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);
        request.onupgradeneeded = () => {
            if (!request.result.objectStoreNames.contains(STORE)) {
                request.result.createObjectStore(STORE);
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function idb(mode, fn) {
    const db = await openDb();
    try {
        return await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, mode);
            const result = fn(tx.objectStore(STORE));
            tx.oncomplete = () => resolve(result.result ?? null);
            tx.onerror = () => reject(tx.error);
            tx.onabort = () => reject(tx.error);
        });
    } finally {
        db.close();
    }
}

async function loadHandle() {
    try {
        return await idb("readonly", store => store.get(HANDLE_KEY));
    } catch {
        return null;
    }
}

async function storeHandle(handle) {
    await idb("readwrite", store => store.put(handle, HANDLE_KEY));
}

async function clearHandle() {
    await idb("readwrite", store => store.delete(HANDLE_KEY));
}

// --- permissions

async function permissionState(handle) {
    if (!handle?.queryPermission) {
        return "unsupported";
    }
    return handle.queryPermission({ mode: "readwrite" });
}

/**
 * What the UI needs to know without touching the disk or prompting:
 * "unsupported" | "unlinked" | "granted" | "needs-permission".
 */
export async function status() {
    if (!isSupported()) {
        return { state: "unsupported", fileName: null };
    }

    const handle = await loadHandle();

    if (!handle) {
        return { state: "unlinked", fileName: null };
    }

    const permission = await permissionState(handle);

    return {
        state: permission === "granted" ? "granted" : "needs-permission",
        fileName: handle.name ?? null
    };
}

/** Pick the file to keep the plan in. Requires a user gesture. */
export async function link(suggestedName) {
    if (!isSupported()) {
        throw new Error("This browser cannot write files directly. Use Export instead.");
    }

    let handle;
    try {
        handle = await window.showSaveFilePicker({
            suggestedName: suggestedName || "cashflow-plan.json",
            // `id` gives this picker its own remembered directory, so it reopens
            // where the user last saved rather than wherever the browser last was.
            id: "cashflowplanner-plan",
            types: [{
                description: "CashFlow Planner plan",
                accept: { "application/json": [".json", ".cfplan"] }
            }]
        });
    } catch (error) {
        if (error?.name === "AbortError") {
            return { state: "unlinked", fileName: null, cancelled: true };
        }
        throw error;
    }

    await storeHandle(handle);

    return { state: "granted", fileName: handle.name ?? null, cancelled: false };
}

/**
 * Re-grant access to the remembered file. Requires a user gesture; calling this
 * on startup throws SecurityError, which is why it is a button and not automatic.
 */
export async function reconnect() {
    const handle = await loadHandle();

    if (!handle) {
        return { state: "unlinked", fileName: null };
    }

    const permission = await handle.requestPermission({ mode: "readwrite" });

    return {
        state: permission === "granted" ? "granted" : "needs-permission",
        fileName: handle.name ?? null
    };
}

export async function unlink() {
    await clearHandle();
    return { state: "unlinked", fileName: null };
}

/**
 * Write text to the linked file. Returns a result rather than throwing, because
 * this runs on a debounce behind the user's back and a silent failure here is
 * exactly the class of bug this whole project has been removing.
 */
export async function write(text) {
    const handle = await loadHandle();

    if (!handle) {
        return { ok: false, reason: "unlinked", message: "No file is linked." };
    }

    const permission = await permissionState(handle);

    if (permission !== "granted") {
        // Do NOT call requestPermission here: without a user gesture it throws, and
        // an autosave is by definition not a gesture. The UI asks instead.
        return {
            ok: false,
            reason: "needs-permission",
            message: "Access to the linked file needs to be granted again."
        };
    }

    try {
        const writable = await handle.createWritable();
        await writable.write(text);
        // The original file is replaced only here. Until close() resolves, a crash
        // leaves the previous contents intact.
        await writable.close();

        return { ok: true, reason: "written", fileName: handle.name ?? null };
    } catch (error) {
        return {
            ok: false,
            reason: error?.name === "NotAllowedError" ? "needs-permission" : "failed",
            message: error?.message ?? String(error)
        };
    }
}

/** Read the linked file back, so the app can offer to open what it is writing to. */
export async function read() {
    const handle = await loadHandle();

    if (!handle) {
        return null;
    }

    if (await permissionState(handle) !== "granted") {
        return null;
    }

    const file = await handle.getFile();

    return { text: await file.text(), fileName: handle.name ?? null };
}
