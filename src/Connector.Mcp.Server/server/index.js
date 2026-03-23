#!/usr/bin/env node
/**
 * Entry point for the Revit Connector MCP server (Node)
 * - stdio JSON-RPC server for Claude
 * - Proxies to a local Revit add-in HTTP bridge
 *
 * Usage:
 *   node server.mjs --base=http://127.0.0.1:5578 --timeout=60000
 *   # or set REVIT_BRIDGE_BASE env var
 */

import readline from "node:readline";
import http from "node:http";
import { randomUUID } from "node:crypto";

// ---------- args/env ----------
const argv = process.argv.slice(2);
const arg = (k, def) => {
    const hit = argv.find(a => a.startsWith(`--${k}=`));
    return hit ? hit.split("=", 2)[1] : def;
};
const BASE = arg("base", process.env.REVIT_BRIDGE_BASE || "http://127.0.0.1:5578").replace(/\/+$/, "");
const TIMEOUT_MS = parseInt(arg("timeout", process.env.REVIT_BRIDGE_TIMEOUT || "60000"), 10);

// ---------- io helpers ----------
const rl = readline.createInterface({ input: process.stdin, crlfDelay: Infinity });
const log = (...a) => console.error("[MCP]", new Date().toTimeString().slice(0, 8), ...a);
const write = obj => { process.stdout.write(JSON.stringify(obj) + "\n"); };

// ---------- tiny http helpers ----------
function httpGet(path) {
    return new Promise((resolve, reject) => {
        const req = http.get(BASE + path, { timeout: TIMEOUT_MS }, res => {
            let body = ""; res.setEncoding("utf8");
            res.on("data", c => body += c);
            res.on("end", () => resolve({ status: res.statusCode ?? 0, body }));
        });
        req.on("timeout", () => { req.destroy(new Error("GET timeout")); });
        req.on("error", reject);
    });
}

function httpPost(path, json) {
    return new Promise((resolve, reject) => {
        const data = JSON.stringify(json ?? {});
        const req = http.request(BASE + path,
            {
                method: "POST", timeout: TIMEOUT_MS, headers: {
                    "Content-Type": "application/json",
                    "Content-Length": Buffer.byteLength(data)
                }
            },
            res => {
                let body = ""; res.setEncoding("utf8");
                res.on("data", c => body += c);
                res.on("end", () => resolve({ status: res.statusCode ?? 0, body }));
            }
        );
        req.on("timeout", () => { req.destroy(new Error("POST timeout")); });
        req.on("error", reject);
        req.write(data);
        req.end();
    });
}

// ---------- MCP loop ----------
let initialized = false;

rl.on("line", async line => {
    let req;
    try { req = JSON.parse(line); }
    catch (e) { log("bad json:", e?.message); return; }

    const id = req.id;                   // undefined for notifications
    const isNote = (id === undefined);
    const method = req.method || "";
    const params = req.params || {};

    try {
        // initialize
        if (method === "initialize") {
            if (isNote) { log("initialize notification ignored"); return; }
            if (initialized) { write({ jsonrpc: "2.0", id, error: { code: -32002, message: "Already initialized" } }); return; }
            initialized = true;
            write({
                jsonrpc: "2.0",
                id,
                result: {
                    protocolVersion: "2025-06-18",
                    capabilities: { tools: {} },
                    serverInfo: { name: "revit-bridge-js", version: "1.0.0" },
                    sessionId: randomUUID()
                }
            });
            return;
        }

        // tools/list → GET /tools
        if (method === "tools/list") {
            if (isNote) { log("tools/list notification ignored"); return; }
            if (!initialized) throw new Error("Server not initialized");
            const { body, status } = await httpGet("/tools");
            if (status >= 400) throw new Error(`GET /tools HTTP ${status}`);
            let tools = [];
            try { tools = JSON.parse(body); } catch (e) { log("failed to parse /tools response:", e?.message); }
            write({ jsonrpc: "2.0", id, result: { tools } });
            return;
        }

        // tools/call → POST /call { name, arguments }
        if (method === "tools/call") {
            if (isNote) { log("tools/call notification ignored"); return; }
            if (!initialized) throw new Error("Server not initialized");
            const name = params?.name ?? "";
            const args = params?.arguments ?? {};
            const { body, status } = await httpPost("/call", { name, arguments: args });
            if (status >= 400) { log("POST /call error body:", body); throw new Error(`POST /call HTTP ${status}`); }
            write({
                jsonrpc: "2.0",
                id,
                result: { content: [{ type: "text", text: body }] }
            });
            return;
        }

        // notifications/initialized — client sends after initialize, no response needed
        if (method === "notifications/initialized") { return; }

        // optional requests
        if (method === "setLogLevel") {
            if (!isNote) write({ jsonrpc: "2.0", id, result: {} });
            return;
        }

        // unknown method
        if (!isNote) write({ jsonrpc: "2.0", id, error: { code: -32601, message: `Method not found: ${method}` } });
    } catch (e) {
        if (!isNote) write({ jsonrpc: "2.0", id, error: { code: -32000, message: String(e?.message || e) } });
        log("handler error:", e?.stack || e);
    }
});

rl.on("close", () => { log("stdin closed; exiting"); process.exit(0); });

log("Revit Connector MCP server (JS) starting with BASE =", BASE);
