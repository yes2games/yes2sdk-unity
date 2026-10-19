#!/usr/bin/env node
import { spawn, spawnSync } from "node:child_process";
import { createReadStream, existsSync, mkdtempSync, rmSync, statSync } from "node:fs";
import { createServer } from "node:http";
import { tmpdir } from "node:os";
import { extname, join, resolve, sep } from "node:path";

const MARKER = "YES2SDK_CONTROL_PLANE_PROOF";
const JOURNAL_POINTER = "/Yes2SDK/control-plane/v1/cp-sandbox/development/lkg-current";
const PROOF_TIMEOUT_MS = 180_000;
const FLUSH_TIMEOUT_MS = 30_000;
const TYPES = {
    ".html": "text/html",
    ".js": "text/javascript",
    ".wasm": "application/wasm",
    ".json": "application/json",
    ".css": "text/css",
    ".png": "image/png",
    ".ico": "image/x-icon",
};

const root = resolve(process.argv[2] ?? fail("usage: node ci~/run-webgl-proof.mjs WEBGL_BUILD_DIR"));
if (!existsSync(join(root, "index.html"))) {
    fail(`no index.html under ${root}`);
}

function fail(message) {
    console.error(`::error title=WebGL control plane proof failed::${message}`);
    process.exit(1);
}

function findChrome() {
    for (const name of [process.env.CHROME_BIN, "google-chrome", "google-chrome-stable", "chromium", "chromium-browser"]) {
        if (name && spawnSync("which", [name]).status === 0) {
            return name;
        }
    }
    fail("no Chrome or Chromium on PATH; set CHROME_BIN");
}

const server = createServer((request, response) => {
    const pathname = decodeURIComponent(new URL(request.url, "http://localhost").pathname);
    const path = resolve(join(root, pathname === "/" ? "index.html" : pathname));
    if (!path.startsWith(root + sep) || !existsSync(path) || !statSync(path).isFile()) {
        response.writeHead(404).end();
        return;
    }
    response.writeHead(200, { "Content-Type": TYPES[extname(path)] ?? "application/octet-stream" });
    createReadStream(path).pipe(response);
});
await new Promise((ready) => server.listen(0, "127.0.0.1", ready));
const origin = `http://127.0.0.1:${server.address().port}`;

const profile = mkdtempSync(join(tmpdir(), "yes2sdk-webgl-proof-"));
const browser = spawn(
    findChrome(),
    [
        "--headless=new",
        "--no-sandbox",
        "--no-first-run",
        "--no-default-browser-check",
        "--use-angle=swiftshader",
        "--enable-unsafe-swiftshader",
        "--remote-debugging-port=0",
        `--user-data-dir=${profile}`,
        "about:blank",
    ],
    { stdio: ["ignore", "ignore", "pipe"] }
);

let exitCode = 1;
try {
    const endpoint = await new Promise((found, failed) => {
        let stderr = "";
        browser.stderr.on("data", (chunk) => {
            stderr += chunk;
            const match = /DevTools listening on (ws:\/\/\S+)/.exec(stderr);
            if (match) {
                found(match[1]);
            }
        });
        browser.on("exit", (code) => failed(new Error(`Chrome exited with ${code} before DevTools listened:\n${stderr}`)));
    });

    const socket = new WebSocket(endpoint);
    await new Promise((open, failed) => {
        socket.onopen = open;
        socket.onerror = () => failed(new Error(`cannot connect to ${endpoint}`));
    });

    let nextId = 0;
    const pending = new Map();
    const listeners = new Set();
    socket.onmessage = (event) => {
        const message = JSON.parse(event.data);
        if (message.id === undefined) {
            listeners.forEach((listener) => listener(message));
        } else if (message.error) {
            pending.get(message.id).failed(new Error(`${message.error.message} (${message.error.code})`));
        } else {
            pending.get(message.id).done(message.result);
        }
    };
    const send = (method, params = {}, sessionId = undefined) =>
        new Promise((done, failed) => {
            const id = ++nextId;
            pending.set(id, { done, failed });
            socket.send(JSON.stringify({ id, method, params, sessionId }));
        });

    const { targetId } = await send("Target.createTarget", { url: "about:blank" });
    const { sessionId } = await send("Target.attachToTarget", { targetId, flatten: true });
    await send("Runtime.enable", {}, sessionId);
    await send("Page.enable", {}, sessionId);

    const proof = (url) =>
        new Promise((done, failed) => {
            const timer = setTimeout(() => {
                listeners.delete(listener);
                failed(new Error(`no ${MARKER} line from ${url} within ${PROOF_TIMEOUT_MS / 1000} s`));
            }, PROOF_TIMEOUT_MS);
            const listener = (message) => {
                if (message.sessionId !== sessionId) {
                    return;
                }
                if (message.method === "Runtime.exceptionThrown") {
                    console.log(`[page exception] ${message.params.exceptionDetails.exception?.description ?? message.params.exceptionDetails.text}`);
                    return;
                }
                if (message.method !== "Runtime.consoleAPICalled") {
                    return;
                }
                const text = message.params.args.map((arg) => arg.value ?? arg.description ?? "").join(" ");
                console.log(`[page ${message.params.type}] ${text}`);
                const at = text.indexOf(MARKER);
                if (at >= 0) {
                    clearTimeout(timer);
                    listeners.delete(listener);
                    done(JSON.parse(text.slice(at + MARKER.length).split("\n")[0]));
                }
            };
            listeners.add(listener);
            send("Page.navigate", { url }, sessionId).catch(failed);
        });

    const expectPass = (result, mode) => {
        if (result.result !== "pass" || result.mode !== mode || result.platform !== "WebGLPlayer") {
            throw new Error(`${mode} proof: ${JSON.stringify(result)}`);
        }
        console.log(`${mode} proof passed: ${JSON.stringify(result)}`);
    };

    expectPass(await proof(`${origin}/index.html`), "full");

    const flushed = `new Promise((answer) => {
        const open = indexedDB.open("/idbfs");
        open.onerror = () => answer(false);
        open.onsuccess = () => {
            const db = open.result;
            if (!db.objectStoreNames.contains("FILE_DATA")) { db.close(); answer(false); return; }
            const keys = db.transaction("FILE_DATA").objectStore("FILE_DATA").getAllKeys();
            keys.onerror = () => { db.close(); answer(false); };
            keys.onsuccess = () => { db.close(); answer(keys.result.some((key) => String(key).endsWith(${JSON.stringify(JOURNAL_POINTER)}))); };
        };
    })`;
    const deadline = Date.now() + FLUSH_TIMEOUT_MS;
    for (;;) {
        const { result } = await send("Runtime.evaluate", { expression: flushed, awaitPromise: true, returnByValue: true }, sessionId);
        if (result.value === true) {
            console.log(`IndexedDB /idbfs holds ${JOURNAL_POINTER}`);
            break;
        }
        if (Date.now() > deadline) {
            throw new Error(`the LKG pointer never reached IndexedDB /idbfs within ${FLUSH_TIMEOUT_MS / 1000} s`);
        }
        await new Promise((wait) => setTimeout(wait, 250));
    }

    expectPass(await proof(`${origin}/index.html?proof=reload`), "reload");
    console.log("WebGL control plane proof passed: LKG written, flushed to IndexedDB, and activated after a reload");
    exitCode = 0;
    socket.close();
} catch (error) {
    console.error(`::error title=WebGL control plane proof failed::${error.message}`);
} finally {
    browser.kill("SIGKILL");
    server.close();
    rmSync(profile, { recursive: true, force: true, maxRetries: 5 });
}
process.exit(exitCode);
