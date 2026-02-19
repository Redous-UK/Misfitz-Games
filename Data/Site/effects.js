const el = (id) => document.getElementById(id);

// Make sure this matches your deployed routes:
const EFFECTS_BASE = "/api/effects";

async function api(path, opts = {}) {
    console.log("API:", path, opts);

    const fetchOpts = {
        credentials: "include",
        ...opts,
        headers: {
            ...(opts.headers || {}),
            ...(opts.body ? { "Content-Type": "application/json" } : {}),
        },
    };

    try {
        const res = await fetch(path, fetchOpts);
        console.log("FETCH RES", res.status, res.statusText, "for", path);

        const text = await res.text();
        let payload = null;

        if (text) {
            try { payload = JSON.parse(text); }
            catch { payload = { _nonJson: true, raw: text }; }
        }

        if (!res.ok) {
            console.error("API ERROR:", { path, status: res.status, statusText: res.statusText, payload, text });

            const msg =
                payload?.error ||
                payload?.message ||
                `HTTP ${res.status} ${res.statusText} @ ${path}`;

            const err = new Error(msg);
            err.status = res.status;
            err.path = path;
            err.payload = payload;
            err.raw = text;
            throw err;
        }

        return payload;
    } catch (err) {
        console.error("FETCH FAILED:", { path, name: err?.name, message: err?.message, err });
        throw err;
    }

const res = await fetch(path, fetchOpts);
const text = await res.text();

let payload = null;
if (text) {
    try { payload = JSON.parse(text); }
    catch { payload = { _nonJson: true, raw: text }; }
}

if (!res.ok) {
    console.error("API ERROR:", { path, status: res.status, statusText: res.statusText, payload, text });
    const msg = payload?.error || payload?.message || `HTTP ${res.status} ${res.statusText} @ ${path}`;
    const err = new Error(msg);
    err.status = res.status;
    err.path = path;
    err.payload = payload;
    err.raw = text;
    throw err;
}

return payload;
};

const pretty = (x) => {
    try { return JSON.stringify(x, null, 2); } catch { return String(x); }
};

function escapeHtml(s) {
    return String(s)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

// State
let devices = [];
let groups = [];
let effects = [];
let selectedEffectId = null;
let activity = [];
let showRaw = false;

function setPretty(msg) {
    el("prettyOut").textContent = msg ?? "";
}

function setRaw(obj) {
    el("out").textContent = typeof obj === "string" ? obj : pretty(obj);
}

function addActivity(type, title, meta, rawObj) {
    activity.unshift({
        id: crypto.randomUUID(),
        type, title, meta,
        at: new Date(),
        raw: rawObj ?? null
    });
    if (activity.length > 50) activity = activity.slice(0, 50);
    renderActivity();
}

function renderActivity() {
    const box = el("activity");
    if (!box) return;

    box.innerHTML = "";
    if (!activity.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No activity yet.";
        box.appendChild(empty);
        return;
    }

    for (const a of activity) {
        const item = document.createElement("div");
        item.className = "activityItem " + (a.type === "ok" ? "ok" : a.type === "bad" ? "bad" : "");
        const time = a.at.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

        item.innerHTML = `
      <div class="activityTitle">${escapeHtml(a.title)}</div>
      <div class="activityMeta">${escapeHtml(time)} • ${escapeHtml(a.meta || "")}</div>
    `;

        item.addEventListener("click", () => {
            if (a.raw) {
                setPretty(`${a.title} (${time})`);
                setRaw(a.raw);
                if (!showRaw) toggleRaw(true);
            }
        });

        box.appendChild(item);
    }
}

function toggleRaw(force) {
    showRaw = typeof force === "boolean" ? force : !showRaw;
    el("out").classList.toggle("hidden", !showRaw);
    el("btnToggleRaw").textContent = showRaw ? "Hide Raw" : "Show Raw";
}

// Tabs
function setTab(which) {
    for (const id of ["operator", "builder", "diag"]) {
        el("panel" + cap(id)).classList.toggle("hidden", id !== which);
        el("tab" + cap(id)).classList.toggle("active", id === which);
    }
}

function cap(s) { return s.charAt(0).toUpperCase() + s.slice(1); }

// Health
async function loadTuyaHealth() {
    try {
        await api("/api/health/tuya");
        const b = el("tuyaBadge");
        b.className = "badge ok";
        b.textContent = "Tuya: online";
        return true;
    } catch (e) {
        const b = el("tuyaBadge");
        b.className = "badge warn";
        b.textContent = "Tuya: offline";
        addActivity("bad", "Tuya offline", e.message, e.payload || { error: e.message });
        return false;
    }
}

// Catalogs
async function loadCatalogs() {
    const [d, g] = await Promise.all([
        api("/api/effects/devices"),
        api("/api/effects/groups"),
    ]);
    devices = d.devices || [];
    groups = g.groups || [];
    renderTargetPicker();
    renderDevicesMini();
    renderGroupsMini();
}

function renderDevicesMini() {
    const box = el("devicesList");
    if (!box) return;
    box.innerHTML = "";

    if (!devices.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No devices yet (add via API for now).";
        box.appendChild(empty);
        return;
    }

    for (const d of devices) {
        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${escapeHtml(d.name)}</div>
        <div class="chips">
          <span class="chip">${d.isEnabled ? "Enabled" : "Disabled"}</span>
          <span class="chip subtle">Cooldown ${d.cooldownSeconds}s</span>
          <span class="chip subtle">Max ${d.maxPulseSeconds}s</span>
        </div>
      </div>
    `;
        box.appendChild(row);
    }
}

function renderGroupsMini() {
    const box = el("groupsList");
    if (!box) return;
    box.innerHTML = "";

    if (!groups.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No groups yet.";
        box.appendChild(empty);
        return;
    }

    for (const g of groups) {
        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${escapeHtml(g.name)}</div>
      </div>
    `;
        box.appendChild(row);
    }
}

// Effects list
async function loadEffects() {
    const r = await api(EFFECTS_BASE);
    effects = r.effects || [];
    renderEffectsList();
    renderOperatorGrid();
}

function normAction(a) {
    // your API returns enum as int OR string depending on serialization
    if (typeof a === "number") return a;
    const s = String(a).toLowerCase();
    if (s.includes("pulse")) return 1;
    if (s.includes("on")) return 2;
    if (s.includes("off")) return 3;
    return 1;
}

function actionLabel(a) {
    const n = normAction(a);
    if (n === 1) return "Pulse";
    if (n === 2) return "On";
    if (n === 3) return "Off";
    return String(a);
}

function renderEffectsList() {
    const box = el("effectsList");
    if (!box) return;

    const q = (el("effectSearch")?.value || "").trim().toLowerCase();
    const list = q ? effects.filter(e => (e.name || "").toLowerCase().includes(q)) : effects;

    box.innerHTML = "";

    if (!list.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No effects found.";
        box.appendChild(empty);
        return;
    }

    for (const e of list) {
        const btn = document.createElement("button");
        btn.className = "listItem" + (e.id === selectedEffectId ? " selected" : "");
        btn.innerHTML = `
      <div class="grow">
        <div class="strong">${escapeHtml(e.name)}</div>
        <div class="chips">
          <span class="chip">${actionLabel(e.action)}</span>
          <span class="chip subtle">${e.durationSeconds}s</span>
          <span class="chip subtle">CD ${e.cooldownSeconds}s</span>
          <span class="chip ${e.isEnabled ? "" : "warn"}">${e.isEnabled ? "Enabled" : "Disabled"}</span>
        </div>
      </div>
    `;
        btn.addEventListener("click", () => selectEffect(e.id));
        box.appendChild(btn);
    }
}

// Operator grid
function renderOperatorGrid() {
    const grid = el("operatorGrid");
    if (!grid) return;

    const q = (el("operatorSearch")?.value || "").trim().toLowerCase();

    const list = effects
        .filter(e => e.isEnabled !== false)
        .filter(e => !q || (e.name || "").toLowerCase().includes(q));

    grid.innerHTML = "";

    if (!list.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No enabled effects.";
        grid.appendChild(empty);
        return;
    }

    for (const e of list) {
        const card = document.createElement("div");
        card.className = "opCard";
        card.innerHTML = `
      <div class="opTop">
        <div class="opName">${escapeHtml(e.name)}</div>
        <div class="chips">
          <span class="chip">${actionLabel(e.action)}</span>
          <span class="chip subtle">${e.durationSeconds}s</span>
          <span class="chip subtle">CD ${e.cooldownSeconds}s</span>
        </div>
      </div>
      <button class="btn primary big opRun">▶ Run</button>
    `;

        card.querySelector(".opRun").addEventListener("click", async () => {
            try {
                await runEffectById(e.id, e.name);
            } catch (err) {
                // already logged in run
            }
        });

        grid.appendChild(card);
    }
}

// Effect details
function renderTargetPicker() {
    const type = Number(el("targetType").value);
    const picker = el("targetPicker");
    picker.innerHTML = "";

    const items = type === 1 ? devices : groups;
    for (const it of items) {
        const opt = document.createElement("option");
        opt.value = it.id;
        opt.textContent =
            type === 1
                ? `${it.name}${it.isEnabled ? "" : " (disabled)"}`
                : `${it.name}`;
        picker.appendChild(opt);
    }
}

function renderEffectHeader(effect) {
    if (!effect) {
        el("effectTitle").textContent = "Select an effect";
        el("effectSub").textContent = "—";
        el("effectChips").innerHTML = "";
        el("btnRun").disabled = true;
        return;
    }

    el("effectTitle").textContent = effect.name;
    el("effectSub").textContent = `Action ${actionLabel(effect.action)} • ${effect.durationSeconds}s • Cooldown ${effect.cooldownSeconds}s`;
    el("btnRun").disabled = false;

    el("effectChips").innerHTML = `
    <div class="chips">
      <span class="chip">${effect.isEnabled ? "Enabled" : "Disabled"}</span>
      <span class="chip subtle">Created ${new Date(effect.createdUtc).toLocaleDateString()}</span>
    </div>
  `;
}

function renderTargets(effect) {
    const list = el("targetsList");
    list.innerHTML = "";

    const targets = effect?.targets || [];
    if (!targets.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No targets yet. Add one below.";
        list.appendChild(empty);
        return;
    }

    for (const t of targets) {
        const name = t.targetType === 1
            ? (t.deviceName || t.deviceId)
            : (t.groupName || t.groupId);

        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${escapeHtml(name || "(unknown)")}</div>
        <div class="chips">
          <span class="chip">${t.targetType === 1 ? "Device" : "Group"}</span>
          ${t.durationSecondsOverride ? `<span class="chip warn">Override ${t.durationSecondsOverride}s</span>` : `<span class="chip subtle">No override</span>`}
          <span class="chip subtle">Sort ${t.sortOrder}</span>
        </div>
      </div>
      <button class="btn" data-del="${t.id}">Remove</button>
    `;

        row.querySelector("[data-del]").addEventListener("click", async () => {
            try {
                setPretty("Removing target…");
                await api(`/api/effects/targets/${t.id}`, { method: "DELETE" });
                await selectEffect(selectedEffectId);
                addActivity("ok", "Target removed", name, { targetId: t.id });
            } catch (e) {
                addActivity("bad", "Remove failed", e.message, e.payload || { error: e.message });
                setPretty(e.message);
                setRaw(e.payload || { error: e.message });
                toggleRaw(true);
            }
        });

        list.appendChild(row);
    }
}

async function selectEffect(effectId) {
    selectedEffectId = effectId;
    renderEffectsList();

    try {
        const r = await api(`/api/effects/${effectId}`);
        renderEffectHeader(r.effect);
        renderTargets(r.effect);
        setPretty("Ready.");
    } catch (e) {
        setPretty(e.message);
        setRaw(e.payload || { error: e.message });
        toggleRaw(true);
    }
}

// Builder actions
async function createEffect() {
    const name = el("newEffectName").value.trim();
    const action = Number(el("newEffectAction").value);
    const durationSeconds = Number(el("newEffectDuration").value || 2);
    const cooldownSeconds = Number(el("newEffectCooldown").value || 2);

    if (!name) {
        setPretty("Name required");
        return;
    }

    setPretty("Creating effect…");
    const r = await api(EFFECTS_BASE, {
        method: "POST",
        body: JSON.stringify({ name, action, durationSeconds, cooldownSeconds }),
    });

    await loadEffects();
    el("newEffectName").value = "";
    if (r.effectId) await selectEffect(r.effectId);

    addActivity("ok", "Effect created", name, r);
    setPretty("Effect created.");
}

async function addTarget() {
    if (!selectedEffectId) return;

    const targetType = Number(el("targetType").value);
    const pickId = el("targetPicker").value;
    const durationRaw = el("targetDurationOverride").value.trim();
    const durationSecondsOverride = durationRaw ? Number(durationRaw) : null;
    const sortOrder = Number(el("targetSort").value || 0);

    const body = {
        targetType,
        deviceId: targetType === 1 ? pickId : null,
        groupId: targetType === 2 ? pickId : null,
        durationSecondsOverride,
        sortOrder,
    };

    setPretty("Adding target…");
    const r = await api(`/api/effects/${selectedEffectId}/targets`, {
        method: "POST",
        body: JSON.stringify(body),
    });

    await selectEffect(selectedEffectId);
    addActivity("ok", "Target added", `Effect ${selectedEffectId}`, r);
    setPretty("Target added.");
}

// Run (shared)
async function runEffectById(effectId, effectName) {
    setPretty("Running…");
    try {
        const r = await api(`/api/effects/${effectId}/run`, { method: "POST" });
        addActivity("ok", "Effect ran", effectName || effectId, r);
        setPretty("Effect ran.");
        return r;
    } catch (e) {
        addActivity("bad", "Run failed", effectName || effectId, e.payload || { error: e.message });
        setPretty(e.message);
        setRaw(e.payload || { error: e.message });
        toggleRaw(true);
        throw e;
    }
}

async function runSelected() {
    if (!selectedEffectId) return;
    const name = effects.find(x => x.id === selectedEffectId)?.name || selectedEffectId;
    await runEffectById(selectedEffectId, name);
}

// Diagnostics
async function diagCheckTuya() {
    el("diagOut").textContent = "Checking…";
    try {
        const r = await api("/api/health/tuya");
        el("diagOut").textContent = pretty(r);
    } catch (e) {
        el("diagOut").textContent = pretty(e.payload || { error: e.message });
    }
}

async function refreshAll() {
    try { await loadTuyaHealth(); } catch { }
    await loadCatalogs();
    await loadEffects();
    renderActivity();
}

// Boot
document.addEventListener("DOMContentLoaded", async () => {
    // Tabs
    el("tabOperator").addEventListener("click", () => setTab("operator"));
    el("tabBuilder").addEventListener("click", () => setTab("builder"));
    el("tabDiag").addEventListener("click", () => setTab("diag"));

    // Buttons
    el("btnNewEffect").addEventListener("click", () => createEffect().catch(() => { }));
    el("btnAddTarget").addEventListener("click", () => addTarget().catch(() => { }));
    el("btnRun").addEventListener("click", () => runSelected().catch(() => { }));
    el("btnToggleRaw").addEventListener("click", () => toggleRaw());
    el("btnClearActivity").addEventListener("click", () => { activity = []; renderActivity(); });

    // Searches
    el("effectSearch").addEventListener("input", () => renderEffectsList());
    el("operatorSearch").addEventListener("input", () => renderOperatorGrid());
    el("targetType").addEventListener("change", () => renderTargetPicker());

    // Diagnostics buttons
    el("btnRefreshAll").addEventListener("click", () => refreshAll().catch(() => { }));
    el("btnHealthTuya").addEventListener("click", () => diagCheckTuya().catch(() => { }));

    // Initial load
    setPretty("Loading…");
    await loadTuyaHealth(); // never blocks UI
    await loadCatalogs();
    await loadEffects();
    renderActivity();
    setPretty("Ready.");

    // Default tab
    setTab("operator");
});